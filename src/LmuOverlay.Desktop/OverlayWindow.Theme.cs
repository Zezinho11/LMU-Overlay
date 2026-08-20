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
}
