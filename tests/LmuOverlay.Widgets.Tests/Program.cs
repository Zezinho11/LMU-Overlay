using LmuOverlay.Domain;
using LmuOverlay.Widgets;

var player = new LmuPlayerTelemetry(
    VehicleId: 1,
    VehicleName: "Car",
    VehicleModel: "Model",
    VehicleClassId: 6,
    LapNumber: 4,
    LapStartElapsedTime: 0,
    LapDistanceMeters: 100,
    Position: 2,
    Gear: -1,
    MaximumGears: 6,
    EngineRpm: 9500,
    EngineMaximumRpm: 9000,
    SpeedKilometersPerHour: 250,
    Throttle: 1.2,
    Brake: -0.1,
    Steering: 2,
    Clutch: 0.4,
    FuelLiters: 40,
    FuelCapacityLiters: 100,
    DeltaBestSeconds: 0,
    BatteryChargeFraction: 0,
    StateOfCharge: 0,
    RegenerationKilowatts: 0,
    VirtualEnergy: 0,
    GapToCarAheadSeconds: 0,
    GapToCarBehindSeconds: 0,
    CurrentSector: 1,
    SpeedLimiterActive: false,
    LapInvalidated: false,
    AbsActive: true,
    TractionControlActive: false,
    TireTemperatures: new LmuWheelTemperatures(82, 84, 78, 79));
var snapshot = new LmuTelemetrySnapshot(
    LmuConnectionState.Connected, 14000, 1, 1, 1, 1, null, player,
    Array.Empty<LmuVehicleStanding>(), DateTimeOffset.UtcNow, string.Empty);

var dashboard = EssentialWidgetStateFactory.CreateDashboard(snapshot);
var inputs = EssentialWidgetStateFactory.CreateInputs(snapshot);

Assert(dashboard.Gear == "R", "Reverse gear must be renderer-independent.");
Assert(dashboard.EngineRpmFraction == 1, "RPM fraction must be clamped.");
Assert(inputs.Throttle == 1 && inputs.Brake == 0, "Pedal inputs must be clamped.");
Assert(inputs.Steering == 1, "Steering must be clamped.");

var standings = new[]
{
    Standing(1, "Leader", 1, 0, false, false),
    Standing(2, "Player", 2, 3.2, true, false),
    Standing(3, "Pitting", 3, 5.7, false, true),
};
var session = new LmuSessionSnapshot(
    "Spa", 10, LmuSessionKind.Race, LmuGamePhase.FullCourseYellow,
    120, 3720, 20, 7004, true, "Player",
    new LmuWeatherSnapshot(0.2, 0, 21, 29, new LmuVector3(0, 0, 0), 0, 0));
var raceSnapshot = snapshot with { Session = session, Standings = standings };
var relative = EssentialWidgetStateFactory.CreateRelative(raceSnapshot);
var sessionFlags = EssentialWidgetStateFactory.CreateSessionFlags(raceSnapshot);

Assert(relative.Rows.Count == 3, "Relative must include cars around the player.");
Assert(relative.Rows[0].RelativeGapSeconds == -3.2, "Relative gaps must be player-relative.");
Assert(relative.Rows[2].IsInPitLane, "Relative must preserve pit state.");
Assert(sessionFlags.FlagName == "YELLOW", "FCY must produce a yellow flag state.");
Assert(sessionFlags.RemainingSeconds == 3600, "Remaining session time must be derived.");
Console.WriteLine("Widget state checks passed.");
return 0;

static LmuVehicleStanding Standing(
    int id,
    string driver,
    int position,
    double gap,
    bool isPlayer,
    bool isInPits) => new(
        id, driver, "Car", "Hypercar", position, 4, 1, 100,
        120, 121, 1, 0, gap, 0, 0, 0, isPlayer, isInPits,
        isInPits ? LmuPitState.Entering : LmuPitState.None,
        0, false, false, 0.5, false);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
