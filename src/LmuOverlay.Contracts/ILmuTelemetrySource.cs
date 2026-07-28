using LmuOverlay.Domain;

namespace LmuOverlay.Contracts;

public interface ILmuTelemetrySource : IDisposable
{
    LmuProbeSnapshot ReadProbeSnapshot();

    LmuTelemetrySnapshot ReadTelemetrySnapshot();
}
