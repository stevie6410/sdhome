using Microsoft.AspNetCore.SignalR;

namespace SDHome.Api.Hubs;

/// <summary>
/// SignalR hub for real-time signal events streaming to clients
/// </summary>
public class SignalsHub : Hub
{
    private readonly ILogger<SignalsHub> _logger;

    public SignalsHub(ILogger<SignalsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to specific device events
    /// </summary>
    public async Task SubscribeToDevice(string deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to device {DeviceId}", Context.ConnectionId, deviceId);
    }

    /// <summary>
    /// Unsubscribe from specific device events
    /// </summary>
    public async Task UnsubscribeFromDevice(string deviceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"device:{deviceId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from device {DeviceId}", Context.ConnectionId, deviceId);
    }

    /// <summary>
    /// Subscribe to device sync progress updates
    /// </summary>
    public async Task SubscribeToDeviceSync(string syncId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"sync:{syncId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to sync {SyncId}", Context.ConnectionId, syncId);
    }

    /// <summary>
    /// Unsubscribe from device sync progress updates
    /// </summary>
    public async Task UnsubscribeFromDeviceSync(string syncId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sync:{syncId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from sync {SyncId}", Context.ConnectionId, syncId);
    }

    /// <summary>
    /// Subscribe to device pairing progress updates
    /// </summary>
    public async Task SubscribeToPairing(string pairingId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"pairing:{pairingId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to pairing {PairingId}", Context.ConnectionId, pairingId);
    }

    /// <summary>
    /// Unsubscribe from device pairing progress updates
    /// </summary>
    public async Task UnsubscribeFromPairing(string pairingId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"pairing:{pairingId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from pairing {PairingId}", Context.ConnectionId, pairingId);
    }

    /// <summary>
    /// Subscribe to automation log events for a specific automation
    /// </summary>
    public async Task SubscribeToAutomation(string automationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"automation:{automationId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to automation {AutomationId}", Context.ConnectionId, automationId);
    }

    /// <summary>
    /// Unsubscribe from automation log events
    /// </summary>
    public async Task UnsubscribeFromAutomation(string automationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"automation:{automationId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from automation {AutomationId}", Context.ConnectionId, automationId);
    }

    /// <summary>
    /// Subscribe to all automation log events
    /// </summary>
    public async Task SubscribeToAllAutomations()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "automations:all");
        _logger.LogInformation("Client {ConnectionId} subscribed to all automations", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from all automation log events
    /// </summary>
    public async Task UnsubscribeFromAllAutomations()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "automations:all");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from all automations", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to pipeline timeline events for latency monitoring
    /// </summary>
    public async Task SubscribeToPipelineTimelines()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "pipelines:all");
        _logger.LogInformation("Client {ConnectionId} subscribed to pipeline timelines", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from pipeline timeline events
    /// </summary>
    public async Task UnsubscribeFromPipelineTimelines()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "pipelines:all");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from pipeline timelines", Context.ConnectionId);
    }
}
