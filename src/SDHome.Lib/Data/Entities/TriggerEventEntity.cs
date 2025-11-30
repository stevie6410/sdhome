using SDHome.Lib.Models;

namespace SDHome.Lib.Data.Entities;

public class TriggerEventEntity
{
    public Guid Id { get; set; }
    public Guid SignalEventId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string? TriggerSubType { get; set; }
    public bool? Value { get; set; }

    public TriggerEvent ToModel()
    {
        return new TriggerEvent(
            Id: Id,
            SignalEventId: SignalEventId,
            TimestampUtc: TimestampUtc,
            DeviceId: DeviceId,
            Capability: Capability,
            TriggerType: TriggerType,
            TriggerSubType: TriggerSubType,
            Value: Value
        );
    }

    public static TriggerEventEntity FromModel(TriggerEvent model)
    {
        return new TriggerEventEntity
        {
            Id = model.Id,
            SignalEventId = model.SignalEventId,
            TimestampUtc = model.TimestampUtc,
            DeviceId = model.DeviceId,
            Capability = model.Capability,
            TriggerType = model.TriggerType,
            TriggerSubType = model.TriggerSubType,
            Value = model.Value
        };
    }
}
