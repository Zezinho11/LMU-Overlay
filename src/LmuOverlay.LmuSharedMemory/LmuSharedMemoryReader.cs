using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using System.Runtime.Versioning;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

[SupportedOSPlatform("windows")]
public sealed class LmuSharedMemoryReader : ILmuTelemetrySource
{
    private readonly MemoryMappedFile? _map;
    private readonly MemoryMappedViewAccessor? _view;
    private readonly byte[] _buffer = new byte[LmuApiLayoutV1.ObjectSize];
    private readonly byte[] _playerBuffer = new byte[LmuApiLayoutV1.VehicleTelemetrySize];
    private readonly LmuConnectionState? _startupFailureState;
    private readonly string _startupFailureDetail = string.Empty;
    private LmuTelemetrySnapshot? _lastSnapshot;
    private long _lastFullParseTimestamp;

    public LmuSharedMemoryReader()
    {
        try
        {
            _map = MemoryMappedFile.OpenExisting(
                LmuApiLayoutV1.MapName,
                MemoryMappedFileRights.Read);
            _view = _map.CreateViewAccessor(
                0,
                LmuApiLayoutV1.ObjectSize,
                MemoryMappedFileAccess.Read);
        }
        catch (FileNotFoundException)
        {
            _startupFailureState = LmuConnectionState.Disconnected;
            _startupFailureDetail =
                "LMU_Data is unavailable. Start Le Mans Ultimate and enable its shared-memory plugin.";
        }
        catch (UnauthorizedAccessException)
        {
            _startupFailureState = LmuConnectionState.AccessDenied;
            _startupFailureDetail = "Windows denied read-only access to LMU_Data.";
        }
    }

    public LmuProbeSnapshot ReadProbeSnapshot()
    {
        var snapshot = ReadTelemetrySnapshot();
        return new(
            snapshot.State,
            snapshot.GameVersion == 0
                ? string.Empty
                : snapshot.GameVersion.ToString(CultureInfo.InvariantCulture),
            snapshot.Session?.TrackName ?? string.Empty,
            snapshot.Session?.SessionCode ?? 0,
            snapshot.Player?.VehicleName ?? string.Empty,
            snapshot.ActiveVehicles,
            snapshot.ScoredVehicles,
            snapshot.Player is not null,
            snapshot.CapturedAt,
            snapshot.Detail);
    }

    public LmuTelemetrySnapshot ReadTelemetrySnapshot()
    {
        if (_startupFailureState is { } startupState)
        {
            return LmuTelemetrySnapshot.Unavailable(
                startupState,
                _startupFailureDetail);
        }

        if (_map is null)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "LMU shared-memory map was not opened.");
        }

        if (_view is null)
        {
            return LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "LMU shared-memory view was not opened.");
        }

        var now = Stopwatch.GetTimestamp();
        var requiresFullRead = _lastSnapshot is null ||
            _lastFullParseTimestamp == 0 ||
            now - _lastFullParseTimestamp >= Stopwatch.Frequency / 5;
        if (!requiresFullRead && TryReadPlayerTelemetryBlock() is { } fastSnapshot)
        {
            _lastSnapshot = fastSnapshot;
            return fastSnapshot;
        }

        return ReadFullSnapshot();
    }

    private LmuTelemetrySnapshot ReadFullSnapshot()
    {
        var view = _view ?? throw new ObjectDisposedException(nameof(LmuSharedMemoryReader));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var scoringBefore = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));

            var bytesRead = view.ReadArray(0, _buffer, 0, _buffer.Length);
            if (bytesRead != _buffer.Length)
            {
                return LmuTelemetrySnapshot.Unavailable(
                    LmuConnectionState.IncompatibleLayout,
                    $"Expected {_buffer.Length} bytes, read {bytesRead}.");
            }

            var scoringAfter = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));

            // Scoring and telemetry are independent producer streams. Requiring
            // both counters to remain stable while copying the whole mapping made
            // fast telemetry collisions trigger a one-second reconnect. Guard the
            // scoring copy here, then overlay a separately guarded player block.
            if (scoringBefore == scoringAfter)
            {
                _lastSnapshot = LmuSnapshotParser.ParseTelemetry(_buffer);
                _lastFullParseTimestamp = Stopwatch.GetTimestamp();
                if (_lastSnapshot.State == LmuConnectionState.Connected &&
                    TryReadPlayerTelemetryBlock() is { } playerSnapshot)
                {
                    _lastSnapshot = playerSnapshot;
                }

                return _lastSnapshot;
            }
        }

        return LmuTelemetrySnapshot.Unavailable(
            LmuConnectionState.InvalidData,
            "LMU updated scoring during three consecutive read attempts.");
    }

    private LmuTelemetrySnapshot? TryReadPlayerTelemetryBlock()
    {
        if (_view is null || _lastSnapshot?.Player is not { } previousPlayer)
        {
            return null;
        }

        var activeVehicles = _view.ReadByte(LmuApiLayoutV1.ActiveVehiclesOffset);
        var reportedIndex = _view.ReadByte(LmuApiLayoutV1.PlayerVehicleIndexOffset);
        var hasReportedVehicle = _view.ReadBoolean(LmuApiLayoutV1.PlayerHasVehicleOffset);
        var playerIndex = ResolvePlayerIndex(
            activeVehicles,
            reportedIndex,
            hasReportedVehicle,
            previousPlayer.VehicleId);
        if (playerIndex < 0)
        {
            return null;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var telemetryBefore = _view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));
            var bytesRead = _view.ReadArray(
                LmuApiLayoutV1.VehicleTelemetryOffset(playerIndex),
                _playerBuffer,
                0,
                _playerBuffer.Length);
            var telemetryAfter = _view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));
            if (bytesRead == _playerBuffer.Length && telemetryBefore == telemetryAfter)
            {
                var sourceElapsedTime = LmuSnapshotParser.ReadPlayerElapsedTime(
                    _playerBuffer);
                if (double.IsFinite(sourceElapsedTime) &&
                    sourceElapsedTime == previousPlayer.ElapsedTime)
                {
                    return _lastSnapshot;
                }

                return LmuSnapshotParser.ParsePlayerTelemetryBlock(
                    _playerBuffer,
                    _lastSnapshot,
                    telemetryAfter);
            }
        }

        return null;
    }

    private int ResolvePlayerIndex(
        int activeVehicles,
        int reportedIndex,
        bool hasReportedVehicle,
        int playerVehicleId)
    {
        if (_view is null)
        {
            return -1;
        }

        if (hasReportedVehicle && reportedIndex < activeVehicles &&
            _view.ReadInt32(
                LmuApiLayoutV1.VehicleTelemetryOffset(reportedIndex) +
                LmuApiLayoutV1.TelemetryVehicleIdOffset) == playerVehicleId)
        {
            return reportedIndex;
        }

        for (var index = 0; index < activeVehicles; index++)
        {
            if (_view.ReadInt32(
                    LmuApiLayoutV1.VehicleTelemetryOffset(index) +
                    LmuApiLayoutV1.TelemetryVehicleIdOffset) == playerVehicleId)
            {
                return index;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        _view?.Dispose();
        _map?.Dispose();
    }
}
