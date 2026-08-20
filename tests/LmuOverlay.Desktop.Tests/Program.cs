using LmuOverlay.Desktop;
using LmuOverlay.Widgets;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "lmu-overlay-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var defaultWheel = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "LmuOverlay.DirectX", "Assets", "steering-wheel.png"));
    var importedWheel = SteeringWheelAssetStore.Import(
        defaultWheel,
        Path.Combine(root, "wheel-assets"));
    Assert(File.Exists(importedWheel),
        "A valid PNG steering icon must be normalized and persisted.");
    Assert(new FileInfo(Path.ChangeExtension(importedWheel, ".bgra")).Length == 512 * 512 * 4,
        "Desktop must receive an exact 512x512 premultiplied GPU cache.");

    using (var timingHistory = JsonDocument.Parse(
        """{"42":[{"sectorTime1":1,"sectorTime2":2,"lapTime":3,"valid":false},{"sectorTime1":30,"sectorTime2":70,"lapTime":120,"valid":true},{"sectorTime1":29,"sectorTime2":71,"lapTime":119,"valid":true}]}"""))
    {
        Assert(
            Math.Abs(OfficialTimingOptimalProvider.ParseOptimal(
                timingHistory.RootElement,
                42) - 117) < 0.0001,
            "Optimal must use the same per-sector history calculation as LMU Timing.");
        Assert(OfficialTimingOptimalProvider.HasSupportedSchema(
                timingHistory.RootElement,
                42),
            "The official timing adapter must accept the known LMU history schema.");
    }
    using (var incompatibleHistory = JsonDocument.Parse(
        """{"42":[{"sectorOne":30,"sectorTwo":40,"total":120}]}"""))
    {
        Assert(!OfficialTimingOptimalProvider.HasSupportedSchema(
                incompatibleHistory.RootElement,
                42),
            "Unknown timing schemas must be rejected instead of contaminating Optimal.");
    }
    var hardenedOptions = new OfficialTimingOptimalOptions
    {
        BaseAddress = new Uri("https://example.com/"),
        Timeout = TimeSpan.FromSeconds(30),
        PollInterval = TimeSpan.FromMilliseconds(1),
    }.Sanitize();
    Assert(hardenedOptions.BaseAddress.IsLoopback &&
           hardenedOptions.BaseAddress.Scheme == Uri.UriSchemeHttp &&
           hardenedOptions.Timeout <= TimeSpan.FromSeconds(2) &&
           hardenedOptions.PollInterval >= TimeSpan.FromMilliseconds(250),
        "The optional HTTP adapter must remain loopback-only and bounded.");

    Assert(OfficialTimingOptimalProvider.IsNewSession(
            true, "Monza", 1, 42, 600, "Laguna Seca", 1, 42, 5),
        "Changing track must start a new optimal-timing generation even when session code and vehicle id are reused.");
    Assert(OfficialTimingOptimalProvider.IsNewSession(
            true, "Monza", 1, 42, 600, "Monza", 1, 42, 2),
        "Restarting the same session type on the same track must clear the previous optimal.");
    Assert(!OfficialTimingOptimalProvider.IsNewSession(
            true, "Monza", 1, 42, 600, "Monza", 1, 42, 601),
        "Normal elapsed-time progression must preserve the current session optimal.");
    using (var crossTrackHistory = JsonDocument.Parse(
        """{"42":[{"sectorTime1":36.827,"sectorTime2":75.012,"lapTime":112.721,"valid":true},{"sectorTime1":24.2,"sectorTime2":51.5,"lapTime":83.8,"valid":true}]}"""))
    {
        var laps = crossTrackHistory.RootElement.GetProperty("42");
        var monzaHistory = new HashSet<string>(StringComparer.Ordinal)
        {
            laps[0].GetRawText(),
        };
        Assert(Math.Abs(OfficialTimingOptimalProvider.ParseOptimal(
                crossTrackHistory.RootElement,
                42,
                monzaHistory) - 83.8) < 0.0001,
            "A new track optimal must exclude every lap quarantined from the previous track.");
    }

    var path = Path.Combine(root, "layout.json");
    var store = new LayoutStore(path);
    Assert(store.Load() == LayoutProfile.Default, "Missing profile must load defaults.");
    Assert(store.ActiveProfileName == LayoutStore.DefaultProfileName, "Default profile must be active.");
    Assert(store.ProfileNames.Count == 1, "A fresh store must contain one profile.");

    var sectorPath = Path.Combine(root, "sector-references.json");
    var sectorStore = new SectorReferenceStore(sectorPath);
    Assert(sectorStore.Load("Spa", "GT3") == default,
        "A fresh sector store must not invent references.");
    sectorStore.Save("Spa", "GT3", new(29.4, 40.1, 51.2));
    var persistedSectors = new SectorReferenceStore(sectorPath)
        .Load("Spa", "GT3");
    Assert(persistedSectors == new LmuOverlay.Widgets.SectorReferenceSeed(29.4, 40.1, 51.2),
        "Personal sector references must survive an application restart.");
    sectorStore.Save("spa", "gt3", new(0, 39.8, 0));
    persistedSectors = sectorStore.Load("SPA", "GT3");
    Assert(persistedSectors == new LmuOverlay.Widgets.SectorReferenceSeed(29.4, 39.8, 51.2),
        "Concurrent dashboard hosts must merge personal bests without erasing sectors.");
    sectorStore.Save("Spa", "GT3", new(29.1, 40.3, 50.7));
    persistedSectors = sectorStore.Load("Spa", "GT3");
    Assert(persistedSectors == new LmuOverlay.Widgets.SectorReferenceSeed(29.1, 39.8, 50.7),
        "A new valid BEST must update only its faster sectors and preserve a faster existing sector.");

    var personalBestPath = Path.Combine(root, "personal-bests.json");
    var personalBestStore = new PersonalBestLapStore(personalBestPath);
    var firstPersonalBest = new LmuOverlay.Widgets.PersonalBestLap(120, 30, 40, 50);
    Assert(personalBestStore.SaveIfFaster(
        "Spa", "Driver One", "GT3", firstPersonalBest) == firstPersonalBest,
        "The first valid personal best must be saved.");
    var slowerLap = new LmuOverlay.Widgets.PersonalBestLap(121, 30.5, 40.5, 50);
    Assert(personalBestStore.SaveIfFaster(
        "Spa", "Driver One", "GT3", slowerLap) == firstPersonalBest,
        "A slower lap must not overwrite the personal best.");
    var fasterLap = new LmuOverlay.Widgets.PersonalBestLap(118, 29, 39, 50);
    Assert(personalBestStore.SaveIfFaster(
        "Spa", "Driver One", "GT3", fasterLap) == fasterLap,
        "A faster valid lap must atomically replace the complete record.");
    Assert(new PersonalBestLapStore(personalBestPath).Load(
        "SPA", "driver one", "gt3") == fasterLap,
        "Personal bests must be local and separated by track, driver and vehicle model.");
    Assert(personalBestStore.Load("Spa", "Driver Two", "GT3") == default,
        "Different drivers must never share a personal record.");
    var mixedSectorBest = new LmuOverlay.Widgets.PersonalBestLap(117, 28, 40.5, 48.5);
    personalBestStore.SaveIfFaster("Spa", "Driver One", "GT3", mixedSectorBest);
    var timingRecord = personalBestStore.LoadRecord("Spa", "Driver One", "GT3");
    Assert(timingRecord.BestLap == mixedSectorBest &&
           timingRecord.BestSectors == new LmuOverlay.Widgets.SectorReferenceSeed(28, 39, 48.5),
        "A new valid BEST must keep independently faster sectors from prior valid best laps.");
    Assert(Math.Abs(personalBestStore.SaveOptimalIfFaster(
            "Spa", "Driver One", "GT3", 114) - 114) < 0.0001 &&
           Math.Abs(personalBestStore.SaveOptimalIfFaster(
            "Spa", "Driver One", "GT3", 116) - 114) < 0.0001,
        "The per-track Optimal must persist only a faster valid value.");
    Assert(Math.Abs(new PersonalBestLapStore(personalBestPath).LoadRecord(
            "Spa", "Driver One", "GT3").OptimalLapTimeSeconds - 114) < 0.0001,
        "Saved Optimal must survive restart and remain isolated by track, driver and car.");
    Assert(new PersonalBestLapStore(personalBestPath, "physics-next").LoadRecord(
            "Spa", "Driver One", "GT3") == default,
        "A new telemetry/physics generation must not silently reuse older records.");
    Assert(new SectorReferenceStore(sectorPath, "physics-next").Load("Spa", "GT3") == default,
        "Sector references must be isolated by the compatibility generation.");
    var officialStanding = new LmuOverlay.Domain.LmuVehicleStanding(
        42, "Driver One", "Car", "GT3", "GT3", 1, 5, 1, 100,
        118, 119, 0, 0, 0, 0, 0, 0, true, false,
        LmuOverlay.Domain.LmuPitState.None, 0, false, false, 0.5, false,
        BestLapSector1Seconds: 29,
        BestLapSector2CumulativeSeconds: 68);
    Assert(PersistentSectorReferenceTracker.OfficialPersonalBest(officialStanding) ==
           new LmuOverlay.Widgets.PersonalBestLap(118, 29, 39, 50),
        "Only LMU's official best lap and its own sectors may form a saved PB.");
    Assert(!PersistentSectorReferenceTracker.OfficialPersonalBest(
            officialStanding with { BestLapSector2CumulativeSeconds = 0 }).IsValid,
        "Incomplete official best-lap sectors must never be persisted.");
    var lmuHudBest = officialStanding with
    {
        BestLapTimeSeconds = 112.721,
        BestLapSector1Seconds = 36.827,
        BestLapSector2CumulativeSeconds = 75.012,
    };
    var decomposedHudBest =
        PersistentSectorReferenceTracker.OfficialPersonalBest(lmuHudBest);
    Assert(decomposedHudBest.IsValid &&
           Math.Abs(decomposedHudBest.LapTimeSeconds - 112.721) < 0.0001 &&
           Math.Abs(decomposedHudBest.Sector1Seconds - 36.827) < 0.0001 &&
           Math.Abs(decomposedHudBest.Sector2Seconds - 38.185) < 0.0001 &&
           Math.Abs(decomposedHudBest.Sector3Seconds - 37.709) < 0.0001,
        "LMU's cumulative S1+S2 split must be decomposed into individual S2 and S3 times.");

    var requested = new LayoutProfile(
        LayoutProfile.CurrentSchemaVersion,
        new WidgetPlacement(-2, 4, 0.01, 5, 8, -1, true),
        LayoutProfile.Default.Inputs,
        LayoutProfile.Default.LiveStandings,
        LayoutProfile.Default.Relative,
        LayoutProfile.Default.SessionFlags,
        LayoutProfile.Default.FuelStrategy);
    requested = requested with
    {
        RaceControl = new WidgetPlacement(2, -1, 0.01, 4, 5, 0.1, true),
        Settings = new OverlayProfileSettings
        {
            Theme = "Unsupported",
            RefreshRateHz = 100,
            GridSnapPixels = 80,
            FuelReserveLaps = 20,
            EnergyReservePercent = 50,
        },
    };
    store.Save(requested);
    var loaded = store.Load();

    Assert(loaded.Diagnostic.X == 0, "X must be clamped.");
    Assert(loaded.Diagnostic.Y == 0.95, "Y must be clamped.");
    Assert(loaded.Diagnostic.Width == 0.08, "Width must respect its minimum.");
    Assert(loaded.Diagnostic.Height == 1, "Height must be clamped.");
    Assert(loaded.Diagnostic.Scale == 2, "Scale must be clamped.");
    Assert(loaded.Diagnostic.Opacity == 0.2, "Opacity must be clamped.");
    Assert(loaded.RaceControl.X == 0.95 && loaded.RaceControl.Y == 0,
        "Race Control placement must be sanitized.");
    Assert(loaded.RaceControl.Scale == 2,
        "Race Control scale must be clamped.");
    Assert(loaded.Settings.Theme == "RedFox", "Unknown themes must fail to RedFox.");
    Assert(loaded.Settings.RefreshRateHz == 100, "Valid high refresh rate must be preserved.");
    Assert(loaded.Settings.GridSnapPixels == 50, "Grid snapping must be clamped.");
    Assert(loaded.Settings.FuelReserveLaps == 5, "Fuel reserve must be clamped.");

    var customPath = Path.Combine(root, "custom-layout.json");
    var customStore = new LayoutStore(customPath);
    customStore.Save(LayoutProfile.Default with
    {
        Settings = LayoutProfile.Default.Settings with
        {
            Theme = "Custom",
            CustomAccentColor = "e04b73",
            CustomBackgroundColor = "invalid",
            CustomCardColor = "#112233",
            CustomPrimaryTextColor = "#F0F1F2",
            CustomSecondaryTextColor = "#A0A1A2",
            CustomInformationColor = "#22CCEE",
            CustomAttentionColor = "#EEAA22",
            CustomCriticalColor = "#EE2244",
            CustomPositiveColor = "#33DD88",
            DashboardTitle = "  BLUE FOX RACING  ",
            DashboardShowSectors = false,
            DashboardShowTires = true,
            DashboardShowTelemetry = false,
            DashboardTextScale = 2,
            TimingTextScale = 0.1,
            InputsTextScale = 1.15,
            LiveStandingsMaximumRows = 50,
            RelativeCarsEachSide = 1,
        },
    });
    var custom = customStore.Load();
    Assert(custom.Settings.Theme == "Custom", "The custom theme must be persisted.");
    Assert(custom.Settings.CustomAccentColor == "#E04B73",
        "Custom colors must be normalized to six-digit uppercase hex.");
    Assert(custom.Settings.CustomBackgroundColor == "#0A0F1A",
        "Invalid custom colors must safely fall back to the RedFox background.");
    Assert(custom.Settings.CustomCardColor == "#112233" &&
           custom.Settings.CustomInformationColor == "#22CCEE" &&
           custom.Settings.CustomCriticalColor == "#EE2244",
        "The complete semantic palette must be normalized and persisted.");
    Assert(!custom.Settings.DashboardShowSectors &&
           custom.Settings.DashboardShowTires &&
           !custom.Settings.DashboardShowTelemetry,
        "Dashboard module composition must be persisted per profile.");
    Assert(custom.Settings.DashboardTitle == "BLUE FOX RACING",
        "Dashboard titles must be trimmed before persistence.");
    Assert(custom.Settings.DashboardTextScale == 1.25 &&
           custom.Settings.TimingTextScale == 0.8 &&
           custom.Settings.InputsTextScale == 1.15,
        "Independent text scales must be sanitized and persisted.");
    Assert(custom.Settings.LiveStandingsMaximumRows == 12 &&
           custom.Settings.RelativeCarsEachSide == 2,
        "Timing tower row preferences must remain inside safe visual limits.");
    var customPalette = OverlayVisualSystem.Resolve(custom.Settings);
    Assert(customPalette.Accent.R == 224 && customPalette.Accent.G == 75 && customPalette.Accent.B == 115,
        "The custom palette must use the persisted accent color.");
    Assert(customPalette.Card.R == 17 && customPalette.Card.G == 34 && customPalette.Card.B == 51 &&
           customPalette.Information.R == 34 && customPalette.Information.G == 204 &&
           customPalette.Critical.B == 68,
        "Custom card and semantic colors must reach the resolved Desktop palette.");
    Assert(OverlayVisualSystem.ContrastRatio(customPalette.PrimaryText, customPalette.Background) >= 4.5,
        "Custom themes must preserve readable primary text contrast.");
    var lightPalette = OverlayVisualSystem.Resolve(custom.Settings with
    {
        CustomBackgroundColor = "#F5F5F5",
    });
    Assert(OverlayVisualSystem.ContrastRatio(lightPalette.PrimaryText, lightPalette.Background) >= 4.5,
        "Light custom backgrounds must automatically use dark readable text.");

    store.Create("Endurance", loaded);
    Assert(store.ActiveProfileName == "Endurance", "New profiles must become active.");
    Assert(store.ProfileNames.Count == 2, "Created profiles must be listed.");
    store.Save(loaded with
    {
        Inputs = loaded.Inputs with { Visible = false },
    });
    Assert(!store.Load().Inputs.Visible, "Changes must be saved to the active profile.");

    store.Switch(LayoutStore.DefaultProfileName);
    Assert(store.Load().Inputs.Visible, "Switching profiles must restore independent settings.");
    Assert(store.Rename(LayoutStore.DefaultProfileName, "Corrida") == "Corrida",
        "Profiles must be renameable.");
    Assert(store.ActiveProfileName == "Corrida", "Renaming must preserve the active profile.");

    var exportPath = Path.Combine(root, "corrida.lmu-layout.json");
    store.Export("Corrida", exportPath);
    var importedName = store.Import(exportPath);
    Assert(importedName == "Corrida (2)", "Import must resolve duplicate names.");
    Assert(store.ActiveProfileName == importedName, "Imported profiles must become active.");
    Assert(store.Load() == store.Switch("Corrida"), "Imported settings must match their source.");

    store.Delete("Endurance");
    Assert(!store.ProfileNames.Contains("Endurance"), "Deleted profiles must be removed.");
    AssertThrows<InvalidOperationException>(
        () =>
        {
            store.Delete("Corrida");
            store.Delete("Corrida (2)");
        },
        "The last profile must not be removable.");

    var legacyPath = Path.Combine(root, "legacy-layout.json");
    File.WriteAllText(legacyPath, JsonSerializer.Serialize(LayoutProfile.Default));
    var legacyStore = new LayoutStore(legacyPath);
    Assert(legacyStore.Load() == LayoutProfile.Default, "Legacy single profiles must migrate.");
    Assert(legacyStore.ProfileNames.Count == 1, "Migration must create one catalog profile.");
    legacyStore.Create("Migrated copy", legacyStore.Load());
    Assert(legacyStore.ProfileNames.Count == 2, "Migrated catalogs must remain writable.");

    foreach (var (width, height) in new[]
    {
        (1280d, 720d),
        (1920d, 1080d),
        (2560d, 1080d),
        (3440d, 1440d),
        (3840d, 2160d),
    })
    {
        foreach (var (name, placement) in new[]
        {
            ("DiagnosticWidget", LayoutProfile.Default.Diagnostic),
            ("InputsWidget", LayoutProfile.Default.Inputs),
            ("LiveStandingsWidget", LayoutProfile.Default.LiveStandings),
            ("RelativeWidget", LayoutProfile.Default.Relative),
            ("SessionFlagsWidget", LayoutProfile.Default.SessionFlags),
            ("FuelStrategyWidget", LayoutProfile.Default.FuelStrategy),
            ("RaceControlWidget", LayoutProfile.Default.RaceControl),
        })
        {
            var spec = ResponsiveWidgetLayout.For(name);
            var bounds = ResponsiveWidgetLayout.Calculate(width, height, placement, spec);
            Assert(bounds.Width > 0 && bounds.Height > 0,
                $"{name} must remain visible at {width}x{height}.");
            Assert(bounds.X >= 0 && bounds.Y >= 0 &&
                   bounds.X + bounds.Width <= width + 0.001 &&
                   bounds.Y + bounds.Height <= height + 0.001,
                $"{name} must remain inside {width}x{height}.");
            Assert(Math.Abs(bounds.Width / bounds.Height - spec.AspectRatio) < 0.001,
                $"{name} must preserve its aspect ratio at {width}x{height}.");
        }

        var fuelBounds = ResponsiveWidgetLayout.Calculate(
            width,
            height,
            LayoutProfile.Default.FuelStrategy,
            ResponsiveWidgetLayout.For("FuelStrategyWidget"));
        var inputBounds = ResponsiveWidgetLayout.Calculate(
            width,
            height,
            LayoutProfile.Default.Inputs,
            ResponsiveWidgetLayout.For("InputsWidget"));
        Assert(
            fuelBounds.Y + fuelBounds.Height <= inputBounds.Y ||
            inputBounds.Y + inputBounds.Height <= fuelBounds.Y,
            $"Fuel and inputs must not overlap at {width}x{height}.");
    }

    var narrowTowerPath = Path.Combine(root, "layout-v4.json");
    var versionFourProfile = LayoutProfile.Default with
    {
        SchemaVersion = 4,
        LiveStandings = LayoutProfile.Default.LiveStandings with
        {
            X = 0.72,
            Width = 0.25,
            Opacity = 0.92,
        },
    };
    File.WriteAllText(narrowTowerPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy"] = versionFourProfile,
        },
    }));
    var narrowTowerStore = new LayoutStore(narrowTowerPath);
    var migratedTower = narrowTowerStore.Load().LiveStandings;
    Assert(Math.Abs(migratedTower.Width - 0.28) < 0.0001,
        "The old standings layout must migrate to the wider panel.");
    Assert(Math.Abs(migratedTower.X - 0.69) < 0.0001,
        "The wider timing panel must remain right aligned.");

    var fuelPanelPath = Path.Combine(root, "layout-v5.json");
    var versionFiveProfile = LayoutProfile.Default with
    {
        SchemaVersion = 5,
        FuelStrategy = new WidgetPlacement(
            0.025, 0.38, 0.22, 0.16, 1, 0.92, true),
    };
    File.WriteAllText(fuelPanelPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Fuel",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Fuel"] = versionFiveProfile,
        },
    }));
    var fuelPanelStore = new LayoutStore(fuelPanelPath);
    var migratedFuelPanel = fuelPanelStore.Load().FuelStrategy;
    Assert(migratedFuelPanel.Width == 0.30,
        "The old fuel widget must migrate to the strategy-table width.");
    Assert(migratedFuelPanel.Height == 0.25,
        "The old fuel widget must migrate to the strategy-table height.");

    var relativeTowerPath = Path.Combine(root, "layout-v6.json");
    var versionSixProfile = LayoutProfile.Default with
    {
        SchemaVersion = 6,
        Relative = new WidgetPlacement(
            0.36, 0.58, 0.28, 0.28, 1, 0.92, true),
    };
    File.WriteAllText(relativeTowerPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Relative",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Relative"] = versionSixProfile,
        },
    }));
    var relativeTowerStore = new LayoutStore(relativeTowerPath);
    var migratedRelative = relativeTowerStore.Load().Relative;
    Assert(Math.Abs(migratedRelative.Width - 0.28) < 0.0001 &&
           Math.Abs(migratedRelative.Height - 0.40) < 0.0001,
        "The old relative box must migrate to the wider timing proportions.");
    Assert(Math.Abs(migratedRelative.X - 0.40) < 0.0001 &&
           Math.Abs(migratedRelative.Y - 0.05) < 0.0001,
        "The wider relative panel must migrate beside live standings without overlap.");

    var sessionPanelPath = Path.Combine(root, "layout-v7.json");
    var versionSevenProfile = LayoutProfile.Default with
    {
        SchemaVersion = 7,
        SessionFlags = new WidgetPlacement(
            0.36, 0.05, 0.28, 0.12, 1, 0.92, true),
    };
    File.WriteAllText(sessionPanelPath, JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        ActiveProfile = "Legacy Session",
        Profiles = new Dictionary<string, LayoutProfile>
        {
            ["Legacy Session"] = versionSevenProfile,
        },
    }));
    var sessionPanelStore = new LayoutStore(sessionPanelPath);
    var migratedSession = sessionPanelStore.Load().SessionFlags;
    Assert(migratedSession.Width == 0.30 && migratedSession.Height == 0.18,
        "The old session strip must migrate to the three-card panel.");
    Assert(migratedSession.X == 0.33,
        "The migrated session panel must stay centered.");

    var oldInputProfilePath = Path.Combine(root, "layout-v14.json");
    var oldInputProfile = LayoutProfile.Default with
    {
        SchemaVersion = 14,
        Inputs = LayoutProfile.Default.Inputs with { Y = 0.47 },
    };
    File.WriteAllText(oldInputProfilePath, JsonSerializer.Serialize(oldInputProfile));
    var migratedInputs = new LayoutStore(oldInputProfilePath).Load().Inputs;
    Assert(migratedInputs.Y == 0.66,
        "The old default input panel must migrate below fuel without overlap.");

    foreach (var theme in new[] { "RedFox", "Black", "HighContrast", "ColorVisionSafe" })
    {
        var palette = OverlayVisualSystem.Resolve(theme);
        Assert(
            OverlayVisualSystem.ContrastRatio(
                palette.PrimaryText,
                palette.Background) >= 4.5,
            $"{theme} primary text must meet the visual contrast target.");
        Assert(
            OverlayVisualSystem.ContrastRatio(
                palette.SecondaryText,
                palette.Background) >= 3,
            $"{theme} secondary text must remain distinguishable.");
    }
    var colorSafe = OverlayVisualSystem.Resolve("ColorVisionSafe");
    Assert(colorSafe.Information != colorSafe.Attention &&
           colorSafe.Attention != colorSafe.Critical &&
           colorSafe.Critical != colorSafe.Positive,
        "Color-vision-safe semantic states must use distinct Okabe-Ito colors.");

    Assert(
        OverlayVisualSystem.ResolveDensity("Auto", 480, 800) == OverlayDensity.Compact,
        "Small automatic dashboards must use compact density.");
    Assert(
        OverlayVisualSystem.ResolveDensity("Expanded", 480, 800) == OverlayDensity.Expanded,
        "Explicit visual density must override automatic breakpoints.");
    foreach (var presetName in LayoutPresets.Names)
    {
        var preset = LayoutPresets.Create(presetName);
        Assert(preset.SchemaVersion == LayoutProfile.CurrentSchemaVersion,
            $"{presetName} must use the current profile schema.");
    }
    var broadcastPreset = LayoutPresets.Create("Broadcast");
    Assert(
        broadcastPreset.Relative.X + broadcastPreset.Relative.Width <=
        broadcastPreset.LiveStandings.X,
        "The Broadcast timing towers must not overlap.");
    Assert(
        LayoutPresets.Create("Minimal").Settings.LiveStandingsMaximumRows == 8 &&
        LayoutPresets.Create("Endurance Pro").Settings.RelativeCarsEachSide == 5,
        "Visual presets must carry their intended timing population settings.");

    File.WriteAllText(path, "{broken");
    var corruptStore = new LayoutStore(path);
    Assert(corruptStore.Load() == LayoutProfile.Default, "Corrupt profiles must be recoverable.");

    Console.WriteLine("Desktop layout checks passed.");
    return 0;
}
finally
{
    Directory.Delete(root, true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
