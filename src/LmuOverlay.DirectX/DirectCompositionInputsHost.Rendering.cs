using System.Drawing;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
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

internal sealed unsafe partial class DirectCompositionInputsHost
{
    private void Capture(NativeInputsFrame frame)
    {
        if (!frame.Inputs.Available)
        {
            _head = 0;
            _count = 0;
            return;
        }
        _history[_head] = new(
            frame.CapturedTimestamp,
            (float)Math.Clamp(frame.Inputs.Throttle, 0, 1),
            (float)Math.Clamp(frame.Inputs.Brake, 0, 1),
            frame.Inputs.AbsActive,
            frame.Inputs.TractionControlActive);
        _head = (_head + 1) % _history.Length;
        _count = Math.Min(_history.Length, _count + 1);
    }

    private void Draw(NativeInputsFrame frame)
    {
        var language = (frame.Style ?? NativeOverlayStyle.RedFox).Language;
        string T(LmuOverlay.Configuration.OverlayTextKey key) => LmuOverlay.Configuration.OverlayText.Get(language, key);
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        var scale = Math.Min(frame.Bounds.Width / (float)DesignWidth, frame.Bounds.Height / (float)DesignHeight);
        var offsetX = (frame.Bounds.Width - DesignWidth * scale) / 2f;
        var offsetY = (frame.Bounds.Height - DesignHeight * scale) / 2f;
        drawing.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);
        drawing.BeginDraw();
        drawing.Clear(new Color4(0, 0, 0, 0));
        FillRounded(drawing, 2, 2, 516, 216, 12, _panel!);
        DrawRounded(drawing, 2, 2, 516, 216, 12, _border!, 2);
        DrawText(drawing, T(LmuOverlay.Configuration.OverlayTextKey.DriverInputs).ToUpperInvariant(), 18, 12, 260, 28, 17, _white!);
        DrawSteering(
            drawing,
            frame.Inputs.Steering,
            frame.Inputs.SteeringWheelRangeDegrees);
        DrawGraph(drawing);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Throttle)} {frame.Inputs.Throttle:P0}", 172, 184, 82, 22, 13,
            frame.Inputs.TractionControlActive ? _amber! : _green!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Brake)} {frame.Inputs.Brake:P0}", 258, 184, 82, 22, 13,
            frame.Inputs.AbsActive ? _amber! : _red!);
        DrawText(drawing, $"{T(LmuOverlay.Configuration.OverlayTextKey.Clutch)} {frame.Inputs.Clutch:P0}", 344, 184, 82, 22, 13, _cyan!);
        DrawText(drawing, $"STR {frame.Inputs.Steering:+0%;-0%;0%}", 430, 184, 72, 22, 12, _white!, TextAlignment.Trailing);
        if (frame.Inputs.TractionControlActive)
        {
            FillRounded(drawing, 368, 13, 64, 25, 4, _amber!);
            DrawText(drawing, "TC", 368, 14, 64, 22, 13, _panel!, TextAlignment.Center);
        }
        if (frame.Inputs.AbsActive)
        {
            var blinkOn = (frame.CapturedTimestamp / Math.Max(1, Stopwatch.Frequency / 8)) % 2 == 0;
            FillRounded(drawing, 438, 13, 64, 25, 4, blinkOn ? _amber! : _red!);
            DrawText(drawing, "ABS", 438, 14, 64, 22, 13, _panel!, TextAlignment.Center);
        }
        drawing.EndDraw().CheckError();
        _swapChain!.Present(1, PresentFlags.None).CheckError();
    }

    private void DrawSteering(
        ID2D1DeviceContext drawing,
        double steering,
        double rangeDegrees)
    {
        var center = new Vector2(88, 112);
        var angle = (float)(LmuOverlay.Widgets.SteeringWheelRotation.AngleDegrees(
            steering,
            rangeDegrees) * Math.PI / 180);
        if (_steeringWheel is not null)
        {
            drawing.FillEllipse(new Ellipse(center, 56, 56), _muted!);
            drawing.DrawEllipse(new Ellipse(center, 57, 57), _cyan!, 1.5f);
            var transform = drawing.Transform;
            drawing.Transform = Matrix3x2.CreateRotation(angle, center) * transform;
            drawing.DrawBitmap(
                _steeringWheel,
                new Vortice.RawRectF(30, 54, 146, 170),
                1,
                Vortice.Direct2D1.InterpolationMode.Linear,
                null,
                null);
            drawing.Transform = transform;
            DrawText(drawing, "STEERING", 34, 169, 108, 20, 11, _muted!, TextAlignment.Center);
            return;
        }

        const float radius = 49;
        drawing.DrawEllipse(new Ellipse(center, radius, radius), _white!, 5);
        for (var index = 0; index < 3; index++)
        {
            var spoke = angle + (float)(index * Math.PI * 2 / 3);
            var end = center + new Vector2(MathF.Cos(spoke), MathF.Sin(spoke)) * 42;
            drawing.DrawLine(center, end, _cyan!, 5);
        }
        drawing.FillEllipse(new Ellipse(center, 10, 10), _white!);
        DrawText(drawing, "STEERING", 34, 169, 108, 20, 11, _muted!, TextAlignment.Center);
    }

    private void DrawGraph(ID2D1DeviceContext drawing)
    {
        const float left = 172;
        const float top = 48;
        const float width = 330;
        const float height = 125;
        DrawRounded(drawing, left, top, width, height, 3, _muted!, 1);
        for (var row = 1; row < 4; row++)
        {
            var y = top + height * row / 4;
            drawing.DrawLine(new Vector2(left, y), new Vector2(left + width, y), _muted!, 0.5f);
        }
        if (_count < 2) return;
        var newestIndex = (_head - 1 + _history.Length) % _history.Length;
        var newest = _history[newestIndex].Timestamp;
        var oldest = newest - Stopwatch.Frequency * 4;
        var hasPrevious = false;
        Vector2 previousThrottle = default;
        Vector2 previousBrake = default;
        for (var offset = _count - 1; offset >= 0; offset--)
        {
            var sample = _history[(_head - 1 - offset + _history.Length) % _history.Length];
            if (sample.Timestamp < oldest) continue;
            var x = left + (float)((sample.Timestamp - oldest) /
                (double)(Stopwatch.Frequency * 4)) * width;
            var throttle = new Vector2(x, top + height - sample.Throttle * height);
            var brake = new Vector2(x, top + height - sample.Brake * height);
            if (hasPrevious)
            {
                drawing.DrawLine(previousThrottle, throttle,
                    sample.TcActive ? _amber! : _green!,
                    sample.TcActive ? 3 : 2);
                drawing.DrawLine(previousBrake, brake, sample.AbsActive ? _amber! : _red!,
                    sample.AbsActive ? 3 : 2);
            }
            previousThrottle = throttle;
            previousBrake = brake;
            hasPrevious = true;
        }
    }
}
