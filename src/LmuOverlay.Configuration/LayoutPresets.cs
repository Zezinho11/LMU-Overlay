namespace LmuOverlay.Desktop;

public static class LayoutPresets
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Race",
        "Endurance",
        "Qualifying",
        "Multiclass",
        "VR Compact",
        "High Contrast",
        "Color Vision Safe",
        "Minimal",
        "Broadcast",
        "Endurance Pro",
    ];

    public static LayoutProfile Create(string name)
    {
        var profile = LayoutProfile.Default;
        return name switch
        {
            "Race" => profile with
            {
                FuelStrategy = profile.FuelStrategy with { Visible = false },
                SessionFlags = profile.SessionFlags with { Visible = true },
                Settings = profile.Settings with { VisualDensity = "Normal" },
            },
            "Endurance" => profile with
            {
                FuelStrategy = profile.FuelStrategy with { Visible = true },
                SessionFlags = profile.SessionFlags with { Visible = true },
                Settings = profile.Settings with { VisualDensity = "Expanded" },
            },
            "Qualifying" => profile with
            {
                LiveStandings = profile.LiveStandings with { Visible = false },
                FuelStrategy = profile.FuelStrategy with { Visible = false },
                RaceControl = profile.RaceControl with { Visible = false },
                Inputs = profile.Inputs with { Visible = true },
                Settings = profile.Settings with { VisualDensity = "Normal" },
            },
            "Multiclass" => profile with
            {
                Diagnostic = profile.Diagnostic with { Width = 0.33, Height = 0.35 },
                LiveStandings = profile.LiveStandings with { Width = 0.30, Height = 0.48 },
                Relative = profile.Relative with { Width = 0.30, Height = 0.48 },
                FuelStrategy = profile.FuelStrategy with { Visible = false },
                Settings = profile.Settings with { VisualDensity = "Compact" },
            },
            "VR Compact" => profile with
            {
                Inputs = profile.Inputs with { Visible = false },
                LiveStandings = profile.LiveStandings with { Visible = false },
                FuelStrategy = profile.FuelStrategy with { Visible = false },
                RaceControl = profile.RaceControl with { Visible = true },
                Settings = profile.Settings with { VisualDensity = "Compact" },
            },
            "High Contrast" => profile with
            {
                Settings = profile.Settings with
                {
                    Theme = "HighContrast",
                    BackgroundOpacity = 1,
                    VisualDensity = "Normal",
                },
            },
            "Color Vision Safe" => profile with
            {
                Settings = profile.Settings with
                {
                    Theme = "ColorVisionSafe",
                    BackgroundOpacity = 0.98,
                    VisualDensity = "Normal",
                },
            },
            "Minimal" => profile with
            {
                FuelStrategy = profile.FuelStrategy with { Visible = false },
                SessionFlags = profile.SessionFlags with { Visible = false },
                RaceControl = profile.RaceControl with { Visible = false },
                Settings = profile.Settings with
                {
                    Theme = "Black",
                    BackgroundOpacity = 0.84,
                    VisualDensity = "Compact",
                    DashboardTextScale = 0.9,
                    TimingTextScale = 0.9,
                    InputsTextScale = 0.9,
                    LiveStandingsMaximumRows = 8,
                    RelativeCarsEachSide = 3,
                },
            },
            "Broadcast" => profile with
            {
                LiveStandings = profile.LiveStandings with { X = 0.69, Width = 0.30, Height = 0.48 },
                Relative = profile.Relative with { X = 0.38, Width = 0.30, Height = 0.48 },
                Settings = profile.Settings with
                {
                    VisualDensity = "Expanded",
                    DashboardTextScale = 1.05,
                    TimingTextScale = 1.1,
                    LiveStandingsMaximumRows = 12,
                    RelativeCarsEachSide = 4,
                },
            },
            "Endurance Pro" => profile with
            {
                FuelStrategy = profile.FuelStrategy with { Visible = true },
                SessionFlags = profile.SessionFlags with { Visible = true },
                RaceControl = profile.RaceControl with { Visible = true },
                Settings = profile.Settings with
                {
                    BackgroundOpacity = 0.98,
                    VisualDensity = "Normal",
                    DashboardTextScale = 1.05,
                    TimingTextScale = 1,
                    InputsTextScale = 1,
                    LiveStandingsMaximumRows = 12,
                    RelativeCarsEachSide = 5,
                },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown layout preset."),
        };
    }
}
