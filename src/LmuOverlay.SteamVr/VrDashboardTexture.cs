using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static class VrDashboardTexture
{
    public const int Width = 1200;
    public const int Height = 720;

    public static byte[] Render(DashboardWidgetState dashboard) =>
        Render(dashboard, VrRenderStyle.From(new VrDesktopSettings()), null);

    public static byte[] Render(
        DashboardWidgetState dashboard,
        VrRenderStyle style,
        IReadOnlyList<VrPedalSample>? pedalHistory)
    {
        using var canvas = new VrCanvas(Width, Height, style);
        canvas.FillRound(style.Background, 4, 4, Width - 8, Height - 8, 22);
        canvas.StrokeRound(style.Accent, 4, 4, 4, Width - 8, Height - 8, 22);
        DrawHeader(canvas, dashboard, style);
        DrawTop(canvas, dashboard, style);
        DrawSectors(canvas, dashboard, style);
        DrawTires(canvas, dashboard, style);
        DrawTelemetry(canvas, dashboard, style, pedalHistory);
        return canvas.Pixels();
    }

    private static void DrawHeader(VrCanvas c, DashboardWidgetState state, VrRenderStyle style)
    {
        var scale = style.DashboardTextScale;
        c.Text(state.TrackName.Length > 0 ? state.TrackName : "LMU", 17 * scale,
            style.SecondaryText, new(45, 42, 300, 34), true);
        c.Text(style.DashboardTitle, 34 * scale, style.PrimaryText,
            new(350, 28, 500, 50), true, StringAlignment.Center);
        c.Text(state.SessionName, 17 * scale, style.SecondaryText,
            new(860, 42, 290, 34), true, StringAlignment.Far);
        var active = (int)Math.Ceiling(Math.Clamp((state.EngineRpmFraction - 0.65) / 0.35, 0, 1) * 12);
        for (var index = 0; index < 12; index++)
        {
            var color = index >= active
                ? Color.FromArgb(55, style.SecondaryText)
                : index < 5 ? style.Positive : index < 8 ? style.Attention : index < 10 ? style.Critical : Color.RoyalBlue;
            c.FillRound(color, 426 + (index * 30), 83, 20, 20, 10);
        }
    }

    private static void DrawTop(VrCanvas c, DashboardWidgetState d, VrRenderStyle s)
    {
        string T(OverlayTextKey key) => OverlayText.Get(s.Language, key);
        Panel(c, 38, 118, 330, 270);
        Panel(c, 382, 118, 430, 270);
        Panel(c, 826, 118, 336, 270);
        var f = s.DashboardTextScale;
        c.Text($"{T(OverlayTextKey.Position)} {(d.Available ? d.Position.ToString() : "--")}", 26 * f, s.Positive, new(60, 138, 210, 34), true);
        c.Text($"{T(OverlayTextKey.Lap)} {(d.Available ? d.LapNumber.ToString() : "--")}", 23 * f, s.PrimaryText, new(60, 176, 210, 32), true);
        c.Text(d.Available ? $"{T(OverlayTextKey.Delta)} {d.DeltaBestSeconds:+0.000;-0.000;0.000}" : $"{T(OverlayTextKey.Delta)} --", 23 * f, s.Attention, new(60, 214, 280, 32), true);
        c.Text(d.Available ? $"{T(OverlayTextKey.Fuel)} {d.FuelLiters:0.0} L" : $"{T(OverlayTextKey.Fuel)} --.- L", 19 * f, s.SecondaryText, new(60, 254, 290, 28), true);
        c.Text(d.Available ? $"{T(OverlayTextKey.VirtualEnergy)} {d.VirtualEnergyFraction:P0}" : $"{T(OverlayTextKey.VirtualEnergy)} --", 18 * f, Color.Magenta, new(60, 289, 295, 28), true);
        c.Text(d.Available ? $"{T(OverlayTextKey.BrakeBias)} {(1 - d.RearBrakeBiasFraction):P1}" : $"{T(OverlayTextKey.BrakeBias)} --", 18 * f, s.SecondaryText, new(60, 324, 295, 28), true);

        c.Text(d.Available ? $"{d.SpeedKilometersPerHour:0} KM/H" : "--- KM/H", 31 * f,
            s.SecondaryText, new(410, 128, 374, 44), true, StringAlignment.Center);
        c.Text(d.Available ? d.Gear : "N", 120 * f, s.PrimaryText,
            new(440, 165, 314, 145), true, StringAlignment.Center);
        c.Text(d.Available ? $"RPM {d.EngineRpm:0}" : "RPM ----", 23 * f,
            s.SecondaryText, new(410, 321, 374, 36), true, StringAlignment.Center);
        if (d.SpeedLimiterActive)
        {
            c.FillRound(s.Attention, 690, 132, 94, 38, 6);
            c.Text("LIMIT", 18 * f, s.Background, new(690, 132, 94, 38), true, StringAlignment.Center);
        }

        c.Text($"{T(OverlayTextKey.Current)} {Lap(d.CurrentLapTimeSeconds)}", 19 * f, s.PrimaryText, new(848, 135, 290, 30), true);
        c.Text($"{T(OverlayTextKey.Last)} {Lap(d.LastLapTimeSeconds)}", 19 * f, s.SecondaryText, new(848, 169, 290, 30), true);
        c.Text($"{T(OverlayTextKey.Best)} {Lap(d.BestLapTimeSeconds)}", 19 * f, s.Positive, new(848, 203, 290, 30), true);
        c.Text($"{T(OverlayTextKey.Optimal)} {Lap(d.OptimalLapTimeSeconds)}", 19 * f, s.Information, new(848, 237, 290, 30), true);
        var controls = new[]
        {
            ("TC", d.TractionControlLevel, d.TractionControlMaximum, d.TractionControlActive),
            ("SLIP", d.TractionControlSlipLevel, d.TractionControlSlipMaximum, false),
            ("CUT", d.TractionControlCutLevel, d.TractionControlCutMaximum, false),
            ("ABS", d.AbsLevel, d.AbsMaximum, d.AbsActive),
        };
        for (var index = 0; index < controls.Length; index++)
        {
            var x = 848 + (index * 72);
            c.FillRound(controls[index].Item4 ? Color.FromArgb(145, s.Positive) : s.Card, x, 278, 64, 54, 5);
            c.Text(controls[index].Item1, 12 * f, s.SecondaryText, new(x, 280, 64, 19), true, StringAlignment.Center);
            c.Text(Level(controls[index].Item2, controls[index].Item3), 20 * f, s.PrimaryText, new(x, 298, 64, 28), true, StringAlignment.Center);
        }
        c.Text($"OIL {d.EngineOilTemperatureCelsius:0}°  WATER {d.EngineWaterTemperatureCelsius:0}°",
            16 * f, s.SecondaryText, new(848, 344, 290, 24), true);
    }

    private static void DrawSectors(VrCanvas c, DashboardWidgetState d, VrRenderStyle s)
    {
        Panel(c, 38, 404, 330, 270);
        c.Fill(s.Information, 38, 404, 330, 38);
        c.Text(OverlayText.Get(s.Language, OverlayTextKey.Sectors), 18 * s.DashboardTextScale, s.Background, new(38, 404, 330, 38), true, StringAlignment.Center);
        var values = new[]
        {
            (d.SectorTimes.CurrentSector1Seconds, d.SectorTimes.BestSector1Seconds),
            (d.SectorTimes.CurrentSector2Seconds, d.SectorTimes.BestSector2Seconds),
            (d.SectorTimes.CurrentSector3Seconds, d.SectorTimes.BestSector3Seconds),
        };
        for (var index = 0; index < 3; index++)
        {
            var y = 458 + (index * 54);
            c.Text($"S{index + 1}", 22 * s.DashboardTextScale, s.PrimaryText, new(60, y, 55, 38), true);
            c.Text(Sector(values[index].Item1), 21 * s.DashboardTextScale, s.PrimaryText, new(116, y, 102, 38), true, StringAlignment.Far);
            c.Text(Sector(values[index].Item2), 21 * s.DashboardTextScale, Color.MediumPurple, new(228, y, 112, 38), true, StringAlignment.Far);
        }
        c.Text($"AIR {d.AmbientTemperatureCelsius:0}°  TRACK {d.TrackTemperatureCelsius:0}°  RAIN {d.RainIntensity:P0}",
            15 * s.DashboardTextScale, s.SecondaryText, new(55, 624, 295, 30), true, StringAlignment.Center);
    }

    private static void DrawTires(VrCanvas c, DashboardWidgetState d, VrRenderStyle s)
    {
        Panel(c, 382, 404, 430, 270);
        c.Fill(s.Attention, 382, 404, 430, 38);
        c.Text(OverlayText.Get(s.Language, OverlayTextKey.TyreTempWear), 18 * s.DashboardTextScale, s.Background, new(382, 404, 430, 38), true, StringAlignment.Center);
        var tires = new[]
        {
            ("FL", d.TireTemperatures.FrontLeftCelsius, d.TireWear.FrontLeftFraction, 410f, 470f),
            ("FR", d.TireTemperatures.FrontRightCelsius, d.TireWear.FrontRightFraction, 615f, 470f),
            ("RL", d.TireTemperatures.RearLeftCelsius, d.TireWear.RearLeftFraction, 410f, 555f),
            ("RR", d.TireTemperatures.RearRightCelsius, d.TireWear.RearRightFraction, 615f, 555f),
        };
        foreach (var tire in tires)
        {
            var color = TireColor(tire.Item2);
            c.FillRound(color, tire.Item4, tire.Item5, 32, 54, 11);
            c.Text(tire.Item1, 19 * s.DashboardTextScale, s.PrimaryText, new(tire.Item4 + 44, tire.Item5, 44, 28), true);
            c.Text(d.Available ? $"{tire.Item2:0}° · {(1 - tire.Item3):P0}" : "--° · --%",
                20 * s.DashboardTextScale, s.PrimaryText, new(tire.Item4 + 82, tire.Item5, 112, 34), true);
        }
        c.Text($"{OverlayText.Get(s.Language, OverlayTextKey.Compound)} {(string.IsNullOrWhiteSpace(d.TireCompound) ? "--" : d.TireCompound.ToUpperInvariant())}",
            16 * s.DashboardTextScale, s.Attention, new(410, 630, 374, 25), true, StringAlignment.Center);
    }

    private static void DrawTelemetry(
        VrCanvas c,
        DashboardWidgetState d,
        VrRenderStyle s,
        IReadOnlyList<VrPedalSample>? history)
    {
        Panel(c, 826, 404, 336, 270);
        c.Fill(s.Information, 826, 404, 336, 38);
        c.Text(OverlayText.Get(s.Language, OverlayTextKey.Telemetry), 18 * s.DashboardTextScale, s.Background, new(826, 404, 336, 38), true, StringAlignment.Center);
        c.Text($"{OverlayText.Get(s.Language, OverlayTextKey.Throttle)} {d.Throttle:P0}", 17 * s.DashboardTextScale, s.Positive, new(846, 451, 110, 28), true);
        c.Text($"{OverlayText.Get(s.Language, OverlayTextKey.Brake)} {d.Brake:P0}", 17 * s.DashboardTextScale, s.Critical, new(963, 451, 110, 28), true);
        c.Text($"GX {d.LateralAccelerationG:+0.0;-0.0;0.0}  GY {d.LongitudinalAccelerationG:+0.0;-0.0;0.0}",
            15 * s.DashboardTextScale, s.SecondaryText, new(846, 482, 290, 25), true);
        const float left = 846, top = 518, width = 290, height = 125;
        for (var grid = 0; grid <= 4; grid++)
        {
            var y = top + (grid * height / 4);
            c.Line(Color.FromArgb(70, s.SecondaryText), 1, new(left, y), new(left + width, y));
        }
        var samples = history is { Count: > 1 }
            ? history
            : new[] { new VrPedalSample((float)d.Throttle, (float)d.Brake), new((float)d.Throttle, (float)d.Brake) };
        for (var index = 1; index < samples.Count; index++)
        {
            var x1 = left + ((index - 1) * width / Math.Max(1, samples.Count - 1));
            var x2 = left + (index * width / Math.Max(1, samples.Count - 1));
            c.Line(s.Positive, 4, new(x1, top + height * (1 - samples[index - 1].Throttle)), new(x2, top + height * (1 - samples[index].Throttle)));
            c.Line(s.Critical, 4, new(x1, top + height * (1 - samples[index - 1].Brake)), new(x2, top + height * (1 - samples[index].Brake)));
        }
    }

    private static void Panel(VrCanvas c, float x, float y, float width, float height)
    {
        c.Fill(c.Style.Card, x, y, width, height);
        c.StrokeRound(Color.FromArgb(100, c.Style.SecondaryText), 2, x, y, width, height, 4);
    }

    private static string Lap(double seconds) => seconds > 0 && double.IsFinite(seconds)
        ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff")
        : "--:--.---";
    private static string Sector(double seconds) => seconds > 0 && double.IsFinite(seconds) ? $"{seconds:0.000}" : "--.---";
    private static string Level(int value, int maximum) => maximum > 0 ? value.ToString() : "--";
    private static Color TireColor(double temperature) => temperature switch
    {
        < 65 => Color.RoyalBlue,
        < 95 => Color.FromArgb(50, 220, 125),
        < 110 => Color.Orange,
        _ => Color.Red,
    };
}
