using Microsoft.AspNetCore.Mvc;
using SDHome.Lib.Models;
using SDHome.Lib.Services;

namespace SDHome.Api.Controllers;

[ApiController]
[Route("/api/automations")]
public class AutomationsController(
    IAutomationService automationService,
    IEndToEndLatencyTracker e2eTracker) : ControllerBase
{
    // ===== Automation Rules =====
    
    /// <summary>
    /// Get all automation rules (summary view)
    /// </summary>
    [HttpGet]
    public async Task<List<AutomationSummary>> GetAutomations()
    {
        return await automationService.GetAutomationSummariesAsync();
    }
    
    /// <summary>
    /// Get automation statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<AutomationStats> GetStats()
    {
        return await automationService.GetStatsAsync();
    }
    
    /// <summary>
    /// Get a single automation rule with full details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutomationRule>> GetAutomation(Guid id)
    {
        var automation = await automationService.GetAutomationAsync(id);
        if (automation == null) return NotFound();
        return automation;
    }
    
    /// <summary>
    /// Create a new automation rule
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AutomationRule), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutomationRule>> CreateAutomation([FromBody] CreateAutomationRequest request)
    {
        try
        {
            var automation = await automationService.CreateAutomationAsync(request);
            return Ok(automation);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
    
    /// <summary>
    /// Update an existing automation rule
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AutomationRule>> UpdateAutomation(Guid id, [FromBody] UpdateAutomationRequest request)
    {
        try
        {
            var automation = await automationService.UpdateAutomationAsync(id, request);
            return automation;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    /// <summary>
    /// Delete an automation rule
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAutomation(Guid id)
    {
        try
        {
            await automationService.DeleteAutomationAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    /// <summary>
    /// Enable or disable an automation rule
    /// </summary>
    [HttpPatch("{id:guid}/toggle")]
    public async Task<ActionResult<AutomationRule>> ToggleAutomation(Guid id, [FromQuery] bool enabled)
    {
        try
        {
            var automation = await automationService.ToggleAutomationAsync(id, enabled);
            return automation;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    /// <summary>
    /// Manually trigger an automation (for testing)
    /// </summary>
    [HttpPost("{id:guid}/trigger")]
    public async Task<ActionResult> TriggerAutomation(Guid id)
    {
        var automation = await automationService.GetAutomationAsync(id);
        if (automation == null) return NotFound();
        
        // TODO: Trigger via AutomationEngine
        return Ok(new { message = "Automation triggered", automationId = id });
    }
    
    // ===== Execution Logs =====
    
    /// <summary>
    /// Get execution logs for all automations or a specific one
    /// </summary>
    [HttpGet("logs")]
    public async Task<List<AutomationExecutionLog>> GetExecutionLogs(
        [FromQuery] Guid? automationId = null,
        [FromQuery] int take = 100)
    {
        return await automationService.GetExecutionLogsAsync(automationId, take);
    }
    
    /// <summary>
    /// Get execution logs for a specific automation
    /// </summary>
    [HttpGet("{id:guid}/logs")]
    public async Task<List<AutomationExecutionLog>> GetAutomationLogs(Guid id, [FromQuery] int take = 50)
    {
        return await automationService.GetExecutionLogsAsync(id, take);
    }
    
    // ===== End-to-End Latency Tracking =====
    
    /// <summary>
    /// Get end-to-end latency timelines (completed automation cycles)
    /// </summary>
    [HttpGet("e2e/completed")]
    public ActionResult<IEnumerable<EndToEndTimeline>> GetCompletedTimelines([FromQuery] int take = 20)
    {
        return Ok(e2eTracker.GetCompletedTimelines(take));
    }
    
    /// <summary>
    /// Get pending (in-progress) automation cycles
    /// </summary>
    [HttpGet("e2e/pending")]
    public ActionResult<IEnumerable<EndToEndTimeline>> GetPendingTimelines()
    {
        return Ok(e2eTracker.GetPendingTimelines());
    }
}

[ApiController]
[Route("/api/scenes")]
public class ScenesController(IAutomationService automationService) : ControllerBase
{
    /// <summary>
    /// Get all scenes
    /// </summary>
    [HttpGet]
    public async Task<List<Scene>> GetScenes()
    {
        return await automationService.GetScenesAsync();
    }
    
    /// <summary>
    /// Get a single scene
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Scene>> GetScene(Guid id)
    {
        var scene = await automationService.GetSceneAsync(id);
        if (scene == null) return NotFound();
        return scene;
    }
    
    /// <summary>
    /// Create a new scene
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Scene>> CreateScene([FromBody] CreateSceneRequest request)
    {
        var scene = await automationService.CreateSceneAsync(request);
        return CreatedAtAction(nameof(GetScene), new { id = scene.Id }, scene);
    }
    
    /// <summary>
    /// Update a scene
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Scene>> UpdateScene(Guid id, [FromBody] UpdateSceneRequest request)
    {
        try
        {
            var scene = await automationService.UpdateSceneAsync(id, request);
            return scene;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    /// <summary>
    /// Delete a scene
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteScene(Guid id)
    {
        try
        {
            await automationService.DeleteSceneAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    /// <summary>
    /// Activate a scene (apply all device states)
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult> ActivateScene(Guid id)
    {
        try
        {
            await automationService.ActivateSceneAsync(id);
            return Ok(new { message = "Scene activated", sceneId = id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
