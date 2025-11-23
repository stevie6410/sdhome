using global::SDHome.Web.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MQTTnet;
using SDHome.Web.Models;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace SDHome.Web.Services
{
    public class SignalsMqttWorker : BackgroundService
    {
        private readonly Microsoft.Extensions.Logging.ILogger _log = Log.ForContext<SignalsMqttWorker>();
        private readonly ISignalsService _signalsService;
        private readonly MqttOptions _mqttOptions;

        public SignalsMqttWorker(
            ISignalsService signalsService,
            IOptions<MqttOptions> mqttOptions)
        {
            _signalsService = signalsService;
            _mqttOptions = mqttOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new MqttClientFactory();
            var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("SDHomeSignals-" + Guid.NewGuid())
                .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                .WithCleanSession()
                .Build();

            client.ConnectedAsync += _ =>
            {
                _log.Information("✅ Connected to MQTT broker {Host}:{Port}", _mqttOptions.Host, _mqttOptions.Port);
                return Task.CompletedTask;
            };

            client.DisconnectedAsync += _ =>
            {
                _log.Warning("⚠️ Disconnected from MQTT broker.");
                return Task.CompletedTask;
            };

            client.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = e.ApplicationMessage.ConvertPayloadToString();

                await _signalsService.HandleMqttMessageAsync(topic, payload, stoppingToken);
            };

            await client.ConnectAsync(options, stoppingToken);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(_mqttOptions.TopicFilter))
                .Build();

            await client.SubscribeAsync(subscribeOptions, stoppingToken);
            _log.Information("🔔 Subscribed to MQTT topic filter {TopicFilter}", _mqttOptions.TopicFilter);
        }
    }
}
