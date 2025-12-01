namespace SDHome.Lib.Data.Entities;

/// <summary>
/// Automation rule entity - the main automation definition
/// </summary>
public class AutomationRuleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// How triggers combine: "any" (OR) or "all" (AND)
    /// </summary>
    public string TriggerMode { get; set; } = "any";
    
    /// <summary>
    /// How conditions combine: "any" (OR) or "all" (AND)
    /// </summary>
    public string ConditionMode { get; set; } = "all";
    
    /// <summary>
    /// Cooldown period in seconds between executions (prevents rapid re-triggering)
    /// </summary>
    public int CooldownSeconds { get; set; } = 0;
    
    public DateTime? LastTriggeredAt { get; set; }
    public int ExecutionCount { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<AutomationTriggerEntity> Triggers { get; set; } = new List<AutomationTriggerEntity>();
    public ICollection<AutomationConditionEntity> Conditions { get; set; } = new List<AutomationConditionEntity>();
    public ICollection<AutomationActionEntity> Actions { get; set; } = new List<AutomationActionEntity>();
    public ICollection<AutomationExecutionLogEntity> ExecutionLogs { get; set; } = new List<AutomationExecutionLogEntity>();
}

/// <summary>
/// Automation trigger - what starts the automation
/// </summary>
public class AutomationTriggerEntity
{
    public Guid Id { get; set; }
    public Guid AutomationRuleId { get; set; }
    
    /// <summary>
    /// Trigger type: device_state, time, sun, sensor_threshold, etc.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;
    
    /// <summary>
    /// For device triggers: the device ID
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// For device triggers: the property/capability to watch
    /// </summary>
    public string? Property { get; set; }
    
    /// <summary>
    /// Comparison operator: equals, not_equals, greater_than, less_than, changes_to, changes_from, any_change
    /// </summary>
    public string? Operator { get; set; }
    
    /// <summary>
    /// The value to compare against (JSON serialized for complex types)
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// For time triggers: cron expression or time string
    /// </summary>
    public string? TimeExpression { get; set; }
    
    /// <summary>
    /// For sun triggers: sunrise, sunset, dawn, dusk
    /// </summary>
    public string? SunEvent { get; set; }
    
    /// <summary>
    /// Offset in minutes (e.g., -30 for 30 minutes before)
    /// </summary>
    public int? OffsetMinutes { get; set; }
    
    public int SortOrder { get; set; } = 0;
    
    // Navigation
    public AutomationRuleEntity? AutomationRule { get; set; }
}

/// <summary>
/// Automation condition - additional requirements that must be met
/// </summary>
public class AutomationConditionEntity
{
    public Guid Id { get; set; }
    public Guid AutomationRuleId { get; set; }
    
    /// <summary>
    /// Condition type: device_state, time_range, sun_position, etc.
    /// </summary>
    public string ConditionType { get; set; } = string.Empty;
    
    /// <summary>
    /// For device conditions: the device ID
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// For device conditions: the property/capability to check
    /// </summary>
    public string? Property { get; set; }
    
    /// <summary>
    /// Comparison operator: equals, not_equals, greater_than, less_than, between, contains
    /// </summary>
    public string? Operator { get; set; }
    
    /// <summary>
    /// The value to compare against (JSON serialized)
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// Secondary value for 'between' operator
    /// </summary>
    public string? Value2 { get; set; }
    
    /// <summary>
    /// For time_range conditions: start time (HH:mm format)
    /// </summary>
    public string? TimeStart { get; set; }
    
    /// <summary>
    /// For time_range conditions: end time (HH:mm format)
    /// </summary>
    public string? TimeEnd { get; set; }
    
    /// <summary>
    /// For day-based conditions: array of day numbers (0=Sunday, 6=Saturday)
    /// </summary>
    public string? DaysOfWeek { get; set; }
    
    public int SortOrder { get; set; } = 0;
    
    // Navigation
    public AutomationRuleEntity? AutomationRule { get; set; }
}

/// <summary>
/// Automation action - what happens when the automation fires
/// </summary>
public class AutomationActionEntity
{
    public Guid Id { get; set; }
    public Guid AutomationRuleId { get; set; }
    
    /// <summary>
    /// Action type: set_device_state, toggle_device, delay, webhook, notification, scene
    /// </summary>
    public string ActionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Target device ID for device actions
    /// </summary>
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// For device actions: the property to set
    /// </summary>
    public string? Property { get; set; }
    
    /// <summary>
    /// The value to set (JSON serialized)
    /// </summary>
    public string? Value { get; set; }
    
    /// <summary>
    /// For delay actions: delay in seconds before this action
    /// </summary>
    public int? DelaySeconds { get; set; }
    
    /// <summary>
    /// For webhook actions: the URL to call
    /// </summary>
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// For webhook actions: HTTP method (GET, POST, etc.)
    /// </summary>
    public string? WebhookMethod { get; set; }
    
    /// <summary>
    /// For webhook actions: request body (JSON)
    /// </summary>
    public string? WebhookBody { get; set; }
    
    /// <summary>
    /// For notification actions: notification message
    /// </summary>
    public string? NotificationMessage { get; set; }
    
    /// <summary>
    /// For notification actions: notification title
    /// </summary>
    public string? NotificationTitle { get; set; }
    
    /// <summary>
    /// For scene actions: scene ID to activate
    /// </summary>
    public Guid? SceneId { get; set; }
    
    public int SortOrder { get; set; } = 0;
    
    // Navigation
    public AutomationRuleEntity? AutomationRule { get; set; }
}

/// <summary>
/// Execution log for automation runs
/// </summary>
public class AutomationExecutionLogEntity
{
    public Guid Id { get; set; }
    public Guid AutomationRuleId { get; set; }
    
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Status: success, partial_failure, failure, skipped_cooldown, skipped_condition
    /// </summary>
    public string Status { get; set; } = "success";
    
    /// <summary>
    /// What triggered this execution (JSON: trigger ID, source event, etc.)
    /// </summary>
    public string? TriggerSource { get; set; }
    
    /// <summary>
    /// Details of each action execution (JSON array)
    /// </summary>
    public string? ActionResults { get; set; }
    
    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public int DurationMs { get; set; }
    
    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Navigation
    public AutomationRuleEntity? AutomationRule { get; set; }
}

/// <summary>
/// Scene entity - a saved collection of device states
/// </summary>
public class SceneEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    
    /// <summary>
    /// Device states for this scene (JSON: { "deviceId": { "property": value, ... }, ... })
    /// </summary>
    public string DeviceStates { get; set; } = "{}";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
