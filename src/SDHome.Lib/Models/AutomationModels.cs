using SDHome.Lib.Data.Entities;

namespace SDHome.Lib.Models;

// ===== Enums =====

public enum TriggerType
{
    DeviceState,      // When a device property changes
    Time,             // At a specific time or cron schedule
    Sunrise,          // At sunrise (with optional offset)
    Sunset,           // At sunset (with optional offset)
    SensorThreshold,  // When sensor exceeds/falls below threshold
    Manual,           // Triggered manually via UI/API
    
    // First-party trigger event types
    TriggerEvent,     // React to TriggerEvent (button press, motion, contact, etc.)
    SensorReading     // React to SensorReading (temperature, humidity, etc.)
}

public enum ConditionType
{
    DeviceState,      // Check device property value
    TimeRange,        // Within a time range
    DayOfWeek,        // On specific days
    SunPosition,      // Before/after sunrise/sunset
    And,              // Logical AND of nested conditions
    Or                // Logical OR of nested conditions
}

public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    Contains,
    StartsWith,
    EndsWith,
    ChangesTo,
    ChangesFrom,
    AnyChange
}

public enum ActionType
{
    SetDeviceState,   // Set a device property
    ToggleDevice,     // Toggle a binary device property
    Delay,            // Wait before next action
    Webhook,          // Call an HTTP webhook
    Notification,     // Send a notification
    ActivateScene,    // Activate a scene
    RunAutomation     // Trigger another automation
}

public enum ExecutionStatus
{
    Success,
    PartialFailure,
    Failure,
    SkippedCooldown,
    SkippedCondition
}

public enum TriggerMode
{
    Any,  // OR - any trigger fires the automation
    All   // AND - all triggers must fire together (within time window)
}

public enum ConditionMode
{
    All,  // AND - all conditions must be true
    Any   // OR - any condition being true is enough
}

// ===== Domain Records =====

public record AutomationRule(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    bool IsEnabled,
    TriggerMode TriggerMode,
    ConditionMode ConditionMode,
    int CooldownSeconds,
    DateTime? LastTriggeredAt,
    int ExecutionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<AutomationTrigger> Triggers,
    List<AutomationCondition> Conditions,
    List<AutomationAction> Actions
);

public record AutomationTrigger(
    Guid Id,
    Guid AutomationRuleId,
    TriggerType TriggerType,
    string? DeviceId,
    string? Property,
    ComparisonOperator? Operator,
    object? Value,
    string? TimeExpression,
    string? SunEvent,
    int? OffsetMinutes,
    int SortOrder
);

public record AutomationCondition(
    Guid Id,
    Guid AutomationRuleId,
    ConditionType ConditionType,
    string? DeviceId,
    string? Property,
    ComparisonOperator? Operator,
    object? Value,
    object? Value2,
    string? TimeStart,
    string? TimeEnd,
    List<DayOfWeek>? DaysOfWeek,
    int SortOrder
);

public record AutomationAction(
    Guid Id,
    Guid AutomationRuleId,
    ActionType ActionType,
    string? DeviceId,
    string? Property,
    object? Value,
    int? DelaySeconds,
    string? WebhookUrl,
    string? WebhookMethod,
    string? WebhookBody,
    string? NotificationTitle,
    string? NotificationMessage,
    Guid? SceneId,
    int SortOrder
);

public record AutomationExecutionLog(
    Guid Id,
    Guid AutomationRuleId,
    DateTime ExecutedAt,
    ExecutionStatus Status,
    string? TriggerSource,
    List<AutomationActionResult>? ActionResults,
    int DurationMs,
    string? ErrorMessage
);

public record AutomationActionResult(
    Guid ActionId,
    bool Success,
    string? Error,
    int DurationMs
);

public record Scene(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    Dictionary<string, Dictionary<string, object>> DeviceStates,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// ===== DTOs for API =====

public record CreateAutomationRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    bool IsEnabled,
    TriggerMode TriggerMode,
    ConditionMode ConditionMode,
    int CooldownSeconds,
    List<CreateTriggerRequest> Triggers,
    List<CreateConditionRequest> Conditions,
    List<CreateActionRequest> Actions
);

public record UpdateAutomationRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    bool IsEnabled,
    TriggerMode TriggerMode,
    ConditionMode ConditionMode,
    int CooldownSeconds,
    List<CreateTriggerRequest> Triggers,
    List<CreateConditionRequest> Conditions,
    List<CreateActionRequest> Actions
);

public record CreateTriggerRequest(
    TriggerType TriggerType,
    string? DeviceId,
    string? Property,
    ComparisonOperator? Operator,
    object? Value,
    string? TimeExpression,
    string? SunEvent,
    int? OffsetMinutes,
    int SortOrder
);

public record CreateConditionRequest(
    ConditionType ConditionType,
    string? DeviceId,
    string? Property,
    ComparisonOperator? Operator,
    object? Value,
    object? Value2,
    string? TimeStart,
    string? TimeEnd,
    List<DayOfWeek>? DaysOfWeek,
    int SortOrder
);

public record CreateActionRequest(
    ActionType ActionType,
    string? DeviceId,
    string? Property,
    object? Value,
    int? DelaySeconds,
    string? WebhookUrl,
    string? WebhookMethod,
    string? WebhookBody,
    string? NotificationTitle,
    string? NotificationMessage,
    Guid? SceneId,
    int SortOrder
);

public record CreateSceneRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    Dictionary<string, Dictionary<string, object>> DeviceStates
);

public record UpdateSceneRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    Dictionary<string, Dictionary<string, object>> DeviceStates
);

// ===== Summary DTOs =====

public record AutomationSummary(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    bool IsEnabled,
    int TriggerCount,
    int ConditionCount,
    int ActionCount,
    DateTime? LastTriggeredAt,
    int ExecutionCount,
    DateTime CreatedAt
);

public record AutomationStats(
    int TotalAutomations,
    int EnabledAutomations,
    int DisabledAutomations,
    int TotalExecutionsToday,
    int SuccessfulExecutionsToday,
    int FailedExecutionsToday
);

// ===== Entity Extensions for Conversion =====

public static class AutomationEntityExtensions
{
    public static AutomationRule ToModel(this AutomationRuleEntity entity)
    {
        return new AutomationRule(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Icon,
            entity.Color,
            entity.IsEnabled,
            Enum.Parse<TriggerMode>(entity.TriggerMode, true),
            Enum.Parse<ConditionMode>(entity.ConditionMode, true),
            entity.CooldownSeconds,
            entity.LastTriggeredAt,
            entity.ExecutionCount,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Triggers.OrderBy(t => t.SortOrder).Select(t => t.ToModel()).ToList(),
            entity.Conditions.OrderBy(c => c.SortOrder).Select(c => c.ToModel()).ToList(),
            entity.Actions.OrderBy(a => a.SortOrder).Select(a => a.ToModel()).ToList()
        );
    }

    public static AutomationTrigger ToModel(this AutomationTriggerEntity entity)
    {
        return new AutomationTrigger(
            entity.Id,
            entity.AutomationRuleId,
            Enum.Parse<TriggerType>(entity.TriggerType, true),
            entity.DeviceId,
            entity.Property,
            string.IsNullOrEmpty(entity.Operator) ? null : Enum.Parse<ComparisonOperator>(entity.Operator, true),
            DeserializeValue(entity.Value),
            entity.TimeExpression,
            entity.SunEvent,
            entity.OffsetMinutes,
            entity.SortOrder
        );
    }

    public static AutomationCondition ToModel(this AutomationConditionEntity entity)
    {
        return new AutomationCondition(
            entity.Id,
            entity.AutomationRuleId,
            Enum.Parse<ConditionType>(entity.ConditionType, true),
            entity.DeviceId,
            entity.Property,
            string.IsNullOrEmpty(entity.Operator) ? null : Enum.Parse<ComparisonOperator>(entity.Operator, true),
            DeserializeValue(entity.Value),
            DeserializeValue(entity.Value2),
            entity.TimeStart,
            entity.TimeEnd,
            DeserializeDaysOfWeek(entity.DaysOfWeek),
            entity.SortOrder
        );
    }

    public static AutomationAction ToModel(this AutomationActionEntity entity)
    {
        return new AutomationAction(
            entity.Id,
            entity.AutomationRuleId,
            Enum.Parse<ActionType>(entity.ActionType, true),
            entity.DeviceId,
            entity.Property,
            DeserializeValue(entity.Value),
            entity.DelaySeconds,
            entity.WebhookUrl,
            entity.WebhookMethod,
            entity.WebhookBody,
            entity.NotificationTitle,
            entity.NotificationMessage,
            entity.SceneId,
            entity.SortOrder
        );
    }

    public static AutomationExecutionLog ToModel(this AutomationExecutionLogEntity entity)
    {
        return new AutomationExecutionLog(
            entity.Id,
            entity.AutomationRuleId,
            entity.ExecutedAt,
            Enum.Parse<ExecutionStatus>(entity.Status, true),
            entity.TriggerSource,
            DeserializeActionResults(entity.ActionResults),
            entity.DurationMs,
            entity.ErrorMessage
        );
    }

    public static Scene ToModel(this SceneEntity entity)
    {
        return new Scene(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Icon,
            entity.Color,
            DeserializeDeviceStates(entity.DeviceStates),
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    public static AutomationSummary ToSummary(this AutomationRuleEntity entity)
    {
        return new AutomationSummary(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Icon,
            entity.Color,
            entity.IsEnabled,
            entity.Triggers.Count,
            entity.Conditions.Count,
            entity.Actions.Count,
            entity.LastTriggeredAt,
            entity.ExecutionCount,
            entity.CreatedAt
        );
    }

    // Helper methods for JSON serialization
    private static object? DeserializeValue(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return json;
        }
    }

    private static List<DayOfWeek>? DeserializeDaysOfWeek(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var days = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
            return days?.Select(d => (DayOfWeek)d).ToList();
        }
        catch
        {
            return null;
        }
    }

    private static List<AutomationActionResult>? DeserializeActionResults(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<AutomationActionResult>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, Dictionary<string, object>> DeserializeDeviceStates(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json)
                ?? new Dictionary<string, Dictionary<string, object>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, object>>();
        }
    }

    public static string SerializeValue(object? value)
    {
        if (value == null) return string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    public static string SerializeDaysOfWeek(List<DayOfWeek>? days)
    {
        if (days == null || days.Count == 0) return string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(days.Select(d => (int)d).ToList());
    }

    public static string SerializeActionResults(List<AutomationActionResult>? results)
    {
        if (results == null) return string.Empty;
        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    public static string SerializeDeviceStates(Dictionary<string, Dictionary<string, object>> states)
    {
        return System.Text.Json.JsonSerializer.Serialize(states);
    }
}
