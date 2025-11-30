using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

public class SignalsMqttWorker : BackgroundService
{
    private readonly ILogger<SignalsMqttWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _mqttOptions;

    public SignalsMqttWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> mqttOptions,
        ILogger<SignalsMqttWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _mqttOptions = mqttOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_mqttOptions.Enabled)
        {
            _logger.LogInformation("MQTT is disabled in configuration. Skipping MQTT connection.");
            return;
        }

        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId("SDHomeSignals-" + Guid.NewGuid())
            .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
            .WithCleanSession()
            .Build();

        client.ConnectedAsync += _ =>
        {
            _logger.LogInformation("✅ Connected to MQTT broker {Host}:{Port}", _mqttOptions.Host, _mqttOptions.Port);
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += _ =>
        {
            _logger.LogWarning("⚠️ Disconnected from MQTT broker.");
            return Task.CompletedTask;
        };

        client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            // Handle Zigbee2MQTT bridge events for device pairing
            if (await HandleBridgeEventAsync(topic, payload))
            {
                return; // Bridge event handled, don't process as regular signal
            }

            // Create a new scope for each message to get scoped services (EF Core DbContext)
            using var scope = _scopeFactory.CreateScope();
            var signalsService = scope.ServiceProvider.GetRequiredService<ISignalsService>();
            await signalsService.HandleMqttMessageAsync(topic, payload, stoppingToken);
        };

        await client.ConnectAsync(options, stoppingToken);

        // Subscribe to device signals
        var baseTopic = _mqttOptions.BaseTopic.TrimEnd('/');
        var bridgeEventTopic = $"{baseTopic}/bridge/event";
        var bridgeResponseTopic = $"{baseTopic}/bridge/response/permit_join";
        
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(_mqttOptions.TopicFilter))
            // Subscribe to Zigbee2MQTT bridge events for pairing
            .WithTopicFilter(f => f.WithTopic(bridgeEventTopic))
            .WithTopicFilter(f => f.WithTopic(bridgeResponseTopic))
            .Build();

        await client.SubscribeAsync(subscribeOptions, stoppingToken);
        _logger.LogInformation("🔔 Subscribed to MQTT topic filter {TopicFilter} and bridge events ({BridgeEvent}, {BridgeResponse})", 
            _mqttOptions.TopicFilter, bridgeEventTopic, bridgeResponseTopic);
    }

    /// <summary>
    /// Handle Zigbee2MQTT bridge events for device pairing
    /// </summary>
    private async Task<bool> HandleBridgeEventAsync(string topic, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var baseTopic = _mqttOptions.BaseTopic.TrimEnd('/');
        var bridgeEventTopic = $"{baseTopic}/bridge/event";
        var bridgeResponseTopic = $"{baseTopic}/bridge/response/permit_join";

        try
        {
            // Get the broadcaster from scope
            using var scope = _scopeFactory.CreateScope();
            var broadcaster = scope.ServiceProvider.GetRequiredService<IRealtimeEventBroadcaster>();

            // Handle bridge events
            if (topic == bridgeEventTopic)
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeElement))
                    return false;

                var eventType = typeElement.GetString();

                switch (eventType)
                {
                    case "device_joined":
                        // A new device has started joining
                        if (root.TryGetProperty("data", out var joinData))
                        {
                            var ieeeAddress = joinData.TryGetProperty("ieee_address", out var ieee) ? ieee.GetString() : null;
                            var friendlyName = joinData.TryGetProperty("friendly_name", out var fn) ? fn.GetString() : null;

                            if (!string.IsNullOrEmpty(ieeeAddress))
                            {
                                _logger.LogInformation("🆕 Device joining: {IeeeAddress} ({FriendlyName})", ieeeAddress, friendlyName);
                                await DeviceService.HandleDeviceJoinEventAsync(ieeeAddress, friendlyName, broadcaster, _logger);
                            }
                        }
                        return true;

                    case "device_interview":
                        // Device interview progress
                        if (root.TryGetProperty("data", out var interviewData))
                        {
                            var ieeeAddress = interviewData.TryGetProperty("ieee_address", out var ieee) ? ieee.GetString() : null;
                            var friendlyName = interviewData.TryGetProperty("friendly_name", out var fn) ? fn.GetString() : null;
                            var status = interviewData.TryGetProperty("status", out var st) ? st.GetString() : null;

                            if (!string.IsNullOrEmpty(ieeeAddress) && !string.IsNullOrEmpty(status))
                            {
                                _logger.LogInformation("📋 Device interview {Status}: {IeeeAddress} ({FriendlyName})", status, ieeeAddress, friendlyName);
                                await DeviceService.HandleDeviceInterviewEventAsync(ieeeAddress, friendlyName, status, broadcaster, _logger);
                            }
                        }
                        return true;

                    case "device_announce":
                        // Device announced itself
                        _logger.LogDebug("📢 Device announce received");
                        return true;
                }
            }
            else if (topic == bridgeResponseTopic)
            {
                // Permit join response
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    var value = data.TryGetProperty("value", out var v) && v.GetBoolean();
                    var time = data.TryGetProperty("time", out var t) ? t.GetInt32() : 0;
                    _logger.LogInformation("🔓 Permit join: {Value}, time: {Time}s", value, time);
                }
                return true;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse bridge event payload: {Payload}", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling bridge event on {Topic}", topic);
        }

        return false;
    }
}
