using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using LmuOverlay.Application;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

public partial class OverlayWindow
{
    private void UpdateShiftLights(double rpmFraction)
    {
        var activeFraction = Math.Clamp((rpmFraction - 0.65) / 0.35, 0, 1);
        var activeCount = (int)Math.Ceiling(activeFraction * _shiftLights.Length);
        for (var index = 0; index < _shiftLights.Length; index++)
        {
            var fill = index < activeCount
                ? index switch
                {
                    < 4 => ShiftGreenBrush,
                    < 7 => ShiftAmberBrush,
                    < 10 => ShiftRedBrush,
                    _ => ShiftBlueBrush,
                }
                : ShiftOffBrush;
            if (!ReferenceEquals(_shiftLights[index].Fill, fill))
            {
                _shiftLights[index].Fill = fill;
            }
        }
    }

    private void UpdateTireReading(
        int tireIndex,
        Border icon,
        TextBlock text,
        double temperatureCelsius,
        double wearFraction,
        bool available,
        TireTemperatureProfile profile)
    {
        SetText(text, available
            ? $"{temperatureCelsius:0}° · {wearFraction:P0}"
            : "--° · --%");
        _tireBands[tireIndex] = available
            ? TireTemperatureClassifier.ClassifyStable(
                temperatureCelsius,
                _tireBands[tireIndex],
                configuredProfile: profile)
            : TireTemperatureBand.Unknown;
        SetBrush(icon, Border.BackgroundProperty, available
            ? _tireBands[tireIndex] switch
            {
                TireTemperatureBand.Cold => TireColdBrush,
                TireTemperatureBand.Warming => TireWarmingBrush,
                TireTemperatureBand.Optimal => TireOptimalBrush,
                TireTemperatureBand.Hot => TireHotBrush,
                TireTemperatureBand.Critical => TireCriticalBrush,
                _ => TireUnknownBrush,
            }
            : TireUnknownBrush);
    }

    private void UpdateIndicator(
        Border indicator,
        TextBlock label,
        bool configured,
        bool active,
        DateTimeOffset capturedAt)
    {
        if (active)
        {
            _indicatorHoldUntil[indicator] = capturedAt + TimeSpan.FromMilliseconds(150);
        }

        var visiblyActive = active ||
            _indicatorHoldUntil.GetValueOrDefault(indicator) > capturedAt;
        SetBrush(indicator, Border.BackgroundProperty, visiblyActive
            ? IndicatorActiveBrush
            : configured
                ? IndicatorEnabledBrush
                : IndicatorOffBrush);
        SetBrush(label, TextBlock.ForegroundProperty, visiblyActive
            ? System.Windows.Media.Brushes.White
            : configured
                ? IndicatorEnabledTextBrush
                : IndicatorTextOffBrush);
    }

    private static string FormatControlLevel(int value, int maximum) =>
        maximum > 0
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "--";

    private static void SetText(TextBlock target, string value)
    {
        if (!string.Equals(target.Text, value, StringComparison.Ordinal))
        {
            target.Text = value;
        }
    }

    private static void SetVisibility(UIElement target, Visibility value)
    {
        if (target.Visibility != value)
        {
            target.Visibility = value;
        }
    }

    private static void SetBrush(
        DependencyObject target,
        DependencyProperty property,
        System.Windows.Media.Brush value)
    {
        if (!ReferenceEquals(target.GetValue(property), value))
        {
            target.SetValue(property, value);
        }
    }

    private static string FormatLapTime(double seconds) =>
        seconds > 0 && double.IsFinite(seconds)
            ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff")
            : "--:--.---";

    private static string FormatSessionTime(double seconds) =>
        seconds > 0 && double.IsFinite(seconds)
            ? TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss")
            : "--:--:--";

    private void UpdateSectorReadings(DashboardSectorTimes sectors)
    {
        UpdateSectorReading(
            Sector1Text,
            Sector1DeltaText,
            sectors.CurrentSector1Seconds,
            sectors.BestSector1Seconds);
        UpdateSectorReading(
            Sector2Text,
            Sector2DeltaText,
            sectors.CurrentSector2Seconds,
            sectors.BestSector2Seconds);
        UpdateSectorReading(
            Sector3Text,
            Sector3DeltaText,
            sectors.CurrentSector3Seconds,
            sectors.BestSector3Seconds);
    }

    private static void UpdateSectorReading(
        TextBlock timeText,
        TextBlock personalBestText,
        double currentSeconds,
        double bestSeconds)
    {
        SetText(timeText, currentSeconds > 0 ? $"{currentSeconds:0.000}" : "--.---");
        SetText(personalBestText, bestSeconds > 0 ? $"{bestSeconds:0.000}" : "--.---");
        personalBestText.Foreground = bestSeconds > 0
            ? PersonalBestBrush
            : System.Windows.Media.Brushes.LightGray;
    }

}
