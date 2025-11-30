using Microsoft.AspNetCore.SignalR;
using SDHome.Api.Hubs;
using SDHome.Lib.Models;

namespace SDHome.Api.Services;

/// <summary>
/// Service to broadcast signal events to connected SignalR clients
/// </summary>
public interface ISignalBroadcaster
{
    Task BroadcastSignalEventAsync(SignalEvent signalEvent);
    Task BroadcastSensorReadingAsync(SensorReading reading);
    Task BroadcastTriggerEventAsync(TriggerEvent trigger);
}

public class SignalBroadcaster : ISignalBroadcaster
{
    private readonly IHubContext<SignalsHub> _hubContext;
    private readonly ILogger<SignalBroadcaster> _logger;

    public SignalBroadcaster(IHubContext<SignalsHub> hubContext, ILogger<SignalBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastSignalEventAsync(SignalEvent signalEvent)
    {
        try
        {
            // Broadcast to all connected clients
            await _hubContext.Clients.All.SendAsync("SignalReceived", signalEvent);

            // Also broadcast to device-specific group
            if (!string.IsNullOrEmpty(signalEvent.DeviceId))
            {
                await _hubContext.Clients.Group($"device:{signalEvent.DeviceId}")
                    .SendAsync("DeviceSignalReceived", signalEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting signal event");
        }
    }

    public async Task BroadcastSensorReadingAsync(SensorReading reading)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReadingReceived", reading);

            if (!string.IsNullOrEmpty(reading.DeviceId))
            {
                await _hubContext.Clients.Group($"device:{reading.DeviceId}")
                    .SendAsync("DeviceReadingReceived", reading);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting sensor reading");
        }
    }

    public async Task BroadcastTriggerEventAsync(TriggerEvent trigger)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TriggerReceived", trigger);

            if (!string.IsNullOrEmpty(trigger.DeviceId))
            {
                await _hubContext.Clients.Group($"device:{trigger.DeviceId}")
                    .SendAsync("DeviceTriggerReceived", trigger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting trigger event");
        }
    }
}
