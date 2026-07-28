using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

[SupportedOSPlatform("windows")]
public sealed class LmuSharedMemoryReader : ILmuTelemetrySource
{
    private readonly MemoryMappedFile? _map;
    private readonly LmuProbeSnapshot? _startupFailure;

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
            _startupFailure = LmuProbeSnapshot.Disconnected(
                "LMU_Data is unavailable. Start Le Mans Ultimate and enable its shared-memory plugin.");
        }
        catch (UnauthorizedAccessException)
        {
            _startupFailure = new(
                LmuConnectionState.AccessDenied,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                0,
                0,
                false,
                DateTimeOffset.UtcNow,
                "Windows denied read-only access to LMU_Data.");
        }
    }

    public LmuProbeSnapshot ReadProbeSnapshot()
    {
        if (_startupFailure is not null)
        {
            return _startupFailure with { CapturedAt = DateTimeOffset.UtcNow };
        }

        if (_map is null)
        {
            return LmuProbeSnapshot.Disconnected("LMU shared-memory map was not opened.");
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
                return new(
                    LmuConnectionState.IncompatibleLayout,
                    string.Empty,
                    string.Empty,
                    0,
                    string.Empty,
                    0,
                    0,
                    false,
                    DateTimeOffset.UtcNow,
                    $"Expected {buffer.Length} bytes, read {bytesRead}.");
            }

            var scoringAfter = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex));
            var telemetryAfter = view.ReadUInt32(
                LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex));

            if (scoringBefore == scoringAfter && telemetryBefore == telemetryAfter)
            {
                return LmuSnapshotParser.Parse(buffer);
            }
        }

        return new(
            LmuConnectionState.InvalidData,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            0,
            0,
            false,
            DateTimeOffset.UtcNow,
            "LMU updated the snapshot during three consecutive read attempts.");
    }

    public void Dispose() => _map?.Dispose();
}
