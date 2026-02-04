# RecoverX - Data Recovery Management System

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

RecoverX is a professional-grade data recovery management system built with ASP.NET Core 8. It demonstrates enterprise-level software architecture, async processing patterns, and database recovery workflows.

## 🎯 Project Purpose

This project showcases:

- ✅ Clean Architecture with clear separation of concerns
- ✅ Domain-Driven Design (DDD) patterns
- ✅ CQRS with MediatR for command/query separation
- ✅ Entity Framework Core with SQL Server
- ✅ Async/await patterns throughout
- ✅ Background service processing with IHostedService
- ✅ Repository and Unit of Work patterns
- ✅ Comprehensive logging with Serilog
- ✅ RESTful API design principles
- ✅ Razor Pages for server-side UI

## 🏗️ Architecture

### Layer Structure

```
RecoverX/
├── src/
│   ├── RecoverX.Domain/          # Core business entities and value objects
│   │   ├── Entities/
│   │   │   ├── FileRecord.cs     # Tracked file metadata
│   │   │   ├── RecoveryJob.cs    # Recovery operation tracking
│   │   │   ├── AuditLog.cs       # Event auditing
│   │   │   └── Backup.cs         # Backup metadata
│   │   ├── Enums/
│   │   │   └── DomainEnums.cs    # Status enumerations
│   │   └── ValueObjects/
│   │       └── FileValueObjects.cs # DDD value objects
│   │
│   ├── RecoverX.Application/     # Business logic and use cases
│   │   ├── Commands/             # Write operations (CQRS)
│   │   │   ├── ScanDirectoryCommand.cs
│   │   │   ├── CheckIntegrityCommand.cs
│   │   │   └── CreateBackupCommand.cs
│   │   ├── Queries/              # Read operations (CQRS)
│   │   │   └── RecoverXQueries.cs
│   │   ├── DTOs/                 # Data transfer objects
│   │   │   └── RecoverXDtos.cs
│   │   └── Interfaces/           # Abstractions
│   │       └── IRepositories.cs
│   │
│   ├── RecoverX.Infrastructure/  # External concerns
│   │   ├── Data/
│   │   │   └── RecoverXDbContext.cs # EF Core context
│   │   ├── Repositories/         # Data access implementations
│   │   ├── Services/
│   │   │   └── FileSystemService.cs # File I/O operations
│   │   └── BackgroundServices/
│   │       └── RecoveryWorker.cs # Async job processing
│   │
│   ├── RecoverX.Api/             # REST API layer
│   │   └── Controllers/          # API endpoints
│   │
│   └── RecoverX.Web/             # Razor Pages UI
│       └── Pages/                # Server-side pages
│
└── RecoverX.sln                  # Solution file
```

### Key Design Patterns

1. **Clean Architecture** - Dependencies point inward, domain is isolated
2. **CQRS** - Commands for writes, Queries for reads (via MediatR)
3. **Repository Pattern** - Abstracted data access
4. **Unit of Work** - Transactional consistency
5. **Domain-Driven Design** - Rich domain models with behavior
6. **Background Worker** - Long-running async operations

## 🚀 Core Features

### 1. File Scanning & Metadata Tracking

```csharp
// Scan a directory and register all files
await mediator.Send(new ScanDirectoryCommand
{
    DirectoryPath = @"C:\Data",
    Recursive = true,
    UpdateExisting = true
});
```

**What it does:**

- Recursively scans directories
- Computes SHA-256 hashes for integrity
- Stores metadata (size, path, timestamps) in SQL Server
- Tracks file status (Healthy, Corrupted, Missing, Recovering)

### 2. Integrity Checking

```csharp
// Check all tracked files for corruption/missing
await mediator.Send(new CheckIntegrityCommand
{
    AutoQueueRecovery = true
});
```

**What it does:**

- Compares database records vs. actual files
- Detects missing files (file deleted/moved)
- Detects corrupted files (hash mismatch)
- Automatically queues recovery jobs

### 3. Recovery Job Queue

**Background Worker** continuously processes jobs:

- Fetches pending jobs (ordered by priority)
- Attempts file restoration/repair
- Implements retry logic with exponential backoff
- Updates file status and logs all actions

**Recovery Strategies:**

- Restore from backup
- Repair corrupted data
- Download from authoritative source
- Manual intervention for complex cases

### 4. Backup & Restore System

```csharp
// Create compressed, encrypted backup
await mediator.Send(new CreateBackupCommand
{
    BackupDirectory = @"C:\Backups",
    BackupType = BackupType.Full,
    EnableCompression = true,
    EnableEncryption = true,
    EncryptionKey = "secure-key-here"
});
```

**Backup Types:**

- **Full** - Complete snapshot of all file metadata
- **Incremental** - Only changes since last backup
- **Differential** - Changes since last full backup

**Advanced Features:**

- GZip compression for storage efficiency
- AES-256 encryption for security
- Backup integrity verification
- Point-in-time restoration

### 5. Audit Trail

Every significant event is logged:

- File discoveries
- Corruption detections
- Recovery operations (started, completed, failed)
- Backup creation
- System errors

**Queryable by:**

- Severity (Debug, Info, Warning, Error, Critical)
- Event type
- Time range
- Associated file/job

### 6. Dashboard & Reporting

Real-time statistics:

- Total files tracked
- Health breakdown (healthy, corrupted, missing)
- Recovery job status
- Success rates
- Storage metrics

## 🗃️ Database Schema

### FileRecords Table

```sql
FileRecords (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FilePath NVARCHAR(500) UNIQUE NOT NULL,
    Hash NVARCHAR(64) NOT NULL,
    SizeInBytes BIGINT NOT NULL,
    LastModified DATETIME2 NOT NULL,
    Status INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
)

-- Indexes for performance
CREATE INDEX IX_FileRecords_Status ON FileRecords(Status)
CREATE INDEX IX_FileRecords_Status_UpdatedAt ON FileRecords(Status, UpdatedAt)
```

### RecoveryJobs Table

```sql
RecoveryJobs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FileRecordId UNIQUEIDENTIFIER FK,
    Status INT NOT NULL,
    AttemptCount INT NOT NULL,
    MaxAttempts INT NOT NULL,
    Priority INT DEFAULT 5,
    RecoveryMethod NVARCHAR(100),
    ErrorMessage NVARCHAR(2000),
    CreatedAt DATETIME2 NOT NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL
)

-- Critical index for worker efficiency
CREATE INDEX IX_RecoveryJobs_Status_Priority_CreatedAt
    ON RecoveryJobs(Status, Priority DESC, CreatedAt)
```

### AuditLogs Table (Append-Only)

```sql
AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventType NVARCHAR(100) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    Severity INT NOT NULL,
    FileRecordId UNIQUEIDENTIFIER FK NULL,
    RecoveryJobId UNIQUEIDENTIFIER FK NULL,
    CreatedAt DATETIME2 NOT NULL,
    TriggeredBy NVARCHAR(200),
    Source NVARCHAR(200),
    AdditionalData NVARCHAR(4000)
)

CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC)
CREATE INDEX IX_AuditLogs_Severity_CreatedAt ON AuditLogs(Severity, CreatedAt DESC)
```

### Backups Table

```sql
Backups (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BackupPath NVARCHAR(500) NOT NULL,
    BackupType INT NOT NULL,
    FileCount INT NOT NULL,
    TotalSizeInBytes BIGINT NOT NULL,
    BackupFileSizeInBytes BIGINT NOT NULL,
    BackupChecksum NVARCHAR(64) NOT NULL,
    IsCompressed BIT NOT NULL,
    IsEncrypted BIT NOT NULL,
    Description NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL,
    RestoredFromId UNIQUEIDENTIFIER FK NULL
)
```

## 📦 Technology Stack

### Backend

- **Framework:** ASP.NET Core 8.0
- **Language:** C# 12 with nullable reference types
- **Database:** SQL Server (LocalDB for dev, SQL Server for prod)
- **ORM:** Entity Framework Core 8.0
- **Logging:** Serilog (Console + File sinks)
- **Validation:** FluentValidation
- **Async Patterns:** Task-based Asynchronous Pattern (TAP)
- **Architecture:** MediatR for CQRS

### Frontend

- **Web UI:** Razor Pages (server-side rendering)
- **API:** Swagger/OpenAPI for documentation
- **Styling:** Bootstrap 5 (optional, can use Tailwind)

### DevOps

- **Version Control:** Git
- **Package Manager:** NuGet
- **Database Migrations:** EF Core Migrations
- **Configuration:** appsettings.json + User Secrets

## 🛠️ Setup Instructions

### Prerequisites

```bash
- .NET 8 SDK
- SQL Server 2019+ or SQL Server Express
- Visual Studio 2022 / VS Code / Rider
- Git
```

### Installation

1. **Clone the repository**

```bash
git clone <https://github.com/PandaBump/RecoverX>
cd RecoverX
```

2. **Configure database connection**

Edit `src/RecoverX.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RecoverXDb;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

For production, use SQL Server:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=RecoverXDb;User Id=sa;Password=your-password;TrustServerCertificate=true"
  }
}
```

3. **Run database migrations**

```bash
cd src/RecoverX.Infrastructure

# Create initial migration
dotnet ef migrations add InitialCreate --startup-project ../RecoverX.Api

# Apply migration to database
dotnet ef database update --startup-project ../RecoverX.Api
```

4. **Build the solution**

```bash
cd ../..
dotnet build
```

5. **Run the API**

```bash
cd src/RecoverX.Api
dotnet run
```

Access Swagger UI at: `https://localhost:5001/swagger`

6. **Run the Web UI** (optional, in separate terminal)

```bash
cd src/RecoverX.Web
dotnet run
```

Access web interface at: `https://localhost:5002`

## 📝 Usage Examples

### Example 1: Complete Recovery Workflow

```csharp
// 1. Scan a directory
var scanResult = await mediator.Send(new ScanDirectoryCommand
{
    DirectoryPath = @"C:\ImportantData",
    Recursive = true,
    UpdateExisting = false
});
// Result: 150 files added to tracking

// 2. Check integrity
var integrityResult = await mediator.Send(new CheckIntegrityCommand
{
    AutoQueueRecovery = true
});
// Result: Found 3 corrupted files, 2 missing files
// Queued 5 recovery jobs automatically

// 3. Background worker processes jobs
// (Automatic - runs continuously)
// Worker picks up jobs by priority, attempts recovery

// 4. Query results
var jobs = await mediator.Send(new GetRecoveryJobsQuery
{
    FilterByStatus = RecoveryJobStatus.Completed
});
// Result: 4/5 jobs completed successfully
```

### Example 2: Backup Strategy

```csharp
// Weekly full backup
await mediator.Send(new CreateBackupCommand
{
    BackupDirectory = @"C:\Backups",
    BackupType = BackupType.Full,
    Description = "Weekly full backup",
    EnableCompression = true,
    EnableEncryption = true,
    EncryptionKey = Environment.GetEnvironmentVariable("BACKUP_KEY")
});

// Daily incremental backups
await mediator.Send(new CreateBackupCommand
{
    BackupDirectory = @"C:\Backups",
    BackupType = BackupType.Incremental,
    Description = "Daily incremental",
    EnableCompression = true
});
```

### Example 3: Dashboard Metrics

```csharp
var stats = await mediator.Send(new GetDashboardStatsQuery());

Console.WriteLine($"Total Files: {stats.TotalFiles}");
Console.WriteLine($"Health: {stats.HealthPercentage:F2}%");
Console.WriteLine($"Recovery Success Rate: {stats.RecoverySuccessRate:F2}%");
```

## 🧪 Testing

### Unit Tests (Future)

```bash
cd tests/RecoverX.UnitTests
dotnet test
```

**Test Coverage Areas:**

- Domain entity business logic
- Value object validation
- Command/Query handlers
- Repository implementations (with InMemory database)
- File system service (with mock file system)

### Integration Tests (Future)

```bash
cd tests/RecoverX.IntegrationTests
dotnet test
```

**Test Scenarios:**

- End-to-end recovery workflows
- Database transactions
- Background worker behavior
- API endpoint responses

## 🎓 Learning Highlights

> I used async/await extensively. For example, the RecoveryWorker is a BackgroundService that continuously processes jobs asynchronously. File I/O operations like hash computation use FileStream with async: true for OS-level async I/O. The CQRS handlers all return Task<T> and await database operations."

> I implemented the Unit of Work pattern. All repositories share a DbContext instance. For complex operations, I use BeginTransactionAsync() explicitly, like in the CreateBackupCommand where I need to ensure backup metadata and audit logs are atomic.

> The application follows constructor injection. RecoveryWorker is interesting - it's a singleton service but needs scoped repositories, so I inject IServiceProvider and create scopes manually in ExecuteAsync using CreateScope()."

> "Multiple layers ensure data integrity: SHA-256 hashes for file integrity, database transactions for atomic updates, optimistic concurrency with UpdatedAt timestamps, and comprehensive audit logging for traceability."

> The background worker can be scaled horizontally with distributed queuing (like Hangfire or Azure Service Bus). Database indexes are optimized for common queries. The CQRS pattern allows read/write separation for future read replicas.

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/recoverx-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "RecoveryWorker": {
    "PollingIntervalSeconds": 5,
    "MaxConcurrentJobs": 3
  },
  "FileScanning": {
    "DefaultScanDirectory": "C:\\DataStore",
    "MaxFileSizeBytes": 1073741824
  }
}
```

## 📊 Performance Considerations

### Database Optimization

- **Indexes:** Strategic indexes on foreign keys and query fields
- **Projections:** DTOs use Select() to load only needed columns
- **AsNoTracking():** Read-only queries don't track changes
- **Batch Operations:** UpdateRangeAsync for bulk updates

### File I/O Optimization

- **Streaming:** Large files use FileStream, not ReadAllBytes
- **Async I/O:** All file operations are async
- **Buffer Size:** 4KB buffer for optimal throughput

### Memory Management

- **Scoped Services:** DbContext per request/worker cycle
- **Using Statements:** IDisposable pattern throughout
- **Lazy Loading:** Disabled, use explicit Include()

## 🚀 Deployment

### Production Roadmap (Near-Future)

- [ ] Update connection string to production SQL Server
- [ ] Enable HTTPS with valid certificate
- [ ] Set up Serilog to Azure Application Insights / ELK
- [ ] Configure health checks endpoint
- [ ] Set up automated backups of SQL database
- [ ] Enable distributed caching (Redis)
- [ ] Set up API rate limiting
- [ ] Configure CORS for production domains
- [ ] Set authentication/authorization
- [ ] Review and harden security settings

### Docker Deployment (Optional)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/RecoverX.Api/RecoverX.Api.csproj", "RecoverX.Api/"]
# ... copy other projects
RUN dotnet restore "RecoverX.Api/RecoverX.Api.csproj"
COPY . .
RUN dotnet build "RecoverX.Api/RecoverX.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RecoverX.Api/RecoverX.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RecoverX.Api.dll"]
```

## 🤝 Contributing

This is a portfolio/learning project, but suggestions are welcome!

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## 📄 License

MIT License - feel free to use this project for learning or as a starting point for your own projects.

## 🎯 Future Enhancements

- [ ] SignalR for real-time job progress updates
- [ ] Azure Blob Storage integration
- [ ] Point-in-time restore UI
- [ ] File versioning system
- [ ] Advanced compression algorithms
- [ ] Distributed recovery workers
- [ ] ML-based corruption prediction
- [ ] GraphQL API
- [ ] React/Angular SPA frontend
- [ ] Docker Compose setup
- [ ] Kubernetes deployment manifests

## 📚 Additional Resources

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [CQRS Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [EF Core Best Practices](https://docs.microsoft.com/en-us/ef/core/)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

---

**Built with ❤️ for learning and demonstrating .NET expertise**
