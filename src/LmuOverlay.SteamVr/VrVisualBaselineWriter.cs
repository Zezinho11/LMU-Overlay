using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using LmuOverlay.Domain;
using LmuOverlay.Widgets;

namespace LmuOverlay.SteamVr;

/// <summary>
/// Produces deterministic PNGs with the exact SteamVR texture renderer. This is
/// intentionally independent from OpenVR so visual baselines can be reviewed in CI
/// and before a physical headset validation pass.
/// </summary>
public static class VrVisualBaselineWriter
{
    public static IReadOnlyList<string> Write(string outputDirectory, OverlayProfileSettings? settings = null)
    {
        Directory.CreateDirectory(outputDirectory);
        settings = LayoutStore.SanitizeSettings(settings);
        var style = VrRenderStyle.From(settings);
        var fixture = CreateFixture();
        var history = Enumerable.Range(0, 150)
            .Select(index =>
            {
                var phase = index / 149d;
                var throttle = Math.Clamp((phase - 0.22) * 1.7, 0, 1);
                var brake = Math.Clamp(1 - phase * 3.4, 0, 0.88);
                return new VrPedalSample((float)throttle, (float)brake);
            })
            .ToArray();

        var frames = new Dictionary<string, VrRenderedFrame>(StringComparer.Ordinal)
        {
            ["01-dashboard.png"] = VrWidgetTextureRenderer.Dashboard(fixture.Dashboard, style, history),
            ["02-live-standings.png"] = VrWidgetTextureRenderer.LiveStandings(fixture.Standings, style),
            ["03-relative.png"] = VrWidgetTextureRenderer.Relative(fixture.Relative, style),
            ["04-inputs.png"] = VrWidgetTextureRenderer.Inputs(fixture.Inputs, style, history),
            ["05-fuel-strategy.png"] = VrWidgetTextureRenderer.Fuel(fixture.Fuel, style),
            ["06-session-flags.png"] = VrWidgetTextureRenderer.Session(fixture.Session, style),
            ["07-race-control.png"] = VrWidgetTextureRenderer.RaceControl(fixture.RaceControl, style),
            ["08-priority-alert.png"] = VrWidgetTextureRenderer.PriorityAlert(
                fixture.Dashboard,
                fixture.Session,
                fixture.Fuel,
                fixture.RaceControl,
                style,
                settings),
        };

        var written = new List<string>();
        foreach (var pair in frames)
        {
            var path = Path.GetFullPath(Path.Combine(outputDirectory, pair.Key));
            using var bitmap = ToBitmap(pair.Value);
            bitmap.Save(path, ImageFormat.Png);
            written.Add(path);
        }

        var compositePath = Path.GetFullPath(Path.Combine(outputDirectory, "00-headset-composite.png"));
        WriteComposite(compositePath, frames);
        written.Insert(0, compositePath);
        return written;
    }

    private static void WriteComposite(string path, IReadOnlyDictionary<string, VrRenderedFrame> frames)
    {
        using var output = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(output);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var background = new LinearGradientBrush(
            new Rectangle(0, 0, output.Width, output.Height),
            Color.FromArgb(13, 18, 25),
            Color.FromArgb(1, 3, 5),
            90);
        graphics.FillRectangle(background, 0, 0, output.Width, output.Height);
        using var horizon = new Pen(Color.FromArgb(32, 66, 211, 166), 2);
        graphics.DrawLine(horizon, 0, 525, 1920, 525);

        DrawPanel(graphics, frames["08-priority-alert.png"], new(570, 32, 780, 130));
        DrawPanel(graphics, frames["02-live-standings.png"], new(40, 125, 405, 512));
        DrawPanel(graphics, frames["03-relative.png"], new(1475, 170, 405, 405));
        DrawPanel(graphics, frames["01-dashboard.png"], new(505, 650, 910, 410));
        DrawPanel(graphics, frames["04-inputs.png"], new(65, 745, 400, 169));
        DrawPanel(graphics, frames["05-fuel-strategy.png"], new(1455, 660, 430, 335));
        DrawPanel(graphics, frames["06-session-flags.png"], new(565, 195, 790, 244));
        DrawPanel(graphics, frames["07-race-control.png"], new(760, 455, 400, 249));

        using var labelFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelBrush = new SolidBrush(Color.FromArgb(170, 215, 224, 232));
        graphics.DrawString("SIMULAÇÃO DE COMPOSIÇÃO NO HMD · TEXTURAS REAIS DO STEAMVR", labelFont, labelBrush, 610, 1040);
        output.Save(path, ImageFormat.Png);
    }

    private static void DrawPanel(Graphics graphics, VrRenderedFrame frame, Rectangle target)
    {
        using var bitmap = ToBitmap(frame);
        graphics.DrawImage(bitmap, target);
    }

    private static Bitmap ToBitmap(VrRenderedFrame frame)
    {
        var bitmap = new Bitmap((int)frame.Width, (int)frame.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var bgra = new byte[frame.Pixels.Length];
            for (var index = 0; index < frame.Pixels.Length; index += 4)
            {
                bgra[index] = frame.Pixels[index + 2];
                bgra[index + 1] = frame.Pixels[index + 1];
                bgra[index + 2] = frame.Pixels[index];
                bgra[index + 3] = frame.Pixels[index + 3];
            }
            Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    private static Fixture CreateFixture()
    {
        var dashboard = new DashboardWidgetState(
            Available: true,
            SpeedKilometersPerHour: 287,
            Gear: "6",
            EngineRpm: 8125,
            EngineRpmFraction: 0.91,
            FuelLiters: 41.8,
            Position: 3,
            LapNumber: 42,
            TrackName: "Circuit de Spa-Francorchamps",
            DeltaBestSeconds: -0.184,
            CurrentLapTimeSeconds: 78.412,
            LastLapTimeSeconds: 132.805,
            BestLapTimeSeconds: 131.972,
            EngineWaterTemperatureCelsius: 83,
            EngineOilTemperatureCelsius: 91,
            RearBrakeBiasFraction: 0.485,
            SpeedLimiterActive: false,
            AbsActive: true,
            TractionControlActive: true,
            TractionControlLevel: 4,
            TractionControlMaximum: 12,
            TractionControlSlipLevel: 6,
            TractionControlSlipMaximum: 12,
            TractionControlCutLevel: 5,
            TractionControlCutMaximum: 12,
            AbsLevel: 6,
            AbsMaximum: 12,
            TireTemperatures: new(84, 87, 79, 81),
            TireWear: new(0.12, 0.14, 0.10, 0.11),
            Throttle: 0.92,
            Brake: 0.04,
            LongitudinalAccelerationG: 0.71,
            LateralAccelerationG: -1.26,
            AmbientTemperatureCelsius: 22,
            TrackTemperatureCelsius: 31,
            RainIntensity: 0,
            SessionRemainingSeconds: 5140,
            SessionName: "RACE",
            OutstandingPenalties: 0,
            TireCompound: "MEDIUM",
            SectorTimes: new(42.181, 73.204, 0, 42.360, 73.512, 16.933, 42.008, 72.980, 16.821),
            VirtualEnergyFraction: 0.73)
        {
            OptimalLapTimeSeconds = 131.809,
        };

        var standingsRows = new[]
        {
            Row(1, "J. M. Lopez", "LOP", "Cadillac V-Series.R", "2", 131.684, 0, false, 0.82, "MEDIUM"),
            Row(2, "R. Kubica", "KUB", "Ferrari 499P", "83", 131.812, 0.128, false, 0.76, "MEDIUM"),
            Row(3, "Z. RedFox", "RED", "BMW M Hybrid V8", "11", 131.972, 0.160, true, 0.73, "MEDIUM"),
            Row(4, "K. Kobayashi", "KOB", "Toyota GR010", "7", 132.095, 0.123, false, 0.68, "MEDIUM"),
            Row(5, "M. Conway", "CON", "Toyota GR010", "8", 132.340, 0.245, false, 0.54, "MEDIUM"),
            Row(6, "A. Lynn", "LYN", "Cadillac V-Series.R", "12", 132.518, 0.178, false, 0.31, "MEDIUM"),
            Row(7, "N. Lapierre", "LAP", "Alpine A424", "36", 132.901, 0.383, false, 0.21, "MEDIUM"),
        };
        var standings = new LiveStandingsWidgetState(
            "HYPERCAR",
            new[] { new LiveStandingsClassState("HYPERCAR", true, standingsRows) },
            "RACE",
            5140,
            false);
        var relative = new RelativeWidgetState(new[]
        {
            Relative(1, "LOP", "HYP", -28.4),
            Relative(9, "VAN", "GT3", -13.7),
            Relative(2, "KUB", "HYP", -4.2),
            Relative(3, "RED", "HYP", 0, true),
            Relative(4, "KOB", "HYP", 3.8),
            Relative(16, "HAR", "GT3", 14.6),
            Relative(5, "CON", "HYP", 26.1),
        });
        // Upright in the baseline so the shared Desktop artwork is directly
        // recognizable; live rendering still rotates from the steering signal.
        var inputs = new InputsWidgetState(true, 0.92, 0.04, 0, 0, true, true);
        var fuel = new FuelStrategyWidgetState(
            Available: true, Learning: false, FuelLiters: 41.8,
            AverageConsumptionLitersPerLap: 2.73, Samples: 12,
            EstimatedRangeLaps: 15.3, EstimatedRangeTimeSeconds: 2020,
            EstimatedLapsToFinish: 39, EstimatedTimeToFinishSeconds: 5140,
            RequiredFuelLiters: 106.5, FuelMarginLiters: 1.8,
            VirtualEnergyFraction: 0.73, AverageVirtualEnergyFractionPerLap: 0.041,
            EstimatedVirtualEnergyRangeLaps: 17.8, EstimatedVirtualEnergyRangeTimeSeconds: 2350,
            RequiredVirtualEnergyFraction: 0.62, VirtualEnergyMarginFraction: 0.11,
            ProjectedConsumptionLitersPerLap: 2.78, TargetConsumptionLitersPerLap: 2.73,
            RequiredFuelSavingFraction: 0.018, LapsUntilPit: 14, SuggestedPitLap: 56,
            FuelToAddLiters: 38.2, Confidence: "HIGH (12/12)", Status: "GOOD")
        {
            EstimatedPitStops = 2,
            PlanSummary = "FULL PUSH · 3 STINTS · 2 STOPS · 1:25:40",
            PitPlan = "L56 +42.7L · L70 +23.4L",
            TirePlan = "2 SETS · CHANGE AT L56",
            FuelSavePlan = "SAVE 3.2% · EXTEND FIRST STINT TO L57",
            FuelSavePitPlan = "L57 +40.0L · L71 +20.5L",
            FuelSaveTirePlan = "2 SETS · CHANGE AT L57",
            FuelSaveVirtualEnergyTargetPerLap = 0.039,
            FinalFuelToAddLiters = 23.4,
            FinalVirtualEnergyTargetFraction = 0.49,
            AveragePaceSeconds = 131.9,
            PaceTrendSecondsPerLap = 0.03,
            FinishProbability = 0.94,
        };
        var session = new SessionFlagsWidgetState(
            true, "RACE", "GREEN FLAG", "GREEN", "HIGH GRIP", 3,
            WeatherConditionKind.PartlyCloudy, "PARTLY CLOUDY", 0, 0.42, 0,
            5140, 42, 80, 22, 31);
        var raceControl = new RaceControlWidgetState(
            true, 1, "DRIVE THROUGH · 2 LAPS", "PIT WINDOW OPEN", "VALID",
            "GREEN", "BODYWORK OK", "NO RECENT IMPACT", "SYSTEMS NOMINAL", true, false);
        return new(dashboard, standings, relative, inputs, fuel, session, raceControl);
    }

    private static LiveStandingsRowState Row(
        int position, string name, string abbreviation, string model, string number,
        double lap, double interval, bool player, double energy, string tire) => new(
        position, name, abbreviation, model, model, number, 41, interval, interval, 0,
        lap, lap, player, false, false, energy, tire, 2);

    private static RelativeRowState Relative(
        int position, string driver, string vehicleClass, double gap, bool player = false) => new(
        position, driver, driver, vehicleClass, vehicleClass, (position * 7).ToString(),
        gap, 0, player, false)
        {
            GapSource = player ? RelativeGapSource.Player : RelativeGapSource.DistanceEstimate,
            GapConfidence = player ? 1 : 0.85,
        };

    private sealed record Fixture(
        DashboardWidgetState Dashboard,
        LiveStandingsWidgetState Standings,
        RelativeWidgetState Relative,
        InputsWidgetState Inputs,
        FuelStrategyWidgetState Fuel,
        SessionFlagsWidgetState Session,
        RaceControlWidgetState RaceControl);
}
