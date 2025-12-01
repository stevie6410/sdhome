namespace SDHome.Lib.Models;

/// <summary>
/// Represents the complete Zigbee network topology
/// </summary>
public class ZigbeeNetworkMap
{
    public List<ZigbeeNode> Nodes { get; set; } = new();
    public List<ZigbeeLink> Links { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A node in the Zigbee network (coordinator, router, or end device)
/// </summary>
public class ZigbeeNode
{
    /// <summary>
    /// IEEE address (unique identifier)
    /// </summary>
    public string IeeeAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Friendly name from Zigbee2MQTT
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Network address (NWK)
    /// </summary>
    public int NetworkAddress { get; set; }
    
    /// <summary>
    /// Device type: Coordinator, Router, or EndDevice
    /// </summary>
    public ZigbeeDeviceType Type { get; set; }
    
    /// <summary>
    /// Device manufacturer
    /// </summary>
    public string? Manufacturer { get; set; }
    
    /// <summary>
    /// Device model
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Model ID for images
    /// </summary>
    public string? ModelId { get; set; }
    
    /// <summary>
    /// Whether the device is mains powered
    /// </summary>
    public bool MainsPowered { get; set; }
    
    /// <summary>
    /// Last known link quality (0-255)
    /// </summary>
    public int? LinkQuality { get; set; }
    
    /// <summary>
    /// Last seen timestamp
    /// </summary>
    public DateTime? LastSeen { get; set; }
    
    /// <summary>
    /// Device image URL
    /// </summary>
    public string? ImageUrl => !string.IsNullOrEmpty(ModelId) 
        ? $"https://www.zigbee2mqtt.io/images/devices/{Uri.EscapeDataString(ModelId.Replace(' ', '-'))}.png" 
        : null;
    
    /// <summary>
    /// X position for layout (optional, for saved layouts)
    /// </summary>
    public double? X { get; set; }
    
    /// <summary>
    /// Y position for layout (optional, for saved layouts)
    /// </summary>
    public double? Y { get; set; }
}

/// <summary>
/// A link between two Zigbee nodes
/// </summary>
public class ZigbeeLink
{
    /// <summary>
    /// Source node IEEE address
    /// </summary>
    public string SourceIeeeAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Target node IEEE address
    /// </summary>
    public string TargetIeeeAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// Link quality indicator (0-255)
    /// </summary>
    public int LinkQuality { get; set; }
    
    /// <summary>
    /// Depth in the network (hops from coordinator)
    /// </summary>
    public int? Depth { get; set; }
    
    /// <summary>
    /// Relationship type
    /// </summary>
    public string? Relationship { get; set; }
}

/// <summary>
/// Type of Zigbee device
/// </summary>
public enum ZigbeeDeviceType
{
    Coordinator,
    Router,
    EndDevice
}
