using Microsoft.EntityFrameworkCore;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;
using RecoverX.Infrastructure.Data;

namespace RecoverX.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for FileRecord entities.
/// Demonstrates EF Core query patterns, async operations, and performance optimization.
/// </summary>
public class FileRecordRepository : IFileRecordRepository
{
    private readonly RecoverXDbContext _context;

    public FileRecordRepository(RecoverXDbContext context)
    {
        _context = context;
    }

    public async Task<FileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Include related entities for complete object graph
        return await _context.FileRecords
            .Include(f => f.RecoveryJobs)
            .Include(f => f.AuditLogs)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public IQueryable<FileRecord> GetAll()
    {
        // Return IQueryable for composability
        // Caller can add additional Where/OrderBy/Select before execution
        // This enables LINQ query optimization
        return _context.FileRecords.AsQueryable();
    }

    public async Task<List<FileRecord>> GetByStatusAsync(FileStatus status, CancellationToken cancellationToken = default)
    {
        // Use AsNoTracking for read-only queries - performance optimization
        // EF Core won't track changes, reducing memory overhead
        return await _context.FileRecords
            .AsNoTracking()
            .Where(f => f.Status == status)
            .OrderBy(f => f.FilePath)
            .ToListAsync(cancellationToken);
    }

    public async Task<FileRecord?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // FilePath has unique index, so this is efficient
        return await _context.FileRecords
            .FirstOrDefaultAsync(f => f.FilePath == filePath, cancellationToken);
    }

    public async Task<FileRecord> AddAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        // Add to context (not yet saved to database)
        await _context.FileRecords.AddAsync(fileRecord, cancellationToken);
        // Caller must call SaveChangesAsync through UnitOfWork
        return fileRecord;
    }

    public Task UpdateAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        // Mark entity as modified
        // UpdatedAt timestamp is set automatically in DbContext.SaveChangesAsync
        _context.FileRecords.Update(fileRecord);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        // Cascade delete will remove related RecoveryJobs
        // AuditLogs will have FileRecordId set to null (OnDelete.SetNull)
        _context.FileRecords.Remove(fileRecord);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<FileRecord> fileRecords, CancellationToken cancellationToken = default)
    {
        // Bulk update - more efficient than individual Update calls
        _context.FileRecords.UpdateRange(fileRecords);
        return Task.CompletedTask;
    }

    public async Task<Dictionary<FileStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        // Efficient aggregation query
        // Groups by status and counts, all in database
        return await _context.FileRecords
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }
}

/// <summary>
/// Repository implementation for RecoveryJob entities.
/// </summary>
public class RecoveryJobRepository : IRecoveryJobRepository
{
    private readonly RecoverXDbContext _context;

    public RecoveryJobRepository(RecoverXDbContext context)
    {
        _context = context;
    }

    public async Task<RecoveryJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Include FileRecord for complete job information
        return await _context.RecoveryJobs
            .Include(j => j.FileRecord)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public IQueryable<RecoveryJob> GetAll()
    {
        // Include FileRecord by default for most queries
        return _context.RecoveryJobs.Include(j => j.FileRecord);
    }

    public async Task<List<RecoveryJob>> GetByStatusAsync(RecoveryJobStatus status, CancellationToken cancellationToken = default)
    {
        // Order by priority (highest first), then FIFO within same priority
        return await _context.RecoveryJobs
            .Include(j => j.FileRecord)
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RecoveryJob>> GetByFileIdAsync(Guid fileRecordId, CancellationToken cancellationToken = default)
    {
        // Get all jobs for a file, ordered by most recent first
        return await _context.RecoveryJobs
            .Where(j => j.FileRecordId == fileRecordId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RecoveryJob> AddAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default)
    {
        await _context.RecoveryJobs.AddAsync(recoveryJob, cancellationToken);
        return recoveryJob;
    }

    public Task UpdateAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default)
    {
        _context.RecoveryJobs.Update(recoveryJob);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default)
    {
        _context.RecoveryJobs.Remove(recoveryJob);
        return Task.CompletedTask;
    }

    // Advanced Feature: Priority-based job fetching
    public async Task<RecoveryJob?> GetNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        // Critical query for RecoveryWorker performance
        // Uses composite index (Status, Priority DESC, CreatedAt)
        // Returns highest priority pending job
        return await _context.RecoveryJobs
            .Include(j => j.FileRecord)
            .Where(j => j.Status == RecoveryJobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<RecoveryJobStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RecoveryJobs
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }
}

/// <summary>
/// Repository implementation for AuditLog entities.
/// Append-only pattern - no Update or Delete operations.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly RecoverXDbContext _context;

    public AuditLogRepository(RecoverXDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Include(a => a.FileRecord)
            .Include(a => a.RecoveryJob)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public IQueryable<AuditLog> GetAll()
    {
        return _context.AuditLogs
            .Include(a => a.FileRecord)
            .AsQueryable();
    }

    public async Task<AuditLog> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        // Audit logs are append-only - never modified or deleted
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
        return auditLog;
    }

    public async Task<List<AuditLog>> GetByFileIdAsync(Guid fileRecordId, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.FileRecordId == fileRecordId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetBySeverityAsync(LogSeverity severity, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.Severity == severity)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        // Most recent logs first
        // Use AsNoTracking for read-only queries
        return await _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.FileRecord)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    // Advanced Feature: Time-range queries for incident investigation
    public async Task<List<AuditLog>> GetByTimeRangeAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        // Efficient time-range query using index on CreatedAt
        return await _context.AuditLogs
            .Where(a => a.CreatedAt >= startTime && a.CreatedAt <= endTime)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Repository implementation for Backup entities.
/// </summary>
public class BackupRepository : IBackupRepository
{
    private readonly RecoverXDbContext _context;

    public BackupRepository(RecoverXDbContext context)
    {
        _context = context;
    }

    public async Task<Backup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.RestoredFrom)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public IQueryable<Backup> GetAll()
    {
        return _context.Backups.AsQueryable();
    }

    public async Task<Backup> AddAsync(Backup backup, CancellationToken cancellationToken = default)
    {
        await _context.Backups.AddAsync(backup, cancellationToken);
        return backup;
    }

    public Task UpdateAsync(Backup backup, CancellationToken cancellationToken = default)
    {
        _context.Backups.Update(backup);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Backup backup, CancellationToken cancellationToken = default)
    {
        _context.Backups.Remove(backup);
        return Task.CompletedTask;
    }

    public async Task<Backup?> GetMostRecentAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Backup>> GetByTypeAsync(BackupType type, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Where(b => b.BackupType == type)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Unit of Work implementation.
/// Coordinates multiple repositories and manages transactions.
/// All repositories share the same DbContext instance.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly RecoverXDbContext _context;
    private IFileRecordRepository? _fileRecords;
    private IRecoveryJobRepository? _recoveryJobs;
    private IAuditLogRepository? _auditLogs;
    private IBackupRepository? _backups;

    public UnitOfWork(RecoverXDbContext context)
    {
        _context = context;
    }

    // Lazy initialization of repositories
    public IFileRecordRepository FileRecords => 
        _fileRecords ??= new FileRecordRepository(_context);

    public IRecoveryJobRepository RecoveryJobs => 
        _recoveryJobs ??= new RecoveryJobRepository(_context);

    public IAuditLogRepository AuditLogs => 
        _auditLogs ??= new AuditLogRepository(_context);

    public IBackupRepository Backups => 
        _backups ??= new BackupRepository(_context);

    /// <summary>
    /// Save all pending changes to database.
    /// Returns number of rows affected.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Begin explicit transaction for complex operations.
    /// Must be followed by CommitTransactionAsync or RollbackTransactionAsync.
    /// </summary>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Commit current transaction.
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.CommitTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Rollback current transaction.
    /// Discards all changes since BeginTransactionAsync.
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.RollbackTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Dispose of DbContext.
    /// Called automatically by DI container at end of request/scope.
    /// </summary>
    public void Dispose()
    {
        _context.Dispose();
    }
}