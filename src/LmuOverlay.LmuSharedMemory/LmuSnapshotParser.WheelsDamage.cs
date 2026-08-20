using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static LmuDamageSnapshot ParseDamage(
        ReadOnlySpan<byte> data,
        int telemetryOffset)
    {
        var maximumDentSeverity = 0;
        var damagedAreas = 0;
        for (var index = 0; index < LmuApiLayoutV1.TelemetryDentSeverityCount; index++)
        {
            var severity = data[
                telemetryOffset +
                LmuApiLayoutV1.TelemetryDentSeverityOffset +
                index];
            maximumDentSeverity = Math.Max(maximumDentSeverity, severity);
            if (severity > 0)
            {
                damagedAreas++;
            }
        }

        return new(
            data[telemetryOffset + LmuApiLayoutV1.TelemetryScheduledStopsOffset],
            ReadBoolean(data, telemetryOffset + LmuApiLayoutV1.TelemetryOverheatingOffset),
            ReadBoolean(data, telemetryOffset + LmuApiLayoutV1.TelemetryBodyDetachedOffset),
            maximumDentSeverity,
            damagedAreas,
            ReadDouble(
                data,
                telemetryOffset + LmuApiLayoutV1.TelemetryLastImpactElapsedTimeOffset),
            ReadDouble(
                data,
                telemetryOffset + LmuApiLayoutV1.TelemetryLastImpactMagnitudeOffset),
            ReadWheelCondition(data, telemetryOffset, 0),
            ReadWheelCondition(data, telemetryOffset, 1),
            ReadWheelCondition(data, telemetryOffset, 2),
            ReadWheelCondition(data, telemetryOffset, 3));
    }

    private static LmuWheelCondition ReadWheelCondition(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex)
    {
        var wheelOffset = telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize);
        return new(
            ReadBoolean(data, wheelOffset + LmuApiLayoutV1.TelemetryWheelFlatOffset),
            ReadBoolean(data, wheelOffset + LmuApiLayoutV1.TelemetryWheelDetachedOffset));
    }

    private static double ReadWheelTemperature(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex)
    {
        var innerLayerOffset = telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize) +
            LmuApiLayoutV1.TelemetryWheelInnerLayerTemperatureOffset;
        var totalKelvin = 0d;
        for (var index = 0;
             index < LmuApiLayoutV1.TelemetryWheelInnerLayerTemperatureCount;
             index++)
        {
            var kelvin = ReadDouble(data, innerLayerOffset + (index * sizeof(double)));
            if (!double.IsFinite(kelvin) || kelvin <= 0)
            {
                return 0;
            }

            totalKelvin += kelvin;
        }

        return totalKelvin /
            LmuApiLayoutV1.TelemetryWheelInnerLayerTemperatureCount - 273.15;
    }

    private static double ReadWheelWear(
        ReadOnlySpan<byte> data,
        int telemetryOffset,
        int wheelIndex)
    {
        var value = ReadDouble(
            data,
            telemetryOffset +
            LmuApiLayoutV1.TelemetryWheelArrayOffset +
            (wheelIndex * LmuApiLayoutV1.TelemetryWheelSize) +
            LmuApiLayoutV1.TelemetryWheelWearOffset);
        return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    }
}
