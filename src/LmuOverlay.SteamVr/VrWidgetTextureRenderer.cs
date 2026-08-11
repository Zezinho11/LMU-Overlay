using System.Drawing;
using System.Drawing.Drawing2D;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public sealed record VrRenderedFrame(byte[] Pixels, uint Width, uint Height);

public static class VrWidgetTextureRenderer
{
    private static VrRenderStyle DefaultStyle => VrRenderStyle.From(new VrDesktopSettings());

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
            DrawSteeringWheel(canvas, state.Steering);
            var samples = history is { Count: > 1 }
                ? history
                : new[] { new VrPedalSample((float)state.Throttle, (float)state.Brake), new((float)state.Throttle, (float)state.Brake) };
            DrawPedalGraph(canvas, samples, 270, 92, 590, 185);
            var f = style.InputsTextScale;
            ValueCard(canvas, T(style, OverlayTextKey.Throttle), state.Available ? state.Throttle.ToString("P0") : "--", style.Positive, 270, 292, f);
            ValueCard(canvas, T(style, OverlayTextKey.Brake), state.Available ? state.Brake.ToString("P0") : "--", state.AbsActive ? style.Attention : style.Critical, 415, 292, f);
            ValueCard(canvas, T(style, OverlayTextKey.Clutch), state.Available ? state.Clutch.ToString("P0") : "--", style.Information, 560, 292, f);
            ValueCard(canvas, "STR", state.Available ? state.Steering.ToString("+0%;-0%;0%") : "--", style.PrimaryText, 705, 292, f);
            if (state.AbsActive) Badge(canvas, "ABS", style.Attention, 784, 25, 80, 38, f);
            if (state.TractionControlActive) Badge(canvas, "TC", style.Positive, 694, 25, 80, 38, f);
        });

    public static VrRenderedFrame PriorityAlert(
        DashboardWidgetState dashboard,
        SessionFlagsWidgetState session,
        FuelStrategyWidgetState fuel,
        RaceControlWidgetState raceControl,
        VrRenderStyle style,
        VrDesktopSettings settings) =>
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
            var maximumWear = new[]
            {
                dashboard.TireWear.FrontLeftFraction,
                dashboard.TireWear.FrontRightFraction,
                dashboard.TireWear.RearLeftFraction,
                dashboard.TireWear.RearRightFraction,
            }.Max();
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

    public static VrRenderedFrame LiveStandings(LiveStandingsWidgetState state) =>
        LiveStandings(state, DefaultStyle);

    public static VrRenderedFrame LiveStandings(LiveStandingsWidgetState state, VrRenderStyle style) =>
        Draw(760, 960, style, canvas =>
        {
            Header(canvas, state.SessionName.Length > 0 ? OverlayText.TranslateExact(style.Language, state.SessionName) : T(style, OverlayTextKey.LiveStandings).ToUpperInvariant(),
                Clock(state.SessionRemainingSeconds), 760, style.TimingTextScale);
            var f = style.TimingTextScale;
            canvas.Fill(style.Card, 0, 82, 760, 38);
            var headings = new[] { ("P", 0f, 52f), (T(style, OverlayTextKey.Manufacturer), 52f, 84f), (T(style, OverlayTextKey.Number), 136f, 60f), (T(style, OverlayTextKey.Driver), 196f, 100f), (T(style, state.IsQualifying ? OverlayTextKey.Best : OverlayTextKey.LastLap), 296f, 160f), (T(style, OverlayTextKey.Gap), 456f, 120f), ("TYRE / NRG", 576f, 184f) };
            foreach (var item in headings)
                canvas.Text(item.Item1, 15 * f, style.SecondaryText, new(item.Item2, 82, item.Item3, 38), true, StringAlignment.Center);
            var totalRows = Math.Max(1, state.Classes.Sum(group => group.Rows.Count));
            var classPixels = state.Classes.Count * 30;
            var rowHeight = Math.Min(58, (930 - 120 - classPixels) / totalRows);
            var y = 120f;
            var alternating = 0;
            foreach (var group in state.Classes)
            {
                canvas.Fill(group.IsPlayerClass ? Color.FromArgb(151, 25, 37) : Color.FromArgb(24, 43, 78), 0, y, 760, 30);
                canvas.Text(group.ClassName.ToUpperInvariant(), 18 * f, style.PrimaryText, new(18, y, 724, 30), true);
                y += 30;
                foreach (var row in group.Rows)
                {
                    var background = row.IsPlayer
                        ? Blend(style.Background, style.Accent, 0.55)
                        : alternating++ % 2 == 0 ? style.Background : style.Card;
                    canvas.Fill(background, 0, y, 760, rowHeight);
                    canvas.Text(row.ClassPosition.ToString(), 21 * f,
                        row.ClassPosition == 1 ? Color.Gold : style.PrimaryText,
                        new(0, y, 52, rowHeight), true, StringAlignment.Center);
                    var (brand, brandColor) = Manufacturer(row.VehicleModel);
                    canvas.FillRound(brandColor, 58, y + 8, 72, rowHeight - 16, 4);
                    canvas.Text(brand, 15 * f, Color.White, new(58, y + 8, 72, rowHeight - 16), true, StringAlignment.Center);
                    canvas.Text(row.CarNumber, 18 * f, style.PrimaryText, new(136, y, 60, rowHeight), true, StringAlignment.Center);
                    canvas.Text(row.DriverAbbreviation, 19 * f, style.PrimaryText, new(196, y, 100, rowHeight), true, StringAlignment.Center);
                    canvas.Text(Lap(row.LastLapTimeSeconds), 17 * f, style.PrimaryText, new(296, y, 160, rowHeight), true, StringAlignment.Center);
                    var interval = StandingsInterval(row);
                    canvas.Text(interval, 17 * f, row.IsInPitLane && !row.IsQualifying ? style.Attention : style.PrimaryText,
                        new(456, y, 120, rowHeight), true, StringAlignment.Center);
                    DrawTireEnergy(canvas, row, y, rowHeight, f);
                    y += rowHeight;
                }
            }
            if (state.Classes.Sum(group => group.Rows.Count) == 0)
                canvas.Text(T(style, OverlayTextKey.Waiting), 24 * f, style.SecondaryText, new(30, 410, 700, 60), true, StringAlignment.Center);
        });

    public static VrRenderedFrame Relative(RelativeWidgetState state) => Relative(state, DefaultStyle);

    public static VrRenderedFrame Relative(RelativeWidgetState state, VrRenderStyle style) =>
        Draw(760, 760, style, canvas =>
        {
            Header(canvas, T(style, OverlayTextKey.Relative).ToUpperInvariant(), T(style, OverlayTextKey.Gap), 760, style.TimingTextScale);
            var f = style.TimingTextScale;
            if (state.Rows.Count == 0)
            {
                canvas.Text(T(style, OverlayTextKey.Waiting), 24 * f, style.SecondaryText, new(30, 340, 700, 60), true, StringAlignment.Center);
                return;
            }
            var rowHeight = Math.Min(72, 670f / state.Rows.Count);
            var y = 82f;
            for (var index = 0; index < state.Rows.Count; index++)
            {
                var row = state.Rows[index];
                var playerBackground = Color.FromArgb(225, 230, 236);
                var background = row.IsPlayer ? playerBackground : index % 2 == 0 ? style.Background : style.Card;
                var foreground = row.IsPlayer ? Color.FromArgb(24, 31, 44) : style.PrimaryText;
                canvas.Fill(background, 0, y, 760, rowHeight);
                var classColor = ClassColor(row.ClassAbbreviation);
                canvas.FillRound(classColor, 16, y + 10, 70, rowHeight - 20, 5);
                canvas.Text(row.OverallPosition.ToString(), 23 * f, Color.White, new(16, y + 10, 70, rowHeight - 20), true, StringAlignment.Center);
                canvas.FillRound(style.PrimaryText, 86, y + 10, 76, rowHeight - 20, 5);
                canvas.Text(row.ClassAbbreviation, 18 * f, classColor, new(86, y + 10, 76, rowHeight - 20), true, StringAlignment.Center);
                canvas.Text(row.DriverDisplayName, 24 * f, foreground, new(182, y, 360, rowHeight), true);
                var gap = row.IsInPitLane && !row.IsPlayer ? "PIT" : row.IsPlayer ? "0.0" : $"{row.RelativeGapSeconds:+0.0;-0.0}";
                canvas.Text(gap, 25 * f, row.IsInPitLane && !row.IsPlayer ? style.Attention : foreground,
                    new(560, y, 180, rowHeight), true, StringAlignment.Far);
                y += rowHeight;
            }
        });

    public static VrRenderedFrame Fuel(FuelStrategyWidgetState state) => Fuel(state, DefaultStyle);

    public static VrRenderedFrame Fuel(FuelStrategyWidgetState state, VrRenderStyle style) =>
        Draw(1000, 780, style, canvas =>
        {
            Header(canvas, T(style, OverlayTextKey.FuelAndEnergy).ToUpperInvariant(), OverlayText.TranslateExact(style.Language, state.Status), 1000, 1);
            var columns = new[] { 20f, 260f, 480f, 700f, 860f };
            var widths = new[] { 240f, 220f, 220f, 160f, 120f };
            var headers = new[] { T(style, OverlayTextKey.Resource), T(style, OverlayTextKey.Current), T(style, OverlayTextKey.UsagePerLap), T(style, OverlayTextKey.Range), T(style, OverlayTextKey.Time) };
            for (var index = 0; index < headers.Length; index++)
                canvas.Text(headers[index], 16, style.SecondaryText, new(columns[index], 90, widths[index], 30), true, StringAlignment.Center);
            FuelResourceRow(canvas, T(style, OverlayTextKey.Fuel), state.Available ? $"{state.FuelLiters:0.0} L" : "--",
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
            StrategyBox(canvas, "PRIMARY STRATEGY", state.PlanSummary, state.PitPlan, state.TirePlan, 390, style.Positive);
            StrategyBox(canvas, "ALTERNATIVE", state.AlternativePlan, state.FlagScenario, state.WeatherScenario, 540, style.Information);
            canvas.Fill(style.Card, 20, 690, 960, 66);
            canvas.Text($"PACE {Lap(state.AveragePaceSeconds)} · TREND {state.PaceTrendSecondsPerLap:+0.00;-0.00;0.00}/LAP · " +
                        $"STOPS {state.EstimatedPitStops} · TYRE SETS {state.RecommendedTireSets} · CONFIDENCE {state.Confidence}",
                18, style.SecondaryText, new(36, 700, 928, 44), true, StringAlignment.Center);
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

    private static VrRenderedFrame Draw(int width, int height, VrRenderStyle style, Action<VrCanvas> paint)
    {
        using var canvas = new VrCanvas(width, height, style);
        paint(canvas);
        return new(canvas.Pixels(), (uint)width, (uint)height);
    }

    private static void Surface(VrCanvas c, float x, float y, float width, float height)
    {
        c.FillRound(c.Style.Background, x, y, width, height, 15);
        c.StrokeRound(c.Style.Accent, 3, x, y, width, height, 15);
    }

    private static void Header(VrCanvas c, string title, string subtitle, int width, float scale)
    {
        c.Fill(c.Style.Background, 0, 0, width, 82);
        c.Fill(c.Style.Accent, 0, 0, 10, 82);
        c.Text(title, 33 * scale, c.Style.PrimaryText, new(28, 8, width * 0.62f, 66), true);
        c.Text(Trim(subtitle, 28), 21 * scale, c.Style.SecondaryText,
            new(width * 0.64f, 12, width * 0.32f, 58), true, StringAlignment.Far);
    }

    private static void ValueCard(VrCanvas c, string label, string value, Color color, float x, float y, float scale)
    {
        c.FillRound(c.Style.Card, x, y, 130, 64, 6);
        c.Text(label, 14 * scale, c.Style.SecondaryText, new(x, y + 2, 130, 22), true, StringAlignment.Center);
        c.Text(value, 24 * scale, color, new(x, y + 22, 130, 38), true, StringAlignment.Center);
    }

    private static void Badge(VrCanvas c, string value, Color color, float x, float y, float width, float height, float scale)
    {
        c.FillRound(color, x, y, width, height, 6);
        c.Text(value, 18 * scale, c.Style.Background, new(x, y, width, height), true, StringAlignment.Center);
    }

    private static void DrawSteeringWheel(VrCanvas c, double steering)
    {
        var graphics = c.Graphics;
        var state = graphics.Save();
        graphics.TranslateTransform(138, 212);
        graphics.RotateTransform((float)Math.Clamp(steering, -1, 1) * 130);
        using var rim = new Pen(c.Style.PrimaryText, 18) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var spoke = new Pen(c.Style.SecondaryText, 12) { StartCap = LineCap.Round };
        graphics.DrawEllipse(rim, -82, -82, 164, 164);
        graphics.DrawLine(spoke, 0, 0, -68, -30);
        graphics.DrawLine(spoke, 0, 0, 68, -30);
        graphics.DrawLine(spoke, 0, 0, 0, 72);
        using var hub = new SolidBrush(c.Style.Accent);
        graphics.FillEllipse(hub, -24, -24, 48, 48);
        graphics.Restore(state);
    }

    private static void DrawPedalGraph(VrCanvas c, IReadOnlyList<VrPedalSample> samples, float left, float top, float width, float height)
    {
        for (var grid = 0; grid <= 4; grid++)
        {
            var y = top + (grid * height / 4);
            c.Line(Color.FromArgb(70, c.Style.SecondaryText), 1, new(left, y), new(left + width, y));
        }
        for (var index = 1; index < samples.Count; index++)
        {
            var x1 = left + ((index - 1) * width / Math.Max(1, samples.Count - 1));
            var x2 = left + (index * width / Math.Max(1, samples.Count - 1));
            c.Line(c.Style.Positive, 5, new(x1, top + height * (1 - samples[index - 1].Throttle)), new(x2, top + height * (1 - samples[index].Throttle)));
            c.Line(c.Style.Critical, 5, new(x1, top + height * (1 - samples[index - 1].Brake)), new(x2, top + height * (1 - samples[index].Brake)));
        }
    }

    private static void DrawTireEnergy(VrCanvas c, LiveStandingsRowState row, float y, float height, float scale)
    {
        if (row.IsInPitLane)
        {
            c.Text("PIT", 18 * scale, c.Style.Attention, new(576, y, 184, height), true, StringAlignment.Center);
            return;
        }
        var code = TireCode(row.TireCompound);
        var compound = code switch { "S" => Color.Red, "M" => Color.Gold, "H" => Color.White, "W" => Color.RoyalBlue, _ => Color.DimGray };
        c.FillRound(compound, 590, y + 8, 34, height - 16, 12);
        c.Text(code, 14 * scale, code is "M" or "H" ? Color.Black : Color.White, new(590, y + 8, 34, height - 16), true, StringAlignment.Center);
        var energy = row.VirtualEnergyFraction is >= 0 and <= 1 && double.IsFinite(row.VirtualEnergyFraction);
        var condition = !energy ? Color.Gray : row.VirtualEnergyFraction <= 0.1 ? c.Style.Critical : row.VirtualEnergyFraction <= 0.25 ? c.Style.Attention : c.Style.Positive;
        c.Fill(condition, 636, y + 9, 6, height - 18);
        c.Text(energy ? $"{row.VirtualEnergyFraction:P0}" : "--%", 18 * scale, c.Style.PrimaryText, new(652, y, 96, height), true);
    }

    private static void FuelResourceRow(VrCanvas c, string label, string current, string usage, double range, double seconds, float y, VrRenderStyle style)
    {
        c.Fill(style.Card, 20, y, 960, 58);
        c.Text(label, 21, style.PrimaryText, new(30, y, 230, 58), true, StringAlignment.Center);
        c.Text(current, 21, style.PrimaryText, new(260, y, 220, 58), true, StringAlignment.Center);
        c.Text(usage, 20, style.Information, new(480, y, 220, 58), true, StringAlignment.Center);
        c.Text(range > 0 ? $"{range:0.0}" : "--", 20, style.PrimaryText, new(700, y, 160, 58), true, StringAlignment.Center);
        c.Text(Minutes(seconds), 20, style.PrimaryText, new(860, y, 120, 58), true, StringAlignment.Center);
    }

    private static void StrategyBox(VrCanvas c, string title, string first, string second, string third, float y, Color accent)
    {
        c.Fill(c.Style.Card, 20, y, 960, 126);
        c.Fill(accent, 20, y, 8, 126);
        c.Text(title, 18, accent, new(42, y + 8, 300, 26), true);
        c.Text(Trim(first, 74), 20, c.Style.PrimaryText, new(42, y + 36, 916, 28), true);
        c.Text(Trim(second, 82), 17, c.Style.SecondaryText, new(42, y + 66, 916, 24), true);
        c.Text(Trim(third, 82), 17, c.Style.SecondaryText, new(42, y + 92, 916, 24), true);
    }

    private static void WeatherIcon(VrCanvas c, WeatherConditionKind weather, float x, float y, VrRenderStyle style)
    {
        var rainy = weather is WeatherConditionKind.LightRain or WeatherConditionKind.Rain or WeatherConditionKind.HeavyRain;
        var cloud = weather is not WeatherConditionKind.Clear;
        if (!cloud)
        {
            c.FillRound(style.Attention, x + 18, y + 8, 44, 44, 22);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Math.PI / 4;
                c.Line(style.Attention, 4,
                    new(x + 40 + (float)Math.Cos(angle) * 30, y + 30 + (float)Math.Sin(angle) * 30),
                    new(x + 40 + (float)Math.Cos(angle) * 39, y + 30 + (float)Math.Sin(angle) * 39));
            }
            return;
        }
        c.FillRound(style.SecondaryText, x + 5, y + 22, 70, 32, 16);
        c.FillRound(style.SecondaryText, x + 22, y + 5, 42, 42, 21);
        if (rainy)
        {
            var drops = weather == WeatherConditionKind.HeavyRain ? 4 : weather == WeatherConditionKind.Rain ? 3 : 2;
            for (var index = 0; index < drops; index++)
                c.Line(style.Information, 4, new(x + 16 + index * 16, y + 60), new(x + 10 + index * 16, y + 72));
        }
    }

    private static (string Code, Color Color) Manufacturer(string value)
    {
        var name = value.ToUpperInvariant();
        return name switch
        {
            _ when name.Contains("BMW") => ("BMW", Color.FromArgb(42, 115, 205)),
            _ when name.Contains("FERRARI") => ("FER", Color.FromArgb(238, 31, 52)),
            _ when name.Contains("PORSCHE") => ("POR", Color.FromArgb(105, 119, 133)),
            _ when name.Contains("ASTON") => ("AST", Color.FromArgb(20, 130, 92)),
            _ when name.Contains("MCLAREN") => ("MCL", Color.FromArgb(224, 104, 24)),
            _ when name.Contains("FORD") => ("FOR", Color.FromArgb(42, 115, 205)),
            _ when name.Contains("LEXUS") => ("LEX", Color.FromArgb(105, 119, 133)),
            _ when name.Contains("TOYOTA") => ("TOY", Color.FromArgb(205, 163, 20)),
            _ when name.Contains("CADILLAC") => ("CAD", Color.FromArgb(105, 119, 133)),
            _ when name.Contains("ALPINE") => ("ALP", Color.FromArgb(42, 115, 205)),
            _ => ("---", Color.FromArgb(105, 119, 133)),
        };
    }

    private static Color ClassColor(string value) => value switch
    {
        "GT3" => Color.FromArgb(0, 186, 104),
        "HYP" => Color.FromArgb(222, 30, 48),
        "P2" => Color.FromArgb(18, 180, 210),
        _ => Color.FromArgb(90, 105, 125),
    };

    private static Color FlagColor(string value, VrRenderStyle style)
    {
        var flag = value.ToUpperInvariant();
        return flag switch
        {
            _ when flag.Contains("GREEN") => style.Positive,
            _ when flag.Contains("YELLOW") => style.Attention,
            _ when flag.Contains("RED") => style.Critical,
            _ when flag.Contains("BLUE") => Color.RoyalBlue,
            _ => style.Card,
        };
    }

    private static Color GripColor(int level) => level switch
    {
        <= 0 => Color.FromArgb(105, 119, 133),
        1 => Color.FromArgb(180, 120, 30),
        2 => Color.FromArgb(30, 150, 115),
        _ => Color.FromArgb(0, 186, 104),
    };

    private static Color Blend(Color first, Color second, double amount) => Color.FromArgb(
        Math.Max(first.A, second.A),
        (int)Math.Round(first.R + ((second.R - first.R) * amount)),
        (int)Math.Round(first.G + ((second.G - first.G) * amount)),
        (int)Math.Round(first.B + ((second.B - first.B) * amount)));

    private static string StandingsInterval(LiveStandingsRowState row) => row.IsQualifying
        ? row.ClassPosition == 1 ? "LEADER" : row.IntervalSeconds >= 0 ? $"+{row.IntervalSeconds:0.000}" : "--.---"
        : row.IsInPitLane ? "PIT"
        : row.ClassPosition == 1 ? "LEADER"
        : row.IntervalLaps > 0 ? $"+{row.IntervalLaps} L"
        : row.IntervalSeconds > 0 ? $"+{row.IntervalSeconds:0.000}" : "--.---";

    private static string TireCode(string value)
    {
        var name = value.ToUpperInvariant();
        if (name.Contains("SOFT")) return "S";
        if (name.Contains("MED")) return "M";
        if (name.Contains("HARD")) return "H";
        if (name.Contains("WET")) return "W";
        return "-";
    }

    private static string Lap(double seconds) => seconds > 0 && double.IsFinite(seconds)
        ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff")
        : "--:--.---";
    private static string Clock(double seconds) => seconds > 0 && double.IsFinite(seconds)
        ? TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss")
        : "--:--:--";
    private static string Minutes(double seconds) => seconds > 0 && double.IsFinite(seconds)
        ? $"{Math.Ceiling(seconds / 60):0} MIN"
        : "-- MIN";
    private static string T(VrRenderStyle style, OverlayTextKey key) =>
        OverlayText.Get(style.Language, key);
    private static string Trim(string? value, int maximum)
    {
        value ??= string.Empty;
        return value.Length <= maximum ? value : value[..Math.Max(0, maximum - 1)] + "…";
    }
}
