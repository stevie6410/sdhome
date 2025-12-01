using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using SDHome.Lib.Data;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

/// <summary>
/// Represents a device state update from MQTT
/// </summary>
public record DeviceStateQueueItem(
    string DeviceId,
    int? LinkQuality,
    string? State,
    double? Brightness,
    double? ColorTemp,
    double? Temperature,
    double? Humidity,
    double? Battery,
    bool? Contact,
    bool? Occupancy,
    Dictionary<string, object?> RawState);

/// <summary>
/// Background worker that keeps device state synchronized by subscribing to
/// all device state topics and updating the database with full device state.
/// Also performs periodic active polling to request state from all devices.
/// </summary>
public class DeviceStateSyncWorker : BackgroundService
{
    private readonly ILogger<DeviceStateSyncWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _mqttOptions;
    private readonly DeviceStateSyncOptions _syncOptions;
    private IMqttClient? _mqttClient;
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
    private int _messagesReceived = 0;
    private int _devicesUpdated = 0;
    private DateTime _lastPollTime = DateTime.MinValue;
    
    // Queue for processing updates to avoid DbContext threading issues
    private readonly ConcurrentQueue<DeviceStateQueueItem> _updateQueue = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    public DeviceStateSyncWorker(
        ILogger<DeviceStateSyncWorker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> mqttOptions,
        IOptions<DeviceStateSyncOptions> syncOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _mqttOptions = mqttOptions.Value;
        _syncOptions = syncOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_mqttOptions.Enabled)
        {
            PrintStatus("⚠️  MQTT is disabled in configuration - worker will not start", ConsoleColor.Yellow);
            _logger.LogWarning("DeviceStateSyncWorker: MQTT is disabled, worker will not start");
            return;
        }

        PrintStatus("🚀 DeviceStateSyncWorker starting...", ConsoleColor.Cyan);
        if (_syncOptions.PollIntervalSeconds > 0)
        {
            PrintStatus($"   Poll interval: {_syncOptions.PollIntervalSeconds}s", ConsoleColor.DarkGray);
        }
        else
        {
            PrintStatus("   Periodic polling: disabled (real-time only)", ConsoleColor.DarkGray);
        }
        _logger.LogInformation("DeviceStateSyncWorker starting with poll interval {PollInterval}s", _syncOptions.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndSubscribeAsync(stoppingToken);
                
                // Keep running until cancelled or disconnected
                while (_mqttClient?.IsConnected == true && !stoppingToken.IsCancellationRequested)
                {
                    // Process queued updates
                    await ProcessUpdateQueueAsync();
                    
                    // Check if it's time for periodic polling
                    if (_syncOptions.PollIntervalSeconds > 0)
                    {
                        var timeSinceLastPoll = DateTime.UtcNow - _lastPollTime;
                        if (timeSinceLastPoll.TotalSeconds >= _syncOptions.PollIntervalSeconds)
                        {
                            await PollAllDevicesAsync(stoppingToken);
                            _lastPollTime = DateTime.UtcNow;
                        }
                    }
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PrintStatus($"❌ DeviceStateSyncWorker error: {ex.Message}", ConsoleColor.Red);
                PrintStatus($"   Will retry in {_reconnectDelay.TotalSeconds}s...", ConsoleColor.Yellow);
                _logger.LogError(ex, "DeviceStateSyncWorker error, will retry in {Delay}s", _reconnectDelay.TotalSeconds);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_reconnectDelay, stoppingToken);
            }
        }

        PrintStatus("🛑 DeviceStateSyncWorker stopping...", ConsoleColor.Yellow);
        _logger.LogInformation("DeviceStateSyncWorker stopping...");
        
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync();
        }
    }

    private async Task PollAllDevicesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
            
            var devices = await db.Devices
                .AsNoTracking()
                .Select(d => d.FriendlyName ?? d.DeviceId)
                .ToListAsync(stoppingToken);

            if (devices.Count == 0)
            {
                PrintStatus("🔄 Poll: No devices in database", ConsoleColor.DarkGray);
                return;
            }

            PrintStatus($"🔄 Polling {devices.Count} devices for state...", ConsoleColor.Magenta);
            _logger.LogInformation("Polling {Count} devices for state updates", devices.Count);

            var baseTopic = _mqttOptions.BaseTopic.TrimEnd('/');
            var polled = 0;

            foreach (var deviceId in devices)
            {
                if (stoppingToken.IsCancellationRequested) break;
                
                try
                {
                    // Send GET request to device to request its current state
                    var getTopic = $"{baseTopic}/{deviceId}/get";
                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(getTopic)
                        .WithPayload("{\"state\": \"\"}")
                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                        .Build();

                    await _mqttClient!.PublishAsync(message, stoppingToken);
                    polled++;
                    
                    // Small delay between requests to avoid overwhelming the Zigbee network
                    await Task.Delay(50, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to poll device {DeviceId}", deviceId);
                }
            }

            PrintStatus($"✅ Poll complete: requested state from {polled}/{devices.Count} devices", ConsoleColor.Green);
            _logger.LogInformation("Poll complete: requested state from {Polled}/{Total} devices", polled, devices.Count);
        }
        catch (Exception ex)
        {
            PrintStatus($"⚠️  Poll failed: {ex.Message}", ConsoleColor.Red);
            _logger.LogWarning(ex, "Failed to poll devices for state");
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        var clientId = $"SDHomeStateSync-{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..6]}";
        var options = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _mqttClient.DisconnectedAsync += e =>
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                PrintStatus($"⚡ DeviceStateSyncWorker disconnected: {e.Reason}", ConsoleColor.Yellow);
                _logger.LogWarning("DeviceStateSyncWorker disconnected: {Reason}", e.Reason);
            }
            return Task.CompletedTask;
        };

        PrintStatus($"🔌 Connecting to MQTT broker at {_mqttOptions.Host}:{_mqttOptions.Port}...", ConsoleColor.Gray);
        _logger.LogInformation("DeviceStateSyncWorker connecting to MQTT broker at {Host}:{Port}...", 
            _mqttOptions.Host, _mqttOptions.Port);

        await _mqttClient.ConnectAsync(options, stoppingToken);

        // Subscribe to all device state topics (zigbee2mqtt/<device_name>)
        // We use a wildcard but filter out bridge topics in the handler
        var baseTopic = _mqttOptions.BaseTopic.TrimEnd('/');
        var wildcardTopic = $"{baseTopic}/+";  // Single-level wildcard for device names

        await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(wildcardTopic)
            .Build(), stoppingToken);

        PrintStatus($"✅ DeviceStateSyncWorker connected and subscribed to '{wildcardTopic}'", ConsoleColor.Green);
        PrintStatus($"   Client ID: {clientId}", ConsoleColor.DarkGray);
        _logger.LogInformation("DeviceStateSyncWorker subscribed to {Topic}", wildcardTopic);
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var baseTopic = _mqttOptions.BaseTopic.TrimEnd('/');

            // Skip bridge topics and other non-device topics
            if (topic.Contains("/bridge/") || 
                topic.EndsWith("/availability") ||
                topic.EndsWith("/get") ||
                topic.EndsWith("/set"))
            {
                return Task.CompletedTask;
            }

            // Extract device name from topic (zigbee2mqtt/<device_name>)
            var deviceName = topic.Replace($"{baseTopic}/", "");
            
            // Skip if it looks like a nested topic
            if (deviceName.Contains('/'))
            {
                return Task.CompletedTask;
            }

            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            
            // Skip non-JSON payloads
            if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith('{'))
            {
                return Task.CompletedTask;
            }

            _messagesReceived++;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Extract all relevant state properties
            var stateItem = ExtractDeviceState(deviceName, root);
            
            // Queue update if we have any meaningful state
            if (stateItem.LinkQuality.HasValue || 
                stateItem.State != null || 
                stateItem.Battery.HasValue ||
                stateItem.Temperature.HasValue ||
                stateItem.Humidity.HasValue ||
                stateItem.Contact.HasValue ||
                stateItem.Occupancy.HasValue)
            {
                _updateQueue.Enqueue(stateItem);
            }
        }
        catch (JsonException)
        {
            // Ignore non-JSON messages
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing state sync message");
        }
        
        return Task.CompletedTask;
    }

    private static DeviceStateQueueItem ExtractDeviceState(string deviceId, JsonElement root)
    {
        var rawState = new Dictionary<string, object?>();
        
        // Parse all properties into raw state
        foreach (var prop in root.EnumerateObject())
        {
            rawState[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.ToString()
            };
        }

        return new DeviceStateQueueItem(
            DeviceId: deviceId,
            LinkQuality: GetIntValue(root, "linkquality"),
            State: GetStringValue(root, "state"),
            Brightness: GetDoubleValue(root, "brightness"),
            ColorTemp: GetDoubleValue(root, "color_temp"),
            Temperature: GetDoubleValue(root, "temperature"),
            Humidity: GetDoubleValue(root, "humidity"),
            Battery: GetDoubleValue(root, "battery"),
            Contact: GetBoolValue(root, "contact"),
            Occupancy: GetBoolValue(root, "occupancy"),
            RawState: rawState
        );
    }

    private static int? GetIntValue(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Number)
            return element.GetInt32();
        return null;
    }

    private static double? GetDoubleValue(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Number)
            return element.GetDouble();
        return null;
    }

    private static string? GetStringValue(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
            return element.GetString();
        return null;
    }

    private static bool? GetBoolValue(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        return null;
    }

    private async Task ProcessUpdateQueueAsync()
    {
        if (_updateQueue.IsEmpty) return;
        
        // Use semaphore to ensure only one batch processes at a time
        if (!await _processingSemaphore.WaitAsync(0)) return;
        
        try
        {
            // Process all queued updates in a single scope
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
            
            var processed = 0;
            var updated = 0;
            
            while (_updateQueue.TryDequeue(out var update))
            {
                try
                {
                    var device = await db.Devices.FirstOrDefaultAsync(d => 
                        d.DeviceId == update.DeviceId || d.FriendlyName == update.DeviceId);
                    
                    if (device != null)
                    {
                        var hasChanges = false;
                        var changes = new List<string>();
                        
                        // Update LinkQuality
                        if (update.LinkQuality.HasValue && device.LinkQuality != update.LinkQuality.Value)
                        {
                            device.LinkQuality = update.LinkQuality.Value;
                            changes.Add($"LQI:{update.LinkQuality}");
                            hasChanges = true;
                        }
                        
                        // Update attributes JSON with current state values
                        var attributes = !string.IsNullOrEmpty(device.Attributes) 
                            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(device.Attributes) ?? new()
                            : new Dictionary<string, object?>();
                        
                        // Merge raw state into attributes
                        foreach (var kvp in update.RawState)
                        {
                            var oldValue = attributes.TryGetValue(kvp.Key, out var existing) ? existing?.ToString() : null;
                            var newValue = kvp.Value?.ToString();
                            
                            if (oldValue != newValue)
                            {
                                attributes[kvp.Key] = kvp.Value;
                                
                                // Track key changes for logging
                                if (kvp.Key is "state" or "brightness" or "temperature" or "humidity" or "battery" or "contact" or "occupancy")
                                {
                                    changes.Add($"{kvp.Key}:{newValue}");
                                }
                                hasChanges = true;
                            }
                        }
                        
                        if (hasChanges)
                        {
                            device.Attributes = JsonSerializer.Serialize(attributes);
                            device.LastSeen = DateTime.UtcNow;
                            device.IsAvailable = true;
                            updated++;
                            _devicesUpdated++;
                            
                            var changesSummary = changes.Count > 0 ? string.Join(", ", changes.Take(4)) : "attrs";
                            if (changes.Count > 4) changesSummary += "...";
                            
                            PrintStatus($"📡 [{update.DeviceId}] {changesSummary} (msgs: {_messagesReceived}, updates: {_devicesUpdated})", ConsoleColor.Cyan);
                        }
                    }
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error updating device {DeviceId}", update.DeviceId);
                }
            }
            
            if (updated > 0)
            {
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            PrintStatus($"⚠️  Queue processing failed: {ex.Message}", ConsoleColor.Red);
            _logger.LogWarning(ex, "Failed to process update queue");
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private static void PrintStatus(string message, ConsoleColor color)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{timestamp}] ");
        Console.ForegroundColor = color;
        Console.WriteLine($"[StateSync] {message}");
        Console.ResetColor();
    }

    public override void Dispose()
    {
        _mqttClient?.Dispose();
        base.Dispose();
    }
}
