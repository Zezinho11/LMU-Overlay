using System.Runtime.InteropServices;
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
    private readonly Queue<(double Throttle, double Brake)> _pedalHistory = new();
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
    private uint _renderedScoringSequence = uint.MaxValue;
    private uint _lastPedalTelemetrySequence = uint.MaxValue;

    public OverlayWindow(LayoutStore layoutStore)
    {
        _layoutStore = layoutStore;
        _profile = layoutStore.Load();
        InitializeComponent();
        _shiftLights =
        [
            ShiftLight01, ShiftLight02, ShiftLight03, ShiftLight04,
            ShiftLight05, ShiftLight06, ShiftLight07, ShiftLight08,
            ShiftLight09, ShiftLight10, ShiftLight11, ShiftLight12,
        ];
        SourceInitialized += (_, _) => ApplyInteractionStyle();
        Loaded += (_, _) => ApplyProfile();
    }

    public bool IsEditMode { get; private set; }
    public LayoutProfile CurrentProfile => _profile;
    public string ActiveProfileName => _layoutStore.ActiveProfileName;
    public IReadOnlyList<string> ProfileNames => _layoutStore.ProfileNames;
    public int RequestedRefreshRateHz => _profile.Settings.RefreshRateHz;

    public void UpdateRuntimeHealth(TelemetryRuntimeHealth health) =>
        _runtimeHealth = health;

    public void ExportDiagnostics(string destinationPath) =>
        DiagnosticsReportWriter.Write(
            destinationPath,
            _lastSnapshot,
            _runtimeHealth,
            _profile,
            ActiveProfileName);

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

    public void UpdateFrame(Rect gameBounds, LmuTelemetrySnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastGameBounds = gameBounds;
        Left = gameBounds.Left;
        Top = gameBounds.Top;
        Width = gameBounds.Width;
        Height = gameBounds.Height;
        if (!IsEditMode)
        {
            ApplyProfile();
        }

        var connected = snapshot.State == LmuConnectionState.Connected;
        ConnectionText.Text = connected ? "CONECTADO" : snapshot.State.ToString().ToUpperInvariant();
        var dashboard = EssentialWidgetStateFactory.CreateDashboard(snapshot);
        var inputs = EssentialWidgetStateFactory.CreateInputs(snapshot);
        TrackText.Text = dashboard.Available ? dashboard.TrackName : "LMU";
        SpeedText.Text = dashboard.Available
            ? $"{dashboard.SpeedKilometersPerHour:0} KM/H"
            : "--- KM/H";
        GearText.Text = dashboard.Available ? dashboard.Gear : "N";
        RpmText.Text = dashboard.Available
            ? $"RPM {dashboard.EngineRpm:0}"
            : "RPM ----";
        PitLimiterIndicator.Visibility = dashboard.Available && dashboard.SpeedLimiterActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        PositionText.Text = dashboard.Available
            ? $"POS {dashboard.Position}"
            : "POS --";
        LapText.Text = dashboard.Available ? $"LAP {dashboard.LapNumber}" : "LAP --";
        DeltaText.Text = dashboard.Available
            ? $"DELTA {dashboard.DeltaBestSeconds:+0.000;-0.000;0.000}"
            : "DELTA --";
        FuelText.Text = dashboard.Available
            ? $"FUEL {dashboard.FuelLiters:0.0} L"
            : "FUEL --.- L";
        VirtualEnergyDashText.Text = dashboard.Available
            ? $"VIRTUAL ENERGY {dashboard.VirtualEnergyFraction:P0}"
            : "VIRTUAL ENERGY --%";
        BrakeBiasText.Text = dashboard.Available &&
            dashboard.RearBrakeBiasFraction is >= 0 and <= 1
                ? $"BRAKE BIAS {(1 - dashboard.RearBrakeBiasFraction):P1}"
                : "BRAKE BIAS --.-%";
        CurrentLapText.Text =
            $"CURRENT {FormatLapTime(dashboard.CurrentLapTimeSeconds)}";
        LastLapText.Text = $"LAST {FormatLapTime(dashboard.LastLapTimeSeconds)}";
        BestLapText.Text = $"BEST {FormatLapTime(dashboard.BestLapTimeSeconds)}";
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
        SessionTimeText.Text = FormatSessionTime(dashboard.SessionRemainingSeconds);
        PenaltyDashText.Text = dashboard.OutstandingPenalties > 0
            ? $"PENALTY {dashboard.OutstandingPenalties}"
            : "PENALTY CLEAR";
        PenaltyDashText.Foreground = dashboard.OutstandingPenalties > 0
            ? System.Windows.Media.Brushes.OrangeRed
            : IndicatorEnabledTextBrush;
        SectorLastLapText.Text = FormatLapTime(dashboard.LastLapTimeSeconds);
        SectorBestLapText.Text = FormatLapTime(dashboard.BestLapTimeSeconds);
        UpdateSectorReadings(dashboard.SectorTimes);
        DashboardThrottleText.Text = dashboard.Available ? $"{dashboard.Throttle:P0}" : "--";
        DashboardBrakeText.Text = dashboard.Available ? $"{dashboard.Brake:P0}" : "--";
        LongitudinalGText.Text = dashboard.Available
            ? $"{dashboard.LongitudinalAccelerationG:+0.0;-0.0;0.0}"
            : "--";
        LateralGText.Text = dashboard.Available
            ? $"{dashboard.LateralAccelerationG:+0.0;-0.0;0.0}"
            : "--";
        TireCompoundText.Text = dashboard.Available
            ? $"COMPOUND {dashboard.TireCompound.ToUpperInvariant()}"
            : "COMPOUND --";
        UpdatePedalGraph(dashboard, snapshot.TelemetrySequence);
        var tire = dashboard.TireTemperatures;
        UpdateTireReading(
            FrontLeftTireIcon,
            FrontLeftTireText,
            tire.FrontLeftCelsius,
            dashboard.TireWear.FrontLeftFraction,
            dashboard.Available);
        UpdateTireReading(
            FrontRightTireIcon,
            FrontRightTireText,
            tire.FrontRightCelsius,
            dashboard.TireWear.FrontRightFraction,
            dashboard.Available);
        UpdateTireReading(
            RearLeftTireIcon,
            RearLeftTireText,
            tire.RearLeftCelsius,
            dashboard.TireWear.RearLeftFraction,
            dashboard.Available);
        UpdateTireReading(
            RearRightTireIcon,
            RearRightTireText,
            tire.RearRightCelsius,
            dashboard.TireWear.RearRightFraction,
            dashboard.Available);
        UpdateShiftLights(dashboard.Available ? dashboard.EngineRpmFraction : 0);
        TcLevelText.Text = FormatControlLevel(
            dashboard.TractionControlLevel,
            dashboard.TractionControlMaximum);
        TcSlipLevelText.Text = FormatControlLevel(
            dashboard.TractionControlSlipLevel,
            dashboard.TractionControlSlipMaximum);
        TcCutLevelText.Text = FormatControlLevel(
            dashboard.TractionControlCutLevel,
            dashboard.TractionControlCutMaximum);
        AbsLevelText.Text = FormatControlLevel(
            dashboard.AbsLevel,
            dashboard.AbsMaximum);
        UpdateIndicator(
            AbsIndicator,
            AbsIndicatorText,
            dashboard.AbsLevel > 0,
            dashboard.AbsActive);
        UpdateIndicator(
            TcIndicator,
            TcIndicatorText,
            dashboard.TractionControlLevel > 0,
            dashboard.TractionControlActive);
        InputsText.Text = inputs.Available
            ? $"THR {inputs.Throttle:P0}  BRK {inputs.Brake:P0}  STR {inputs.Steering:P0}"
            : "THR --  BRK --  STR --";
        if (_renderedScoringSequence != snapshot.ScoringSequence)
        {
            UpdateStandings(EssentialWidgetStateFactory.CreateLiveStandings(snapshot));
            UpdateRelative(EssentialWidgetStateFactory.CreateRelative(snapshot));
            UpdateSessionFlags(EssentialWidgetStateFactory.CreateSessionFlags(snapshot));
            _renderedScoringSequence = snapshot.ScoringSequence;
        }
        UpdateFuelStrategy(_fuelStrategyTracker.Update(
            snapshot,
            new FuelStrategyOptions(
                _profile.Settings.FuelReserveLaps,
                _profile.Settings.EnergyReservePercent / 100,
                _profile.Settings.ManualRemainingLaps,
                _profile.Settings.MaximumStintLaps,
                _profile.Settings.EstimatedPitLossSeconds,
                _profile.Settings.AvailableTireSets,
                _profile.Settings.TireWearLimitPercent / 100,
                _profile.Settings.EstimatedTireChangeSeconds)));
        UpdateRaceControl(EssentialWidgetStateFactory.CreateRaceControl(snapshot));

        SetGameAvailable(connected || IsEditMode);
    }

    private void UpdateShiftLights(double rpmFraction)
    {
        var activeFraction = Math.Clamp((rpmFraction - 0.65) / 0.35, 0, 1);
        var activeCount = (int)Math.Ceiling(activeFraction * _shiftLights.Length);
        for (var index = 0; index < _shiftLights.Length; index++)
        {
            _shiftLights[index].Fill = index < activeCount
                ? index switch
                {
                    < 4 => ShiftGreenBrush,
                    < 7 => ShiftAmberBrush,
                    < 10 => ShiftRedBrush,
                    _ => ShiftBlueBrush,
                }
                : ShiftOffBrush;
        }
    }

    private static void UpdateTireReading(
        Border icon,
        TextBlock text,
        double temperatureCelsius,
        double wearFraction,
        bool available)
    {
        text.Text = available
            ? $"{temperatureCelsius:0}° · {wearFraction:P0}"
            : "--° · --%";
        icon.Background = available
            ? TireTemperatureClassifier.Classify(temperatureCelsius) switch
            {
                TireTemperatureBand.Cold => TireColdBrush,
                TireTemperatureBand.Warming => TireWarmingBrush,
                TireTemperatureBand.Optimal => TireOptimalBrush,
                TireTemperatureBand.Hot => TireHotBrush,
                TireTemperatureBand.Critical => TireCriticalBrush,
                _ => TireUnknownBrush,
            }
            : TireUnknownBrush;
    }

    private static void UpdateIndicator(
        Border indicator,
        TextBlock label,
        bool configured,
        bool active)
    {
        indicator.Background = active
            ? IndicatorActiveBrush
            : configured
                ? IndicatorEnabledBrush
                : IndicatorOffBrush;
        label.Foreground = active
            ? System.Windows.Media.Brushes.White
            : configured
                ? IndicatorEnabledTextBrush
                : IndicatorTextOffBrush;
    }

    private static string FormatControlLevel(int value, int maximum) =>
        maximum > 0
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "--";

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
        timeText.Text = value > 0 ? $"{value:0.000}" : "--.---";
        if (value <= 0 || bestSeconds <= 0)
        {
            deltaText.Text = "--.---";
            deltaText.Foreground = System.Windows.Media.Brushes.LightGray;
            return;
        }

        var delta = value - bestSeconds;
        deltaText.Text = delta.ToString("+0.000;-0.000;0.000");
        deltaText.Foreground = delta <= 0
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;
    }

    private void UpdatePedalGraph(DashboardWidgetState dashboard, uint telemetrySequence)
    {
        if (dashboard.Available && _lastPedalTelemetrySequence != telemetrySequence)
        {
            _pedalHistory.Enqueue((dashboard.Throttle, dashboard.Brake));
            while (_pedalHistory.Count > 90)
            {
                _pedalHistory.Dequeue();
            }

            _lastPedalTelemetrySequence = telemetrySequence;
        }

        var values = _pedalHistory.ToArray();
        var throttlePoints = new System.Windows.Media.PointCollection(values.Length);
        var brakePoints = new System.Windows.Media.PointCollection(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var x = values.Length <= 1 ? 230 : index * 230d / (values.Length - 1);
            throttlePoints.Add(new System.Windows.Point(x, 101 - values[index].Throttle * 96));
            brakePoints.Add(new System.Windows.Point(x, 101 - values[index].Brake * 96));
        }

        ThrottleTrace.Points = throttlePoints;
        BrakeTrace.Points = brakePoints;
        Canvas.SetLeft(
            GForceDot,
            24 + Math.Clamp(dashboard.LateralAccelerationG / 2, -1, 1) * 20);
        Canvas.SetTop(
            GForceDot,
            46 - Math.Clamp(dashboard.LongitudinalAccelerationG / 2, -1, 1) * 38);
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
        ApplyInteractionStyle();
        if (enabled && _lastGameBounds.Width > 0)
        {
            SetGameAvailable(true);
            Activate();
        }
        else
        {
            SaveProfile();
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
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var item = _profile.Diagnostic;
        DiagnosticWidget.Visibility = item.Visible ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticWidget.Opacity = item.Opacity;
        DiagnosticWidget.Width = Math.Max(
            DiagnosticWidget.MinWidth,
            item.Width * ActualWidth * item.Scale);
        DiagnosticWidget.Height = Math.Max(
            DiagnosticWidget.MinHeight,
            item.Height * ActualHeight * item.Scale);
        Canvas.SetLeft(DiagnosticWidget, Math.Clamp(
            item.X * ActualWidth,
            0,
            Math.Max(0, ActualWidth - DiagnosticWidget.Width)));
        Canvas.SetTop(DiagnosticWidget, Math.Clamp(
            item.Y * ActualHeight,
            0,
            Math.Max(0, ActualHeight - DiagnosticWidget.Height)));
        ApplyPlacement(InputsWidget, _profile.Inputs, 170, 70);
        ApplyPlacement(LiveStandingsWidget, _profile.LiveStandings, 220, 150);
        ApplyPlacement(RelativeWidget, _profile.Relative, 140, 240);
        ApplyPlacement(SessionFlagsWidget, _profile.SessionFlags, 300, 150);
        ApplyPlacement(FuelStrategyWidget, _profile.FuelStrategy, 300, 190);
        ApplyPlacement(RaceControlWidget, _profile.RaceControl, 280, 130);
        ApplyTheme();
    }

    private void ApplyPlacement(
        FrameworkElement element,
        WidgetPlacement placement,
        double minimumWidth,
        double minimumHeight)
    {
        element.Visibility = placement.Visible ? Visibility.Visible : Visibility.Collapsed;
        element.Opacity = placement.Opacity;
        element.Width = Math.Max(
            minimumWidth,
            placement.Width * ActualWidth * placement.Scale);
        element.Height = Math.Max(
            minimumHeight,
            placement.Height * ActualHeight * placement.Scale);
        Canvas.SetLeft(element, Math.Clamp(
            placement.X * ActualWidth, 0, Math.Max(0, ActualWidth - element.Width)));
        Canvas.SetTop(element, Math.Clamp(
            placement.Y * ActualHeight, 0, Math.Max(0, ActualHeight - element.Height)));
    }

    private void UpdateStandings(LiveStandingsWidgetState standings)
    {
        StandingsRows.Children.Clear();

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
        foreach (var width in new[] { 28d, 38d, 32d, 40d, 72d })
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
            Width = 32,
            Height = 17,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ManufacturerAbbreviation(row.VehicleModel),
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 8,
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
            9);
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
            row.IsInPitLane
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.Gainsboro,
            FontWeights.SemiBold,
            9,
            System.Windows.HorizontalAlignment.Right,
            new Thickness(0, 0, 4, 0));
        return grid;
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
        RelativeRows.Children.Clear();
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
            Height = 40,
            Background = row.IsPlayer
                ? playerBackground
                : rowIndex % 2 == 0
                    ? Brush(17, 25, 43)
                    : Brush(24, 33, 54),
            ToolTip =
                $"P{row.OverallPosition} · {row.DriverName} · {row.VehicleClass}",
        };
        foreach (var width in new[] { 36d, 40d })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(width),
            });
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(62),
        });

        var numberBadge = new Border
        {
            Width = 30,
            Height = 25,
            CornerRadius = new CornerRadius(3, 0, 0, 3),
            Background = classBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.CarNumber,
                Foreground = System.Windows.Media.Brushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Bahnschrift"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(numberBadge, 0);
        grid.Children.Add(numberBadge);

        var classBadge = new Border
        {
            Width = 36,
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
                FontSize = 10,
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
            FontSize = 12,
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
            FontSize = 12,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(gap, 3);
        grid.Children.Add(gap);
        return grid;
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

    private void ApplyTheme()
    {
        var accent = _profile.Settings.Theme switch
        {
            "HighContrast" => System.Windows.Media.Brushes.White,
            "Black" => Brush(100, 110, 120),
            _ => Brush(66, 211, 166),
        };
        var background = _profile.Settings.Theme switch
        {
            "HighContrast" => Brush(0, 0, 0),
            "Black" => Brush(2, 3, 4),
            _ => Brush(10, 15, 26),
        };

        foreach (var widget in AllWidgets())
        {
            widget.BorderBrush = IsEditMode
                ? System.Windows.Media.Brushes.Orange
                : accent;
            if (widget != DiagnosticWidget)
            {
                widget.Background = background;
            }
        }
    }

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

        var minimumWidth = widget.MinWidth > 0 ? widget.MinWidth : 120;
        var minimumHeight = widget.MinHeight > 0 ? widget.MinHeight : 60;
        var maximumWidth = Math.Max(minimumWidth, ActualWidth - Canvas.GetLeft(widget));
        var maximumHeight = Math.Max(minimumHeight, ActualHeight - Canvas.GetTop(widget));
        var aspectRatio = WidgetAspectRatio(widget.Name);
        if (aspectRatio <= 0)
        {
            widget.Width = Math.Clamp(
                widget.ActualWidth + e.HorizontalChange,
                minimumWidth,
                maximumWidth);
            widget.Height = Math.Clamp(
                widget.ActualHeight + e.VerticalChange,
                minimumHeight,
                maximumHeight);
            return;
        }

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

    private static double WidgetAspectRatio(string widgetName) => widgetName switch
    {
        "DiagnosticWidget" => 800d / 480d,
        "LiveStandingsWidget" or "RelativeWidget" => 260d / 410d,
        "SessionFlagsWidget" => 500d / 190d,
        "FuelStrategyWidget" => 500d / 340d,
        "RaceControlWidget" => 430d / 190d,
        _ => 0,
    };

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
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(red, green, blue));
        brush.Freeze();
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
