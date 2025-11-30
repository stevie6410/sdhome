using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

/// <summary>
/// Interface for broadcasting real-time events to connected clients.
/// Implemented in the API layer (SignalR), but called from the Lib layer.
/// </summary>
public interface IRealtimeEventBroadcaster
{
    Task BroadcastSignalEventAsync(SignalEvent signalEvent);
    Task BroadcastSensorReadingAsync(SensorReading reading);
    Task BroadcastTriggerEventAsync(TriggerEvent trigger);
    Task BroadcastDeviceSyncProgressAsync(DeviceSyncProgress progress);
    Task BroadcastDevicePairingProgressAsync(DevicePairingProgress progress);
}

/// <summary>
/// No-op implementation when real-time broadcasting is not available
/// </summary>
public class NullRealtimeEventBroadcaster : IRealtimeEventBroadcaster
{
    public Task BroadcastSignalEventAsync(SignalEvent signalEvent) => Task.CompletedTask;
    public Task BroadcastSensorReadingAsync(SensorReading reading) => Task.CompletedTask;
    public Task BroadcastTriggerEventAsync(TriggerEvent trigger) => Task.CompletedTask;
    public Task BroadcastDeviceSyncProgressAsync(DeviceSyncProgress progress) => Task.CompletedTask;
    public Task BroadcastDevicePairingProgressAsync(DevicePairingProgress progress) => Task.CompletedTask;
}
