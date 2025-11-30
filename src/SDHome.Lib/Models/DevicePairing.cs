using System.Text.Json.Serialization;

namespace SDHome.Lib.Models;

/// <summary>
/// Progress update for device pairing operations
/// </summary>
public class DevicePairingProgress
{
    public string PairingId { get; set; } = string.Empty;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DevicePairingStatus Status { get; set; }
    
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Remaining seconds until pairing mode ends
    /// </summary>
    public int RemainingSeconds { get; set; }
    
    /// <summary>
    /// Total duration of pairing mode in seconds
    /// </summary>
    public int TotalDuration { get; set; }
    
    /// <summary>
    /// Whether permit join is currently active
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Device currently being interviewed (if any)
    /// </summary>
    public DevicePairingDevice? CurrentDevice { get; set; }
    
    /// <summary>
    /// All devices discovered during this pairing session
    /// </summary>
    public List<DevicePairingDevice> DiscoveredDevices { get; set; } = [];
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string? Error { get; set; }
}

public enum DevicePairingStatus
{
    /// <summary>
    /// Pairing mode is starting
    /// </summary>
    Starting,
    
    /// <summary>
    /// Pairing mode is active and waiting for devices
    /// </summary>
    Active,
    
    /// <summary>
    /// A new device is being interviewed
    /// </summary>
    Interviewing,
    
    /// <summary>
    /// A device was successfully paired
    /// </summary>
    DevicePaired,
    
    /// <summary>
    /// Countdown timer tick (periodic update)
    /// </summary>
    CountdownTick,
    
    /// <summary>
    /// Pairing mode is stopping
    /// </summary>
    Stopping,
    
    /// <summary>
    /// Pairing mode has ended (timeout or manual stop)
    /// </summary>
    Ended,
    
    /// <summary>
    /// Pairing failed
    /// </summary>
    Failed
}

/// <summary>
/// Device info during pairing
/// </summary>
public class DevicePairingDevice
{
    public string IeeeAddress { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? DeviceType { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DevicePairingDeviceStatus Status { get; set; }
    
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
}

public enum DevicePairingDeviceStatus
{
    Discovered,
    Interviewing,
    Configuring,
    Ready,
    Failed
}

/// <summary>
/// Request to start pairing mode
/// </summary>
public class StartPairingRequest
{
    /// <summary>
    /// Duration in seconds (default 120, max 254)
    /// </summary>
    public int Duration { get; set; } = 120;
}

/// <summary>
/// Zigbee2MQTT permit_join response
/// </summary>
public class Zigbee2MqttPermitJoinResponse
{
    public bool Value { get; set; }
    public int? Time { get; set; }
}

/// <summary>
/// Zigbee2MQTT bridge event for device joining
/// </summary>
public class Zigbee2MqttDeviceJoinEvent
{
    public string? FriendlyName { get; set; }
    public string? IeeeAddress { get; set; }
}

/// <summary>
/// Zigbee2MQTT bridge event for device interview
/// </summary>
public class Zigbee2MqttDeviceInterviewEvent
{
    public string? FriendlyName { get; set; }
    public string? IeeeAddress { get; set; }
    public string? Status { get; set; } // "started", "successful", "failed"
    public bool? Supported { get; set; }
}
