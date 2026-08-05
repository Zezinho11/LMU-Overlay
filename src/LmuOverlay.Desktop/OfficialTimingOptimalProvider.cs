using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Desktop;

/// <summary>
/// Reads the same read-only standings history endpoint used by LMU's own
/// Live Timing UI. The endpoint contains the complete sector history required
/// for a true theoretical optimal; shared memory exposes only two best-sector
/// accumulators and cannot reconstruct an earlier best sector three.
/// </summary>
internal sealed class OfficialTimingOptimalProvider : IDisposable
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

    public OfficialTimingOptimalProvider() => _worker = Task.Run(PollAsync);

    public void Update(LmuTelemetrySnapshot snapshot)
    {
        if (snapshot.Player is not { } player || snapshot.Session is not { } session)
        {
            Volatile.Write(ref _target, null);
            Volatile.Write(ref _value, null);
            return;
        }

        var target = new TimingTarget(
            $"{session.TrackName}\u001f{session.SessionCode}\u001f{player.VehicleId}",
            player.VehicleId);
        var previous = Volatile.Read(ref _target);
        if (previous?.SessionKey != target.SessionKey)
        {
            Volatile.Write(ref _value, null);
        }
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
                    var optimal = ParseOptimal(document.RootElement, target.VehicleId);
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

    internal static double ParseOptimal(JsonElement history, int vehicleId)
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

    private sealed record TimingTarget(string SessionKey, int VehicleId);
    private sealed record TimingValue(string SessionKey, double OptimalLapSeconds);
}
