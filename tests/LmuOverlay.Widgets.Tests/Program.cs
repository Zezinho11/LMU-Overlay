using LmuOverlay.Domain;
using LmuOverlay.Widgets;

Assert(VehicleCatalog.Resolve("BMW M4 GT3").Code == "BMW" &&
       VehicleCatalog.Resolve("Ferrari 296 GT3").Code == "FER" &&
       VehicleCatalog.Resolve(string.Empty, "BMW M4 GT3 #32").Code == "BMW" &&
       VehicleCatalog.Resolve("Unknown Prototype").Code == "---",
    "The versioned vehicle catalog must resolve known manufacturers from model or scoring name and fail explicitly.");

var externalCatalogPath = Path.Combine(Path.GetTempPath(), $"vehicle-catalog-{Guid.NewGuid():N}.json");
var tireProfilesPath = Path.Combine(Path.GetTempPath(), $"tire-profiles-{Guid.NewGuid():N}.json");
try
{
    File.WriteAllText(externalCatalogPath,
        "{\"schemaVersion\":1,\"entries\":[{\"tokens\":[\"RED FOX EVO\"]," +
        "\"manufacturer\":\"RedFox\",\"code\":\"RFX\",\"color\":\"#123456\"}]}");
    VehicleCatalog.UseExternalCatalog(externalCatalogPath);
    Assert(VehicleCatalog.Resolve("Red Fox Evo 2026").Code == "RFX",
        "A versioned external vehicle catalog must override unknown future cars.");

    File.WriteAllText(tireProfilesPath,
        "{\"version\":1,\"entries\":[{\"vehicleClass\":\"GT3\",\"vehicleModel\":\"*\"," +
        "\"compound\":\"SOFT\",\"thresholds\":{\"coldToWarming\":50," +
        "\"warmingToOptimal\":65,\"optimalToHot\":85,\"hotToCritical\":100}}]}");
    TireTemperatureProfiles.UseExternalCatalog(tireProfilesPath);
    var profile = TireTemperatureProfiles.Resolve("LMGT3", "Future GT3", "SOFT");
    Assert(TireTemperatureClassifier.Classify(90, profile) == TireTemperatureBand.Hot,
        "Tire temperature thresholds must be configurable by class and compound.");
}
finally
{
    VehicleCatalog.UseExternalCatalog(null);
    TireTemperatureProfiles.UseExternalCatalog(null);
    if (File.Exists(externalCatalogPath)) File.Delete(externalCatalogPath);
    if (File.Exists(tireProfilesPath)) File.Delete(tireProfilesPath);
}

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
    CurrentSector: 0,
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

Assert(dashboard.LapNumber == 5,
    "Dashboard lap must convert telemetry's zero-based lap to LMU HUD numbering.");
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
var synchronizedInputs = EssentialWidgetStateFactory.CreateInputs(
    snapshot with
    {
        Player = player with
        {
            Steering = 0.3,
            VisualSteeringWheelRangeDegrees = 540,
            PhysicalSteeringWheelRangeDegrees = 540,
        },
    },
    directSteeringPosition: 0.18);
Assert(Math.Abs(synchronizedInputs.SteeringWheelRangeDegrees - 900) < 0.001,
    "Direct steering must reconcile controller lock with the active LMU car lock.");
var physicalReferenceInputs = EssentialWidgetStateFactory.CreateInputs(
    snapshot with
    {
        Player = player with
        {
            Steering = 0.3,
            VisualSteeringWheelRangeDegrees = 540,
            PhysicalSteeringWheelRangeDegrees = 600,
        },
    },
    directSteeringPosition: 0.18);
Assert(Math.Abs(physicalReferenceInputs.SteeringWheelRangeDegrees - 1000) < 0.001,
    "The physical LMU range must win over the cockpit visual animation range.");

var bmwShiftLights = new ShiftLightTimingTracker();
var ferrariShiftLights = new ShiftLightTimingTracker();
var bmwLightFraction = bmwShiftLights.Update(player with
{
    VehicleModel = "BMW M4 GT3",
    Gear = 3,
    EngineRpm = 7600,
    EngineMaximumRpm = 8000,
    Throttle = 1,
});
var ferrariLightFraction = ferrariShiftLights.Update(player with
{
    VehicleModel = "Ferrari 296 GT3",
    Gear = 3,
    EngineRpm = 7600,
    EngineMaximumRpm = 8000,
    Throttle = 1,
});
Assert(bmwLightFraction > ferrariLightFraction,
    "Different vehicle shift profiles must not illuminate at one generic RPM fraction.");
var adaptiveShiftLights = new ShiftLightTimingTracker();
_ = adaptiveShiftLights.Update(player with
{
    VehicleModel = "Test Car",
    Gear = 2,
    EngineRpm = 7200,
    EngineMaximumRpm = 8000,
    Throttle = 1,
});
_ = adaptiveShiftLights.Update(player with
{
    VehicleModel = "Test Car",
    Gear = 3,
    EngineRpm = 6100,
    EngineMaximumRpm = 8000,
    Throttle = 1,
});
_ = adaptiveShiftLights.Update(player with
{
    VehicleModel = "Test Car",
    Gear = 2,
    EngineRpm = 6500,
    EngineMaximumRpm = 8000,
    Throttle = 1,
});
Assert(adaptiveShiftLights.Update(player with
{
    VehicleModel = "Test Car",
    Gear = 2,
    EngineRpm = 7200,
    EngineMaximumRpm = 8000,
    Throttle = 1,
}) > 0.99,
    "A clean upshift must refine the light target for that vehicle and gear.");

var standings = new[]
{
    Standing(1, "Leader", 1, 0, false, false, lapDistanceMeters: 700),
    Standing(2, "John Suzuki", 2, 3.2, true, false,
        vehicleName: "Porsche 963 #6", lapDistanceMeters: 1_000) with
    {
        BestSector1Seconds = 30,
        BestSector2CumulativeSeconds = 70,
    },
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
Assert(string.IsNullOrEmpty(relative.SessionName) && relative.SessionRemainingSeconds == 0,
    "Relative must not carry a redundant session header or clock.");
var officialRelative = EssentialWidgetStateFactory.CreateRelative(raceSnapshot with
{
    Player = raceSnapshot.Player! with
    {
        GapToCarAheadSeconds = 2.4,
        GapToCarBehindSeconds = 3.1,
    },
});
Assert(officialRelative.Rows[0].GapSource == RelativeGapSource.OfficialAhead &&
       Math.Abs(officialRelative.Rows[0].RelativeGapSeconds + 2.4) < 0.0001 &&
       officialRelative.Rows[2].GapSource == RelativeGapSource.OfficialBehind &&
       Math.Abs(officialRelative.Rows[2].RelativeGapSeconds - 3.1) < 0.0001,
    "Relative must prefer LMU's official immediate ahead/behind gaps.");
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
Assert(raceDashboard.OptimalLapTimeSeconds == 120,
    "Optimal lap must sum the official best sector values.");
Assert(raceDashboard.SpeedLimiterActive,
    "Active pit limiter must be exposed to the dashboard renderer.");
Assert(liveStandings.Classes[0].Rows[0].DriverAbbreviation == "LEA",
    "Standings must expose compact driver abbreviations.");
Assert(liveStandings.Classes[0].Rows[0].VehicleModel == "Porsche 963",
    "Standings must preserve the telemetry vehicle model for manufacturer badges.");
var tireFuelRow = EssentialWidgetStateFactory.CreateLiveStandings(
    raceSnapshot with
    {
        Standings = standings.Select(item => item with
        {
            VirtualEnergyFraction = 0.78,
            FrontTireCompound = "Medium",
            RearTireCompound = "Medium",
            FrontTireCompoundIndex = 2,
        }).ToArray(),
    }).Classes[0].Rows[0];
Assert(Math.Abs(tireFuelRow.VirtualEnergyFraction - 0.78) < 0.0001 &&
       tireFuelRow.TireCompound == "Medium" &&
       tireFuelRow.TireCompoundIndex == 2,
    "Standings must expose each car's official Virtual Energy and tire compound.");
var timingTracker = new TimingWidgetTracker();
var metadataSnapshot = raceSnapshot with
{
    ScoringSequence = 10,
    Standings = standings.Select(item => item with
    {
        VehicleModel = "BMW M4 GT3",
        VirtualEnergyFraction = 0.78,
        FrontTireCompound = "Medium",
        RearTireCompound = "Medium",
    }).ToArray(),
};
timingTracker.Update(metadataSnapshot, 12, 4);
var temporarilyMissing = timingTracker.Update(metadataSnapshot with
{
    ScoringSequence = 11,
    CapturedAt = metadataSnapshot.CapturedAt.AddMilliseconds(100),
    Standings = metadataSnapshot.Standings.Select(item => item with
    {
        VehicleModel = string.Empty,
        VirtualEnergyFraction = -1,
        FrontTireCompound = string.Empty,
        RearTireCompound = string.Empty,
    }).ToArray(),
}, 12, 4).Standings;
Assert(temporarilyMissing.Classes.SelectMany(group => group.Rows).All(row =>
        row.VehicleModel == "BMW M4 GT3" &&
        Math.Abs(row.VirtualEnergyFraction - 0.78) < 0.0001),
    "Short metadata gaps may reuse data only for the exact same timing identity.");
var reusedSlot = timingTracker.Update(metadataSnapshot with
{
    ScoringSequence = 12,
    CapturedAt = metadataSnapshot.CapturedAt.AddMilliseconds(200),
    Standings = metadataSnapshot.Standings.Select((item, index) => index == 0
        ? item with
        {
            DriverName = "Replacement Driver",
            VehicleName = "Replacement",
            VehicleModel = string.Empty,
            VirtualEnergyFraction = -1,
        }
        : item).ToArray(),
}, 12, 4).Standings;
var replacement = reusedSlot.Classes.SelectMany(group => group.Rows)
    .Single(row => row.DriverName == "Replacement Driver");
Assert(replacement.VehicleModel == string.Empty && replacement.VirtualEnergyFraction < 0,
    "A reused timing slot must never inherit the previous driver's metadata.");
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
Assert(compactRows.Count == 12,
    "The wider standings panel must use all twelve rows below its session header.");
Assert(compactRows[0].ClassPosition == 1, "The class leader must always remain visible.");
Assert(compactRows.Any(row => row.IsPlayer), "The moving window must always include the player.");
Assert(compactRows.Single(row => row.IsPlayer).DriverAbbreviation == "COS",
    "Driver abbreviation must use the final name component.");
Assert(compactRows.Single(row => row.IsPlayer).CarNumber == "6",
    "Explicit race numbers must be extracted from the official vehicle name.");
var sixRowStandings = EssentialWidgetStateFactory.CreateLiveStandings(
    raceSnapshot with { Standings = deepField },
    6);
Assert(sixRowStandings.Classes.Sum(group => group.Rows.Count) == 6 &&
       sixRowStandings.Classes[0].Rows.Any(row => row.IsPlayer),
    "The configurable standings limit must reduce the tower without hiding the player.");

var multiclassField = deepField
    .Select(row => row with { VehicleClass = "GT3" })
    .Concat(new[]
    {
        Standing(101, "Hyper Leader", 16, 0, false, false) with
        {
            VehicleClass = "Hypercar",
            VirtualEnergyFraction = 0.64,
        },
        Standing(102, "LMP2 Leader", 17, 0, false, false) with
        {
            VehicleClass = "LMP2",
            VirtualEnergyFraction = 0.52,
        },
    })
    .ToArray();
var multiclassStandings = EssentialWidgetStateFactory.CreateLiveStandings(
    raceSnapshot with { Standings = multiclassField });
Assert(multiclassStandings.Classes.Sum(group => group.Rows.Count) == 11,
    "Three-class standings must fill the tower without overflowing.");
Assert(multiclassStandings.Classes.Single(group => group.IsPlayerClass).Rows.Count == 9,
    "Spare multiclass rows must belong to the player's class.");
Assert(multiclassStandings.Classes
        .Where(group => !group.IsPlayerClass)
        .Select(group => group.Rows.Single().VirtualEnergyFraction)
        .OrderBy(value => value)
        .SequenceEqual(new[] { 0.52, 0.64 }),
    "Every other-class P1 must carry that car's own Virtual Energy value.");

var qualifyingSnapshot = raceSnapshot with
{
    Session = session with { Kind = LmuSessionKind.Qualifying },
    Standings = new[]
    {
        standings[0] with { BestLapTimeSeconds = 100.000 },
        standings[1] with { BestLapTimeSeconds = 100.500 },
        standings[2] with { BestLapTimeSeconds = 101.200 },
    },
};
var qualifyingStandings = EssentialWidgetStateFactory.CreateLiveStandings(
    qualifyingSnapshot);
Assert(qualifyingStandings.IsQualifying &&
       qualifyingStandings.SessionName == "QUALIFYING" &&
       qualifyingStandings.SessionRemainingSeconds == 3600,
    "Timing panels must expose the official session type and remaining clock.");
Assert(Math.Abs(qualifyingStandings.Classes[0].Rows[1].IntervalSeconds - 0.5) < 0.0001,
    "Qualifying interval must be the best-lap difference to the class leader.");
Assert(qualifyingStandings.Classes[0].Rows[1].LastLapTimeSeconds == 100.5,
    "Qualifying lap column must display each driver's best lap.");

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
Assert(strategy.RequiredFuelSavingFraction == 0,
    "A planned refuel must not be reported as a whole-race fuel-saving deficit.");
Assert(strategy.Confidence == "LOW",
    "One valid lap must produce low-confidence guidance.");
Assert(strategy.EstimatedRangeTimeSeconds == strategy.EstimatedRangeLaps * 121,
    "Fuel range time must use the latest valid lap.");
Assert(strategy.EstimatedTimeToFinishSeconds == 15 * 121,
    "Finish time must use the same reference lap.");
Assert(strategy.Status == "MARGINAL",
    "A feasible one-stop strategy must assess the current stint, not the whole race tank.");
Assert(refueled.Samples == 1, "Refueling must not be recorded as negative consumption.");

var tireLearningTracker = new FuelStrategyTracker();
var tireLearningStart = raceSnapshot with
{
    Player = player with
    {
        FuelLiters = 50,
        TireWear = new(0.99, 0.99, 0.99, 0.99),
    },
};
var tireOptions = new FuelStrategyOptions(
    ManualRemainingLaps: 50,
    MaximumStintLaps: 10);
_ = tireLearningTracker.Update(tireLearningStart, tireOptions);
FuelStrategyWidgetState tireProjection = default!;
for (var lap = 1; lap <= 3; lap++)
{
    tireProjection = tireLearningTracker.Update(tireLearningStart with
    {
        CapturedAt = tireLearningStart.CapturedAt.AddMinutes(lap * 2),
        Player = tireLearningStart.Player! with
        {
            LapNumber = 4 + lap,
            FuelLiters = 50 - lap * 2.5,
            TireWear = new(
                0.99 - lap * 0.01,
                0.99 - lap * 0.012,
                0.99 - lap * 0.01,
                0.99 - lap * 0.012),
        },
        Standings = new[]
        {
            standings[0],
            standings[1] with
            {
                CompletedLaps = 4 + lap,
                LastLapTimeSeconds = 121,
            },
            standings[2],
        },
    }, tireOptions);
}
Assert(tireProjection.AverageTireWearFractionPerLap > 0 &&
       !tireProjection.TirePlan.Contains("LEARNING", StringComparison.Ordinal),
    "Three falling LMU tire-life samples must unlock the complete tire schedule.");

var versionTracker = new FuelStrategyTracker();
_ = versionTracker.Update(raceSnapshot);
Assert(versionTracker.Update(nextLapSnapshot).Samples == 1,
    "A stable physics generation must retain valid learning samples.");
var versionReset = versionTracker.Update(nextLapSnapshot with
{
    GameVersion = nextLapSnapshot.GameVersion + 1,
});
Assert(versionReset.Learning && versionReset.Samples == 0,
    "A game/physics version change must quarantine prior strategy learning.");
var contaminatedTracker = new FuelStrategyTracker();
var garageSnapshot = raceSnapshot with
{
    Player = player with { LapNumber = 0, FuelLiters = 48.56, VirtualEnergy = 1 },
    Standings = new[] { Standing(2, "Player", 1, 0, true, true, 0) },
};
_ = contaminatedTracker.Update(garageSnapshot);
_ = contaminatedTracker.Update(garageSnapshot with
{
    CapturedAt = garageSnapshot.CapturedAt.AddMilliseconds(100),
    Player = garageSnapshot.Player! with { FuelLiters = 21.7, VirtualEnergy = 0.21 },
});
var rejectedOutLap = contaminatedTracker.Update(garageSnapshot with
{
    CapturedAt = garageSnapshot.CapturedAt.AddMinutes(3),
    Player = garageSnapshot.Player! with
    {
        LapNumber = 1,
        FuelLiters = 20.1,
        VirtualEnergy = 0.18,
    },
    Standings = new[]
    {
        Standing(2, "Player", 1, 0, true, false, 1) with
        {
            LastLapTimeSeconds = 121,
        },
    },
});
Assert(rejectedOutLap.Learning && rejectedOutLap.Samples == 0,
    "Garage fuel changes and the out lap must never become consumption samples.");
var firstCleanFuelLap = contaminatedTracker.Update(garageSnapshot with
{
    CapturedAt = garageSnapshot.CapturedAt.AddMinutes(5),
    Player = garageSnapshot.Player! with
    {
        LapNumber = 2,
        FuelLiters = 17.6,
        VirtualEnergy = 0.10,
    },
    Standings = new[]
    {
        Standing(2, "Player", 1, 0, true, false, 2) with
        {
            LastLapTimeSeconds = 120,
        },
    },
});
Assert(firstCleanFuelLap.Samples == 1 &&
       Math.Abs(firstCleanFuelLap.AverageConsumptionLitersPerLap - 2.5) < 0.0001,
    "The first complete flying lap must establish consumption after the out lap.");
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
var probabilistic = StrategyScenarioSimulator.Simulate(new(
    optimizedPlan, scenarioInput.RemainingLaps, 50,
    scenarioInput.ConsumptionLitersPerLap, 0.03,
    scenarioInput.ReferencePaceSeconds, 0.4, 2,
    scenarioInput.ReserveFuelLiters, 42, 512));
var probabilisticReplay = StrategyScenarioSimulator.Simulate(new(
    optimizedPlan, scenarioInput.RemainingLaps, 50,
    scenarioInput.ConsumptionLitersPerLap, 0.03,
    scenarioInput.ReferencePaceSeconds, 0.4, 2,
    scenarioInput.ReserveFuelLiters, 42, 512));
Assert(probabilistic.Available && probabilistic == probabilisticReplay &&
       probabilistic.P10TimeSeconds <= probabilistic.MedianTimeSeconds &&
       probabilistic.MedianTimeSeconds <= probabilistic.P90TimeSeconds,
    "Scenario simulation must be deterministic, bounded and report probability ranges.");
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

var enduranceSession = timedSession with
{
    CurrentElapsedTime = 0,
    EndElapsedTime = 6 * 60 * 60,
};
var enduranceStart = raceSnapshot with
{
    Session = enduranceSession,
    Player = player with { FuelLiters = 25 },
};
var enduranceTracker = new FuelStrategyTracker();
_ = enduranceTracker.Update(enduranceStart);
var enduranceStrategy = enduranceTracker.Update(enduranceStart with
{
    Player = player with { FuelLiters = 22.5 },
    Standings = new[]
    {
        standings[0],
        standings[1] with { CompletedLaps = standings[1].CompletedLaps + 1 },
        standings[2],
    },
});
Assert(enduranceStrategy.EstimatedLapsToFinish > 170,
    "A six-hour timed session must project the full duration from the live pace.");
Assert(enduranceStrategy.EstimatedPitStops is >= 4 and <= 6,
    "A short opening fill must be followed by tank-capacity stints, not repeated short stints.");
Assert(enduranceStrategy.FuelToAddLiters > 50 &&
       enduranceStrategy.FuelToAddLiters <= player.FuelCapacityLiters,
    "Fuel to add must describe the next stop and stay within tank capacity.");
Assert(enduranceStrategy.RequiredFuelSavingFraction == 0,
    "A feasible multi-stop race must not request saving enough to finish on the current tank.");

var practiceSession = enduranceSession with { Kind = LmuSessionKind.Practice };
var practiceTracker = new FuelStrategyTracker();
_ = practiceTracker.Update(enduranceStart with { Session = practiceSession });
var practiceStrategy = practiceTracker.Update(enduranceStart with
{
    Session = practiceSession,
    Player = player with { FuelLiters = 22.5 },
    Standings = new[]
    {
        standings[0],
        standings[1] with { CompletedLaps = standings[1].CompletedLaps + 1 },
        standings[2],
    },
});
Assert(practiceStrategy.EstimatedPitStops > 0 &&
       practiceStrategy.PlanSummary.StartsWith("FULL PUSH", StringComparison.Ordinal),
    "Practice must retain the complete planning simulation for event preparation.");
Assert(Math.Abs(practiceStrategy.EstimatedStrategyTimeSeconds - 6 * 60 * 60) < 0.001,
    "A timed practice plan must display the actual remaining session duration, not accumulated pace trend.");

var finalSplashInput = new EnduranceStrategyInput(
    CompletedLaps: 0,
    RemainingLaps: 100,
    CurrentFuelRangeLaps: 10,
    MaximumFuelStintLaps: 20,
    ConfiguredMaximumStintLaps: 0,
    ReferencePaceSeconds: 90,
    PaceDegradationSecondsPerLap: 0,
    ConsumptionLitersPerLap: 3,
    FuelCapacityLiters: 60,
    ReserveFuelLiters: 6,
    PitLossSeconds: 30,
    TireChangeSeconds: 15,
    CurrentMaximumTireWearFraction: 0.15,
    TireWearFractionPerLap: 0,
    TireWearLimitFraction: 0.7,
    AvailableTireSets: 0);
var finalSplash = EnduranceStrategyPlanner.Calculate(finalSplashInput);
Assert(finalSplash.StintLaps.SequenceEqual(new[] { 10, 20, 20, 20, 20, 10 }),
    "Full-push planning must front-load full stints and leave a short final stint.");
Assert(finalSplash.FuelAtStopsLiters[^1] == 36 &&
       finalSplash.FuelAtStopsLiters[^1] < finalSplashInput.FuelCapacityLiters,
    "The final stop must add only the final-stint fuel plus the configured two-lap reserve.");
var fuelSavePlan = EnduranceStrategyPlanner.CalculateFuelSave(
    finalSplashInput,
    finalSplash,
    currentFuelLiters: 36);
Assert(fuelSavePlan.Available && fuelSavePlan.SavingFraction is > 0 and <= 0.15,
    "The alternative box must provide a feasible bounded fuel-save target.");
Assert(fuelSavePlan.Strategy.Stops <= finalSplash.Stops,
    "Fuel Save must never show more stops than Full Push.");
var sameStopFuelSaveBase = EnduranceStrategyPlanner.Calculate(finalSplashInput with
{
    RemainingLaps = 30,
});
var sameStopFuelSave = EnduranceStrategyPlanner.CalculateFuelSave(
    finalSplashInput with { RemainingLaps = 30 },
    sameStopFuelSaveBase,
    currentFuelLiters: 36);
Assert(sameStopFuelSave.Available &&
       sameStopFuelSave.Strategy.Stops <= sameStopFuelSaveBase.Stops,
    "Fuel Save must remain available as a go-long plan when it cannot remove a stop.");
Assert(!fuelSavePlan.PitPlan.Contains("=>", StringComparison.Ordinal) &&
       fuelSavePlan.TirePlan.Length > 0,
    "Fuel-save guidance must keep the pit sequence compact and include its tire plan.");
Assert(fuelSavePlan.StintInstructions.Count > 0 &&
       fuelSavePlan.StintInstructions.All(item =>
           item.SaveTargetLaps is >= 1 and <= 3) &&
       fuelSavePlan.SaveLapPlan.Contains("SAVE TARGET", StringComparison.Ordinal),
    "Fuel Save must state LMU-compatible +1/+2/+3 lap targets for the stints that need saving.");
Assert(!fuelSavePlan.Summary.Contains('%') &&
       !fuelSavePlan.SaveLapPlan.Contains('('),
    "Fuel Save presentation must show operational MFD lap targets, not a percentage or save-lap count.");
Assert(fuelSavePlan.SaveLapPlan.Length < 90,
    "Repeated Fuel Save stint targets must be grouped so the plan fits the overlay.");

var healthyTires = EnduranceStrategyPlanner.Calculate(finalSplashInput with
{
    RemainingLaps = 40,
    TireWearFractionPerLap = 0.01,
});
Assert(healthyTires.TireStops.All(stop => stop.Tires.Count is 0 or 2),
    "Normal endurance service must rotate one tire side instead of heating all four at once.");

var manualCalculator = new FuelStrategyTracker().Update(
    raceSnapshot,
    new FuelStrategyOptions(
        ManualRemainingMinutes: 60,
        ManualLapTimeSeconds: 120,
        ManualFuelPerLapLiters: 3,
        ManualFuelCapacityLiters: 60));
Assert(manualCalculator.EstimatedLapsToFinish == 30,
    "Manual duration and lap time must produce a deterministic remaining-lap estimate.");
Assert(manualCalculator.ProjectedConsumptionLitersPerLap == 3 &&
       manualCalculator.Confidence == "MANUAL" &&
       !manualCalculator.Learning,
    "Manual fuel inputs must work before automatic lap sampling is complete.");
Assert(manualCalculator.EstimatedPitStops > 0,
    "Manual tank capacity must constrain the generated stint plan.");

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
var fullAllocation = new FuelStrategyTracker().Update(raceSnapshot with
{
    Player = player with
    {
        FuelLiters = 88,
        FuelCapacityLiters = 120,
        VirtualEnergy = 1,
    },
});
Assert(fullAllocation.EffectiveFuelCapacityLiters == 88,
    "At 100% NRG, an observed 88 L allocation must override a 120 L physical tank.");
_ = energyTracker.Update(raceSnapshot with
{
    Player = player with { FuelLiters = 31.25, VirtualEnergy = 1 },
});
var energyStrategy = energyTracker.Update(nextLapSnapshot with
{
    Player = player with { FuelLiters = 28.75, VirtualEnergy = 0.92 },
});
Assert(
    Math.Abs(energyStrategy.AverageVirtualEnergyFractionPerLap - 0.08) < 0.0001,
    "Virtual Energy use must be sampled per lap.");
Assert(
    Math.Abs(energyStrategy.EstimatedVirtualEnergyRangeLaps - 11.5) < 0.0001,
    "Virtual Energy range must use its rolling consumption.");
Assert(
    Math.Abs(energyStrategy.RequiredVirtualEnergyFraction - 0.88) < 0.0001,
    "Virtual Energy need must cover the resource-constrained current stint and its reserve lap.");
Assert(
    Math.Abs(energyStrategy.VirtualEnergyMarginFraction - 0.04) < 0.0001,
    "Virtual Energy margin must compare current energy with the stint need.");
Assert(
    Math.Abs(energyStrategy.EffectiveFuelCapacityLiters - 31.25) < 0.0001,
    "A 100% NRG observation must publish the live balanced fuel allocation.");
Assert(energyStrategy.EffectiveFuelCapacityLiters >= energyStrategy.FuelLiters,
    "A learned maximum must never be lower than fuel currently in the car.");
Assert(energyStrategy.PitPlan.Split('·')
        .Where(item => item.Contains('+'))
        .All(item => !item.Contains("+100.0L", StringComparison.Ordinal)),
    "NRG-limited pit fills must not blindly fill the physical tank.");

var asymmetricTires = EnduranceStrategyPlanner.Calculate(finalSplashInput with
{
    RemainingLaps = 15,
    CurrentFuelRangeLaps = 5,
    MaximumFuelStintLaps = 10,
    FuelCapacityLiters = 30,
    CurrentMaximumTireWearFraction = 0.6,
    TireWearFractionPerLap = 0.01,
    CurrentTireWear = new(0.10, 0.60, 0.10, 0.60),
    TireWearPerLap = new(0.01, 0.01, 0.01, 0.01),
});
Assert(asymmetricTires.TireStops.Count > 0 &&
       asymmetricTires.TireStops[0].Tires.SequenceEqual(new[] { "FR", "RR" }),
    "The tire plan must name only the individual corners that cannot complete the next stint.");
Assert(asymmetricTires.TirePlan.Contains("L5 FR+RR", StringComparison.Ordinal),
    "The complete tire schedule must pair the pit lap with the tires to change.");
var groupedTireService = TireStrategyCalculator.ServiceForUpcomingStints(
    new LmuWheelWear(0.10, 0.66, 0.10, 0.56),
    new LmuWheelWear(0.01, 0.01, 0.01, 0.01),
    nextStintLaps: 5,
    followingStintLaps: 10,
    limit: 0.70);
Assert(groupedTireService.SequenceEqual(new[] { "FR", "RR" }),
    "Once service is required, the plan must group a same-side tire that otherwise needs service at the next stop.");
var alternatingTireService = TireStrategyCalculator.ServiceForUpcomingStints(
    new LmuWheelWear(0.45, 0.20, 0.42, 0.18),
    new LmuWheelWear(0.01, 0.01, 0.01, 0.01),
    nextStintLaps: 5,
    followingStintLaps: 5,
    limit: 0.70,
    previousService: new[] { "FR", "RR" });
Assert(alternatingTireService.SequenceEqual(new[] { "FL", "RL" }),
    "Successive tire services must alternate sides so neither pair is retained beyond its two-stint cycle.");
Assert(TireStrategyCalculator.EquivalentAgeLaps(
        new LmuWheelWear(0, 0.5, 0, 0.4),
        new LmuWheelWear(0.01, 0.01, 0.01, 0.01)) == 50,
    "Changing only some corners must not reset the degradation age of every tire.");

var sectorTracker = new SectorReferenceTracker();
var noSectorReference = default(DashboardSectorTimes);
var outLap = raceSnapshot with
{
    ScoringSequence = 20,
    Player = player with { LapNumber = 0, CurrentSector = 0 },
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
    Player = outLapAfterPit.Player! with { CurrentSector = 1 },
};
var references = sectorTracker.Update(
    enteredSector2,
    noSectorReference with { CurrentSector1Seconds = 30 });
Assert(references.BestSector1Seconds == 0,
    "Pit-contaminated out-lap sector 1 must not become a reference.");
var enteredSector3 = enteredSector2 with
{
    ScoringSequence = 22,
    Player = enteredSector2.Player! with { CurrentSector = 2 },
};
references = sectorTracker.Update(
    enteredSector3,
    noSectorReference with
    {
        CurrentSector1Seconds = 30,
        CurrentSector2Seconds = 40,
    });
Assert(references.BestSector2Seconds == 0,
    "Out-lap sectors must not become timing references.");
var startedFlyingLap = enteredSector3 with
{
    ScoringSequence = 23,
    Player = enteredSector3.Player! with { LapNumber = 1, CurrentSector = 0 },
};
references = sectorTracker.Update(
    startedFlyingLap,
    noSectorReference with { LastSector3Seconds = 50 });
Assert(references.BestSector3Seconds == 0,
    "Out-lap sector 3 must not become a timing reference.");

var persistentExitTracker = new SectorReferenceTracker();
var persistentExit = outLap with
{
    Standings = new[]
    {
        Standing(1, "Player", 1, 0, true, false, 0) with
        {
            PitState = LmuPitState.Exiting,
        },
    },
};
_ = persistentExitTracker.Update(persistentExit, noSectorReference);
var persistentExitS2 = persistentExitTracker.Update(
    persistentExit with
    {
        ScoringSequence = 21,
        Player = persistentExit.Player! with { CurrentSector = 1 },
    },
    noSectorReference with { CurrentSector1Seconds = 30 });
Assert(persistentExitS2.BestSector1Seconds == 0,
    "Starting during pit exit must keep only the partial first sector contaminated.");
var persistentExitS3 = persistentExitTracker.Update(
    persistentExit with
    {
        ScoringSequence = 22,
        Player = persistentExit.Player! with { CurrentSector = 2 },
    },
    noSectorReference with { CurrentSector1Seconds = 30, CurrentSector2Seconds = 40 });
Assert(persistentExitS3.BestSector2Seconds == 0,
    "Out-lap sector 2 must remain excluded even after pit exit.");
var persistentExitFlying = persistentExitTracker.Update(
    persistentExit with
    {
        ScoringSequence = 23,
        Player = persistentExit.Player! with { LapNumber = 1, CurrentSector = 0 },
    },
    noSectorReference with { LastSector3Seconds = 50 });
Assert(persistentExitFlying.BestSector3Seconds == 0,
    "Out-lap sector 3 must remain excluded at the start line.");
var completedFirstFlyingSector1 = startedFlyingLap with
{
    ScoringSequence = 24,
    Player = startedFlyingLap.Player! with { CurrentSector = 1 },
};
references = sectorTracker.Update(
    completedFirstFlyingSector1,
    noSectorReference with { CurrentSector1Seconds = 31 });
Assert(references.BestSector1Seconds == 0,
    "The first clean sector 1 must display NEW instead of comparing against itself.");
Assert(sectorTracker.PersistentReferences.Sector1Seconds == 0,
    "A provisional sector must not be persisted before LMU confirms a valid best.");
references = sectorTracker.Update(
    completedFirstFlyingSector1 with
    {
        Player = completedFirstFlyingSector1.Player! with
        {
            LapNumber = 2,
            CurrentSector = 0,
        },
    },
    noSectorReference with { BestSector1Seconds = 31 });
Assert(references.BestSector1Seconds == 31,
    "A clean sector 1 must become active on the following lap.");
Assert(sectorTracker.PersistentReferences.Sector1Seconds == 31,
    "An official clean sector best must be retained for later sessions.");

var invalidTracker = new SectorReferenceTracker();
_ = invalidTracker.Update(
    startedFlyingLap,
    noSectorReference);
var provisionalInvalid = invalidTracker.Update(
    completedFirstFlyingSector1,
    noSectorReference with { CurrentSector1Seconds = 30.5 });
Assert(provisionalInvalid.BestSector1Seconds == 0,
    "A new provisional sector must initially display NEW.");
var invalidated = invalidTracker.Update(
    completedFirstFlyingSector1 with
    {
        Player = completedFirstFlyingSector1.Player! with { LapInvalidated = true },
    },
    noSectorReference);
Assert(invalidated.BestSector1Seconds == 0,
    "A sector from an invalidated lap must not remain a delta reference.");
Assert(invalidated.CurrentSector1Seconds == 0 &&
       invalidated.CurrentSector2Seconds == 0 &&
       invalidated.CurrentSector3Seconds == 0,
    "An invalidated lap must immediately remove all white current-lap sectors.");

var savedTracker = new SectorReferenceTracker();
var savedReferences = savedTracker.Update(
    outLap,
    noSectorReference,
    new SectorReferenceSeed(29.5, 39.5, 49.5));
Assert(savedReferences.BestSector1Seconds == 29.5 &&
       savedReferences.Sector1ReferenceOrigin == SectorReferenceOrigin.Saved,
    "A matching saved personal reference must enable sector 1 on the first flying lap.");

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
            CurrentSector = 1,
            ElapsedTime = 1_030,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector2.BestSector1Seconds == 0,
    "Telemetry clock must preserve pit contamination for out-lap sector 1.");
Assert(clockSector2.CurrentSector1Seconds == 30 &&
       clockSector2.CurrentSector2Seconds == 0,
    "Sector 1 must freeze its completed time while sector 2 starts at zero.");
var clockSector2Live = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 1,
            ElapsedTime = 1_050,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector2Live.CurrentSector2Seconds == 20,
    "Active sector 2 must count continuously from the telemetry clock.");
var clockSector3 = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 2,
            ElapsedTime = 1_070,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector3.BestSector2Seconds == 0,
    "Telemetry clock must not promote out-lap sector 2 to a reference.");
var clockSector3Live = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 2,
            ElapsedTime = 1_085,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 0) },
    },
    default);
Assert(clockSector3Live.CurrentSector1Seconds == 30 &&
       clockSector3Live.CurrentSector2Seconds == 40 &&
       clockSector3Live.CurrentSector3Seconds == 15,
    "S1 and S2 must remain visible while active sector 3 counts continuously.");
_ = clockTracker.Update(
    clockOutLap with
    {
        Player = clockOutLap.Player! with
        {
            CurrentSector = 2,
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
            CurrentSector = 0,
            LapStartElapsedTime = 1_120,
            ElapsedTime = 1_120.02,
        },
        Standings = new[] { Standing(1, "Player", 1, 0, true, false, 1) },
    },
    default);
Assert(clockFlyingLap.BestSector3Seconds == 0,
    "Telemetry clock must leave out-lap sector 3 out of the references.");

var validLapTracker = new SectorReferenceTracker();
var validLapBase = raceSnapshot with
{
    Player = player with
    {
        LapNumber = 1,
        CurrentSector = 0,
        LapStartElapsedTime = 1_000,
        ElapsedTime = 1_005,
    },
    Standings = new[] { Standing(1, "Player", 1, 0, true, false, 1) },
};
_ = validLapTracker.Update(validLapBase, default);
_ = validLapTracker.Update(
    validLapBase with
    {
        Player = validLapBase.Player! with
        {
            CurrentSector = 1,
            ElapsedTime = 1_030,
        },
    },
    default);
_ = validLapTracker.Update(
    validLapBase with
    {
        Player = validLapBase.Player! with
        {
            CurrentSector = 2,
            ElapsedTime = 1_070,
        },
    },
    default);
_ = validLapTracker.Update(
    validLapBase with
    {
        Player = validLapBase.Player! with
        {
            CurrentSector = 2,
            ElapsedTime = 1_120,
        },
    },
    default);
_ = validLapTracker.Update(
    validLapBase with
    {
        Player = validLapBase.Player! with
        {
            LapNumber = 2,
            CurrentSector = 0,
            LapStartElapsedTime = 1_120,
            ElapsedTime = 1_120.02,
        },
    },
    default);
_ = validLapTracker.Update(
    validLapBase with
    {
        ScoringSequence = validLapBase.ScoringSequence + 1,
        Player = validLapBase.Player! with
        {
            LapNumber = 2,
            CurrentSector = 0,
            LapStartElapsedTime = 1_120,
            ElapsedTime = 1_120.1,
        },
        Standings = new[]
        {
            Standing(1, "Player", 1, 0, true, false, 2) with
            {
                LastLapTimeSeconds = 120,
            },
        },
    },
    default(DashboardSectorTimes) with
    {
        LastSector1Seconds = 30,
        LastSector2Seconds = 40,
        LastSector3Seconds = 50,
    });
Assert(validLapTracker.PersistentReferences == new SectorReferenceSeed(30, 40, 50),
    "A complete valid lap must persist exact S1, S2 and S3 segment times.");
Assert(validLapTracker.LastCompletedValidLap == new PersonalBestLap(120, 30, 40, 50),
    "A personal-best candidate must retain all sectors from the same valid lap.");

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
Assert(SteeringWheelRotation.ResolveRangeDegrees(900, 540) == 900,
    "Physical steering range must take priority over the visual range.");
Assert(SteeringWheelRotation.ResolveRangeDegrees(0, 540) == 540,
    "Visual steering range must be used when physical range is unavailable.");
Assert(SteeringWheelRotation.ResolveDisplayRangeDegrees(540, 900) == 540,
    "The Inputs overlay must use the car's visual steering range for LMU's normalized axis.");
Assert(SteeringWheelRotation.ResolveDisplayRangeDegrees(540, 540) == 540,
    "Matching LMU visual and physical ranges must remain one-to-one.");
Assert(SteeringWheelRotation.AngleDegrees(
        0.5,
        SteeringWheelRotation.ResolveDisplayRangeDegrees(540, 900)) == 135,
    "A half-axis input must match the car steering animation instead of the device capability.");
Assert(SteeringWheelRotation.ResolveDisplayRangeDegrees(540, 900, 720) == 720,
    "A valid per-profile manual calibration must override unreliable LMU ranges.");
Assert(SteeringWheelRotation.ResolvePhysicalRangeDegrees(900, 540) == 900,
    "Raw Windows steering input must use the physical device range.");
Assert(SteeringWheelRotation.AngleDegrees(0.5, 900) == 225,
    "Normalized steering must map to the real half-range angle.");
Assert(SteeringWheelRotation.AngleDegrees(-2, 540) == -270,
    "Steering rendering must clamp malformed input without exceeding full lock.");
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
