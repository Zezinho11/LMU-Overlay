using System.Diagnostics;
using LmuOverlay.Contracts;
using LmuOverlay.Domain;

namespace LmuOverlay.Core;

public sealed class ReplayTelemetrySource :
    ILmuTelemetrySource,
    IWaitableTelemetrySource
{
    private readonly IReadOnlyList<TelemetryRecordingFrame> _frames;
    private readonly double _speed;
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private int _nextFrame;
    private LmuTelemetrySnapshot? _latest;
    private bool _disposed;

    public ReplayTelemetrySource(TelemetryRecording recording, double speed = 1)
    {
        ArgumentNullException.ThrowIfNull(recording);
        if (!double.IsFinite(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        _frames = recording.Frames;
        _speed = speed;
    }

    public int RemainingFrames => Math.Max(0, _frames.Count - _nextFrame);

    public LmuTelemetrySnapshot ReadTelemetrySnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_nextFrame >= _frames.Count)
        {
            return _latest ?? LmuTelemetrySnapshot.Unavailable(
                LmuConnectionState.Disconnected,
                "Replay contains no frames.");
        }

        _latest = _frames[_nextFrame++].Snapshot;
        return _latest;
    }

    public LmuProbeSnapshot ReadProbeSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = _latest ??
            (_frames.Count > 0 ? _frames[0].Snapshot : null);
        if (snapshot is null)
        {
            return LmuProbeSnapshot.Disconnected("Replay contains no frames.");
        }

        return new LmuProbeSnapshot(
            snapshot.State,
            snapshot.GameVersion.ToString(),
            snapshot.Session?.TrackName ?? string.Empty,
            snapshot.Session?.SessionCode ?? 0,
            snapshot.Player?.VehicleModel ?? string.Empty,
            snapshot.ActiveVehicles,
            snapshot.ScoredVehicles,
            snapshot.Player is not null,
            snapshot.CapturedAt,
            "Replay");
    }

    public TelemetryUpdateWaitResult WaitForUpdate(
        WaitHandle cancellation,
        TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(cancellation);
        if (_nextFrame >= _frames.Count)
        {
            return cancellation.WaitOne(timeout)
                ? TelemetryUpdateWaitResult.Cancelled
                : TelemetryUpdateWaitResult.TimedOut;
        }

        var targetMicroseconds = _frames[_nextFrame].OffsetMicroseconds / _speed;
        var elapsedMicroseconds =
            (Stopwatch.GetTimestamp() - _startedTimestamp) * 1_000_000d /
            Stopwatch.Frequency;
        var remaining = TimeSpan.FromMicroseconds(
            Math.Max(0, targetMicroseconds - elapsedMicroseconds));
        if (remaining <= TimeSpan.Zero)
        {
            return TelemetryUpdateWaitResult.Signaled;
        }

        var wait = remaining < timeout ? remaining : timeout;
        if (cancellation.WaitOne(wait))
        {
            return TelemetryUpdateWaitResult.Cancelled;
        }

        return wait == remaining
            ? TelemetryUpdateWaitResult.Signaled
            : TelemetryUpdateWaitResult.TimedOut;
    }

    public void Dispose() => _disposed = true;
}
