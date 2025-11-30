using System.Text.Json;
using SDHome.Lib.Models;

namespace SDHome.Lib.Data.Entities;

public class SignalEventEntity
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Capability { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EventSubType { get; set; }
    public double? Value { get; set; }
    public string RawTopic { get; set; } = string.Empty;
    public string RawPayload { get; set; } = "{}";
    public string DeviceKind { get; set; } = nameof(Models.DeviceKind.Unknown);
    public string EventCategory { get; set; } = nameof(Models.EventCategory.Telemetry);

    public SignalEvent ToModel()
    {
        using var doc = JsonDocument.Parse(RawPayload);
        var payload = doc.RootElement.Clone();

        return new SignalEvent(
            Id: Id,
            TimestampUtc: TimestampUtc,
            Source: Source,
            DeviceId: DeviceId,
            Location: Location,
            Capability: Capability,
            EventType: EventType,
            EventSubType: EventSubType,
            Value: Value,
            RawTopic: RawTopic,
            RawPayload: payload,
            DeviceKind: Enum.TryParse<DeviceKind>(DeviceKind, out var dk) ? dk : Models.DeviceKind.Unknown,
            EventCategory: Enum.TryParse<EventCategory>(EventCategory, out var ec) ? ec : Models.EventCategory.Telemetry,
            RawPayloadArray: null
        );
    }

    public static SignalEventEntity FromModel(SignalEvent model)
    {
        return new SignalEventEntity
        {
            Id = model.Id,
            TimestampUtc = model.TimestampUtc,
            Source = model.Source,
            DeviceId = model.DeviceId,
            Location = model.Location,
            Capability = model.Capability,
            EventType = model.EventType,
            EventSubType = model.EventSubType,
            Value = model.Value,
            RawTopic = model.RawTopic,
            RawPayload = JsonSerializer.Serialize(model.RawPayload),
            DeviceKind = model.DeviceKind.ToString(),
            EventCategory = model.EventCategory.ToString()
        };
    }
}
