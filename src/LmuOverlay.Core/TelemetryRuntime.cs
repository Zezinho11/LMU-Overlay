using System.Diagnostics;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.Core;

public sealed record TelemetryRuntimeHealth(
    long SuccessfulReads,
    long FailedReads,
    long Reconnects,
    double LastReadMilliseconds,
    double MaximumReadMilliseconds,
    DateTimeOffset? LastSuccessfulRead,
    string LastError);

public sealed class TelemetryRuntime : IAsyncDisposable
{
    private readonly Func<ILmuTelemetrySource> _sourceFactory;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _reconnectInterval;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _worker;
    private LmuTelemetrySnapshot _latest = LmuTelemetrySnapshot.Unavailable(
        LmuConnectionState.Disconnected,
        "Telemetry runtime is starting.");
    private long _successfulReads;
    private long _failedReads;
    private long _reconnects;
    private long _lastReadTicks;
    private long _maximumReadTicks;
    private long _lastSuccessfulReadUtcTicks;
    private string _lastError = string.Empty;

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
                ToMilliseconds(Interlocked.Read(ref _maximumReadTicks)),
                successfulUtcTicks > 0
                    ? new DateTimeOffset(successfulUtcTicks, TimeSpan.Zero)
                    : null,
                Volatile.Read(ref _lastError));
        }
    }

    public void Start()
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(() => RunAsync(_shutdown.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        ILmuTelemetrySource? source = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
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
                    Volatile.Write(ref _latest, snapshot);

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
                    Volatile.Write(
                        ref _latest,
                        LmuTelemetrySnapshot.Unavailable(
                            LmuConnectionState.InvalidData,
                            $"Telemetry read failed: {exception.Message}"));
                    source?.Dispose();
                    source = null;
                    wait = _reconnectInterval;
                }

                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            source?.Dispose();
        }
    }

    private void RecordReadDuration(long elapsedTicks)
    {
        Interlocked.Exchange(ref _lastReadTicks, elapsedTicks);
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

    private static TimeSpan ValidateInterval(TimeSpan value, string name) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(name);

    private static double ToMilliseconds(long stopwatchTicks) =>
        stopwatchTicks * 1000d / Stopwatch.Frequency;

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_worker is not null)
        {
            await _worker.ConfigureAwait(false);
        }

        _shutdown.Dispose();
    }
}
