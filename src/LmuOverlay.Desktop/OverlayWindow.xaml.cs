using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private static readonly System.Windows.Media.Geometry StandingsCarGeometry =
        CreateCarGeometry();

    private readonly LayoutStore _layoutStore;
    private readonly FuelStrategyTracker _fuelStrategyTracker = new();
    private System.Windows.Shapes.Ellipse[] _shiftLights = [];
    private LayoutProfile _profile;
    private System.Windows.Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private bool _dragging;
    private FrameworkElement? _activeWidget;
    private Rect _lastGameBounds;

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
        var tire = dashboard.TireTemperatures;
        TiresText.Text = dashboard.Available
            ? $"FL {tire.FrontLeftCelsius:0}°  FR {tire.FrontRightCelsius:0}°  " +
              $"RL {tire.RearLeftCelsius:0}°  RR {tire.RearRightCelsius:0}°"
            : "FL --°  FR --°  RL --°  RR --°";
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
        UpdateStandings(EssentialWidgetStateFactory.CreateLiveStandings(snapshot));
        UpdateRelative(EssentialWidgetStateFactory.CreateRelative(snapshot));
        UpdateSessionFlags(EssentialWidgetStateFactory.CreateSessionFlags(snapshot));
        UpdateFuelStrategy(_fuelStrategyTracker.Update(snapshot));

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
        DiagnosticWidget.Width = Math.Max(DiagnosticWidget.MinWidth, item.Width * ActualWidth);
        DiagnosticWidget.Height = Math.Max(DiagnosticWidget.MinHeight, item.Height * ActualHeight);
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
        ApplyPlacement(RelativeWidget, _profile.Relative, 220, 130);
        ApplyPlacement(SessionFlagsWidget, _profile.SessionFlags, 220, 70);
        ApplyPlacement(FuelStrategyWidget, _profile.FuelStrategy, 210, 125);
    }

    private void ApplyPlacement(
        FrameworkElement element,
        WidgetPlacement placement,
        double minimumWidth,
        double minimumHeight)
    {
        element.Visibility = placement.Visible ? Visibility.Visible : Visibility.Collapsed;
        element.Opacity = placement.Opacity;
        element.Width = Math.Max(minimumWidth, placement.Width * ActualWidth);
        element.Height = Math.Max(minimumHeight, placement.Height * ActualHeight);
        Canvas.SetLeft(element, Math.Clamp(
            placement.X * ActualWidth, 0, Math.Max(0, ActualWidth - element.Width)));
        Canvas.SetTop(element, Math.Clamp(
            placement.Y * ActualHeight, 0, Math.Max(0, ActualHeight - element.Height)));
    }

    private void UpdateStandings(LiveStandingsWidgetState standings)
    {
        StandingsRows.Children.Clear();
        StandingsTitleText.Text = string.IsNullOrWhiteSpace(standings.PlayerClass)
            ? "LIVE STANDINGS"
            : standings.PlayerClass.ToUpperInvariant();
        var visibleCars = standings.Classes.Sum(item => item.Rows.Count);
        StandingsSubtitleText.Text = visibleCars > 0
            ? $"{visibleCars} CARS"
            : "NO DATA";

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
        foreach (var width in new[] { 30d, 35d, 38d, 48d, 100d })
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
        var carIcon = new System.Windows.Shapes.Path
        {
            Data = StandingsCarGeometry,
            Fill = CarIconBrush(row.VehicleName),
            Width = 25,
            Height = 13,
            Stretch = System.Windows.Media.Stretch.Fill,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(carIcon, 1);
        grid.Children.Add(carIcon);
        AddStandingsText(
            grid,
            row.CarNumber,
            2,
            System.Windows.Media.Brushes.White,
            FontWeights.Bold,
            10);
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
            10);
        AddStandingsText(
            grid,
            FormatStandingsInterval(row),
            5,
            row.IsInPitLane
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.Gainsboro,
            FontWeights.SemiBold,
            10,
            System.Windows.HorizontalAlignment.Right,
            new Thickness(0, 0, 8, 0));
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
                Brush(245, 245, 245),
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
            _ => Brush(135, 151, 168),
        };
    }

    private static System.Windows.Media.Geometry CreateCarGeometry()
    {
        var geometry = System.Windows.Media.Geometry.Parse(
            "M 1,7 L 4,3 L 10,2 L 13,0 L 20,0 L 23,2 L 29,3 L 32,7 " +
            "L 29,11 L 23,11 L 21,9 L 12,9 L 10,11 L 4,11 Z");
        geometry.Freeze();
        return geometry;
    }

    private void UpdateRelative(RelativeWidgetState relative)
    {
        RelativeRows.Children.Clear();
        if (relative.Rows.Count == 0)
        {
            RelativeRows.Children.Add(new TextBlock
            {
                Text = "Waiting for player standings",
                Foreground = System.Windows.Media.Brushes.LightGray,
            });
            return;
        }

        foreach (var row in relative.Rows)
        {
            var gap = row.RelativeLaps switch
            {
                > 0 => $"+{row.RelativeLaps}L",
                < 0 => $"{row.RelativeLaps}L",
                _ when row.IsPlayer => "0.000",
                _ when double.IsFinite(row.RelativeGapSeconds) =>
                    row.RelativeGapSeconds.ToString("+0.000;-0.000;0.000"),
                _ => "--.---",
            };
            var pit = row.IsInPitLane ? "  PIT" : string.Empty;
            RelativeRows.Children.Add(new TextBlock
            {
                Text = $"{row.OverallPosition,2}  {row.DriverName}  [{row.VehicleClass}]  {gap}{pit}",
                Foreground = row.IsPlayer
                    ? System.Windows.Media.Brushes.White
                    : System.Windows.Media.Brushes.Gainsboro,
                Background = row.IsPlayer
                    ? new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(80, 66, 211, 166))
                    : System.Windows.Media.Brushes.Transparent,
                FontWeight = row.IsPlayer ? FontWeights.Bold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
    }

    private void UpdateSessionFlags(SessionFlagsWidgetState state)
    {
        if (!state.Available)
        {
            SessionNameText.Text = "SESSION";
            FlagText.Text = "NO DATA";
            FlagText.Foreground = System.Windows.Media.Brushes.LightGray;
            SessionDetailText.Text = "--:--  LAP --  AIR --°  TRACK --°";
            return;
        }

        SessionNameText.Text = $"{state.SessionName} · {state.PhaseName}";
        FlagText.Text = state.FlagName;
        FlagText.Foreground = state.FlagName switch
        {
            "YELLOW" => System.Windows.Media.Brushes.Gold,
            "GREEN" => System.Windows.Media.Brushes.LimeGreen,
            _ => System.Windows.Media.Brushes.LightGray,
        };
        var remaining = state.RemainingSeconds > 0
            ? TimeSpan.FromSeconds(state.RemainingSeconds).ToString(
                state.RemainingSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
            : "--:--";
        var lap = state.MaximumLaps > 0
            ? $"{state.CurrentLap}/{state.MaximumLaps}"
            : state.CurrentLap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SessionDetailText.Text =
            $"{remaining}  LAP {lap}  AIR {state.AmbientTemperatureCelsius:0}°  " +
            $"TRACK {state.TrackTemperatureCelsius:0}°";
    }

    private void UpdateFuelStrategy(FuelStrategyWidgetState state)
    {
        if (!state.Available)
        {
            FuelStatusText.Text = "NO DATA";
            FuelMainText.Text = "FUEL --.- L  •  VE --%";
            FuelProjectionText.Text = "FUEL --.- L/LAP  •  RANGE --.-";
            VirtualEnergyText.Text = "VE --.-%/LAP  •  RANGE --.-";
            FuelMarginText.Text = "NEED --.- L  •  MARGIN --.- L";
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
        FuelMainText.Text =
            $"FUEL {state.FuelLiters:0.0} L  •  VE {state.VirtualEnergyFraction:P0}";
        FuelProjectionText.Text = state.Learning
            ? "Complete a lap to calculate"
            : $"FUEL {state.AverageConsumptionLitersPerLap:0.00} L/LAP  •  " +
              $"RANGE {state.EstimatedRangeLaps:0.0}";
        VirtualEnergyText.Text = state.AverageVirtualEnergyFractionPerLap > 0
            ? $"VE {state.AverageVirtualEnergyFractionPerLap:P1}/LAP  •  " +
              $"RANGE {state.EstimatedVirtualEnergyRangeLaps:0.0}"
            : "VE LEARNING • COMPLETE A LAP";
        FuelMarginText.Text = state.Learning
            ? "NEED --.- L  •  MARGIN --.- L"
            : $"NEED {state.RequiredFuelLiters:0.0} L  •  " +
              $"MARGIN {state.FuelMarginLiters:+0.0;-0.0;0.0} L";
        FuelMarginText.Foreground = state.FuelMarginLiters < 0
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.LimeGreen;
    }

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
        Canvas.SetLeft(_activeWidget, Snap(
            Math.Clamp(left, 0, Math.Max(0, ActualWidth - _activeWidget.ActualWidth)),
            0,
            Math.Max(0, ActualWidth - _activeWidget.ActualWidth)));
        Canvas.SetTop(_activeWidget, Snap(
            Math.Clamp(top, 0, Math.Max(0, ActualHeight - _activeWidget.ActualHeight)),
            0,
            Math.Max(0, ActualHeight - _activeWidget.ActualHeight)));
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
        widget.Width = Math.Clamp(
            widget.ActualWidth + e.HorizontalChange,
            minimumWidth,
            Math.Max(minimumWidth, ActualWidth - Canvas.GetLeft(widget)));
        widget.Height = Math.Clamp(
            widget.ActualHeight + e.VerticalChange,
            minimumHeight,
            Math.Max(minimumHeight, ActualHeight - Canvas.GetTop(widget)));
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
        };
        _layoutStore.Save(_profile);
    }

    private WidgetPlacement CapturePlacement(
        FrameworkElement element,
        WidgetPlacement current) => current with
    {
        X = Canvas.GetLeft(element) / ActualWidth,
        Y = Canvas.GetTop(element) / ActualHeight,
        Width = element.ActualWidth / ActualWidth,
        Height = element.ActualHeight / ActualHeight,
    };

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
