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

public partial class OverlayWindow
{
    private WidgetPlacement PlacementFor(FrameworkElement widget) => widget.Name switch
    {
        "DiagnosticWidget" => _profile.Diagnostic,
        "InputsWidget" => _profile.Inputs,
        "LiveStandingsWidget" => _profile.LiveStandings,
        "RelativeWidget" => _profile.Relative,
        "SessionFlagsWidget" => _profile.SessionFlags,
        "FuelStrategyWidget" => _profile.FuelStrategy,
        "RaceControlWidget" => _profile.RaceControl,
        "PriorityAlert" => _profile.PriorityAlert,
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
        PriorityAlert,
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
            PriorityAlert = CapturePlacement(
                PriorityAlert,
                _profile.PriorityAlert),
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
