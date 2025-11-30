using SDHome.Lib.Models;

namespace SDHome.Lib.Data.Entities;

public class SensorReadingEntity
{
    public Guid Id { get; set; }
    public Guid SignalEventId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }

    public SensorReading ToModel()
    {
        return new SensorReading(
            Id: Id,
            SignalEventId: SignalEventId,
            TimestampUtc: TimestampUtc,
            DeviceId: DeviceId,
            Metric: Metric,
            Value: Value,
            Unit: Unit
        );
    }

    public static SensorReadingEntity FromModel(SensorReading model)
    {
        return new SensorReadingEntity
        {
            Id = model.Id,
            SignalEventId = model.SignalEventId,
            TimestampUtc = model.TimestampUtc,
            DeviceId = model.DeviceId,
            Metric = model.Metric,
            Value = model.Value,
            Unit = model.Unit
        };
    }
}
