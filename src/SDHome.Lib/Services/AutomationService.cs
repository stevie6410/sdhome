using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SDHome.Lib.Data;
using SDHome.Lib.Data.Entities;
using SDHome.Lib.Models;

namespace SDHome.Lib.Services;

public interface IAutomationService
{
    // Automation Rules
    Task<List<AutomationSummary>> GetAutomationSummariesAsync();
    Task<AutomationRule?> GetAutomationAsync(Guid id);
    Task<AutomationRule> CreateAutomationAsync(CreateAutomationRequest request);
    Task<AutomationRule> UpdateAutomationAsync(Guid id, UpdateAutomationRequest request);
    Task DeleteAutomationAsync(Guid id);
    Task<AutomationRule> ToggleAutomationAsync(Guid id, bool enabled);
    Task<AutomationStats> GetStatsAsync();
    
    // Execution logs
    Task<List<AutomationExecutionLog>> GetExecutionLogsAsync(Guid? automationId = null, int take = 100);
    Task LogExecutionAsync(AutomationExecutionLogEntity log);
    
    // Scenes
    Task<List<Scene>> GetScenesAsync();
    Task<Scene?> GetSceneAsync(Guid id);
    Task<Scene> CreateSceneAsync(CreateSceneRequest request);
    Task<Scene> UpdateSceneAsync(Guid id, UpdateSceneRequest request);
    Task DeleteSceneAsync(Guid id);
    Task ActivateSceneAsync(Guid id);
    
    // Rule matching (for automation engine)
    Task<List<AutomationRuleEntity>> GetRulesForDeviceTriggerAsync(string deviceId, string property);
    Task<List<AutomationRuleEntity>> GetTimeTriggerRulesAsync();
    Task<List<AutomationRuleEntity>> GetSunTriggerRulesAsync(string sunEvent);
}

public class AutomationService(
    SignalsDbContext db,
    IDeviceService deviceService,
    ILogger<AutomationService> logger) : IAutomationService
{
    // ===== Automation Rules =====
    
    public async Task<List<AutomationSummary>> GetAutomationSummariesAsync()
    {
        return await db.AutomationRules
            .AsNoTracking()
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .OrderBy(r => r.Name)
            .Select(r => r.ToSummary())
            .ToListAsync();
    }
    
    public async Task<AutomationRule?> GetAutomationAsync(Guid id)
    {
        var entity = await db.AutomationRules
            .AsNoTracking()
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        return entity?.ToModel();
    }
    
    public async Task<AutomationRule> CreateAutomationAsync(CreateAutomationRequest request)
    {
        var entity = new AutomationRuleEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            Color = request.Color,
            IsEnabled = request.IsEnabled,
            TriggerMode = request.TriggerMode.ToString(),
            ConditionMode = request.ConditionMode.ToString(),
            CooldownSeconds = request.CooldownSeconds,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Add triggers
        foreach (var (trigger, index) in request.Triggers.Select((t, i) => (t, i)))
        {
            entity.Triggers.Add(new AutomationTriggerEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                TriggerType = trigger.TriggerType.ToString(),
                DeviceId = trigger.DeviceId,
                Property = trigger.Property,
                Operator = trigger.Operator?.ToString(),
                Value = AutomationEntityExtensions.SerializeValue(trigger.Value),
                TimeExpression = trigger.TimeExpression,
                SunEvent = trigger.SunEvent,
                OffsetMinutes = trigger.OffsetMinutes,
                SortOrder = trigger.SortOrder > 0 ? trigger.SortOrder : index
            });
        }
        
        // Add conditions
        foreach (var (condition, index) in request.Conditions.Select((c, i) => (c, i)))
        {
            entity.Conditions.Add(new AutomationConditionEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                ConditionType = condition.ConditionType.ToString(),
                DeviceId = condition.DeviceId,
                Property = condition.Property,
                Operator = condition.Operator?.ToString(),
                Value = AutomationEntityExtensions.SerializeValue(condition.Value),
                Value2 = AutomationEntityExtensions.SerializeValue(condition.Value2),
                TimeStart = condition.TimeStart,
                TimeEnd = condition.TimeEnd,
                DaysOfWeek = AutomationEntityExtensions.SerializeDaysOfWeek(condition.DaysOfWeek),
                SortOrder = condition.SortOrder > 0 ? condition.SortOrder : index
            });
        }
        
        // Add actions
        foreach (var (action, index) in request.Actions.Select((a, i) => (a, i)))
        {
            entity.Actions.Add(new AutomationActionEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                ActionType = action.ActionType.ToString(),
                DeviceId = action.DeviceId,
                Property = action.Property,
                Value = AutomationEntityExtensions.SerializeValue(action.Value),
                DelaySeconds = action.DelaySeconds,
                WebhookUrl = action.WebhookUrl,
                WebhookMethod = action.WebhookMethod,
                WebhookBody = action.WebhookBody,
                NotificationTitle = action.NotificationTitle,
                NotificationMessage = action.NotificationMessage,
                SceneId = action.SceneId,
                SortOrder = action.SortOrder > 0 ? action.SortOrder : index
            });
        }
        
        db.AutomationRules.Add(entity);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Created automation rule: {Name} (ID: {Id})", entity.Name, entity.Id);
        
        return entity.ToModel();
    }
    
    public async Task<AutomationRule> UpdateAutomationAsync(Guid id, UpdateAutomationRequest request)
    {
        var entity = await db.AutomationRules
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Automation rule {id} not found");
        
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Icon = request.Icon;
        entity.Color = request.Color;
        entity.IsEnabled = request.IsEnabled;
        entity.TriggerMode = request.TriggerMode.ToString();
        entity.ConditionMode = request.ConditionMode.ToString();
        entity.CooldownSeconds = request.CooldownSeconds;
        entity.UpdatedAt = DateTime.UtcNow;
        
        // Replace triggers
        db.AutomationTriggers.RemoveRange(entity.Triggers);
        entity.Triggers.Clear();
        foreach (var (trigger, index) in request.Triggers.Select((t, i) => (t, i)))
        {
            entity.Triggers.Add(new AutomationTriggerEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                TriggerType = trigger.TriggerType.ToString(),
                DeviceId = trigger.DeviceId,
                Property = trigger.Property,
                Operator = trigger.Operator?.ToString(),
                Value = AutomationEntityExtensions.SerializeValue(trigger.Value),
                TimeExpression = trigger.TimeExpression,
                SunEvent = trigger.SunEvent,
                OffsetMinutes = trigger.OffsetMinutes,
                SortOrder = trigger.SortOrder > 0 ? trigger.SortOrder : index
            });
        }
        
        // Replace conditions
        db.AutomationConditions.RemoveRange(entity.Conditions);
        entity.Conditions.Clear();
        foreach (var (condition, index) in request.Conditions.Select((c, i) => (c, i)))
        {
            entity.Conditions.Add(new AutomationConditionEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                ConditionType = condition.ConditionType.ToString(),
                DeviceId = condition.DeviceId,
                Property = condition.Property,
                Operator = condition.Operator?.ToString(),
                Value = AutomationEntityExtensions.SerializeValue(condition.Value),
                Value2 = AutomationEntityExtensions.SerializeValue(condition.Value2),
                TimeStart = condition.TimeStart,
                TimeEnd = condition.TimeEnd,
                DaysOfWeek = AutomationEntityExtensions.SerializeDaysOfWeek(condition.DaysOfWeek),
                SortOrder = condition.SortOrder > 0 ? condition.SortOrder : index
            });
        }
        
        // Replace actions
        db.AutomationActions.RemoveRange(entity.Actions);
        entity.Actions.Clear();
        foreach (var (action, index) in request.Actions.Select((a, i) => (a, i)))
        {
            entity.Actions.Add(new AutomationActionEntity
            {
                Id = Guid.NewGuid(),
                AutomationRuleId = entity.Id,
                ActionType = action.ActionType.ToString(),
                DeviceId = action.DeviceId,
                Property = action.Property,
                Value = AutomationEntityExtensions.SerializeValue(action.Value),
                DelaySeconds = action.DelaySeconds,
                WebhookUrl = action.WebhookUrl,
                WebhookMethod = action.WebhookMethod,
                WebhookBody = action.WebhookBody,
                NotificationTitle = action.NotificationTitle,
                NotificationMessage = action.NotificationMessage,
                SceneId = action.SceneId,
                SortOrder = action.SortOrder > 0 ? action.SortOrder : index
            });
        }
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Updated automation rule: {Name} (ID: {Id})", entity.Name, entity.Id);
        
        return entity.ToModel();
    }
    
    public async Task DeleteAutomationAsync(Guid id)
    {
        var entity = await db.AutomationRules.FindAsync(id)
            ?? throw new KeyNotFoundException($"Automation rule {id} not found");
        
        db.AutomationRules.Remove(entity);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Deleted automation rule: {Name} (ID: {Id})", entity.Name, entity.Id);
    }
    
    public async Task<AutomationRule> ToggleAutomationAsync(Guid id, bool enabled)
    {
        var entity = await db.AutomationRules
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Automation rule {id} not found");
        
        entity.IsEnabled = enabled;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Toggled automation rule: {Name} -> {Enabled}", entity.Name, enabled);
        
        return entity.ToModel();
    }
    
    public async Task<AutomationStats> GetStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        var automations = await db.AutomationRules.AsNoTracking().ToListAsync();
        var logsToday = await db.AutomationExecutionLogs
            .AsNoTracking()
            .Where(l => l.ExecutedAt >= today && l.ExecutedAt < tomorrow)
            .ToListAsync();
        
        return new AutomationStats(
            TotalAutomations: automations.Count,
            EnabledAutomations: automations.Count(a => a.IsEnabled),
            DisabledAutomations: automations.Count(a => !a.IsEnabled),
            TotalExecutionsToday: logsToday.Count,
            SuccessfulExecutionsToday: logsToday.Count(l => l.Status == "Success"),
            FailedExecutionsToday: logsToday.Count(l => l.Status == "Failure" || l.Status == "PartialFailure")
        );
    }
    
    // ===== Execution Logs =====
    
    public async Task<List<AutomationExecutionLog>> GetExecutionLogsAsync(Guid? automationId = null, int take = 100)
    {
        var query = db.AutomationExecutionLogs.AsNoTracking();
        
        if (automationId.HasValue)
        {
            query = query.Where(l => l.AutomationRuleId == automationId.Value);
        }
        
        return await query
            .OrderByDescending(l => l.ExecutedAt)
            .Take(take)
            .Select(l => l.ToModel())
            .ToListAsync();
    }
    
    public async Task LogExecutionAsync(AutomationExecutionLogEntity log)
    {
        db.AutomationExecutionLogs.Add(log);
        await db.SaveChangesAsync();
    }
    
    // ===== Scenes =====
    
    public async Task<List<Scene>> GetScenesAsync()
    {
        return await db.Scenes
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => s.ToModel())
            .ToListAsync();
    }
    
    public async Task<Scene?> GetSceneAsync(Guid id)
    {
        var entity = await db.Scenes.FindAsync(id);
        return entity?.ToModel();
    }
    
    public async Task<Scene> CreateSceneAsync(CreateSceneRequest request)
    {
        var entity = new SceneEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            Color = request.Color,
            DeviceStates = AutomationEntityExtensions.SerializeDeviceStates(request.DeviceStates),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        db.Scenes.Add(entity);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Created scene: {Name} (ID: {Id})", entity.Name, entity.Id);
        
        return entity.ToModel();
    }
    
    public async Task<Scene> UpdateSceneAsync(Guid id, UpdateSceneRequest request)
    {
        var entity = await db.Scenes.FindAsync(id)
            ?? throw new KeyNotFoundException($"Scene {id} not found");
        
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Icon = request.Icon;
        entity.Color = request.Color;
        entity.DeviceStates = AutomationEntityExtensions.SerializeDeviceStates(request.DeviceStates);
        entity.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Updated scene: {Name} (ID: {Id})", entity.Name, entity.Id);
        
        return entity.ToModel();
    }
    
    public async Task DeleteSceneAsync(Guid id)
    {
        var entity = await db.Scenes.FindAsync(id)
            ?? throw new KeyNotFoundException($"Scene {id} not found");
        
        db.Scenes.Remove(entity);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Deleted scene: {Name} (ID: {Id})", entity.Name, entity.Id);
    }
    
    public async Task ActivateSceneAsync(Guid id)
    {
        var scene = await GetSceneAsync(id)
            ?? throw new KeyNotFoundException($"Scene {id} not found");
        
        logger.LogInformation("Activating scene: {Name}", scene.Name);
        
        foreach (var (deviceId, state) in scene.DeviceStates)
        {
            try
            {
                await deviceService.SetDeviceStateAsync(deviceId, state);
                logger.LogDebug("Set device {DeviceId} state for scene {SceneName}", deviceId, scene.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to set device {DeviceId} state for scene {SceneName}", deviceId, scene.Name);
            }
        }
    }
    
    // ===== Rule Matching =====
    
    public async Task<List<AutomationRuleEntity>> GetRulesForDeviceTriggerAsync(string deviceId, string property)
    {
        return await db.AutomationRules
            .AsNoTracking()
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .Where(r => r.IsEnabled)
            .Where(r => r.Triggers.Any(t => 
                t.TriggerType == "DeviceState" && 
                t.DeviceId == deviceId && 
                (t.Property == property || t.Property == null)))
            .ToListAsync();
    }
    
    public async Task<List<AutomationRuleEntity>> GetTimeTriggerRulesAsync()
    {
        return await db.AutomationRules
            .AsNoTracking()
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .Where(r => r.IsEnabled)
            .Where(r => r.Triggers.Any(t => t.TriggerType == "Time"))
            .ToListAsync();
    }
    
    public async Task<List<AutomationRuleEntity>> GetSunTriggerRulesAsync(string sunEvent)
    {
        return await db.AutomationRules
            .AsNoTracking()
            .Include(r => r.Triggers)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .Where(r => r.IsEnabled)
            .Where(r => r.Triggers.Any(t => 
                (t.TriggerType == "Sunrise" || t.TriggerType == "Sunset") && 
                t.SunEvent == sunEvent))
            .ToListAsync();
    }
}
