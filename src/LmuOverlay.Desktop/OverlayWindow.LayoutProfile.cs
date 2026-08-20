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
        PriorityAlertResizeThumb.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
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
        PriorityAlert.BorderBrush = borderBrush;
        PriorityAlert.IsHitTestVisible = enabled;
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
        ApplyPriorityAlertPlacement();
        ApplyTheme();
        OverlayLocalization.Apply(this, _profile.Settings.Language);
    }

    private void ApplyPriorityAlertPlacement()
    {
        var bounds = ResponsiveWidgetLayout.Calculate(
            LayoutWidth,
            LayoutHeight,
            _profile.PriorityAlert,
            ResponsiveWidgetLayout.For(PriorityAlert.Name));
        PriorityAlert.Width = bounds.Width;
        PriorityAlert.Height = bounds.Height;
        PriorityAlert.Opacity = _profile.PriorityAlert.Opacity;
        Canvas.SetLeft(PriorityAlert, bounds.X);
        Canvas.SetTop(PriorityAlert, bounds.Y);
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
}
