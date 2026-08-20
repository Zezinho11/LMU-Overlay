using System.Globalization;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class OfficialTimingOptimalProvider
{
    private void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _nextAttemptAt, 0);
        Volatile.Write(ref _lastError, string.Empty);
        _lastSuccessAt = DateTimeOffset.UtcNow;
    }

    private void RecordFailure(string message)
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        var exponent = Math.Min(Math.Max(0, failures - 1), 6);
        var delaySeconds = Math.Min(
            _options.MaximumBackoff.TotalSeconds,
            _options.InitialBackoff.TotalSeconds * Math.Pow(2, exponent));
        Interlocked.Exchange(
            ref _nextAttemptAt,
            Stopwatch.GetTimestamp() +
            (long)(Stopwatch.Frequency * Math.Max(0, delaySeconds)));
        Volatile.Write(ref _lastError, message);
    }

    private static bool Valid(double seconds) =>
        double.IsFinite(seconds) && seconds is > 0 and < 1_800;

    public void Dispose()
    {
        _cancellation.Cancel();
        try { _worker.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _cancellation.Dispose();
        _client.Dispose();
    }

    private sealed class TimingTarget
    {
        public TimingTarget(
            string sessionKey,
            string trackName,
            int sessionCode,
            int vehicleId,
            bool discardExistingHistory)
        {
            SessionKey = sessionKey;
            TrackName = trackName;
            SessionCode = sessionCode;
            VehicleId = vehicleId;
            DiscardExistingHistory = discardExistingHistory;
        }

        public string SessionKey { get; }
        public string TrackName { get; }
        public int SessionCode { get; }
        public int VehicleId { get; }
        public bool DiscardExistingHistory { get; }
        public HashSet<string>? ExcludedLapSignatures { get; set; }
    }
    private sealed record TimingValue(string SessionKey, double OptimalLapSeconds);
}
