using System.Windows;

namespace LmuOverlay.Desktop;

public partial class ConfigurationWindow : Window
{
    private readonly OverlayWindow _overlay;

    public ConfigurationWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
        LoadProfile();
    }

    private void LoadProfile()
    {
        var profile = _overlay.CurrentProfile;
        Set(DashboardVisible, DashboardOpacity, profile.Diagnostic);
        Set(InputsVisible, InputsOpacity, profile.Inputs);
        Set(StandingsVisible, StandingsOpacity, profile.LiveStandings);
        Set(RelativeVisible, RelativeOpacity, profile.Relative);
        Set(SessionVisible, SessionOpacity, profile.SessionFlags);
        Set(FuelVisible, FuelOpacity, profile.FuelStrategy);
    }

    private void ApplyClicked(object sender, RoutedEventArgs e)
    {
        var profile = _overlay.CurrentProfile;
        _overlay.ApplyDisplaySettings(profile with
        {
            Diagnostic = Read(DashboardVisible, DashboardOpacity, profile.Diagnostic),
            Inputs = Read(InputsVisible, InputsOpacity, profile.Inputs),
            LiveStandings = Read(
                StandingsVisible,
                StandingsOpacity,
                profile.LiveStandings),
            Relative = Read(RelativeVisible, RelativeOpacity, profile.Relative),
            SessionFlags = Read(SessionVisible, SessionOpacity, profile.SessionFlags),
            FuelStrategy = Read(FuelVisible, FuelOpacity, profile.FuelStrategy),
        });
    }

    private void ResetClicked(object sender, RoutedEventArgs e)
    {
        _overlay.ResetLayout();
        LoadProfile();
    }

    private void CloseClicked(object sender, RoutedEventArgs e) => Close();

    private static void Set(
        System.Windows.Controls.CheckBox visible,
        System.Windows.Controls.Slider opacity,
        WidgetPlacement placement)
    {
        visible.IsChecked = placement.Visible;
        opacity.Value = placement.Opacity;
    }

    private static WidgetPlacement Read(
        System.Windows.Controls.CheckBox visible,
        System.Windows.Controls.Slider opacity,
        WidgetPlacement placement) => placement with
    {
        Visible = visible.IsChecked == true,
        Opacity = opacity.Value,
    };
}
