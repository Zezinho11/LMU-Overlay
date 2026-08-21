using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class VrWidgetTextureRenderer
{
    public static VrRenderedFrame Fuel(FuelStrategyWidgetState state) => Fuel(state, DefaultStyle);

    public static VrRenderedFrame Fuel(FuelStrategyWidgetState state, VrRenderStyle style) =>
        Draw(1000, 820, style, canvas =>
        {
            Header(canvas, T(style, OverlayTextKey.FuelAndEnergy).ToUpperInvariant(), OverlayText.TranslateExact(style.Language, state.Status), 1000, 1);
            var columns = new[] { 20f, 260f, 480f, 700f, 860f };
            var widths = new[] { 240f, 220f, 220f, 160f, 120f };
            var headers = new[] { T(style, OverlayTextKey.Resource), T(style, OverlayTextKey.Current), T(style, OverlayTextKey.UsagePerLap), T(style, OverlayTextKey.Range), T(style, OverlayTextKey.Time) };
            for (var index = 0; index < headers.Length; index++)
                canvas.Text(headers[index], 16, style.SecondaryText, new(columns[index], 90, widths[index], 30), true, StringAlignment.Center);
            FuelResourceRow(canvas, T(style, OverlayTextKey.Fuel), state.Available
                    ? state.EffectiveFuelCapacityLiters > 0
                        ? $"{state.FuelLiters:0.0}/{state.EffectiveFuelCapacityLiters:0.0} L"
                        : $"{state.FuelLiters:0.0} L"
                    : "--",
                state.Learning ? "LEARNING" : $"{state.ProjectedConsumptionLitersPerLap:0.00} L",
                state.EstimatedRangeLaps, state.EstimatedRangeTimeSeconds, 126, style);
            FuelResourceRow(canvas, T(style, OverlayTextKey.VirtualEnergy), state.Available ? $"{state.VirtualEnergyFraction:P0}" : "--",
                state.Learning ? "LEARNING" : $"{state.AverageVirtualEnergyFractionPerLap:P1}",
                state.EstimatedVirtualEnergyRangeLaps, state.EstimatedVirtualEnergyRangeTimeSeconds, 192, style);
            canvas.Fill(style.Card, 20, 270, 960, 96);
            canvas.Text(T(style, OverlayTextKey.ToFinish), 17, style.SecondaryText, new(40, 280, 180, 28), true);
            canvas.Text($"{state.EstimatedLapsToFinish} LAPS / {Minutes(state.EstimatedTimeToFinishSeconds)}", 25, style.PrimaryText, new(40, 310, 300, 40), true);
            canvas.Text(T(style, OverlayTextKey.NeedMargin), 17, style.SecondaryText, new(360, 280, 270, 28), true);
            canvas.Text($"{state.RequiredFuelLiters:0.0} L / {state.FuelMarginLiters:+0.0;-0.0} L", 25,
                state.FuelMarginLiters >= 0 ? style.Positive : style.Critical, new(360, 310, 280, 40), true);
            canvas.Text("STINT / PIT", 17, style.SecondaryText, new(680, 280, 260, 28), true);
            canvas.Text(state.SuggestedPitLap > 0 ? $"LAP {state.SuggestedPitLap} · {state.LapsUntilPit} TO GO" : "LEARNING",
                25, style.Attention, new(680, 310, 270, 40), true);
            StrategyBox(canvas, "FULL PUSH", state.PlanSummary, state.PitPlan,
                $"{state.TirePlan} · FINAL +{state.FinalFuelToAddLiters:0.0}L / NRG {state.FinalVirtualEnergyTargetFraction:P0}", 390, style.Positive);
            FuelSaveStrategyBox(canvas, "FUEL SAVE", state.FuelSavePlan,
                state.FuelSaveLapPlan,
                state.FuelSaveVirtualEnergyTargetPerLap > 0
                    ? $"TARGET {state.FuelSaveTargetLitersPerLap:0.00}L/LAP · NRG {state.FuelSaveVirtualEnergyTargetPerLap:P1}/LAP"
                    : $"TARGET {state.FuelSaveTargetLitersPerLap:0.00}L/LAP",
                $"PIT {state.FuelSavePitPlan}",
                $"TIRES {state.FuelSaveTirePlan}", 540, style.Information);
            canvas.Fill(style.Card, 20, 710, 960, 66);
            canvas.Text($"PACE {Lap(state.AveragePaceSeconds)} · TREND {state.PaceTrendSecondsPerLap:+0.00;-0.00;0.00}/LAP · " +
                        $"STOPS {state.EstimatedPitStops} · FINISH {state.FinishProbability:P0} · CONFIDENCE {state.Confidence}",
                18, style.SecondaryText, new(36, 720, 928, 44), true, StringAlignment.Center);
        });

    public static VrRenderedFrame Session(SessionFlagsWidgetState state) => Session(state, DefaultStyle);

    public static VrRenderedFrame Session(SessionFlagsWidgetState state, VrRenderStyle style) =>
        Draw(1100, 340, style, canvas =>
        {
            Surface(canvas, 4, 4, 1092, 332);
            Header(canvas, state.SessionName.Length > 0 ? state.SessionName : T(style, OverlayTextKey.Session),
                Clock(state.RemainingSeconds), 1100, 1);
            var flag = FlagColor(state.FlagName, style);
            canvas.FillRound(flag, 28, 104, 210, 78, 8);
            canvas.Text(OverlayText.TranslateExact(style.Language, state.FlagName), 31, Color.White, new(28, 104, 210, 78), true, StringAlignment.Center);
            canvas.FillRound(GripColor(state.TrackGripLevel), 258, 104, 270, 78, 8);
            canvas.Text($"GRIP · {state.TrackGripName}", 27, Color.White, new(258, 104, 270, 78), true, StringAlignment.Center);
            canvas.FillRound(style.Card, 548, 104, 524, 78, 8);
            WeatherIcon(canvas, state.WeatherCondition, 570, 112, style);
            canvas.Text(state.WeatherName, 27, style.PrimaryText, new(660, 104, 390, 78), true);
            var values = new[]
            {
                $"AIR {state.AmbientTemperatureCelsius:0}°",
                $"TRACK {state.TrackTemperatureCelsius:0}°",
                $"RAIN {state.RainIntensity:P0}",
                $"WET {state.AveragePathWetness:P0}",
                $"LAP {state.CurrentLap}/{(state.MaximumLaps > 0 ? state.MaximumLaps : 0)}",
                state.PhaseName,
            };
            for (var index = 0; index < values.Length; index++)
            {
                var x = 28 + (index * 174);
                canvas.Fill(style.Card, x, 210, 158, 88);
                canvas.Text(values[index], 21, index == 2 ? style.Information : style.PrimaryText,
                    new(x + 6, 210, 146, 88), true, StringAlignment.Center);
            }
        });

    public static VrRenderedFrame RaceControl(RaceControlWidgetState state) => RaceControl(state, DefaultStyle);

    public static VrRenderedFrame RaceControl(RaceControlWidgetState state, VrRenderStyle style) =>
        Draw(900, 560, style, canvas =>
        {
            Header(canvas, $"{T(style, OverlayTextKey.RaceControl).ToUpperInvariant()} / {T(style, OverlayTextKey.Damage).ToUpperInvariant()}", state.RequiresAttention ? T(style, OverlayTextKey.Attention) : T(style, OverlayTextKey.Clear), 900, 1);
            var items = new[]
            {
                (T(style, OverlayTextKey.Penalty), state.PenaltyStatus, state.OutstandingPenalties > 0 ? style.Critical : style.Positive),
                (T(style, OverlayTextKey.Pit), state.PitStatus, style.Information),
                (T(style, OverlayTextKey.Lap), state.LapStatus, state.LapStatus.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ? style.Critical : style.Positive),
                (T(style, OverlayTextKey.Flag), state.FlagStatus, FlagColor(state.FlagStatus, style)),
                (T(style, OverlayTextKey.Damage), state.DamageStatus, state.HasCriticalDamage ? style.Critical : state.RequiresAttention ? style.Attention : style.Positive),
                ("LAST IMPACT", state.ImpactStatus, style.SecondaryText),
                (T(style, OverlayTextKey.Systems), state.SystemsStatus, style.Information),
            };
            var y = 104f;
            foreach (var item in items)
            {
                canvas.Fill(style.Card, 24, y, 852, 54);
                canvas.Text(item.Item1, 19, style.SecondaryText, new(42, y, 220, 54), true);
                canvas.Text(Trim(item.Item2, 42), 22, item.Item3, new(270, y, 580, 54), true, StringAlignment.Far);
                y += 60;
            }
        });
}
