using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly FuelStrategyTracker _fuelStrategyTracker = new();
    private readonly SectorReferenceTracker _sectorReferenceTracker = new();
    private readonly Queue<(double TimeSeconds, double Throttle, double Brake)> _pedalHistory = new();
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
    private bool _nativeTimingActive;

    public OverlayWindow(LayoutStore layoutStore)
    {
        _layoutStore = layoutStore;
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

    public bool NativeLiveStandingsShouldBeVisible =>
        _profile.LiveStandings.Visible && !IsEditMode;
    public bool NativeRelativeShouldBeVisible =>
        _profile.Relative.Visible && !IsEditMode;
    public double NativeLiveStandingsOpacity =>
        _profile.LiveStandings.Opacity * _profile.Settings.BackgroundOpacity;
    public double NativeRelativeOpacity =>
        _profile.Relative.Opacity * _profile.Settings.BackgroundOpacity;

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

    public void UpdateRuntimeHealth(TelemetryRuntimeHealth health) =>
        _runtimeHealth = health;

    public void ExportDiagnostics(string destinationPath) =>
        DiagnosticsReportWriter.Write(
            destinationPath,
            _lastSnapshot,
            _runtimeHealth,
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

        var connected = snapshot.State == LmuConnectionState.Connected;
        var dashboard = EssentialWidgetStateFactory.CreateDashboard(snapshot);
        if (!_nativeDashboardActive || IsEditMode)
        {
        SetText(ConnectionText, connected ? "CONECTADO" : snapshot.State.ToString().ToUpperInvariant());
        var inputs = EssentialWidgetStateFactory.CreateInputs(snapshot);
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
        var trackedSectors = _sectorReferenceTracker.Update(
            snapshot,
            dashboard.SectorTimes);
        UpdateSectorReadings(trackedSectors);
        SetText(OptimalLapText, $"OPTIMAL {FormatLapTime(officialOptimalLapSeconds)}");
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
        UpdateTireReading(
            0,
            FrontLeftTireIcon,
            FrontLeftTireText,
            tire.FrontLeftCelsius,
            dashboard.TireWear.FrontLeftFraction,
            dashboard.Available);
        UpdateTireReading(
            1,
            FrontRightTireIcon,
            FrontRightTireText,
            tire.FrontRightCelsius,
            dashboard.TireWear.FrontRightFraction,
            dashboard.Available);
        UpdateTireReading(
            2,
            RearLeftTireIcon,
            RearLeftTireText,
            tire.RearLeftCelsius,
            dashboard.TireWear.RearLeftFraction,
            dashboard.Available);
        UpdateTireReading(
            3,
            RearRightTireIcon,
            RearRightTireText,
            tire.RearRightCelsius,
            dashboard.TireWear.RearRightFraction,
            dashboard.Available);
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
                UpdateStandings(EssentialWidgetStateFactory.CreateLiveStandings(snapshot));
                UpdateRelative(EssentialWidgetStateFactory.CreateRelative(snapshot));
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
                    _profile.Settings.EstimatedTireChangeSeconds));
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
        bool available)
    {
        SetText(text, available
            ? $"{temperatureCelsius:0}° · {wearFraction:P0}"
            : "--° · --%");
        _tireBands[tireIndex] = available
            ? TireTemperatureClassifier.ClassifyStable(
                temperatureCelsius,
                _tireBands[tireIndex])
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
            sectors.LastSector1Seconds,
            sectors.BestSector1Seconds);
        UpdateSectorReading(
            Sector2Text,
            Sector2DeltaText,
            sectors.CurrentSector2Seconds,
            sectors.LastSector2Seconds,
            sectors.BestSector2Seconds);
        UpdateSectorReading(
            Sector3Text,
            Sector3DeltaText,
            sectors.CurrentSector3Seconds,
            sectors.LastSector3Seconds,
            sectors.BestSector3Seconds);
    }

    private static void UpdateSectorReading(
        TextBlock timeText,
        TextBlock deltaText,
        double currentSeconds,
        double lastSeconds,
        double bestSeconds)
    {
        var value = currentSeconds > 0 ? currentSeconds : lastSeconds;
        SetText(timeText, value > 0 ? $"{value:0.000}" : "--.---");
        if (value <= 0 || bestSeconds <= 0)
        {
            SetText(deltaText, "--.---");
            deltaText.Foreground = System.Windows.Media.Brushes.LightGray;
            return;
        }

        var delta = value - bestSeconds;
        SetText(deltaText, delta.ToString("+0.000;-0.000;0.000"));
        deltaText.Foreground = delta <= 0
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;
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
            dashboard.Brake));
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
        Array.Fill(_pedalGraphPixels, unchecked((int)0xFF080B0D));
        DrawGraphGridRow(25);
        DrawGraphGridRow(51);
        DrawGraphGridRow(76);

        if (_pedalHistory.Count > 0 &&
            double.IsFinite(_lastPedalSampleTimeSeconds))
        {
            DrawPedalTrace(
                throttle: true,
                unchecked((int)0xFF063B17),
                unchecked((int)0xFF00F23D));
            DrawPedalTrace(
                throttle: false,
                unchecked((int)0xFF3B1116),
                unchecked((int)0xFFFF2738));
        }

        _pedalGraphBitmap.WritePixels(
            new Int32Rect(0, 0, PedalGraphWidth, PedalGraphHeight),
            _pedalGraphPixels,
            PedalGraphWidth * sizeof(int),
            0);
    }

    private void DrawGraphGridRow(int y)
    {
        for (var x = 0; x < PedalGraphWidth; x++)
        {
            _pedalGraphPixels[(y * PedalGraphWidth) + x] =
                unchecked((int)0xFF30373E);
        }
    }

    private void DrawPedalTrace(bool throttle, int fillColor, int lineColor)
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
                DrawGraphLine(previousX, previousY, x, y, lineColor);
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
    }

    private void ApplyPlacement(
        FrameworkElement element,
        WidgetPlacement placement)
    {
        var renderedNatively = !IsEditMode &&
            ((element == DiagnosticWidget && _nativeDashboardActive) ||
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
        SetText(StandingsSessionText, standings.SessionName);
        SetText(StandingsClockText, FormatSessionTime(standings.SessionRemainingSeconds));
        SetText(StandingsLapHeading, standings.IsQualifying ? "BEST LAP" : "LAST LAP");
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
                        ? Brush(133, 24, 34)
                        : Brush(24, 37, 69);
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
                    ? Brush(133, 24, 34)
                    : Brush(24, 37, 69),
                Child = new TextBlock
                {
                    Text = category.ClassName.ToUpperInvariant(),
                    Foreground = System.Windows.Media.Brushes.White,
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

        ApplySurfaceOpacity(
            LiveStandingsWidget,
            _profile.LiveStandings.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private static Grid CreateStandingsRow(
        LiveStandingsRowState row,
        int rowIndex)
    {
        var grid = new Grid
        {
            Height = 25,
            Background = row.IsPlayer
                ? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(14, 92, 116))
                : rowIndex % 2 == 0
                    ? Brush(13, 19, 38)
                    : Brush(19, 27, 49),
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
                ? System.Windows.Media.Brushes.Gold
                : System.Windows.Media.Brushes.White,
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
                Foreground = System.Windows.Media.Brushes.White,
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
            System.Windows.Media.Brushes.White,
            FontWeights.Bold,
            9);
        AddStandingsText(
            grid,
            row.DriverAbbreviation,
            3,
            System.Windows.Media.Brushes.White,
            FontWeights.Bold,
            10);
        AddStandingsText(
            grid,
            FormatLapTime(row.LastLapTimeSeconds),
            4,
            System.Windows.Media.Brushes.Gainsboro,
            FontWeights.SemiBold,
            9);
        AddStandingsText(
            grid,
            FormatStandingsInterval(row),
            5,
            row.IsInPitLane && !row.IsQualifying
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.Gainsboro,
            FontWeights.SemiBold,
            10,
            System.Windows.HorizontalAlignment.Right,
            new Thickness(0, 0, 4, 0));
        AddStandingsText(
            grid,
            FormatTireEnergy(row),
            6,
            row.IsInPitLane
                ? System.Windows.Media.Brushes.Orange
                : TireCompoundBrush(row.TireCompound),
            FontWeights.Bold,
            9,
            System.Windows.HorizontalAlignment.Center);
        return grid;
    }

    private static void UpdateStandingsRow(
        Grid grid,
        LiveStandingsRowState row,
        int rowIndex)
    {
        grid.Background = row.IsPlayer
            ? Brush(14, 92, 116)
            : rowIndex % 2 == 0
                ? Brush(13, 19, 38)
                : Brush(19, 27, 49);
        grid.ToolTip = $"{row.DriverName} · {row.VehicleName}";
        var texts = grid.Children.OfType<TextBlock>().ToArray();
        if (texts.Length >= 6)
        {
            texts[0].Text = row.ClassPosition.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            texts[0].Foreground = row.ClassPosition == 1
                ? System.Windows.Media.Brushes.Gold
                : System.Windows.Media.Brushes.White;
            texts[1].Text = row.CarNumber;
            texts[2].Text = row.DriverAbbreviation;
            texts[3].Text = FormatLapTime(row.LastLapTimeSeconds);
            texts[4].Text = FormatStandingsInterval(row);
            texts[4].Foreground = row.IsInPitLane && !row.IsQualifying
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.Gainsboro;
            texts[5].Text = FormatTireEnergy(row);
            texts[5].Foreground = row.IsInPitLane
                ? System.Windows.Media.Brushes.Orange
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
        var name = vehicleName.ToUpperInvariant();
        return name switch
        {
            _ when name.Contains("FERRARI", StringComparison.Ordinal) =>
                Brush(238, 37, 48),
            _ when name.Contains("PORSCHE", StringComparison.Ordinal) =>
                Brush(95, 108, 118),
            _ when name.Contains("BMW", StringComparison.Ordinal) =>
                Brush(45, 123, 225),
            _ when name.Contains("CADILLAC", StringComparison.Ordinal) =>
                Brush(239, 188, 46),
            _ when name.Contains("ALPINE", StringComparison.Ordinal) =>
                Brush(44, 122, 230),
            _ when name.Contains("TOYOTA", StringComparison.Ordinal) =>
                Brush(220, 35, 45),
            _ when name.Contains("ASTON", StringComparison.Ordinal) =>
                Brush(25, 168, 119),
            _ when name.Contains("CORVETTE", StringComparison.Ordinal) =>
                Brush(255, 211, 42),
            _ when name.Contains("MCLAREN", StringComparison.Ordinal) =>
                Brush(255, 125, 20),
            _ when name.Contains("FORD", StringComparison.Ordinal) =>
                Brush(35, 105, 190),
            _ when name.Contains("LEXUS", StringComparison.Ordinal) =>
                Brush(78, 85, 92),
            _ when name.Contains("LAMBORGHINI", StringComparison.Ordinal) =>
                Brush(174, 145, 25),
            _ when name.Contains("PEUGEOT", StringComparison.Ordinal) =>
                Brush(54, 68, 82),
            _ => Brush(135, 151, 168),
        };
    }

    private static string ManufacturerAbbreviation(string vehicleName)
    {
        var name = vehicleName.ToUpperInvariant();
        return name switch
        {
            _ when name.Contains("PORSCHE", StringComparison.Ordinal) => "POR",
            _ when name.Contains("FERRARI", StringComparison.Ordinal) => "FER",
            _ when name.Contains("BMW", StringComparison.Ordinal) => "BMW",
            _ when name.Contains("CADILLAC", StringComparison.Ordinal) => "CAD",
            _ when name.Contains("ALPINE", StringComparison.Ordinal) => "ALP",
            _ when name.Contains("TOYOTA", StringComparison.Ordinal) => "TOY",
            _ when name.Contains("ASTON", StringComparison.Ordinal) => "AST",
            _ when name.Contains("CORVETTE", StringComparison.Ordinal) => "COR",
            _ when name.Contains("MCLAREN", StringComparison.Ordinal) => "MCL",
            _ when name.Contains("FORD", StringComparison.Ordinal) => "FOR",
            _ when name.Contains("LEXUS", StringComparison.Ordinal) => "LEX",
            _ when name.Contains("LAMBORGHINI", StringComparison.Ordinal) => "LAM",
            _ when name.Contains("PEUGEOT", StringComparison.Ordinal) => "PEU",
            _ => "---",
        };
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
            RelativeRows.Children.Add(new TextBlock
            {
                Text = "WAITING FOR PLAYER",
                Foreground = System.Windows.Media.Brushes.LightGray,
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Height = 40,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }

        for (var index = 0; index < relative.Rows.Count; index++)
        {
            RelativeRows.Children.Add(CreateRelativeRow(relative.Rows[index], index));
        }

        ApplySurfaceOpacity(
            RelativeWidget,
            _profile.Relative.Opacity * _profile.Settings.BackgroundOpacity);
    }

    private static Grid CreateRelativeRow(RelativeRowState row, int rowIndex)
    {
        var playerBackground = Brush(216, 221, 232);
        var darkText = Brush(24, 31, 44);
        var foreground = row.IsPlayer
            ? darkText
            : System.Windows.Media.Brushes.White;
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        var grid = new Grid
        {
            Height = 37,
            Background = row.IsPlayer
                ? playerBackground
                : rowIndex % 2 == 0
                    ? Brush(17, 25, 43)
                    : Brush(24, 33, 54),
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
                Foreground = System.Windows.Media.Brushes.White,
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
            Background = System.Windows.Media.Brushes.White,
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
                    : System.Windows.Media.Brushes.Orange
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

    private static void UpdateRelativeRow(Grid grid, RelativeRowState row, int rowIndex)
    {
        var playerBackground = Brush(216, 221, 232);
        var darkText = Brush(24, 31, 44);
        var foreground = row.IsPlayer ? darkText : System.Windows.Media.Brushes.White;
        var classBrush = RelativeClassBrush(row.ClassAbbreviation);
        grid.Background = row.IsPlayer
            ? playerBackground
            : rowIndex % 2 == 0
                ? Brush(17, 25, 43)
                : Brush(24, 33, 54);
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
                ? System.Windows.Media.Brushes.Orange
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
        if (!state.Available)
        {
            SessionNameText.Text = "SESSION";
            SessionMetaText.Text = "--:--  ·  LAP --";
            GripValueText.Text = "UNKNOWN";
            GripValueText.Foreground = System.Windows.Media.Brushes.LightGray;
            GripCard.BorderBrush = NeutralCardBrush;
            WeatherIconText.Text = "☁";
            WeatherNameText.Text = "NO DATA";
            WeatherDetailText.Text = "RAIN --%  ·  WET --%";
            WeatherCard.BorderBrush = NeutralCardBrush;
            FlagCardText.Text = "NO DATA";
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

        FlagCardText.Text = state.FlagName;
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
            "SHORT" => System.Windows.Media.Brushes.OrangeRed,
            "MARGINAL" => System.Windows.Media.Brushes.Gold,
            "GOOD" => System.Windows.Media.Brushes.LimeGreen,
            _ => System.Windows.Media.Brushes.LightGray,
        };
        FuelCurrentText.Text = $"{state.FuelLiters:0.0} L";
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
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.LimeGreen;
        EnergyFinishText.Foreground =
            state.AverageVirtualEnergyFractionPerLap > 0 &&
            state.VirtualEnergyMarginFraction < 0
                ? System.Windows.Media.Brushes.OrangeRed
                : System.Windows.Media.Brushes.LightSkyBlue;
        StrategyPlanText.Text = state.PlanSummary;
        StrategyPitPlanText.Text = $"PIT  {state.PitPlan}";
        StrategyTirePlanText.Text = $"TIRES  {state.TirePlan}";
        StrategyAlternativeText.Text = state.AlternativePlan;
        FlagScenarioText.Text = state.FlagScenario;
        WeatherScenarioText.Text = state.WeatherScenario;
        TrafficScenarioText.Text = state.TrafficScenario;
        FuelSamplesText.Text = state.Learning
            ? "COMPLETE A LAP TO CALCULATE"
            : $"PIT L{state.SuggestedPitLap} ({state.LapsUntilPit} LAPS)  " +
              $"· SAVE {state.RequiredFuelSavingFraction:P0}  " +
              $"· ADD {state.FuelToAddLiters:0.0} L  " +
              $"· CONF {state.Confidence} ({state.Samples}/8)";
    }

    private void UpdateRaceControl(RaceControlWidgetState state)
    {
        RaceAttentionText.Text = !state.Available
            ? "NO DATA"
            : state.RequiresAttention ? "ATTENTION" : "CLEAR";
        RaceAttentionText.Foreground = state.RequiresAttention
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.LimeGreen;
        RacePenaltyText.Text = state.PenaltyStatus;
        RacePenaltyText.Foreground = state.OutstandingPenalties > 0
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.White;
        RacePitLapText.Text = $"{state.PitStatus} · {state.LapStatus}";
        RaceDamageText.Text = state.DamageStatus == "OK"
            ? "OK"
            : $"{state.DamageStatus} · {state.ImpactStatus}";
        RaceDamageText.Foreground = state.HasCriticalDamage
            ? System.Windows.Media.Brushes.OrangeRed
            : state.RequiresAttention
                ? System.Windows.Media.Brushes.Gold
                : System.Windows.Media.Brushes.White;
        RaceFlagText.Text = $"FLAG {state.FlagStatus}";
        RaceSystemsText.Text = state.SystemsStatus;
        RaceControlHeader.Background = state.HasCriticalDamage
            ? Brush(112, 23, 32)
            : state.RequiresAttention
                ? Brush(109, 72, 17)
                : Brush(27, 36, 65);
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

        (OverlayAlertSeverity Severity, string Icon, string Text, string Detail)? alert =
            raceControl.HasCriticalDamage
                ? (OverlayAlertSeverity.Critical, "!", "CRITICAL DAMAGE", raceControl.DamageStatus)
                : raceControl.OutstandingPenalties > 0
                    ? (OverlayAlertSeverity.Critical, "!", "PENALTY", raceControl.PenaltyStatus)
                    : session.FlagName == "RED"
                        ? (OverlayAlertSeverity.Critical, "!", "RED FLAG", "SESSION STOPPED")
                        : fuel.Available && !fuel.Learning && fuel.Status == "SHORT"
                            ? (OverlayAlertSeverity.Critical, "!", "ENERGY SHORTFALL", fuel.PlanSummary)
                            : TireTemperatureClassifier.Classify(hottestTire) == TireTemperatureBand.Critical
                                ? (OverlayAlertSeverity.Attention, "▲", "TYRE TEMPERATURE", $"HOTTEST {hottestTire:0}°C")
                                : maximumWear >= _profile.Settings.TireWearLimitPercent / 100
                                    ? (OverlayAlertSeverity.Attention, "▲", "TYRE WEAR", $"MAXIMUM {maximumWear:P0}")
                                    : session.FlagName == "YELLOW"
                                        ? (OverlayAlertSeverity.Attention, "▲", "YELLOW FLAG", "NO SAFETY-CAR ASSUMPTION")
                                        : session.RainIntensity >= 0.02
                                            ? (OverlayAlertSeverity.Attention, "☂", session.WeatherName, $"RAIN {session.RainIntensity:P0}")
                                            : dashboard.SpeedLimiterActive
                                                ? (OverlayAlertSeverity.Information, "P", "PIT LIMITER", "ACTIVE")
                                                : null;

        if (alert is null)
        {
            PriorityAlert.Visibility = Visibility.Collapsed;
            return;
        }

        var palette = OverlayVisualSystem.Resolve(_profile.Settings.Theme);
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
        var palette = OverlayVisualSystem.Resolve(_profile.Settings.Theme);
        var accent = new System.Windows.Media.SolidColorBrush(palette.Accent);
        var background = new System.Windows.Media.SolidColorBrush(palette.Background);

        foreach (var widget in AllWidgets())
        {
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
