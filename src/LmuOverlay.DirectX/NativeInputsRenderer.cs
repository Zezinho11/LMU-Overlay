using System.Diagnostics;

namespace LmuOverlay.DirectX;

public sealed class NativeInputsRenderer : IDisposable
{
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly ManualResetEventSlim _shutdown = new(false);
    private readonly object _latestSync = new();
    private readonly Thread _thread;
    private NativeInputsFrame _latest;
    private bool _hasLatest;
    private Exception? _startupFailure;
    private int _ready;

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
        _startupFailure is null &&
        _thread.IsAlive;

    public string FailureDetail => _startupFailure?.Message ?? string.Empty;

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
        try
        {
            using var host = new DirectCompositionInputsHost();
            Volatile.Write(ref _ready, 1);
            while (!_shutdown.IsSet)
            {
                _frameReady.WaitOne(8);
                if (TryGetLatest(out var frame))
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
