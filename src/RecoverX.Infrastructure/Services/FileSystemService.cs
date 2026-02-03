using RecoverX.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace RecoverX.Infrastructure.Services;

/// <summary>
/// Implementation of file system operations.
/// Provides async file I/O with proper error handling and resource management.
/// Abstracts filesystem for testability and future cloud storage integration.
/// </summary>
public class FileSystemService : IFileSystemService
{
    /// <summary>
    /// Check if file exists at given path.
    /// Thread-safe and async-safe operation.
    /// </summary>
    public Task<bool> FileExistsAsync(string filePath)
    {
        return Task.FromResult(File.Exists(filePath));
    }

    /// <summary>
    /// Get file size in bytes.
    /// Throws FileNotFoundException if file doesn't exist.
    /// </summary>
    public Task<long> GetFileSizeAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"File not found: {filePath}");

        return Task.FromResult(fileInfo.Length);
    }

    /// <summary>
    /// Get last modification timestamp in UTC.
    /// Important: Always use UTC for consistency across time zones.
    /// </summary>
    public Task<DateTime> GetLastModifiedAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"File not found: {filePath}");

        // Convert to UTC for consistent database storage
        return Task.FromResult(fileInfo.LastWriteTimeUtc);
    }

    /// <summary>
    /// Compute SHA-256 hash of file content.
    /// This is the core of integrity verification.
    /// Uses streaming to handle large files efficiently without loading entire file into memory.
    /// </summary>
    public async Task<string> ComputeHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        // Use SHA256 algorithm for cryptographic-strength hashing
        using var sha256 = SHA256.Create();
        
        // Stream the file to avoid loading entire file into memory
        // This is crucial for large files (GB+)
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096, // 4KB buffer is optimal for most scenarios
            useAsync: true    // Enable async I/O at OS level
        );

        // Compute hash asynchronously
        var hashBytes = await sha256.ComputeHashAsync(stream);

        // Convert to hexadecimal string (lowercase)
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Read entire file content as byte array.
    /// Use with caution for large files - prefer streaming for GB+ files.
    /// </summary>
    public async Task<byte[]> ReadAllBytesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        return await File.ReadAllBytesAsync(filePath);
    }

    /// <summary>
    /// Write byte array to file.
    /// Creates directory if it doesn't exist.
    /// Overwrites existing file.
    /// </summary>
    public async Task WriteAllBytesAsync(string filePath, byte[] content)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(filePath, content);
    }

    /// <summary>
    /// Copy file from source to destination.
    /// Creates destination directory if needed.
    /// Overwrites if destination exists.
    /// </summary>
    public Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath}");

        // Ensure destination directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // File.Copy is synchronous but fast for local files
        // For network drives or cloud storage, implement async version
        File.Copy(sourcePath, destinationPath, overwrite: true);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Delete a file.
    /// Does not throw if file doesn't exist (idempotent).
    /// </summary>
    public Task DeleteFileAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Scan directory and return all file paths.
    /// Supports recursive scanning of subdirectories.
    /// Handles access denied errors gracefully.
    /// </summary>
    public Task<List<string>> ScanDirectoryAsync(string directoryPath, bool recursive = true)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = new List<string>();

        try
        {
            // Get search option based on recursive flag
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // EnumerateFiles is more memory-efficient than GetFiles for large directories
            // It returns IEnumerable instead of array, enabling lazy evaluation
            var enumeratedFiles = Directory.EnumerateFiles(directoryPath, "*", searchOption);
            
            files.AddRange(enumeratedFiles);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Log but don't fail - some directories may be inaccessible
            // In production, use Serilog here
            Console.WriteLine($"Access denied to directory: {directoryPath} - {ex.Message}");
        }

        return Task.FromResult(files);
    }

    // Advanced Feature: File encryption/decryption
    /// <summary>
    /// Encrypt file content using AES-256.
    /// Returns encrypted bytes that can be written to a new file.
    /// </summary>
    public async Task<byte[]> EncryptFileAsync(string filePath, string encryptionKey)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var fileContent = await File.ReadAllBytesAsync(filePath);

        using var aes = Aes.Create();
        aes.KeySize = 256; // AES-256
        
        // Derive key from password using PBKDF2
        var salt = Encoding.UTF8.GetBytes("RecoverX-Salt-v1"); // In production, use random salt per file
        var pdb = new Rfc2898DeriveBytes(encryptionKey, salt, 10000, HashAlgorithmName.SHA256);
        
        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            await cs.WriteAsync(fileContent, 0, fileContent.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Decrypt file content using AES-256.
    /// Returns original bytes that can be written to restore the file.
    /// </summary>
    public async Task<byte[]> DecryptFileAsync(string filePath, string encryptionKey)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var encryptedContent = await File.ReadAllBytesAsync(filePath);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        
        // Use same salt as encryption
        var salt = Encoding.UTF8.GetBytes("RecoverX-Salt-v1");
        var pdb = new Rfc2898DeriveBytes(encryptionKey, salt, 10000, HashAlgorithmName.SHA256);
        
        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedContent);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        
        await cs.CopyToAsync(output);
        return output.ToArray();
    }
}