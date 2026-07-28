namespace LmuOverlay.Desktop;

public sealed record LayoutProfile(
    int SchemaVersion,
    WidgetPlacement Diagnostic,
    WidgetPlacement Inputs,
    WidgetPlacement LiveStandings,
    WidgetPlacement Relative,
    WidgetPlacement SessionFlags)
{
    public const int CurrentSchemaVersion = 3;

    public static LayoutProfile Default => new(
        CurrentSchemaVersion,
        new WidgetPlacement(0.025, 0.05, 0.22, 0.18, 1, 0.92, true),
        new WidgetPlacement(0.025, 0.25, 0.22, 0.10, 1, 0.92, true),
        new WidgetPlacement(0.72, 0.05, 0.25, 0.40, 1, 0.92, true),
        new WidgetPlacement(0.36, 0.58, 0.28, 0.28, 1, 0.92, true),
        new WidgetPlacement(0.36, 0.05, 0.28, 0.12, 1, 0.92, true));
}

public sealed record WidgetPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    double Scale,
    double Opacity,
    bool Visible);
