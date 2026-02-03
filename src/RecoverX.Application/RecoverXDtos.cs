namespace RecoverX.Application.DTOs;

/// <summary>
/// Data Transfer Object for file records.
/// Used to transfer file data between layers without exposing domain entities.
/// Prevents over-fetching and provides a stable contract for API consumers.
/// </summary>
public class FileRecordDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Formatted file size for human readability
    /// Example: "1.5 MB" instead of "1572864"
    /// </summary>
    public string FormattedSize => FormatBytes(SizeInBytes);

    /// <summary>
    /// Count of associated recovery jobs
    /// Useful for UI without fetching full navigation property
    /// </summary>
    public int RecoveryJobCount { get; set; }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// DTO for recovery job information.
/// Includes calculated fields and formatted data for presentation.
/// </summary>
public class RecoveryJobDto
{
    public Guid Id { get; set; }
    public Guid FileRecordId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string RecoveryMethod { get; set; } = string.Empty;
    public int Priority { get; set; }

    /// <summary>
    /// File path from associated FileRecord
    /// Denormalized for convenience in lists/grids
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Duration in human-readable format
    /// Example: "2m 35s" or "Not completed"
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            if (!StartedAt.HasValue) return "Not started";
            if (!CompletedAt.HasValue) return "In progress";

            var duration = CompletedAt.Value - StartedAt.Value;
            if (duration.TotalSeconds < 60)
                return $"{duration.TotalSeconds:F1}s";
            if (duration.TotalMinutes < 60)
                return $"{duration.TotalMinutes:F1}m";
            return $"{duration.TotalHours:F1}h";
        }
    }

    /// <summary>
    /// Progress percentage (0-100)
    /// Simplified calculation based on attempts
    /// </summary>
    public int ProgressPercentage
    {
        get
        {
            if (Status == "Completed") return 100;
            if (Status == "Failed" || Status == "PermanentlyFailed") return 0;
            if (Status == "Pending") return 0;
            // Running: show progress based on attempts
            return Math.Min(90, (AttemptCount * 100) / MaxAttempts);
        }
    }
}

/// <summary>
/// DTO for audit log entries.
/// Simplified view for displaying event history.
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? FilePath { get; set; }
    public string? TriggeredBy { get; set; }
    public string? Source { get; set; }

    /// <summary>
    /// Relative time description for recent events
    /// Example: "2 minutes ago", "3 hours ago"
    /// </summary>
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return CreatedAt.ToString("MMM dd, yyyy");
        }
    }
}

/// <summary>
/// DTO for backup information.
/// Includes metadata for backup management UI.
/// </summary>
public class BackupDto
{
    public Guid Id { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int FileCount { get; set; }
    public long TotalSizeInBytes { get; set; }
    public string BackupChecksum { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
    public long BackupFileSizeInBytes { get; set; }

    /// <summary>
    /// Formatted backup file size
    /// </summary>
    public string FormattedBackupSize => FormatBytes(BackupFileSizeInBytes);

    /// <summary>
    /// Formatted total tracked files size
    /// </summary>
    public string FormattedTotalSize => FormatBytes(TotalSizeInBytes);

    /// <summary>
    /// Compression ratio if backup is compressed
    /// Shows how much space was saved
    /// </summary>
    public double? CompressionRatio => 
        IsCompressed && TotalSizeInBytes > 0 
            ? (double)BackupFileSizeInBytes / TotalSizeInBytes 
            : null;

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Dashboard statistics DTO.
/// Aggregated metrics for overview displays.
/// </summary>
public class DashboardStatsDto
{
    public int TotalFiles { get; set; }
    public int HealthyFiles { get; set; }
    public int CorruptedFiles { get; set; }
    public int MissingFiles { get; set; }
    public int RecoveringFiles { get; set; }
    public int ActiveRecoveryJobs { get; set; }
    public int PendingRecoveryJobs { get; set; }
    public int CompletedRecoveryJobs { get; set; }
    public int FailedRecoveryJobs { get; set; }
    public long TotalStorageInBytes { get; set; }
    public DateTime LastScanTime { get; set; }

    /// <summary>
    /// Percentage of files in healthy state
    /// Key metric for system health
    /// </summary>
    public double HealthPercentage => 
        TotalFiles > 0 ? (double)HealthyFiles / TotalFiles * 100 : 0;

    /// <summary>
    /// Success rate of recovery operations
    /// </summary>
    public double RecoverySuccessRate
    {
        get
        {
            var total = CompletedRecoveryJobs + FailedRecoveryJobs;
            return total > 0 ? (double)CompletedRecoveryJobs / total * 100 : 0;
        }
    }
}