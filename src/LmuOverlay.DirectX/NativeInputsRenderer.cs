using System.Diagnostics;
using LmuOverlay.Core;

namespace LmuOverlay.DirectX;

public sealed class NativeInputsRenderer : IDisposable
{
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly ManualResetEventSlim _shutdown = new(false);
    private readonly object _latestSync = new();
    private readonly Thread _thread;
    private NativeInputsFrame _latest;
    private bool _hasLatest;
    private Exception? _lastFailure;
    private int _ready;
    private long _recoveryAttempts;
    private long _lastRecoveredUtcTicks;

    public NativeInputsRenderer()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "LMU DirectX inputs",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public bool IsAvailable =>
        Volatile.Read(ref _ready) == 1 &&
        _thread.IsAlive;

    public string FailureDetail => Volatile.Read(ref _lastFailure)?.Message ?? string.Empty;
    public PresentationHostHealth Health => new(
        IsAvailable,
        Interlocked.Read(ref _recoveryAttempts),
        Interlocked.Read(ref _lastRecoveredUtcTicks) is var ticks && ticks > 0
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null,
        FailureDetail);

    public void Publish(NativeInputsFrame frame)
    {
        lock (_latestSync)
        {
            _latest = frame;
            _hasLatest = true;
        }
        _frameReady.Set();
    }

    public void Hide(long sequence)
    {
        if (!TryGetLatest(out var frame))
        {
            return;
        }

        Publish(frame with
        {
            Visible = false,
            Sequence = sequence,
            CapturedTimestamp = Stopwatch.GetTimestamp(),
        });
    }

    private void Run()
    {
        var consecutiveFailures = 0;
        while (!_shutdown.IsSet)
        {
            try
            {
                using var host = new DirectCompositionInputsHost();
                if (consecutiveFailures > 0)
                {
                    Interlocked.Exchange(ref _lastRecoveredUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
                }
                consecutiveFailures = 0;
                Volatile.Write(ref _lastFailure, null);
                Volatile.Write(ref _ready, 1);
                while (!_shutdown.IsSet)
                {
                    _frameReady.WaitOne(8);
                    if (TryGetLatest(out var frame)) host.Render(frame);
                    host.PumpMessages();
                }
            }
            catch (Exception exception) when (!_shutdown.IsSet)
            {
                Volatile.Write(ref _ready, 0);
                Volatile.Write(ref _lastFailure, exception);
                Interlocked.Increment(ref _recoveryAttempts);
                consecutiveFailures++;
                if (_shutdown.Wait(PresentationRecoveryPolicy.DelayForFailure(consecutiveFailures))) break;
            }
            finally
            {
                Volatile.Write(ref _ready, 0);
            }
        }
    }

    private bool TryGetLatest(out NativeInputsFrame frame)
    {
        lock (_latestSync)
        {
            frame = _latest;
            return _hasLatest;
        }
    }

    public void Dispose()
    {
        _shutdown.Set();
        _frameReady.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        _frameReady.Dispose();
        _shutdown.Dispose();
    }
}
