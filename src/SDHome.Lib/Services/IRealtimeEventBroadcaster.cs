using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

/// <summary>
/// Represents a real-time device state update
/// </summary>
public class DeviceStateUpdate
{
    public string DeviceId { get; set; } = string.Empty;
    public Dictionary<string, object?> State { get; set; } = new();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an automation execution log entry for real-time monitoring
/// </summary>
public class AutomationLogEntry
{
    public Guid AutomationId { get; set; }
    public string AutomationName { get; set; } = string.Empty;
    public AutomationLogLevel Level { get; set; }
    public AutomationLogPhase Phase { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object?>? Details { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Duration of this phase in milliseconds (optional)
    /// </summary>
    public double? DurationMs { get; set; }
}

/// <summary>
/// Represents a complete pipeline execution timeline for visualization
/// </summary>
public class PipelineTimeline
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string DeviceId { get; set; } = string.Empty;
    public string? AutomationName { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double TotalMs { get; set; }
    public List<PipelineStage> Stages { get; set; } = new();
}

/// <summary>
/// A single stage in the pipeline timeline
/// </summary>
public class PipelineStage
{
    public string Name { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public double StartOffsetMs { get; set; }
    public string Category { get; set; } = "other"; // signal, db, broadcast, automation, mqtt
    public bool IsSuccess { get; set; } = true;
}

/// <summary>
/// Log level for automation events
/// </summary>
public enum AutomationLogLevel
{
    Debug,
    Info,
    Warning,
    Success,
    Error
}

/// <summary>
/// Phase of automation execution
/// </summary>
public enum AutomationLogPhase
{
    TriggerReceived,
    TriggerEvaluating,
    TriggerMatched,
    TriggerSkipped,
    ConditionEvaluating,
    ConditionPassed,
    ConditionFailed,
    CooldownActive,
    ActionExecuting,
    ActionCompleted,
    ActionFailed,
    ExecutionCompleted,
    ExecutionFailed
}

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
    Task BroadcastDeviceStateUpdateAsync(DeviceStateUpdate stateUpdate);
    Task BroadcastAutomationLogAsync(AutomationLogEntry logEntry);
    Task BroadcastPipelineTimelineAsync(PipelineTimeline timeline);
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
    public Task BroadcastDeviceStateUpdateAsync(DeviceStateUpdate stateUpdate) => Task.CompletedTask;
    public Task BroadcastAutomationLogAsync(AutomationLogEntry logEntry) => Task.CompletedTask;
    public Task BroadcastPipelineTimelineAsync(PipelineTimeline timeline) => Task.CompletedTask;
}
