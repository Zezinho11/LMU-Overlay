using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class VrWidgetTextureRenderer
{
    public static VrRenderedFrame Dashboard(DashboardWidgetState state) =>
        Dashboard(state, DefaultStyle, null);

    public static VrRenderedFrame Dashboard(
        DashboardWidgetState state,
        VrRenderStyle style,
        IReadOnlyList<VrPedalSample>? history) => new(
            VrDashboardTexture.Render(state, style, history),
            VrDashboardTexture.Width,
            VrDashboardTexture.Height);

    public static VrRenderedFrame Inputs(InputsWidgetState state) =>
        Inputs(state, DefaultStyle, null);

    public static VrRenderedFrame Inputs(
        InputsWidgetState state,
        VrRenderStyle style,
        IReadOnlyList<VrPedalSample>? history) =>
        Draw(900, 380, style, canvas =>
        {
            Surface(canvas, 4, 4, 892, 372);
            Header(canvas, T(style, OverlayTextKey.DriverInputs).ToUpperInvariant(), state.Available ? "LIVE" : T(style, OverlayTextKey.Waiting), 900, style.InputsTextScale);
            VrSteeringWheelRenderer.Draw(
                canvas,
                state.Steering,
                state.SteeringWheelRangeDegrees,
                style.SteeringWheelImagePath);
            var samples = history is { Count: > 1 }
                ? history
                : new[]
                {
                    new VrPedalSample((float)state.Throttle, (float)state.Brake, state.AbsActive, state.TractionControlActive),
                    new VrPedalSample((float)state.Throttle, (float)state.Brake, state.AbsActive, state.TractionControlActive),
                };
            DrawPedalGraph(canvas, samples, 270, 92, 590, 185);
            var f = style.InputsTextScale;
            ValueCard(canvas, T(style, OverlayTextKey.Throttle), state.Available ? state.Throttle.ToString("P0") : "--", state.TractionControlActive ? style.Attention : style.Positive, 270, 292, f);
            ValueCard(canvas, T(style, OverlayTextKey.Brake), state.Available ? state.Brake.ToString("P0") : "--", state.AbsActive ? style.Attention : style.Critical, 415, 292, f);
            ValueCard(canvas, T(style, OverlayTextKey.Clutch), state.Available ? state.Clutch.ToString("P0") : "--", style.Information, 560, 292, f);
            ValueCard(canvas, "STR", state.Available ? state.Steering.ToString("+0%;-0%;0%") : "--", style.PrimaryText, 705, 292, f);
            if (state.AbsActive) Badge(canvas, "ABS", style.Attention, 784, 25, 80, 38, f);
            if (state.TractionControlActive) Badge(canvas, "TC", style.Attention, 694, 25, 80, 38, f);
        });

    public static VrRenderedFrame PriorityAlert(
        DashboardWidgetState dashboard,
        SessionFlagsWidgetState session,
        FuelStrategyWidgetState fuel,
        RaceControlWidgetState raceControl,
        VrRenderStyle style,
        OverlayProfileSettings settings) =>
        Draw(900, 150, style, canvas =>
        {
            if (!settings.ShowPriorityAlerts || !dashboard.Available) return;
            var hottestTire = new[]
            {
                dashboard.TireTemperatures.FrontLeftCelsius,
                dashboard.TireTemperatures.FrontRightCelsius,
                dashboard.TireTemperatures.RearLeftCelsius,
                dashboard.TireTemperatures.RearRightCelsius,
            }.Max();
            var maximumWear = 1 - new[]
            {
                dashboard.TireWear.FrontLeftFraction,
                dashboard.TireWear.FrontRightFraction,
                dashboard.TireWear.RearLeftFraction,
                dashboard.TireWear.RearRightFraction,
            }.Min();
            maximumWear = Math.Clamp(maximumWear, 0, 1);
            (Color Color, string Icon, string Title, string Detail)? alert =
                raceControl.HasCriticalDamage
                    ? (style.Critical, "!", T(style, OverlayTextKey.CriticalDamage), raceControl.DamageStatus)
                : raceControl.OutstandingPenalties > 0
                        ? (style.Critical, "!", T(style, OverlayTextKey.Penalty), raceControl.PenaltyStatus)
                        : session.FlagName == "RED"
                            ? (style.Critical, "!", T(style, OverlayTextKey.RedFlag), T(style, OverlayTextKey.SessionStopped))
                            : fuel.Available && !fuel.Learning && fuel.Status == "SHORT"
                                ? (style.Critical, "!", T(style, OverlayTextKey.EnergyShortfall), fuel.PlanSummary)
                                : TireTemperatureClassifier.Classify(hottestTire) == TireTemperatureBand.Critical
                                    ? (style.Attention, "▲", T(style, OverlayTextKey.TireTemperature), $"{T(style, OverlayTextKey.Hottest)} {hottestTire:0}°C")
                                    : maximumWear >= settings.TireWearLimitPercent / 100
                                        ? (style.Attention, "▲", T(style, OverlayTextKey.TireWear), $"{T(style, OverlayTextKey.Maximum)} {maximumWear:P0}")
                                        : session.FlagName == "YELLOW"
                                            ? (style.Attention, "▲", T(style, OverlayTextKey.YellowFlag), T(style, OverlayTextKey.NoSafetyCarAssumption))
                                            : session.RainIntensity >= 0.02
                                                ? (style.Information, "☂", session.WeatherName, $"RAIN {session.RainIntensity:P0}")
                                                : dashboard.SpeedLimiterActive
                                                    ? (style.Information, "P", T(style, OverlayTextKey.PitLimiter), T(style, OverlayTextKey.Active))
                                                    : null;
            if (alert is null) return;
            canvas.FillRound(Color.FromArgb(248, style.Background), 4, 4, 892, 142, 16);
            canvas.StrokeRound(alert.Value.Color, 5, 4, 4, 892, 142, 16);
            canvas.FillRound(alert.Value.Color, 24, 24, 94, 102, 12);
            canvas.Text(alert.Value.Icon, 62, Color.White, new(24, 24, 94, 102), true, StringAlignment.Center);
            canvas.Text(alert.Value.Title, 36, style.PrimaryText, new(142, 20, 720, 62), true);
            canvas.Text(alert.Value.Detail, 23, style.SecondaryText, new(142, 78, 720, 45), true);
        });
}
