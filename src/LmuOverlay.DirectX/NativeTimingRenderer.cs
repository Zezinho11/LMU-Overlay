using LmuOverlay.Core;

namespace LmuOverlay.DirectX;

public sealed class NativeTimingRenderer : IDisposable
{
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly ManualResetEventSlim _shutdown = new(false);
    private readonly Thread _thread;
    private NativeTimingFrame? _latest;
    private Exception? _lastFailure;
    private int _ready;
    private long _recoveryAttempts;
    private long _lastRecoveredUtcTicks;

    public NativeTimingRenderer()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "LMU DirectX timing panels",
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

    public void Publish(NativeTimingFrame frame)
    {
        Volatile.Write(ref _latest, frame);
        _frameReady.Set();
    }

    public void Hide(long sequence)
    {
        if (Volatile.Read(ref _latest) is not { } frame)
        {
            return;
        }

        Publish(frame with
        {
            LiveStandingsVisible = false,
            RelativeVisible = false,
            Sequence = sequence,
        });
    }

    private void Run()
    {
        var consecutiveFailures = 0;
        while (!_shutdown.IsSet)
        {
            try
            {
                using var host = new DirectCompositionTimingHost();
                if (consecutiveFailures > 0)
                {
                    Interlocked.Exchange(ref _lastRecoveredUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
                }
                consecutiveFailures = 0;
                Volatile.Write(ref _lastFailure, null);
                Volatile.Write(ref _ready, 1);
                while (!_shutdown.IsSet)
                {
                    _frameReady.WaitOne(16);
                    if (Volatile.Read(ref _latest) is { } frame) host.Render(frame);

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

    public void Dispose()
    {
        _shutdown.Set();
        _frameReady.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
        _frameReady.Dispose();
        _shutdown.Dispose();
    }
}
