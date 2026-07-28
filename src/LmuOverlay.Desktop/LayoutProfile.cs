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
    public const int CurrentSchemaVersion = 8;

    public static LayoutProfile Default => new(
        CurrentSchemaVersion,
        new WidgetPlacement(0.025, 0.05, 0.22, 0.18, 1, 0.92, true),
        new WidgetPlacement(0.025, 0.25, 0.22, 0.10, 1, 0.92, true),
        new WidgetPlacement(0.81, 0.05, 0.16, 0.40, 1, 0.96, true),
        new WidgetPlacement(0.64, 0.05, 0.16, 0.40, 1, 0.96, true),
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
