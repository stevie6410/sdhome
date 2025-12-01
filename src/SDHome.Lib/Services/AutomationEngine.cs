using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SDHome.Lib.Data;
using SDHome.Lib.Data.Entities;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

/// <summary>
/// Background service that listens to device state changes and executes matching automations
/// </summary>
public class AutomationEngine : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationEngine> _logger;
    private readonly IRealtimeEventBroadcaster _broadcaster;
    private readonly HttpClient _httpClient;
    
    // Cache of device states for condition evaluation
    private readonly Dictionary<string, Dictionary<string, object?>> _deviceStates = new();
    private readonly object _stateLock = new();

    public AutomationEngine(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationEngine> logger,
        IRealtimeEventBroadcaster broadcaster,
        HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _broadcaster = broadcaster;
        _httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automation Engine started");
        
        // Initial load of device states
        await LoadDeviceStatesAsync();
        
        // The engine primarily reacts to device state changes via ProcessDeviceStateChangeAsync
        // This loop handles time-based triggers
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimeTriggersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing time triggers");
            }
            
            // Check time triggers every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        
        _logger.LogInformation("Automation Engine stopped");
    }

    /// <summary>
    /// Called when a device state changes - evaluates and executes matching automations
    /// </summary>
    public async Task ProcessDeviceStateChangeAsync(
        string deviceId, 
        string property, 
        object? oldValue, 
        object? newValue)
    {
        _logger.LogDebug("Processing device state change: {DeviceId}.{Property} = {Value}", 
            deviceId, property, newValue);
        
        // Update cached state
        lock (_stateLock)
        {
            if (!_deviceStates.TryGetValue(deviceId, out var state))
            {
                state = new Dictionary<string, object?>();
                _deviceStates[deviceId] = state;
            }
            state[property] = newValue;
        }
        
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        // Find matching rules
        var rules = await automationService.GetRulesForDeviceTriggerAsync(deviceId, property);
        
        foreach (var rule in rules)
        {
            try
            {
                await EvaluateAndExecuteRuleAsync(rule, deviceId, property, oldValue, newValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing automation rule: {RuleName}", rule.Name);
            }
        }
    }

    /// <summary>
    /// Process time-based triggers
    /// </summary>
    private async Task ProcessTimeTriggersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        var rules = await automationService.GetTimeTriggerRulesAsync();
        var now = DateTime.Now; // Use local time for scheduling
        
        foreach (var rule in rules)
        {
            if (ct.IsCancellationRequested) break;
            
            var timeTriggers = rule.Triggers.Where(t => t.TriggerType == "Time");
            
            foreach (var trigger in timeTriggers)
            {
                if (ShouldFireTimeTrigger(trigger, now))
                {
                    _logger.LogInformation("Time trigger fired for rule: {RuleName}", rule.Name);
                    await EvaluateAndExecuteRuleAsync(rule, null, null, null, null);
                }
            }
        }
    }

    /// <summary>
    /// Check if a time trigger should fire
    /// </summary>
    private static bool ShouldFireTimeTrigger(AutomationTriggerEntity trigger, DateTime now)
    {
        if (string.IsNullOrEmpty(trigger.TimeExpression)) return false;
        
        // Simple time matching (HH:mm format)
        if (TimeOnly.TryParse(trigger.TimeExpression, out var targetTime))
        {
            var currentTime = TimeOnly.FromDateTime(now);
            // Allow 30-second window for matching
            var diff = Math.Abs((currentTime.ToTimeSpan() - targetTime.ToTimeSpan()).TotalSeconds);
            return diff <= 30;
        }
        
        // TODO: Add cron expression support
        return false;
    }

    /// <summary>
    /// Evaluate conditions and execute a rule
    /// </summary>
    private async Task EvaluateAndExecuteRuleAsync(
        AutomationRuleEntity rule,
        string? triggerDeviceId,
        string? triggerProperty,
        object? oldValue,
        object? newValue)
    {
        var sw = Stopwatch.StartNew();
        
        // Check cooldown
        if (rule.LastTriggeredAt.HasValue && rule.CooldownSeconds > 0)
        {
            var timeSinceLastTrigger = DateTime.UtcNow - rule.LastTriggeredAt.Value;
            if (timeSinceLastTrigger.TotalSeconds < rule.CooldownSeconds)
            {
                _logger.LogDebug("Rule {RuleName} skipped due to cooldown ({Remaining}s remaining)", 
                    rule.Name, rule.CooldownSeconds - timeSinceLastTrigger.TotalSeconds);
                return;
            }
        }
        
        // Evaluate trigger conditions (for device state triggers)
        if (triggerDeviceId != null)
        {
            var matchingTriggers = rule.Triggers
                .Where(t => t.DeviceId == triggerDeviceId && 
                           (t.Property == triggerProperty || t.Property == null))
                .ToList();
            
            var anyTriggerMatches = matchingTriggers.Any(t => EvaluateTrigger(t, oldValue, newValue));
            
            if (!anyTriggerMatches)
            {
                _logger.LogDebug("Rule {RuleName} skipped - trigger condition not met", rule.Name);
                return;
            }
        }
        
        // Evaluate conditions
        var conditionsPassed = EvaluateConditions(rule);
        if (!conditionsPassed)
        {
            _logger.LogDebug("Rule {RuleName} skipped - conditions not met", rule.Name);
            await LogExecutionAsync(rule, ExecutionStatus.SkippedCondition, sw.ElapsedMilliseconds, 
                triggerDeviceId, triggerProperty, "Conditions not met");
            return;
        }
        
        _logger.LogInformation("Executing automation rule: {RuleName}", rule.Name);
        
        // Execute actions
        var actionResults = new List<AutomationActionResult>();
        var hasFailure = false;
        
        foreach (var action in rule.Actions.OrderBy(a => a.SortOrder))
        {
            var actionSw = Stopwatch.StartNew();
            try
            {
                await ExecuteActionAsync(action);
                actionResults.Add(new AutomationActionResult(action.Id, true, null, (int)actionSw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                hasFailure = true;
                actionResults.Add(new AutomationActionResult(action.Id, false, ex.Message, (int)actionSw.ElapsedMilliseconds));
                _logger.LogError(ex, "Action {ActionType} failed in rule {RuleName}", action.ActionType, rule.Name);
            }
        }
        
        sw.Stop();
        
        // Update rule stats
        await UpdateRuleStatsAsync(rule.Id);
        
        // Log execution
        var status = hasFailure 
            ? (actionResults.Any(a => a.Success) ? ExecutionStatus.PartialFailure : ExecutionStatus.Failure)
            : ExecutionStatus.Success;
        
        await LogExecutionAsync(rule, status, sw.ElapsedMilliseconds, 
            triggerDeviceId, triggerProperty, null, actionResults);
        
        _logger.LogInformation("Automation rule {RuleName} executed in {Duration}ms - Status: {Status}", 
            rule.Name, sw.ElapsedMilliseconds, status);
    }

    /// <summary>
    /// Evaluate a single trigger
    /// </summary>
    private static bool EvaluateTrigger(AutomationTriggerEntity trigger, object? oldValue, object? newValue)
    {
        if (string.IsNullOrEmpty(trigger.Operator)) return true; // No operator = any change
        
        var op = Enum.Parse<ComparisonOperator>(trigger.Operator, true);
        var targetValue = string.IsNullOrEmpty(trigger.Value) ? null : JsonSerializer.Deserialize<object>(trigger.Value);
        
        return op switch
        {
            ComparisonOperator.AnyChange => !Equals(oldValue, newValue),
            ComparisonOperator.ChangesTo => Equals(newValue, targetValue),
            ComparisonOperator.ChangesFrom => Equals(oldValue, targetValue),
            ComparisonOperator.Equals => Equals(newValue, targetValue),
            ComparisonOperator.NotEquals => !Equals(newValue, targetValue),
            _ => CompareValues(newValue, targetValue, op)
        };
    }

    /// <summary>
    /// Evaluate all conditions for a rule
    /// </summary>
    private bool EvaluateConditions(AutomationRuleEntity rule)
    {
        if (rule.Conditions.Count == 0) return true;
        
        var conditionMode = Enum.Parse<ConditionMode>(rule.ConditionMode, true);
        var results = rule.Conditions.Select(c => EvaluateCondition(c)).ToList();
        
        return conditionMode == ConditionMode.All 
            ? results.All(r => r) 
            : results.Any(r => r);
    }

    /// <summary>
    /// Evaluate a single condition
    /// </summary>
    private bool EvaluateCondition(AutomationConditionEntity condition)
    {
        var condType = Enum.Parse<ConditionType>(condition.ConditionType, true);
        
        return condType switch
        {
            ConditionType.DeviceState => EvaluateDeviceStateCondition(condition),
            ConditionType.TimeRange => EvaluateTimeRangeCondition(condition),
            ConditionType.DayOfWeek => EvaluateDayOfWeekCondition(condition),
            _ => true
        };
    }

    private bool EvaluateDeviceStateCondition(AutomationConditionEntity condition)
    {
        if (string.IsNullOrEmpty(condition.DeviceId) || string.IsNullOrEmpty(condition.Property))
            return true;
        
        object? currentValue = null;
        lock (_stateLock)
        {
            if (_deviceStates.TryGetValue(condition.DeviceId, out var state))
            {
                state.TryGetValue(condition.Property, out currentValue);
            }
        }
        
        if (currentValue == null) return false;
        
        var targetValue = string.IsNullOrEmpty(condition.Value) 
            ? null 
            : JsonSerializer.Deserialize<object>(condition.Value);
        
        var op = string.IsNullOrEmpty(condition.Operator) 
            ? ComparisonOperator.Equals 
            : Enum.Parse<ComparisonOperator>(condition.Operator, true);
        
        return CompareValues(currentValue, targetValue, op);
    }

    private static bool EvaluateTimeRangeCondition(AutomationConditionEntity condition)
    {
        if (string.IsNullOrEmpty(condition.TimeStart) || string.IsNullOrEmpty(condition.TimeEnd))
            return true;
        
        if (!TimeOnly.TryParse(condition.TimeStart, out var start) ||
            !TimeOnly.TryParse(condition.TimeEnd, out var end))
            return true;
        
        var now = TimeOnly.FromDateTime(DateTime.Now);
        
        // Handle overnight ranges (e.g., 22:00 - 06:00)
        if (end < start)
        {
            return now >= start || now <= end;
        }
        
        return now >= start && now <= end;
    }

    private static bool EvaluateDayOfWeekCondition(AutomationConditionEntity condition)
    {
        if (string.IsNullOrEmpty(condition.DaysOfWeek)) return true;
        
        try
        {
            var days = JsonSerializer.Deserialize<List<int>>(condition.DaysOfWeek);
            if (days == null || days.Count == 0) return true;
            
            var today = (int)DateTime.Now.DayOfWeek;
            return days.Contains(today);
        }
        catch
        {
            return true;
        }
    }

    private static bool CompareValues(object? actual, object? target, ComparisonOperator op)
    {
        if (actual == null || target == null) return false;
        
        // Try numeric comparison
        if (double.TryParse(actual.ToString(), out var actualNum) &&
            double.TryParse(target.ToString(), out var targetNum))
        {
            return op switch
            {
                ComparisonOperator.Equals => Math.Abs(actualNum - targetNum) < 0.001,
                ComparisonOperator.NotEquals => Math.Abs(actualNum - targetNum) >= 0.001,
                ComparisonOperator.GreaterThan => actualNum > targetNum,
                ComparisonOperator.GreaterThanOrEqual => actualNum >= targetNum,
                ComparisonOperator.LessThan => actualNum < targetNum,
                ComparisonOperator.LessThanOrEqual => actualNum <= targetNum,
                _ => false
            };
        }
        
        // String comparison
        var actualStr = actual.ToString() ?? "";
        var targetStr = target.ToString() ?? "";
        
        return op switch
        {
            ComparisonOperator.Equals => actualStr.Equals(targetStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.NotEquals => !actualStr.Equals(targetStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Contains => actualStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.StartsWith => actualStr.StartsWith(targetStr, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.EndsWith => actualStr.EndsWith(targetStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>
    /// Execute a single action
    /// </summary>
    private async Task ExecuteActionAsync(AutomationActionEntity action)
    {
        var actionType = Enum.Parse<ActionType>(action.ActionType, true);
        
        _logger.LogDebug("Executing action: {ActionType}", actionType);
        
        switch (actionType)
        {
            case ActionType.SetDeviceState:
                await ExecuteSetDeviceStateAsync(action);
                break;
            
            case ActionType.ToggleDevice:
                await ExecuteToggleDeviceAsync(action);
                break;
            
            case ActionType.Delay:
                if (action.DelaySeconds.HasValue && action.DelaySeconds.Value > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(action.DelaySeconds.Value));
                }
                break;
            
            case ActionType.Webhook:
                await ExecuteWebhookAsync(action);
                break;
            
            case ActionType.ActivateScene:
                if (action.SceneId.HasValue)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                    await automationService.ActivateSceneAsync(action.SceneId.Value);
                }
                break;
            
            case ActionType.Notification:
                // TODO: Implement notification service
                _logger.LogInformation("Notification: {Title} - {Message}", 
                    action.NotificationTitle, action.NotificationMessage);
                break;
            
            default:
                _logger.LogWarning("Unknown action type: {ActionType}", actionType);
                break;
        }
    }

    private async Task ExecuteSetDeviceStateAsync(AutomationActionEntity action)
    {
        if (string.IsNullOrEmpty(action.DeviceId) || string.IsNullOrEmpty(action.Property))
            return;
        
        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var value = string.IsNullOrEmpty(action.Value) 
            ? null 
            : JsonSerializer.Deserialize<object>(action.Value);
        
        var state = new Dictionary<string, object> { { action.Property, value! } };
        await deviceService.SetDeviceStateAsync(action.DeviceId, state);
    }

    private async Task ExecuteToggleDeviceAsync(AutomationActionEntity action)
    {
        if (string.IsNullOrEmpty(action.DeviceId) || string.IsNullOrEmpty(action.Property))
            return;
        
        // Get current state
        object? currentValue = null;
        lock (_stateLock)
        {
            if (_deviceStates.TryGetValue(action.DeviceId, out var deviceState))
            {
                deviceState.TryGetValue(action.Property, out currentValue);
            }
        }
        
        // Toggle boolean or "ON"/"OFF" string
        object newValue;
        if (currentValue is bool boolVal)
        {
            newValue = !boolVal;
        }
        else if (currentValue?.ToString()?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true)
        {
            newValue = "OFF";
        }
        else
        {
            newValue = "ON";
        }
        
        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var state = new Dictionary<string, object> { { action.Property, newValue } };
        await deviceService.SetDeviceStateAsync(action.DeviceId, state);
    }

    private async Task ExecuteWebhookAsync(AutomationActionEntity action)
    {
        if (string.IsNullOrEmpty(action.WebhookUrl)) return;
        
        var method = action.WebhookMethod?.ToUpperInvariant() ?? "POST";
        
        var request = new HttpRequestMessage(new HttpMethod(method), action.WebhookUrl);
        
        if (!string.IsNullOrEmpty(action.WebhookBody) && (method == "POST" || method == "PUT"))
        {
            request.Content = new StringContent(action.WebhookBody, System.Text.Encoding.UTF8, "application/json");
        }
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Update rule execution stats
    /// </summary>
    private async Task UpdateRuleStatsAsync(Guid ruleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
        
        var rule = await db.AutomationRules.FindAsync(ruleId);
        if (rule != null)
        {
            rule.LastTriggeredAt = DateTime.UtcNow;
            rule.ExecutionCount++;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Log an automation execution
    /// </summary>
    private async Task LogExecutionAsync(
        AutomationRuleEntity rule, 
        ExecutionStatus status, 
        long durationMs,
        string? triggerDeviceId,
        string? triggerProperty,
        string? errorMessage,
        List<AutomationActionResult>? actionResults = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        var log = new AutomationExecutionLogEntity
        {
            Id = Guid.NewGuid(),
            AutomationRuleId = rule.Id,
            ExecutedAt = DateTime.UtcNow,
            Status = status.ToString(),
            TriggerSource = JsonSerializer.Serialize(new { deviceId = triggerDeviceId, property = triggerProperty }),
            ActionResults = actionResults != null ? AutomationEntityExtensions.SerializeActionResults(actionResults) : null,
            DurationMs = (int)durationMs,
            ErrorMessage = errorMessage
        };
        
        await automationService.LogExecutionAsync(log);
    }

    /// <summary>
    /// Load all device states into cache
    /// </summary>
    private async Task LoadDeviceStatesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
        
        // Load latest state for each device from recent signals
        // This is a simplified approach - in production you'd want a dedicated state table
        var devices = await db.Devices.AsNoTracking().ToListAsync();
        
        _logger.LogInformation("Loaded {Count} devices into automation engine cache", devices.Count);
    }
}
