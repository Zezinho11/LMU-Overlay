using LmuOverlay.Domain;

namespace LmuOverlay.Contracts;

public interface ILmuTelemetrySource : IDisposable
{
    LmuProbeSnapshot ReadProbeSnapshot();

    LmuTelemetrySnapshot ReadTelemetrySnapshot();
}

public enum TelemetryUpdateWaitResult
{
    Signaled,
    TimedOut,
    Cancelled,
}

public interface IWaitableTelemetrySource
{
    TelemetryUpdateWaitResult WaitForUpdate(
        WaitHandle cancellation,
        TimeSpan timeout);
}
