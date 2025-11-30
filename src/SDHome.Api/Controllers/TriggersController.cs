using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SDHome.Lib.Data;
using SDHome.Lib.Models;

namespace SDHome.Api.Controllers;

[ApiController]
[Route("/api/triggers")]
public class TriggersController(SignalsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<TriggerEvent>> GetRecentTriggers([FromQuery] int take = 100)
    {
        return await db.TriggerEvents
            .AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .Select(e => e.ToModel())
            .ToListAsync();
    }

    [HttpGet("{deviceId}")]
    public async Task<List<TriggerEvent>> GetTriggersForDevice(
        string deviceId,
        [FromQuery] int take = 100)
    {
        return await db.TriggerEvents
            .AsNoTracking()
            .Where(e => e.DeviceId == deviceId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .Select(e => e.ToModel())
            .ToListAsync();
    }
}

