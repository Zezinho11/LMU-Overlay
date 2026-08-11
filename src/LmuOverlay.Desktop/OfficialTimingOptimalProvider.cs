using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

/// <summary>
/// Reads the same read-only standings history endpoint used by LMU's own
/// Live Timing UI. The endpoint contains the complete sector history required
/// for a true theoretical optimal; shared memory exposes only two best-sector
/// accumulators and cannot reconstruct an earlier best sector three.
/// </summary>
public sealed class OfficialTimingOptimalProvider : IDisposable
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:6397/"),
        Timeout = TimeSpan.FromMilliseconds(750),
    };
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private TimingTarget? _target;
    private TimingValue? _value;
    private bool _hasSessionIdentity;
    private string _lastTrackName = string.Empty;
    private int _lastSessionCode;
    private int _lastVehicleId;
    private double _lastSessionElapsedTime;
    private long _sessionGeneration;

    public OfficialTimingOptimalProvider() => _worker = Task.Run(PollAsync);

    public void Update(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player ||
            snapshot.Session is not { } session ||
            session.GamePhase == LmuGamePhase.SessionOver)
        {
            Volatile.Write(ref _target, null);
            Volatile.Write(ref _value, null);
            return;
        }

        var isNewSession = IsNewSession(
            _hasSessionIdentity,
            _lastTrackName,
            _lastSessionCode,
            _lastVehicleId,
            _lastSessionElapsedTime,
            session.TrackName,
            session.SessionCode,
            player.VehicleId,
            session.CurrentElapsedTime);
        if (isNewSession)
        {
            _sessionGeneration++;
        }

        _hasSessionIdentity = true;
        _lastTrackName = session.TrackName;
        _lastSessionCode = session.SessionCode;
        _lastVehicleId = player.VehicleId;
        _lastSessionElapsedTime = session.CurrentElapsedTime;

        var previous = Volatile.Read(ref _target);
        var sessionKey =
            $"{session.TrackName}\u001f{session.SessionCode}\u001f" +
            $"{player.VehicleId}\u001f{_sessionGeneration}";
        if (previous?.SessionKey == sessionKey)
        {
            return;
        }

        var target = new TimingTarget(
            sessionKey,
            session.TrackName,
            session.SessionCode,
            player.VehicleId,
            discardExistingHistory: isNewSession);
        Volatile.Write(ref _value, null);
        Volatile.Write(ref _target, target);
    }

    public double GetOptimal(LmuTelemetrySnapshot snapshot)
    {
        var target = Volatile.Read(ref _target);
        var value = Volatile.Read(ref _value);
        return target is not null && value?.SessionKey == target.SessionKey
            ? value.OptimalLapSeconds
            : 0;
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false))
            {
                var target = Volatile.Read(ref _target);
                if (target is null)
                {
                    continue;
                }

                try
                {
                    using var response = await _client.GetAsync(
                        "rest/watch/standings/history",
                        HttpCompletionOption.ResponseHeadersRead,
                        _cancellation.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(
                        _cancellation.Token).ConfigureAwait(false);
                    using var document = await JsonDocument.ParseAsync(
                        stream,
                        cancellationToken: _cancellation.Token).ConfigureAwait(false);
                    if (target.DiscardExistingHistory &&
                        target.ExcludedLapSignatures is null)
                    {
                        target.ExcludedLapSignatures = CaptureLapSignatures(
                            document.RootElement,
                            target.VehicleId);
                        continue;
                    }

                    var optimal = ParseOptimal(
                        document.RootElement,
                        target.VehicleId,
                        target.ExcludedLapSignatures);
                    if (optimal > 0 && Volatile.Read(ref _target)?.SessionKey == target.SessionKey)
                    {
                        Volatile.Write(ref _value, new TimingValue(target.SessionKey, optimal));
                    }
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or JsonException)
                {
                    // The local UI server is optional and may start after the overlay.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static double ParseOptimal(JsonElement history, int vehicleId)
        => ParseOptimal(history, vehicleId, excludedLapSignatures: null);

    public static bool IsNewSession(
        bool hasPreviousSession,
        string previousTrackName,
        int previousSessionCode,
        int previousVehicleId,
        double previousElapsedTime,
        string trackName,
        int sessionCode,
        int vehicleId,
        double elapsedTime)
    {
        if (!hasPreviousSession)
        {
            return false;
        }

        var sameIdentity = previousSessionCode == sessionCode &&
            previousVehicleId == vehicleId &&
            string.Equals(previousTrackName, trackName, StringComparison.Ordinal);
        return !sameIdentity || elapsedTime + 1 < previousElapsedTime;
    }

    public static double ParseOptimal(
        JsonElement history,
        int vehicleId,
        IReadOnlySet<string>? excludedLapSignatures)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var best1 = double.PositiveInfinity;
        var best2 = double.PositiveInfinity;
        var best3 = double.PositiveInfinity;
        foreach (var lap in laps.EnumerateArray())
        {
            if (excludedLapSignatures?.Contains(lap.GetRawText()) == true)
            {
                continue;
            }
            if (IsExplicitlyInvalid(lap))
            {
                continue;
            }
            var sector1 = Number(lap, "sectorTime1");
            var sector2Cumulative = Number(lap, "sectorTime2");
            var lapTime = Number(lap, "lapTime");
            if (Valid(sector1)) best1 = Math.Min(best1, sector1);
            var sector2 = sector2Cumulative - sector1;
            if (Valid(sector1) && Valid(sector2Cumulative) && Valid(sector2))
            {
                best2 = Math.Min(best2, sector2);
            }
            var sector3 = lapTime - sector2Cumulative;
            if (Valid(sector2Cumulative) && Valid(lapTime) && Valid(sector3))
            {
                best3 = Math.Min(best3, sector3);
            }
        }

        return double.IsFinite(best1) && double.IsFinite(best2) && double.IsFinite(best3)
            ? best1 + best2 + best3
            : 0;
    }

    private static HashSet<string> CaptureLapSignatures(
        JsonElement history,
        int vehicleId)
    {
        if (history.ValueKind != JsonValueKind.Object ||
            !TryGetVehicleHistory(history, vehicleId, out var laps) ||
            laps.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return laps.EnumerateArray()
            .Select(lap => lap.GetRawText())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsExplicitlyInvalid(JsonElement lap)
    {
        foreach (var property in lap.EnumerateObject())
        {
            if ((property.Name.Equals("valid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapValid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isLapValid", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.False)
            {
                return true;
            }

            if ((property.Name.Equals("invalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("isInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalid", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("lapInvalidated", StringComparison.OrdinalIgnoreCase)) &&
                property.Value.ValueKind is JsonValueKind.True)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetVehicleHistory(
        JsonElement history,
        int vehicleId,
        out JsonElement laps)
    {
        var key = vehicleId.ToString(CultureInfo.InvariantCulture);
        if (history.TryGetProperty(key, out laps))
        {
            return true;
        }

        laps = default;
        return false;
    }

    private static double Number(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.TryGetDouble(out var number))
            {
                return number;
            }
        }
        return 0;
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
