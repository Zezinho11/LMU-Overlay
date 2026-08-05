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
    TireTemperatures: new LmuWheelTemperatures(82, 84, 78, 79),
    TireWear: new LmuWheelWear(0.12, 0.14, 0.18, 0.2),
    Damage: new LmuDamageSnapshot(
        1,
        true,
        false,
        2,
        3,
        112,
        24,
        new LmuWheelCondition(false, false),
        new LmuWheelCondition(false, false),
        new LmuWheelCondition(false, false),
        new LmuWheelCondition(false, false)));
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
Assert(dashboard.Throttle == 1 && dashboard.Brake == 0,
    "Clamped pedal inputs must reach the dashboard graph.");
Assert(!dashboard.SpeedLimiterActive, "Pit limiter state must reach the dashboard.");
Assert(dashboard.AbsActive, "ABS activation must reach the dashboard.");
Assert(dashboard.TractionControlLevel == 4, "TC level must reach the dashboard.");
Assert(dashboard.TractionControlSlipLevel == 7, "TC slip must reach the dashboard.");
Assert(dashboard.TractionControlCutLevel == 3, "TC cut must reach the dashboard.");
Assert(dashboard.AbsLevel == 6, "ABS level must reach the dashboard.");
Assert(dashboard.TireWear.FrontLeftFraction == 0.12,
    "Front-left wear must reach the dashboard.");
Assert(dashboard.TireWear.RearRightFraction == 0.2,
    "Rear-right wear must reach the dashboard.");
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
    Standing(1, "Leader", 1, 0, false, false, lapDistanceMeters: 700),
    Standing(2, "John Suzuki", 2, 3.2, true, false,
        vehicleName: "Porsche 963 #6", lapDistanceMeters: 1_000),
    Standing(3, "Pitting", 14, 5.7, false, true, lapDistanceMeters: 1_300),
};
var session = new LmuSessionSnapshot(
    "Spa", 10, LmuSessionKind.Race, LmuGamePhase.FullCourseYellow,
    120, 3720, 20, 7004, true, "Player",
    new LmuWeatherSnapshot(
        0.2, 0, 21, 29, new LmuVector3(0, 0, 0), 0, 0, 0.05, 3));
var raceSnapshot = snapshot with
{
    Session = session,
    Standings = standings,
    Player = player with { SpeedLimiterActive = true },
};
var raceDashboard = EssentialWidgetStateFactory.CreateDashboard(raceSnapshot);
var liveStandings = EssentialWidgetStateFactory.CreateLiveStandings(raceSnapshot);
var relative = EssentialWidgetStateFactory.CreateRelative(raceSnapshot);
var sessionFlags = EssentialWidgetStateFactory.CreateSessionFlags(raceSnapshot);
var raceControl = EssentialWidgetStateFactory.CreateRaceControl(raceSnapshot);

Assert(relative.Rows.Count == 3, "Relative must include cars around the player.");
Assert(relative.Rows[0].OverallPosition == 14,
    "A lower race position physically ahead must render above the player.");
Assert(relative.Rows[0].RelativeGapSeconds < 0,
    "A car physically ahead must have a negative relative gap.");
Assert(relative.Rows[0].IsInPitLane, "Relative must preserve pit state.");
Assert(relative.Rows[1].OverallPosition == 2 && relative.Rows[1].IsPlayer,
    "The badge must remain the official race position.");
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
Assert(raceDashboard.SpeedLimiterActive,
    "Active pit limiter must be exposed to the dashboard renderer.");
Assert(liveStandings.Classes[0].Rows[0].DriverAbbreviation == "LEA",
    "Standings must expose compact driver abbreviations.");
Assert(liveStandings.Classes[0].Rows[0].VehicleModel == "Porsche 963",
    "Standings must preserve the telemetry vehicle model for manufacturer badges.");
Assert(raceControl.RequiresAttention, "Damage must raise race-control attention.");
Assert(raceControl.HasCriticalDamage, "Overheating must be critical damage.");
Assert(raceControl.DamageStatus == "CRITICAL", "Critical damage must be explicit.");

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
var configuredStrategy = fuelTracker.Update(
    nextLapSnapshot,
    new FuelStrategyOptions(
        FuelReserveLaps: 2,
        EnergyReserveFraction: 0,
        ManualRemainingLaps: 20,
        MaximumStintLaps: 8,
        EstimatedPitLossSeconds: 25));
Assert(configuredStrategy.EstimatedLapsToFinish == 20,
    "Manual remaining laps must override automatic estimation.");
Assert(configuredStrategy.EstimatedPitStops == 2,
    "Maximum stint length must produce a multi-stop plan.");
Assert(configuredStrategy.EstimatedTotalPitLossSeconds == 50,
    "Pit-loss projection must include every planned stop.");
Assert(configuredStrategy.FlagScenario.Length > 0 &&
       configuredStrategy.WeatherScenario.Length > 0 &&
       configuredStrategy.TrafficScenario.Length > 0,
    "Every live strategy must expose the three contingency scenarios.");

var scenarioInput = new EnduranceStrategyInput(
    CompletedLaps: 10,
    RemainingLaps: 30,
    CurrentFuelRangeLaps: 8,
    MaximumFuelStintLaps: 12,
    ConfiguredMaximumStintLaps: 12,
    ReferencePaceSeconds: 120,
    PaceDegradationSecondsPerLap: 0.02,
    ConsumptionLitersPerLap: 2.5,
    FuelCapacityLiters: 30,
    ReserveFuelLiters: 2.5,
    PitLossSeconds: 30,
    TireChangeSeconds: 15,
    CurrentMaximumTireWearFraction: 0.2,
    TireWearFractionPerLap: 0.025,
    TireWearLimitFraction: 0.7,
    AvailableTireSets: 3);
var optimizedPlan = EnduranceStrategyPlanner.Calculate(scenarioInput);
Assert(optimizedPlan.Available, "A feasible endurance strategy must be produced.");
Assert(optimizedPlan.StintLaps.Sum() == 30,
    "Strategy stints must cover every remaining lap exactly once.");
Assert(optimizedPlan.PitLaps.Count == optimizedPlan.Stops,
    "Every planned stop must expose its race lap.");
Assert(optimizedPlan.TireSets <= 3,
    "Strategy must respect the configured tire allocation.");
var scenarioAdvice = RaceScenarioAdvisor.Calculate(
    scenarioInput,
    optimizedPlan,
    new(
        LmuGamePhase.FullCourseYellow,
        RainIntensity: 0.6,
        TrackWetness: 0.7,
        RainTrendPerSample: 0.01,
        GapAheadSeconds: 0.8,
        GapBehindSeconds: 2.5,
        CompletedLaps: 10,
        LapsUntilPit: 2,
        SuggestedPitLap: 12,
        CurrentMaximumTireWearFraction: 0.62,
        TireWearLimitFraction: 0.7,
        TireCompound: "SOFT"));
Assert(scenarioAdvice.FlagState.Contains("KEEP GREEN-PACE PIT PLAN", StringComparison.Ordinal),
    "A yellow flag must not imply a Safety Car, reduced speed or discounted pit loss.");
Assert(!scenarioAdvice.FlagState.Contains("BOX", StringComparison.Ordinal),
    "A yellow flag alone must not trigger a pit recommendation.");
Assert(scenarioAdvice.Weather.Contains("WET WINDOW", StringComparison.Ordinal),
    "Heavy rain and wetness must expose the wet-tire scenario.");
Assert(scenarioAdvice.Traffic.Contains("UNDERCUT", StringComparison.Ordinal),
    "Close traffic ahead must expose an undercut scenario.");

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

var sectorTracker = new SectorReferenceTracker();
var noSectorReference = default(DashboardSectorTimes);
var outLap = raceSnapshot with
{
    ScoringSequence = 20,
    Player = player with { LapNumber = 0, CurrentSector = 1 },
    Standings = new[] { Standing(1, "Player", 1, 0, true, true, 0) },
};
_ = sectorTracker.Update(outLap, noSectorReference);
var outLapAfterPit = outLap with
{
    Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
};
_ = sectorTracker.Update(outLapAfterPit, noSectorReference);
var enteredSector2 = outLapAfterPit with
{
    ScoringSequence = 21,
    Player = outLapAfterPit.Player! with { CurrentSector = 2 },
};
var references = sectorTracker.Update(
    enteredSector2,
    noSectorReference with { CurrentSector1Seconds = 30 });
Assert(references.BestSector1Seconds == 0,
    "Pit-contaminated out-lap sector 1 must not become a reference.");
var enteredSector3 = enteredSector2 with
{
    ScoringSequence = 22,
    Player = enteredSector2.Player! with { CurrentSector = 0 },
};
references = sectorTracker.Update(
    enteredSector3,
    noSectorReference with
    {
        CurrentSector1Seconds = 30,
        CurrentSector2Seconds = 40,
    });
Assert(references.BestSector2Seconds == 40,
    $"A clean out-lap sector 2 must seed the first-lap reference (actual {references.BestSector2Seconds}).");
var startedFlyingLap = enteredSector3 with
{
    ScoringSequence = 23,
    Player = enteredSector3.Player! with { LapNumber = 1, CurrentSector = 1 },
};
references = sectorTracker.Update(
    startedFlyingLap,
    noSectorReference with { LastSector3Seconds = 50 });
Assert(references.BestSector3Seconds == 50,
    $"A clean out-lap sector 3 must seed the first-lap reference (actual {references.BestSector3Seconds}).");
var completedFirstFlyingSector1 = startedFlyingLap with
{
    ScoringSequence = 24,
    Player = startedFlyingLap.Player! with { CurrentSector = 2 },
};
references = sectorTracker.Update(
    completedFirstFlyingSector1,
    noSectorReference with { CurrentSector1Seconds = 31 });
Assert(references.BestSector1Seconds == 31,
    "Sector 1 must wait for the first clean complete sector when pit exit contaminated the out lap.");

var clockTracker = new SectorReferenceTracker();
var clockOutLap = outLap with
{
    Player = outLap.Player! with
    {
        LapStartElapsedTime = 1_000,
        ElapsedTime = 1_005,
    },
};
_ = clockTracker.Update(clockOutLap, default);
var clockCleanSector1 = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with { ElapsedTime = 1_010 },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
var clockSector2 = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 2,
            ElapsedTime = 1_030,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector2.BestSector1Seconds == 0,
    "Telemetry clock must preserve pit contamination for out-lap sector 1.");
var clockSector3 = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 0,
            ElapsedTime = 1_070,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector3.BestSector2Seconds == 40,
    "Telemetry clock must capture clean out-lap sector 2 without waiting for scoring.");
_ = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 0,
            ElapsedTime = 1_120,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
var clockFlyingLap = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            LapNumber = 1,
            CurrentSector = 1,
            LapStartElapsedTime = 1_120,
            ElapsedTime = 1_120.02,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 1) },
    },
    default);
Assert(clockFlyingLap.BestSector3Seconds == 50,
    "Telemetry clock must capture clean out-lap sector 3 at the start line.");

var stableBand = TireTemperatureClassifier.ClassifyStable(
    100.5,
    TireTemperatureBand.Optimal);
Assert(stableBand == TireTemperatureBand.Optimal,
    "Tire colors must not flicker immediately above a threshold.");
stableBand = TireTemperatureClassifier.ClassifyStable(
    102.5,
    TireTemperatureBand.Optimal);
Assert(stableBand == TireTemperatureBand.Hot,
    "Tire colors must change after the hysteresis margin is crossed.");
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
    string vehicleName = "Car",
    double lapDistanceMeters = 100) => new(
        id, driver, vehicleName, "Porsche 963", "Hypercar", position, completedLaps, 1, lapDistanceMeters,
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
