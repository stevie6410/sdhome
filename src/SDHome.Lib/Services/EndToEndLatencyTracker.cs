using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SDHome.Lib.Services;

/// <summary>
/// Represents a complete end-to-end automation execution timeline
/// </summary>
public class EndToEndTimeline
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string TriggerDeviceId { get; set; } = string.Empty;
    public string? TargetDeviceId { get; set; }
    public string? AutomationName { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    
    /// <summary>
    /// Total end-to-end latency from trigger received to target device confirmation
    /// </summary>
    public double? TotalE2EMs { get; set; }
    
    /// <summary>
    /// Breakdown of timing stages
    /// </summary>
    public EndToEndStages Stages { get; set; } = new();
    
    /// <summary>
    /// Pipeline stages from signal processing (set when we know them)
    /// </summary>
    public PipelineStagesSnapshot? PipelineSnapshot { get; set; }
    
    /// <summary>
    /// Is this timeline complete (target device responded)?
    /// </summary>
    public bool IsComplete => CompletedAtUtc.HasValue;
}

/// <summary>
/// Snapshot of pipeline stages for inclusion in E2E timeline
/// </summary>
public class PipelineStagesSnapshot
{
    public double ParseMs { get; set; }
    public double DatabaseMs { get; set; }
    public double BroadcastMs { get; set; }
}

public class EndToEndStages
{
    // Stage 1: Signal arrives at our app
    public double? SignalReceivedMs { get; set; }
    
    // Stage 2: Parse + DB save
    public double? ParseAndDbMs { get; set; }
    
    // Stage 3: Automation rule lookup + evaluation
    public double? AutomationLookupMs { get; set; }
    
    // Stage 4: Action execution (MQTT publish)
    public double? ActionExecutionMs { get; set; }
    
    // Stage 5: Time until target device confirms state change
    // This includes: MQTT to broker → Z2M → Zigbee radio → device → Zigbee radio → Z2M → MQTT → our app
    public double? TargetDeviceResponseMs { get; set; }
    
    // Calculated: Zigbee/radio overhead (Stage 5 minus our processing)
    public double? EstimatedRadioOverheadMs => TargetDeviceResponseMs.HasValue 
        ? TargetDeviceResponseMs.Value 
        : null;
}

/// <summary>
/// Tracks end-to-end latency of automation executions
/// </summary>
public interface IEndToEndLatencyTracker
{
    /// <summary>
    /// Start tracking an automation execution with optional pipeline stages snapshot
    /// </summary>
    string StartTracking(
        string triggerDeviceId, 
        string? automationName = null, 
        string? targetDeviceId = null,
        PipelineStagesSnapshot? pipelineSnapshot = null);
    
    /// <summary>
    /// Record when automation rule lookup completed
    /// </summary>
    void RecordAutomationLookup(string trackingId, double durationMs);
    
    /// <summary>
    /// Record when action execution completed (MQTT command sent)
    /// </summary>
    void RecordActionExecution(string trackingId, double durationMs, string targetDeviceId);
    
    /// <summary>
    /// Record when target device state change is received (completes the timeline)
    /// </summary>
    void RecordTargetDeviceResponse(string targetDeviceId);
    
    /// <summary>
    /// Get all pending (incomplete) timelines
    /// </summary>
    IEnumerable<EndToEndTimeline> GetPendingTimelines();
    
    /// <summary>
    /// Get recent completed timelines
    /// </summary>
    IEnumerable<EndToEndTimeline> GetCompletedTimelines(int count = 20);
}

public class EndToEndLatencyTracker : IEndToEndLatencyTracker
{
    private readonly ILogger<EndToEndLatencyTracker> _logger;
    private readonly IRealtimeEventBroadcaster _broadcaster;
    
    // Active timelines indexed by tracking ID
    private readonly ConcurrentDictionary<string, EndToEndTimeline> _activeTimelines = new();
    
    // Pending timelines waiting for target device response, indexed by target device ID
    private readonly ConcurrentDictionary<string, List<(string TrackingId, long ActionSentTimestamp)>> _pendingByDevice = new();
    
    // Completed timelines (circular buffer)
    private readonly ConcurrentQueue<EndToEndTimeline> _completedTimelines = new();
    private const int MaxCompletedTimelines = 100;
    
    public EndToEndLatencyTracker(
        ILogger<EndToEndLatencyTracker> logger,
        IRealtimeEventBroadcaster broadcaster)
    {
        _logger = logger;
        _broadcaster = broadcaster;
    }
    
    public string StartTracking(
        string triggerDeviceId, 
        string? automationName = null, 
        string? targetDeviceId = null,
        PipelineStagesSnapshot? pipelineSnapshot = null)
    {
        var timeline = new EndToEndTimeline
        {
            TriggerDeviceId = triggerDeviceId,
            AutomationName = automationName,
            TargetDeviceId = targetDeviceId,
            PipelineSnapshot = pipelineSnapshot,
            Stages = new EndToEndStages
            {
                SignalReceivedMs = 0 // This is our t=0
            }
        };
        
        _activeTimelines[timeline.Id] = timeline;
        
        _logger.LogDebug("📊 E2E: Started tracking {TrackingId} for trigger from {DeviceId}", 
            timeline.Id, triggerDeviceId);
        
        return timeline.Id;
    }
    
    public void RecordAutomationLookup(string trackingId, double durationMs)
    {
        if (_activeTimelines.TryGetValue(trackingId, out var timeline))
        {
            timeline.Stages.AutomationLookupMs = durationMs;
            _logger.LogDebug("📊 E2E: {TrackingId} automation lookup took {Ms:F1}ms", trackingId, durationMs);
        }
    }
    
    public void RecordActionExecution(string trackingId, double durationMs, string targetDeviceId)
    {
        if (_activeTimelines.TryGetValue(trackingId, out var timeline))
        {
            timeline.Stages.ActionExecutionMs = durationMs;
            timeline.TargetDeviceId = targetDeviceId;
            
            // Register this timeline as waiting for target device response
            var pending = _pendingByDevice.GetOrAdd(targetDeviceId, _ => new List<(string, long)>());
            lock (pending)
            {
                pending.Add((trackingId, Stopwatch.GetTimestamp()));
            }
            
            _logger.LogDebug("📊 E2E: {TrackingId} action sent to {TargetDevice} in {Ms:F1}ms, waiting for response", 
                trackingId, targetDeviceId, durationMs);
            
            // Set a timeout to complete the timeline if device doesn't respond
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                if (_activeTimelines.TryGetValue(trackingId, out var t) && !t.IsComplete)
                {
                    CompleteTimeline(trackingId, timedOut: true);
                }
            });
        }
    }
    
    public void RecordTargetDeviceResponse(string targetDeviceId)
    {
        var responseTimestamp = Stopwatch.GetTimestamp();
        
        if (_pendingByDevice.TryGetValue(targetDeviceId, out var pending))
        {
            List<(string TrackingId, long ActionSentTimestamp)> toProcess;
            lock (pending)
            {
                toProcess = pending.ToList();
                pending.Clear();
            }
            
            foreach (var (trackingId, actionSentTimestamp) in toProcess)
            {
                var responseTime = Stopwatch.GetElapsedTime(actionSentTimestamp);
                
                if (_activeTimelines.TryGetValue(trackingId, out var timeline))
                {
                    timeline.Stages.TargetDeviceResponseMs = responseTime.TotalMilliseconds;
                    CompleteTimeline(trackingId, timedOut: false);
                    
                    _logger.LogInformation(
                        "📊 E2E Complete: {TrackingId} | Trigger: {TriggerDevice} → Target: {TargetDevice} | " +
                        "Response: {ResponseMs:F0}ms | Total E2E: {TotalMs:F0}ms",
                        trackingId, 
                        timeline.TriggerDeviceId, 
                        targetDeviceId,
                        responseTime.TotalMilliseconds,
                        timeline.TotalE2EMs);
                }
            }
        }
    }
    
    private void CompleteTimeline(string trackingId, bool timedOut)
    {
        if (_activeTimelines.TryRemove(trackingId, out var timeline))
        {
            timeline.CompletedAtUtc = DateTime.UtcNow;
            
            // Calculate total E2E time
            timeline.TotalE2EMs = (timeline.CompletedAtUtc.Value - timeline.StartedAtUtc).TotalMilliseconds;
            
            if (timedOut)
            {
                _logger.LogWarning("📊 E2E: {TrackingId} timed out waiting for target device response", trackingId);
            }
            
            // Add to completed queue
            _completedTimelines.Enqueue(timeline);
            
            // Trim queue if needed
            while (_completedTimelines.Count > MaxCompletedTimelines)
            {
                _completedTimelines.TryDequeue(out _);
            }
            
            // Broadcast the completed timeline
            _ = BroadcastTimelineAsync(timeline);
        }
    }
    
    private async Task BroadcastTimelineAsync(EndToEndTimeline timeline)
    {
        try
        {
            // Convert to PipelineTimeline format for the existing UI
            // Include pipeline stages if available for a complete E2E picture
            var pipelineTimeline = new PipelineTimeline
            {
                Id = timeline.Id,
                DeviceId = timeline.TriggerDeviceId,
                AutomationName = timeline.AutomationName,
                TimestampUtc = timeline.StartedAtUtc,
                Stages = new List<PipelineStage>()
            };
            
            double offset = 0;
            
            // Add pipeline stages first if we have them (Parse, DB, Broadcast)
            if (timeline.PipelineSnapshot != null)
            {
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "Parse",
                    DurationMs = timeline.PipelineSnapshot.ParseMs,
                    StartOffsetMs = offset,
                    Category = "signal"
                });
                offset += timeline.PipelineSnapshot.ParseMs;
                
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "Database",
                    DurationMs = timeline.PipelineSnapshot.DatabaseMs,
                    StartOffsetMs = offset,
                    Category = "db"
                });
                offset += timeline.PipelineSnapshot.DatabaseMs;
                
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "Broadcast",
                    DurationMs = timeline.PipelineSnapshot.BroadcastMs,
                    StartOffsetMs = offset,
                    Category = "broadcast"
                });
                offset += timeline.PipelineSnapshot.BroadcastMs;
            }
            
            // Add automation stages
            if (timeline.Stages.AutomationLookupMs.HasValue)
            {
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "RuleLookup",
                    DurationMs = timeline.Stages.AutomationLookupMs.Value,
                    StartOffsetMs = offset,
                    Category = "automation"
                });
                offset += timeline.Stages.AutomationLookupMs.Value;
            }
            
            if (timeline.Stages.ActionExecutionMs.HasValue)
            {
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "ActionExec",
                    DurationMs = timeline.Stages.ActionExecutionMs.Value,
                    StartOffsetMs = offset,
                    Category = "mqtt"
                });
                offset += timeline.Stages.ActionExecutionMs.Value;
            }
            
            if (timeline.Stages.TargetDeviceResponseMs.HasValue)
            {
                pipelineTimeline.Stages.Add(new PipelineStage
                {
                    Name = "ZigbeeRoundTrip",
                    DurationMs = timeline.Stages.TargetDeviceResponseMs.Value,
                    StartOffsetMs = offset,
                    Category = "zigbee", // Radio/Zigbee layer
                    IsSuccess = true
                });
                offset += timeline.Stages.TargetDeviceResponseMs.Value;
            }
            
            // Calculate total from all stages
            pipelineTimeline.TotalMs = offset;
            
            await _broadcaster.BroadcastPipelineTimelineAsync(pipelineTimeline);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast E2E timeline");
        }
    }
    
    public IEnumerable<EndToEndTimeline> GetPendingTimelines()
    {
        return _activeTimelines.Values.ToList();
    }
    
    public IEnumerable<EndToEndTimeline> GetCompletedTimelines(int count = 20)
    {
        return _completedTimelines.Reverse().Take(count).ToList();
    }
}
