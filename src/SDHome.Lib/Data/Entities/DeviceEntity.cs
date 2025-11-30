using System.Text.Json;
using SDHome.Lib.Models;

namespace SDHome.Lib.Data.Entities;

public class DeviceEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string? IeeeAddress { get; set; }
    public string? ModelId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Description { get; set; }
    public bool PowerSource { get; set; }
    public string? DeviceType { get; set; }
    public string? Room { get; set; }
    public string Capabilities { get; set; } = "[]";
    public string Attributes { get; set; } = "{}";
    public DateTime? LastSeen { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Device ToModel()
    {
        return new Device
        {
            DeviceId = DeviceId,
            FriendlyName = FriendlyName,
            IeeeAddress = IeeeAddress,
            ModelId = ModelId,
            Manufacturer = Manufacturer,
            Description = Description,
            PowerSource = PowerSource,
            DeviceType = !string.IsNullOrEmpty(DeviceType) 
                ? Enum.Parse<Models.DeviceType>(DeviceType) 
                : null,
            Room = Room,
            Capabilities = JsonSerializer.Deserialize<List<string>>(Capabilities) ?? [],
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(Attributes) ?? [],
            LastSeen = LastSeen,
            IsAvailable = IsAvailable,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public static DeviceEntity FromModel(Device model)
    {
        return new DeviceEntity
        {
            DeviceId = model.DeviceId,
            FriendlyName = model.FriendlyName,
            IeeeAddress = model.IeeeAddress,
            ModelId = model.ModelId,
            Manufacturer = model.Manufacturer,
            Description = model.Description,
            PowerSource = model.PowerSource,
            DeviceType = model.DeviceType?.ToString(),
            Room = model.Room,
            Capabilities = JsonSerializer.Serialize(model.Capabilities),
            Attributes = JsonSerializer.Serialize(model.Attributes),
            LastSeen = model.LastSeen,
            IsAvailable = model.IsAvailable,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
