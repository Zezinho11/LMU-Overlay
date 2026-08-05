using System.Diagnostics;

namespace LmuOverlay.DirectX;

public sealed class NativeDashboardRenderer : IDisposable
{
    private readonly AutoResetEvent _frameReady = new(false);
    private readonly ManualResetEventSlim _shutdown = new(false);
    private readonly Thread _thread;
    private NativeDashboardFrame? _latest;
    private Exception? _startupFailure;
    private int _ready;

    public NativeDashboardRenderer()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "LMU DirectX dashboard",
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

    public void Publish(NativeDashboardFrame frame)
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
            Visible = false,
            Sequence = sequence,
            CapturedTimestamp = Stopwatch.GetTimestamp(),
        });
    }

    private void Run()
    {
        try
        {
            using var host = new DirectCompositionDashboardHost();
            Volatile.Write(ref _ready, 1);
            while (!_shutdown.IsSet)
            {
                _frameReady.WaitOne(8);
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
