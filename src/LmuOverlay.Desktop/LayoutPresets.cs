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
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown layout preset."),
        };
    }
}
