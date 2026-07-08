using Spotster.DTOs;
using Spotster.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Spotster.Controllers;

/// <summary>Public system metrics (users, reports, moderation counts).</summary>
[ApiController]
[Route("api/stats")]
[ApiExplorerSettings(GroupName = "Stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>Aggregate counts for the last 24 hours and active listings.</summary>
    [HttpGet("system")]
    [EnableRateLimiting("stats")]
    [ProducesResponseType(typeof(SystemStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatsDto>> GetSystemStats()
    {
        var stats = await _statsService.GetSystemStatsAsync();
        return Ok(stats);
    }
}
