using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using LmuOverlay.Domain;

namespace LmuOverlay.Core;

public sealed record TelemetryRecordingHeader(
    int SchemaVersion,
    string Producer,
    DateTimeOffset TimelineOriginUtc,
    bool Anonymized);

public sealed record TelemetryRecordingFrame(
    long Sequence,
    long OffsetMicroseconds,
    LmuTelemetrySnapshot Snapshot);

public sealed record TelemetryRecording(
    TelemetryRecordingHeader Header,
    IReadOnlyList<TelemetryRecordingFrame> Frames);

public sealed class TelemetryRecordingWriter : IAsyncDisposable
{
    public const int CurrentSchemaVersion = 1;
    private const string ProducerName = "LMU Overlay";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly StreamWriter _writer;
    private readonly Channel<TelemetryRecordingFrame> _frames;
    private readonly TelemetryRecordingAnonymizer _anonymizer = new();
    private readonly Task _pump;
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private long _sequence;
    private long _droppedFrames;
    private bool _disposed;

    private TelemetryRecordingWriter(
        StreamWriter writer,
        Channel<TelemetryRecordingFrame> frames)
    {
        _writer = writer;
        _frames = frames;
        _pump = PumpAsync();
    }

    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

    public static async Task<TelemetryRecordingWriter> CreateAsync(
        string path,
        int capacity = 512,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (capacity < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var stream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            64 * 1024);
        var header = new TelemetryRecordingHeader(
            CurrentSchemaVersion,
            ProducerName,
            DateTimeOffset.UnixEpoch,
            Anonymized: true);
        await writer.WriteLineAsync(
            JsonSerializer.Serialize(
                new RecordingEnvelope<TelemetryRecordingHeader>("header", header),
                JsonOptions).AsMemory(),
            cancellationToken);
        await writer.FlushAsync(cancellationToken);

        var frames = Channel.CreateBounded<TelemetryRecordingFrame>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false,
            });
        return new TelemetryRecordingWriter(writer, frames);
    }

    public bool TryRecord(LmuTelemetrySnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);
        var elapsed = Stopwatch.GetTimestamp() - _startedTimestamp;
        var frame = new TelemetryRecordingFrame(
            Interlocked.Increment(ref _sequence),
            Math.Max(0, elapsed * 1_000_000 / Stopwatch.Frequency),
            snapshot);
        if (_frames.Writer.TryWrite(frame))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedFrames);
        return false;
    }

    private async Task PumpAsync()
    {
        var pendingFlush = 0;
        await foreach (var frame in _frames.Reader.ReadAllAsync())
        {
            var sanitized = frame with
            {
                Snapshot = _anonymizer.Anonymize(frame.Snapshot, frame.OffsetMicroseconds),
            };
            await _writer.WriteLineAsync(JsonSerializer.Serialize(
                new RecordingEnvelope<TelemetryRecordingFrame>("frame", sanitized),
                JsonOptions));
            if (++pendingFlush >= 64)
            {
                await _writer.FlushAsync();
                pendingFlush = 0;
            }
        }

        await _writer.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frames.Writer.TryComplete();
        try
        {
            await _pump;
        }
        finally
        {
            await _writer.DisposeAsync();
        }
    }

    internal static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RecordingEnvelope<T>(string Type, T Value);
}

public static class TelemetryRecordingReader
{
    public const int DefaultMaximumFrames = 2_000_000;
    private const int MaximumLineCharacters = 16 * 1024 * 1024;
    private const long MaximumOffsetMicroseconds = 7L * 24 * 60 * 60 * 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions =
        TelemetryRecordingWriter.CreateJsonOptions();

    public static async Task<TelemetryRecording> ReadAsync(
        string path,
        CancellationToken cancellationToken = default,
        int maximumFrames = DefaultMaximumFrames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));
        }

        await using var stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        TelemetryRecordingHeader? header = null;
        var frames = new List<TelemetryRecordingFrame>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Length > MaximumLineCharacters)
            {
                throw new InvalidDataException("Recording entry exceeds the size limit.");
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            var value = root.GetProperty("value");
            if (type == "header")
            {
                if (header is not null)
                {
                    throw new InvalidDataException("Recording contains more than one header.");
                }

                header = value.Deserialize<TelemetryRecordingHeader>(JsonOptions)
                    ?? throw new InvalidDataException("Recording header is invalid.");
                if (header.SchemaVersion != TelemetryRecordingWriter.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported recording schema {header.SchemaVersion}.");
                }
            }
            else if (type == "frame")
            {
                if (header is null)
                {
                    throw new InvalidDataException("Recording frame precedes its header.");
                }

                var frame = value.Deserialize<TelemetryRecordingFrame>(JsonOptions)
                    ?? throw new InvalidDataException("Recording frame is invalid.");
                if (frame.OffsetMicroseconds < 0 ||
                    frame.OffsetMicroseconds > MaximumOffsetMicroseconds)
                {
                    throw new InvalidDataException("Recording frame offset is outside the supported range.");
                }
                if (frames.Count > 0 &&
                    (frame.Sequence <= frames[^1].Sequence ||
                     frame.OffsetMicroseconds < frames[^1].OffsetMicroseconds))
                {
                    throw new InvalidDataException("Recording frames are not monotonic.");
                }

                frames.Add(frame);
                if (frames.Count > maximumFrames)
                {
                    throw new InvalidDataException("Recording exceeds the frame limit.");
                }
            }
            else
            {
                throw new InvalidDataException($"Unknown recording entry '{type}'.");
            }
        }

        return new TelemetryRecording(
            header ?? throw new InvalidDataException("Recording header is missing."),
            frames);
    }
}

internal sealed class TelemetryRecordingAnonymizer
{
    private readonly Dictionary<int, int> _vehicleIds = new();

    public LmuTelemetrySnapshot Anonymize(
        LmuTelemetrySnapshot snapshot,
        long offsetMicroseconds)
    {
        var playerSourceId = snapshot.Player?.VehicleId ??
            snapshot.Standings.FirstOrDefault(standing => standing.IsPlayer)?.VehicleId;
        var playerId = playerSourceId is null
            ? (int?)null
            : MapVehicleId(playerSourceId.Value);
        var standings = snapshot.Standings.Select(standing =>
        {
            var id = MapVehicleId(standing.VehicleId);
            return standing with
            {
                VehicleId = id,
                DriverName = $"Driver {id:00}",
                VehicleName = $"Car {id:00}",
            };
        }).ToArray();
        var session = snapshot.Session is null
            ? null
            : snapshot.Session with
            {
                PlayerName = playerId is { } id ? $"Driver {id:00}" : "Driver 00",
            };
        var player = snapshot.Player is null
            ? null
            : snapshot.Player with
            {
                VehicleId = playerId!.Value,
                VehicleName = $"Car {playerId.Value:00}",
            };

        return snapshot with
        {
            Session = session,
            Player = player,
            Standings = standings,
            CapturedAt = DateTimeOffset.UnixEpoch.AddTicks(offsetMicroseconds * 10),
            Detail = string.Empty,
        };
    }

    private int MapVehicleId(int sourceId)
    {
        if (_vehicleIds.TryGetValue(sourceId, out var mapped))
        {
            return mapped;
        }

        mapped = _vehicleIds.Count + 1;
        _vehicleIds.Add(sourceId, mapped);
        return mapped;
    }
}
