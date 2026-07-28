using System.Runtime.CompilerServices;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.Core;

public sealed class TelemetryPollingService(
    ILmuTelemetrySource source,
    TimeSpan interval)
{
    private readonly ILmuTelemetrySource _source =
        source ?? throw new ArgumentNullException(nameof(source));

    private readonly TimeSpan _interval =
        interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval));

    public async IAsyncEnumerable<LmuTelemetrySnapshot> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return _source.ReadTelemetrySnapshot();

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return _source.ReadTelemetrySnapshot();
        }
    }
}
