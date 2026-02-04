using Microsoft.AspNetCore.Mvc;
using MediatR;
using RecoverX.Application.Queries;
using RecoverX.Domain.Entities;

namespace RecoverX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecoveryController : ControllerBase
{
    private readonly IMediator _mediator;

    public RecoveryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get recovery jobs with optional filtering
    /// </summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> GetRecoveryJobs(
        [FromQuery] string? status = null,
        [FromQuery] Guid? fileId = null,
        [FromQuery] int? take = null,
        [FromQuery] int? skip = null)
    {
        RecoveryJobStatus? filterStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<RecoveryJobStatus>(status, out var parsed))
        {
            filterStatus = parsed;
        }

        var query = new GetRecoveryJobsQuery
        {
            FilterByStatus = filterStatus,
            FileRecordId = fileId,
            Take = take,
            Skip = skip
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }
}