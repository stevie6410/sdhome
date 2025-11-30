using System.Text.Json;
using SDHome.Lib.Data;
using SDHome.Lib.Data.Entities;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

public class SignalEventProjectionService(
    SignalsDbContext db,
    IRealtimeEventBroadcaster broadcaster) : ISignalEventProjectionService
{
    public async Task<ProjectedEventData> ProjectAsync(SignalEvent ev, CancellationToken cancellationToken = default)
    {
        // Example: handle motion sensor events
        if (ev.Capability == "motion" && ev.EventType == "detection")
        {
            return await HandleMotionEventAsync(ev, cancellationToken);
        }

        // Later: handle other capabilities (contact, button, temp-only devices, etc.)
        return new ProjectedEventData(null, []);
    }

    private async Task<ProjectedEventData> HandleMotionEventAsync(SignalEvent ev, CancellationToken ct)
    {
        var payload = ev.RawPayload;

        bool? occupancy = TryGetBool(payload, "occupancy");
        bool isActive = string.Equals(ev.EventSubType, "active", StringComparison.OrdinalIgnoreCase);

        var trigger = new TriggerEvent(
            Id: Guid.NewGuid(),
            SignalEventId: ev.Id,
            TimestampUtc: ev.TimestampUtc,
            DeviceId: ev.DeviceId,
            Capability: ev.Capability,
            TriggerType: "motion",
            TriggerSubType: ev.EventSubType,
            Value: occupancy ?? isActive
        );

        db.TriggerEvents.Add(TriggerEventEntity.FromModel(trigger));

        var readings = new List<SensorReading>();

        if (TryGetDouble(payload, "device_temperature", out var temp))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "temperature",
                Value: temp,
                Unit: "°C"
            ));
        }

        if (TryGetDouble(payload, "illuminance", out var lux))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "illuminance",
                Value: lux,
                Unit: "lx"
            ));
        }

        if (TryGetDouble(payload, "battery", out var battery))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "battery",
                Value: battery,
                Unit: "%"
            ));
        }

        if (TryGetDouble(payload, "linkquality", out var lqi))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "linkquality",
                Value: lqi,
                Unit: null
            ));
        }

        if (TryGetDouble(payload, "voltage", out var voltage))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "voltage",
                Value: voltage / 1000.0,
                Unit: "V"
            ));
        }

        if (readings.Count > 0)
        {
            db.SensorReadings.AddRange(readings.Select(SensorReadingEntity.FromModel));
        }

        await db.SaveChangesAsync(ct);

        // Broadcast to real-time clients
        await broadcaster.BroadcastTriggerEventAsync(trigger);
        foreach (var reading in readings)
        {
            await broadcaster.BroadcastSensorReadingAsync(reading);
        }

        return new ProjectedEventData(trigger, readings);
    }

    private static bool? TryGetBool(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return null;

        if (payload.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }

        return null;
    }

    private static bool TryGetDouble(JsonElement payload, string name, out double value)
    {
        value = default;

        if (payload.ValueKind != JsonValueKind.Object)
            return false;

        if (!payload.TryGetProperty(name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value))
            return true;

        return false;
    }
}

