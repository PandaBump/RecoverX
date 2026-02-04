using Xunit;
using Moq;
using RecoverX.Application.Commands;
using RecoverX.Application.Interfaces;
using RecoverX.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace RecoverX.Tests.Application;

/// <summary>
/// Example unit tests for ScanDirectoryCommand.
/// Demonstrates:
/// - Mocking dependencies
/// - Testing async methods
/// - Verifying behavior
/// - Arranging test data
/// </summary>
public class ScanDirectoryCommandTests
{
    /// <summary>
    /// Test that scanning a directory adds new files to the database
    /// </summary>
    [Fact]
    public async Task Handle_ScanDirectory_AddsNewFiles()
    {
        // Arrange - Set up test data and mocks
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFileSystem = new Mock<IFileSystemService>();
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockAuditLogRepo = new Mock<IAuditLogRepository>();

        // Configure UnitOfWork to return our mocked repositories
        mockUnitOfWork.Setup(u => u.FileRecords).Returns(mockFileRecordRepo.Object);
        mockUnitOfWork.Setup(u => u.AuditLogs).Returns(mockAuditLogRepo.Object);
        mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Configure FileSystem to return test files
        var testFiles = new List<string> 
        { 
            @"C:\Test\file1.txt",
            @"C:\Test\file2.txt" 
        };
        mockFileSystem.Setup(fs => fs.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(testFiles);

        // Configure file metadata
        mockFileSystem.Setup(fs => fs.GetFileSizeAsync(It.IsAny<string>()))
            .ReturnsAsync(1024);
        mockFileSystem.Setup(fs => fs.GetLastModifiedAsync(It.IsAny<string>()))
            .ReturnsAsync(DateTime.UtcNow);
        mockFileSystem.Setup(fs => fs.ComputeHashAsync(It.IsAny<string>()))
            .ReturnsAsync("abc123hash");

        // Files don't exist in database yet (null return)
        mockFileRecordRepo.Setup(r => r.GetByFilePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileRecord?)null);

        var command = new ScanDirectoryCommand
        {
            DirectoryPath = @"C:\Test",
            Recursive = true,
            UpdateExisting = false
        };

        var handler = new ScanDirectoryCommandHandler(mockUnitOfWork.Object, mockFileSystem.Object);

        // Act - Execute the command
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - Verify behavior
        Assert.Equal(2, result.FilesScanned);
        Assert.Equal(2, result.NewFilesAdded);
        Assert.Equal(0, result.ExistingFilesUpdated);
        Assert.Equal(0, result.ErrorsEncountered);

        // Verify AddAsync was called twice (once per file)
        mockFileRecordRepo.Verify(
            r => r.AddAsync(It.IsAny<FileRecord>(), It.IsAny<CancellationToken>()), 
            Times.Exactly(2));

        // Verify SaveChangesAsync was called
        mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce());
    }

    /// <summary>
    /// Test that existing files are updated when UpdateExisting is true
    /// </summary>
    [Fact]
    public async Task Handle_ScanDirectory_UpdatesExistingFilesWhenRequested()
    {
        // Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFileSystem = new Mock<IFileSystemService>();
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockAuditLogRepo = new Mock<IAuditLogRepository>();

        mockUnitOfWork.Setup(u => u.FileRecords).Returns(mockFileRecordRepo.Object);
        mockUnitOfWork.Setup(u => u.AuditLogs).Returns(mockAuditLogRepo.Object);

        // Return one existing file
        var existingFile = new FileRecord
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\Test\file1.txt",
            Hash = "oldhash",
            SizeInBytes = 512,
            LastModified = DateTime.UtcNow.AddDays(-1),
            Status = FileStatus.Healthy
        };

        mockFileRecordRepo.Setup(r => r.GetByFilePathAsync(@"C:\Test\file1.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFile);

        mockFileSystem.Setup(fs => fs.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<string> { @"C:\Test\file1.txt" });

        // File has changed - new hash
        mockFileSystem.Setup(fs => fs.GetFileSizeAsync(It.IsAny<string>()))
            .ReturnsAsync(1024);
        mockFileSystem.Setup(fs => fs.GetLastModifiedAsync(It.IsAny<string>()))
            .ReturnsAsync(DateTime.UtcNow);
        mockFileSystem.Setup(fs => fs.ComputeHashAsync(It.IsAny<string>()))
            .ReturnsAsync("newhash");

        var command = new ScanDirectoryCommand
        {
            DirectoryPath = @"C:\Test",
            UpdateExisting = true
        };

        var handler = new ScanDirectoryCommandHandler(mockUnitOfWork.Object, mockFileSystem.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.FilesScanned);
        Assert.Equal(0, result.NewFilesAdded);
        Assert.Equal(1, result.ExistingFilesUpdated);

        // Verify file was marked as corrupted (hash changed)
        Assert.Equal(FileStatus.Corrupted, existingFile.Status);

        // Verify UpdateAsync was called
        mockFileRecordRepo.Verify(
            r => r.UpdateAsync(It.IsAny<FileRecord>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    /// <summary>
    /// Test that file system errors are handled gracefully
    /// </summary>
    [Fact]
    public async Task Handle_ScanDirectory_HandlesFileSystemErrors()
    {
        // Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFileSystem = new Mock<IFileSystemService>();
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockAuditLogRepo = new Mock<IAuditLogRepository>();

        mockUnitOfWork.Setup(u => u.FileRecords).Returns(mockFileRecordRepo.Object);
        mockUnitOfWork.Setup(u => u.AuditLogs).Returns(mockAuditLogRepo.Object);

        var testFiles = new List<string> 
        { 
            @"C:\Test\file1.txt",
            @"C:\Test\file2.txt" // This one will error
        };

        mockFileSystem.Setup(fs => fs.ScanDirectoryAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(testFiles);

        // First file succeeds
        mockFileSystem.Setup(fs => fs.GetFileSizeAsync(@"C:\Test\file1.txt"))
            .ReturnsAsync(1024);
        mockFileSystem.Setup(fs => fs.GetLastModifiedAsync(@"C:\Test\file1.txt"))
            .ReturnsAsync(DateTime.UtcNow);
        mockFileSystem.Setup(fs => fs.ComputeHashAsync(@"C:\Test\file1.txt"))
            .ReturnsAsync("hash1");

        // Second file throws exception
        mockFileSystem.Setup(fs => fs.GetFileSizeAsync(@"C:\Test\file2.txt"))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        mockFileRecordRepo.Setup(r => r.GetByFilePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileRecord?)null);

        var command = new ScanDirectoryCommand
        {
            DirectoryPath = @"C:\Test"
        };

        var handler = new ScanDirectoryCommandHandler(mockUnitOfWork.Object, mockFileSystem.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.FilesScanned);
        Assert.Equal(1, result.NewFilesAdded); // First file added
        Assert.Equal(1, result.ErrorsEncountered); // Second file errored
        Assert.Single(result.Errors); // Error message recorded

        // Verify error was logged to audit log
        mockAuditLogRepo.Verify(
            r => r.AddAsync(It.Is<AuditLog>(log => log.EventType == "ScanError"), It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

/// <summary>
/// Example unit tests for CheckIntegrityCommand.
/// Demonstrates testing domain logic and complex scenarios.
/// </summary>
public class CheckIntegrityCommandTests
{
    [Fact]
    public async Task Handle_DetectsMissingFiles()
    {
        // Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFileSystem = new Mock<IFileSystemService>();
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockRecoveryJobRepo = new Mock<IRecoveryJobRepository>();
        var mockAuditLogRepo = new Mock<IAuditLogRepository>();

        mockUnitOfWork.Setup(u => u.FileRecords).Returns(mockFileRecordRepo.Object);
        mockUnitOfWork.Setup(u => u.RecoveryJobs).Returns(mockRecoveryJobRepo.Object);
        mockUnitOfWork.Setup(u => u.AuditLogs).Returns(mockAuditLogRepo.Object);

        // File exists in database but not on disk
        var fileRecord = new FileRecord
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\Test\missing.txt",
            Hash = "abc123",
            Status = FileStatus.Healthy
        };

        mockFileRecordRepo.Setup(r => r.GetByStatusAsync(It.IsAny<FileStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileRecord> { fileRecord });

        // File doesn't exist on filesystem
        mockFileSystem.Setup(fs => fs.FileExistsAsync(fileRecord.FilePath))
            .ReturnsAsync(false);

        mockRecoveryJobRepo.Setup(r => r.GetByFileIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecoveryJob>());

        var command = new CheckIntegrityCommand
        {
            AutoQueueRecovery = true
        };

        var handler = new CheckIntegrityCommandHandler(mockUnitOfWork.Object, mockFileSystem.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalFilesChecked);
        Assert.Equal(1, result.MissingFiles);
        Assert.Equal(1, result.RecoveryJobsQueued);

        // Verify file status updated to Missing
        Assert.Equal(FileStatus.Missing, fileRecord.Status);

        // Verify recovery job was queued
        mockRecoveryJobRepo.Verify(
            r => r.AddAsync(It.IsAny<RecoveryJob>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_DetectsCorruptedFiles()
    {
        // Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockFileSystem = new Mock<IFileSystemService>();
        var mockFileRecordRepo = new Mock<IFileRecordRepository>();
        var mockRecoveryJobRepo = new Mock<IRecoveryJobRepository>();
        var mockAuditLogRepo = new Mock<IAuditLogRepository>();

        mockUnitOfWork.Setup(u => u.FileRecords).Returns(mockFileRecordRepo.Object);
        mockUnitOfWork.Setup(u => u.RecoveryJobs).Returns(mockRecoveryJobRepo.Object);
        mockUnitOfWork.Setup(u => u.AuditLogs).Returns(mockAuditLogRepo.Object);

        var fileRecord = new FileRecord
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\Test\corrupted.txt",
            Hash = "originalHash",
            Status = FileStatus.Healthy
        };

        mockFileRecordRepo.Setup(r => r.GetByStatusAsync(It.IsAny<FileStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileRecord> { fileRecord });

        // File exists but hash doesn't match
        mockFileSystem.Setup(fs => fs.FileExistsAsync(fileRecord.FilePath))
            .ReturnsAsync(true);
        mockFileSystem.Setup(fs => fs.ComputeHashAsync(fileRecord.FilePath))
            .ReturnsAsync("corruptedHash");

        mockRecoveryJobRepo.Setup(r => r.GetByFileIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecoveryJob>());

        var command = new CheckIntegrityCommand
        {
            AutoQueueRecovery = true
        };

        var handler = new CheckIntegrityCommandHandler(mockUnitOfWork.Object, mockFileSystem.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.CorruptedFiles);
        Assert.Equal(FileStatus.Corrupted, fileRecord.Status);
        Assert.NotEqual("originalHash", fileRecord.Hash); // Hash updated to current value
    }
}

/*
 * ADDITIONAL TEST CLASSES TO CREATE:
 * 
 * - FileRecordRepositoryTests (using InMemory database)
 * - RecoveryWorkerTests (testing background service behavior)
 * - FileSystemServiceTests (with mock file system)
 * - ValueObjectTests (Checksum, FilePath validation)
 * - BackupCommandTests (testing compression and encryption)
 * 
 * INTEGRATION TEST IDEAS:
 * 
 * - End-to-end recovery workflow
 * - Database transaction rollback scenarios
 * - Concurrent job processing
 * - API endpoint testing with WebApplicationFactory
 */