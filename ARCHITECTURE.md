# RecoverX Architecture Documentation

## Table of Contents
1. [Overview](#overview)
2. [Layer Architecture](#layer-architecture)
3. [Data Flow](#data-flow)
4. [Recovery Workflow](#recovery-workflow)
5. [Database Design](#database-design)
6. [Async Processing](#async-processing)
7. [Design Patterns](#design-patterns)

---

## Overview

RecoverX follows **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  ┌──────────────────┐        ┌──────────────────┐      │
│  │   RecoverX.Api   │        │  RecoverX.Web    │      │
│  │  (REST API)      │        │  (Razor Pages)   │      │
│  └──────────────────┘        └──────────────────┘      │
└────────────────┬────────────────────┬───────────────────┘
                 │                    │
                 ▼                    ▼
┌─────────────────────────────────────────────────────────┐
│               Application Layer                          │
│  ┌────────────┐  ┌─────────────┐  ┌────────────────┐  │
│  │ Commands   │  │  Queries    │  │     DTOs       │  │
│  │ (CQRS)     │  │  (CQRS)     │  │  (Transfer)    │  │
│  └────────────┘  └─────────────┘  └────────────────┘  │
│  ┌──────────────────────────────────────────────────┐  │
│  │          Interfaces (Abstractions)               │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│                Infrastructure Layer                      │
│  ┌────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │Repositories│  │   Services   │  │  Background   │  │
│  │ (EF Core)  │  │ (File I/O)   │  │   Workers     │  │
│  └────────────┘  └──────────────┘  └───────────────┘  │
│  ┌──────────────────────────────────────────────────┐  │
│  │          DbContext (EF Core)                     │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│                    Domain Layer                          │
│  ┌────────────┐  ┌─────────────┐  ┌────────────────┐  │
│  │  Entities  │  │   Enums     │  │ Value Objects  │  │
│  │ (Core)     │  │             │  │    (DDD)       │  │
│  └────────────┘  └─────────────┘  └────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

**Dependency Rule:** Dependencies point INWARD. Domain has no dependencies. Infrastructure depends on Application and Domain, but not vice versa.

---

## Layer Architecture

### 1. Domain Layer (Core Business Logic)
**Responsibilities:**
- Define core business entities
- Contain business rules and validation
- No external dependencies

**Key Components:**
```
Domain/
├── Entities/
│   ├── FileRecord       ← File metadata tracking
│   ├── RecoveryJob      ← Recovery operation state
│   ├── AuditLog         ← Event auditing
│   └── Backup           ← Backup metadata
├── Enums/
│   └── FileStatus, RecoveryJobStatus, LogSeverity, BackupType
└── ValueObjects/
    ├── Checksum         ← Validated hash representation
    └── FilePath         ← Validated path representation
```

### 2. Application Layer (Use Cases)
**Responsibilities:**
- Orchestrate business workflows
- Define application boundaries
- Contain no business rules (delegates to Domain)

**CQRS Pattern:**
```
Application/
├── Commands/            ← Write operations
│   ├── ScanDirectoryCommand
│   ├── CheckIntegrityCommand
│   └── CreateBackupCommand
├── Queries/             ← Read operations
│   ├── GetFileRecordsQuery
│   ├── GetRecoveryJobsQuery
│   └── GetDashboardStatsQuery
├── DTOs/                ← Data transfer objects
│   └── FileRecordDto, RecoveryJobDto, etc.
└── Interfaces/          ← Abstractions
    ├── IFileRecordRepository
    ├── IRecoveryJobRepository
    ├── IFileSystemService
    └── IUnitOfWork
```

### 3. Infrastructure Layer (External Concerns)
**Responsibilities:**
- Implement interfaces from Application layer
- Handle persistence (database)
- Handle file system I/O
- External service integrations

**Components:**
```
Infrastructure/
├── Data/
│   └── RecoverXDbContext     ← EF Core DbContext
├── Repositories/
│   ├── FileRecordRepository
│   ├── RecoveryJobRepository
│   ├── AuditLogRepository
│   └── UnitOfWork
├── Services/
│   └── FileSystemService     ← File I/O implementation
└── BackgroundServices/
    └── RecoveryWorker        ← Async job processor
```

### 4. Presentation Layer (User Interface)
**Responsibilities:**
- Accept user input
- Display information
- Route requests to Application layer

**Components:**
```
Api/
├── Controllers/
│   ├── FilesController
│   ├── RecoveryController
│   ├── DashboardController
│   └── BackupsController
└── Program.cs           ← DI configuration

Web/
└── Pages/
    ├── Index.cshtml     ← Dashboard
    ├── Files.cshtml     ← File listing
    └── Recovery.cshtml  ← Recovery jobs
```

---

## Data Flow

### Request Flow (API)
```
1. HTTP Request
   │
   ▼
2. API Controller
   │ (receives request)
   │
   ▼
3. MediatR Send(Command/Query)
   │ (decouples controller from handlers)
   │
   ▼
4. Command/Query Handler
   │ (contains business logic)
   │
   ├─────────► FileSystemService (for file operations)
   │
   └─────────► UnitOfWork/Repositories (for database)
                │
                ▼
5. EF Core DbContext
   │ (translates to SQL)
   │
   ▼
6. SQL Server
   │ (data persistence)
   │
   ▼
7. Response returns up the chain
   │
   ▼
8. DTO returned to controller
   │
   ▼
9. HTTP Response (JSON)
```

### Background Processing Flow
```
RecoveryWorker (BackgroundService)
   │ (runs continuously)
   │
   ├──► Create Scope (for scoped services)
   │       │
   │       ▼
   │    UnitOfWork.RecoveryJobs.GetNextPendingJobAsync()
   │       │ (priority-based fetch)
   │       │
   │       ▼
   │    [Job Found?]
   │       │
   │       ├─ Yes ──► ProcessRecoveryJobAsync()
   │       │            │
   │       │            ├──► Update job status to Running
   │       │            │
   │       │            ├──► Perform recovery (restore/repair)
   │       │            │      │
   │       │            │      ├──► FileSystemService operations
   │       │            │      │
   │       │            │      └──► Update file status
   │       │            │
   │       │            ├──► Update job status (Completed/Failed)
   │       │            │
   │       │            └──► Log to AuditLog
   │       │
   │       └─ No ───► Wait (polling interval)
   │
   └──► Dispose Scope
         │
         └──► Loop (until cancellation)
```

---

## Recovery Workflow

### Complete Recovery Cycle

```
┌─────────────────────────────────────────────────────────────┐
│ 1. FILE SCANNING                                            │
│                                                             │
│   User/Scheduler triggers scan                             │
│            │                                                 │
│            ▼                                                 │
│   Scan Directory ────► For each file:                      │
│                         ├─ Compute SHA-256 hash            │
│                         ├─ Get size & timestamps           │
│                         └─ Insert/Update FileRecord        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. INTEGRITY CHECK                                          │
│                                                             │
│   For each tracked file:                                   │
│   ├─ Does file exist? ────► No ──► Mark as MISSING        │
│   │                                     │                   │
│   └─ Yes ──► Hash matches? ─┬─ No ──► Mark as CORRUPTED   │
│                              │            │                 │
│                              └─ Yes ──► Mark as HEALTHY    │
│                                          (no action)        │
│                                                             │
│   If Missing or Corrupted AND AutoQueue = true:           │
│       └──► Create RecoveryJob (status: Pending)           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. BACKGROUND RECOVERY (Async Worker)                      │
│                                                             │
│   RecoveryWorker polls for pending jobs                    │
│            │                                                 │
│            ▼                                                 │
│   Get next job (by priority) ───► [Job found?]            │
│                                       │                     │
│                                       └─ Yes ──► Process    │
│                                                     │        │
│   ┌─────────────────────────────────────────────┐ │        │
│   │ Recovery Attempt                             │ │        │
│   │                                              │ │        │
│   │ Update status → Running                     │ │        │
│   │ AttemptCount++                              │ │        │
│   │                                              │ │        │
│   │ Execute recovery strategy:                  │ │        │
│   │   ├─ Restore from backup                    │ │        │
│   │   ├─ Repair corrupted data                  │ │        │
│   │   └─ Download from source                   │ │        │
│   │                                              │ │        │
│   │ [Success?]                                   │ │        │
│   │   ├─ Yes ──► Status = Completed             │ │        │
│   │   │           FileStatus = Healthy          │ │        │
│   │   │                                          │ │        │
│   │   └─ No ───► [AttemptCount < MaxAttempts?] │ │        │
│   │               ├─ Yes ──► Status = Failed    │ │        │
│   │               │          (will retry)        │ │        │
│   │               │                              │ │        │
│   │               └─ No ───► Status =           │ │        │
│   │                          PermanentlyFailed   │ │        │
│   │                                              │ │        │
│   │ Log to AuditLog                             │ │        │
│   └─────────────────────────────────────────────┘ │        │
│                                                     │        │
│   Wait (polling interval) ◄────────────────────────┘        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Retry Logic with Exponential Backoff

```
Recovery Job Lifecycle:

Attempt 1: [Failed] ──► Status = Failed, wait normal interval
              │
              ▼
Attempt 2: [Failed] ──► Status = Failed, wait normal interval
              │
              ▼
Attempt 3: [Failed] ──► Status = PermanentlyFailed
              │          (no more retries)
              │
              ▼
          Manual intervention required
```

---

## Database Design

### Entity Relationships

```
┌────────────────────────┐
│     FileRecord         │
│────────────────────────│
│ + Id (PK)              │
│ + FilePath (UQ)        │◄────┐
│ + Hash                 │     │
│ + SizeInBytes          │     │ 1
│ + Status               │     │
│ + CreatedAt            │     │
│ + UpdatedAt            │     │
└────────────────────────┘     │
         │                      │
         │ 1                    │
         │                      │
         │ *                    │
         ▼                      │
┌────────────────────────┐     │
│    RecoveryJob         │     │
│────────────────────────│     │
│ + Id (PK)              │     │
│ + FileRecordId (FK) ───┘     │
│ + Status               │     │
│ + AttemptCount         │     │
│ + Priority             │     │
│ + ErrorMessage         │     │
│ + CreatedAt            │     │
│ + StartedAt            │     │
│ + CompletedAt          │     │
└────────────────────────┘     │
                               │
┌────────────────────────┐     │
│      AuditLog          │     │
│────────────────────────│     │
│ + Id (PK)              │     │
│ + FileRecordId (FK) ───┘ (optional)
│ + RecoveryJobId (FK)   │
│ + EventType            │
│ + Message              │
│ + Severity             │
│ + CreatedAt            │
│ + TriggeredBy          │
│ + Source               │
└────────────────────────┘

┌────────────────────────┐
│       Backup           │
│────────────────────────│
│ + Id (PK)              │
│ + BackupPath           │
│ + BackupType           │
│ + FileCount            │
│ + IsCompressed         │
│ + IsEncrypted          │
│ + CreatedAt            │
│ + RestoredFromId (FK) ─┐
└────────────────────────┘│
              │            │
              └────────────┘
              (self-reference)
```

### Key Indexes

**FileRecords:**
- `IX_FileRecords_FilePath` (UNIQUE) - Fast file lookup
- `IX_FileRecords_Status` - Filter by health status
- `IX_FileRecords_Status_UpdatedAt` - Dashboard queries

**RecoveryJobs:**
- `IX_RecoveryJobs_Status_Priority_CreatedAt` - **CRITICAL** for worker
  - Enables efficient: `WHERE Status = Pending ORDER BY Priority DESC, CreatedAt`
  
**AuditLogs:**
- `IX_AuditLogs_CreatedAt` - Recent logs query
- `IX_AuditLogs_Severity_CreatedAt` - Filter by severity

---

## Async Processing

### IHostedService Pattern

```csharp
public class RecoveryWorker : BackgroundService
{
    // Runs in background thread
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create scope for scoped services (DbContext)
            using var scope = _serviceProvider.CreateScope();
            
            // Process jobs
            await ProcessPendingJobsAsync(scope, stoppingToken);
            
            // Wait before next poll
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }
}
```

**Why Scoping is Critical:**
- `RecoveryWorker` is a singleton (lives for app lifetime)
- `DbContext` is scoped (lives for request/scope lifetime)
- Must create new scope each cycle to get fresh DbContext
- Prevents EF Core tracking issues and memory leaks

### Async File I/O

```csharp
// Streaming hash computation for large files
using var stream = new FileStream(
    filePath, 
    FileMode.Open, 
    FileAccess.Read,
    FileShare.Read,
    bufferSize: 4096,
    useAsync: true);  // ← OS-level async I/O

var hashBytes = await sha256.ComputeHashAsync(stream);
```

**Benefits:**
- Doesn't block threads while waiting for disk I/O
- Can process multiple files concurrently
- Handles large files without loading into memory

---

## Design Patterns

### 1. Repository Pattern
**Purpose:** Abstract data access logic

```csharp
public interface IFileRecordRepository
{
    Task<FileRecord?> GetByIdAsync(Guid id);
    Task<FileRecord> AddAsync(FileRecord fileRecord);
    // ... more methods
}

// Implementation uses EF Core
public class FileRecordRepository : IFileRecordRepository
{
    private readonly RecoverXDbContext _context;
    // ... implementation
}
```

**Benefits:**
- Testability (can mock IFileRecordRepository)
- Flexibility (can swap EF Core for Dapper, MongoDB, etc.)
- Encapsulation of queries

### 2. Unit of Work Pattern
**Purpose:** Coordinate transactions across multiple repositories

```csharp
public interface IUnitOfWork
{
    IFileRecordRepository FileRecords { get; }
    IRecoveryJobRepository RecoveryJobs { get; }
    IAuditLogRepository AuditLogs { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
}
```

**Benefits:**
- Atomic operations across multiple entities
- Explicit transaction control
- Single SaveChanges call

### 3. CQRS (Command Query Responsibility Segregation)
**Purpose:** Separate read and write operations

```
Commands (Write):               Queries (Read):
- ScanDirectoryCommand          - GetFileRecordsQuery
- CheckIntegrityCommand         - GetRecoveryJobsQuery
- CreateBackupCommand           - GetDashboardStatsQuery

Commands can have side effects  Queries are read-only
Commands return results         Queries return DTOs
Commands use entities           Queries use projections
```

**Benefits:**
- Different optimization strategies for reads vs writes
- Clearer intent (command = action, query = question)
- Enables read replicas and caching strategies

### 4. Dependency Injection
**Container manages object lifetimes:**

```csharp
// Singleton - one instance for app lifetime
services.AddSingleton<IHostedService, RecoveryWorker>();

// Scoped - one instance per request/scope
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<RecoverXDbContext>();

// Transient - new instance every time
services.AddTransient<IFileSystemService, FileSystemService>();
```

### 5. Domain-Driven Design (DDD)

**Value Objects:**
```csharp
// Checksum is immutable and validated
public class Checksum
{
    private Checksum(string value) { /* validated in factory */ }
    
    public static Checksum Create(string value)
    {
        // Validation logic
        if (!IsValidHex(value))
            throw new ArgumentException();
            
        return new Checksum(value);
    }
}
```

**Rich Domain Entities:**
```csharp
public class RecoveryJob
{
    // Business logic in entity
    public bool CanRetry => 
        Status == RecoveryJobStatus.Failed && 
        !IsMaxAttemptsReached;
    
    public bool IsMaxAttemptsReached => 
        AttemptCount >= MaxAttempts;
}
```

---

## Performance Optimizations

### Database
1. **Strategic Indexes** - Cover common query patterns
2. **AsNoTracking()** - Read-only queries don't track changes
3. **Projections** - Select only needed columns: `.Select(f => new FileRecordDto { ... })`
4. **Batch Operations** - `UpdateRangeAsync()` for bulk updates

### File System
1. **Async I/O** - Don't block threads on I/O
2. **Streaming** - Process large files without loading into memory
3. **Buffer Size** - Optimal 4KB buffer for most scenarios

### Application
1. **Scoped Services** - Fresh DbContext per request prevents tracking bloat
2. **Lazy Loading Disabled** - Explicit `.Include()` for clarity
3. **Connection Pooling** - Reuse database connections

---

## Security Considerations

1. **Input Validation** - FluentValidation on commands
2. **SQL Injection** - Parameterized queries via EF Core
3. **Path Traversal** - FilePath value object validates paths
4. **Encryption** - AES-256 for backups with key derivation
5. **Audit Trail** - Comprehensive logging of all actions

