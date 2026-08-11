using System.Drawing;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using LmuOverlay.Core;
using LmuOverlay.Domain;
using LmuOverlay.DirectX;
using LmuOverlay.LmuSharedMemory;
using LmuOverlay.Widgets;
using WinForms = System.Windows.Forms;

namespace LmuOverlay.Desktop;

public partial class App
{
    private readonly DispatcherTimer _windowTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(100),
    };

    private OverlayWindow? _overlay;
    private OverlayToolbarWindow? _toolbar;
    private ConfigurationWindow? _configurationWindow;
    private WinForms.NotifyIcon? _trayIcon;
    private Icon? _trayIconImage;
    private TelemetryRuntime? _telemetryRuntime;
    private NativeDashboardRenderer? _nativeDashboard;
    private NativeInputsRenderer? _nativeInputs;
    private NativeTimingRenderer? _nativeTiming;
    private bool _isExiting;
    private System.Windows.Rect? _gameBounds;
    private long _lastRenderedAt;
    private long _lastSlowUpdateAt;
    private LmuTelemetrySnapshot? _lastRenderedSnapshot;
    private long _nativeDashboardSequence;
    private NativeDashboardConfiguration? _nativeDashboardConfiguration;
    private long _nativeInputsSequence;
    private NativeInputsConfiguration? _nativeInputsConfiguration;
    private readonly FuelStrategyTracker _nativeFuelStrategyTracker = new();
    private long _lastNativeFuelStrategyAt;
    private double _nativeFuelSaveFraction;
    private long _nativeTimingSequence;
    private IReadOnlyList<LmuVehicleStanding>? _nativeTimingStandingsSource;
    private LiveStandingsWidgetState? _nativeLiveStandingsState;
    private RelativeWidgetState? _nativeRelativeState;
    private NativeTimingConfiguration? _lastNativeTimingConfiguration;
    private readonly SectorReferenceStore _sectorReferenceStore = new();
    private readonly PersonalBestLapStore _personalBestLapStore = new();
    private PersistentSectorReferenceTracker? _nativeSectorReferenceTracker;
    private readonly OfficialTimingOptimalProvider _officialTimingOptimal = new();
    private string _nativeSessionKey = string.Empty;
    private string _nativeSessionTrack = string.Empty;
    private int _nativeSessionCode = int.MinValue;
    private int _nativeSessionVehicleId = int.MinValue;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
            CrashLogWriter.TryWrite(args.Exception);
        if (e.Args.FirstOrDefault() == "--capture-visual-baselines")
        {
            StartVisualBaselineCapture(e.Args.Skip(1).FirstOrDefault());
            return;
        }

        _nativeSectorReferenceTracker = new(
            _sectorReferenceStore,
            _personalBestLapStore);
        _overlay = new OverlayWindow(
            new LayoutStore(),
            _sectorReferenceStore,
            _personalBestLapStore);
        _nativeDashboard = new NativeDashboardRenderer();
        _nativeInputs = new NativeInputsRenderer();
        _nativeTiming = new NativeTimingRenderer();
        _toolbar = new OverlayToolbarWindow(_overlay, ShowConfiguration);
        _telemetryRuntime = new TelemetryRuntime(
            () => new LmuSharedMemoryReader(),
            // LMU's named event wakes capture immediately. Eight milliseconds is
            // only the safety fallback when an event is missed or unavailable.
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromSeconds(1));
        _telemetryRuntime.SnapshotPublished += OnTelemetrySnapshot;
        _telemetryRuntime.Start();
        CreateTrayIcon();
        _windowTimer.Tick += OnWindowTick;
        _windowTimer.Start();
        CompositionTarget.Rendering += OnRendering;
    }

    private void StartVisualBaselineCapture(string? outputDirectory)
    {
        var destination = string.IsNullOrWhiteSpace(outputDirectory)
            ? System.IO.Path.Combine(AppContext.BaseDirectory, "visual-baselines")
            : System.IO.Path.GetFullPath(outputDirectory);
        var temporaryProfile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "lmu-overlay-visual-qa",
            $"{Guid.NewGuid():N}.json");
        _overlay = new OverlayWindow(
            new LayoutStore(temporaryProfile),
            new SectorReferenceStore(temporaryProfile + ".sectors"),
            new PersonalBestLapStore(temporaryProfile + ".personal-bests"))
        {
            ShowActivated = false,
        };
        _overlay.Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            _overlay.SetEditMode(true);
            var unavailable = LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Visual baseline");
            foreach (var (name, width, height) in new[]
            {
                ("720p", 1280d, 720d),
                ("1080p", 1920d, 1080d),
                ("ultrawide", 3440d, 1440d),
                ("1440p", 2560d, 1440d),
                ("4k", 3840d, 2160d),
            })
            {
                _overlay.UpdateFrame(new System.Windows.Rect(0, 0, width, height), unavailable);
                _overlay.CapturePng(
                    System.IO.Path.Combine(destination, $"overlay-{name}.png"),
                    (int)width,
                    (int)height);
            }

            _overlay.Close();
            try
            {
                System.IO.File.Delete(temporaryProfile);
            }
            catch (System.IO.IOException)
            {
            }

            Shutdown();
        });
        _overlay.Show();
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Configurar widgets", null, (_, _) => ShowConfiguration());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Editar layout", null, (_, _) => SetEditMode(true));
        menu.Items.Add("Bloquear overlay", null, (_, _) => SetEditMode(false));
        menu.Items.Add("Restaurar layout", null, (_, _) => _overlay?.ResetLayout());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApplication());

        _trayIconImage = Environment.ProcessPath is { Length: > 0 } executablePath
            ? Icon.ExtractAssociatedIcon(executablePath)
            : null;
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = _trayIconImage ?? SystemIcons.Application,
            Text = "LMU Overlay",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => SetEditMode(!(_overlay?.IsEditMode ?? false));
    }

    private void SetEditMode(bool enabled)
    {
        _overlay?.SetEditMode(enabled);
        _toolbar?.SyncFromOverlay();
    }

    private void ShowConfiguration()
    {
        if (_overlay is null)
        {
            return;
        }

        if (_configurationWindow is null)
        {
            _configurationWindow = new ConfigurationWindow(_overlay);
            _configurationWindow.Closed += (_, _) => _configurationWindow = null;
            _configurationWindow.Show();
        }
        else
        {
            _configurationWindow.Activate();
        }
    }

    private void OnWindowTick(object? sender, EventArgs e)
    {
        if (_overlay is null)
        {
            return;
        }

        var snapshot = _telemetryRuntime?.Latest
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");
        if (_telemetryRuntime is not null)
        {
            _overlay.UpdateRuntimeHealth(_telemetryRuntime.Health);
        }
        _overlay.UpdatePresentationHealth(new DesktopPresentationHealth(
            _nativeDashboard?.Health ?? new(false, 0, null, string.Empty),
            _nativeInputs?.Health ?? new(false, 0, null, string.Empty),
            _nativeTiming?.Health ?? new(false, 0, null, string.Empty)));
        var gameBounds = LmuWindowTracker.TryGetClientBounds();
        if (gameBounds is null && snapshot.State == LmuConnectionState.Connected)
        {
            var screen = WinForms.Screen.PrimaryScreen?.Bounds;
            if (screen is not null)
            {
                gameBounds = new System.Windows.Rect(
                    screen.Value.Left,
                    screen.Value.Top,
                    screen.Value.Width,
                    screen.Value.Height);
            }
        }

        if (gameBounds is null)
        {
            _gameBounds = null;
            Volatile.Write(ref _nativeDashboardConfiguration, null);
            Volatile.Write(ref _nativeInputsConfiguration, null);
            _nativeDashboard?.Hide(
                Interlocked.Increment(ref _nativeDashboardSequence));
            _nativeInputs?.Hide(
                Interlocked.Increment(ref _nativeInputsSequence));
            _nativeTiming?.Hide(
                Interlocked.Increment(ref _nativeTimingSequence));
            _overlay.SetGameAvailable(false);
            _toolbar?.SetGameAvailable(false);
            return;
        }

        _gameBounds = gameBounds;
        Volatile.Write(
            ref _nativeDashboardConfiguration,
            new NativeDashboardConfiguration(
                _overlay.GetNativeDashboardBounds(gameBounds.Value),
                _overlay.NativeDashboardShouldBeVisible,
                _overlay.NativeStyle with
                {
                    BackgroundOpacity = _overlay.NativeDashboardOpacity,
                }));
        Volatile.Write(
            ref _nativeInputsConfiguration,
            new NativeInputsConfiguration(
                _overlay.GetNativeInputsBounds(gameBounds.Value),
                _overlay.NativeInputsShouldBeVisible,
                _overlay.NativeStyle with
                {
                    BackgroundOpacity = _overlay.NativeInputsOpacity,
                }));
        PublishNativeTiming(snapshot, gameBounds.Value);
        _toolbar?.UpdateForGame(gameBounds.Value);
        var sinceRender = Stopwatch.GetTimestamp() - _lastRenderedAt;
        if (!_overlay.IsVisible ||
            sinceRender > Stopwatch.Frequency / 5)
        {
            RenderLatest(forceSlowUpdate: true);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_overlay is null || _gameBounds is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var refreshRate = Math.Clamp(_overlay.RequestedRefreshRateHz, 30, 144);
        var minimumTicks = Stopwatch.Frequency / (double)refreshRate;
        if (_lastRenderedAt > 0 && now - _lastRenderedAt < minimumTicks)
        {
            return;
        }

        RenderLatest(forceSlowUpdate: false);
    }

    private void RenderLatest(bool forceSlowUpdate)
    {
        if (_overlay is null || _gameBounds is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var slowUpdate = forceSlowUpdate ||
            _lastSlowUpdateAt == 0 ||
            now - _lastSlowUpdateAt >= Stopwatch.Frequency / 5;
        var snapshot = _telemetryRuntime?.Latest
            ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Waiting for LMU shared memory.");
        if (ReferenceEquals(snapshot, _lastRenderedSnapshot) && !slowUpdate)
        {
            _lastRenderedAt = now;
            return;
        }

        _overlay.SetNativeDashboardActive(_nativeDashboard?.IsAvailable == true);
        _overlay.SetNativeInputsActive(_nativeInputs?.IsAvailable == true);
        _overlay.SetNativeTimingActive(_nativeTiming?.IsAvailable == true);
        _officialTimingOptimal.Update(snapshot);
        _overlay.UpdateFrame(
            _gameBounds.Value,
            snapshot,
            slowUpdate,
            _officialTimingOptimal.GetOptimal(snapshot));
        _lastRenderedSnapshot = snapshot;
        _lastRenderedAt = now;
        if (slowUpdate)
        {
            _lastSlowUpdateAt = now;
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        CompositionTarget.Rendering -= OnRendering;
        _windowTimer.Stop();
        if (_telemetryRuntime is not null)
        {
            _telemetryRuntime.SnapshotPublished -= OnTelemetrySnapshot;
        }
        _telemetryRuntime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _nativeDashboard?.Dispose();
        _nativeDashboard = null;
        _nativeInputs?.Dispose();
        _nativeInputs = null;
        _nativeTiming?.Dispose();
        _nativeTiming = null;
        _officialTimingOptimal.Dispose();
        _configurationWindow?.Close();
        _toolbar?.Close();
        _overlay?.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _trayIconImage?.Dispose();
        _trayIconImage = null;

        Shutdown();
    }

    private void OnTelemetrySnapshot(LmuTelemetrySnapshot snapshot)
    {
        _officialTimingOptimal.Update(snapshot);
        var sessionEnded = snapshot.Session?.GamePhase == LmuGamePhase.SessionOver;
        var liveSnapshot = sessionEnded
            ? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Session ended.")
            : snapshot;
        var dashboard = EssentialWidgetStateFactory.CreateDashboard(liveSnapshot);
        var trackedSectors = _nativeSectorReferenceTracker?.Update(
            liveSnapshot,
            dashboard.SectorTimes) ?? dashboard.SectorTimes;
        dashboard = dashboard with
        {
            SectorTimes = trackedSectors,
            BestLapTimeSeconds =
                _nativeSectorReferenceTracker?.PersonalBestLapTimeSeconds > 0
                    ? _nativeSectorReferenceTracker.PersonalBestLapTimeSeconds
                    : dashboard.BestLapTimeSeconds,
            OptimalLapTimeSeconds = _officialTimingOptimal.GetOptimal(snapshot),
        };
        var sessionKey = GetNativeSessionKey(snapshot);
        var capturedTimestamp = Stopwatch.GetTimestamp();
        if (sessionEnded)
        {
            Volatile.Write(ref _nativeFuelSaveFraction, 0);
            Interlocked.Exchange(ref _lastNativeFuelStrategyAt, 0);
        }
        else if (_overlay is { } overlay &&
                 (Volatile.Read(ref _lastNativeFuelStrategyAt) == 0 ||
                  capturedTimestamp - Volatile.Read(ref _lastNativeFuelStrategyAt) >=
                  Stopwatch.Frequency / 5))
        {
            var settings = overlay.CurrentProfile.Settings;
            var fuelSave = _nativeFuelStrategyTracker.Update(
                snapshot,
                new FuelStrategyOptions(
                    settings.FuelReserveLaps,
                    settings.EnergyReservePercent / 100,
                    settings.ManualRemainingLaps,
                    settings.MaximumStintLaps,
                    settings.EstimatedPitLossSeconds,
                    settings.AvailableTireSets,
                    settings.TireWearLimitPercent / 100,
                    settings.EstimatedTireChangeSeconds,
                    settings.ManualRemainingMinutes,
                    settings.ManualLapTimeSeconds,
                    settings.ManualFuelPerLapLiters,
                    settings.ManualFuelCapacityLiters)).RequiredFuelSavingFraction;
            Volatile.Write(ref _nativeFuelSaveFraction, fuelSave);
            Interlocked.Exchange(ref _lastNativeFuelStrategyAt, capturedTimestamp);
        }
        var fuelSaveFraction = Volatile.Read(ref _nativeFuelSaveFraction);

        var dashboardRenderer = _nativeDashboard;
        var dashboardConfiguration = Volatile.Read(ref _nativeDashboardConfiguration);
        if (dashboardRenderer is not null && dashboardConfiguration is not null)
        {
            dashboardRenderer.Publish(new NativeDashboardFrame(
                dashboard,
                dashboardConfiguration.Bounds,
                dashboardConfiguration.Visible && dashboardRenderer.IsAvailable,
                Interlocked.Increment(ref _nativeDashboardSequence),
                capturedTimestamp,
                fuelSaveFraction,
                sessionKey,
                dashboardConfiguration.Style));
        }

        var inputsRenderer = _nativeInputs;
        var inputsConfiguration = Volatile.Read(ref _nativeInputsConfiguration);
        if (inputsRenderer is not null && inputsConfiguration is not null)
        {
            inputsRenderer.Publish(new NativeInputsFrame(
                EssentialWidgetStateFactory.CreateInputs(liveSnapshot),
                inputsConfiguration.Bounds,
                inputsConfiguration.Visible && inputsRenderer.IsAvailable,
                Interlocked.Increment(ref _nativeInputsSequence),
                capturedTimestamp,
                sessionKey,
                inputsConfiguration.Style));
        }
    }

    private string GetNativeSessionKey(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Session is not { } session || snapshot.Player is not { } player)
        {
            return string.Empty;
        }

        if (_nativeSessionCode == session.SessionCode &&
            _nativeSessionVehicleId == player.VehicleId &&
            string.Equals(_nativeSessionTrack, session.TrackName, StringComparison.Ordinal))
        {
            return _nativeSessionKey;
        }

        _nativeSessionTrack = session.TrackName;
        _nativeSessionCode = session.SessionCode;
        _nativeSessionVehicleId = player.VehicleId;
        _nativeSessionKey =
            $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}";
        return _nativeSessionKey;
    }

    private void PublishNativeTiming(
        LmuTelemetrySnapshot snapshot,
        System.Windows.Rect gameBounds)
    {
        var renderer = _nativeTiming;
        var overlay = _overlay;
        if (renderer is null || overlay is null)
        {
            return;
        }

        var configuration = new NativeTimingConfiguration(
            overlay.GetNativeLiveStandingsBounds(gameBounds),
            overlay.NativeLiveStandingsShouldBeVisible && renderer.IsAvailable,
            overlay.NativeLiveStandingsOpacity,
            overlay.GetNativeRelativeBounds(gameBounds),
            overlay.NativeRelativeShouldBeVisible && renderer.IsAvailable,
            overlay.NativeRelativeOpacity,
            overlay.NativeStyle,
            overlay.LiveStandingsMaximumRows,
            overlay.RelativeCarsEachSide);
        var standingsChanged = !ReferenceEquals(
            _nativeTimingStandingsSource,
            snapshot.Standings);
        if (!standingsChanged && configuration == _lastNativeTimingConfiguration)
        {
            return;
        }

        var preferencesChanged = _lastNativeTimingConfiguration is null ||
            configuration.LiveStandingsMaximumRows != _lastNativeTimingConfiguration.LiveStandingsMaximumRows ||
            configuration.RelativeCarsEachSide != _lastNativeTimingConfiguration.RelativeCarsEachSide;
        if (standingsChanged || preferencesChanged ||
            _nativeLiveStandingsState is null ||
            _nativeRelativeState is null)
        {
            _nativeTimingStandingsSource = snapshot.Standings;
            _nativeLiveStandingsState =
                EssentialWidgetStateFactory.CreateLiveStandings(
                    snapshot,
                    configuration.LiveStandingsMaximumRows);
            _nativeRelativeState =
                EssentialWidgetStateFactory.CreateRelative(
                    snapshot,
                    configuration.RelativeCarsEachSide);
        }

        _lastNativeTimingConfiguration = configuration;
        renderer.Publish(new NativeTimingFrame(
            _nativeLiveStandingsState,
            configuration.LiveStandingsBounds,
            configuration.LiveStandingsVisible,
            configuration.LiveStandingsOpacity,
            _nativeRelativeState,
            configuration.RelativeBounds,
            configuration.RelativeVisible,
            configuration.RelativeOpacity,
            Interlocked.Increment(ref _nativeTimingSequence),
            configuration.Style));
    }

    private sealed record NativeDashboardConfiguration(
        NativeDashboardBounds Bounds,
        bool Visible,
        NativeOverlayStyle Style);

    private sealed record NativeInputsConfiguration(
        NativeDashboardBounds Bounds,
        bool Visible,
        NativeOverlayStyle Style);

    private sealed record NativeTimingConfiguration(
        NativeDashboardBounds LiveStandingsBounds,
        bool LiveStandingsVisible,
        double LiveStandingsOpacity,
        NativeDashboardBounds RelativeBounds,
        bool RelativeVisible,
        double RelativeOpacity,
        NativeOverlayStyle Style,
        int LiveStandingsMaximumRows,
        int RelativeCarsEachSide);

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        base.OnExit(e);
    }
}
