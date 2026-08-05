using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public sealed record VrRenderedFrame(byte[] Pixels, uint Width, uint Height);

public static class VrWidgetTextureRenderer
{
    private static readonly Color Background = Color.FromArgb(238, 5, 9, 18);
    private static readonly Color Panel = Color.FromArgb(255, 18, 27, 45);
    private static readonly Color Red = Color.FromArgb(255, 170, 24, 36);
    private static readonly Color Cyan = Color.FromArgb(255, 28, 180, 201);
    private static readonly Color Green = Color.FromArgb(255, 43, 211, 147);
    private static readonly Color Yellow = Color.FromArgb(255, 255, 190, 30);
    private static readonly Color Muted = Color.FromArgb(255, 151, 164, 186);

    public static VrRenderedFrame Dashboard(DashboardWidgetState state) =>
        new(VrDashboardTexture.Render(state), VrDashboardTexture.Width, VrDashboardTexture.Height);

    public static VrRenderedFrame LiveStandings(LiveStandingsWidgetState state) =>
        Draw(640, 960, graphics =>
        {
            DrawHeader(graphics, "LIVE STANDINGS", state.PlayerClass, 640);
            var y = 92;
            foreach (var group in state.Classes)
            {
                Fill(graphics, group.IsPlayerClass ? Red : Color.FromArgb(255, 30, 48, 78), 20, y, 600, 36);
                Text(graphics, group.ClassName.ToUpperInvariant(), 28, Color.White, 32, y + 3, true);
                y += 40;
                foreach (var row in group.Rows)
                {
                    DrawRowBackground(graphics, row.IsPlayer, y, 600, 46);
                    Text(graphics, row.ClassPosition.ToString(), 25, row.ClassPosition == 1 ? Yellow : Color.White, 30, y + 8, true);
                    Text(graphics, row.CarNumber, 23, Muted, 84, y + 10, true);
                    Text(graphics, row.DriverAbbreviation, 25, Color.White, 155, y + 8, true);
                    Text(graphics, FormatLap(row.LastLapTimeSeconds), 23, Color.White, 290, y + 10);
                    var interval = row.IsInPitLane ? "PIT" : FormatGap(row.GapToLeaderSeconds);
                    Text(graphics, interval, 23, row.IsInPitLane ? Yellow : Muted, 470, y + 10, true);
                    y += 48;
                    if (y > 900)
                    {
                        return;
                    }
                }
            }
        });

    public static VrRenderedFrame Relative(RelativeWidgetState state) =>
        Draw(640, 800, graphics =>
        {
            DrawHeader(graphics, "RELATIVE", $"{state.Rows.Count} CARS", 640);
            var y = 92;
            foreach (var row in state.Rows.Take(13))
            {
                DrawRowBackground(graphics, row.IsPlayer, y, 600, 54);
                Fill(graphics, ClassColor(row.ClassAbbreviation), 28, y + 8, 92, 38);
                Text(graphics, row.CarNumber, 22, Color.White, 35, y + 13, true);
                Text(graphics, row.ClassAbbreviation, 20, Color.White, 75, y + 14, true);
                Text(graphics, row.DriverDisplayName, 25, Color.White, 138, y + 11, true);
                var gap = row.IsInPitLane
                    ? "PIT"
                    : row.IsPlayer ? "0.0" : $"{row.RelativeGapSeconds:+0.0;-0.0}";
                Text(graphics, gap, 26, row.IsInPitLane ? Yellow : Color.White, 500, y + 11, true);
                y += 57;
            }
        });

    public static VrRenderedFrame Fuel(FuelStrategyWidgetState state) =>
        Draw(800, 700, graphics =>
        {
            DrawHeader(graphics, "FUEL / ENERGY", state.Status, 800);
            var rows = new (string Label, string Value, Color Color)[]
            {
                ("FUEL", state.Available ? $"{state.FuelLiters:0.0} L" : "--", Color.White),
                ("CONSUMPTION", state.Learning ? "LEARNING" : $"{state.ProjectedConsumptionLitersPerLap:0.00} L/LAP", Cyan),
                ("RANGE", state.Available ? $"{state.EstimatedRangeLaps:0.0} LAPS" : "--", Color.White),
                ("VIRTUAL ENERGY", state.Available ? $"{state.VirtualEnergyFraction:P0}" : "--", Color.Magenta),
                ("NEXT PIT", state.SuggestedPitLap > 0 ? $"LAP {state.SuggestedPitLap}" : "--", Yellow),
                ("PLAN", string.IsNullOrWhiteSpace(state.PlanSummary) ? "LEARNING" : state.PlanSummary, Green),
                ("PIT WINDOWS", string.IsNullOrWhiteSpace(state.PitPlan) ? "--" : state.PitPlan, Color.White),
                ("TYRES", string.IsNullOrWhiteSpace(state.TirePlan) ? "--" : state.TirePlan, Color.White),
            };
            var y = 105;
            foreach (var row in rows)
            {
                Fill(graphics, Panel, 24, y, 752, 58);
                Text(graphics, row.Label, 23, Muted, 42, y + 16, true);
                Text(graphics, Trim(row.Value, 33), 25, row.Color, 270, y + 14, true);
                y += 63;
            }
            Text(graphics, Trim(state.WeatherScenario, 54), 20, Cyan, 35, 626, true);
            Text(graphics, Trim(state.TrafficScenario, 54), 20, Muted, 35, 654, true);
        });

    public static VrRenderedFrame Session(SessionFlagsWidgetState state) =>
        Draw(900, 300, graphics =>
        {
            DrawHeader(graphics, state.SessionName.Length > 0 ? state.SessionName : "SESSION", state.PhaseName, 900);
            Fill(graphics, FlagColor(state.FlagName), 24, 104, 200, 70);
            Text(graphics, state.FlagName, 31, Color.White, 42, 120, true);
            Fill(graphics, Panel, 242, 104, 300, 70);
            Text(graphics, $"GRIP {state.TrackGripName}", 28, Color.White, 262, 121, true);
            Fill(graphics, Panel, 560, 104, 316, 70);
            Text(graphics, state.WeatherName, 28, Color.White, 580, 121, true);
            Text(graphics, $"AIR {state.AmbientTemperatureCelsius:0}°", 26, Muted, 40, 214, true);
            Text(graphics, $"TRACK {state.TrackTemperatureCelsius:0}°", 26, Muted, 235, 214, true);
            Text(graphics, $"RAIN {state.RainIntensity:P0}", 26, Cyan, 500, 214, true);
            Text(graphics, $"LAP {state.CurrentLap}/{(state.MaximumLaps > 0 ? state.MaximumLaps : 0)}", 26, Color.White, 690, 214, true);
        });

    private static VrRenderedFrame Draw(int width, int height, Action<Graphics> paint)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Background);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        paint(graphics);
        return new(VrDashboardTexture.ToRgba(bitmap), (uint)width, (uint)height);
    }

    private static void DrawHeader(Graphics graphics, string title, string subtitle, int width)
    {
        Fill(graphics, Color.FromArgb(255, 8, 14, 35), 0, 0, width, 82);
        Fill(graphics, Red, 0, 0, 10, 82);
        Text(graphics, title, 34, Color.White, 28, 14, true);
        Text(graphics, Trim(subtitle, 24), 21, Muted, width - 235, 28, true);
    }

    private static void DrawRowBackground(Graphics graphics, bool player, int y, int width, int height) =>
        Fill(graphics, player ? Color.FromArgb(255, 21, 105, 127) : Panel, 20, y, width, height);

    private static void Fill(Graphics graphics, Color color, int x, int y, int width, int height)
    {
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, x, y, width, height);
    }

    private static void Text(
        Graphics graphics,
        string value,
        float size,
        Color color,
        float x,
        float y,
        bool bold = false)
    {
        using var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        graphics.DrawString(value, font, brush, x, y);
    }

    private static Color ClassColor(string value) => value switch
    {
        "GT3" => Green,
        "HYP" => Red,
        "P2" => Cyan,
        _ => Color.FromArgb(255, 90, 105, 125),
    };

    private static Color FlagColor(string value) => value switch
    {
        "GREEN" => Green,
        "YELLOW" => Yellow,
        "RED" => Red,
        _ => Color.FromArgb(255, 60, 74, 92),
    };

    private static string FormatLap(double seconds) => seconds > 0
        ? $"{(int)(seconds / 60)}:{seconds % 60:00.000}"
        : "--:--.---";

    private static string FormatGap(double seconds) => seconds > 0 ? $"+{seconds:0.0}" : "LEADER";
    private static string Trim(string value, int maximum) =>
        value.Length <= maximum ? value : value[..Math.Max(0, maximum - 1)] + "…";
}
