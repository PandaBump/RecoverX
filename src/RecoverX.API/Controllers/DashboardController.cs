using Microsoft.AspNetCore.Mvc;
using MediatR;
using RecoverX.Application.Queries;

namespace RecoverX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var query = new GetDashboardStatsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get recent audit logs
    /// </summary>
    [HttpGet("recent-logs")]
    public async Task<IActionResult> GetRecentLogs([FromQuery] int count = 50)
    {
        var query = new GetAuditLogsQuery { Take = count };
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}