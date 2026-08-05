namespace LmuOverlay.DirectX;

public sealed class NativeTimingRenderer : IDisposable
{
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly ManualResetEventSlim _shutdown = new(false);
    private readonly Thread _thread;
    private NativeTimingFrame? _latest;
    private Exception? _startupFailure;
    private int _ready;

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
        _startupFailure is null &&
        _thread.IsAlive;
    public string FailureDetail => _startupFailure?.Message ?? string.Empty;

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
        try
        {
            using var host = new DirectCompositionTimingHost();
            Volatile.Write(ref _ready, 1);
            while (!_shutdown.IsSet)
            {
                _frameReady.WaitOne(16);
                if (Volatile.Read(ref _latest) is { } frame)
                {
                    host.Render(frame);
                }

                host.PumpMessages();
            }
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
            Volatile.Write(ref _ready, 0);
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
