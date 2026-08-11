using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;
using LmuOverlay.Widgets;

namespace LmuOverlay.Desktop;

public partial class OverlayToolbarWindow : Window
{
    private static readonly MediaBrush NeutralBrush = CreateBrush(32, 43, 58);
    private static readonly MediaBrush NeutralBorderBrush = CreateBrush(82, 98, 119);
    private static readonly MediaBrush EditBrush = CreateBrush(168, 96, 24);
    private static readonly MediaBrush EditBorderBrush = CreateBrush(255, 190, 64);
    private static readonly MediaBrush LockedBrush = CreateBrush(18, 91, 74);
    private static readonly MediaBrush LockedBorderBrush = CreateBrush(66, 211, 166);

    private readonly OverlayWindow _overlay;
    private readonly Action _showConfiguration;
    private Rect _lastGameBounds;
    private bool _refreshing;
    private bool _positionInitialized;

    public OverlayToolbarWindow(
        OverlayWindow overlay,
        Action showConfiguration)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _showConfiguration = showConfiguration ??
            throw new ArgumentNullException(nameof(showConfiguration));
        InitializeComponent();
        SyncFromOverlay();
    }

    public void UpdateForGame(Rect gameBounds)
    {
        SyncFromOverlay();
        if (!_positionInitialized || gameBounds != _lastGameBounds)
        {
            PositionWithin(gameBounds);
            _positionInitialized = true;
            _lastGameBounds = gameBounds;
        }

        if (!IsVisible)
        {
            Show();
        }
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

    public void SyncFromOverlay()
    {
        _refreshing = true;
        var names = _overlay.ProfileNames.ToArray();
        var currentNames = ProfileSelector.Items
            .Cast<object>()
            .Select(item => item.ToString() ?? string.Empty)
            .ToArray();
        if (!names.SequenceEqual(currentNames, StringComparer.Ordinal))
        {
            ProfileSelector.ItemsSource = names;
        }

        ProfileSelector.SelectedItem = _overlay.ActiveProfileName;
        _refreshing = false;

        var editing = _overlay.IsEditMode;
        var language = _overlay.CurrentProfile.Settings.Language;
        SettingsButton.Content = OverlayText.Get(language, OverlayTextKey.Settings);
        EditButton.Content = OverlayText.Get(language, editing ? OverlayTextKey.Editing : OverlayTextKey.Edit);
        EditButton.Background = editing ? EditBrush : NeutralBrush;
        EditButton.BorderBrush = editing ? EditBorderBrush : NeutralBorderBrush;
        LockButton.Content = OverlayText.Get(language, editing ? OverlayTextKey.Lock : OverlayTextKey.Locked);
        LockButton.Background = editing ? NeutralBrush : LockedBrush;
        LockButton.BorderBrush = editing
            ? NeutralBorderBrush
            : LockedBorderBrush;
    }

    private void PositionWithin(Rect gameBounds)
    {
        var proposedLeft = _positionInitialized
            ? Left
            : gameBounds.Right - Width - 16;
        var proposedTop = _positionInitialized
            ? Top
            : gameBounds.Top + 16;
        Left = Math.Clamp(
            proposedLeft,
            gameBounds.Left,
            Math.Max(gameBounds.Left, gameBounds.Right - Width));
        Top = Math.Clamp(
            proposedTop,
            gameBounds.Top,
            Math.Max(gameBounds.Top, gameBounds.Bottom - Height));
    }

    private void ProfileSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_refreshing || ProfileSelector.SelectedItem is not string name)
        {
            return;
        }

        try
        {
            _overlay.SwitchProfile(name);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.IO.IOException)
        {
            WpfMessageBox.Show(
                this,
                exception.Message,
                "Não foi possível trocar o perfil",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        SyncFromOverlay();
    }

    private void SettingsClicked(object sender, RoutedEventArgs e) =>
        _showConfiguration();

    private void EditClicked(object sender, RoutedEventArgs e)
    {
        _overlay.SetEditMode(true);
        SyncFromOverlay();
    }

    private void LockClicked(object sender, RoutedEventArgs e)
    {
        _overlay.SetEditMode(false);
        SyncFromOverlay();
    }

    private void DragHandleMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue) =>
        new(MediaColor.FromRgb(red, green, blue));
}
