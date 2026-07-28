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

    private readonly LayoutStore _layoutStore;
    private readonly FuelStrategyTracker _fuelStrategyTracker = new();
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
        SourceInitialized += (_, _) => ApplyInteractionStyle();
        Loaded += (_, _) => ApplyProfile();
    }

    public bool IsEditMode { get; private set; }

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
        SpeedText.Text = dashboard.Available
            ? $"{dashboard.SpeedKilometersPerHour:0} km/h  G{dashboard.Gear}  {dashboard.EngineRpm:0} rpm"
            : "--- km/h";
        DetailText.Text = dashboard.Available
            ? $"{dashboard.TrackName} • P{dashboard.Position} • combustível {dashboard.FuelLiters:0.0} L"
            : snapshot.Detail;
        DeltaText.Text = dashboard.Available
            ? $"DELTA {dashboard.DeltaBestSeconds:+0.000;-0.000;0.000}"
            : "DELTA --";
        var tire = dashboard.TireTemperatures;
        TiresText.Text = dashboard.Available
            ? $"FL {tire.FrontLeftCelsius:0}°  FR {tire.FrontRightCelsius:0}°  " +
              $"RL {tire.RearLeftCelsius:0}°  RR {tire.RearRightCelsius:0}°"
            : "FL --°  FR --°  RL --°  RR --°";
        InputsText.Text = inputs.Available
            ? $"THR {inputs.Throttle:P0}  BRK {inputs.Brake:P0}  STR {inputs.Steering:P0}"
            : "THR --  BRK --  STR --";
        UpdateStandings(EssentialWidgetStateFactory.CreateLiveStandings(snapshot));
        UpdateRelative(EssentialWidgetStateFactory.CreateRelative(snapshot));
        UpdateSessionFlags(EssentialWidgetStateFactory.CreateSessionFlags(snapshot));
        UpdateFuelStrategy(_fuelStrategyTracker.Update(snapshot));

        SetGameAvailable(connected || IsEditMode);
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
        foreach (var category in standings.Classes)
        {
            StandingsRows.Children.Add(new TextBlock
            {
                Text = category.ClassName,
                Foreground = category.IsPlayerClass
                    ? System.Windows.Media.Brushes.Orange
                    : System.Windows.Media.Brushes.LightGray,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 3, 0, 1),
            });
            foreach (var row in category.Rows)
            {
                var pit = row.IsInPitLane ? "  PIT" : string.Empty;
                var time = row.LastLapTimeSeconds > 0
                    ? TimeSpan.FromSeconds(row.LastLapTimeSeconds).ToString(@"m\:ss\.fff")
                    : "--:--.---";
                StandingsRows.Children.Add(new TextBlock
                {
                    Text = $"{row.ClassPosition,2}  {row.DriverName}  {time}{pit}",
                    Foreground = row.IsPlayer
                        ? System.Windows.Media.Brushes.White
                        : System.Windows.Media.Brushes.Gainsboro,
                    FontWeight = row.IsPlayer ? FontWeights.Bold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
        }
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
