using System.Globalization;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using LmuOverlay.Domain;

namespace LmuOverlay.Widgets;

public sealed record OfficialTimingOptimalOptions
{
    public bool Enabled { get; init; } = true;
    public Uri BaseAddress { get; init; } = new("http://127.0.0.1:6397/");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumBackoff { get; init; } = TimeSpan.FromSeconds(30);

    public OfficialTimingOptimalOptions Sanitize()
    {
        var loopback = BaseAddress.IsLoopback &&
            BaseAddress.Scheme == Uri.UriSchemeHttp
            ? BaseAddress
            : new Uri("http://127.0.0.1:6397/");
        return this with
        {
            BaseAddress = loopback,
            Timeout = TimeSpan.FromMilliseconds(Math.Clamp(Timeout.TotalMilliseconds, 100, 2_000)),
            PollInterval = TimeSpan.FromMilliseconds(Math.Clamp(PollInterval.TotalMilliseconds, 250, 5_000)),
            InitialBackoff = TimeSpan.FromMilliseconds(Math.Clamp(InitialBackoff.TotalMilliseconds, 250, 5_000)),
            MaximumBackoff = TimeSpan.FromSeconds(Math.Clamp(MaximumBackoff.TotalSeconds, 1, 120)),
        };
    }
}

public sealed record OfficialTimingOptimalDiagnostics(
    bool Enabled,
    int ConsecutiveFailures,
    bool CircuitOpen,
    DateTimeOffset? LastSuccessAt,
    string LastError);
