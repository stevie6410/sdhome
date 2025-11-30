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

            // Create a new scope for each message to get scoped services (EF Core DbContext)
            using var scope = _scopeFactory.CreateScope();
            var signalsService = scope.ServiceProvider.GetRequiredService<ISignalsService>();
            await signalsService.HandleMqttMessageAsync(topic, payload, stoppingToken);
        };

        await client.ConnectAsync(options, stoppingToken);

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(_mqttOptions.TopicFilter))
            .Build();

        await client.SubscribeAsync(subscribeOptions, stoppingToken);
        _logger.LogInformation("🔔 Subscribed to MQTT topic filter {TopicFilter}", _mqttOptions.TopicFilter);
    }
}
