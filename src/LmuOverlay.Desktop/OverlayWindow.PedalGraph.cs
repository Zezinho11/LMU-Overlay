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
    private void UpdatePedalGraph(
        DashboardWidgetState dashboard,
        double sourceTimeSeconds)
    {
        if (!dashboard.Available)
        {
            _pedalHistory.Clear();
            _lastPedalSampleTimeSeconds = double.NaN;
            RenderPedalGraph();
            return;
        }

        if (!double.IsFinite(sourceTimeSeconds))
        {
            return;
        }

        if (double.IsFinite(_lastPedalSampleTimeSeconds))
        {
            if (sourceTimeSeconds == _lastPedalSampleTimeSeconds)
            {
                return;
            }

            if (sourceTimeSeconds < _lastPedalSampleTimeSeconds)
            {
                _pedalHistory.Clear();
            }
        }

        _lastPedalSampleTimeSeconds = sourceTimeSeconds;
        _pedalHistory.Enqueue((
            sourceTimeSeconds,
            dashboard.Throttle,
            dashboard.Brake,
            dashboard.AbsActive,
            dashboard.TractionControlActive));
        var historySeconds = Math.Clamp(
            _profile.Settings.PedalHistorySeconds,
            3,
            10);
        var oldestTime = sourceTimeSeconds - historySeconds;
        while (_pedalHistory.Count > 1 &&
               _pedalHistory.Peek().TimeSeconds < oldestTime)
        {
            _pedalHistory.Dequeue();
        }

        while (_pedalHistory.Count > 512)
        {
            _pedalHistory.Dequeue();
        }

        RenderPedalGraph();
        Canvas.SetLeft(
            GForceDot,
            24 + Math.Clamp(dashboard.LateralAccelerationG / 2, -1, 1) * 20);
        Canvas.SetTop(
            GForceDot,
            46 - Math.Clamp(dashboard.LongitudinalAccelerationG / 2, -1, 1) * 38);
    }

    private void RenderPedalGraph()
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        Array.Fill(_pedalGraphPixels, Pixel(palette.Background));
        var gridColor = Pixel(OverlayVisualSystem.Mix(
            palette.Background,
            palette.SecondaryText,
            0.25));
        DrawGraphGridRow(25, gridColor);
        DrawGraphGridRow(51, gridColor);
        DrawGraphGridRow(76, gridColor);

        if (_pedalHistory.Count > 0 &&
            double.IsFinite(_lastPedalSampleTimeSeconds))
        {
            DrawPedalTrace(
                throttle: true,
                Pixel(OverlayVisualSystem.Mix(palette.Background, palette.Positive, 0.25)),
                Pixel(palette.Positive),
                Pixel(palette.Attention));
            DrawPedalTrace(
                throttle: false,
                Pixel(OverlayVisualSystem.Mix(palette.Background, palette.Critical, 0.25)),
                Pixel(palette.Critical),
                Pixel(palette.Attention));
        }

        _pedalGraphBitmap.WritePixels(
            new Int32Rect(0, 0, PedalGraphWidth, PedalGraphHeight),
            _pedalGraphPixels,
            PedalGraphWidth * sizeof(int),
            0);
    }

    private void DrawGraphGridRow(int y, int color)
    {
        for (var x = 0; x < PedalGraphWidth; x++)
        {
            _pedalGraphPixels[(y * PedalGraphWidth) + x] =
                color;
        }
    }

    private static int Pixel(System.Windows.Media.Color color) => unchecked((int)(
        0xFF000000u |
        ((uint)color.R << 16) |
        ((uint)color.G << 8) |
        color.B));

    private void DrawPedalTrace(
        bool throttle,
        int fillColor,
        int lineColor,
        int interventionColor)
    {
        var historySeconds = Math.Clamp(
            _profile.Settings.PedalHistorySeconds,
            3,
            10);
        var hasPrevious = false;
        var previousX = 0;
        var previousY = 0;
        foreach (var sample in _pedalHistory)
        {
            var x = (int)Math.Round((PedalGraphWidth - 1) - Math.Clamp(
                (_lastPedalSampleTimeSeconds - sample.TimeSeconds) / historySeconds,
                0,
                1) * (PedalGraphWidth - 1));
            var input = throttle ? sample.Throttle : sample.Brake;
            var y = (int)Math.Round((PedalGraphHeight - 1) - input * 96);
            y = Math.Clamp(y, 0, PedalGraphHeight - 1);
            if (hasPrevious)
            {
                DrawGraphFill(previousX, previousY, x, y, fillColor);
                var intervention = throttle ? sample.TcActive : sample.AbsActive;
                DrawGraphLine(
                    previousX,
                    previousY,
                    x,
                    y,
                    intervention ? interventionColor : lineColor);
            }
            else
            {
                DrawGraphFill(x, y, x, y, fillColor);
                DrawGraphPixel(x, y, lineColor, 1);
            }

            previousX = x;
            previousY = y;
            hasPrevious = true;
        }
    }

    private void DrawGraphFill(int x0, int y0, int x1, int y1, int color)
    {
        if (x1 < x0)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        var width = Math.Max(1, x1 - x0);
        for (var x = x0; x <= x1; x++)
        {
            var fraction = (x - x0) / (double)width;
            var y = (int)Math.Round(y0 + ((y1 - y0) * fraction));
            for (var fillY = Math.Clamp(y, 0, PedalGraphHeight - 1);
                 fillY < PedalGraphHeight;
                 fillY++)
            {
                _pedalGraphPixels[(fillY * PedalGraphWidth) +
                    Math.Clamp(x, 0, PedalGraphWidth - 1)] = color;
            }
        }
    }

    private void DrawGraphLine(int x0, int y0, int x1, int y1, int color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            DrawGraphPixel(x0, y0, color, 1);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var twiceError = 2 * error;
            if (twiceError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twiceError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private void DrawGraphPixel(int x, int y, int color, int radius)
    {
        for (var offsetY = -radius; offsetY <= radius; offsetY++)
        {
            var targetY = y + offsetY;
            if (targetY < 0 || targetY >= PedalGraphHeight)
            {
                continue;
            }

            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                var targetX = x + offsetX;
                if (targetX >= 0 && targetX < PedalGraphWidth)
                {
                    _pedalGraphPixels[(targetY * PedalGraphWidth) + targetX] = color;
                }
            }
        }
    }
}
