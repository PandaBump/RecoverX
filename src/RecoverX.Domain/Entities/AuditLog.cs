namespace RecoverX.Domain.Entities;

/// <summary>
/// Immutable audit log entry for tracking all significant system events.
/// Provides complete traceability for compliance, debugging, and analysis.
/// Once created, audit logs are never modified (append-only pattern).
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Primary key for the audit entry
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Type of event being logged
    /// Examples: FileScan, CorruptionDetected, RecoveryStarted, RecoveryCompleted
    /// Used for filtering and categorizing events
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what happened
    /// Human-readable message explaining the event context
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the event
    /// Follows standard logging levels: Debug, Info, Warning, Error, Critical
    /// </summary>
    public LogSeverity Severity { get; set; } = LogSeverity.Info;

    /// <summary>
    /// Optional foreign key to associated file
    /// Null for system-wide events that don't relate to a specific file
    /// </summary>
    public Guid? FileRecordId { get; set; }

    /// <summary>
    /// Navigation property to the associated file (if any)
    /// Enables joining audit logs with file records for comprehensive history
    /// </summary>
    public FileRecord? FileRecord { get; set; }

    /// <summary>
    /// Optional foreign key to associated recovery job
    /// Links audit entries to specific recovery operations
    /// </summary>
    public Guid? RecoveryJobId { get; set; }

    /// <summary>
    /// Navigation property to associated recovery job (if any)
    /// </summary>
    public RecoveryJob? RecoveryJob { get; set; }

    /// <summary>
    /// When this event occurred
    /// Always stored in UTC for consistency across time zones
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional contextual data as JSON
    /// Flexible field for storing event-specific metadata
    /// Examples: stack traces, before/after values, system metrics
    /// </summary>
    public string? AdditionalData { get; set; }

    // Advanced Feature: User tracking for multi-tenant scenarios
    /// <summary>
    /// User or system process that triggered this event
    /// Example: "System.BackgroundWorker", "Admin.User", "API.FileScan"
    /// Useful for security auditing and responsibility tracking
    /// </summary>
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// IP address or machine name where event originated
    /// Useful for distributed system debugging
    /// </summary>
    public string? Source { get; set; }
}