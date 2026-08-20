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
}
