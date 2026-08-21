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

public partial class OverlayWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const long ToolWindowStyle = 0x00000080L;
    private const long NoActivateStyle = 0x08000000L;
    private const long TransparentStyle = 0x00000020L;
    private const double SnapDistance = 12;
    private const int PedalGraphWidth = 230;
    private const int PedalGraphHeight = 102;
    private static readonly Dictionary<int, System.Windows.Media.Brush> BrushCache = [];
    private static readonly ConditionalWeakTable<TextBlock, TextScaleBaseline> TextScaleBaselines = new();
    private static readonly ConditionalWeakTable<DependencyObject, ThemeBrushBaseline> ThemeBrushBaselines = new();
    private static readonly System.Windows.Media.Brush ShiftOffBrush =
        Brush(38, 49, 59);
    private static readonly System.Windows.Media.Brush ShiftGreenBrush =
        Brush(42, 218, 109);
    private static readonly System.Windows.Media.Brush ShiftAmberBrush =
        Brush(255, 190, 64);
    private static readonly System.Windows.Media.Brush ShiftRedBrush =
        Brush(255, 70, 75);
    private static readonly System.Windows.Media.Brush ShiftBlueBrush =
        Brush(65, 120, 255);
    private static readonly System.Windows.Media.Brush IndicatorOffBrush =
        Brush(32, 42, 51);
    private static readonly System.Windows.Media.Brush IndicatorTextOffBrush =
        Brush(120, 135, 149);
    private static readonly System.Windows.Media.Brush IndicatorEnabledBrush =
        Brush(15, 55, 48);
    private static readonly System.Windows.Media.Brush IndicatorEnabledTextBrush =
        Brush(66, 211, 166);
    private static readonly System.Windows.Media.Brush IndicatorActiveBrush =
        Brush(255, 135, 40);
    private static readonly System.Windows.Media.Brush PersonalBestBrush =
        Brush(193, 76, 255);
    private static readonly System.Windows.Media.Brush TireUnknownBrush =
        Brush(72, 84, 96);
    private static readonly System.Windows.Media.Brush TireColdBrush =
        Brush(52, 115, 224);
    private static readonly System.Windows.Media.Brush TireWarmingBrush =
        Brush(46, 194, 214);
    private static readonly System.Windows.Media.Brush TireOptimalBrush =
        Brush(35, 205, 105);
    private static readonly System.Windows.Media.Brush TireHotBrush =
        Brush(255, 181, 45);
    private static readonly System.Windows.Media.Brush TireCriticalBrush =
        Brush(244, 53, 68);
    private static readonly System.Windows.Media.Brush GripGreenBrush =
        Brush(218, 78, 68);
    private static readonly System.Windows.Media.Brush GripLightBrush =
        Brush(232, 135, 50);
    private static readonly System.Windows.Media.Brush GripMediumBrush =
        Brush(215, 181, 54);
    private static readonly System.Windows.Media.Brush GripHeavyBrush =
        Brush(67, 187, 105);
    private static readonly System.Windows.Media.Brush GripSaturatedBrush =
        Brush(31, 185, 169);
    private static readonly System.Windows.Media.Brush FlagGreenBrush =
        Brush(23, 133, 74);
    private static readonly System.Windows.Media.Brush FlagYellowBrush =
        Brush(184, 142, 25);
    private static readonly System.Windows.Media.Brush FlagRedBrush =
        Brush(184, 42, 53);
    private static readonly System.Windows.Media.Brush NeutralCardBrush =
        Brush(52, 64, 77);

    private readonly LayoutStore _layoutStore;
    private readonly EssentialOverlayFrameComposer _frameComposer = new();
    private readonly FuelStrategyTracker _fuelStrategyTracker = new();
    private readonly PersistentSectorReferenceTracker _sectorReferenceTracker;
    private readonly TimingWidgetTracker _timingWidgetTracker = new();
    private readonly ShiftLightTimingTracker _shiftLightTiming = new();
    private readonly Queue<(double TimeSeconds, double Throttle, double Brake, bool AbsActive, bool TcActive)> _pedalHistory = new();
    private readonly int[] _pedalGraphPixels = new int[PedalGraphWidth * PedalGraphHeight];
    private readonly System.Windows.Media.Imaging.WriteableBitmap _pedalGraphBitmap;
    private System.Windows.Shapes.Ellipse[] _shiftLights = [];
    private LayoutProfile _profile;
    private System.Windows.Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private bool _dragging;
    private FrameworkElement? _activeWidget;
    private Rect _lastGameBounds;
    private Rect _lastLayoutBounds;
    private LmuTelemetrySnapshot _lastSnapshot = LmuTelemetrySnapshot.Unavailable(
        LmuConnectionState.Disconnected,
        "No telemetry captured yet.");
    private TelemetryRuntimeHealth _runtimeHealth =
        new(0, 0, 0, 0, 0, 0, null, string.Empty);
    private DesktopPresentationHealth _presentationHealth = DesktopPresentationHealth.Empty;
    private double _lastPedalSampleTimeSeconds = double.NaN;
    private OverlayDensity _dashboardDensity = OverlayDensity.Normal;
    private string _standingsStructureKey = string.Empty;
    private string _relativeStructureKey = string.Empty;
    private readonly TireTemperatureBand[] _tireBands = new TireTemperatureBand[4];
    private readonly Dictionary<Border, DateTimeOffset> _indicatorHoldUntil = [];
    private SessionFlagsWidgetState? _alertSessionState;
    private FuelStrategyWidgetState? _alertFuelState;
    private RaceControlWidgetState? _alertRaceControlState;
    private bool _nativeDashboardActive;
    private bool _nativeInputsActive;
    private bool _nativeTimingActive;

    public OverlayWindow(
        LayoutStore layoutStore,
        SectorReferenceStore? sectorReferenceStore = null,
        PersonalBestLapStore? personalBestLapStore = null)
    {
        _layoutStore = layoutStore;
        _sectorReferenceTracker = new(
            sectorReferenceStore ?? new SectorReferenceStore(),
            personalBestLapStore ?? new PersonalBestLapStore());
        _profile = layoutStore.Load();
        InitializeComponent();
        _pedalGraphBitmap = new System.Windows.Media.Imaging.WriteableBitmap(
            PedalGraphWidth,
            PedalGraphHeight,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null);
        PedalGraphImage.Source = _pedalGraphBitmap;
        RenderPedalGraph();
        _shiftLights =
        [
            ShiftLight01, ShiftLight02, ShiftLight03, ShiftLight04,
            ShiftLight05, ShiftLight06, ShiftLight07, ShiftLight08,
            ShiftLight09, ShiftLight10, ShiftLight11, ShiftLight12,
        ];
        SourceInitialized += (_, _) => ApplyInteractionStyle();
        Loaded += (_, _) => ApplyProfile();
        SizeChanged += (_, _) =>
        {
            if (!_dragging)
            {
                ApplyProfile();
            }
        };
    }

    public bool IsEditMode { get; private set; }
    public LayoutProfile CurrentProfile => _profile;
    public string ActiveProfileName => _layoutStore.ActiveProfileName;
    public IReadOnlyList<string> ProfileNames => _layoutStore.ProfileNames;
    public int RequestedRefreshRateHz => _profile.Settings.RefreshRateHz;



    public void UpdateFrame(
        Rect gameBounds,
        LmuTelemetrySnapshot snapshot,
        bool updateSlowWidgets = true,
        double officialOptimalLapSeconds = 0,
        double? directSteeringPosition = null)
    {
        _lastSnapshot = snapshot;
        var layoutBounds = GetPlacementBounds(gameBounds);
        var boundsChanged = layoutBounds != _lastLayoutBounds;
        _lastGameBounds = gameBounds;
        _lastLayoutBounds = layoutBounds;
        if (boundsChanged)
        {
            Left = layoutBounds.Left;
            Top = layoutBounds.Top;
            Width = layoutBounds.Width;
            Height = layoutBounds.Height;
            // Switch the canvas coordinate space immediately. Waiting for the
            // next WPF arrange pass made rebased widgets disappear in edit mode.
            OverlayCanvas.Width = layoutBounds.Width;
            OverlayCanvas.Height = layoutBounds.Height;
            ApplyProfile();
        }

        var sessionEnded = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
        var dashboardSnapshot = sessionEnded
            ? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Session ended.")
            : snapshot;
        var connected = snapshot.State == LmuConnectionState.Connected && !sessionEnded;
        var essentialFrame = _frameComposer.Compose(
            dashboardSnapshot,
            _profile.Settings.SteeringWheelRangeDegrees,
            directSteeringPosition);
        var dashboard = essentialFrame.Dashboard with
        {
            EngineRpmFraction = _shiftLightTiming.Update(dashboardSnapshot.Player),
        };
        var trackedSectors = _sectorReferenceTracker.Update(
            dashboardSnapshot,
            dashboard.SectorTimes);
        var persistedOptimal = _sectorReferenceTracker.ObserveOptimal(
            dashboardSnapshot,
            officialOptimalLapSeconds);
        dashboard = dashboard with
        {
            SectorTimes = trackedSectors,
            BestLapTimeSeconds =
                _sectorReferenceTracker.PersonalBestLapTimeSeconds > 0
                    ? _sectorReferenceTracker.PersonalBestLapTimeSeconds
                    : dashboard.BestLapTimeSeconds,
        };
        if (!_nativeDashboardActive || IsEditMode)
        {
        SetText(ConnectionText, connected ? "CONECTADO" : snapshot.State.ToString().ToUpperInvariant());
        var inputs = essentialFrame.Inputs;
        SetText(TrackText, dashboard.Available ? dashboard.TrackName : "LMU");
        SetText(SpeedText, dashboard.Available
            ? $"{dashboard.SpeedKilometersPerHour:0} KM/H"
            : "--- KM/H");
        SetText(GearText, dashboard.Available ? dashboard.Gear : "N");
        SetText(RpmText, dashboard.Available
            ? $"RPM {dashboard.EngineRpm:0}"
            : "RPM ----");
        SetVisibility(
            PitLimiterIndicator,
            dashboard.Available && dashboard.SpeedLimiterActive
                ? Visibility.Visible
                : Visibility.Collapsed);
        SetText(PositionText, dashboard.Available
            ? $"POS {dashboard.Position}"
            : "POS --");
        SetText(LapText, dashboard.Available ? $"LAP {dashboard.LapNumber}" : "LAP --");
        SetText(DeltaText, dashboard.Available
            ? $"DELTA {dashboard.DeltaBestSeconds:+0.000;-0.000;0.000}"
            : "DELTA --");
        SetText(FuelText, dashboard.Available
            ? $"FUEL {dashboard.FuelLiters:0.0} L"
            : "FUEL --.- L");
        SetText(VirtualEnergyDashText, dashboard.Available
            ? $"VIRTUAL ENERGY {dashboard.VirtualEnergyFraction:P0}"
            : "VIRTUAL ENERGY --%");
        SetText(BrakeBiasText, dashboard.Available &&
            dashboard.RearBrakeBiasFraction is >= 0 and <= 1
                ? $"BRAKE BIAS {(1 - dashboard.RearBrakeBiasFraction):P1}"
                : "BRAKE BIAS --.-%");
        SetText(
            CurrentLapText,
            $"CURRENT {FormatLapTime(dashboard.CurrentLapTimeSeconds)}");
        SetText(LastLapText, $"LAST {FormatLapTime(dashboard.LastLapTimeSeconds)}");
        SetText(BestLapText, $"BEST {FormatLapTime(dashboard.BestLapTimeSeconds)}");
        EngineTempsText.Text = dashboard.Available
            ? $"OIL {dashboard.EngineOilTemperatureCelsius:0}°  " +
              $"WATER {dashboard.EngineWaterTemperatureCelsius:0}°"
            : "OIL --°  WATER --°";
        EnvironmentText.Text = dashboard.Available
            ? $"TRACK {dashboard.TrackTemperatureCelsius:0}Â°  " +
              $"AIR {dashboard.AmbientTemperatureCelsius:0}Â°  " +
              $"RAIN {dashboard.RainIntensity:P0}"
            : "TRACK --  AIR --  RAIN --%";
        SessionModeText.Text = dashboard.Available
            ? dashboard.SessionName
            : "SESSION --";
        SetText(SessionTimeText, FormatSessionTime(dashboard.SessionRemainingSeconds));
        SetText(PenaltyDashText, dashboard.OutstandingPenalties > 0
            ? $"PENALTY {dashboard.OutstandingPenalties}"
            : "PENALTY CLEAR");
        SetBrush(PenaltyDashText, TextBlock.ForegroundProperty, dashboard.OutstandingPenalties > 0
            ? System.Windows.Media.Brushes.OrangeRed
            : IndicatorEnabledTextBrush);
        SetText(SectorLastLapText, FormatLapTime(dashboard.LastLapTimeSeconds));
        SetText(SectorBestLapText, FormatLapTime(dashboard.BestLapTimeSeconds));
        UpdateSectorReadings(trackedSectors);
        SetText(
            OptimalLapText,
            $"OPTIMAL {FormatLapTime(sessionEnded ? 0 : persistedOptimal)}");
        SetText(DashboardThrottleText, dashboard.Available ? $"{dashboard.Throttle:P0}" : "--");
        SetText(DashboardBrakeText, dashboard.Available ? $"{dashboard.Brake:P0}" : "--");
        SetText(LongitudinalGText, dashboard.Available
            ? $"{dashboard.LongitudinalAccelerationG:+0.0;-0.0;0.0}"
            : "--");
        SetText(LateralGText, dashboard.Available
            ? $"{dashboard.LateralAccelerationG:+0.0;-0.0;0.0}"
            : "--");
        SetText(TireCompoundText, dashboard.Available
            ? $"COMPOUND {dashboard.TireCompound.ToUpperInvariant()}"
            : "COMPOUND --");
        UpdatePedalGraph(
            dashboard,
            snapshot.Player?.ElapsedTime ?? double.NaN);
        var tire = dashboard.TireTemperatures;
        var tireProfile = TireTemperatureProfiles.Resolve(
            dashboard.VehicleClass,
            dashboard.VehicleModel,
            dashboard.TireCompound);
        UpdateTireReading(
            0,
            FrontLeftTireIcon,
            FrontLeftTireText,
            tire.FrontLeftCelsius,
            dashboard.TireWear.FrontLeftFraction,
            dashboard.Available,
            tireProfile);
        UpdateTireReading(
            1,
            FrontRightTireIcon,
            FrontRightTireText,
            tire.FrontRightCelsius,
            dashboard.TireWear.FrontRightFraction,
            dashboard.Available,
            tireProfile);
        UpdateTireReading(
            2,
            RearLeftTireIcon,
            RearLeftTireText,
            tire.RearLeftCelsius,
            dashboard.TireWear.RearLeftFraction,
            dashboard.Available,
            tireProfile);
        UpdateTireReading(
            3,
            RearRightTireIcon,
            RearRightTireText,
            tire.RearRightCelsius,
            dashboard.TireWear.RearRightFraction,
            dashboard.Available,
            tireProfile);
        UpdateShiftLights(dashboard.Available ? dashboard.EngineRpmFraction : 0);
        SetText(TcLevelText, FormatControlLevel(
            dashboard.TractionControlLevel,
            dashboard.TractionControlMaximum));
        SetText(TcSlipLevelText, FormatControlLevel(
            dashboard.TractionControlSlipLevel,
            dashboard.TractionControlSlipMaximum));
        SetText(TcCutLevelText, FormatControlLevel(
            dashboard.TractionControlCutLevel,
            dashboard.TractionControlCutMaximum));
        SetText(AbsLevelText, FormatControlLevel(
            dashboard.AbsLevel,
            dashboard.AbsMaximum));
        UpdateIndicator(
            AbsIndicator,
            AbsIndicatorText,
            dashboard.AbsLevel > 0,
            dashboard.AbsActive,
            snapshot.CapturedAt);
        UpdateIndicator(
            TcIndicator,
            TcIndicatorText,
            dashboard.TractionControlLevel > 0,
            dashboard.TractionControlActive,
            snapshot.CapturedAt);
        SetText(InputsText, inputs.Available
            ? $"THR {inputs.Throttle:P0}  BRK {inputs.Brake:P0}  STR {inputs.Steering:P0}"
            : "THR --  BRK --  STR --");
        }
        if (updateSlowWidgets)
        {
            var sessionState = EssentialWidgetStateFactory.CreateSessionFlags(snapshot);
            if (!_nativeTimingActive || IsEditMode)
            {
                var timing = _timingWidgetTracker.Update(
                    snapshot,
                    _profile.Settings.LiveStandingsMaximumRows,
                    _profile.Settings.RelativeCarsEachSide);
                UpdateStandings(timing.Standings);
                UpdateRelative(timing.Relative);
            }
            UpdateSessionFlags(sessionState);

            var fuelStrategy = _fuelStrategyTracker.Update(
                snapshot,
                new FuelStrategyOptions(
                    _profile.Settings.FuelReserveLaps,
                    _profile.Settings.EnergyReservePercent / 100,
                    _profile.Settings.ManualRemainingLaps,
                    _profile.Settings.MaximumStintLaps,
                    _profile.Settings.EstimatedPitLossSeconds,
                    _profile.Settings.AvailableTireSets,
                    _profile.Settings.TireWearLimitPercent / 100,
                    _profile.Settings.EstimatedTireChangeSeconds,
                    _profile.Settings.ManualRemainingMinutes,
                    _profile.Settings.ManualLapTimeSeconds,
                    _profile.Settings.ManualFuelPerLapLiters,
                    _profile.Settings.ManualFuelCapacityLiters));
            UpdateFuelStrategy(fuelStrategy);
            var raceControl = EssentialWidgetStateFactory.CreateRaceControl(snapshot);
            UpdateRaceControl(raceControl);
            _alertSessionState = sessionState;
            _alertFuelState = fuelStrategy;
            _alertRaceControlState = raceControl;
        }

        if (_alertSessionState is not null &&
            _alertFuelState is not null &&
            _alertRaceControlState is not null)
        {
            UpdatePriorityAlert(
                dashboard,
                _alertSessionState,
                _alertFuelState,
                _alertRaceControlState);
        }

        SetGameAvailable(connected || IsEditMode);
    }






}
