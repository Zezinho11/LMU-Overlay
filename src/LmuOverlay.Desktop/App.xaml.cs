using System.Drawing;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.DirectX;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.Widgets;
using WinForms = System.Windows.Forms;

namespace LmuOverlay.Desktop;

public partial class App
{
    private readonly DispatcherTimer _windowTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(100),
    };

    private OverlayWindow? _overlay;
    private OverlayToolbarWindow? _toolbar;
    private ConfigurationWindow? _configurationWindow;
    private WinForms.NotifyIcon? _trayIcon;
    private Icon? _trayIconImage;
    private TelemetryRuntime? _telemetryRuntime;
    private NativeDashboardRenderer? _nativeDashboard;
    private NativeInputsRenderer? _nativeInputs;
    private NativeTimingRenderer? _nativeTiming;
    private bool _isExiting;
    private System.Windows.Rect? _gameBounds;
    private long _lastRenderedAt;
    private long _lastSlowUpdateAt;
    private LmuTelemetrySnapshot? _lastRenderedSnapshot;
    private long _nativeDashboardSequence;
    private NativeDashboardConfiguration? _nativeDashboardConfiguration;
    private long _nativeInputsSequence;
    private NativeInputsConfiguration? _nativeInputsConfiguration;
    private readonly FuelStrategyTracker _nativeFuelStrategyTracker = new();
    private long _lastNativeFuelStrategyAt;
    private double _nativeFuelSaveFraction;
    private long _nativeTimingSequence;
    private LiveStandingsWidgetState? _nativeLiveStandingsState;
    private RelativeWidgetState? _nativeRelativeState;
    private readonly TimingWidgetTracker _nativeTimingTracker = new();
    private SectorReferenceStore _sectorReferenceStore = null!;
    private PersonalBestLapStore _personalBestLapStore = null!;
    private GameCompatibilityReport _compatibility = null!;
    private PersistentSectorReferenceTracker? _nativeSectorReferenceTracker;
    private OfficialTimingOptimalProvider? _officialTimingOptimal;
    private bool _safeMode;
    private string _nativeSessionKey = string.Empty;
    private string _nativeSessionTrack = string.Empty;
    private int _nativeSessionCode = int.MinValue;
    private int _nativeSessionVehicleId = int.MinValue;







}
