namespace RecoverX.Domain.Entities;

/// <summary>
/// Represents the current health status of a tracked file.
/// Used to drive recovery workflows and reporting.
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// File exists, matches expected hash, no issues detected
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// File exists but hash doesn't match expected value
    /// Indicates data corruption or unauthorized modification
    /// </summary>
    Corrupted = 1,

    /// <summary>
    /// File doesn't exist at expected path
    /// May have been deleted or moved
    /// </summary>
    Missing = 2,

    /// <summary>
    /// File is currently being recovered by a recovery job
    /// Temporary state during active recovery operations
    /// </summary>
    Recovering = 3,

    /// <summary>
    /// File was quarantined due to suspected security issues
    /// Requires manual review before restoration
    /// </summary>
    Quarantined = 4
}

/// <summary>
/// Represents the lifecycle status of a recovery job.
/// Follows a state machine pattern for job processing.
/// </summary>
public enum RecoveryJobStatus
{
    /// <summary>
    /// Job created but not yet picked up by worker
    /// Initial state for all new jobs
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Job is actively being processed by a worker thread
    /// </summary>
    Running = 1,

    /// <summary>
    /// Job completed successfully
    /// File should now be in Healthy status
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Job failed but may be retried
    /// Check AttemptCount against MaxAttempts
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Job was manually cancelled by operator
    /// Will not be automatically retried
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Job failed all retry attempts
    /// Requires manual intervention
    /// </summary>
    PermanentlyFailed = 5
}

/// <summary>
/// Severity levels for audit log entries.
/// Matches standard logging frameworks (Serilog, NLog, etc.)
/// </summary>
public enum LogSeverity
{
    /// <summary>
    /// Detailed information for troubleshooting
    /// Typically only logged in development
    /// </summary>
    Debug = 0,

    /// <summary>
    /// General informational messages
    /// Normal operations and state transitions
    /// </summary>
    Info = 1,

    /// <summary>
    /// Potential issues that don't prevent operation
    /// Examples: retry attempts, performance degradation
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Errors that prevent a specific operation
    /// Examples: failed recovery job, file access denied
    /// </summary>
    Error = 3,

    /// <summary>
    /// Severe errors that may affect system stability
    /// Examples: database connection lost, storage full
    /// </summary>
    Critical = 4
}

/// <summary>
/// Types of backup operations supported by the system.
/// Different strategies offer trade-offs between speed and completeness.
/// </summary>
public enum BackupType
{
    /// <summary>
    /// Complete snapshot of all file records
    /// Slowest but provides complete restore point
    /// </summary>
    Full = 0,

    /// <summary>
    /// Only records changed since last backup (any type)
    /// Fast but requires backup chain for full restore
    /// </summary>
    Incremental = 1,

    /// <summary>
    /// Records changed since last full backup
    /// Faster than full, easier to restore than incremental
    /// </summary>
    Differential = 2
}