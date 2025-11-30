using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

public record ProjectedEventData(
    TriggerEvent? Trigger,
    IReadOnlyList<SensorReading> Readings
);

public interface ISignalEventProjectionService
{
    Task<ProjectedEventData> ProjectAsync(SignalEvent ev, CancellationToken cancellationToken = default);
}
