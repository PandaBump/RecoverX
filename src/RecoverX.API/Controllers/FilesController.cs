using Microsoft.AspNetCore.Mvc;
using MediatR;
using RecoverX.Application.Commands;
using RecoverX.Application.Queries;
using RecoverX.Domain.Entities;

namespace RecoverX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IMediator mediator, ILogger<FilesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all file records with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFiles(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int? take = null,
        [FromQuery] int? skip = null)
    {
        FileStatus? filterStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<FileStatus>(status, out var parsed))
        {
            filterStatus = parsed;
        }

        var query = new GetFileRecordsQuery
        {
            FilterByStatus = filterStatus,
            SearchTerm = search,
            Take = take,
            Skip = skip
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get a single file record by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFileById(Guid id)
    {
        var query = new GetFileRecordByIdQuery { Id = id };
        var result = await _mediator.Send(query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Scan a directory and register files
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanDirectory([FromBody] ScanDirectoryCommand command)
    {
        _logger.LogInformation("Starting directory scan: {Path}", command.DirectoryPath);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Check integrity of all tracked files
    /// </summary>
    [HttpPost("check-integrity")]
    public async Task<IActionResult> CheckIntegrity([FromBody] CheckIntegrityCommand command)
    {
        _logger.LogInformation("Starting integrity check");
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}