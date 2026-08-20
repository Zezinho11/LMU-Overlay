using System.Drawing;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using LmuOverlay.Widgets;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DirectComposition.DComp;
using static Vortice.DirectWrite.DWrite;
using static Vortice.DXGI.DXGI;

namespace LmuOverlay.DirectX;

internal sealed partial class DirectCompositionDashboardHost
{
    private void Draw(NativeDashboardFrame frame)
    {
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        var dashboard = frame.Dashboard;
        var scale = Math.Min(
            frame.Bounds.Width / (float)DesignWidth,
            frame.Bounds.Height / (float)DesignHeight);
        var offsetX = (frame.Bounds.Width - (DesignWidth * scale)) / 2f;
        var offsetY = (frame.Bounds.Height - (DesignHeight * scale)) / 2f;
        drawing.Transform = Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateTranslation(offsetX, offsetY);
        drawing.BeginDraw();
        drawing.Clear(new Color4(0, 0, 0, 0));

        FillRounded(drawing, 3, 3, 794, 474, 18, _panel!);
        DrawRounded(drawing, 3, 3, 794, 474, 18, _border!, 3);
        DrawDashboard(
            drawing,
            dashboard,
            frame.CapturedTimestamp,
            frame.FuelSaveFraction,
            (frame.Style ?? NativeOverlayStyle.RedFox).DashboardTitle,
            (frame.Style ?? NativeOverlayStyle.RedFox).Language,
            (frame.Style ?? NativeOverlayStyle.RedFox).DashboardShowSectors,
            (frame.Style ?? NativeOverlayStyle.RedFox).DashboardShowTires,
            (frame.Style ?? NativeOverlayStyle.RedFox).DashboardShowTelemetry,
            (frame.Style ?? NativeOverlayStyle.RedFox).DashboardModuleOrder);

        drawing.EndDraw().CheckError();
        _swapChain!.Present(1, PresentFlags.None).CheckError();
    }

    private void DrawDashboard(
        ID2D1DeviceContext drawing,
        LmuOverlay.Widgets.DashboardWidgetState dashboard,
        long timestamp,
        double fuelSaveFraction,
        string dashboardTitle,
        string language,
        bool showSectors,
        bool showTires,
        bool showTelemetry,
        string dashboardModuleOrder)
    {
        string T(LmuOverlay.Configuration.OverlayTextKey key) => LmuOverlay.Configuration.OverlayText.Get(language, key);
        DrawText(drawing, dashboard.TrackName.ToUpperInvariant(), 42, 54, 210, 20, 11, _muted!);
        DrawText(drawing, dashboardTitle, 236, 34, 328, 36, 26, _white!, TextAlignment.Center);
        DrawText(drawing, dashboard.SessionName, 606, 54, 152, 20, 11, _muted!, TextAlignment.Trailing);
        DrawShiftLights(drawing, dashboard.EngineRpmFraction);
        DrawSideLights(drawing, dashboard, timestamp, fuelSaveFraction);
        DrawPanel(drawing, 42, 86, 225, 176);
        DrawPanel(drawing, 276, 86, 248, 176);
        DrawPanel(drawing, 533, 86, 225, 176);

        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Position)} {(dashboard.Available ? dashboard.Position.ToString() : "--")}", 56, 104, 120, 24, 17, _green!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Lap)} {(dashboard.Available ? dashboard.LapNumber.ToString() : "--")}", 56, 130, 120, 22, 15, _white!);
        DrawText(drawing, dashboard.Available ? $"{T(LmuOverlay.Configuration.OverlayTextKey.Delta)} {dashboard.DeltaBestSeconds:+0.000;-0.000;0.000}" : $"{T(LmuOverlay.Configuration.OverlayTextKey.Delta)} --", 56, 156, 178, 22, 15, _amber!);
        DrawText(drawing, dashboard.Available ? $"{T(LmuOverlay.Configuration.OverlayTextKey.Fuel)} {dashboard.FuelLiters:0.0} L" : $"{T(LmuOverlay.Configuration.OverlayTextKey.Fuel)} --.- L", 56, 184, 190, 20, 13, _muted!);
        DrawText(drawing, dashboard.Available ? $"{T(LmuOverlay.Configuration.OverlayTextKey.VirtualEnergy)} {dashboard.VirtualEnergyFraction:P0}" : $"{T(LmuOverlay.Configuration.OverlayTextKey.VirtualEnergy)} --", 56, 207, 195, 20, 12, _cyan!);
        DrawText(drawing, dashboard.Available ? $"{T(LmuOverlay.Configuration.OverlayTextKey.BrakeBias)} {(1 - dashboard.RearBrakeBiasFraction):P1}" : $"{T(LmuOverlay.Configuration.OverlayTextKey.BrakeBias)} --", 56, 230, 195, 20, 12, _muted!);

        DrawText(drawing, dashboard.Available ? $"{dashboard.SpeedKilometersPerHour:0} KM/H" : "--- KM/H", 296, 98, 208, 34, 23, _muted!, TextAlignment.Center);
        DrawText(drawing, dashboard.Available ? dashboard.Gear : "N", 306, 125, 188, 96, 74, _white!, TextAlignment.Center);
        DrawText(drawing, dashboard.Available ? $"RPM {dashboard.EngineRpm:0}" : "RPM ----", 306, 224, 188, 24, 15, _muted!, TextAlignment.Center);
        if (dashboard.SpeedLimiterActive)
        {
            FillRounded(drawing, 448, 98, 58, 24, 4, _amber!);
            DrawText(drawing, "LIMIT", 448, 99, 58, 22, 12, _panel!, TextAlignment.Center);
        }

        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Current)} {FormatLap(dashboard.CurrentLapTimeSeconds)}", 548, 100, 190, 20, 13, _white!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Last)} {FormatLap(dashboard.LastLapTimeSeconds)}", 548, 123, 190, 20, 13, _muted!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Best)} {FormatLap(dashboard.BestLapTimeSeconds)}", 548, 146, 190, 20, 13, _green!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Optimal)} {FormatLap(dashboard.OptimalLapTimeSeconds)}", 548, 169, 190, 20, 13, _cyan!);
        DrawControlCards(drawing, dashboard, timestamp);
        DrawText(drawing, $"OIL {dashboard.EngineOilTemperatureCelsius:0}°  WATER {dashboard.EngineWaterTemperatureCelsius:0}°", 548, 242, 190, 16, 11, _muted!);

        DrawModules(
            drawing,
            dashboard,
            language,
            showSectors,
            showTires,
            showTelemetry,
            dashboardModuleOrder);
    }

    private void DrawModules(
        ID2D1DeviceContext drawing,
        LmuOverlay.Widgets.DashboardWidgetState dashboard,
        string language,
        bool showSectors,
        bool showTires,
        bool showTelemetry,
        string order)
    {
        var nextX = 42f;
        var baseTransform = drawing.Transform;
        foreach (var module in LmuOverlay.Configuration.DashboardModuleLayout.Parse(order))
        {
            var (baseX, width, visible) = module switch
            {
                LmuOverlay.Configuration.DashboardModule.Sectors => (42f, 225f, showSectors),
                LmuOverlay.Configuration.DashboardModule.Tires => (276f, 248f, showTires),
                _ => (533f, 225f, showTelemetry),
            };
            if (visible)
            {
                drawing.Transform = Matrix3x2.CreateTranslation(nextX - baseX, 0) * baseTransform;
                switch (module)
                {
                    case LmuOverlay.Configuration.DashboardModule.Sectors:
                        DrawPanel(drawing, 42, 272, 225, 164);
                        DrawText(drawing, LmuOverlay.Configuration.OverlayText.Get(language, LmuOverlay.Configuration.OverlayTextKey.Sectors), 42, 276, 225, 23, 13, _cyan!, TextAlignment.Center);
                        DrawSectors(drawing, dashboard);
                        break;
                    case LmuOverlay.Configuration.DashboardModule.Tires:
                        DrawPanel(drawing, 276, 272, 248, 164);
                        DrawText(drawing, LmuOverlay.Configuration.OverlayText.Get(language, LmuOverlay.Configuration.OverlayTextKey.TyreTempWear), 276, 276, 248, 23, 12, _amber!, TextAlignment.Center);
                        DrawTires(drawing, dashboard);
                        break;
                    default:
                        DrawTelemetryModule(drawing, dashboard, language);
                        break;
                }
                nextX += width + 9;
            }
        }
        drawing.Transform = baseTransform;
    }

    private void DrawTelemetryModule(
        ID2D1DeviceContext drawing,
        LmuOverlay.Widgets.DashboardWidgetState dashboard,
        string language)
    {
        string T(LmuOverlay.Configuration.OverlayTextKey key) => LmuOverlay.Configuration.OverlayText.Get(language, key);
        DrawPanel(drawing, 533, 272, 225, 164);
        DrawText(drawing, T(LmuOverlay.Configuration.OverlayTextKey.Telemetry), 533, 276, 225, 23, 13, _cyan!, TextAlignment.Center);
        DrawPedals(drawing, dashboard);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Throttle)} {dashboard.Throttle:P0}", 548, 309, 90, 20, 13,
            dashboard.TractionControlActive ? _amber! : _green!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Brake)} {dashboard.Brake:P0}", 648, 309, 90, 20, 13, _red!);
        DrawText(drawing, $"GX {dashboard.LateralAccelerationG:+0.0;-0.0;0.0}", 548, 406, 90, 20, 12, _muted!);
        DrawText(drawing, $"GY {dashboard.LongitudinalAccelerationG:+0.0;-0.0;0.0}", 648, 406, 90, 20, 12, _muted!);
    }

    private void DrawSectors(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        var sectors = dashboard.SectorTimes;
        var values = new[]
        {
            ("S1", sectors.CurrentSector1Seconds, sectors.BestSector1Seconds),
            ("S2", sectors.CurrentSector2Seconds, sectors.BestSector2Seconds),
            ("S3", sectors.CurrentSector3Seconds, sectors.BestSector3Seconds),
        };
        for (var index = 0; index < values.Length; index++)
        {
            var current = values[index].Item2;
            var personalBest = values[index].Item3;
            var y = 314 + (index * 34);
            DrawText(drawing, values[index].Item1, 58, y, 32, 22, 14, _white!);
            DrawText(drawing, current > 0 ? $"{current:0.000}" : "--.---",
                88, y, 76, 22, 14, current > 0 ? _white! : _muted!, TextAlignment.Trailing);
            DrawText(drawing, personalBest > 0 ? $"{personalBest:0.000}" : "--.---",
                170, y, 76, 22, 14, personalBest > 0 ? _purple! : _muted!, TextAlignment.Trailing);
        }
        DrawText(
            drawing,
            $"AIR {dashboard.AmbientTemperatureCelsius:0}°  TRACK {dashboard.TrackTemperatureCelsius:0}°  RAIN {dashboard.RainIntensity:P0}",
            58,
            410,
            188,
            18,
            10,
            _muted!);
    }

    private void DrawShiftLights(ID2D1DeviceContext drawing, double rpmFraction)
    {
        var active = (int)Math.Ceiling(Math.Clamp((rpmFraction - 0.65) / 0.35, 0, 1) * 12);
        for (var index = 0; index < 12; index++)
        {
            var brush = index < active
                ? index < 4 ? _green! : index < 7 ? _amber! : index < 10 ? _red! : _blue!
                : _muted!;
            drawing.FillEllipse(new Ellipse(new Vector2(323 + (index * 14), 22), 5, 5), brush);
        }
    }

    private void DrawControlCards(
        ID2D1DeviceContext drawing,
        LmuOverlay.Widgets.DashboardWidgetState dashboard,
        long timestamp)
    {
        var values = new[]
        {
            ("TC", dashboard.TractionControlLevel, dashboard.TractionControlMaximum),
            ("SLIP", dashboard.TractionControlSlipLevel, dashboard.TractionControlSlipMaximum),
            ("CUT", dashboard.TractionControlCutLevel, dashboard.TractionControlCutMaximum),
            ("ABS", dashboard.AbsLevel, dashboard.AbsMaximum),
        };
        for (var index = 0; index < values.Length; index++)
        {
            var x = 548 + (index * 48);
            var active = index == 3
                ? dashboard.AbsActive
                : dashboard.TractionControlActive;
            var blinkOn = (timestamp / Math.Max(1, Stopwatch.Frequency / 8)) % 2 == 0;
            FillRounded(drawing, x, 195, 43, 42, 4, active && blinkOn ? _amber! : _green!);
            DrawText(drawing, values[index].Item1, x, 197, 43, 14, 9, _panel!, TextAlignment.Center);
            DrawText(drawing, values[index].Item3 > 0 ? values[index].Item2.ToString() : "--", x, 211, 43, 21, 15, _white!, TextAlignment.Center);
        }
    }

    private void DrawSideLights(
        ID2D1DeviceContext drawing,
        LmuOverlay.Widgets.DashboardWidgetState dashboard,
        long timestamp,
        double fuelSaveFraction)
    {
        var blinkOn = (timestamp / Math.Max(1, Stopwatch.Frequency / 8)) % 2 == 0;
        var intervention = dashboard.AbsActive || dashboard.TractionControlActive;
        var saving = fuelSaveFraction >= 0.005;
        var brush = intervention
            ? blinkOn ? _amber! : _red!
            : saving
                ? blinkOn ? _cyan! : _blue!
                : _muted!;
        for (var index = 0; index < 6; index++)
        {
            var y = 157 + (index * 23);
            drawing.FillEllipse(new Ellipse(new Vector2(20, y), 5, 5), brush);
            drawing.FillEllipse(new Ellipse(new Vector2(780, y), 5, 5), brush);
        }
    }

    private void DrawTires(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        var tires = new[]
        {
            ("FL", dashboard.TireTemperatures.FrontLeftCelsius, dashboard.TireWear.FrontLeftFraction, 294f, 316f),
            ("FR", dashboard.TireTemperatures.FrontRightCelsius, dashboard.TireWear.FrontRightFraction, 408f, 316f),
            ("RL", dashboard.TireTemperatures.RearLeftCelsius, dashboard.TireWear.RearLeftFraction, 294f, 368f),
            ("RR", dashboard.TireTemperatures.RearRightCelsius, dashboard.TireWear.RearRightFraction, 408f, 368f),
        };
        foreach (var tire in tires)
        {
            var color = TireBrush(tire.Item2, TireTemperatureProfiles.Resolve(
                dashboard.VehicleClass, dashboard.VehicleModel, dashboard.TireCompound));
            FillRounded(drawing, tire.Item4, tire.Item5 + 1, 16, 34, 6, color);
            DrawText(drawing, $"{tire.Item1}  {tire.Item2:0}° · {tire.Item3:P0}", tire.Item4 + 22, tire.Item5, 90, 36, 14, _white!);
        }
        DrawText(drawing, $"COMPOUND {dashboard.TireCompound.ToUpperInvariant()}", 294, 414, 212, 18, 11, _amber!, TextAlignment.Center);
    }

    private ID2D1SolidColorBrush TireBrush(double temperature, TireTemperatureProfile profile) =>
        TireTemperatureClassifier.Classify(temperature, profile) switch
        {
            TireTemperatureBand.Cold => _blue!,
            TireTemperatureBand.Warming => _cyan!,
            TireTemperatureBand.Optimal => _green!,
            TireTemperatureBand.Hot => _amber!,
            TireTemperatureBand.Critical => _red!,
            _ => _muted!,
        };

    private void DrawPedals(ID2D1DeviceContext drawing, LmuOverlay.Widgets.DashboardWidgetState dashboard)
    {
        const float left = 548;
        const float top = 338;
        const float width = 190;
        const float height = 62;
        DrawRounded(drawing, left, top, width, height, 3, _muted!, 1);
        for (var row = 1; row < 4; row++)
        {
            var y = top + (height * row / 4);
            drawing.DrawLine(new Vector2(left, y), new Vector2(left + width, y), _muted!, 0.5f);
        }

        if (_pedalCount < 2)
        {
            return;
        }

        var newestIndex = (_pedalHead - 1 + _pedalHistory.Length) % _pedalHistory.Length;
        var newestTime = _pedalHistory[newestIndex].Timestamp;
        var oldestTime = newestTime - Stopwatch.Frequency * 4;
        var hasPrevious = false;
        Vector2 previousThrottle = default;
        Vector2 previousBrake = default;
        for (var offset = _pedalCount - 1; offset >= 0; offset--)
        {
            var index = (_pedalHead - 1 - offset + _pedalHistory.Length) % _pedalHistory.Length;
            var sample = _pedalHistory[index];
            if (sample.Timestamp < oldestTime)
            {
                continue;
            }

            var fraction = (sample.Timestamp - oldestTime) /
                (double)(Stopwatch.Frequency * 4);
            var x = left + ((float)fraction * width);
            var throttlePoint = new Vector2(x, top + height - (sample.Throttle * height));
            var brakePoint = new Vector2(x, top + height - (sample.Brake * height));
            if (hasPrevious)
            {
                drawing.DrawLine(
                    previousThrottle,
                    throttlePoint,
                    sample.TcActive ? _amber! : _green!,
                    sample.TcActive ? 3 : 2);
                drawing.DrawLine(
                    previousBrake,
                    brakePoint,
                    sample.AbsActive ? _amber! : _red!,
                    sample.AbsActive ? 3 : 2);
            }

            previousThrottle = throttlePoint;
            previousBrake = brakePoint;
            hasPrevious = true;
        }
    }

    private void CapturePedals(NativeDashboardFrame frame)
    {
        if (!frame.Dashboard.Available)
        {
            _pedalHead = 0;
            _pedalCount = 0;
            return;
        }

        _pedalHistory[_pedalHead] = new(
            frame.CapturedTimestamp,
            (float)Math.Clamp(frame.Dashboard.Throttle, 0, 1),
            (float)Math.Clamp(frame.Dashboard.Brake, 0, 1),
            frame.Dashboard.AbsActive,
            frame.Dashboard.TractionControlActive);
        _pedalHead = (_pedalHead + 1) % _pedalHistory.Length;
        _pedalCount = Math.Min(_pedalHistory.Length, _pedalCount + 1);
    }
}
