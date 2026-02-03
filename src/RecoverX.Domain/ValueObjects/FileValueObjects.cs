namespace RecoverX.Domain.ValueObjects;

/// <summary>
/// Value object representing a cryptographic checksum.
/// Immutable and validates format on creation (DDD pattern).
/// </summary>
public class Checksum : IEquatable<Checksum>
{
    /// <summary>
    /// The hash value as a hexadecimal string
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Algorithm used to generate this checksum
    /// Default: SHA256
    /// </summary>
    public string Algorithm { get; }

    private Checksum(string value, string algorithm = "SHA256")
    {
        Value = value;
        Algorithm = algorithm;
    }

    /// <summary>
    /// Factory method to create a checksum with validation
    /// Ensures hash string is valid hex and correct length for algorithm
    /// </summary>
    public static Checksum Create(string value, string algorithm = "SHA256")
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Checksum value cannot be empty", nameof(value));

        // SHA256 produces 64 hex characters (256 bits / 4 bits per hex char)
        if (algorithm == "SHA256" && value.Length != 64)
            throw new ArgumentException("SHA256 checksum must be 64 characters", nameof(value));

        // Validate hex format
        if (!IsValidHex(value))
            throw new ArgumentException("Checksum must be valid hexadecimal", nameof(value));

        return new Checksum(value, algorithm);
    }

    private static bool IsValidHex(string value)
    {
        return value.All(c => (c >= '0' && c <= '9') || 
                              (c >= 'a' && c <= 'f') || 
                              (c >= 'A' && c <= 'F'));
    }

    // Value object equality - two checksums are equal if their values match
    public bool Equals(Checksum? other)
    {
        if (other is null) return false;
        return Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase) &&
               Algorithm.Equals(other.Algorithm, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as Checksum);
    
    public override int GetHashCode() => HashCode.Combine(
        Value.ToUpperInvariant(), 
        Algorithm.ToUpperInvariant()
    );

    public override string ToString() => $"{Algorithm}:{Value}";

    public static bool operator ==(Checksum? left, Checksum? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Checksum? left, Checksum? right) =>
        !(left == right);
}

/// <summary>
/// Value object representing a file path with validation.
/// Ensures paths are properly formatted and prevents common errors.
/// </summary>
public class FilePath : IEquatable<FilePath>
{
    /// <summary>
    /// The normalized absolute path
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The file name without directory path
    /// </summary>
    public string FileName => Path.GetFileName(Value);

    /// <summary>
    /// The directory containing the file
    /// </summary>
    public string Directory => Path.GetDirectoryName(Value) ?? string.Empty;

    /// <summary>
    /// The file extension (including the dot)
    /// </summary>
    public string Extension => Path.GetExtension(Value);

    private FilePath(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Factory method to create a validated and normalized file path
    /// </summary>
    public static FilePath Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path cannot be empty", nameof(path));

        // Normalize the path (resolve relative paths, fix separators, etc.)
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid file path: {path}", nameof(path), ex);
        }

        // Validate path doesn't contain invalid characters
        if (normalizedPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("File path contains invalid characters", nameof(path));

        return new FilePath(normalizedPath);
    }

    // Value object equality
    public bool Equals(FilePath? other)
    {
        if (other is null) return false;
        
        // Case-insensitive comparison for Windows, case-sensitive for Unix
        var comparison = OperatingSystem.IsWindows() 
            ? StringComparison.OrdinalIgnoreCase 
            : StringComparison.Ordinal;
        
        return Value.Equals(other.Value, comparison);
    }

    public override bool Equals(object? obj) => Equals(obj as FilePath);
    
    public override int GetHashCode() => 
        OperatingSystem.IsWindows() 
            ? Value.ToUpperInvariant().GetHashCode() 
            : Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(FilePath? left, FilePath? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(FilePath? left, FilePath? right) =>
        !(left == right);
}