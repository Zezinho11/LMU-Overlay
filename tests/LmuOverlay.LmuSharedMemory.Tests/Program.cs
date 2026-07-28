using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using LmuOverlay.Domain;
using LmuOverlay.LmuSharedMemory;

var data = new byte[LmuApiLayoutV1.ObjectSize];
WriteUInt32(data, LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.ScoringUpdateEventIndex), 7);
WriteUInt32(data, LmuApiLayoutV1.EventOffset(LmuApiLayoutV1.TelemetryUpdateEventIndex), 9);
WriteInt32(data, LmuApiLayoutV1.GameVersionOffset, 14_000);
WriteText(data, LmuApiLayoutV1.TrackNameOffset, "Circuit de Spa-Francorchamps");
WriteInt32(data, LmuApiLayoutV1.SessionCodeOffset, 10);
WriteDouble(data, LmuApiLayoutV1.SessionCurrentElapsedTimeOffset, 1_000);
WriteDouble(data, LmuApiLayoutV1.SessionEndElapsedTimeOffset, 1_600);
WriteInt32(data, LmuApiLayoutV1.SessionMaximumLapsOffset, 100);
WriteDouble(data, LmuApiLayoutV1.SessionLapLengthOffset, 7_004);
WriteInt32(data, LmuApiLayoutV1.ScoredVehiclesOffset, 1);
data[LmuApiLayoutV1.GamePhaseOffset] = (byte)LmuGamePhase.GreenFlag;
data[LmuApiLayoutV1.IsRealtimeOffset] = 1;
WriteText(data, LmuApiLayoutV1.PlayerNameOffset, "Anonymous");
WriteDouble(data, LmuApiLayoutV1.CloudinessOffset, 0.4);
WriteDouble(data, LmuApiLayoutV1.RainIntensityOffset, 0.1);
WriteDouble(data, LmuApiLayoutV1.AmbientTemperatureOffset, 18.5);
WriteDouble(data, LmuApiLayoutV1.TrackTemperatureOffset, 26.0);
WriteDouble(data, LmuApiLayoutV1.WindOffset, 3);
WriteDouble(data, LmuApiLayoutV1.WindOffset + 8, 0);
WriteDouble(data, LmuApiLayoutV1.WindOffset + 16, 4);
WriteDouble(data, LmuApiLayoutV1.MinimumPathWetnessOffset, 0.05);
WriteDouble(data, LmuApiLayoutV1.MaximumPathWetnessOffset, 0.2);
WriteDouble(data, LmuApiLayoutV1.AveragePathWetnessOffset, 0.12);
data[LmuApiLayoutV1.TrackGripLevelOffset] = 3;

var scoring = LmuApiLayoutV1.VehicleScoringOffset(0);
WriteInt32(data, scoring + LmuApiLayoutV1.ScoringVehicleIdOffset, 42);
WriteText(data, scoring + LmuApiLayoutV1.ScoringDriverNameOffset, "Fixture Driver");
WriteText(data, scoring + LmuApiLayoutV1.ScoringVehicleNameOffset, "Porsche 963");
WriteText(data, scoring + LmuApiLayoutV1.ScoringVehicleClassOffset, "Hypercar");
WriteInt16(data, scoring + LmuApiLayoutV1.ScoringCompletedLapsOffset, 12);
data[scoring + LmuApiLayoutV1.ScoringSectorOffset] = 2;
WriteDouble(data, scoring + LmuApiLayoutV1.ScoringLapDistanceOffset, 3_502);
WriteDouble(data, scoring + LmuApiLayoutV1.ScoringBestLapTimeOffset, 122.5);
WriteDouble(data, scoring + LmuApiLayoutV1.ScoringLastLapTimeOffset, 123.1);
WriteInt16(data, scoring + LmuApiLayoutV1.ScoringPitStopsOffset, 1);
data[scoring + LmuApiLayoutV1.VehicleScoringIsPlayerOffset] = 1;
data[scoring + LmuApiLayoutV1.ScoringPositionOffset] = 1;
data[scoring + LmuApiLayoutV1.ScoringPitStateOffset] = (byte)LmuPitState.None;
data[scoring + LmuApiLayoutV1.ScoringFuelFractionOffset] = 128;
data[scoring + LmuApiLayoutV1.ScoringDrsActiveOffset] = 1;

data[LmuApiLayoutV1.ActiveVehiclesOffset] = 1;
data[LmuApiLayoutV1.PlayerVehicleIndexOffset] = 0;
data[LmuApiLayoutV1.PlayerHasVehicleOffset] = 1;
var telemetry = LmuApiLayoutV1.VehicleTelemetryOffset(0);
WriteInt32(data, telemetry + LmuApiLayoutV1.TelemetryVehicleIdOffset, 42);
WriteInt32(data, telemetry + LmuApiLayoutV1.TelemetryLapNumberOffset, 13);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryLapStartElapsedTimeOffset, 950);
WriteText(data, telemetry + LmuApiLayoutV1.VehicleNameOffset, "Porsche 963");
WriteText(data, telemetry + LmuApiLayoutV1.TelemetryVehicleModelOffset, "963");
data[telemetry + LmuApiLayoutV1.TelemetryVehicleClassOffset] = 3;
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryLocalVelocityOffset, 0);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryLocalVelocityOffset + 8, 0);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryLocalVelocityOffset + 16, 50);
WriteInt32(data, telemetry + LmuApiLayoutV1.TelemetryGearOffset, 4);
data[telemetry + LmuApiLayoutV1.TelemetryMaximumGearsOffset] = 7;
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryEngineRpmOffset, 8_000);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryEngineMaximumRpmOffset, 10_000);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryEngineWaterTemperatureOffset, 78);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryEngineOilTemperatureOffset, 92);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryThrottleOffset, 0.75);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryBrakeOffset, 0.1);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetrySteeringOffset, -0.2);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryClutchOffset, 0);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryFuelOffset, 50);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryFuelCapacityOffset, 100);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryRearBrakeBiasOffset, 0.43);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryDeltaBestOffset, -0.2);
WriteDouble(data, telemetry + LmuApiLayoutV1.TelemetryBatteryChargeOffset, 0.6);
WriteSingle(data, telemetry + LmuApiLayoutV1.TelemetryRegenerationOffset, 25);
WriteSingle(data, telemetry + LmuApiLayoutV1.TelemetryStateOfChargeOffset, 0.65f);
WriteSingle(data, telemetry + LmuApiLayoutV1.TelemetryVirtualEnergyOffset, 42);
WriteSingle(data, telemetry + LmuApiLayoutV1.TelemetryGapToCarAheadOffset, 1.2f);
WriteSingle(data, telemetry + LmuApiLayoutV1.TelemetryGapToCarBehindOffset, 0.8f);
WriteInt32(data, telemetry + LmuApiLayoutV1.TelemetryCurrentSectorOffset, 1);
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlActiveOffset] = 1;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlLevelOffset] = 4;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlMaximumOffset] = 12;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlSlipLevelOffset] = 7;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlSlipMaximumOffset] = 12;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlCutLevelOffset] = 3;
data[telemetry + LmuApiLayoutV1.TelemetryTractionControlCutMaximumOffset] = 12;
data[telemetry + LmuApiLayoutV1.TelemetryAbsLevelOffset] = 6;
data[telemetry + LmuApiLayoutV1.TelemetryAbsMaximumOffset] = 12;
data[telemetry + LmuApiLayoutV1.TelemetryScheduledStopsOffset] = 2;
data[telemetry + LmuApiLayoutV1.TelemetryOverheatingOffset] = 1;
data[telemetry + LmuApiLayoutV1.TelemetryDentSeverityOffset] = 2;
data[telemetry + LmuApiLayoutV1.TelemetryDentSeverityOffset + 1] = 1;
WriteDouble(
    data,
    telemetry + LmuApiLayoutV1.TelemetryLastImpactElapsedTimeOffset,
    321.5);
WriteDouble(
    data,
    telemetry + LmuApiLayoutV1.TelemetryLastImpactMagnitudeOffset,
    18.25);
for (var wheel = 0; wheel < 4; wheel++)
{
    WriteDouble(
        data,
        telemetry +
        LmuApiLayoutV1.TelemetryWheelArrayOffset +
        (wheel * LmuApiLayoutV1.TelemetryWheelSize) +
        LmuApiLayoutV1.TelemetryWheelWearOffset,
        0.1 + (wheel * 0.05));
    WriteDouble(
        data,
        telemetry +
        LmuApiLayoutV1.TelemetryWheelArrayOffset +
        (wheel * LmuApiLayoutV1.TelemetryWheelSize) +
        LmuApiLayoutV1.TelemetryWheelCarcassTemperatureOffset,
        353.15 + wheel);
}
data[
    telemetry +
    LmuApiLayoutV1.TelemetryWheelArrayOffset +
    LmuApiLayoutV1.TelemetryWheelFlatOffset] = 1;

var expectedPath = Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "spa-single-vehicle.snapshot.json");
var expected = JsonSerializer.Deserialize<ExpectedFixture>(
    File.ReadAllText(expectedPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("Fixture could not be loaded.");

var snapshot = LmuSnapshotParser.ParseTelemetry(data);
Require(snapshot.State == LmuConnectionState.Connected, "Connected state");
Require(snapshot.GameVersion == expected.GameVersion, "Game version");
Require(snapshot.ScoringSequence == 7, "Scoring sequence");
Require(snapshot.TelemetrySequence == 9, "Telemetry sequence");
Require(snapshot.Session?.TrackName == expected.TrackName, "Track name");
Require(snapshot.Session?.Kind == LmuSessionKind.Race, "Session kind");
Require(snapshot.Session?.GamePhase == LmuGamePhase.GreenFlag, "Game phase");
Require(snapshot.Session?.Weather.WindMetersPerSecond.Length == 5, "Wind magnitude");
Require(snapshot.Session?.Weather.AveragePathWetness == 0.12, "Average path wetness");
Require(snapshot.Session?.Weather.TrackGripLevel == 3, "Track grip level");
Require(snapshot.Player?.VehicleName == expected.PlayerVehicleName, "Player vehicle");
Require(snapshot.Player?.Position == 1, "Player position");
Require(snapshot.Player?.Gear == 4, "Gear");
Require(snapshot.Player?.SpeedKilometersPerHour == expected.SpeedKilometersPerHour, "Speed");
Require(snapshot.Player?.FuelLiters == 50, "Fuel");
Require(snapshot.Player?.EngineWaterTemperatureCelsius == 78, "Water temperature");
Require(snapshot.Player?.EngineOilTemperatureCelsius == 92, "Oil temperature");
Require(snapshot.Player?.RearBrakeBiasFraction == 0.43, "Rear brake bias");
Require(snapshot.Player?.TractionControlLevel == 4, "TC level");
Require(snapshot.Player?.TractionControlSlipLevel == 7, "TC slip level");
Require(snapshot.Player?.TractionControlCutLevel == 3, "TC cut level");
Require(snapshot.Player?.AbsLevel == 6, "ABS level");
Require(snapshot.Player?.TireTemperatures.FrontLeftCelsius == 80, "Front-left tire temperature");
Require(snapshot.Player?.TireTemperatures.RearRightCelsius == 83, "Rear-right tire temperature");
Require(snapshot.Player?.TireWear.FrontLeftFraction == 0.1, "Front-left tire wear");
Require(
    Math.Abs((snapshot.Player?.TireWear.RearRightFraction ?? 0) - 0.25) < 0.0001,
    "Rear-right tire wear");
Require(snapshot.Player?.Damage?.ScheduledStops == 2, "Scheduled stops");
Require(snapshot.Player?.Damage?.Overheating == true, "Overheating state");
Require(snapshot.Player?.Damage?.MaximumDentSeverity == 2, "Maximum dent severity");
Require(snapshot.Player?.Damage?.DamagedAreas == 2, "Damaged area count");
Require(snapshot.Player?.Damage?.FrontLeft.Flat == true, "Front-left flat state");
Require(snapshot.Player?.Damage?.HasCriticalDamage == true, "Critical damage aggregate");
Require(snapshot.Standings.Count == 1, "Standings count");
Require(snapshot.Standings[0].DriverName == "Fixture Driver", "Driver name");
Require(
    snapshot.Standings[0].VehicleModel == "Porsche 963 963",
    "Standing vehicle model must be joined from telemetry by vehicle id");
Require(snapshot.Standings[0].FuelFraction == 128d / 255d, "Scoring fuel fraction");

var metrics = LmuTelemetryMetricsCalculator.Calculate(snapshot);
Require(metrics.SessionTimeRemainingSeconds == 600, "Session time remaining");
Require(metrics.LapsRemaining == 87, "Laps remaining");
Require(metrics.CurrentLapTimeSeconds == 50, "Current lap time");
Require(metrics.LapProgress == 0.5, "Lap progress");
Require(metrics.FuelFraction == 0.5, "Fuel fraction");
Require(metrics.EngineRpmFraction == 0.8, "RPM fraction");
Require(
    metrics.BatteryFraction is { } battery &&
    Math.Abs(battery - 0.65) < 0.0001,
    "Battery fraction");

WriteInt32(data, LmuApiLayoutV1.SessionMaximumLapsOffset, int.MaxValue);
var timedSnapshot = LmuSnapshotParser.ParseTelemetry(data);
var timedMetrics = LmuTelemetryMetricsCalculator.Calculate(timedSnapshot);
Require(timedSnapshot.Session?.MaximumLaps == 0, "Unlimited lap sentinel normalization");
Require(timedMetrics.LapsRemaining is null, "Timed session must not expose fake laps remaining");

var probe = LmuSnapshotParser.Parse(data);
Require(probe.GameVersion == "14000", "Probe game version");
Require(probe.PlayerVehicleName == expected.PlayerVehicleName, "Probe player vehicle");

var shortSnapshot = LmuSnapshotParser.ParseTelemetry(new byte[128]);
Require(shortSnapshot.State == LmuConnectionState.IncompatibleLayout, "Short layout");

var invalidCounts = new byte[LmuApiLayoutV1.ObjectSize];
WriteInt32(
    invalidCounts,
    LmuApiLayoutV1.ScoredVehiclesOffset,
    LmuApiLayoutV1.MaximumVehicles + 1);
var invalidSnapshot = LmuSnapshotParser.ParseTelemetry(invalidCounts);
Require(invalidSnapshot.State == LmuConnectionState.InvalidData, "Invalid counts");

Console.WriteLine("LMU shared-memory checks passed.");
return 0;

static void WriteText(byte[] destination, int offset, string value) =>
    Encoding.UTF8.GetBytes(value, destination.AsSpan(offset));

static void WriteInt16(byte[] destination, int offset, short value) =>
    BinaryPrimitives.WriteInt16LittleEndian(destination.AsSpan(offset, sizeof(short)), value);

static void WriteInt32(byte[] destination, int offset, int value) =>
    BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset, sizeof(int)), value);

static void WriteUInt32(byte[] destination, int offset, uint value) =>
    BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);

static void WriteSingle(byte[] destination, int offset, float value) =>
    BinaryPrimitives.WriteSingleLittleEndian(destination.AsSpan(offset, sizeof(float)), value);

static void WriteDouble(byte[] destination, int offset, double value) =>
    BinaryPrimitives.WriteDoubleLittleEndian(destination.AsSpan(offset, sizeof(double)), value);

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Check failed: {name}");
    }
}

internal sealed record ExpectedFixture(
    int GameVersion,
    string TrackName,
    string PlayerVehicleName,
    double SpeedKilometersPerHour);
