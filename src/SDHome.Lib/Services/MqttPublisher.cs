using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

/// <summary>
/// Interface for publishing MQTT messages with a persistent connection
/// </summary>
public interface IMqttPublisher
{
    Task PublishAsync(string topic, object payload);
    Task PublishAsync(string topic, string payload);
    bool IsConnected { get; }
}

/// <summary>
/// Singleton MQTT publisher that maintains a persistent connection for fast publishing
/// </summary>
public class MqttPublisher : BackgroundService, IMqttPublisher
{
    private readonly ILogger<MqttPublisher> _logger;
    private readonly MqttOptions _mqttOptions;
    private IMqttClient? _client;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _isConnecting;

    public MqttPublisher(
        IOptions<MqttOptions> mqttOptions,
        ILogger<MqttPublisher> logger)
    {
        _mqttOptions = mqttOptions.Value;
        _logger = logger;
    }

    public bool IsConnected => _client?.IsConnected == true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_mqttOptions.Enabled)
        {
            _logger.LogInformation("MQTT Publisher disabled - MQTT not enabled in configuration");
            return;
        }

        await EnsureConnectedAsync();
        
        // Keep alive - reconnect if disconnected
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("MQTT Publisher disconnected, reconnecting...");
                await EnsureConnectedAsync();
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (IsConnected || _isConnecting) return;

        await _connectionLock.WaitAsync();
        try
        {
            if (IsConnected) return;
            _isConnecting = true;

            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("SDHomePublisher-" + Environment.MachineName)
                .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                .WithCleanSession(false) // Maintain session for faster reconnects
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .Build();

            _client.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT Publisher disconnected: {Reason}", e.ReasonString);
                if (!e.ClientWasConnected)
                {
                    // Connection failed, wait before retry
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            };

            var connectStart = Stopwatch.GetTimestamp();
            var result = await _client.ConnectAsync(options);
            var connectTime = Stopwatch.GetElapsedTime(connectStart);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation("✅ MQTT Publisher connected to {Host}:{Port} in {ConnectMs:F1}ms",
                    _mqttOptions.Host, _mqttOptions.Port, connectTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("❌ MQTT Publisher failed to connect: {ResultCode}", result.ResultCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting MQTT Publisher");
        }
        finally
        {
            _isConnecting = false;
            _connectionLock.Release();
        }
    }

    public async Task PublishAsync(string topic, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        await PublishAsync(topic, json);
    }

    public async Task PublishAsync(string topic, string payload)
    {
        if (!_mqttOptions.Enabled)
        {
            throw new InvalidOperationException("MQTT is disabled in configuration");
        }

        await EnsureConnectedAsync();

        if (_client == null || !_client.IsConnected)
        {
            throw new InvalidOperationException("MQTT Publisher is not connected");
        }

        var publishStart = Stopwatch.GetTimestamp();

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(message);

        var publishTime = Stopwatch.GetElapsedTime(publishStart);
        _logger.LogDebug("⚡ Published to {Topic} in {PublishMs:F1}ms", topic, publishTime.TotalMilliseconds);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client?.IsConnected == true)
        {
            await _client.DisconnectAsync();
            _logger.LogInformation("MQTT Publisher disconnected");
        }
        
        await base.StopAsync(cancellationToken);
    }
}
