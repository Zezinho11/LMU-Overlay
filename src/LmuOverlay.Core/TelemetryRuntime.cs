using System.Diagnostics;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.Core;

public sealed record TelemetryRuntimeHealth(
    long SuccessfulReads,
    long FailedReads,
    long Reconnects,
    double LastReadMilliseconds,
    double AverageReadMilliseconds,
    double MaximumReadMilliseconds,
    DateTimeOffset? LastSuccessfulRead,
    string LastError)
{
    public long EventWakeups { get; init; }
    public long EventTimeouts { get; init; }
    public long DuplicateSnapshots { get; init; }
    public long PublishedSnapshots { get; init; }
}

public sealed class TelemetryRuntime : IAsyncDisposable
{
    private readonly Func<ILmuTelemetrySource> _sourceFactory;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _reconnectInterval;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ManualResetEventSlim _shutdownSignal = new(false);
    private Thread? _worker;
    private LmuTelemetrySnapshot _latest = LmuTelemetrySnapshot.Unavailable(
        LmuConnectionState.Disconnected,
        "Telemetry runtime is starting.");
    private long _successfulReads;
    private long _failedReads;
    private long _reconnects;
    private long _lastReadTicks;
    private long _totalReadTicks;
    private long _maximumReadTicks;
    private long _lastSuccessfulReadUtcTicks;
    private string _lastError = string.Empty;
    private long _eventWakeups;
    private long _eventTimeouts;
    private long _duplicateSnapshots;
    private long _publishedSnapshots;

    public TelemetryRuntime(
        Func<ILmuTelemetrySource> sourceFactory,
        TimeSpan pollInterval,
        TimeSpan reconnectInterval)
    {
        _sourceFactory = sourceFactory ??
            throw new ArgumentNullException(nameof(sourceFactory));
        _pollInterval = ValidateInterval(pollInterval, nameof(pollInterval));
        _reconnectInterval = ValidateInterval(
            reconnectInterval,
            nameof(reconnectInterval));
    }

    public LmuTelemetrySnapshot Latest => Volatile.Read(ref _latest);

    public event Action<LmuTelemetrySnapshot>? SnapshotPublished;

    public TelemetryRuntimeHealth Health
    {
        get
        {
            var successfulUtcTicks = Interlocked.Read(
                ref _lastSuccessfulReadUtcTicks);
            return new(
                Interlocked.Read(ref _successfulReads),
                Interlocked.Read(ref _failedReads),
                Interlocked.Read(ref _reconnects),
                ToMilliseconds(Interlocked.Read(ref _lastReadTicks)),
                successfulUtcTicks > 0
                    ? ToMilliseconds(Interlocked.Read(ref _totalReadTicks)) /
                      Math.Max(
                          1,
                          Interlocked.Read(ref _successfulReads) +
                          Interlocked.Read(ref _failedReads))
                    : 0,
                ToMilliseconds(Interlocked.Read(ref _maximumReadTicks)),
                successfulUtcTicks > 0
                    ? new DateTimeOffset(successfulUtcTicks, TimeSpan.Zero)
                    : null,
                Volatile.Read(ref _lastError))
            {
                EventWakeups = Interlocked.Read(ref _eventWakeups),
                EventTimeouts = Interlocked.Read(ref _eventTimeouts),
                DuplicateSnapshots = Interlocked.Read(ref _duplicateSnapshots),
                PublishedSnapshots = Interlocked.Read(ref _publishedSnapshots),
            };
        }
    }

    public void Start()
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "LMU telemetry capture",
            Priority = ThreadPriority.AboveNormal,
        };
        _worker.Start();
    }

    private void Run()
    {
        var cancellationToken = _shutdown.Token;
        ILmuTelemetrySource? source = null;
        var nextAttempt = Stopwatch.GetTimestamp();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var waitedForSourceEvent = source is IWaitableTelemetrySource;
                if (source is IWaitableTelemetrySource waitable)
                {
                    var waitResult = waitable.WaitForUpdate(
                        _shutdownSignal.WaitHandle,
                        _pollInterval);
                    if (waitResult == TelemetryUpdateWaitResult.Cancelled)
                    {
                        break;
                    }

                    if (waitResult == TelemetryUpdateWaitResult.Signaled)
                    {
                        Interlocked.Increment(ref _eventWakeups);
                    }
                    else
                    {
                        Interlocked.Increment(ref _eventTimeouts);
                    }
                }
                else if (!WaitUntil(nextAttempt, cancellationToken))
                {
                    break;
                }

                var attemptStarted = Stopwatch.GetTimestamp();
                var wait = _pollInterval;
                try
                {
                    if (source is null)
                    {
                        source = _sourceFactory();
                        Interlocked.Increment(ref _reconnects);
                    }

                    var started = Stopwatch.GetTimestamp();
                    var snapshot = source.ReadTelemetrySnapshot();
                    RecordReadDuration(Stopwatch.GetTimestamp() - started);
                    Publish(snapshot);

                    if (snapshot.State == LmuConnectionState.Connected)
                    {
                        Interlocked.Increment(ref _successfulReads);
                        Interlocked.Exchange(
                            ref _lastSuccessfulReadUtcTicks,
                            DateTimeOffset.UtcNow.UtcTicks);
                        Volatile.Write(ref _lastError, string.Empty);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedReads);
                        Volatile.Write(ref _lastError, snapshot.Detail);
                        source.Dispose();
                        source = null;
                        wait = _reconnectInterval;
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref _failedReads);
                    Volatile.Write(ref _lastError, exception.Message);
                    Publish(LmuTelemetrySnapshot.Unavailable(
                        LmuConnectionState.InvalidData,
                        $"Telemetry read failed: {exception.Message}"));
                    source?.Dispose();
                    source = null;
                    wait = _reconnectInterval;
                }

                var afterAttempt = Stopwatch.GetTimestamp();
                if (waitedForSourceEvent && source is not null &&
                    wait == _pollInterval)
                {
                    nextAttempt = afterAttempt;
                }
                else
                {
                    var waitTicks = ToStopwatchTicks(wait);
                    nextAttempt = AdvanceDeadline(
                        nextAttempt,
                        afterAttempt,
                        waitTicks);
                }
            }
        }
        finally
        {
            source?.Dispose();
        }
    }

    private void RecordReadDuration(long elapsedTicks)
    {
        Interlocked.Exchange(ref _lastReadTicks, elapsedTicks);
        Interlocked.Add(ref _totalReadTicks, elapsedTicks);
        var maximum = Interlocked.Read(ref _maximumReadTicks);
        while (elapsedTicks > maximum)
        {
            var observed = Interlocked.CompareExchange(
                ref _maximumReadTicks,
                elapsedTicks,
                maximum);
            if (observed == maximum)
            {
                break;
            }

            maximum = observed;
        }
    }

    private void Publish(LmuTelemetrySnapshot snapshot)
    {
        var previous = Volatile.Read(ref _latest);
        Volatile.Write(ref _latest, snapshot);
        if (ReferenceEquals(previous, snapshot))
        {
            Interlocked.Increment(ref _duplicateSnapshots);
            return;
        }

        Interlocked.Increment(ref _publishedSnapshots);

        var handlers = SnapshotPublished;
        if (handlers is null)
        {
            return;
        }

        try
        {
            handlers(snapshot);
        }
        catch
        {
            // A presentation consumer must never interrupt capture or force a
            // reconnect. Consumers expose their own health/fallback state.
        }
    }

    private static TimeSpan ValidateInterval(TimeSpan value, string name) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(name);

    private static double ToMilliseconds(long stopwatchTicks) =>
        stopwatchTicks * 1000d / Stopwatch.Frequency;

    private static long ToStopwatchTicks(TimeSpan interval) =>
        Math.Max(1, (long)Math.Round(interval.TotalSeconds * Stopwatch.Frequency));

    internal static long AdvanceDeadline(
        long previousDeadline,
        long attemptCompleted,
        long waitTicks)
    {
        var scheduledDeadline = previousDeadline + waitTicks;
        // Never build a backlog. The dashboard consumes only the freshest
        // sample and resumes from the current clock after a missed deadline.
        return scheduledDeadline <= attemptCompleted
            ? attemptCompleted + waitTicks
            : scheduledDeadline;
    }

    private bool WaitUntil(
        long targetTimestamp,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var remaining = targetTimestamp - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return true;
            }

            var remainingMilliseconds =
                remaining * 1000d / Stopwatch.Frequency;
            if (remainingMilliseconds > 1.5)
            {
                var waitMilliseconds = Math.Max(
                    1,
                    (int)Math.Floor(remainingMilliseconds - 0.5));
                // DisposeAsync cancels the token and sets this signal. Waiting
                // only on the signal keeps shutdown interruptible without
                // letting the expected cancellation escape the worker thread
                // as an unhandled OperationCanceledException.
                if (_shutdownSignal.Wait(waitMilliseconds))
                {
                    return false;
                }
            }
            else
            {
                Thread.SpinWait(64);
            }
        }

        return false;
    }

    public ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _shutdownSignal.Set();
        if (_worker is not null)
        {
            _worker.Join(TimeSpan.FromSeconds(2));
        }

        _shutdownSignal.Dispose();
        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }
}
