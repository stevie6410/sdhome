using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using SDHome.Lib.Data;
using SDHome.Lib.Data.Entities;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

public class DeviceService(
    SignalsDbContext db,
    ILogger<DeviceService> logger,
    IOptions<MqttOptions> mqttOptions,
    IMqttClient? mqttClient = null) : IDeviceService
{
    private readonly MqttOptions _mqttOptions = mqttOptions.Value;

    public async Task<IEnumerable<Device>> GetAllDevicesAsync()
    {
        return await db.Devices
            .AsNoTracking()
            .OrderBy(d => d.FriendlyName)
            .Select(d => d.ToModel())
            .ToListAsync();
    }

    public async Task<Device?> GetDeviceAsync(string deviceId)
    {
        var entity = await db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        
        return entity?.ToModel();
    }

    public async Task<Device> UpdateDeviceAsync(Device device)
    {
        var entity = await db.Devices.FindAsync(device.DeviceId)
            ?? throw new InvalidOperationException($"Device {device.DeviceId} not found");

        entity.FriendlyName = device.FriendlyName;
        entity.IeeeAddress = device.IeeeAddress;
        entity.ModelId = device.ModelId;
        entity.Manufacturer = device.Manufacturer;
        entity.Description = device.Description;
        entity.PowerSource = device.PowerSource;
        entity.DeviceType = device.DeviceType?.ToString();
        entity.Room = device.Room;
        entity.Capabilities = JsonSerializer.Serialize(device.Capabilities);
        entity.Attributes = JsonSerializer.Serialize(device.Attributes);
        entity.LastSeen = device.LastSeen;
        entity.IsAvailable = device.IsAvailable;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return device;
    }

    public async Task<IEnumerable<Device>> GetDevicesByRoomAsync(string room)
    {
        return await db.Devices
            .AsNoTracking()
            .Where(d => d.Room == room)
            .OrderBy(d => d.FriendlyName)
            .Select(d => d.ToModel())
            .ToListAsync();
    }

    public async Task<IEnumerable<Device>> GetDevicesByTypeAsync(DeviceType deviceType)
    {
        var deviceTypeString = deviceType.ToString();
        return await db.Devices
            .AsNoTracking()
            .Where(d => d.DeviceType == deviceTypeString)
            .OrderBy(d => d.FriendlyName)
            .Select(d => d.ToModel())
            .ToListAsync();
    }

    public async Task SyncDevicesFromZigbee2MqttAsync()
    {
        if (!_mqttOptions.Enabled)
        {
            logger.LogWarning("MQTT is disabled in configuration. Cannot sync devices from Zigbee2MQTT.");
            return;
        }

        if (mqttClient == null)
        {
            logger.LogError("MQTT client is not available. Cannot sync devices from Zigbee2MQTT.");
            throw new InvalidOperationException("MQTT client is not configured");
        }

        try
        {
            logger.LogInformation("Starting device sync from Zigbee2MQTT");

            if (!mqttClient.IsConnected)
            {
                var options = new MqttClientOptionsBuilder()
                    .WithClientId("SDHomeDeviceSync-" + Guid.NewGuid())
                    .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                    .WithCleanSession()
                    .Build();

                await mqttClient.ConnectAsync(options);
                logger.LogInformation("Connected to MQTT broker for device sync");
            }

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter("zigbee2mqtt/bridge/devices")
                .Build();

            var deviceListReceived = new TaskCompletionSource<List<Zigbee2MqttDevice>>();

            mqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                if (e.ApplicationMessage.Topic == "zigbee2mqtt/bridge/devices")
                {
                    try
                    {
                        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                        var devices = JsonSerializer.Deserialize<List<Zigbee2MqttDevice>>(payload);
                        
                        if (devices != null)
                        {
                            deviceListReceived.TrySetResult(devices);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error parsing Zigbee2MQTT device list");
                        deviceListReceived.TrySetException(ex);
                    }
                }
                
                await Task.CompletedTask;
            };

            await mqttClient.SubscribeAsync(subscribeOptions);

            var requestMessage = new MqttApplicationMessageBuilder()
                .WithTopic("zigbee2mqtt/bridge/request/devices")
                .WithPayload("{}")
                .Build();

            await mqttClient.PublishAsync(requestMessage);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(deviceListReceived.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                logger.LogWarning("Timeout waiting for Zigbee2MQTT device list");
                return;
            }

            var zigbeeDevices = await deviceListReceived.Task;
            logger.LogInformation("Received {Count} devices from Zigbee2MQTT", zigbeeDevices.Count);

            foreach (var zigbeeDevice in zigbeeDevices)
            {
                try
                {
                    var device = MapZigbeeDeviceToDevice(zigbeeDevice);
                    
                    var existing = await db.Devices.FindAsync(device.DeviceId);
                    if (existing == null)
                    {
                        var entity = DeviceEntity.FromModel(device);
                        entity.CreatedAt = DateTime.UtcNow;
                        entity.UpdatedAt = DateTime.UtcNow;
                        db.Devices.Add(entity);
                        logger.LogInformation("Created new device: {DeviceId}", device.DeviceId);
                    }
                    else
                    {
                        existing.FriendlyName = device.FriendlyName;
                        existing.IeeeAddress = device.IeeeAddress;
                        existing.ModelId = device.ModelId;
                        existing.Manufacturer = device.Manufacturer;
                        existing.Description = device.Description;
                        existing.Capabilities = JsonSerializer.Serialize(device.Capabilities);
                        existing.IsAvailable = device.IsAvailable;
                        existing.LastSeen = DateTime.UtcNow;
                        existing.UpdatedAt = DateTime.UtcNow;
                        logger.LogInformation("Updated device: {DeviceId}", device.DeviceId);
                    }

                    await db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing device {FriendlyName}", zigbeeDevice.friendly_name);
                }
            }

            logger.LogInformation("Device sync completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing devices from Zigbee2MQTT");
            throw;
        }
    }

    private static Device MapZigbeeDeviceToDevice(Zigbee2MqttDevice zigbeeDevice)
    {
        var device = new Device
        {
            DeviceId = zigbeeDevice.friendly_name,
            FriendlyName = zigbeeDevice.friendly_name,
            IeeeAddress = zigbeeDevice.ieee_address,
            ModelId = zigbeeDevice.model_id,
            Manufacturer = zigbeeDevice.manufacturer ?? zigbeeDevice.definition?.vendor,
            Description = zigbeeDevice.description ?? zigbeeDevice.definition?.description,
            PowerSource = zigbeeDevice.power_source != "Battery",
            IsAvailable = !zigbeeDevice.disabled,
            LastSeen = DateTime.UtcNow
        };

        if (zigbeeDevice.definition?.exposes != null)
        {
            device.Capabilities = zigbeeDevice.definition.exposes
                .Where(e => !string.IsNullOrEmpty(e.property))
                .Select(e => e.property!)
                .Distinct()
                .ToList();

            device.DeviceType = InferDeviceType(device.Capabilities);
        }

        return device;
    }

    private static DeviceType InferDeviceType(List<string> capabilities)
    {
        if (capabilities.Contains("state") && (capabilities.Contains("brightness") || capabilities.Contains("color")))
            return DeviceType.Light;
        
        if (capabilities.Contains("state") && !capabilities.Contains("brightness"))
            return DeviceType.Switch;
        
        if (capabilities.Contains("temperature") || capabilities.Contains("humidity") || capabilities.Contains("occupancy"))
            return DeviceType.Sensor;
        
        if (capabilities.Contains("local_temperature") || capabilities.Contains("system_mode"))
            return DeviceType.Climate;
        
        if (capabilities.Contains("lock_state"))
            return DeviceType.Lock;
        
        if (capabilities.Contains("position"))
            return DeviceType.Cover;

        return DeviceType.Other;
    }
}
