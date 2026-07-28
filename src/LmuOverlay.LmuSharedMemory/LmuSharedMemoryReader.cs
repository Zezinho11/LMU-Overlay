using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

[SupportedOSPlatform("windows")]
public sealed class LmuSharedMemoryReader : ILmuTelemetrySource
{
    private readonly MemoryMappedFile? _map;
    private readonly LmuConnectionState? _startupFailureState;
    private readonly string _startupFailureDetail = string.Empty;

    public LmuSharedMemoryReader()
    {
        try
        {
            _map = MemoryMappedFile.OpenExisting(
                LmuApiLayoutV1.MapName,
                MemoryMappedFileRights.Read);
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

        using var view = _map.CreateViewAccessor(
            0,
            LmuApiLayoutV1.ObjectSize,
            MemoryMappedFileAccess.Read);

        var buffer = new byte[LmuApiLayoutV1.ObjectSize];
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var scoringBefore = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));
            var telemetryBefore = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));

            var bytesRead = view.ReadArray(0, buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
            {
                return LmuTelemetrySnapshot.Unavailable(
                    LmuConnectionState.IncompatibleLayout,
                    $"Expected {buffer.Length} bytes, read {bytesRead}.");
            }

            var scoringAfter = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));
            var telemetryAfter = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));

            if (scoringBefore == scoringAfter && telemetryBefore == telemetryAfter)
            {
                return LmuSnapshotParser.ParseTelemetry(buffer);
            }
        }

        return LmuTelemetrySnapshot.Unavailable(
            LmuConnectionState.InvalidData,
            "LMU updated the snapshot during three consecutive read attempts.");
    }

    public void Dispose() => _map?.Dispose();
}
