using System.Globalization;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

/// <summary>
/// Reads the same read-only standings history endpoint used by LMU's own
/// Live Timing UI. The endpoint contains the complete sector history required
/// for a true theoretical optimal; shared memory exposes only two best-sector
/// accumulators and cannot reconstruct an earlier best sector three.
/// </summary>
public sealed class OfficialTimingOptimalProvider : IDisposable
{
    public const int SupportedHistorySchemaVersion = 1;
    private readonly OfficialTimingOptimalOptions _options;
    private readonly HttpClient _client;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private TimingTarget? _target;
    private TimingValue? _value;
    private bool _hasSessionIdentity;
    private string _lastTrackName = string.Empty;
    private int _lastSessionCode;
    private int _lastVehicleId;
    private double _lastSessionElapsedTime;
    private long _sessionGeneration;
    private int _consecutiveFailures;
    private long _nextAttemptAt;
    private string _lastError = string.Empty;
    private DateTimeOffset? _lastSuccessAt;

    public OfficialTimingOptimalProvider(OfficialTimingOptimalOptions? options = null)
    {
        _options = (options ?? new()).Sanitize();
        _client = new HttpClient
        {
            BaseAddress = _options.BaseAddress,
            Timeout = _options.Timeout,
        };
        _worker = _options.Enabled ? Task.Run(PollAsync) : Task.CompletedTask;
    }

    public void Update(LmuTelemetrySnapshot snapshot)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (snapshot.Player is not { } player ||
            snapshot.Session is not { } session ||
            session.GamePhase == LmuGamePhase.SessionOver)
        {
            Volatile.Write(ref _target, null);
            Volatile.Write(ref _value, null);
            return;
        }

        var isNewSession = IsNewSession(
            _hasSessionIdentity,
            _lastTrackName,
            _lastSessionCode,
            _lastVehicleId,
            _lastSessionElapsedTime,
            session.TrackName,
            session.SessionCode,
            player.VehicleId,
            session.CurrentElapsedTime);
        if (isNewSession)
        {
            _sessionGeneration++;
        }

        _hasSessionIdentity = true;
        _lastTrackName = session.TrackName;
        _lastSessionCode = session.SessionCode;
        _lastVehicleId = player.VehicleId;
        _lastSessionElapsedTime = session.CurrentElapsedTime;

        var previous = Volatile.Read(ref _target);
        var sessionKey =
            $"{session.TrackName}\u001f{session.SessionCode}\u001f" +
            $"{player.VehicleId}\u001f{_sessionGeneration}";
        if (previous?.SessionKey == sessionKey)
        {
            return;
        }

        var target = new TimingTarget(
            sessionKey,
            session.TrackName,
            session.SessionCode,
            player.VehicleId,
            discardExistingHistory: isNewSession);
        Volatile.Write(ref _value, null);
        Volatile.Write(ref _target, target);
    }

    public OfficialTimingOptimalDiagnostics Diagnostics => new(
        _options.Enabled,
        Volatile.Read(ref _consecutiveFailures),
        Volatile.Read(ref _nextAttemptAt) > Stopwatch.GetTimestamp(),
        _lastSuccessAt,
        Volatile.Read(ref _lastError));

    public double GetOptimal(LmuTelemetrySnapshot snapshot)
    {
        var target = Volatile.Read(ref _target);
        var value = Volatile.Read(ref _value);
        return target is not null && value?.SessionKey == target.SessionKey
            ? value.OptimalLapSeconds
            : 0;
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false))
            {
                var target = Volatile.Read(ref _target);
                if (target is null)
                {
                    continue;
                }
                if (Volatile.Read(ref _nextAttemptAt) > Stopwatch.GetTimestamp())
                {
                    continue;
                }

                try
                {
                    using var response = await _client.GetAsync(
                        "rest/watch/standings/history",
                        HttpCompletionOption.ResponseHeadersRead,
                        _cancellation.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(
                        _cancellation.Token).ConfigureAwait(false);
                    using var document = await JsonDocument.ParseAsync(
                        stream,
                        cancellationToken: _cancellation.Token).ConfigureAwait(false);
                    if (!HasSupportedSchema(document.RootElement, target.VehicleId))
                    {
                        RecordFailure("Unsupported or incomplete LMU timing history schema.");
                        continue;
                    }
                    if (target.DiscardExistingHistory &&
                        target.ExcludedLapSignatures is null)
                    {
                        target.ExcludedLapSignatures = CaptureLapSignatures(
                            document.RootElement,
                            target.VehicleId);
                        RecordSuccess();
                        continue;
                    }

                    var optimal = ParseOptimal(
                        document.RootElement,
                        target.VehicleId,
                        target.ExcludedLapSignatures);
                    if (optimal > 0 && Volatile.Read(ref _target)?.SessionKey == target.SessionKey)
                    {
                        Volatile.Write(ref _value, new TimingValue(target.SessionKey, optimal));
                    }
                    RecordSuccess();
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or JsonException)
                {
                    RecordFailure(exception.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static double ParseOptimal(JsonElement history, int vehicleId)
        => ParseOptimal(history, vehicleId, excludedLapSignatures: null);

    public static bool HasSupportedSchema(JsonElement history, int vehicleId)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var lap in laps.EnumerateArray())
        {
            if (lap.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (HasNumber(lap, "sectorTime1") &&
                HasNumber(lap, "sectorTime2") &&
                HasNumber(lap, "lapTime"))
            {
                return true;
            }
        }

        // An empty history is valid at the beginning of a session.
        return laps.GetArrayLength() == 0;
    }

    public static bool IsNewSession(
        bool hasPreviousSession,
        string previousTrackName,
        int previousSessionCode,
        int previousVehicleId,
        double previousElapsedTime,
        string trackName,
        int sessionCode,
        int vehicleId,
        double elapsedTime)
    {
        if (!hasPreviousSession)
        {
            return false;
        }

        var sameIdentity = previousSessionCode == sessionCode &&
            previousVehicleId == vehicleId &&
            string.Equals(previousTrackName, trackName, StringComparison.Ordinal);
        return !sameIdentity || elapsedTime + 1 < previousElapsedTime;
    }

    public static double ParseOptimal(
        JsonElement history,
        int vehicleId,
        IReadOnlySet<string>? excludedLapSignatures)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var best1 = double.PositiveInfinity;
        var best2 = double.PositiveInfinity;
        var best3 = double.PositiveInfinity;
        foreach (var lap in laps.EnumerateArray())
        {
            if (excludedLapSignatures?.Contains(lap.GetRawText()) == true)
            {
                continue;
            }
            if (IsExplicitlyInvalid(lap))
            {
                continue;
            }
            var sector1 = Number(lap, "sectorTime1");
            var sector2Cumulative = Number(lap, "sectorTime2");
            var lapTime = Number(lap, "lapTime");
            if (Valid(sector1)) best1 = Math.Min(best1, sector1);
            var sector2 = sector2Cumulative - sector1;
            if (Valid(sector1) && Valid(sector2Cumulative) && Valid(sector2))
            {
                best2 = Math.Min(best2, sector2);
            }
            var sector3 = lapTime - sector2Cumulative;
            if (Valid(sector2Cumulative) && Valid(lapTime) && Valid(sector3))
            {
                best3 = Math.Min(best3, sector3);
            }
        }

        return double.IsFinite(best1) && double.IsFinite(best2) && double.IsFinite(best3)
            ? best1 + best2 + best3
            : 0;
    }

    private static HashSet<string> CaptureLapSignatures(
        JsonElement history,
        int vehicleId)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return laps.EnumerateArray()
            .Select(lap => lap.GetRawText())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsExplicitlyInvalid(JsonElement lap)
    {
        foreach (var property in lap.EnumerateObject())
        {
            if ((property.Name.Equals("valid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isLapValid", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.False)
            {
                return true;
            }

            if ((property.Name.Equals("invalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalidated", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetVehicleHistory(
        JsonElement history,
        int vehicleId,
        out JsonElement laps)
    {
        var key = vehicleId.ToString(CultureInfo.InvariantCulture);
        if (history.TryGetProperty(key, out laps))
        {
            return true;
        }

        laps = default;
        return false;
    }

    private static double Number(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.TryGetDouble(out var number))
            {
                return number;
            }
        }
        return 0;
    }

    private static bool HasNumber(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetDouble(out var number) &&
                double.IsFinite(number))
            {
                return true;
            }
        }
        return false;
    }

    private void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _nextAttemptAt, 0);
        Volatile.Write(ref _lastError, string.Empty);
        _lastSuccessAt = DateTimeOffset.UtcNow;
    }

    private void RecordFailure(string message)
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        var exponent = Math.Min(Math.Max(0, failures - 1), 6);
        var delaySeconds = Math.Min(
            _options.MaximumBackoff.TotalSeconds,
            _options.InitialBackoff.TotalSeconds * Math.Pow(2, exponent));
        Interlocked.Exchange(
            ref _nextAttemptAt,
            Stopwatch.GetTimestamp() +
            (long)(Stopwatch.Frequency * Math.Max(0, delaySeconds)));
        Volatile.Write(ref _lastError, message);
    }

    private static bool Valid(double seconds) =>
        double.IsFinite(seconds) && seconds is > 0 and < 1_800;

    public void Dispose()
    {
        _cancellation.Cancel();
        try { _worker.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _cancellation.Dispose();
        _client.Dispose();
    }

    private sealed class TimingTarget
    {
        public TimingTarget(
            string sessionKey,
            string trackName,
            int sessionCode,
            int vehicleId,
            bool discardExistingHistory)
        {
            SessionKey = sessionKey;
            TrackName = trackName;
            SessionCode = sessionCode;
            VehicleId = vehicleId;
            DiscardExistingHistory = discardExistingHistory;
        }

        public string SessionKey { get; }
        public string TrackName { get; }
        public int SessionCode { get; }
        public int VehicleId { get; }
        public bool DiscardExistingHistory { get; }
        public HashSet<string>? ExcludedLapSignatures { get; set; }
    }
    private sealed record TimingValue(string SessionKey, double OptimalLapSeconds);
}

public sealed record OfficialTimingOptimalOptions
{
    public bool Enabled { get; init; } = true;
    public Uri BaseAddress { get; init; } = new("http://127.0.0.1:6397/");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumBackoff { get; init; } = TimeSpan.FromSeconds(30);

    public OfficialTimingOptimalOptions Sanitize()
    {
        var loopback = BaseAddress.IsLoopback &&
            BaseAddress.Scheme == Uri.UriSchemeHttp
            ? BaseAddress
            : new Uri("http://127.0.0.1:6397/");
        return this with
        {
            BaseAddress = loopback,
            Timeout = TimeSpan.FromMilliseconds(Math.Clamp(Timeout.TotalMilliseconds, 100, 2_000)),
            PollInterval = TimeSpan.FromMilliseconds(Math.Clamp(PollInterval.TotalMilliseconds, 250, 5_000)),
            InitialBackoff = TimeSpan.FromMilliseconds(Math.Clamp(InitialBackoff.TotalMilliseconds, 250, 5_000)),
            MaximumBackoff = TimeSpan.FromSeconds(Math.Clamp(MaximumBackoff.TotalSeconds, 1, 120)),
        };
    }
}

public sealed record OfficialTimingOptimalDiagnostics(
    bool Enabled,
    int ConsecutiveFailures,
    bool CircuitOpen,
    DateTimeOffset? LastSuccessAt,
    string LastError);
