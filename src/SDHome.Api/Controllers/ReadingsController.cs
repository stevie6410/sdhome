using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SDHome.Lib.Data;
using SDHome.Lib.Models;

namespace SDHome.Api.Controllers;

[ApiController]
[Route("/api/readings")]
public class ReadingsController(SignalsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<SensorReading>> GetRecentReadings([FromQuery] int take = 100)
    {
        return await db.SensorReadings
            .AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .Select(e => e.ToModel())
            .ToListAsync();
    }

    [HttpGet("{deviceId}/{metric}")]
    public async Task<List<SensorReading>> GetReadingsForDeviceAndMetric(
        string deviceId,
        string metric,
        [FromQuery] int take = 100)
    {
        return await db.SensorReadings
            .AsNoTracking()
            .Where(e => e.DeviceId == deviceId && e.Metric == metric)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .Select(e => e.ToModel())
            .ToListAsync();
    }
}

