using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class VrWidgetTextureRenderer
{
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
            c.Line(samples[index].TcActive ? c.Style.Attention : c.Style.Positive,
                samples[index].TcActive ? 7 : 5,
                new(x1, top + height * (1 - samples[index - 1].Throttle)),
                new(x2, top + height * (1 - samples[index].Throttle)));
            c.Line(samples[index].AbsActive ? c.Style.Attention : c.Style.Critical,
                samples[index].AbsActive ? 7 : 5,
                new(x1, top + height * (1 - samples[index - 1].Brake)),
                new(x2, top + height * (1 - samples[index].Brake)));
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

    private static void StrategyBoxFourLines(
        VrCanvas c,
        string title,
        string first,
        string second,
        string third,
        string fourth,
        float y,
        Color accent)
    {
        c.Fill(c.Style.Card, 20, y, 960, 150);
        c.Fill(accent, 20, y, 8, 150);
        c.Text(title, 18, accent, new(42, y + 6, 300, 24), true);
        c.Text(Trim(first, 74), 19, c.Style.PrimaryText, new(42, y + 30, 916, 27), true);
        c.Text(Trim(second, 82), 16, c.Style.SecondaryText, new(42, y + 58, 916, 22), true);
        c.Text(Trim(third, 82), 16, c.Style.SecondaryText, new(42, y + 84, 916, 22), true);
        c.Text(Trim(fourth, 82), 16, c.Style.SecondaryText, new(42, y + 110, 916, 28), true);
    }

    private static void FuelSaveStrategyBox(
        VrCanvas c,
        string title,
        string summary,
        string saveTargets,
        string consumptionTargets,
        string pitPlan,
        string tirePlan,
        float y,
        Color accent)
    {
        c.Fill(c.Style.Card, 20, y, 960, 170);
        c.Fill(accent, 20, y, 8, 170);
        c.Text(title, 17, accent, new(42, y + 4, 300, 22), true);
        c.Text(Trim(summary, 92), 16, c.Style.PrimaryText, new(42, y + 25, 916, 21), true);
        c.Text(Trim(saveTargets, 104), 15, accent, new(42, y + 46, 916, 20), true);
        c.Text(Trim(consumptionTargets, 104), 14, c.Style.SecondaryText, new(42, y + 66, 916, 19), true);

        var lineY = y + 86;
        foreach (var line in WrapStrategyPlan(pitPlan, 104, 2))
        {
            c.Text(line, 14, c.Style.SecondaryText, new(42, lineY, 916, 18), true);
            lineY += 18;
        }
        foreach (var line in WrapStrategyPlan(tirePlan, 104, 2))
        {
            c.Text(line, 14, c.Style.Information, new(42, lineY, 916, 18), true);
            lineY += 18;
        }
    }

    private static IReadOnlyList<string> WrapStrategyPlan(
        string value,
        int maximumCharacters,
        int maximumLines)
    {
        if (value.Length <= maximumCharacters)
            return new[] { value };

        var segments = value.Split(" · ", StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var segment in segments)
        {
            var candidate = current.Length == 0 ? segment : $"{current} · {segment}";
            if (candidate.Length <= maximumCharacters || current.Length == 0)
            {
                current = candidate;
                continue;
            }

            lines.Add(current);
            current = segment;
        }
        if (current.Length > 0)
            lines.Add(current);

        if (lines.Count <= maximumLines)
            return lines;

        var visible = lines.Take(maximumLines - 1).ToList();
        visible.Add(string.Join(" · ", lines.Skip(maximumLines - 1)));
        return visible;
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

    private static (string Code, Color Color) Manufacturer(
        string vehicleModel,
        string vehicleName)
    {
        var identity = VehicleCatalog.Resolve(vehicleModel, vehicleName);
        return (identity.Code, ColorTranslator.FromHtml(identity.Color));
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
