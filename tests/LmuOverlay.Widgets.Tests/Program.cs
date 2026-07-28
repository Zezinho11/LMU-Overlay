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
    EngineWaterTemperatureCelsius: 78,
    EngineOilTemperatureCelsius: 92,
    RearBrakeBiasFraction: 0.43,
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
    TractionControlLevel: 4,
    TractionControlMaximum: 12,
    TractionControlSlipLevel: 7,
    TractionControlSlipMaximum: 12,
    TractionControlCutLevel: 3,
    TractionControlCutMaximum: 12,
    AbsLevel: 6,
    AbsMaximum: 12,
    TireTemperatures: new LmuWheelTemperatures(82, 84, 78, 79));
var snapshot = new LmuTelemetrySnapshot(
    LmuConnectionState.Connected, 14000, 1, 1, 1, 1, null, player,
    Array.Empty<LmuVehicleStanding>(), DateTimeOffset.UtcNow, string.Empty);

var dashboard = EssentialWidgetStateFactory.CreateDashboard(snapshot);
var inputs = EssentialWidgetStateFactory.CreateInputs(snapshot);

Assert(dashboard.Gear == "R", "Reverse gear must be renderer-independent.");
Assert(dashboard.EngineRpmFraction == 1, "RPM fraction must be clamped.");
Assert(dashboard.EngineWaterTemperatureCelsius == 78, "Water temperature must reach the dashboard.");
Assert(dashboard.EngineOilTemperatureCelsius == 92, "Oil temperature must reach the dashboard.");
Assert(dashboard.RearBrakeBiasFraction == 0.43, "Brake bias must reach the dashboard.");
Assert(dashboard.AbsActive, "ABS activation must reach the dashboard.");
Assert(dashboard.TractionControlLevel == 4, "TC level must reach the dashboard.");
Assert(dashboard.TractionControlSlipLevel == 7, "TC slip must reach the dashboard.");
Assert(dashboard.TractionControlCutLevel == 3, "TC cut must reach the dashboard.");
Assert(dashboard.AbsLevel == 6, "ABS level must reach the dashboard.");
Assert(TireTemperatureClassifier.Classify(55) == TireTemperatureBand.Cold,
    "Cold tires must use the cold visual band.");
Assert(TireTemperatureClassifier.Classify(74) == TireTemperatureBand.Warming,
    "Warming tires must use the transitional visual band.");
Assert(TireTemperatureClassifier.Classify(82) == TireTemperatureBand.Optimal,
    "Normal operating temperatures must use the optimal visual band.");
Assert(TireTemperatureClassifier.Classify(105) == TireTemperatureBand.Hot,
    "Hot tires must use the warning visual band.");
Assert(TireTemperatureClassifier.Classify(120) == TireTemperatureBand.Critical,
    "Critical tire temperatures must use the critical visual band.");
Assert(inputs.Throttle == 1 && inputs.Brake == 0, "Pedal inputs must be clamped.");
Assert(inputs.Steering == 1, "Steering must be clamped.");

var standings = new[]
{
    Standing(1, "Leader", 1, 0, false, false),
    Standing(2, "John Suzuki", 2, 3.2, true, false,
        vehicleName: "Porsche 963 #6"),
    Standing(3, "Pitting", 3, 5.7, false, true),
};
var session = new LmuSessionSnapshot(
    "Spa", 10, LmuSessionKind.Race, LmuGamePhase.FullCourseYellow,
    120, 3720, 20, 7004, true, "Player",
    new LmuWeatherSnapshot(
        0.2, 0, 21, 29, new LmuVector3(0, 0, 0), 0, 0, 0.05, 3));
var raceSnapshot = snapshot with { Session = session, Standings = standings };
var raceDashboard = EssentialWidgetStateFactory.CreateDashboard(raceSnapshot);
var liveStandings = EssentialWidgetStateFactory.CreateLiveStandings(raceSnapshot);
var relative = EssentialWidgetStateFactory.CreateRelative(raceSnapshot);
var sessionFlags = EssentialWidgetStateFactory.CreateSessionFlags(raceSnapshot);

Assert(relative.Rows.Count == 3, "Relative must include cars around the player.");
Assert(relative.Rows[0].RelativeGapSeconds == -3.2, "Relative gaps must be player-relative.");
Assert(relative.Rows[2].IsInPitLane, "Relative must preserve pit state.");
Assert(relative.Rows[1].DriverDisplayName == "J Suzuki",
    "Relative must expose a compact driver display name.");
Assert(relative.Rows[1].ClassAbbreviation == "HYP",
    "Relative must expose a compact multiclass badge.");
Assert(relative.Rows[1].CarNumber == "6",
    "Relative must use only explicit official race numbers.");
Assert(sessionFlags.FlagName == "YELLOW", "FCY must produce a yellow flag state.");
Assert(sessionFlags.TrackGripName == "HEAVY",
    "Official RealRoad level 3 must render as heavy grip.");
Assert(sessionFlags.WeatherCondition == WeatherConditionKind.PartlyCloudy,
    "Low cloud cover without rain must render as partly cloudy.");
Assert(sessionFlags.AveragePathWetness == 0.05,
    "Average official path wetness must reach the session widget.");
Assert(sessionFlags.RemainingSeconds == 3600, "Remaining session time must be derived.");
Assert(raceDashboard.CurrentLapTimeSeconds == 120, "Current lap time must be derived.");
Assert(raceDashboard.LastLapTimeSeconds == 121, "Last lap time must be preserved.");
Assert(raceDashboard.BestLapTimeSeconds == 120, "Best lap time must be preserved.");
Assert(liveStandings.Classes[0].Rows[0].DriverAbbreviation == "LEA",
    "Standings must expose compact driver abbreviations.");
Assert(liveStandings.Classes[0].Rows[0].VehicleModel == "Porsche 963",
    "Standings must preserve the telemetry vehicle model for manufacturer badges.");

var deepField = Enumerable.Range(1, 15)
    .Select(position => Standing(
        position,
        position == 12 ? "Antônio da Costa" : $"Driver {position}",
        position,
        position * 2,
        position == 12,
        false,
        vehicleName: position == 12 ? "Porsche 963 #6" : "Porsche 963"))
    .ToArray();
var compactStandings = EssentialWidgetStateFactory.CreateLiveStandings(
    raceSnapshot with { Standings = deepField });
var compactRows = compactStandings.Classes[0].Rows;
Assert(compactRows.Count == 14,
    "A single-class standings tower must use all fourteen available car rows.");
Assert(compactRows[0].ClassPosition == 1, "The class leader must always remain visible.");
Assert(compactRows.Any(row => row.IsPlayer), "The moving window must always include the player.");
Assert(compactRows.Single(row => row.IsPlayer).DriverAbbreviation == "COS",
    "Driver abbreviation must use the final name component.");
Assert(compactRows.Single(row => row.IsPlayer).CarNumber == "6",
    "Explicit race numbers must be extracted from the official vehicle name.");

var multiclassField = deepField
    .Select(row => row with { VehicleClass = "GT3" })
    .Concat(new[]
    {
        Standing(101, "Hyper Leader", 16, 0, false, false) with
        {
            VehicleClass = "Hypercar",
        },
        Standing(102, "LMP2 Leader", 17, 0, false, false) with
        {
            VehicleClass = "LMP2",
        },
    })
    .ToArray();
var multiclassStandings = EssentialWidgetStateFactory.CreateLiveStandings(
    raceSnapshot with { Standings = multiclassField });
Assert(multiclassStandings.Classes.Sum(group => group.Rows.Count) == 13,
    "Three-class standings must fill the tower without overflowing.");
Assert(multiclassStandings.Classes.Single(group => group.IsPlayerClass).Rows.Count == 11,
    "Spare multiclass rows must belong to the player's class.");

var fuelTracker = new FuelStrategyTracker();
var learning = fuelTracker.Update(raceSnapshot);
var nextLapSnapshot = raceSnapshot with
{
    Player = player with { FuelLiters = 37.5 },
    Standings = new[]
    {
        Standing(1, "Leader", 1, 0, false, false, 5),
        Standing(2, "Player", 2, 3.2, true, false, 5),
    },
};
var strategy = fuelTracker.Update(nextLapSnapshot);
var refueled = fuelTracker.Update(nextLapSnapshot with
{
    Player = player with { FuelLiters = 60 },
});

Assert(learning.Learning, "Fuel strategy must learn before the first completed lap.");
Assert(strategy.AverageConsumptionLitersPerLap == 2.5, "Fuel use must be sampled per lap.");
Assert(strategy.ProjectedConsumptionLitersPerLap == 2.5,
    "One valid sample must become the conservative projection baseline.");
Assert(strategy.EstimatedLapsToFinish == 15, "Remaining laps must use session length.");
Assert(strategy.RequiredFuelLiters == 40, "Fuel need must include a one-lap reserve.");
Assert(strategy.SuggestedPitLap == 19,
    "Pit recommendation must account for usable fuel and the reserve lap.");
Assert(strategy.LapsUntilPit == 14,
    "Pit countdown must report the usable stint length.");
Assert(strategy.FuelToAddLiters == 2.5,
    "Fuel-to-add must close the projected finish deficit.");
Assert(strategy.RequiredFuelSavingFraction > 0,
    "A short strategy must expose the required saving target.");
Assert(strategy.Confidence == "LOW",
    "One valid lap must produce low-confidence guidance.");
Assert(strategy.EstimatedRangeTimeSeconds == strategy.EstimatedRangeLaps * 121,
    "Fuel range time must use the latest valid lap.");
Assert(strategy.EstimatedTimeToFinishSeconds == 15 * 121,
    "Finish time must use the same reference lap.");
Assert(strategy.Status == "SHORT", "Negative fuel margin must be highlighted.");
Assert(refueled.Samples == 1, "Refueling must not be recorded as negative consumption.");

var timedSession = session with { MaximumLaps = int.MaxValue };
var timedRace = raceSnapshot with { Session = timedSession };
var timedFlags = EssentialWidgetStateFactory.CreateSessionFlags(timedRace);
var timedTracker = new FuelStrategyTracker();
_ = timedTracker.Update(timedRace);
var timedStrategy = timedTracker.Update(nextLapSnapshot with { Session = timedSession });
Assert(timedFlags.MaximumLaps == 0, "Timed sessions must hide the unlimited-lap sentinel.");
Assert(timedStrategy.EstimatedLapsToFinish == 30, "Timed fuel projection must use time and lap pace.");
Assert(timedStrategy.RequiredFuelLiters == 77.5, "Timed projection must remain within a realistic range.");

var rainSession = session with
{
    GamePhase = LmuGamePhase.Stopped,
    Weather = session.Weather with
    {
        Cloudiness = 0.95,
        RainIntensity = 0.8,
        AveragePathWetness = 0.7,
    },
};
var rainFlags = EssentialWidgetStateFactory.CreateSessionFlags(
    raceSnapshot with { Session = rainSession });
Assert(rainFlags.FlagName == "RED",
    "A stopped race phase must produce the red flag card.");
Assert(rainFlags.WeatherCondition == WeatherConditionKind.HeavyRain,
    "High official rain intensity must produce the heavy-rain icon state.");

var energyTracker = new FuelStrategyTracker();
_ = energyTracker.Update(raceSnapshot with
{
    Player = player with { FuelLiters = 40, VirtualEnergy = 1 },
});
var energyStrategy = energyTracker.Update(nextLapSnapshot with
{
    Player = player with { FuelLiters = 37.5, VirtualEnergy = 0.92 },
});
Assert(
    Math.Abs(energyStrategy.AverageVirtualEnergyFractionPerLap - 0.08) < 0.0001,
    "Virtual Energy use must be sampled per lap.");
Assert(
    Math.Abs(energyStrategy.EstimatedVirtualEnergyRangeLaps - 11.5) < 0.0001,
    "Virtual Energy range must use its rolling consumption.");
Assert(
    Math.Abs(energyStrategy.RequiredVirtualEnergyFraction - 1.28) < 0.0001,
    "Virtual Energy finish need must include the reserve lap.");
Assert(
    Math.Abs(energyStrategy.VirtualEnergyMarginFraction + 0.36) < 0.0001,
    "Virtual Energy margin must compare current energy with finish need.");
Console.WriteLine("Widget state checks passed.");
return 0;

static LmuVehicleStanding Standing(
    int id,
    string driver,
    int position,
    double gap,
    bool isPlayer,
    bool isInPits,
    int completedLaps = 4,
    string vehicleName = "Car") => new(
        id, driver, vehicleName, "Porsche 963", "Hypercar", position, completedLaps, 1, 100,
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
