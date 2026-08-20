using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;
using LmuOverlay.Widgets;
using LmuOverlay.Core;

namespace LmuOverlay.Desktop;

public partial class ConfigurationWindow : Window
{
    private readonly OverlayWindow _overlay;
    private bool _refreshingProfiles;
    private bool _loadingProfile;

    public ConfigurationWindow(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
        var compatibility = GameCompatibilityProbe.Detect();
        var vr = VrRuntimeProbe.Detect();
        var build = string.IsNullOrWhiteSpace(compatibility.InstalledBuildId)
            ? "--"
            : compatibility.InstalledBuildId;
        CompatibilityStatus.Text =
            $"LMU {build} · " +
            $"{compatibility.State} · VR: {(vr.SteamVrIsActiveOpenXrRuntime ? "SteamVR/OpenXR" : vr.Detail)}";
        PresetSelector.ItemsSource = LayoutPresets.Names;
        PresetSelector.SelectedIndex = 0;
        RefreshProfiles();
        LoadProfile();
        ApplyLocalization();
    }




}
