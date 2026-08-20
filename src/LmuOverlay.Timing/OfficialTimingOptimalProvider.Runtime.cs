using System.Globalization;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed partial class OfficialTimingOptimalProvider
{
    public OfficialTimingOptimalProvider(OfficialTimingOptimalOptions? options = null)
    {
        _options = (options ?? new()).Sanitize();
        _client = new HttpClient
        {
            BaseAddress = _options.BaseAddress,
            Timeout = _options.Timeout,
        };
        _worker = _options.Enabled ? Task.Run(PollAsync) : Task.CompletedTask;
    }

    public void Update(LmuTelemetrySnapshot snapshot)
    {
        if (!_options.Enabled)
        {
            return;
        }

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

    public OfficialTimingOptimalDiagnostics Diagnostics => new(
        _options.Enabled,
        Volatile.Read(ref _consecutiveFailures),
        Volatile.Read(ref _nextAttemptAt) > Stopwatch.GetTimestamp(),
        _lastSuccessAt,
        Volatile.Read(ref _lastError));

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
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false))
            {
                var target = Volatile.Read(ref _target);
                if (target is null)
                {
                    continue;
                }
                if (Volatile.Read(ref _nextAttemptAt) > Stopwatch.GetTimestamp())
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
                    if (!HasSupportedSchema(document.RootElement, target.VehicleId))
                    {
                        RecordFailure("Unsupported or incomplete LMU timing history schema.");
                        continue;
                    }
                    if (target.DiscardExistingHistory &&
                        target.ExcludedLapSignatures is null)
                    {
                        target.ExcludedLapSignatures = CaptureLapSignatures(
                            document.RootElement,
                            target.VehicleId);
                        RecordSuccess();
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
                    RecordSuccess();
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or JsonException)
                {
                    RecordFailure(exception.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
