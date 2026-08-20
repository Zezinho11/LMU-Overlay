using System.Globalization;
using System.Diagnostics;
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
public sealed partial class OfficialTimingOptimalProvider : IDisposable
{
    public const int SupportedHistorySchemaVersion = 1;
    private readonly OfficialTimingOptimalOptions _options;
    private readonly HttpClient _client;
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
    private int _consecutiveFailures;
    private long _nextAttemptAt;
    private string _lastError = string.Empty;
    private DateTimeOffset? _lastSuccessAt;



}
