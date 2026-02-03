using MediatR;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RecoverX.Application.Commands;

/// <summary>
/// Command to create a backup of file metadata.
/// Supports full, incremental, and differential backups with optional compression/encryption.
/// </summary>
public class CreateBackupCommand : IRequest<CreateBackupResult>
{
    /// <summary>
    /// Directory where backup file will be created
    /// </summary>
    public string BackupDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Type of backup to create
    /// </summary>
    public BackupType BackupType { get; set; } = BackupType.Full;

    /// <summary>
    /// Optional description/tag for this backup
    /// </summary>
    public string? Description { get; set; }

    // Advanced Feature: Compression and encryption
    /// <summary>
    /// Whether to compress the backup file
    /// Reduces storage space
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Whether to encrypt the backup file
    /// Protects sensitive metadata
    /// </summary>
    public bool EnableEncryption { get; set; } = false;

    /// <summary>
    /// Encryption key (required if EnableEncryption is true)
    /// </summary>
    public string? EncryptionKey { get; set; }
}

public class CreateBackupResult
{
    public Guid BackupId { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSizeInBytes { get; set; }
    public long BackupFileSizeInBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
}

/// <summary>
/// Simplified backup data structure for serialization.
/// Contains all file metadata at backup time.
/// </summary>
public class BackupData
{
    public DateTime BackupTimestamp { get; set; }
    public BackupType BackupType { get; set; }
    public List<FileRecordBackup> Files { get; set; } = new();
}

public class FileRecordBackup
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Handler for creating backups.
/// Demonstrates JSON serialization, file compression, and encryption.
/// </summary>
public class CreateBackupCommandHandler : IRequestHandler<CreateBackupCommand, CreateBackupResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileSystemService _fileSystem;

    public CreateBackupCommandHandler(IUnitOfWork unitOfWork, IFileSystemService fileSystem)
    {
        _unitOfWork = unitOfWork;
        _fileSystem = fileSystem;
    }

    public async Task<CreateBackupResult> Handle(CreateBackupCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        // Validate encryption settings
        if (request.EnableEncryption && string.IsNullOrEmpty(request.EncryptionKey))
        {
            throw new ArgumentException("Encryption key is required when encryption is enabled");
        }

        // Log backup started
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            EventType = "BackupStarted",
            Message = $"Started creating {request.BackupType} backup",
            Severity = LogSeverity.Info,
            Source = "CreateBackupCommand",
            TriggeredBy = "System"
        }, cancellationToken);

        // Gather data to backup based on type
        var filesToBackup = await GetFilesToBackupAsync(request.BackupType, cancellationToken);

        // Create backup data structure
        var backupData = new BackupData
        {
            BackupTimestamp = DateTime.UtcNow,
            BackupType = request.BackupType,
            Files = filesToBackup.Select(f => new FileRecordBackup
            {
                Id = f.Id,
                FilePath = f.FilePath,
                Hash = f.Hash,
                SizeInBytes = f.SizeInBytes,
                LastModified = f.LastModified,
                Status = f.Status.ToString()
            }).ToList()
        };

        // Serialize to JSON
        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        var jsonData = JsonSerializer.Serialize(backupData, jsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonData);

        // Apply compression if enabled
        byte[] finalData = jsonBytes;
        if (request.EnableCompression)
        {
            finalData = await CompressDataAsync(jsonBytes);
        }

        // Apply encryption if enabled
        // Advanced Feature: AES encryption for backup protection
        if (request.EnableEncryption && !string.IsNullOrEmpty(request.EncryptionKey))
        {
            finalData = EncryptData(finalData, request.EncryptionKey);
        }

        // Generate backup file path
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"RecoverX_Backup_{request.BackupType}_{timestamp}.bak";
        var backupFilePath = Path.Combine(request.BackupDirectory, fileName);

        // Ensure backup directory exists
        Directory.CreateDirectory(request.BackupDirectory);

        // Write backup file
        await File.WriteAllBytesAsync(backupFilePath, finalData, cancellationToken);

        // Compute checksum of backup file
        var backupChecksum = await _fileSystem.ComputeHashAsync(backupFilePath);

        // Create backup record in database
        var backup = new Backup
        {
            Id = Guid.NewGuid(),
            BackupPath = backupFilePath,
            CreatedAt = DateTime.UtcNow,
            FileCount = backupData.Files.Count,
            TotalSizeInBytes = backupData.Files.Sum(f => f.SizeInBytes),
            BackupChecksum = backupChecksum,
            Description = request.Description,
            BackupType = request.BackupType,
            IsCompressed = request.EnableCompression,
            IsEncrypted = request.EnableEncryption,
            BackupFileSizeInBytes = finalData.Length
        };

        await _unitOfWork.Backups.AddAsync(backup, cancellationToken);

        // Log backup completed
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog
        {
            EventType = "BackupCompleted",
            Message = $"Backup completed successfully. Files: {backup.FileCount}, Size: {backup.BackupFileSizeInBytes} bytes",
            Severity = LogSeverity.Info,
            Source = "CreateBackupCommand",
            TriggeredBy = "System",
            AdditionalData = backupFilePath
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateBackupResult
        {
            BackupId = backup.Id,
            BackupFilePath = backupFilePath,
            FileCount = backup.FileCount,
            TotalSizeInBytes = backup.TotalSizeInBytes,
            BackupFileSizeInBytes = backup.BackupFileSizeInBytes,
            Duration = DateTime.UtcNow - startTime,
            IsCompressed = request.EnableCompression,
            IsEncrypted = request.EnableEncryption
        };
    }

    /// <summary>
    /// Get files to include in backup based on backup type.
    /// Full: All files
    /// Incremental: Files changed since last backup
    /// Differential: Files changed since last full backup
    /// </summary>
    private async Task<List<FileRecord>> GetFilesToBackupAsync(BackupType backupType, CancellationToken cancellationToken)
    {
        if (backupType == BackupType.Full)
        {
            // Full backup: include all files
            return _unitOfWork.FileRecords.GetAll().ToList();
        }

        // Get most recent backup
        var lastBackup = await _unitOfWork.Backups.GetMostRecentAsync(cancellationToken);

        if (lastBackup == null)
        {
            // No previous backup exists - fall back to full backup
            return _unitOfWork.FileRecords.GetAll().ToList();
        }

        DateTime cutoffDate = backupType == BackupType.Differential
            ? (await _unitOfWork.Backups.GetByTypeAsync(BackupType.Full, cancellationToken))
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault()?.CreatedAt ?? lastBackup.CreatedAt
            : lastBackup.CreatedAt;

        // Return only files modified since cutoff date
        return _unitOfWork.FileRecords.GetAll()
            .Where(f => f.UpdatedAt > cutoffDate)
            .ToList();
    }

    /// <summary>
    /// Compress data using GZip compression.
    /// Reduces backup file size significantly.
    /// </summary>
    private async Task<byte[]> CompressDataAsync(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            await gzipStream.WriteAsync(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    // Advanced Feature: AES encryption for backup security
    /// <summary>
    /// Encrypt data using AES-256 encryption.
    /// Protects backup files from unauthorized access.
    /// </summary>
    private byte[] EncryptData(byte[] data, string encryptionKey)
    {
        using var aes = Aes.Create();
        
        // Derive a proper key from the password using PBKDF2
        var salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };
        var pdb = new Rfc2898DeriveBytes(encryptionKey, salt, 10000, HashAlgorithmName.SHA256);
        
        aes.Key = pdb.GetBytes(32); // AES-256
        aes.IV = pdb.GetBytes(16);
        
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        
        return ms.ToArray();
    }
}