using System.Text.Json.Serialization;

namespace SDHome.Lib.Models;

/// <summary>
/// Progress update for device sync operations
/// </summary>
public class DeviceSyncProgress
{
    public string SyncId { get; set; } = string.Empty;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceSyncStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DevicesFound { get; set; }
    public int DevicesProcessed { get; set; }
    public int DevicesTotal { get; set; }
    public DeviceSyncDevice? CurrentDevice { get; set; }
    public List<DeviceSyncDevice> DiscoveredDevices { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}

public enum DeviceSyncStatus
{
    Started,
    Connecting,
    Subscribing,
    WaitingForDevices,
    DeviceReceived,
    Processing,
    DeviceProcessed,
    Completed,
    Failed
}

/// <summary>
/// Minimal device info for sync progress updates
/// </summary>
public class DeviceSyncDevice
{
    public string DeviceId { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? DeviceType { get; set; }
    public bool IsNew { get; set; }
    public bool IsRemoved { get; set; }
}
