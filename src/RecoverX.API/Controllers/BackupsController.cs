using Microsoft.AspNetCore.Mvc;
using MediatR;
using RecoverX.Application.Commands;
using RecoverX.Application.Queries;
using RecoverX.Domain.Entities;

namespace RecoverX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BackupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all backups
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBackups(
        [FromQuery] string? type = null,
        [FromQuery] int? take = null,
        [FromQuery] int? skip = null)
    {
        BackupType? filterType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<BackupType>(type, out var parsed))
        {
            filterType = parsed;
        }

        var query = new GetBackupsQuery
        {
            FilterByType = filterType,
            Take = take,
            Skip = skip
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a new backup
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBackup([FromBody] CreateBackupCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetBackups), new { id = result.BackupId }, result);
    }
}