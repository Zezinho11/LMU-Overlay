using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static LmuSessionSnapshot ParseSession(ReadOnlySpan<byte> data)
    {
        var sessionCode = ReadInt32(data, LmuApiLayoutV1.SessionCodeOffset);
        return new(
            ReadText(data, LmuApiLayoutV1.TrackNameOffset, LmuApiLayoutV1.TrackNameLength),
            sessionCode,
            ToSessionKind(sessionCode),
            (LmuGamePhase)data[LmuApiLayoutV1.GamePhaseOffset],
            ReadDouble(data, LmuApiLayoutV1.SessionCurrentElapsedTimeOffset),
            ReadDouble(data, LmuApiLayoutV1.SessionEndElapsedTimeOffset),
            LmuSessionLimits.NormalizeMaximumLaps(
                ReadInt32(data, LmuApiLayoutV1.SessionMaximumLapsOffset)),
            ReadDouble(data, LmuApiLayoutV1.SessionLapLengthOffset),
            ReadBoolean(data, LmuApiLayoutV1.IsRealtimeOffset),
            ReadText(data, LmuApiLayoutV1.PlayerNameOffset, LmuApiLayoutV1.PlayerNameLength),
            new(
                ReadDouble(data, LmuApiLayoutV1.CloudinessOffset),
                ReadDouble(data, LmuApiLayoutV1.RainIntensityOffset),
                ReadDouble(data, LmuApiLayoutV1.AmbientTemperatureOffset),
                ReadDouble(data, LmuApiLayoutV1.TrackTemperatureOffset),
                ReadVector3(data, LmuApiLayoutV1.WindOffset),
                ReadDouble(data, LmuApiLayoutV1.MinimumPathWetnessOffset),
                ReadDouble(data, LmuApiLayoutV1.MaximumPathWetnessOffset),
                ReadDouble(data, LmuApiLayoutV1.AveragePathWetnessOffset),
                data[LmuApiLayoutV1.TrackGripLevelOffset]));
    }
}
