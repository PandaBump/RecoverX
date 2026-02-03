using MediatR;
using RecoverX.Application.DTOs;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;

namespace RecoverX.Application.Queries;

/// <summary>
/// Query to get file records with optional filtering and pagination.
/// Demonstrates LINQ, projections, and efficient data retrieval.
/// </summary>
public class GetFileRecordsQuery : IRequest<List<FileRecordDto>>
{
    public FileStatus? FilterByStatus { get; set; }
    public int? Take { get; set; }
    public int? Skip { get; set; }
    public string? SearchTerm { get; set; }
}

public class GetFileRecordsQueryHandler : IRequestHandler<GetFileRecordsQuery, List<FileRecordDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFileRecordsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<FileRecordDto>> Handle(GetFileRecordsQuery request, CancellationToken cancellationToken)
    {
        // Start with base query
        var query = _unitOfWork.FileRecords.GetAll();

        // Apply status filter if provided
        if (request.FilterByStatus.HasValue)
        {
            query = query.Where(f => f.Status == request.FilterByStatus.Value);
        }

        // Apply search term if provided
        // Search in file path (case-insensitive)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(f => f.FilePath.ToLower().Contains(searchLower));
        }

        // Apply pagination
        if (request.Skip.HasValue)
        {
            query = query.Skip(request.Skip.Value);
        }

        if (request.Take.HasValue)
        {
            query = query.Take(request.Take.Value);
        }

        // Project to DTO - this is where the magic happens
        // We use Select to avoid loading unnecessary data (optimization)
        var results = query.Select(f => new FileRecordDto
        {
            Id = f.Id,
            FilePath = f.FilePath,
            Hash = f.Hash,
            SizeInBytes = f.SizeInBytes,
            LastModified = f.LastModified,
            Status = f.Status.ToString(),
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt,
            RecoveryJobCount = f.RecoveryJobs.Count
        }).ToList();

        return results;
    }
}

/// <summary>
/// Query to get a single file record by ID with full details.
/// Includes navigation properties for related data.
/// </summary>
public class GetFileRecordByIdQuery : IRequest<FileRecordDto?>
{
    public Guid Id { get; set; }
}

public class GetFileRecordByIdQueryHandler : IRequestHandler<GetFileRecordByIdQuery, FileRecordDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFileRecordByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FileRecordDto?> Handle(GetFileRecordByIdQuery request, CancellationToken cancellationToken)
    {
        var fileRecord = await _unitOfWork.FileRecords.GetByIdAsync(request.Id, cancellationToken);

        if (fileRecord == null)
            return null;

        return new FileRecordDto
        {
            Id = fileRecord.Id,
            FilePath = fileRecord.FilePath,
            Hash = fileRecord.Hash,
            SizeInBytes = fileRecord.SizeInBytes,
            LastModified = fileRecord.LastModified,
            Status = fileRecord.Status.ToString(),
            CreatedAt = fileRecord.CreatedAt,
            UpdatedAt = fileRecord.UpdatedAt,
            RecoveryJobCount = fileRecord.RecoveryJobs.Count
        };
    }
}

/// <summary>
/// Query to get recovery jobs with optional filtering.
/// </summary>
public class GetRecoveryJobsQuery : IRequest<List<RecoveryJobDto>>
{
    public RecoveryJobStatus? FilterByStatus { get; set; }
    public int? Take { get; set; }
    public int? Skip { get; set; }
    public Guid? FileRecordId { get; set; }
}

public class GetRecoveryJobsQueryHandler : IRequestHandler<GetRecoveryJobsQuery, List<RecoveryJobDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRecoveryJobsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<RecoveryJobDto>> Handle(GetRecoveryJobsQuery request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.RecoveryJobs.GetAll();

        // Filter by status
        if (request.FilterByStatus.HasValue)
        {
            query = query.Where(j => j.Status == request.FilterByStatus.Value);
        }

        // Filter by file
        if (request.FileRecordId.HasValue)
        {
            query = query.Where(j => j.FileRecordId == request.FileRecordId.Value);
        }

        // Order by priority (highest first), then by creation time
        query = query.OrderByDescending(j => j.Priority)
                     .ThenBy(j => j.CreatedAt);

        // Apply pagination
        if (request.Skip.HasValue)
        {
            query = query.Skip(request.Skip.Value);
        }

        if (request.Take.HasValue)
        {
            query = query.Take(request.Take.Value);
        }

        // Project to DTO with file path included
        var results = query.Select(j => new RecoveryJobDto
        {
            Id = j.Id,
            FileRecordId = j.FileRecordId,
            Status = j.Status.ToString(),
            AttemptCount = j.AttemptCount,
            MaxAttempts = j.MaxAttempts,
            CreatedAt = j.CreatedAt,
            StartedAt = j.StartedAt,
            CompletedAt = j.CompletedAt,
            ErrorMessage = j.ErrorMessage,
            RecoveryMethod = j.RecoveryMethod,
            Priority = j.Priority,
            FilePath = j.FileRecord.FilePath
        }).ToList();

        return results;
    }
}

/// <summary>
/// Query to get audit logs with filtering and pagination.
/// Useful for debugging and compliance.
/// </summary>
public class GetAuditLogsQuery : IRequest<List<AuditLogDto>>
{
    public LogSeverity? FilterBySeverity { get; set; }
    public string? FilterByEventType { get; set; }
    public Guid? FileRecordId { get; set; }
    public int Take { get; set; } = 100;
    public int Skip { get; set; } = 0;
}

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLogDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAuditLogsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.AuditLogs.GetAll();

        // Apply filters
        if (request.FilterBySeverity.HasValue)
        {
            query = query.Where(a => a.Severity == request.FilterBySeverity.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.FilterByEventType))
        {
            query = query.Where(a => a.EventType == request.FilterByEventType);
        }

        if (request.FileRecordId.HasValue)
        {
            query = query.Where(a => a.FileRecordId == request.FileRecordId.Value);
        }

        // Order by most recent first
        query = query.OrderByDescending(a => a.CreatedAt);

        // Apply pagination
        query = query.Skip(request.Skip).Take(request.Take);

        // Project to DTO
        var results = query.Select(a => new AuditLogDto
        {
            Id = a.Id,
            EventType = a.EventType,
            Message = a.Message,
            Severity = a.Severity.ToString(),
            CreatedAt = a.CreatedAt,
            FilePath = a.FileRecord != null ? a.FileRecord.FilePath : null,
            TriggeredBy = a.TriggeredBy,
            Source = a.Source
        }).ToList();

        return results;
    }
}

/// <summary>
/// Query to get dashboard statistics.
/// Aggregates data from multiple sources for overview display.
/// Demonstrates complex LINQ aggregations and performance optimization.
/// </summary>
public class GetDashboardStatsQuery : IRequest<DashboardStatsDto>
{
}

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDashboardStatsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        // Get file status counts efficiently
        var fileStatusCounts = await _unitOfWork.FileRecords.GetStatusCountsAsync(cancellationToken);

        // Get recovery job status counts
        var jobStatusCounts = await _unitOfWork.RecoveryJobs.GetStatusCountsAsync(cancellationToken);

        // Get total storage size
        var totalStorage = _unitOfWork.FileRecords.GetAll().Sum(f => f.SizeInBytes);

        // Get last scan time from most recent audit log
        var lastScanLog = await _unitOfWork.AuditLogs.GetAll()
            .Where(a => a.EventType == "ScanCompleted")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardStatsDto
        {
            TotalFiles = fileStatusCounts.Values.Sum(),
            HealthyFiles = fileStatusCounts.GetValueOrDefault(FileStatus.Healthy, 0),
            CorruptedFiles = fileStatusCounts.GetValueOrDefault(FileStatus.Corrupted, 0),
            MissingFiles = fileStatusCounts.GetValueOrDefault(FileStatus.Missing, 0),
            RecoveringFiles = fileStatusCounts.GetValueOrDefault(FileStatus.Recovering, 0),
            ActiveRecoveryJobs = jobStatusCounts.GetValueOrDefault(RecoveryJobStatus.Running, 0),
            PendingRecoveryJobs = jobStatusCounts.GetValueOrDefault(RecoveryJobStatus.Pending, 0),
            CompletedRecoveryJobs = jobStatusCounts.GetValueOrDefault(RecoveryJobStatus.Completed, 0),
            FailedRecoveryJobs = jobStatusCounts.GetValueOrDefault(RecoveryJobStatus.Failed, 0) +
                                jobStatusCounts.GetValueOrDefault(RecoveryJobStatus.PermanentlyFailed, 0),
            TotalStorageInBytes = totalStorage,
            LastScanTime = lastScanLog?.CreatedAt ?? DateTime.MinValue
        };
    }
}

/// <summary>
/// Query to get backup information with filtering.
/// </summary>
public class GetBackupsQuery : IRequest<List<BackupDto>>
{
    public BackupType? FilterByType { get; set; }
    public int? Take { get; set; }
    public int? Skip { get; set; }
}

public class GetBackupsQueryHandler : IRequestHandler<GetBackupsQuery, List<BackupDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBackupsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<BackupDto>> Handle(GetBackupsQuery request, CancellationToken cancellationToken)
    {
        var query = _unitOfWork.Backups.GetAll();

        // Filter by type
        if (request.FilterByType.HasValue)
        {
            query = query.Where(b => b.BackupType == request.FilterByType.Value);
        }

        // Order by most recent first
        query = query.OrderByDescending(b => b.CreatedAt);

        // Apply pagination
        if (request.Skip.HasValue)
        {
            query = query.Skip(request.Skip.Value);
        }

        if (request.Take.HasValue)
        {
            query = query.Take(request.Take.Value);
        }

        // Project to DTO
        var results = query.Select(b => new BackupDto
        {
            Id = b.Id,
            BackupPath = b.BackupPath,
            CreatedAt = b.CreatedAt,
            FileCount = b.FileCount,
            TotalSizeInBytes = b.TotalSizeInBytes,
            BackupChecksum = b.BackupChecksum,
            Description = b.Description,
            BackupType = b.BackupType.ToString(),
            IsCompressed = b.IsCompressed,
            IsEncrypted = b.IsEncrypted,
            BackupFileSizeInBytes = b.BackupFileSizeInBytes
        }).ToList();

        return results;
    }
}