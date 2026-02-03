using MediatR;
using RecoverX.Application.DTOs;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;

namespace RecoverX.Application.Commands;

/// <summary>
/// Command to scan a directory and register all files.
/// Follows CQRS pattern using MediatR for clean separation.
/// </summary>
public class ScanDirectoryCommand : IRequest<ScanDirectoryResult>
{
    /// <summary>
    /// Directory path to scan
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to scan subdirectories recursively
    /// </summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// Whether to update existing file records or skip them
    /// </summary>
    public bool UpdateExisting { get; set; } = false;
}

/// <summary>
/// Result of directory scan operation.
/// Contains statistics about what was found and processed.
/// </summary>
public class ScanDirectoryResult
{
    public int FilesScanned { get; set; }
    public int NewFilesAdded { get; set; }
    public int ExistingFilesUpdated { get; set; }
    public int ErrorsEncountered { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime ScanCompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Handler for ScanDirectoryCommand.
/// Contains the business logic for file scanning and registration.
/// Demonstrates async processing, error handling, and transaction management.
/// </summary>
public class ScanDirectoryCommandHandler : IRequestHandler<ScanDirectoryCommand, ScanDirectoryResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileSystemService _fileSystem;

    public ScanDirectoryCommandHandler(IUnitOfWork unitOfWork, IFileSystemService fileSystem)
    {
        _unitOfWork = unitOfWork;
        _fileSystem = fileSystem;
    }

    public async Task<ScanDirectoryResult> Handle(ScanDirectoryCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var result = new ScanDirectoryResult();

        try
        {
            // Log scan started
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "ScanStarted",
                Message = $"Started scanning directory: {request.DirectoryPath}",
                Severity = LogSeverity.Info,
                Source = "ScanDirectoryCommand",
                TriggeredBy = "System"
            }, cancellationToken);

            // Get all files in directory
            var filePaths = await _fileSystem.ScanDirectoryAsync(request.DirectoryPath, request.Recursive);
            result.FilesScanned = filePaths.Count;

            // Process each file
            foreach (var filePath in filePaths)
            {
                try
                {
                    await ProcessFileAsync(filePath, request.UpdateExisting, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other files
                    result.ErrorsEncountered++;
                    result.Errors.Add($"{filePath}: {ex.Message}");

                    await _unitOfWork.AuditLogs.AddAsync(new AuditLog
                    {
                        EventType = "ScanError",
                        Message = $"Error scanning file: {ex.Message}",
                        Severity = LogSeverity.Error,
                        AdditionalData = filePath
                    }, cancellationToken);
                }
            }

            // Save all changes in a single transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Log scan completed
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "ScanCompleted",
                Message = $"Scan completed. Files: {result.FilesScanned}, New: {result.NewFilesAdded}, Updated: {result.ExistingFilesUpdated}, Errors: {result.ErrorsEncountered}",
                Severity = LogSeverity.Info,
                Source = "ScanDirectoryCommand",
                TriggeredBy = "System"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log critical failure
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "ScanFailed",
                Message = $"Scan failed critically: {ex.Message}",
                Severity = LogSeverity.Critical,
                Source = "ScanDirectoryCommand"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    /// <summary>
    /// Process a single file: compute metadata and add/update database record.
    /// Demonstrates async I/O, hashing, and database operations.
    /// </summary>
    private async Task ProcessFileAsync(string filePath, bool updateExisting, ScanDirectoryResult result, CancellationToken cancellationToken)
    {
        // Check if file already exists in database
        var existingFile = await _unitOfWork.FileRecords.GetByFilePathAsync(filePath, cancellationToken);

        // Get file metadata from filesystem
        var size = await _fileSystem.GetFileSizeAsync(filePath);
        var lastModified = await _fileSystem.GetLastModifiedAsync(filePath);
        var hash = await _fileSystem.ComputeHashAsync(filePath);

        if (existingFile == null)
        {
            // New file - add to database
            var fileRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                Hash = hash,
                SizeInBytes = size,
                LastModified = lastModified,
                Status = FileStatus.Healthy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.FileRecords.AddAsync(fileRecord, cancellationToken);
            result.NewFilesAdded++;

            // Log file discovered
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                EventType = "FileDiscovered",
                Message = $"New file added to tracking: {filePath}",
                Severity = LogSeverity.Info,
                FileRecordId = fileRecord.Id
            }, cancellationToken);
        }
        else if (updateExisting)
        {
            // Existing file - update if metadata changed
            bool changed = false;

            if (existingFile.Hash != hash)
            {
                existingFile.Hash = hash;
                // Hash mismatch could indicate corruption or modification
                existingFile.Status = FileStatus.Corrupted;
                changed = true;

                await _unitOfWork.AuditLogs.AddAsync(new AuditLog
                {
                    EventType = "CorruptionDetected",
                    Message = $"Hash mismatch detected for file: {filePath}",
                    Severity = LogSeverity.Warning,
                    FileRecordId = existingFile.Id
                }, cancellationToken);
            }

            if (existingFile.SizeInBytes != size || existingFile.LastModified != lastModified)
            {
                existingFile.SizeInBytes = size;
                existingFile.LastModified = lastModified;
                existingFile.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }

            if (changed)
            {
                await _unitOfWork.FileRecords.UpdateAsync(existingFile, cancellationToken);
                result.ExistingFilesUpdated++;
            }
        }
    }
}