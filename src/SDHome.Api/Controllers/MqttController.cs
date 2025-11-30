using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using SDHome.Lib.Models;

namespace SDHome.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MqttController(
    IOptions<MqttOptions> mqttOptions,
    ILogger<MqttController> logger) : ControllerBase
{
    private readonly MqttOptions _mqttOptions = mqttOptions.Value;

    /// <summary>
    /// Test MQTT connection
    /// </summary>
    [HttpGet("test")]
    public async Task<ActionResult> TestConnection()
    {
        if (!_mqttOptions.Enabled)
        {
            return BadRequest(new { error = "MQTT is disabled in configuration" });
        }

        try
        {
            var factory = new MqttClientFactory();
            using var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("SDHomeTest-" + Guid.NewGuid().ToString()[..8])
                .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                .WithCleanSession(true)
                .WithTimeout(TimeSpan.FromSeconds(5))
                .Build();

            logger.LogInformation("Testing MQTT connection to {Host}:{Port}...", _mqttOptions.Host, _mqttOptions.Port);
            
            var connectResult = await mqttClient.ConnectAsync(options);
            
            await mqttClient.DisconnectAsync();

            return Ok(new
            {
                success = connectResult.ResultCode == MqttClientConnectResultCode.Success,
                resultCode = connectResult.ResultCode.ToString(),
                host = _mqttOptions.Host,
                port = _mqttOptions.Port,
                message = connectResult.ResultCode == MqttClientConnectResultCode.Success 
                    ? "Successfully connected to MQTT broker" 
                    : $"Connection failed: {connectResult.ResultCode}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MQTT connection test failed");
            return Ok(new
            {
                success = false,
                error = ex.Message,
                host = _mqttOptions.Host,
                port = _mqttOptions.Port
            });
        }
    }

    /// <summary>
    /// Publish a message to an MQTT topic
    /// </summary>
    [HttpPost("publish")]
    public async Task<ActionResult> Publish([FromBody] MqttPublishRequest request)
    {
        if (!_mqttOptions.Enabled)
        {
            return BadRequest(new { error = "MQTT is disabled in configuration" });
        }

        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return BadRequest(new { error = "Topic is required" });
        }

        try
        {
            var factory = new MqttClientFactory();
            using var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId("SDHomeConsole-" + Guid.NewGuid().ToString()[..8])
                .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
                .WithCleanSession(true)
                .WithTimeout(TimeSpan.FromSeconds(5))
                .Build();

            logger.LogInformation("Connecting to MQTT broker at {Host}:{Port} for publish...", _mqttOptions.Host, _mqttOptions.Port);
            
            var connectResult = await mqttClient.ConnectAsync(options);
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                logger.LogError("Failed to connect to MQTT broker. Result: {ResultCode}", connectResult.ResultCode);
                return StatusCode(500, new { error = $"Failed to connect to MQTT broker: {connectResult.ResultCode}" });
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(request.Topic)
                .WithPayload(request.Payload ?? "")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(request.Retain)
                .Build();

            await mqttClient.PublishAsync(message);
            
            logger.LogInformation("Published message to topic {Topic}: {Payload}", request.Topic, request.Payload ?? "(empty)");

            await mqttClient.DisconnectAsync();

            return Ok(new { 
                success = true, 
                topic = request.Topic, 
                payloadLength = request.Payload?.Length ?? 0,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing to MQTT topic {Topic}", request.Topic);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get MQTT connection status
    /// </summary>
    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            enabled = _mqttOptions.Enabled,
            host = _mqttOptions.Host,
            port = _mqttOptions.Port
        });
    }
}

/// <summary>
/// Request to publish an MQTT message
/// </summary>
public class MqttPublishRequest
{
    /// <summary>
    /// The MQTT topic to publish to
    /// </summary>
    public string Topic { get; set; } = string.Empty;
    
    /// <summary>
    /// The message payload (JSON string or plain text)
    /// </summary>
    public string? Payload { get; set; }
    
    /// <summary>
    /// Whether to set the retain flag on the message
    /// </summary>
    public bool Retain { get; set; } = false;
}
