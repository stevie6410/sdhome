using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SDHome.Lib.Data;
using SDHome.Lib.Models;

namespace SDHome.Api.Controllers;

[ApiController]
[Route("/api/signals")]
public class SignalsController(SignalsDbContext db) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<List<SignalEvent>> GetSignalLogs([FromQuery] int take = 100)
    {
        return await db.SignalEvents
            .AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .Take(take)
            .Select(e => e.ToModel())
            .ToListAsync();
    }
}

