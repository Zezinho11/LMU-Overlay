namespace LmuOverlay.Desktop;

public readonly record struct WidgetLayoutSpec(
    double DesignWidth,
    double DesignHeight,
    double MinimumWidth,
    double MinimumHeight)
{
    public double AspectRatio => DesignWidth / DesignHeight;
}

public readonly record struct ResponsiveWidgetBounds(
    double X,
    double Y,
    double Width,
    double Height);

public static class ResponsiveWidgetLayout
{
    private const double ReferenceWidth = 1920;
    private const double ReferenceHeight = 1080;

    public static WidgetLayoutSpec For(string widgetName) => widgetName switch
    {
        "DiagnosticWidget" => new(800, 480, 360, 216),
        "InputsWidget" => new(520, 220, 260, 110),
        "LiveStandingsWidget" or "RelativeWidget" => new(500, 410, 280, 230),
        "SessionFlagsWidget" => new(500, 190, 250, 95),
        "FuelStrategyWidget" => new(500, 418, 250, 209),
        "RaceControlWidget" => new(430, 190, 240, 106),
        "PriorityAlert" => new(460, 58, 240, 30),
        _ => new(1, 1, 120, 60),
    };

    public static ResponsiveWidgetBounds Calculate(
        double canvasWidth,
        double canvasHeight,
        WidgetPlacement placement,
        WidgetLayoutSpec spec,
        double localDisplayScale = 0)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return default;
        }

        var displayScale = localDisplayScale > 0
            ? Math.Clamp(localDisplayScale, 0.55, 1.5)
            : Math.Clamp(
                Math.Min(canvasWidth / ReferenceWidth, canvasHeight / ReferenceHeight),
                0.55,
                1.5);
        var minimumWidth = Math.Min(canvasWidth, spec.MinimumWidth * displayScale);
        var minimumHeight = Math.Min(canvasHeight, spec.MinimumHeight * displayScale);

        var requestedWidth = placement.Width * canvasWidth * placement.Scale;
        var requestedHeight = placement.Height * canvasHeight * placement.Scale;
        var aspectRatio = spec.AspectRatio;

        // Fit inside both normalized dimensions. This keeps tall timing towers from
        // growing from their width alone and makes every widget resolution-independent.
        var width = Math.Min(requestedWidth, requestedHeight * aspectRatio);
        width = Math.Max(width, Math.Max(minimumWidth, minimumHeight * aspectRatio));
        width = Math.Min(width, Math.Min(canvasWidth, canvasHeight * aspectRatio));
        var height = width / aspectRatio;

        var x = Math.Clamp(
            placement.X * canvasWidth,
            0,
            Math.Max(0, canvasWidth - width));
        var y = Math.Clamp(
            placement.Y * canvasHeight,
            0,
            Math.Max(0, canvasHeight - height));
        return new ResponsiveWidgetBounds(x, y, width, height);
    }
}
