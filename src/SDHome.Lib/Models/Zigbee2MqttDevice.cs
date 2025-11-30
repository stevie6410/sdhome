using System.Text.Json;
using System.Text.Json.Serialization;

namespace SDHome.Lib.Models;

public class Zigbee2MqttDevice
{
    public string ieee_address { get; set; } = string.Empty;
    public string friendly_name { get; set; } = string.Empty;
    public string? type { get; set; }  // "Coordinator", "Router", "EndDevice"
    public string? model_id { get; set; }
    public string? manufacturer { get; set; }
    public string? description { get; set; }
    public string? power_source { get; set; }
    public Definition? definition { get; set; }
    public bool disabled { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class Definition
{
    public string? model { get; set; }
    public string? vendor { get; set; }
    public string? description { get; set; }
    public List<Expose>? exposes { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class Expose
{
    public string? type { get; set; }
    public string? name { get; set; }
    public string? property { get; set; }
    public int? access { get; set; }  // Bitmask: 1=read, 2=write, 4=publish
    public string? unit { get; set; }
    public string? description { get; set; }
    public List<Expose>? features { get; set; }  // Nested features for composite types
    public JsonElement? values { get; set; }  // Enum values
    public double? value_min { get; set; }
    public double? value_max { get; set; }
    public double? value_step { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
