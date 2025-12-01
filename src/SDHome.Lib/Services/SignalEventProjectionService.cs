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
        // Handle motion sensor events
        if (ev.Capability == "motion" && ev.EventType == "detection")
        {
            return await HandleMotionEventAsync(ev, cancellationToken);
        }

        // Handle button presses (triggers)
        if (ev.Capability == "button" && ev.EventType == "press")
        {
            return await HandleButtonEventAsync(ev, cancellationToken);
        }

        // Handle temperature sensors (readings)
        if (ev.Capability == "temperature" && ev.EventType == "measurement")
        {
            return await HandleTemperatureEventAsync(ev, cancellationToken);
        }

        // Handle contact sensors (triggers)
        if (ev.Capability == "contact")
        {
            return await HandleContactEventAsync(ev, cancellationToken);
        }

        // Handle generic sensor readings from any device with numeric values
        return await HandleGenericSensorReadingsAsync(ev, cancellationToken);
    }

    private async Task<ProjectedEventData> HandleButtonEventAsync(SignalEvent ev, CancellationToken ct)
    {
        var payload = ev.RawPayload;

        var trigger = new TriggerEvent(
            Id: Guid.NewGuid(),
            SignalEventId: ev.Id,
            TimestampUtc: ev.TimestampUtc,
            DeviceId: ev.DeviceId,
            Capability: ev.Capability,
            TriggerType: "button",
            TriggerSubType: ev.EventSubType, // e.g., "single", "double", "hold"
            Value: true
        );

        db.TriggerEvents.Add(TriggerEventEntity.FromModel(trigger));

        // Also extract any sensor readings from the button (battery, linkquality, etc.)
        var readings = ExtractCommonSensorReadings(ev);

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

    private async Task<ProjectedEventData> HandleTemperatureEventAsync(SignalEvent ev, CancellationToken ct)
    {
        var payload = ev.RawPayload;
        var readings = new List<SensorReading>();

        // Primary temperature reading
        if (ev.Value.HasValue)
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "temperature",
                Value: ev.Value.Value,
                Unit: "°C"
            ));
        }

        // Humidity (often paired with temperature)
        if (TryGetDouble(payload, "humidity", out var humidity))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "humidity",
                Value: humidity,
                Unit: "%"
            ));
        }

        // Pressure (some sensors include this)
        if (TryGetDouble(payload, "pressure", out var pressure))
        {
            readings.Add(new SensorReading(
                Id: Guid.NewGuid(),
                SignalEventId: ev.Id,
                TimestampUtc: ev.TimestampUtc,
                DeviceId: ev.DeviceId,
                Metric: "pressure",
                Value: pressure,
                Unit: "hPa"
            ));
        }

        // Common sensor readings (battery, linkquality, voltage)
        readings.AddRange(ExtractCommonSensorReadings(ev));

        if (readings.Count > 0)
        {
            db.SensorReadings.AddRange(readings.Select(SensorReadingEntity.FromModel));
        }

        await db.SaveChangesAsync(ct);

        // Broadcast to real-time clients
        foreach (var reading in readings)
        {
            await broadcaster.BroadcastSensorReadingAsync(reading);
        }

        return new ProjectedEventData(null, readings);
    }

    private async Task<ProjectedEventData> HandleContactEventAsync(SignalEvent ev, CancellationToken ct)
    {
        var payload = ev.RawPayload;

        bool? contact = TryGetBool(payload, "contact");

        var trigger = new TriggerEvent(
            Id: Guid.NewGuid(),
            SignalEventId: ev.Id,
            TimestampUtc: ev.TimestampUtc,
            DeviceId: ev.DeviceId,
            Capability: ev.Capability,
            TriggerType: "contact",
            TriggerSubType: contact == true ? "closed" : "open",
            Value: contact ?? false
        );

        db.TriggerEvents.Add(TriggerEventEntity.FromModel(trigger));

        var readings = ExtractCommonSensorReadings(ev);

        if (readings.Count > 0)
        {
            db.SensorReadings.AddRange(readings.Select(SensorReadingEntity.FromModel));
        }

        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastTriggerEventAsync(trigger);
        foreach (var reading in readings)
        {
            await broadcaster.BroadcastSensorReadingAsync(reading);
        }

        return new ProjectedEventData(trigger, readings);
    }

    private async Task<ProjectedEventData> HandleGenericSensorReadingsAsync(SignalEvent ev, CancellationToken ct)
    {
        // For any other event, try to extract common sensor readings
        var readings = ExtractCommonSensorReadings(ev);

        // Also try to extract any state-based trigger (e.g., on/off switches)
        TriggerEvent? trigger = null;
        var payload = ev.RawPayload;

        if (payload.ValueKind == JsonValueKind.Object)
        {
            // Check for state property (common in switches/lights)
            if (payload.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String)
            {
                var state = stateProp.GetString();
                if (state == "ON" || state == "OFF")
                {
                    trigger = new TriggerEvent(
                        Id: Guid.NewGuid(),
                        SignalEventId: ev.Id,
                        TimestampUtc: ev.TimestampUtc,
                        DeviceId: ev.DeviceId,
                        Capability: "switch",
                        TriggerType: "state",
                        TriggerSubType: state?.ToLower(),
                        Value: state == "ON"
                    );

                    db.TriggerEvents.Add(TriggerEventEntity.FromModel(trigger));
                }
            }

            // Check for brightness changes (also a trigger for automation purposes)
            if (TryGetDouble(payload, "brightness", out var brightness))
            {
                readings.Add(new SensorReading(
                    Id: Guid.NewGuid(),
                    SignalEventId: ev.Id,
                    TimestampUtc: ev.TimestampUtc,
                    DeviceId: ev.DeviceId,
                    Metric: "brightness",
                    Value: brightness,
                    Unit: null
                ));
            }

            // Check for power consumption
            if (TryGetDouble(payload, "power", out var power))
            {
                readings.Add(new SensorReading(
                    Id: Guid.NewGuid(),
                    SignalEventId: ev.Id,
                    TimestampUtc: ev.TimestampUtc,
                    DeviceId: ev.DeviceId,
                    Metric: "power",
                    Value: power,
                    Unit: "W"
                ));
            }

            // Check for energy consumption
            if (TryGetDouble(payload, "energy", out var energy))
            {
                readings.Add(new SensorReading(
                    Id: Guid.NewGuid(),
                    SignalEventId: ev.Id,
                    TimestampUtc: ev.TimestampUtc,
                    DeviceId: ev.DeviceId,
                    Metric: "energy",
                    Value: energy,
                    Unit: "kWh"
                ));
            }
        }

        if (readings.Count > 0)
        {
            db.SensorReadings.AddRange(readings.Select(SensorReadingEntity.FromModel));
        }

        if (trigger != null || readings.Count > 0)
        {
            await db.SaveChangesAsync(ct);

            if (trigger != null)
            {
                await broadcaster.BroadcastTriggerEventAsync(trigger);
            }

            foreach (var reading in readings)
            {
                await broadcaster.BroadcastSensorReadingAsync(reading);
            }
        }

        return new ProjectedEventData(trigger, readings);
    }

    /// <summary>
    /// Extracts common sensor readings present in most Zigbee device payloads
    /// </summary>
    private List<SensorReading> ExtractCommonSensorReadings(SignalEvent ev)
    {
        var payload = ev.RawPayload;
        var readings = new List<SensorReading>();

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

        return readings;
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

        // Motion sensors often have temperature
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

        // Motion sensors often have illuminance
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

        // Common sensor readings (battery, linkquality, voltage)
        readings.AddRange(ExtractCommonSensorReadings(ev));

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

