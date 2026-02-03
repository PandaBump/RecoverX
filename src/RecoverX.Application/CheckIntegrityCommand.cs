using MediatR;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;

namespace RecoverX.Application.Commands;

/// <summary>
/// Command to check integrity of all tracked files.
/// Compares database records against actual filesystem state.
/// </summary>
public class CheckIntegrityCommand : IRequest<IntegrityCheckResult>
{
    /// <summary>
    /// Whether to automatically queue recovery jobs for problematic files
    /// </summary>
    public bool AutoQueueRecovery { get; set; } = true;

    /// <summary>
    /// Optional: Only check files with specific status
    /// </summary>
    public FileStatus? FilterByStatus { get; set; }
}

/// <summary>
/// Result of integrity check operation.
/// Shows what issues were found and what actions were taken.
/// </summary>
public class IntegrityCheckResult
{
    public int TotalFilesChecked { get; set; }
    public int HealthyFiles { get; set; }
    public int CorruptedFiles { get; set; }
    public int MissingFiles { get; set; }
    public int RecoveryJobsQueued { get; set; }
    public List<string> IssuesFound { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime CheckCompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Handler for integrity checking.
/// This is where the core "data recovery detection" logic lives.
/// Demonstrates LINQ queries, file I/O, and complex business rules.
/// </summary>
public class CheckIntegrityCommandHandler : IRequestHandler<CheckIntegrityCommand, IntegrityCheckResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileSystemService _fileSystem;

    public CheckIntegrityCommandHandler(IUnitOfWork unitOfWork, IFileSystemService fileSystem)
    {
        _unitOfWork = unitOfWork;
        _fileSystem = fileSystem;
    }

    public async Task<IntegrityCheckResult> Handle(CheckIntegrityCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var result = new IntegrityCheckResult();

        try
        {
            // Log integrity check started
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "IntegrityCheckStarted",
                Message = "Started integrity check of all tracked files",
                Severity = LogSeverity.Info,
                Source = "CheckIntegrityCommand",
                TriggeredBy = "System"
            }, cancellationToken);

            // Get files to check (with optional filtering)
            var filesToCheck = request.FilterByStatus.HasValue
                ? await _unitOfWork.FileRecords.GetByStatusAsync(request.FilterByStatus.Value, cancellationToken)
                : _unitOfWork.FileRecords.GetAll().ToList();

            result.TotalFilesChecked = filesToCheck.Count;

            // Check each file's integrity
            foreach (var fileRecord in filesToCheck)
            {
                try
                {
                    await CheckFileIntegrityAsync(fileRecord, request.AutoQueueRecovery, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    result.IssuesFound.Add($"{fileRecord.FilePath}: Check failed - {ex.Message}");
                }
            }

            // Save all changes (file status updates and recovery jobs)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Log check completed
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "IntegrityCheckCompleted",
                Message = $"Integrity check completed. Checked: {result.TotalFilesChecked}, Healthy: {result.HealthyFiles}, Corrupted: {result.CorruptedFiles}, Missing: {result.MissingFiles}, Jobs Queued: {result.RecoveryJobsQueued}",
                Severity = result.CorruptedFiles > 0 || result.MissingFiles > 0 ? LogSeverity.Warning : LogSeverity.Info,
                Source = "CheckIntegrityCommand",
                TriggeredBy = "System"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "IntegrityCheckFailed",
                Message = $"Integrity check failed: {ex.Message}",
                Severity = LogSeverity.Critical,
                Source = "CheckIntegrityCommand"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    /// <summary>
    /// Check a single file's integrity against database record.
    /// This method demonstrates the core data recovery detection logic:
    /// 1. Check if file exists
    /// 2. If exists, verify hash matches
    /// 3. Update status accordingly
    /// 4. Queue recovery if needed
    /// </summary>
    private async Task CheckFileIntegrityAsync(
        FileRecord fileRecord, 
        bool autoQueueRecovery, 
        IntegrityCheckResult result, 
        CancellationToken cancellationToken)
    {
        // Check if file exists on filesystem
        bool fileExists = await _fileSystem.FileExistsAsync(fileRecord.FilePath);

        if (!fileExists)
        {
            // File is missing
            if (fileRecord.Status != FileStatus.Missing)
            {
                fileRecord.Status = FileStatus.Missing;
                fileRecord.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.FileRecords.UpdateAsync(fileRecord, cancellationToken);

                result.MissingFiles++;
                result.IssuesFound.Add($"MISSING: {fileRecord.FilePath}");

                // Log missing file
                await _unitOfWork.AuditLogs.AddAsync(new AuditLog
                {
                    EventType = "FileMissing",
                    Message = $"File not found at expected location: {fileRecord.FilePath}",
                    Severity = LogSeverity.Error,
                    FileRecordId = fileRecord.Id
                }, cancellationToken);

                // Auto-queue recovery if enabled
                if (autoQueueRecovery)
                {
                    await QueueRecoveryJobAsync(fileRecord, "File missing from filesystem", cancellationToken);
                    result.RecoveryJobsQueued++;
                }
            }
            else
            {
                result.MissingFiles++;
            }
        }
        else
        {
            // File exists - verify hash
            var currentHash = await _fileSystem.ComputeHashAsync(fileRecord.FilePath);

            if (currentHash != fileRecord.Hash)
            {
                // Hash mismatch - file is corrupted or modified
                if (fileRecord.Status != FileStatus.Corrupted)
                {
                    var oldHash = fileRecord.Hash;
                    fileRecord.Hash = currentHash;
                    fileRecord.Status = FileStatus.Corrupted;
                    fileRecord.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.FileRecords.UpdateAsync(fileRecord, cancellationToken);

                    result.CorruptedFiles++;
                    result.IssuesFound.Add($"CORRUPTED: {fileRecord.FilePath} (hash mismatch)");

                    // Log corruption
                    await _unitOfWork.AuditLogs.AddAsync(new AuditLog
                    {
                        EventType = "CorruptionDetected",
                        Message = $"Hash mismatch detected for file: {fileRecord.FilePath}",
                        Severity = LogSeverity.Warning,
                        FileRecordId = fileRecord.Id,
                        AdditionalData = $"Expected: {oldHash}, Found: {currentHash}"
                    }, cancellationToken);

                    // Auto-queue recovery if enabled
                    if (autoQueueRecovery)
                    {
                        await QueueRecoveryJobAsync(fileRecord, "Hash mismatch detected", cancellationToken);
                        result.RecoveryJobsQueued++;
                    }
                }
                else
                {
                    result.CorruptedFiles++;
                }
            }
            else
            {
                // File is healthy
                if (fileRecord.Status != FileStatus.Healthy)
                {
                    fileRecord.Status = FileStatus.Healthy;
                    fileRecord.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.FileRecords.UpdateAsync(fileRecord, cancellationToken);

                    // Log recovery (file became healthy again)
                    await _unitOfWork.AuditLogs.AddAsync(new AuditLog
                    {
                        EventType = "FileHealthy",
                        Message = $"File returned to healthy status: {fileRecord.FilePath}",
                        Severity = LogSeverity.Info,
                        FileRecordId = fileRecord.Id
                    }, cancellationToken);
                }
                result.HealthyFiles++;
            }
        }
    }

    /// <summary>
    /// Queue a recovery job for a problematic file.
    /// Sets initial priority based on file status severity.
    /// </summary>
    private async Task QueueRecoveryJobAsync(FileRecord fileRecord, string reason, CancellationToken cancellationToken)
    {
        // Check if there's already a pending/running job for this file
        var existingJobs = await _unitOfWork.RecoveryJobs.GetByFileIdAsync(fileRecord.Id, cancellationToken);
        var hasActiveJob = existingJobs.Any(j => 
            j.Status == RecoveryJobStatus.Pending || 
            j.Status == RecoveryJobStatus.Running);

        if (hasActiveJob)
        {
            // Don't queue duplicate job
            return;
        }

        // Create new recovery job
        var recoveryJob = new RecoveryJob
        {
            Id = Guid.NewGuid(),
            FileRecordId = fileRecord.Id,
            Status = RecoveryJobStatus.Pending,
            RecoveryMethod = "BackupRestore",
            // Advanced Feature: Priority based on file status
            // Missing files get higher priority than corrupted ones
            Priority = fileRecord.Status == FileStatus.Missing ? 8 : 5,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.RecoveryJobs.AddAsync(recoveryJob, cancellationToken);

        // Update file status to Recovering
        fileRecord.Status = FileStatus.Recovering;
        await _unitOfWork.FileRecords.UpdateAsync(fileRecord, cancellationToken);

        // Log job queued
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            EventType = "RecoveryJobQueued",
            Message = $"Recovery job queued for file: {fileRecord.FilePath}. Reason: {reason}",
            Severity = LogSeverity.Info,
            FileRecordId = fileRecord.Id,
            RecoveryJobId = recoveryJob.Id
        }, cancellationToken);
    }
}