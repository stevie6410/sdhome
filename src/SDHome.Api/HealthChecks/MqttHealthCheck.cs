using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MQTTnet;
using SDHome.Lib.Models;

namespace SDHome.Api.HealthChecks;

public class MqttHealthCheck(IOptions<MqttOptions> mqttOptions) : IHealthCheck
{
    private readonly MqttOptions _mqttOptions = mqttOptions.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new MqttClientFactory();
            using var client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("HealthCheck-" + Guid.NewGuid())
                .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                .WithCleanSession()
                .WithTimeout(TimeSpan.FromSeconds(5))
                .Build();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await client.ConnectAsync(options, cts.Token);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                await client.DisconnectAsync(cancellationToken: cts.Token);
                return HealthCheckResult.Healthy($"Connected to MQTT broker at {_mqttOptions.Host}:{_mqttOptions.Port}");
            }

            return HealthCheckResult.Unhealthy($"Failed to connect to MQTT broker: {result.ResultCode}");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("MQTT health check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MQTT broker connection failed: {ex.Message}");
        }
    }
}
