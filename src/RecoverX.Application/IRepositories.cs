using RecoverX.Domain.Entities;

namespace RecoverX.Application.Interfaces;

/// <summary>
/// Repository interface for FileRecord entities.
/// Abstracts data access logic following Repository pattern.
/// Enables testability and separation of concerns.
/// </summary>
public interface IFileRecordRepository
{
    /// <summary>
    /// Get a single file record by ID
    /// </summary>
    Task<FileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all file records with optional filtering
    /// Returns IQueryable for deferred execution and composability
    /// </summary>
    IQueryable<FileRecord> GetAll();

    /// <summary>
    /// Get file records by status
    /// Common query pattern for recovery workflows
    /// </summary>
    Task<List<FileRecord>> GetByStatusAsync(FileStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a file record by its file path
    /// Used during scanning to check if file is already tracked
    /// </summary>
    Task<FileRecord?> GetByFilePathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new file record to tracking
    /// </summary>
    Task<FileRecord> AddAsync(FileRecord fileRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing file record
    /// Automatically updates UpdatedAt timestamp
    /// </summary>
    Task UpdateAsync(FileRecord fileRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a file record
    /// Cascades to related recovery jobs and audit logs
    /// </summary>
    Task DeleteAsync(FileRecord fileRecord, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk update multiple file records
    /// More efficient than individual updates for batch operations
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<FileRecord> fileRecords, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of files by status
    /// Optimized for dashboard statistics
    /// </summary>
    Task<Dictionary<FileStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for RecoveryJob entities.
/// Manages recovery operation lifecycle.
/// </summary>
public interface IRecoveryJobRepository
{
    Task<RecoveryJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<RecoveryJob> GetAll();
    
    /// <summary>
    /// Get jobs by status, ordered by priority (highest first)
    /// Used by recovery worker to fetch next job
    /// </summary>
    Task<List<RecoveryJob>> GetByStatusAsync(RecoveryJobStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all jobs for a specific file
    /// Shows recovery history for audit purposes
    /// </summary>
    Task<List<RecoveryJob>> GetByFileIdAsync(Guid fileRecordId, CancellationToken cancellationToken = default);

    Task<RecoveryJob> AddAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default);
    Task UpdateAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default);
    Task DeleteAsync(RecoveryJob recoveryJob, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the next pending job to process
    /// Orders by priority DESC, then CreatedAt ASC (FIFO within same priority)
    /// </summary>
    Task<RecoveryJob?> GetNextPendingJobAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of jobs by status
    /// Dashboard statistics
    /// </summary>
    Task<Dictionary<RecoveryJobStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for AuditLog entities.
/// Append-only store for event tracking.
/// </summary>
public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<AuditLog> GetAll();

    /// <summary>
    /// Add a new audit log entry
    /// Audit logs are never updated or deleted (immutable)
    /// </summary>
    Task<AuditLog> AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get logs for a specific file
    /// Shows complete history of file events
    /// </summary>
    Task<List<AuditLog>> GetByFileIdAsync(Guid fileRecordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get logs by severity level
    /// Useful for filtering errors/warnings
    /// </summary>
    Task<List<AuditLog>> GetBySeverityAsync(LogSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent logs with pagination
    /// Most recent first for real-time monitoring
    /// </summary>
    Task<List<AuditLog>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    // Advanced Feature: Time-based log queries
    /// <summary>
    /// Get logs within a time range
    /// Useful for incident investigation
    /// </summary>
    Task<List<AuditLog>> GetByTimeRangeAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Backup entities.
/// Manages backup metadata and restoration points.
/// </summary>
public interface IBackupRepository
{
    Task<Backup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<Backup> GetAll();
    Task<Backup> AddAsync(Backup backup, CancellationToken cancellationToken = default);
    Task UpdateAsync(Backup backup, CancellationToken cancellationToken = default);
    Task DeleteAsync(Backup backup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get most recent backup
    /// Used to determine last backup time
    /// </summary>
    Task<Backup?> GetMostRecentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backups by type
    /// Filter full vs incremental vs differential
    /// </summary>
    Task<List<Backup>> GetByTypeAsync(BackupType type, CancellationToken cancellationToken = default);
}

/// <summary>
/// File system operations interface.
/// Abstracts file I/O for testability and flexibility.
/// Enables switching between local storage, cloud storage, etc.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Check if a file exists at the given path
    /// </summary>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Get file size in bytes
    /// </summary>
    Task<long> GetFileSizeAsync(string filePath);

    /// <summary>
    /// Get last modification timestamp
    /// </summary>
    Task<DateTime> GetLastModifiedAsync(string filePath);

    /// <summary>
    /// Compute SHA-256 hash of file content
    /// Used for integrity verification
    /// </summary>
    Task<string> ComputeHashAsync(string filePath);

    /// <summary>
    /// Read file content as bytes
    /// Used during recovery operations
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string filePath);

    /// <summary>
    /// Write bytes to file
    /// Used to restore file content
    /// </summary>
    Task WriteAllBytesAsync(string filePath, byte[] content);

    /// <summary>
    /// Copy file from source to destination
    /// Used in backup operations
    /// </summary>
    Task CopyFileAsync(string sourcePath, string destinationPath);

    /// <summary>
    /// Delete a file
    /// Used in cleanup operations
    /// </summary>
    Task DeleteFileAsync(string filePath);

    /// <summary>
    /// Scan a directory and return all file paths
    /// Recursive option to include subdirectories
    /// </summary>
    Task<List<string>> ScanDirectoryAsync(string directoryPath, bool recursive = true);

    // Advanced Feature: File encryption/decryption
    /// <summary>
    /// Encrypt file content using provided key
    /// Returns encrypted bytes
    /// </summary>
    Task<byte[]> EncryptFileAsync(string filePath, string encryptionKey);

    /// <summary>
    /// Decrypt file content using provided key
    /// Returns original bytes
    /// </summary>
    Task<byte[]> DecryptFileAsync(string filePath, string encryptionKey);
}

/// <summary>
/// Unit of Work pattern for transactional operations.
/// Ensures all database changes succeed or fail as a single unit.
/// Critical for data consistency in recovery operations.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Repository properties for accessing entities
    /// All repositories share the same DbContext instance
    /// </summary>
    IFileRecordRepository FileRecords { get; }
    IRecoveryJobRepository RecoveryJobs { get; }
    IAuditLogRepository AuditLogs { get; }
    IBackupRepository Backups { get; }

    /// <summary>
    /// Commit all pending changes to the database
    /// Returns number of rows affected
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a database transaction
    /// Enables explicit transaction control for complex operations
    /// Must call CommitTransactionAsync or RollbackTransactionAsync
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit current transaction
    /// Makes all changes permanent
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback current transaction
    /// Discards all changes since BeginTransactionAsync
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}