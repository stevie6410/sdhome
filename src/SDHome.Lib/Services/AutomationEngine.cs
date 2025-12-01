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
/// Interface for the automation engine, allowing components to trigger automation evaluation
/// </summary>
public interface IAutomationEngine
{
    /// <summary>
    /// Called when a device state changes - evaluates and executes matching automations
    /// </summary>
    Task ProcessDeviceStateChangeAsync(
        string deviceId, 
        string property, 
        object? oldValue, 
        object? newValue,
        PipelineContext? pipelineContext = null);
    
    /// <summary>
    /// Called when a TriggerEvent is created (button press, motion detected, contact changed, etc.)
    /// </summary>
    Task ProcessTriggerEventAsync(TriggerEvent triggerEvent, PipelineContext? pipelineContext = null);
    
    /// <summary>
    /// Called when a SensorReading is created (temperature, humidity, etc.)
    /// </summary>
    Task ProcessSensorReadingAsync(SensorReading reading, PipelineContext? pipelineContext = null);
}

/// <summary>
/// Background service that listens to device state changes and executes matching automations
/// </summary>
public class AutomationEngine : BackgroundService, IAutomationEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationEngine> _logger;
    private readonly IRealtimeEventBroadcaster _broadcaster;
    private readonly IEndToEndLatencyTracker _e2eTracker;
    private readonly HttpClient _httpClient;
    
    // Cache of device states for condition evaluation
    private readonly Dictionary<string, Dictionary<string, object?>> _deviceStates = new();
    
    // Cache of latest sensor readings for condition evaluation
    private readonly Dictionary<string, Dictionary<string, double>> _sensorReadings = new();
    
    private readonly object _stateLock = new();

    public AutomationEngine(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationEngine> logger,
        IRealtimeEventBroadcaster broadcaster,
        IEndToEndLatencyTracker e2eTracker,
        HttpClient httpClient)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _broadcaster = broadcaster;
        _e2eTracker = e2eTracker;
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
        object? newValue,
        PipelineContext? pipelineContext = null)
    {
        var processStart = Stopwatch.GetTimestamp();
        
        _logger.LogInformation("🔄 AutomationEngine: Device state change - {DeviceId}.{Property}: {OldValue} → {NewValue}", 
            deviceId, property, oldValue ?? "null", newValue ?? "null");
        
        // Check if this is a target device responding to an automation action
        // This completes the E2E tracking for any pending automations targeting this device
        _e2eTracker.RecordTargetDeviceResponse(deviceId);
        
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
        var cacheTime = Stopwatch.GetElapsedTime(processStart);
        
        var queryStart = Stopwatch.GetTimestamp();
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        // Find matching rules
        var rules = await automationService.GetRulesForDeviceTriggerAsync(deviceId, property);
        var queryTime = Stopwatch.GetElapsedTime(queryStart);
        
        _logger.LogInformation("🔍 AutomationEngine: Found {Count} rules matching {DeviceId}.{Property} (query: {QueryMs:F1}ms)", 
            rules.Count, deviceId, property, queryTime.TotalMilliseconds);
        
        // Start E2E tracking if there are rules to execute (with pipeline context if available)
        string? trackingId = null;
        if (rules.Count > 0)
        {
            PipelineStagesSnapshot? snapshot = null;
            if (pipelineContext != null)
            {
                snapshot = new PipelineStagesSnapshot
                {
                    ParseMs = pipelineContext.ParseMs,
                    DatabaseMs = pipelineContext.DatabaseMs,
                    BroadcastMs = pipelineContext.BroadcastMs
                };
            }
            
            trackingId = _e2eTracker.StartTracking(
                deviceId, 
                rules.FirstOrDefault()?.Name,
                pipelineSnapshot: snapshot);
            _e2eTracker.RecordAutomationLookup(trackingId, queryTime.TotalMilliseconds);
        }
        
        foreach (var rule in rules)
        {
            var ruleStart = Stopwatch.GetTimestamp();
            _logger.LogInformation("⚡ AutomationEngine: Evaluating rule '{RuleName}' (ID: {RuleId})", 
                rule.Name, rule.Id);
            try
            {
                await EvaluateAndExecuteRuleAsync(rule, deviceId, property, oldValue, newValue, trackingId);
                var ruleTime = Stopwatch.GetElapsedTime(ruleStart);
                _logger.LogInformation("⏱️ Rule '{RuleName}' completed in {RuleMs:F1}ms", rule.Name, ruleTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ AutomationEngine: Error executing rule '{RuleName}'", rule.Name);
            }
        }
        
        var totalTime = Stopwatch.GetElapsedTime(processStart);
        if (rules.Count > 0)
        {
            _logger.LogInformation("⏱️ ProcessDeviceStateChange total: {TotalMs:F1}ms (cache: {CacheMs:F1}ms, query: {QueryMs:F1}ms)",
                totalTime.TotalMilliseconds, cacheTime.TotalMilliseconds, queryTime.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Called when a TriggerEvent is created (button press, motion detected, contact changed, etc.)
    /// This provides first-party support for trigger events in automations.
    /// </summary>
    public async Task ProcessTriggerEventAsync(TriggerEvent triggerEvent, PipelineContext? pipelineContext = null)
    {
        _logger.LogDebug("Processing trigger event: {DeviceId} {TriggerType}/{TriggerSubType}", 
            triggerEvent.DeviceId, triggerEvent.TriggerType, triggerEvent.TriggerSubType);
        
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        // Find matching rules for this trigger event
        var rules = await automationService.GetRulesForTriggerEventAsync(
            triggerEvent.DeviceId, 
            triggerEvent.TriggerType, 
            triggerEvent.TriggerSubType);
        
        _logger.LogDebug("Found {Count} automation rules matching trigger event", rules.Count);
        
        foreach (var rule in rules)
        {
            try
            {
                await EvaluateAndExecuteRuleForTriggerEventAsync(rule, triggerEvent, pipelineContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing automation rule for trigger event: {RuleName}", rule.Name);
            }
        }
    }

    /// <summary>
    /// Called when a SensorReading is created (temperature, humidity, etc.)
    /// This provides first-party support for sensor readings in automations.
    /// </summary>
    public async Task ProcessSensorReadingAsync(SensorReading reading, PipelineContext? pipelineContext = null)
    {
        _logger.LogDebug("Processing sensor reading: {DeviceId} {Metric}={Value}", 
            reading.DeviceId, reading.Metric, reading.Value);
        
        // Update cached readings
        double? oldValue = null;
        lock (_stateLock)
        {
            if (!_sensorReadings.TryGetValue(reading.DeviceId, out var deviceReadings))
            {
                deviceReadings = new Dictionary<string, double>();
                _sensorReadings[reading.DeviceId] = deviceReadings;
            }
            
            if (deviceReadings.TryGetValue(reading.Metric, out var existingValue))
            {
                oldValue = existingValue;
            }
            
            deviceReadings[reading.Metric] = reading.Value;
        }
        
        using var scope = _scopeFactory.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        
        // Find matching rules for this sensor reading
        var rules = await automationService.GetRulesForSensorReadingAsync(
            reading.DeviceId, 
            reading.Metric);
        
        _logger.LogDebug("Found {Count} automation rules matching sensor reading", rules.Count);
        
        foreach (var rule in rules)
        {
            try
            {
                await EvaluateAndExecuteRuleForSensorReadingAsync(rule, reading, oldValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing automation rule for sensor reading: {RuleName}", rule.Name);
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
        object? newValue,
        string? e2eTrackingId = null)
    {
        var sw = Stopwatch.StartNew();
        
        // Check cooldown
        if (rule.LastTriggeredAt.HasValue && rule.CooldownSeconds > 0)
        {
            var timeSinceLastTrigger = DateTime.UtcNow - rule.LastTriggeredAt.Value;
            if (timeSinceLastTrigger.TotalSeconds < rule.CooldownSeconds)
            {
                var remaining = rule.CooldownSeconds - timeSinceLastTrigger.TotalSeconds;
                _logger.LogDebug("Rule {RuleName} skipped due to cooldown ({Remaining}s remaining)", 
                    rule.Name, remaining);
                await EmitLogAsync(rule, AutomationLogLevel.Debug, AutomationLogPhase.CooldownActive,
                    $"Skipped - cooldown active ({remaining:F0}s remaining)",
                    new Dictionary<string, object?> { ["remainingSeconds"] = remaining });
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
            
            _logger.LogInformation("📋 Rule '{RuleName}': Found {Count} matching triggers for {DeviceId}.{Property}", 
                rule.Name, matchingTriggers.Count, triggerDeviceId, triggerProperty);
            
            foreach (var trigger in matchingTriggers)
            {
                var result = EvaluateTrigger(trigger, oldValue, newValue);
                var normalizedNew = NormalizeValue(newValue);
                var normalizedTarget = NormalizeValue(ParseJsonValue(trigger.Value));
                _logger.LogInformation("  🎯 Trigger: Op={Operator}, TargetValue='{TargetValue}' (normalized='{NormalizedTarget}'), NewValue='{NewValue}' (normalized='{NormalizedNew}'), Result={Result}", 
                    trigger.Operator, trigger.Value, normalizedTarget, newValue, normalizedNew, result);
            }
            
            var anyTriggerMatches = matchingTriggers.Any(t => EvaluateTrigger(t, oldValue, newValue));
            
            if (!anyTriggerMatches)
            {
                _logger.LogInformation("⏭️ Rule '{RuleName}' skipped - trigger condition not met", rule.Name);
                await EmitLogAsync(rule, AutomationLogLevel.Debug, AutomationLogPhase.TriggerSkipped,
                    $"Trigger not matched: {triggerDeviceId}.{triggerProperty} = {newValue}",
                    new Dictionary<string, object?> 
                    { 
                        ["deviceId"] = triggerDeviceId, 
                        ["property"] = triggerProperty, 
                        ["value"] = newValue 
                    });
                return;
            }
            
            // Trigger matched!
            await EmitLogAsync(rule, AutomationLogLevel.Info, AutomationLogPhase.TriggerMatched,
                $"Trigger matched: {triggerDeviceId}.{triggerProperty} = {newValue}",
                new Dictionary<string, object?> 
                { 
                    ["deviceId"] = triggerDeviceId, 
                    ["property"] = triggerProperty, 
                    ["value"] = newValue 
                });
        }
        
        // Evaluate conditions
        await EmitLogAsync(rule, AutomationLogLevel.Debug, AutomationLogPhase.ConditionEvaluating,
            $"Evaluating {rule.Conditions.Count} condition(s) (mode: {rule.ConditionMode})");
        
        var conditionsPassed = EvaluateConditions(rule);
        if (!conditionsPassed)
        {
            _logger.LogDebug("Rule {RuleName} skipped - conditions not met", rule.Name);
            await EmitLogAsync(rule, AutomationLogLevel.Warning, AutomationLogPhase.ConditionFailed,
                "Conditions not met - execution skipped");
            await LogExecutionAsync(rule, ExecutionStatus.SkippedCondition, sw.ElapsedMilliseconds, 
                triggerDeviceId, triggerProperty, "Conditions not met");
            return;
        }
        
        if (rule.Conditions.Count > 0)
        {
            await EmitLogAsync(rule, AutomationLogLevel.Info, AutomationLogPhase.ConditionPassed,
                "All conditions passed");
        }
        
        _logger.LogInformation("Executing automation rule: {RuleName}", rule.Name);
        
        // Execute actions
        var actionResults = new List<AutomationActionResult>();
        var hasFailure = false;
        var actionIndex = 0;
        var totalActions = rule.Actions.Count;
        var actionStartTime = Stopwatch.GetTimestamp();
        string? lastTargetDeviceId = null;
        
        foreach (var action in rule.Actions.OrderBy(a => a.SortOrder))
        {
            actionIndex++;
            var actionSw = Stopwatch.StartNew();
            
            await EmitLogAsync(rule, AutomationLogLevel.Info, AutomationLogPhase.ActionExecuting,
                $"Executing action {actionIndex}/{totalActions}: {action.ActionType}",
                new Dictionary<string, object?>
                {
                    ["actionType"] = action.ActionType,
                    ["deviceId"] = action.DeviceId,
                    ["property"] = action.Property,
                    ["value"] = action.Value
                });
            
            try
            {
                await ExecuteActionAsync(action);
                actionResults.Add(new AutomationActionResult(action.Id, true, null, (int)actionSw.ElapsedMilliseconds));
                
                // Track target device for E2E measurement
                if (!string.IsNullOrEmpty(action.DeviceId))
                {
                    lastTargetDeviceId = action.DeviceId;
                }
                
                await EmitLogAsync(rule, AutomationLogLevel.Success, AutomationLogPhase.ActionCompleted,
                    $"Action completed: {action.ActionType} ({actionSw.ElapsedMilliseconds}ms)",
                    new Dictionary<string, object?>
                    {
                        ["actionType"] = action.ActionType,
                        ["durationMs"] = actionSw.ElapsedMilliseconds
                    });
            }
            catch (Exception ex)
            {
                hasFailure = true;
                actionResults.Add(new AutomationActionResult(action.Id, false, ex.Message, (int)actionSw.ElapsedMilliseconds));
                _logger.LogError(ex, "Action {ActionType} failed in rule {RuleName}", action.ActionType, rule.Name);
                
                await EmitLogAsync(rule, AutomationLogLevel.Error, AutomationLogPhase.ActionFailed,
                    $"Action failed: {action.ActionType} - {ex.Message}",
                    new Dictionary<string, object?>
                    {
                        ["actionType"] = action.ActionType,
                        ["error"] = ex.Message
                    });
            }
        }
        
        // Record E2E action execution time
        if (!string.IsNullOrEmpty(e2eTrackingId) && !string.IsNullOrEmpty(lastTargetDeviceId))
        {
            var actionTime = Stopwatch.GetElapsedTime(actionStartTime);
            _e2eTracker.RecordActionExecution(e2eTrackingId, actionTime.TotalMilliseconds, lastTargetDeviceId);
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
    /// Evaluate conditions and execute a rule for a TriggerEvent
    /// </summary>
    private async Task EvaluateAndExecuteRuleForTriggerEventAsync(
        AutomationRuleEntity rule,
        TriggerEvent triggerEvent,
        PipelineContext? pipelineContext = null)
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
        
        // Evaluate trigger conditions for TriggerEvent
        var matchingTriggers = rule.Triggers
            .Where(t => t.TriggerType == "TriggerEvent" && 
                       t.DeviceId == triggerEvent.DeviceId)
            .ToList();
        
        var anyTriggerMatches = matchingTriggers.Any(t => EvaluateTriggerEventTrigger(t, triggerEvent));
        
        if (!anyTriggerMatches)
        {
            _logger.LogDebug("Rule {RuleName} skipped - trigger event condition not met", rule.Name);
            return;
        }
        
        // Evaluate conditions
        var conditionsPassed = EvaluateConditions(rule);
        if (!conditionsPassed)
        {
            _logger.LogDebug("Rule {RuleName} skipped - conditions not met", rule.Name);
            await LogExecutionAsync(rule, ExecutionStatus.SkippedCondition, sw.ElapsedMilliseconds, 
                triggerEvent.DeviceId, triggerEvent.TriggerType, "Conditions not met");
            return;
        }
        
        _logger.LogInformation("Executing automation rule: {RuleName} (triggered by {TriggerType}/{TriggerSubType} from {DeviceId})", 
            rule.Name, triggerEvent.TriggerType, triggerEvent.TriggerSubType, triggerEvent.DeviceId);
        
        // Execute actions (no E2E tracking for TriggerEvent type rules)
        await ExecuteRuleActionsAsync(rule, sw, triggerEvent.DeviceId, triggerEvent.TriggerType);
    }

    /// <summary>
    /// Evaluate conditions and execute a rule for a SensorReading
    /// </summary>
    private async Task EvaluateAndExecuteRuleForSensorReadingAsync(
        AutomationRuleEntity rule,
        SensorReading reading,
        double? oldValue)
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
        
        // Evaluate trigger conditions for SensorReading
        var matchingTriggers = rule.Triggers
            .Where(t => t.TriggerType == "SensorReading" && 
                       t.DeviceId == reading.DeviceId &&
                       (t.Property == null || t.Property == reading.Metric))
            .ToList();
        
        var anyTriggerMatches = matchingTriggers.Any(t => EvaluateSensorReadingTrigger(t, reading.Value, oldValue));
        
        if (!anyTriggerMatches)
        {
            _logger.LogDebug("Rule {RuleName} skipped - sensor reading condition not met", rule.Name);
            return;
        }
        
        // Evaluate conditions
        var conditionsPassed = EvaluateConditions(rule);
        if (!conditionsPassed)
        {
            _logger.LogDebug("Rule {RuleName} skipped - conditions not met", rule.Name);
            await LogExecutionAsync(rule, ExecutionStatus.SkippedCondition, sw.ElapsedMilliseconds, 
                reading.DeviceId, reading.Metric, "Conditions not met");
            return;
        }
        
        _logger.LogInformation("Executing automation rule: {RuleName} (triggered by {Metric}={Value} from {DeviceId})", 
            rule.Name, reading.Metric, reading.Value, reading.DeviceId);
        
        // Execute actions
        await ExecuteRuleActionsAsync(rule, sw, reading.DeviceId, reading.Metric);
    }

    /// <summary>
    /// Execute all actions for a rule (shared by all execution paths)
    /// </summary>
    private async Task ExecuteRuleActionsAsync(
        AutomationRuleEntity rule, 
        Stopwatch sw, 
        string? triggerDeviceId, 
        string? triggerProperty)
    {
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
    /// Evaluate a TriggerEvent against a trigger definition
    /// </summary>
    private static bool EvaluateTriggerEventTrigger(AutomationTriggerEntity trigger, TriggerEvent triggerEvent)
    {
        // Check trigger type (property field stores the trigger type: button, motion, contact, etc.)
        if (!string.IsNullOrEmpty(trigger.Property) && trigger.Property != triggerEvent.TriggerType)
        {
            return false;
        }
        
        // Check trigger sub-type (value field stores the expected sub-type: single, double, hold, active, open, etc.)
        if (!string.IsNullOrEmpty(trigger.Value))
        {
            var expectedSubType = trigger.Value.Trim('"');
            if (triggerEvent.TriggerSubType != expectedSubType)
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Evaluate a SensorReading against a trigger definition
    /// </summary>
    private static bool EvaluateSensorReadingTrigger(AutomationTriggerEntity trigger, double newValue, double? oldValue)
    {
        if (string.IsNullOrEmpty(trigger.Operator)) return true; // No operator = any reading
        
        var op = Enum.Parse<ComparisonOperator>(trigger.Operator, true);
        
        double? targetValue = null;
        if (!string.IsNullOrEmpty(trigger.Value))
        {
            var valueStr = trigger.Value.Trim('"');
            if (double.TryParse(valueStr, out var parsed))
            {
                targetValue = parsed;
            }
        }
        
        return op switch
        {
            ComparisonOperator.AnyChange => oldValue.HasValue && Math.Abs(newValue - oldValue.Value) > 0.001,
            ComparisonOperator.Equals => targetValue.HasValue && Math.Abs(newValue - targetValue.Value) < 0.001,
            ComparisonOperator.NotEquals => targetValue.HasValue && Math.Abs(newValue - targetValue.Value) >= 0.001,
            ComparisonOperator.GreaterThan => targetValue.HasValue && newValue > targetValue.Value,
            ComparisonOperator.GreaterThanOrEqual => targetValue.HasValue && newValue >= targetValue.Value,
            ComparisonOperator.LessThan => targetValue.HasValue && newValue < targetValue.Value,
            ComparisonOperator.LessThanOrEqual => targetValue.HasValue && newValue <= targetValue.Value,
            ComparisonOperator.ChangesTo => targetValue.HasValue && 
                                            oldValue.HasValue && 
                                            Math.Abs(newValue - targetValue.Value) < 0.001 &&
                                            Math.Abs(oldValue.Value - targetValue.Value) >= 0.001,
            _ => true
        };
    }

    /// <summary>
    /// Evaluate a single trigger
    /// </summary>
    private static bool EvaluateTrigger(AutomationTriggerEntity trigger, object? oldValue, object? newValue)
    {
        if (string.IsNullOrEmpty(trigger.Operator)) return true; // No operator = any change
        
        var op = Enum.Parse<ComparisonOperator>(trigger.Operator, true);
        var targetValue = ParseJsonValue(trigger.Value);
        
        // Normalize values to strings for comparison since they come from different sources
        var normalizedOld = NormalizeValue(oldValue);
        var normalizedNew = NormalizeValue(newValue);
        var normalizedTarget = NormalizeValue(targetValue);
        
        return op switch
        {
            ComparisonOperator.AnyChange => !Equals(normalizedOld, normalizedNew),
            ComparisonOperator.ChangesTo => Equals(normalizedNew, normalizedTarget),
            ComparisonOperator.ChangesFrom => Equals(normalizedOld, normalizedTarget),
            ComparisonOperator.Equals => Equals(normalizedNew, normalizedTarget),
            ComparisonOperator.NotEquals => !Equals(normalizedNew, normalizedTarget),
            _ => CompareValues(newValue, targetValue, op)
        };
    }
    
    /// <summary>
    /// Parse a JSON value, handling JsonElement properly
    /// </summary>
    private static object? ParseJsonValue(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => json // Return raw JSON for complex types
            };
        }
        catch
        {
            return json; // Return as-is if not valid JSON
        }
    }
    
    /// <summary>
    /// Normalize a value to a comparable string representation
    /// </summary>
    private static string? NormalizeValue(object? value)
    {
        if (value == null) return null;
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }
        return value.ToString();
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
        {
            _logger.LogWarning("ToggleDevice action missing DeviceId or Property");
            return;
        }
        
        // Get current state from cache
        object? currentValue = null;
        lock (_stateLock)
        {
            if (_deviceStates.TryGetValue(action.DeviceId, out var deviceState))
            {
                deviceState.TryGetValue(action.Property, out currentValue);
                _logger.LogInformation("🔄 Toggle: Found cached state for {DeviceId}.{Property} = {Value}", 
                    action.DeviceId, action.Property, currentValue);
            }
            else
            {
                _logger.LogWarning("🔄 Toggle: No cached state found for {DeviceId}, defaulting to ON", action.DeviceId);
            }
        }
        
        // Toggle boolean or "ON"/"OFF" string
        object newValue;
        if (currentValue is bool boolVal)
        {
            newValue = !boolVal;
        }
        else if (currentValue is JsonElement jsonElement)
        {
            // Handle JsonElement from deserialized state
            var strVal = jsonElement.ValueKind == JsonValueKind.String 
                ? jsonElement.GetString() 
                : jsonElement.ToString();
            newValue = strVal?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true ? "OFF" : "ON";
        }
        else if (currentValue?.ToString()?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true)
        {
            newValue = "OFF";
        }
        else
        {
            newValue = "ON";
        }
        
        _logger.LogInformation("🔄 Toggle: {DeviceId}.{Property}: {OldValue} → {NewValue}", 
            action.DeviceId, action.Property, currentValue, newValue);
        
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
    /// Load all device states into cache from recent signal events
    /// </summary>
    private async Task LoadDeviceStatesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SignalsDbContext>();
        
        // Get the most recent signal for each device (last 24 hours)
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var recentSignals = await db.SignalEvents
            .AsNoTracking()
            .Where(s => s.TimestampUtc >= cutoff && s.RawPayload != null)
            .GroupBy(s => s.DeviceId)
            .Select(g => g.OrderByDescending(s => s.TimestampUtc).First())
            .ToListAsync();
        
        lock (_stateLock)
        {
            foreach (var signal in recentSignals)
            {
                if (string.IsNullOrEmpty(signal.DeviceId)) continue;
                
                try
                {
                    // Parse the raw payload to extract state
                    var payload = JsonSerializer.Deserialize<JsonElement>(signal.RawPayload!);
                    if (payload.ValueKind == JsonValueKind.Object)
                    {
                        var deviceState = new Dictionary<string, object?>();
                        
                        foreach (var prop in payload.EnumerateObject())
                        {
                            deviceState[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString(),
                                JsonValueKind.Number => prop.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => prop.Value.GetRawText()
                            };
                        }
                        
                        _deviceStates[signal.DeviceId] = deviceState;
                    }
                }
                catch
                {
                    // Skip signals with invalid payloads
                }
            }
        }
        
        _logger.LogInformation("Loaded state for {Count} devices into automation engine cache", _deviceStates.Count);
    }

    /// <summary>
    /// Broadcast an automation log entry for real-time monitoring
    /// </summary>
    private async Task EmitLogAsync(
        AutomationRuleEntity rule,
        AutomationLogLevel level,
        AutomationLogPhase phase,
        string message,
        Dictionary<string, object?>? details = null)
    {
        var logEntry = new AutomationLogEntry
        {
            AutomationId = rule.Id,
            AutomationName = rule.Name,
            Level = level,
            Phase = phase,
            Message = message,
            Details = details,
            TimestampUtc = DateTime.UtcNow
        };
        
        try
        {
            await _broadcaster.BroadcastAutomationLogAsync(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast automation log");
        }
    }

    /// <summary>
    /// Helper to emit log for specific automation by ID (used when rule entity not loaded)
    /// </summary>
    private async Task EmitLogByIdAsync(
        Guid automationId,
        string automationName,
        AutomationLogLevel level,
        AutomationLogPhase phase,
        string message,
        Dictionary<string, object?>? details = null)
    {
        var logEntry = new AutomationLogEntry
        {
            AutomationId = automationId,
            AutomationName = automationName,
            Level = level,
            Phase = phase,
            Message = message,
            Details = details,
            TimestampUtc = DateTime.UtcNow
        };
        
        try
        {
            await _broadcaster.BroadcastAutomationLogAsync(logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast automation log");
        }
    }
}
