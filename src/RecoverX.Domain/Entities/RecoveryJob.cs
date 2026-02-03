namespace RecoverX.Domain.Entities;

/// <summary>
/// Represents a single recovery operation for a file.
/// Tracks the entire lifecycle of attempting to restore or repair a file.
/// Implements retry logic and failure tracking for reliability.
/// </summary>
public class RecoveryJob
{
    /// <summary>
    /// Primary key for the recovery job
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the file being recovered
    /// </summary>
    public Guid FileRecordId { get; set; }

    /// <summary>
    /// Navigation property to the associated file record
    /// EF Core uses this for joins and includes
    /// </summary>
    public FileRecord FileRecord { get; set; } = null!;

    /// <summary>
    /// Current status of this recovery job
    /// Transitions: Pending → Running → Completed/Failed
    /// </summary>
    public RecoveryJobStatus Status { get; set; } = RecoveryJobStatus.Pending;

    /// <summary>
    /// Number of times this job has been attempted
    /// Used for exponential backoff and eventual failure determination
    /// Max attempts typically set to 3-5 in production
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Maximum allowed attempts before marking job as permanently failed
    /// Configurable per job for flexibility
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// When the job was created and queued
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job actually started processing
    /// Null if job is still pending
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the job finished (successfully or not)
    /// Null if job is still running or pending
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Detailed error message if the job failed
    /// Contains exception messages and stack traces for debugging
    /// Null if job succeeded or hasn't failed yet
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Recovery strategy used for this job
    /// Examples: "BackupRestore", "ChecksumRepair", "ManualIntervention"
    /// </summary>
    public string RecoveryMethod { get; set; } = "BackupRestore";

    // Advanced Feature: Priority-based job processing
    /// <summary>
    /// Priority level for job processing (1-10, where 10 is highest)
    /// Higher priority jobs are processed first by the background worker
    /// Useful for critical file recovery scenarios
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Calculated property: How long the job took to complete
    /// Returns null if job hasn't finished yet
    /// Useful for performance monitoring and SLA tracking
    /// </summary>
    public TimeSpan? Duration => 
        CompletedAt.HasValue && StartedAt.HasValue 
            ? CompletedAt.Value - StartedAt.Value 
            : null;

    /// <summary>
    /// Checks if the job has exhausted all retry attempts
    /// Used by the recovery worker to determine if job should be abandoned
    /// </summary>
    public bool IsMaxAttemptsReached => AttemptCount >= MaxAttempts;

    /// <summary>
    /// Checks if this job can be retried
    /// Job must be in Failed status and not have exceeded max attempts
    /// </summary>
    public bool CanRetry => Status == RecoveryJobStatus.Failed && !IsMaxAttemptsReached;
}