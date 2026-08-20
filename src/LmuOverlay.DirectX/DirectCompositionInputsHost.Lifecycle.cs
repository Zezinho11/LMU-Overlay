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
    public DirectCompositionInputsHost()
    {
        _window = CreateOverlayWindow();
        var featureLevels = new[]
        {
            Vortice.Direct3D.FeatureLevel.Level_11_1,
            Vortice.Direct3D.FeatureLevel.Level_11_0,
            Vortice.Direct3D.FeatureLevel.Level_10_1,
            Vortice.Direct3D.FeatureLevel.Level_10_0,
        };
        D3D11CreateDevice(
            IntPtr.Zero,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out _d3dDevice,
            out _d3dContext).CheckError();
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
        _dxgiFactory = CreateDXGIFactory2<IDXGIFactory2>(false);
        _compositionDevice = DCompositionCreateDevice<IDCompositionDevice>(_dxgiDevice);
        _compositionDevice.CreateTargetForHwnd(_window, true, out _compositionTarget).CheckError();
        _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
        _compositionTarget.SetRoot(_compositionVisual).CheckError();
        _writeFactory = DWriteCreateFactory<IDWriteFactory>();
    }

    public void Render(NativeInputsFrame frame)
    {
        if (frame.Sequence == _renderedSequence)
        {
            return;
        }
        if (!frame.Visible || frame.Bounds.Width < 100 || frame.Bounds.Height < 60)
        {
            if (_visible)
            {
                ShowWindow(_window, 0);
                _visible = false;
            }
            _renderedSequence = frame.Sequence;
            return;
        }

        EnsureBounds(frame.Bounds);
        if (!_visible)
        {
            ShowWindow(_window, 4);
            _visible = true;
        }
        EnsureSwapChain(frame.Bounds.Width, frame.Bounds.Height);
        ApplyStyle(frame.Style ?? NativeOverlayStyle.RedFox);
        if (!string.Equals(_sessionKey, frame.SessionKey, StringComparison.Ordinal))
        {
            _sessionKey = frame.SessionKey;
            _head = 0;
            _count = 0;
        }
        Capture(frame);
        Draw(frame);
        _renderedSequence = frame.Sequence;
    }

    public void PumpMessages()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, PmRemove))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private void EnsureBounds(NativeDashboardBounds bounds)
    {
        if (bounds == _bounds) return;
        SetWindowPos(_window, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SwpNoActivate | SwpShowWindow);
        _bounds = bounds;
    }

    private void EnsureSwapChain(int width, int height)
    {
        if (_swapChain is not null && _surface is not null && _drawing is not null &&
            _surfaceWidth == width && _surfaceHeight == height)
        {
            return;
        }
        ReleaseDrawingResources();
        _swapChain?.Dispose();
        _swapChain = _dxgiFactory.CreateSwapChainForComposition(
            _d3dDevice,
            new SwapChainDescription1
            {
                Width = (uint)width,
                Height = (uint)height,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
            },
            null);
        _compositionVisual.SetContent(_swapChain).CheckError();
        _compositionDevice.Commit().CheckError();
        _surface = _swapChain.GetBuffer<IDXGISurface>(0);
        _drawing = D2D1CreateDeviceContext(_surface, null);
        _drawing.SetDpi(96, 96);
        _surfaceWidth = width;
        _surfaceHeight = height;
        CreateDrawingResources();
    }

    private void CreateDrawingResources()
    {
        var drawing = _drawing ?? throw new InvalidOperationException("Direct2D context is unavailable.");
        _white = drawing.CreateSolidColorBrush(Color(245, 247, 250));
        _muted = drawing.CreateSolidColorBrush(Color(116, 132, 148));
        _green = drawing.CreateSolidColorBrush(Color(52, 222, 130));
        _red = drawing.CreateSolidColorBrush(Color(247, 66, 77));
        _amber = drawing.CreateSolidColorBrush(Color(255, 190, 64));
        _cyan = drawing.CreateSolidColorBrush(Color(18, 217, 229));
        _panel = drawing.CreateSolidColorBrush(Color(5, 8, 10, 238));
        _border = drawing.CreateSolidColorBrush(Color(66, 211, 166));
        _steeringWheel = LoadSteeringWheel(drawing, string.Empty);
    }

    private void ApplyStyle(NativeOverlayStyle style)
    {
        _white!.Color = Color(style.PrimaryText);
        _muted!.Color = Color(style.SecondaryText);
        _green!.Color = Color(style.Positive);
        _red!.Color = Color(style.Critical);
        _amber!.Color = Color(style.Attention);
        _cyan!.Color = Color(style.Information);
        _panel!.Color = Color(style.Background);
        _panel.Opacity = (float)Math.Clamp(style.BackgroundOpacity, 0.2, 1);
        _border!.Color = Color(style.Accent);
        _textScale = (float)Math.Clamp(style.InputsTextScale, 0.8, 1.25);
        var imagePath = style.SteeringWheelImagePath ?? string.Empty;
        if (!string.Equals(_steeringWheelImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
        {
            _steeringWheelImagePath = imagePath;
            _steeringWheel?.Dispose();
            _steeringWheel = LoadSteeringWheel(_drawing!, imagePath);
        }
    }
}
