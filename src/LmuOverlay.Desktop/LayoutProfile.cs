namespace LmuOverlay.Desktop;

public sealed record LayoutProfile(
    int SchemaVersion,
    WidgetPlacement Diagnostic,
    WidgetPlacement Inputs,
    WidgetPlacement LiveStandings,
    WidgetPlacement Relative,
    WidgetPlacement SessionFlags,
    WidgetPlacement FuelStrategy)
{
    public const int CurrentSchemaVersion = 17;

    public WidgetPlacement RaceControl { get; init; } =
        new(0.35, 0.25, 0.26, 0.18, 1, 0.96, true);

    public OverlayProfileSettings Settings { get; init; } = new();

    public static LayoutProfile Default => new(
        CurrentSchemaVersion,
        new WidgetPlacement(0.025, 0.05, 0.38, 0.40, 1, 0.96, true),
        new WidgetPlacement(0.025, 0.66, 0.22, 0.10, 1, 0.92, true),
        new WidgetPlacement(0.69, 0.05, 0.28, 0.40, 1, 0.96, true),
        new WidgetPlacement(0.40, 0.05, 0.28, 0.40, 1, 0.96, true),
        new WidgetPlacement(0.33, 0.05, 0.30, 0.18, 1, 0.96, true),
        new WidgetPlacement(0.025, 0.38, 0.30, 0.25, 1, 0.96, true));
}

public sealed record WidgetPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    double Scale,
    double Opacity,
    bool Visible);

public sealed record OverlayProfileSettings
{
    public bool Locked { get; init; } = true;
    public string Theme { get; init; } = "RedFox";
    public int RefreshRateHz { get; init; } = 120;
    public int GridSnapPixels { get; init; } = 10;
    public double FuelReserveLaps { get; init; } = 1;
    public double EnergyReservePercent { get; init; } = 2;
    public int ManualRemainingLaps { get; init; }
    public int MaximumStintLaps { get; init; }
    public double EstimatedPitLossSeconds { get; init; } = 30;
    public int AvailableTireSets { get; init; }
    public double TireWearLimitPercent { get; init; } = 70;
    public double EstimatedTireChangeSeconds { get; init; } = 15;
    public double BackgroundOpacity { get; init; } = 0.94;
    public string VisualDensity { get; init; } = "Auto";
    public bool ShowPriorityAlerts { get; init; } = true;
    public bool ReduceMotion { get; init; } = true;
    public int PedalHistorySeconds { get; init; } = 5;
}
