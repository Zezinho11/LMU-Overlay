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

internal sealed unsafe partial class DirectCompositionInputsHost : IDisposable
{
    private const int DesignWidth = 520;
    private const int DesignHeight = 220;
    private const int SteeringWheelPixels = 512;
    private const string SteeringWheelResource =
        "LmuOverlay.DirectX.Assets.steering-wheel-bgra.bin";
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExNoRedirectionBitmap = 0x00200000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint PmRemove = 0x0001;
    private const uint WmDestroy = 0x0002;
    private const uint WmEraseBackground = 0x0014;
    private const uint WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly WindowProcedure WindowProc = ProcessWindowMessage;

    private readonly IntPtr _window;
    private readonly ID3D11Device _d3dDevice;
    private readonly ID3D11DeviceContext _d3dContext;
    private readonly IDXGIDevice _dxgiDevice;
    private readonly IDXGIFactory2 _dxgiFactory;
    private readonly IDCompositionDevice _compositionDevice;
    private readonly IDCompositionTarget _compositionTarget;
    private readonly IDCompositionVisual _compositionVisual;
    private readonly IDWriteFactory _writeFactory;
    private readonly Dictionary<float, IDWriteTextFormat> _textFormats = [];
    private readonly InputSample[] _history = new InputSample[768];
    private IDXGISwapChain1? _swapChain;
    private IDXGISurface? _surface;
    private ID2D1DeviceContext? _drawing;
    private ID2D1SolidColorBrush? _white;
    private ID2D1SolidColorBrush? _muted;
    private ID2D1SolidColorBrush? _green;
    private ID2D1SolidColorBrush? _red;
    private ID2D1SolidColorBrush? _amber;
    private ID2D1SolidColorBrush? _cyan;
    private ID2D1SolidColorBrush? _panel;
    private ID2D1SolidColorBrush? _border;
    private ID2D1Bitmap1? _steeringWheel;
    private NativeDashboardBounds _bounds;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private int _head;
    private int _count;
    private long _renderedSequence = -1;
    private bool _visible;
    private string _sessionKey = string.Empty;
    private string _steeringWheelImagePath = string.Empty;
    private float _textScale = 1;





}
