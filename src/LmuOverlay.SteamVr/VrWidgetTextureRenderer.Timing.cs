using System.Drawing;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

public static partial class VrWidgetTextureRenderer
{
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
}
