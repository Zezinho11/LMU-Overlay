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
    private LayoutProfile _profile;
    private System.Windows.Point _dragStart;
    private double _dragLeft;
    private double _dragTop;
    private bool _dragging;
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
        ApplyProfile();

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
        InputsText.Text = inputs.Available
            ? $"THR {inputs.Throttle:P0}  BRK {inputs.Brake:P0}  STR {inputs.Steering:P0}"
            : "THR --  BRK --  STR --";

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
        EditHint.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticWidget.BorderBrush = enabled
            ? System.Windows.Media.Brushes.Orange
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(66, 211, 166));
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
    }

    private void WidgetMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditMode || e.OriginalSource == ResizeThumb)
        {
            return;
        }

        _dragging = true;
        _dragStart = e.GetPosition(OverlayCanvas);
        _dragLeft = Canvas.GetLeft(DiagnosticWidget);
        _dragTop = Canvas.GetTop(DiagnosticWidget);
        DiagnosticWidget.CaptureMouse();
        e.Handled = true;
    }

    private void WidgetMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || !IsEditMode)
        {
            return;
        }

        var position = e.GetPosition(OverlayCanvas);
        var left = _dragLeft + position.X - _dragStart.X;
        var top = _dragTop + position.Y - _dragStart.Y;
        Canvas.SetLeft(DiagnosticWidget, Snap(
            Math.Clamp(left, 0, Math.Max(0, ActualWidth - DiagnosticWidget.ActualWidth)),
            0,
            Math.Max(0, ActualWidth - DiagnosticWidget.ActualWidth)));
        Canvas.SetTop(DiagnosticWidget, Snap(
            Math.Clamp(top, 0, Math.Max(0, ActualHeight - DiagnosticWidget.ActualHeight)),
            0,
            Math.Max(0, ActualHeight - DiagnosticWidget.ActualHeight)));
    }

    private void WidgetMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        DiagnosticWidget.ReleaseMouseCapture();
        SaveProfile();
        e.Handled = true;
    }

    private void ResizeThumbDragDelta(
        object sender,
        System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!IsEditMode)
        {
            return;
        }

        DiagnosticWidget.Width = Math.Clamp(
            DiagnosticWidget.ActualWidth + e.HorizontalChange,
            DiagnosticWidget.MinWidth,
            Math.Max(DiagnosticWidget.MinWidth, ActualWidth - Canvas.GetLeft(DiagnosticWidget)));
        DiagnosticWidget.Height = Math.Clamp(
            DiagnosticWidget.ActualHeight + e.VerticalChange,
            DiagnosticWidget.MinHeight,
            Math.Max(DiagnosticWidget.MinHeight, ActualHeight - Canvas.GetTop(DiagnosticWidget)));
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
            Diagnostic = _profile.Diagnostic with
            {
                X = Canvas.GetLeft(DiagnosticWidget) / ActualWidth,
                Y = Canvas.GetTop(DiagnosticWidget) / ActualHeight,
                Width = DiagnosticWidget.ActualWidth / ActualWidth,
                Height = DiagnosticWidget.ActualHeight / ActualHeight,
            },
        };
        _layoutStore.Save(_profile);
    }

    private static double Snap(double value, double start, double end)
    {
        if (Math.Abs(value - start) <= SnapDistance)
        {
            return start;
        }

        return Math.Abs(value - end) <= SnapDistance ? end : value;
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
