using Spotster.DTOs;
using Spotster.Services;
using Microsoft.AspNetCore.Mvc;

namespace Spotster.Controllers;

[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("system")]
    public async Task<ActionResult<SystemStatsDto>> GetSystemStats()
    {
        var stats = await _statsService.GetSystemStatsAsync();
        return Ok(stats);
    }
}
