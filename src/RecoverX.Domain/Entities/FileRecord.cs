namespace RecoverX.Domain.Entities;

/// <summary>
/// Represents a tracked file in the system with its metadata and integrity information.
/// This is the core entity that drives the entire recovery workflow.
/// </summary>
public class FileRecord
{
    /// <summary>
    /// Primary key - uniquely identifies each file record
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Full path to the file on the filesystem
    /// Example: C:\DataStore\Documents\report.pdf
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the file content - used for integrity verification
    /// Computed when file is first scanned and compared during integrity checks
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes - used for quick corruption detection
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Last modification timestamp from the filesystem
    /// Used to detect if a file has been modified since last scan
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Current status of the file (Healthy, Missing, Corrupted, Recovering)
    /// Drives business logic for recovery workflows
    /// </summary>
    public FileStatus Status { get; set; } = FileStatus.Healthy;

    /// <summary>
    /// When this record was created in the database
    /// Useful for auditing and tracking file discovery timeline
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated
    /// Automatically updated by the infrastructure layer on changes
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties for EF Core relationships

    /// <summary>
    /// All recovery jobs associated with this file
    /// One-to-many relationship: one file can have multiple recovery attempts
    /// </summary>
    public ICollection<RecoveryJob> RecoveryJobs { get; set; } = new List<RecoveryJob>();

    /// <summary>
    /// All audit logs related to this file's lifecycle
    /// Tracks every significant event (scanned, corrupted, recovered, etc.)
    /// </summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}