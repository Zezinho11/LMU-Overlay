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

    public LmuOverlay.DirectX.NativeDashboardBounds GetNativeDashboardBounds(
        Rect gameBounds)
        => GetNativeBounds(gameBounds, _profile.Diagnostic, DiagnosticWidget.Name);

    public LmuOverlay.DirectX.NativeDashboardBounds GetNativeLiveStandingsBounds(
        Rect gameBounds)
        => GetNativeBounds(
            gameBounds,
            _profile.LiveStandings,
            LiveStandingsWidget.Name);

    public LmuOverlay.DirectX.NativeDashboardBounds GetNativeInputsBounds(
        Rect gameBounds)
        => GetNativeBounds(gameBounds, _profile.Inputs, InputsWidget.Name);

    public LmuOverlay.DirectX.NativeDashboardBounds GetNativeRelativeBounds(
        Rect gameBounds)
        => GetNativeBounds(gameBounds, _profile.Relative, RelativeWidget.Name);

    private static LmuOverlay.DirectX.NativeDashboardBounds GetNativeBounds(
        Rect gameBounds,
        WidgetPlacement placement,
        string widgetName)
    {
        var bounds = ResponsiveWidgetLayout.Calculate(
            gameBounds.Width,
            gameBounds.Height,
            placement,
            ResponsiveWidgetLayout.For(widgetName));
        return new(
            (int)Math.Round(gameBounds.Left + bounds.X),
            (int)Math.Round(gameBounds.Top + bounds.Y),
            Math.Max(1, (int)Math.Round(bounds.Width)),
            Math.Max(1, (int)Math.Round(bounds.Height)));
    }

    public bool NativeDashboardShouldBeVisible =>
        _profile.Diagnostic.Visible && !IsEditMode;

    public bool NativeInputsShouldBeVisible =>
        _profile.Inputs.Visible && !IsEditMode;

    public bool NativeLiveStandingsShouldBeVisible =>
        _profile.LiveStandings.Visible && !IsEditMode;
    public bool NativeRelativeShouldBeVisible =>
        _profile.Relative.Visible && !IsEditMode;
    public double NativeLiveStandingsOpacity => NativeOpacity(_profile.LiveStandings);
    public double NativeRelativeOpacity => NativeOpacity(_profile.Relative);
    public double NativeDashboardOpacity => NativeOpacity(_profile.Diagnostic);
    public double NativeInputsOpacity => NativeOpacity(_profile.Inputs);
    public LmuOverlay.DirectX.NativeOverlayStyle NativeStyle
    {
        get
        {
            var palette = OverlayVisualSystem.Resolve(_profile.Settings);
            return new(
                NativeColor(palette.Background),
                NativeColor(palette.Card),
                NativeColor(palette.Accent),
                NativeColor(palette.PrimaryText),
                NativeColor(palette.SecondaryText),
                NativeColor(palette.Information),
                NativeColor(palette.Attention),
                NativeColor(palette.Critical),
                NativeColor(palette.Positive),
                _profile.Settings.Theme == "HighContrast" ? 1 : _profile.Settings.BackgroundOpacity,
                _profile.Settings.DashboardTitle,
                _profile.Settings.DashboardTextScale,
                _profile.Settings.TimingTextScale,
                _profile.Settings.InputsTextScale,
                _profile.Settings.Language,
                _profile.Settings.DashboardShowSectors,
                _profile.Settings.DashboardShowTires,
                _profile.Settings.DashboardShowTelemetry,
                _profile.Settings.DashboardModuleOrder,
                _profile.Settings.SteeringWheelImagePath);
        }
    }

    private double NativeOpacity(WidgetPlacement placement) =>
        _profile.Settings.Theme == "HighContrast"
            ? 1
            : placement.Opacity * _profile.Settings.BackgroundOpacity;

    private static LmuOverlay.DirectX.NativeOverlayColor NativeColor(
        System.Windows.Media.Color color) => new(color.R, color.G, color.B);

    public int LiveStandingsMaximumRows => _profile.Settings.LiveStandingsMaximumRows;
    public int RelativeCarsEachSide => _profile.Settings.RelativeCarsEachSide;

    public void SetNativeDashboardActive(bool active)
    {
        if (_nativeDashboardActive == active)
        {
            return;
        }

        _nativeDashboardActive = active;
        ApplyProfile();
    }

    public void SetNativeTimingActive(bool active)
    {
        if (_nativeTimingActive == active)
        {
            return;
        }

        _nativeTimingActive = active;
        ApplyProfile();
    }

    public void SetNativeInputsActive(bool active)
    {
        if (_nativeInputsActive == active)
        {
            return;
        }

        _nativeInputsActive = active;
        ApplyProfile();
    }

    public void UpdateRuntimeHealth(TelemetryRuntimeHealth health) =>
        _runtimeHealth = health;

    public void UpdatePresentationHealth(DesktopPresentationHealth health) =>
        _presentationHealth = health;

    public void ExportDiagnostics(string destinationPath) =>
        DiagnosticsReportWriter.Write(
            destinationPath,
            _lastSnapshot,
            _runtimeHealth,
            _presentationHealth,
            _profile,
            ActiveProfileName);

    public void CapturePng(string destinationPath, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        OverlayCanvas.Width = width;
        OverlayCanvas.Height = height;
        OverlayCanvas.Measure(new System.Windows.Size(width, height));
        OverlayCanvas.Arrange(new Rect(0, 0, width, height));
        ApplyProfile();
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(OverlayCanvas);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(destinationPath);
        encoder.Save(stream);
    }

    public void SwitchProfile(string name)
    {
        _profile = _layoutStore.Switch(name);
        ApplyProfile();
    }

    public void CreateProfile(string name, bool duplicateCurrent)
    {
        _profile = _layoutStore.Create(
            name,
            duplicateCurrent ? _profile : LayoutProfile.Default);
        ApplyProfile();
    }

    public void RenameProfile(string newName) =>
        _layoutStore.Rename(ActiveProfileName, newName);

    public void DeleteActiveProfile()
    {
        _profile = _layoutStore.Delete(ActiveProfileName);
        ApplyProfile();
    }

    public void ExportActiveProfile(string destinationPath) =>
        _layoutStore.Export(ActiveProfileName, destinationPath);

    public void ImportProfile(string sourcePath)
    {
        var importedName = _layoutStore.Import(sourcePath);
        _profile = _layoutStore.Switch(importedName);
        ApplyProfile();
    }

    public void ApplyPreset(string name)
    {
        _profile = LayoutPresets.Create(name);
        ApplyProfile();
        _layoutStore.Save(_profile);
    }

    public void UpdateFrame(
        Rect gameBounds,
        LmuTelemetrySnapshot snapshot,
        bool updateSlowWidgets = true,
        double officialOptimalLapSeconds = 0)
    {
        _lastSnapshot = snapshot;
        var boundsChanged = gameBounds != _lastGameBounds;
        _lastGameBounds = gameBounds;
        if (boundsChanged)
        {
            Left = gameBounds.Left;
            Top = gameBounds.Top;
            Width = gameBounds.Width;
            Height = gameBounds.Height;
            if (!IsEditMode)
            {
                ApplyProfile();
            }
        }

        var sessionEnded = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
        var dashboardSnapshot = sessionEnded
            ? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Session ended.")
            : snapshot;
        var connected = snapshot.State == LmuConnectionState.Connected && !sessionEnded;
        var essentialFrame = _frameComposer.Compose(dashboardSnapshot);
        var dashboard = essentialFrame.Dashboard;
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

    public void SetGameAvailable(bool available)
    {
        if (available && !IsVisible)
        {
            Show();
        }
        else if (!available && IsVisible)
        {
            Hide();
        }
    }

    public void SetEditMode(bool enabled)
    {
        if (!enabled && IsEditMode)
        {
            // Capture geometry while the WPF editor surfaces still have their
            // real arranged sizes. Native mode collapses them immediately.
            SaveProfile();
        }

        IsEditMode = enabled;
        ResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        InputsResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        LiveStandingsResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        RelativeResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        SessionFlagsResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        FuelStrategyResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        RaceControlResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        EditHint.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        var borderBrush = enabled
            ? System.Windows.Media.Brushes.Orange
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(66, 211, 166));
        DiagnosticWidget.BorderBrush = borderBrush;
        InputsWidget.BorderBrush = borderBrush;
        LiveStandingsWidget.BorderBrush = borderBrush;
        RelativeWidget.BorderBrush = borderBrush;
        SessionFlagsWidget.BorderBrush = borderBrush;
        FuelStrategyWidget.BorderBrush = borderBrush;
        RaceControlWidget.BorderBrush = borderBrush;
        _profile = _profile with
        {
            Settings = _profile.Settings with { Locked = !enabled },
        };
        ApplyProfile();
        ApplyInteractionStyle();
        if (enabled && _lastGameBounds.Width > 0)
        {
            SetGameAvailable(true);
            Activate();
        }
        else
        {
            _layoutStore.Save(_profile);
        }
    }

    public void ResetLayout()
    {
        _profile = LayoutProfile.Default;
        ApplyProfile();
        SaveProfile();
    }

    public void ApplyDisplaySettings(LayoutProfile profile)
    {
        _profile = profile with
        {
            SchemaVersion = LayoutProfile.CurrentSchemaVersion,
        };
        ApplyProfile();
        _layoutStore.Save(_profile);
    }

    private void ApplyProfile()
    {
        if (LayoutWidth <= 0 || LayoutHeight <= 0)
        {
            return;
        }

        ApplyPlacement(DiagnosticWidget, _profile.Diagnostic);
        ApplyPlacement(InputsWidget, _profile.Inputs);
        ApplyPlacement(LiveStandingsWidget, _profile.LiveStandings);
        ApplyPlacement(RelativeWidget, _profile.Relative);
        ApplyPlacement(SessionFlagsWidget, _profile.SessionFlags);
        ApplyPlacement(FuelStrategyWidget, _profile.FuelStrategy);
        ApplyPlacement(RaceControlWidget, _profile.RaceControl);
        PriorityAlert.Width = Math.Min(460, Math.Max(240, LayoutWidth - 24));
        Canvas.SetLeft(PriorityAlert, Math.Max(12, (LayoutWidth - PriorityAlert.Width) / 2));
        ApplyTheme();
        OverlayLocalization.Apply(this, _profile.Settings.Language);
    }

    private void ApplyPlacement(
        FrameworkElement element,
        WidgetPlacement placement)
    {
        var renderedNatively = !IsEditMode &&
            ((element == DiagnosticWidget && _nativeDashboardActive) ||
             (element == InputsWidget && _nativeInputsActive) ||
             ((element == LiveStandingsWidget || element == RelativeWidget) &&
              _nativeTimingActive));
        element.Visibility = placement.Visible &&
            !renderedNatively
                ? Visibility.Visible
                : Visibility.Collapsed;
        element.Opacity = 1;
        var bounds = ResponsiveWidgetLayout.Calculate(
            LayoutWidth,
            LayoutHeight,
            placement,
            ResponsiveWidgetLayout.For(element.Name));
        element.Width = bounds.Width;
        element.Height = bounds.Height;
        Canvas.SetLeft(element, bounds.X);
        Canvas.SetTop(element, bounds.Y);
        if (element == DiagnosticWidget)
        {
            ApplyDashboardDensity(OverlayVisualSystem.ResolveDensity(
                _profile.Settings.VisualDensity,
                bounds.Width,
                ResponsiveWidgetLayout.For(element.Name).DesignWidth));
        }
    }

    private void UpdateStandings(LiveStandingsWidgetState standings)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        SetText(StandingsSessionText, OverlayText.TranslateExact(_profile.Settings.Language, standings.SessionName));
        SetText(StandingsClockText, FormatSessionTime(standings.SessionRemainingSeconds));
        SetText(
            StandingsLapHeading,
            OverlayText.Get(
                _profile.Settings.Language,
                standings.IsQualifying ? OverlayTextKey.Best : OverlayTextKey.LastLap));
        var structureKey = string.Join(
            "|",
            standings.Classes.Select(category =>
                $"{category.ClassName}:{string.Join(',', category.Rows.Select(row => row.CarNumber))}"));
        if (structureKey == _standingsStructureKey)
        {
            var childIndex = 0;
            foreach (var category in standings.Classes)
            {
                if (StandingsRows.Children[childIndex++] is Border header)
                {
                    header.Background = category.IsPlayerClass
                        ? Brush(palette.Critical)
                        : Brush(palette.Card);
                    if (header.Child is TextBlock label)
                    {
                        label.Text = category.ClassName.ToUpperInvariant();
                    }
                }

                for (var rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
                {
                    if (StandingsRows.Children[childIndex++] is Grid rowGrid)
                    {
                        UpdateStandingsRow(rowGrid, category.Rows[rowIndex], rowIndex);
                    }
                }
            }

            return;
        }

        StandingsRows.Children.Clear();
        _standingsStructureKey = structureKey;

        foreach (var category in standings.Classes)
        {
            StandingsRows.Children.Add(new Border
            {
                Height = 18,
                Background = category.IsPlayerClass
                    ? Brush(palette.Critical)
                    : Brush(palette.Card),
                Child = new TextBlock
                {
                    Text = category.ClassName.ToUpperInvariant(),
                    Foreground = Brush(palette.PrimaryText),
                    FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 9,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(9, 0, 0, 0),
                },
            });

            for (var rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
            {
                StandingsRows.Children.Add(CreateStandingsRow(
                    category.Rows[rowIndex],
                    rowIndex));
            }
        }

        ApplyTextScale(LiveStandingsWidget, _profile.Settings.TimingTextScale);
        ApplySurfaceOpacity(
            LiveStandingsWidget,
            _profile.LiveStandings.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private Grid CreateStandingsRow(
        LiveStandingsRowState row,
        int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var grid = new Grid
        {
            Height = 25,
            Background = row.IsPlayer
                ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Accent, 0.55))
                : rowIndex % 2 == 0
                    ? Brush(palette.Background)
                    : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.55)),
            ToolTip = $"{row.DriverName} · {row.VehicleName}",
        };
        foreach (var width in new[] { 36d, 50d, 40d, 62d, 104d, 76d })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(width),
            });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddStandingsText(
            grid,
            row.ClassPosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            0,
            row.ClassPosition == 1
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText),
            FontWeights.Bold,
            11);
        var manufacturerBadge = new Border
        {
            Background = CarIconBrush(row.VehicleModel),
            Width = 42,
            Height = 19,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ManufacturerAbbreviation(row.VehicleModel),
                Foreground = Brush(palette.PrimaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(manufacturerBadge, 1);
        grid.Children.Add(manufacturerBadge);
        AddStandingsText(
            grid,
            row.CarNumber,
            2,
            Brush(palette.PrimaryText),
            FontWeights.Bold,
            9);
        AddStandingsText(
            grid,
            row.DriverAbbreviation,
            3,
            Brush(palette.PrimaryText),
            FontWeights.Bold,
            10);
        AddStandingsText(
            grid,
            FormatLapTime(row.LastLapTimeSeconds),
            4,
            Brush(palette.PrimaryText),
            FontWeights.SemiBold,
            9);
        AddStandingsText(
            grid,
            FormatStandingsInterval(row),
            5,
            row.IsInPitLane && !row.IsQualifying
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText),
            FontWeights.SemiBold,
            10,
            System.Windows.HorizontalAlignment.Right,
            new Thickness(0, 0, 4, 0));
        AddStandingsText(
            grid,
            FormatTireEnergy(row),
            6,
            row.IsInPitLane
                ? Brush(palette.Attention)
                : TireCompoundBrush(row.TireCompound),
            FontWeights.Bold,
            9,
            System.Windows.HorizontalAlignment.Center);
        return grid;
    }

    private void UpdateStandingsRow(
        Grid grid,
        LiveStandingsRowState row,
        int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        grid.Background = row.IsPlayer
            ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Accent, 0.55))
            : rowIndex % 2 == 0
                ? Brush(palette.Background)
                : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.55));
        grid.ToolTip = $"{row.DriverName} · {row.VehicleName}";
        var texts = grid.Children.OfType<TextBlock>().ToArray();
        if (texts.Length >= 6)
        {
            texts[0].Text = row.ClassPosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            texts[0].Foreground = row.ClassPosition == 1
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
            texts[1].Text = row.CarNumber;
            texts[2].Text = row.DriverAbbreviation;
            texts[3].Text = FormatLapTime(row.LastLapTimeSeconds);
            texts[4].Text = FormatStandingsInterval(row);
            texts[4].Foreground = row.IsInPitLane && !row.IsQualifying
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
            texts[5].Text = FormatTireEnergy(row);
            texts[5].Foreground = row.IsInPitLane
                ? Brush(palette.Attention)
                : TireCompoundBrush(row.TireCompound);
        }

        var badge = grid.Children.OfType<Border>().FirstOrDefault();
        if (badge is not null)
        {
            badge.Background = CarIconBrush(row.VehicleModel);
            if (badge.Child is TextBlock badgeText)
            {
                badgeText.Text = ManufacturerAbbreviation(row.VehicleModel);
            }
        }
    }

    private static void AddStandingsText(
        Grid grid,
        string text,
        int column,
        System.Windows.Media.Brush foreground,
        FontWeight fontWeight,
        double fontSize,
        System.Windows.HorizontalAlignment alignment =
            System.Windows.HorizontalAlignment.Center,
        Thickness? margin = null)
    {
        var element = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = fontWeight,
            FontSize = fontSize,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margin ?? new Thickness(0),
        };
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static string FormatStandingsInterval(LiveStandingsRowState row)
    {
        if (row.IsQualifying)
        {
            return row.ClassPosition == 1
                ? "LEADER"
                : row.IntervalSeconds >= 0 && double.IsFinite(row.IntervalSeconds)
                    ? $"+{row.IntervalSeconds:0.000}"
                    : "--.---";
        }

        if (row.IsInPitLane)
        {
            return "PIT";
        }

        if (row.ClassPosition == 1)
        {
            return "LEADER";
        }

        if (row.IntervalLaps > 0)
        {
            return $"+{row.IntervalLaps} L";
        }

        return row.IntervalSeconds > 0 && double.IsFinite(row.IntervalSeconds)
            ? $"+{row.IntervalSeconds:0.000}"
            : "--.---";
    }

    private static string FormatTireEnergy(LiveStandingsRowState row)
    {
        if (row.IsInPitLane)
        {
            return "PIT";
        }

        var compound = TireCompoundCode(row.TireCompound);
        var energy = row.VirtualEnergyFraction is >= 0 and <= 1 &&
                     double.IsFinite(row.VirtualEnergyFraction)
            ? $"{row.VirtualEnergyFraction:P0}"
            : "--%";
        return $"{compound}  |  {energy}";
    }

    private static string TireCompoundCode(string compound)
    {
        var value = compound.Trim().ToUpperInvariant();
        return value switch
        {
            _ when value.Contains("SOFT", StringComparison.Ordinal) => "S",
            _ when value.Contains("MED", StringComparison.Ordinal) => "M",
            _ when value.Contains("HARD", StringComparison.Ordinal) => "H",
            _ when value.Contains("WET", StringComparison.Ordinal) => "W",
            _ when value.Contains("INTER", StringComparison.Ordinal) => "I",
            { Length: > 0 } => value[..Math.Min(3, value.Length)],
            _ => "--",
        };
    }

    private static System.Windows.Media.Brush TireCompoundBrush(string compound)
    {
        var code = TireCompoundCode(compound);
        return code switch
        {
            "S" => System.Windows.Media.Brushes.Red,
            "M" => System.Windows.Media.Brushes.Gold,
            "H" => System.Windows.Media.Brushes.White,
            "W" => System.Windows.Media.Brushes.DeepSkyBlue,
            "I" => System.Windows.Media.Brushes.LimeGreen,
            _ => System.Windows.Media.Brushes.Gainsboro,
        };
    }

    private static System.Windows.Media.Brush CarIconBrush(string vehicleName)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter
            .ConvertFromString(VehicleCatalog.Resolve(vehicleName).Color)!;
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string ManufacturerAbbreviation(string vehicleName)
    {
        return VehicleCatalog.Resolve(vehicleName).Code;
    }

    private void UpdateRelative(RelativeWidgetState relative)
    {
        var structureKey = string.Join(
            "|",
            relative.Rows.Select(row => $"{row.CarNumber}:{row.DriverName}"));
        if (structureKey == _relativeStructureKey && relative.Rows.Count > 0)
        {
            for (var index = 0; index < relative.Rows.Count; index++)
            {
                if (RelativeRows.Children[index] is Grid grid)
                {
                    UpdateRelativeRow(grid, relative.Rows[index], index);
                }
            }

            return;
        }

        RelativeRows.Children.Clear();
        _relativeStructureKey = structureKey;
        if (relative.Rows.Count == 0)
        {
            var palette = OverlayVisualSystem.Resolve(_profile.Settings);
            RelativeRows.Children.Add(new TextBlock
            {
                Text = "WAITING FOR PLAYER",
                Foreground = Brush(palette.SecondaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Height = 40,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            ApplyTextScale(RelativeWidget, _profile.Settings.TimingTextScale);
            return;
        }

        var rowHeight = Math.Min(37, 386d / relative.Rows.Count);
        for (var index = 0; index < relative.Rows.Count; index++)
        {
            var row = CreateRelativeRow(relative.Rows[index], index);
            row.Height = rowHeight;
            RelativeRows.Children.Add(row);
        }

        ApplyTextScale(RelativeWidget, _profile.Settings.TimingTextScale);
        ApplySurfaceOpacity(
            RelativeWidget,
            _profile.Relative.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private Grid CreateRelativeRow(RelativeRowState row, int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var playerBackground = Brush(palette.PrimaryText);
        var darkText = Brush(palette.Background);
        var foreground = row.IsPlayer
            ? darkText
            : Brush(palette.PrimaryText);
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        var grid = new Grid
        {
            Height = 37,
            Background = row.IsPlayer
                ? playerBackground
                : rowIndex % 2 == 0
                    ? Brush(palette.Background)
                    : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.7)),
            ToolTip =
                $"P{row.OverallPosition} · {row.DriverName} · {row.VehicleClass}",
        };
        foreach (var width in new[] { 52d, 60d })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(width),
            });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(118),
        });

        var numberBadge = new Border
        {
            Width = 44,
            Height = 25,
            CornerRadius = new CornerRadius(3, 0, 0, 3),
            Background = classBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.OverallPosition.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                Foreground = Brush(palette.PrimaryText),
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(numberBadge, 0);
        grid.Children.Add(numberBadge);

        var classBadge = new Border
        {
            Width = 54,
            Height = 25,
            CornerRadius = new CornerRadius(0, 3, 3, 0),
            Background = Brush(palette.PrimaryText),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.ClassAbbreviation,
                Foreground = classBrush,
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(classBadge, 1);
        grid.Children.Add(classBadge);

        var driver = new TextBlock
        {
            Text = row.DriverDisplayName,
            Foreground = foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 3, 0),
        };
        Grid.SetColumn(driver, 2);
        grid.Children.Add(driver);

        var gap = new TextBlock
        {
            Text = FormatRelativeGap(row),
            Foreground = row.IsInPitLane
                ? row.IsPlayer
                    ? darkText
                    : Brush(palette.Attention)
                : foreground,
            FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(gap, 3);
        grid.Children.Add(gap);
        return grid;
    }

    private void UpdateRelativeRow(Grid grid, RelativeRowState row, int rowIndex)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var playerBackground = Brush(palette.PrimaryText);
        var darkText = Brush(palette.Background);
        var foreground = row.IsPlayer ? darkText : Brush(palette.PrimaryText);
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        grid.Background = row.IsPlayer
            ? playerBackground
            : rowIndex % 2 == 0
                ? Brush(palette.Background)
                : Brush(OverlayVisualSystem.Mix(palette.Background, palette.Card, 0.7));
        grid.ToolTip = $"P{row.OverallPosition} · {row.DriverName} · {row.VehicleClass}";
        var badges = grid.Children.OfType<Border>().ToArray();
        if (badges.Length >= 2)
        {
            badges[0].Background = classBrush;
            if (badges[0].Child is TextBlock number)
            {
                number.Text = row.OverallPosition.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            if (badges[1].Child is TextBlock className)
            {
                className.Text = row.ClassAbbreviation;
                className.Foreground = classBrush;
            }
        }

        var texts = grid.Children.OfType<TextBlock>().ToArray();
        if (texts.Length >= 2)
        {
            texts[0].Text = row.DriverDisplayName;
            texts[0].Foreground = foreground;
            texts[1].Text = FormatRelativeGap(row);
            texts[1].Foreground = row.IsInPitLane && !row.IsPlayer
                ? Brush(palette.Attention)
                : foreground;
        }
    }

    private static string FormatRelativeGap(RelativeRowState row)
    {
        if (row.IsInPitLane)
        {
            return "PIT";
        }

        return row.RelativeLaps switch
        {
            > 0 => $"+{row.RelativeLaps}L",
            < 0 => $"{row.RelativeLaps}L",
            _ when row.IsPlayer => "0.0",
            _ when double.IsFinite(row.RelativeGapSeconds) =>
                row.RelativeGapSeconds.ToString("+0.0;-0.0;0.0"),
            _ => "--.-",
        };
    }

    private static System.Windows.Media.Brush RelativeClassBrush(
        string classAbbreviation) =>
        classAbbreviation switch
        {
            "GT3" => Brush(0, 225, 112),
            "HYP" => Brush(244, 32, 55),
            "P2" => Brush(45, 123, 225),
            _ => Brush(125, 141, 160),
        };

    private void UpdateSessionFlags(SessionFlagsWidgetState state)
    {
        string T(OverlayTextKey key) => OverlayText.Get(_profile.Settings.Language, key);
        if (!state.Available)
        {
            SessionNameText.Text = T(OverlayTextKey.Session);
            SessionMetaText.Text = "--:--  ·  LAP --";
            GripValueText.Text = "UNKNOWN";
            GripValueText.Foreground = System.Windows.Media.Brushes.LightGray;
            GripCard.BorderBrush = NeutralCardBrush;
            WeatherIconText.Text = "☁";
            WeatherNameText.Text = T(OverlayTextKey.NoData);
            WeatherDetailText.Text = "RAIN --%  ·  WET --%";
            WeatherCard.BorderBrush = NeutralCardBrush;
            FlagCardText.Text = T(OverlayTextKey.NoData);
            FlagCard.Background = NeutralCardBrush;
            AmbientTemperatureText.Text = "--°C";
            TrackTemperatureText.Text = "--°C";
            WetnessText.Text = "--%";
            return;
        }

        SessionNameText.Text = $"{state.SessionName} · {state.PhaseName}";
        var remaining = state.RemainingSeconds > 0
            ? TimeSpan.FromSeconds(state.RemainingSeconds).ToString(
                state.RemainingSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
            : "--:--";
        var lap = state.MaximumLaps > 0
            ? $"{state.CurrentLap}/{state.MaximumLaps}"
            : state.CurrentLap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SessionMetaText.Text = $"{remaining}  ·  LAP {lap}";

        var gripBrush = state.TrackGripLevel switch
        {
            0 => GripGreenBrush,
            1 => GripLightBrush,
            2 => GripMediumBrush,
            3 => GripHeavyBrush,
            >= 4 => GripSaturatedBrush,
            _ => NeutralCardBrush,
        };
        GripValueText.Text = state.TrackGripName;
        GripValueText.Foreground = gripBrush;
        GripCard.BorderBrush = gripBrush;

        WeatherIconText.Text = state.WeatherCondition switch
        {
            WeatherConditionKind.Clear => "☀",
            WeatherConditionKind.PartlyCloudy => "☀☁",
            WeatherConditionKind.Cloudy => "☁",
            WeatherConditionKind.Overcast => "☁☁",
            WeatherConditionKind.LightRain => "☂",
            WeatherConditionKind.Rain => "☂☂",
            WeatherConditionKind.HeavyRain => "☔",
            _ => "☁",
        };
        WeatherNameText.Text = state.WeatherName;
        WeatherDetailText.Text =
            $"RAIN {state.RainIntensity:P0}  ·  WET {state.AveragePathWetness:P0}";
        WeatherIconText.Foreground = state.WeatherCondition switch
        {
            WeatherConditionKind.Clear => System.Windows.Media.Brushes.Gold,
            WeatherConditionKind.PartlyCloudy =>
                System.Windows.Media.Brushes.LightSkyBlue,
            WeatherConditionKind.Cloudy or WeatherConditionKind.Overcast =>
                System.Windows.Media.Brushes.LightGray,
            WeatherConditionKind.LightRain or
            WeatherConditionKind.Rain or
            WeatherConditionKind.HeavyRain =>
                System.Windows.Media.Brushes.DeepSkyBlue,
            _ => System.Windows.Media.Brushes.LightGray,
        };
        WeatherCard.BorderBrush = WeatherIconText.Foreground;

        FlagCardText.Text = OverlayText.TranslateExact(_profile.Settings.Language, state.FlagName);
        FlagCard.Background = state.FlagName switch
        {
            "GREEN" => FlagGreenBrush,
            "YELLOW" => FlagYellowBrush,
            "RED" => FlagRedBrush,
            _ => NeutralCardBrush,
        };
        AmbientTemperatureText.Text =
            $"{state.AmbientTemperatureCelsius:0}°C";
        TrackTemperatureText.Text =
            $"{state.TrackTemperatureCelsius:0}°C";
        WetnessText.Text = $"{state.AveragePathWetness:P0}";
    }

    private void UpdateFuelStrategy(FuelStrategyWidgetState state)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        if (!state.Available)
        {
            FuelStatusText.Text = "NO DATA";
            FuelCurrentText.Text = "--.- L";
            EnergyCurrentText.Text = "--%";
            FuelTableCurrentText.Text = "--.- L";
            FuelUsageText.Text = "--.-- L";
            FuelRangeText.Text = "--.-";
            FuelRangeTimeText.Text = "-- MIN";
            EnergyTableCurrentText.Text = "--%";
            EnergyUsageText.Text = "--.-%";
            EnergyRangeText.Text = "--.-";
            EnergyRangeTimeText.Text = "-- MIN";
            FinishTargetText.Text = "-- LAPS / -- MIN";
            FuelFinishText.Text = "--.- L / --.- L";
            EnergyFinishText.Text = "--% / --%";
            StrategyPlanText.Text = "STRATEGY LEARNING";
            StrategyPitPlanText.Text = "PIT --";
            StrategyTirePlanText.Text = "TIRES --";
            StrategyAlternativeText.Text = "ALT --";
            FlagScenarioText.Text = "FLAGS · LEARNING";
            WeatherScenarioText.Text = "WEATHER · LEARNING";
            TrafficScenarioText.Text = "TRAFFIC · LEARNING";
            FuelSamplesText.Text = "WAITING FOR TELEMETRY";
            return;
        }

        FuelStatusText.Text = state.Status;
        FuelStatusText.Foreground = state.Status switch
        {
            "SHORT" => Brush(palette.Critical),
            "MARGINAL" => Brush(palette.Attention),
            "GOOD" => Brush(palette.Positive),
            _ => Brush(palette.SecondaryText),
        };
        FuelCurrentText.Text = state.EffectiveFuelCapacityLiters > 0
            ? $"{state.FuelLiters:0.0} / {state.EffectiveFuelCapacityLiters:0.0} L"
            : $"{state.FuelLiters:0.0} L";
        EnergyCurrentText.Text = $"{state.VirtualEnergyFraction:P0}";
        FuelTableCurrentText.Text = $"{state.FuelLiters:0.0} L";
        FuelUsageText.Text = state.Learning
            ? "LEARNING"
            : $"{state.ProjectedConsumptionLitersPerLap:0.00} L";
        FuelRangeText.Text = state.Learning
            ? "--.-"
            : $"{state.EstimatedRangeLaps:0.0}";
        FuelRangeTimeText.Text = state.Learning
            ? "-- MIN"
            : FormatStrategyMinutes(state.EstimatedRangeTimeSeconds);
        EnergyTableCurrentText.Text = $"{state.VirtualEnergyFraction:P0}";
        EnergyUsageText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.AverageVirtualEnergyFractionPerLap:P1}"
            : "LEARNING";
        EnergyRangeText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.EstimatedVirtualEnergyRangeLaps:0.0}"
            : "--.-";
        EnergyRangeTimeText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? FormatStrategyMinutes(state.EstimatedVirtualEnergyRangeTimeSeconds)
            : "-- MIN";
        FinishTargetText.Text =
            $"{state.EstimatedLapsToFinish} LAPS / " +
            FormatStrategyMinutes(state.EstimatedTimeToFinishSeconds);
        FuelFinishText.Text = state.Learning
            ? "--.- L / --.- L"
            : $"{state.RequiredFuelLiters:0.0} L / " +
              $"{state.FuelMarginLiters:+0.0;-0.0;0.0} L";
        EnergyFinishText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"{state.RequiredVirtualEnergyFraction:P0} / " +
              $"{state.VirtualEnergyMarginFraction:+0.0%;-0.0%;0.0%}"
            : "--% / --%";
        FuelFinishText.Foreground = state.FuelMarginLiters < 0
            ? Brush(palette.Critical)
            : Brush(palette.Positive);
        EnergyFinishText.Foreground =
            state.AverageVirtualEnergyFractionPerLap > 0 &&
            state.VirtualEnergyMarginFraction < 0
                ? Brush(palette.Critical)
                : Brush(palette.Information);
        StrategyPlanText.Text = state.PlanSummary;
        StrategyPitPlanText.Text = $"PIT  {state.PitPlan}";
        StrategyTirePlanText.Text = $"TIRES  {state.TirePlan}";
        StrategyAlternativeText.Text = state.EstimatedPitStops > 0
            ? $"FINAL FILL  +{state.FinalFuelToAddLiters:0.0} L · " +
              $"NRG {state.FinalVirtualEnergyTargetFraction:P0} · FINISH {state.FinishProbability:P0}"
            : $"NO FINAL FILL REQUIRED · FINISH {state.FinishProbability:P0}";
        FlagScenarioText.Text = state.FuelSavePlan;
        WeatherScenarioText.Text = state.FuelSaveVirtualEnergyTargetPerLap > 0
            ? $"{state.FuelSavePitPlan} · NRG TARGET {state.FuelSaveVirtualEnergyTargetPerLap:P1}/LAP"
            : state.FuelSavePitPlan;
        TrafficScenarioText.Text = $"TIRES  {state.FuelSaveTirePlan}";
        FuelSamplesText.Text = state.Learning
            ? "COMPLETE A LAP TO CALCULATE"
            : $"PIT L{state.SuggestedPitLap} ({state.LapsUntilPit} LAPS)  " +
              $"· SAVE {state.RequiredFuelSavingFraction:P0}  " +
              $"· ADD {state.FuelToAddLiters:0.0} L  " +
              $"· CONF {state.Confidence} ({state.Samples}/12)";
    }

    private void UpdateRaceControl(RaceControlWidgetState state)
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        RaceAttentionText.Text = !state.Available
            ? "NO DATA"
            : state.RequiresAttention ? "ATTENTION" : "CLEAR";
        RaceAttentionText.Foreground = state.RequiresAttention
            ? Brush(palette.Critical)
            : Brush(palette.Positive);
        RacePenaltyText.Text = state.PenaltyStatus;
        RacePenaltyText.Foreground = state.OutstandingPenalties > 0
            ? Brush(palette.Critical)
            : Brush(palette.PrimaryText);
        RacePitLapText.Text = $"{state.PitStatus} · {state.LapStatus}";
        RaceDamageText.Text = state.DamageStatus == "OK"
            ? "OK"
            : $"{state.DamageStatus} · {state.ImpactStatus}";
        RaceDamageText.Foreground = state.HasCriticalDamage
            ? Brush(palette.Critical)
            : state.RequiresAttention
                ? Brush(palette.Attention)
                : Brush(palette.PrimaryText);
        RaceFlagText.Text = $"FLAG {state.FlagStatus}";
        RaceSystemsText.Text = state.SystemsStatus;
        RaceControlHeader.Background = state.HasCriticalDamage
            ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Critical, 0.55))
            : state.RequiresAttention
                ? Brush(OverlayVisualSystem.Mix(palette.Background, palette.Attention, 0.55))
                : Brush(palette.Card);
    }

    private void UpdatePriorityAlert(
        DashboardWidgetState dashboard,
        SessionFlagsWidgetState session,
        FuelStrategyWidgetState fuel,
        RaceControlWidgetState raceControl)
    {
        if (!_profile.Settings.ShowPriorityAlerts || !dashboard.Available)
        {
            PriorityAlert.Visibility = Visibility.Collapsed;
            return;
        }

        var hottestTire = new[]
        {
            dashboard.TireTemperatures.FrontLeftCelsius,
            dashboard.TireTemperatures.FrontRightCelsius,
            dashboard.TireTemperatures.RearLeftCelsius,
            dashboard.TireTemperatures.RearRightCelsius,
        }.Max();
        var maximumWear = new[]
        {
            dashboard.TireWear.FrontLeftFraction,
            dashboard.TireWear.FrontRightFraction,
            dashboard.TireWear.RearLeftFraction,
            dashboard.TireWear.RearRightFraction,
        }.Max();

        string T(OverlayTextKey key) => OverlayText.Get(_profile.Settings.Language, key);
        (OverlayAlertSeverity Severity, string Icon, string Text, string Detail)? alert =
            raceControl.HasCriticalDamage
                ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.CriticalDamage), raceControl.DamageStatus)
                : raceControl.OutstandingPenalties > 0
                    ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.Penalty), raceControl.PenaltyStatus)
                    : session.FlagName == "RED"
                        ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.RedFlag), T(OverlayTextKey.SessionStopped))
                        : fuel.Available && !fuel.Learning && fuel.Status == "SHORT"
                            ? (OverlayAlertSeverity.Critical, "!", T(OverlayTextKey.EnergyShortfall), fuel.PlanSummary)
                            : TireTemperatureClassifier.Classify(hottestTire) == TireTemperatureBand.Critical
                                ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.TireTemperature), $"{T(OverlayTextKey.Hottest)} {hottestTire:0}°C")
                                : maximumWear >= _profile.Settings.TireWearLimitPercent / 100
                                    ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.TireWear), $"{T(OverlayTextKey.Maximum)} {maximumWear:P0}")
                                    : session.FlagName == "YELLOW"
                                        ? (OverlayAlertSeverity.Attention, "▲", T(OverlayTextKey.YellowFlag), T(OverlayTextKey.NoSafetyCarAssumption))
                                        : session.RainIntensity >= 0.02
                                            ? (OverlayAlertSeverity.Attention, "☂", session.WeatherName, $"RAIN {session.RainIntensity:P0}")
                                            : dashboard.SpeedLimiterActive
                                                ? (OverlayAlertSeverity.Information, "P", T(OverlayTextKey.PitLimiter), T(OverlayTextKey.Active))
                                                : null;

        if (alert is null)
        {
            PriorityAlert.Visibility = Visibility.Collapsed;
            return;
        }

        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var color = alert.Value.Severity switch
        {
            OverlayAlertSeverity.Critical => palette.Critical,
            OverlayAlertSeverity.Attention => palette.Attention,
            _ => palette.Information,
        };
        PriorityAlertIcon.Text = alert.Value.Icon;
        PriorityAlertText.Text = alert.Value.Text;
        PriorityAlertDetail.Text = alert.Value.Detail;
        PriorityAlert.BorderBrush = new System.Windows.Media.SolidColorBrush(color);
        PriorityAlert.Background = new System.Windows.Media.SolidColorBrush(
            OverlayVisualSystem.WithOpacity(palette.Background, 0.96));
        PriorityAlert.Visibility = Visibility.Visible;
    }

    private void ApplyDashboardDensity(OverlayDensity density)
    {
        if (_dashboardDensity == density && IsLoaded)
        {
            return;
        }

        _dashboardDensity = density;
        var compact = density == OverlayDensity.Compact;
        EnvironmentText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        BrakeBiasText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SessionModeText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SessionTimeText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        EngineTempsText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SectorPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SectorColumn.Width = compact ? new GridLength(0) : new GridLength(214);
        TireColumn.Width = compact ? new GridLength(290) : new GridLength(270);
        TelemetryColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private void ApplySurfaceOpacity(DependencyObject root, double opacity)
    {
        if (root is System.Windows.Controls.Panel panel)
        {
            panel.Background = SurfaceBrush(panel.Background, opacity);
        }
        else if (root is Border border)
        {
            border.Background = SurfaceBrush(border.Background, opacity);
        }

        var children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < children; index++)
        {
            ApplySurfaceOpacity(
                System.Windows.Media.VisualTreeHelper.GetChild(root, index),
                opacity);
        }
    }

    private static System.Windows.Media.Brush? SurfaceBrush(
        System.Windows.Media.Brush? brush,
        double opacity)
    {
        if (brush is not System.Windows.Media.SolidColorBrush solid ||
            solid.Color.A == 0)
        {
            return brush;
        }

        var color = solid.Color;
        if (Math.Max(color.R, Math.Max(color.G, color.B)) >= 140)
        {
            return brush;
        }

        return Brush(OverlayVisualSystem.WithOpacity(
            System.Windows.Media.Color.FromArgb(255, color.R, color.G, color.B),
            opacity));
    }

    private void ApplyTheme()
    {
        var palette = OverlayVisualSystem.Resolve(_profile.Settings);
        var accent = new System.Windows.Media.SolidColorBrush(palette.Accent);
        var background = new System.Windows.Media.SolidColorBrush(palette.Background);
        DashboardBrandText.Text = _profile.Settings.DashboardTitle;
        DashboardRoot.Background = background;
        ApplyTextScale(DiagnosticWidget, _profile.Settings.DashboardTextScale);
        ApplyTextScale(InputsWidget, _profile.Settings.InputsTextScale);
        ApplyTextScale(LiveStandingsWidget, _profile.Settings.TimingTextScale);
        ApplyTextScale(RelativeWidget, _profile.Settings.TimingTextScale);

        foreach (var widget in AllWidgets())
        {
            ApplyPalette(widget, palette);
            widget.BorderBrush = IsEditMode
                ? System.Windows.Media.Brushes.Orange
                : accent;
            if (widget == LiveStandingsWidget || widget == RelativeWidget)
            {
                widget.Background = System.Windows.Media.Brushes.Transparent;
            }
            else if (widget != DiagnosticWidget)
            {
                widget.Background = background;
            }

            ApplySurfaceOpacity(
                widget,
                _profile.Settings.Theme == "HighContrast"
                    ? 1
                    : PlacementFor(widget).Opacity * _profile.Settings.BackgroundOpacity);
        }

        SectorPanel.Visibility = _profile.Settings.DashboardShowSectors
            ? Visibility.Visible
            : Visibility.Collapsed;
        TirePanel.Visibility = _profile.Settings.DashboardShowTires
            ? Visibility.Visible
            : Visibility.Collapsed;
        TelemetryPanel.Visibility = _profile.Settings.DashboardShowTelemetry
            ? Visibility.Visible
            : Visibility.Collapsed;
        var moduleOrder = DashboardModuleLayout.Parse(_profile.Settings.DashboardModuleOrder);
        for (var index = 0; index < moduleOrder.Count; index++)
        {
            Grid.SetColumn(moduleOrder[index] switch
            {
                DashboardModule.Sectors => SectorPanel,
                DashboardModule.Tires => TirePanel,
                _ => TelemetryPanel,
            }, index);
        }
        SectorColumn.Width = _profile.Settings.DashboardShowSectors
            ? new GridLength(214)
            : new GridLength(0);
        TireColumn.Width = _profile.Settings.DashboardShowTires
            ? new GridLength(270)
            : new GridLength(0);
        TelemetryColumn.Width = _profile.Settings.DashboardShowTelemetry
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
    }

    private static void ApplyPalette(
        DependencyObject root,
        OverlayThemePalette palette)
    {
        var baseline = ThemeBrushBaselines.GetValue(root, CaptureThemeBrushes);
        switch (root)
        {
            case TextBlock text when baseline.Foreground is { } foreground:
                text.Foreground = ThemeBrush(foreground, palette, ThemeBrushRole.Text);
                break;
            case Border border:
                if (baseline.Background is { } borderBackground)
                {
                    border.Background = ThemeBrush(
                        borderBackground,
                        palette,
                        ThemeBrushRole.Background);
                }
                if (baseline.Border is { } borderColor)
                {
                    border.BorderBrush = ThemeBrush(
                        borderColor,
                        palette,
                        ThemeBrushRole.Border);
                }
                break;
            case System.Windows.Controls.Panel panel when baseline.Background is { } panelBackground:
                panel.Background = ThemeBrush(
                    panelBackground,
                    palette,
                    ThemeBrushRole.Background);
                break;
        }

        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            ApplyPalette(
                System.Windows.Media.VisualTreeHelper.GetChild(root, index),
                palette);
        }
    }

    private static ThemeBrushBaseline CaptureThemeBrushes(DependencyObject item) => item switch
    {
        TextBlock text => new(BrushColor(text.Foreground), null, null),
        Border border => new(
            null,
            BrushColor(border.Background),
            BrushColor(border.BorderBrush)),
        System.Windows.Controls.Panel panel => new(null, BrushColor(panel.Background), null),
        _ => new(null, null, null),
    };

    private static System.Windows.Media.Color? BrushColor(
        System.Windows.Media.Brush? brush) =>
        brush is System.Windows.Media.SolidColorBrush solid
            ? solid.Color
            : null;

    private static System.Windows.Media.Brush ThemeBrush(
        System.Windows.Media.Color baseline,
        OverlayThemePalette palette,
        ThemeBrushRole role)
    {
        var color = ResolveThemeColor(baseline, palette, role);
        return new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(
                baseline.A,
                color.R,
                color.G,
                color.B));
    }

    private static System.Windows.Media.Color ResolveThemeColor(
        System.Windows.Media.Color color,
        OverlayThemePalette palette,
        ThemeBrushRole role)
    {
        var maximum = Math.Max(color.R, Math.Max(color.G, color.B));
        var minimum = Math.Min(color.R, Math.Min(color.G, color.B));
        if (color.R >= 175 && color.G < 120 && color.B < 130)
        {
            return palette.Critical;
        }
        if (color.R >= 175 && color.G >= 100 && color.G < 225 && color.B < 130)
        {
            return palette.Attention;
        }
        if (color.G >= 125 && color.B >= 125 && color.R < 110)
        {
            return palette.Information;
        }
        if (color.G >= 120 && color.G > color.R * 1.2 && color.G > color.B * 1.05)
        {
            return role == ThemeBrushRole.Border ? palette.Accent : palette.Positive;
        }
        if (role == ThemeBrushRole.Background && maximum < 75)
        {
            return maximum < 18 ? palette.Background : palette.Card;
        }
        if (role == ThemeBrushRole.Border)
        {
            return maximum - minimum < 70 ? palette.SecondaryText : palette.Accent;
        }
        if (maximum >= 215 && maximum - minimum < 45)
        {
            return palette.PrimaryText;
        }
        if (role == ThemeBrushRole.Text && maximum - minimum < 90)
        {
            return maximum < 45 ? palette.Background : palette.SecondaryText;
        }
        return color;
    }

    private static void ApplyTextScale(DependencyObject root, double scale)
    {
        if (root is TextBlock text)
        {
            var baseline = TextScaleBaselines.GetValue(
                text,
                item => new TextScaleBaseline(item.FontSize));
            text.FontSize = baseline.FontSize * Math.Clamp(scale, 0.8, 1.25);
        }

        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            ApplyTextScale(
                System.Windows.Media.VisualTreeHelper.GetChild(root, index),
                scale);
        }
    }

    private sealed record TextScaleBaseline(double FontSize);
    private sealed record ThemeBrushBaseline(
        System.Windows.Media.Color? Foreground,
        System.Windows.Media.Color? Background,
        System.Windows.Media.Color? Border);

    private enum ThemeBrushRole
    {
        Text,
        Background,
        Border,
    }

    private WidgetPlacement PlacementFor(FrameworkElement widget) => widget.Name switch
    {
        "DiagnosticWidget" => _profile.Diagnostic,
        "InputsWidget" => _profile.Inputs,
        "LiveStandingsWidget" => _profile.LiveStandings,
        "RelativeWidget" => _profile.Relative,
        "SessionFlagsWidget" => _profile.SessionFlags,
        "FuelStrategyWidget" => _profile.FuelStrategy,
        "RaceControlWidget" => _profile.RaceControl,
        _ => _profile.Diagnostic,
    };

    private Border[] AllWidgets() =>
    [
        DiagnosticWidget,
        InputsWidget,
        LiveStandingsWidget,
        RelativeWidget,
        SessionFlagsWidget,
        FuelStrategyWidget,
        RaceControlWidget,
    ];

    private double LayoutWidth => double.IsNaN(OverlayCanvas.Width)
        ? ActualWidth
        : OverlayCanvas.Width;

    private double LayoutHeight => double.IsNaN(OverlayCanvas.Height)
        ? ActualHeight
        : OverlayCanvas.Height;

    private static string FormatStrategyMinutes(double seconds) =>
        seconds > 0 && double.IsFinite(seconds)
            ? $"{Math.Ceiling(seconds / 60):0} MIN"
            : "-- MIN";

    private void WidgetMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditMode ||
            e.OriginalSource is DependencyObject source &&
            FindVisualAncestor<System.Windows.Controls.Primitives.Thumb>(source) is not null)
        {
            return;
        }

        if (sender is not FrameworkElement widget)
        {
            return;
        }

        _dragging = true;
        _activeWidget = widget;
        _dragStart = e.GetPosition(OverlayCanvas);
        _dragLeft = Canvas.GetLeft(widget);
        _dragTop = Canvas.GetTop(widget);
        widget.CaptureMouse();
        e.Handled = true;
    }

    private void WidgetMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || !IsEditMode || _activeWidget is null)
        {
            return;
        }

        var position = e.GetPosition(OverlayCanvas);
        var left = _dragLeft + position.X - _dragStart.X;
        var top = _dragTop + position.Y - _dragStart.Y;
        Canvas.SetLeft(_activeWidget, SnapToNearbyX(SnapToGrid(Snap(
            Math.Clamp(left, 0, Math.Max(0, ActualWidth - _activeWidget.ActualWidth)),
            0,
            Math.Max(0, ActualWidth - _activeWidget.ActualWidth)))));
        Canvas.SetTop(_activeWidget, SnapToNearbyY(SnapToGrid(Snap(
            Math.Clamp(top, 0, Math.Max(0, ActualHeight - _activeWidget.ActualHeight)),
            0,
            Math.Max(0, ActualHeight - _activeWidget.ActualHeight)))));
    }

    private void WidgetMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _activeWidget?.ReleaseMouseCapture();
        _activeWidget = null;
        SaveProfile();
        e.Handled = true;
    }

    private void ResizeThumbDragDelta(
        object sender,
        System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!IsEditMode ||
            sender is not System.Windows.Controls.Primitives.Thumb thumb ||
            thumb.Tag is not string widgetName ||
            FindName(widgetName) is not FrameworkElement widget)
        {
            return;
        }

        var spec = ResponsiveWidgetLayout.For(widget.Name);
        var displayScale = Math.Clamp(
            Math.Min(ActualWidth / 1920, ActualHeight / 1080),
            0.55,
            1.5);
        var minimumWidth = Math.Min(ActualWidth, spec.MinimumWidth * displayScale);
        var minimumHeight = Math.Min(ActualHeight, spec.MinimumHeight * displayScale);
        var maximumWidth = Math.Max(minimumWidth, ActualWidth - Canvas.GetLeft(widget));
        var maximumHeight = Math.Max(minimumHeight, ActualHeight - Canvas.GetTop(widget));
        var aspectRatio = spec.AspectRatio;

        var widthFromHorizontal = widget.ActualWidth + e.HorizontalChange;
        var widthFromVertical = (widget.ActualHeight + e.VerticalChange) * aspectRatio;
        var targetWidth = Math.Abs(e.HorizontalChange) >= Math.Abs(e.VerticalChange)
            ? widthFromHorizontal
            : widthFromVertical;
        targetWidth = Math.Clamp(
            targetWidth,
            Math.Max(minimumWidth, minimumHeight * aspectRatio),
            Math.Min(maximumWidth, maximumHeight * aspectRatio));
        widget.Width = targetWidth;
        widget.Height = targetWidth / aspectRatio;
    }

    private void ResizeThumbDragCompleted(
        object sender,
        System.Windows.Controls.Primitives.DragCompletedEventArgs e) => SaveProfile();

    private void SaveProfile()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _profile = _profile with
        {
            Diagnostic = CapturePlacement(DiagnosticWidget, _profile.Diagnostic),
            Inputs = CapturePlacement(InputsWidget, _profile.Inputs),
            LiveStandings = CapturePlacement(
                LiveStandingsWidget,
                _profile.LiveStandings),
            Relative = CapturePlacement(RelativeWidget, _profile.Relative),
            SessionFlags = CapturePlacement(
                SessionFlagsWidget,
                _profile.SessionFlags),
            FuelStrategy = CapturePlacement(
                FuelStrategyWidget,
                _profile.FuelStrategy),
            RaceControl = CapturePlacement(
                RaceControlWidget,
                _profile.RaceControl),
        };
        _layoutStore.Save(_profile);
    }

    private WidgetPlacement CapturePlacement(
        FrameworkElement element,
        WidgetPlacement current) => current with
    {
        X = Canvas.GetLeft(element) / ActualWidth,
        Y = Canvas.GetTop(element) / ActualHeight,
        Width = element.ActualWidth / ActualWidth / Math.Max(0.5, current.Scale),
        Height = element.ActualHeight / ActualHeight / Math.Max(0.5, current.Scale),
    };

    private double SnapToGrid(double value)
    {
        var grid = _profile.Settings.GridSnapPixels;
        return grid <= 0 ? value : Math.Round(value / grid) * grid;
    }

    private double SnapToNearbyX(double value)
    {
        if (_activeWidget is null)
        {
            return value;
        }

        var width = _activeWidget.ActualWidth;
        foreach (var other in AllWidgets().Where(
            item => item != _activeWidget && item.Visibility == Visibility.Visible))
        {
            var otherLeft = Canvas.GetLeft(other);
            var otherRight = otherLeft + other.ActualWidth;
            foreach (var candidate in new[]
            {
                otherLeft,
                otherRight,
                otherLeft - width,
                otherRight - width,
            })
            {
                if (Math.Abs(value - candidate) <= SnapDistance)
                {
                    return Math.Clamp(candidate, 0, Math.Max(0, ActualWidth - width));
                }
            }
        }

        return value;
    }

    private double SnapToNearbyY(double value)
    {
        if (_activeWidget is null)
        {
            return value;
        }

        var height = _activeWidget.ActualHeight;
        foreach (var other in AllWidgets().Where(
            item => item != _activeWidget && item.Visibility == Visibility.Visible))
        {
            var otherTop = Canvas.GetTop(other);
            var otherBottom = otherTop + other.ActualHeight;
            foreach (var candidate in new[]
            {
                otherTop,
                otherBottom,
                otherTop - height,
                otherBottom - height,
            })
            {
                if (Math.Abs(value - candidate) <= SnapDistance)
                {
                    return Math.Clamp(candidate, 0, Math.Max(0, ActualHeight - height));
                }
            }
        }

        return value;
    }

    private static double Snap(double value, double start, double end)
    {
        if (Math.Abs(value - start) <= SnapDistance)
        {
            return start;
        }

        return Math.Abs(value - end) <= SnapDistance ? end : value;
    }

    private static System.Windows.Media.Brush Brush(byte red, byte green, byte blue)
        => Brush(System.Windows.Media.Color.FromRgb(red, green, blue));

    private static System.Windows.Media.Brush Brush(System.Windows.Media.Color color)
    {
        var key = (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        if (BrushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var brush = new System.Windows.Media.SolidColorBrush(
            color);
        brush.Freeze();
        BrushCache[key] = brush;
        return brush;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ApplyInteractionStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        var style = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        style |= ToolWindowStyle | NoActivateStyle;
        if (IsEditMode)
        {
            style &= ~TransparentStyle;
        }
        else
        {
            style |= TransparentStyle;
        }

        _ = SetWindowLongPtr(handle, ExtendedStyleIndex, new nint(style));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newLong);
}
