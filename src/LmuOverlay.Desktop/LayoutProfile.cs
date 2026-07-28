namespace LmuOverlay.Desktop;

public sealed record LayoutProfile(
    int SchemaVersion,
    WidgetPlacement Diagnostic)
{
    public const int CurrentSchemaVersion = 1;

    public static LayoutProfile Default => new(
        CurrentSchemaVersion,
        new WidgetPlacement(0.025, 0.05, 0.22, 0.20, 1, 0.92, true));
}

public sealed record WidgetPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    double Scale,
    double Opacity,
    bool Visible);
