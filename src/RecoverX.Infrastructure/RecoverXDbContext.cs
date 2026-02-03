using Microsoft.EntityFrameworkCore;
using RecoverX.Domain.Entities;

namespace RecoverX.Infrastructure.Data;

/// <summary>
/// Main database context for RecoverX.
/// Configures entity relationships, indexes, and database behavior.
/// Demonstrates EF Core best practices and performance optimization.
/// </summary>
public class RecoverXDbContext : DbContext
{
    public RecoverXDbContext(DbContextOptions<RecoverXDbContext> options) : base(options)
    {
    }

    // DbSet properties define our database tables
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();
    public DbSet<RecoveryJob> RecoveryJobs => Set<RecoveryJob>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Backup> Backups => Set<Backup>();

    /// <summary>
    /// Configure entity mappings and relationships.
    /// This is where we define database schema through code (Code First approach).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure FileRecord entity
        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.ToTable("FileRecords");
            entity.HasKey(e => e.Id);

            // FilePath must be unique - can't track the same file twice
            entity.HasIndex(e => e.FilePath).IsUnique();

            // Index on Status for efficient filtering by file health
            entity.HasIndex(e => e.Status);

            // Composite index for common queries (status + updated date)
            entity.HasIndex(e => new { e.Status, e.UpdatedAt });

            // String length constraints for database optimization
            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(64); // SHA-256 = 64 hex characters

            // Configure relationships
            entity.HasMany(e => e.RecoveryJobs)
                .WithOne(e => e.FileRecord)
                .HasForeignKey(e => e.FileRecordId)
                .OnDelete(DeleteBehavior.Cascade); // Delete jobs when file is deleted

            entity.HasMany(e => e.AuditLogs)
                .WithOne(e => e.FileRecord)
                .HasForeignKey(e => e.FileRecordId)
                .OnDelete(DeleteBehavior.SetNull); // Keep audit logs even if file is deleted
        });

        // Configure RecoveryJob entity
        modelBuilder.Entity<RecoveryJob>(entity =>
        {
            entity.ToTable("RecoveryJobs");
            entity.HasKey(e => e.Id);

            // Index on FileRecordId for quick lookup of jobs by file
            entity.HasIndex(e => e.FileRecordId);

            // Index on Status for filtering pending/running jobs
            entity.HasIndex(e => e.Status);

            // Composite index for job queue queries (status + priority + created date)
            // This is critical for performance of the recovery worker
            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt });

            entity.Property(e => e.RecoveryMethod)
                .HasMaxLength(100);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000); // Allow for detailed error messages

            // Advanced Feature: Priority-based processing
            // Default priority is 5, stored as integer for efficient sorting
            entity.Property(e => e.Priority)
                .HasDefaultValue(5);
        });

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            // Index on CreatedAt for time-based queries
            entity.HasIndex(e => e.CreatedAt);

            // Index on EventType for filtering specific events
            entity.HasIndex(e => e.EventType);

            // Index on Severity for filtering by log level
            entity.HasIndex(e => e.Severity);

            // Composite index for common audit queries
            entity.HasIndex(e => new { e.Severity, e.CreatedAt });

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.AdditionalData)
                .HasMaxLength(4000); // JSON data can be large

            entity.Property(e => e.TriggeredBy)
                .HasMaxLength(200);

            entity.Property(e => e.Source)
                .HasMaxLength(200);

            // Optional foreign keys for audit trail context
            entity.HasOne(e => e.FileRecord)
                .WithMany(e => e.AuditLogs)
                .HasForeignKey(e => e.FileRecordId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasOne(e => e.RecoveryJob)
                .WithMany()
                .HasForeignKey(e => e.RecoveryJobId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // Configure Backup entity
        modelBuilder.Entity<Backup>(entity =>
        {
            entity.ToTable("Backups");
            entity.HasKey(e => e.Id);

            // Index on CreatedAt for finding recent backups
            entity.HasIndex(e => e.CreatedAt);

            // Index on BackupType for filtering by backup strategy
            entity.HasIndex(e => e.BackupType);

            entity.Property(e => e.BackupPath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.BackupChecksum)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Self-referencing relationship for backup lineage
            entity.HasOne(e => e.RestoredFrom)
                .WithMany()
                .HasForeignKey(e => e.RestoredFromId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });
    }

    /// <summary>
    /// Override SaveChanges to automatically update timestamps.
    /// This ensures UpdatedAt is always current without manual intervention.
    /// Demonstrates EF Core interceptor pattern.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps for modified entities
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is FileRecord fileRecord)
            {
                fileRecord.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}