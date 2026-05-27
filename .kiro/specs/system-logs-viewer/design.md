# Design Document: System Logs Viewer

## Overview

The System Logs Viewer provides SuperAdmin users with a read-only view into the application's Serilog-generated log entries stored in the `Portal.Logging` database (`[dbo].[Logs]` table). It follows the established Audit Log viewer pattern: a dedicated controller with role-based access, a service layer for filtering/pagination, a repository for data access, and a Razor view with filter card + data table + expandable detail rows + pagination.

The key architectural distinction is that this feature reads from a **separate database** (`Portal.Logging`) via a dedicated `LoggingDbContext`, keeping log queries isolated from the main `PortalDbContext` and `MembershipDbContext`.

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| Separate `LoggingDbContext` | Logs live in a different database (`Portal.Logging`). Mixing contexts would require cross-database joins or violate single-responsibility. A dedicated read-only context keeps concerns isolated. |
| `QueryTrackingBehavior.NoTracking` | We only read log data — never insert, update, or delete. Disabling change tracking reduces memory overhead and improves query performance. |
| Reuse `PortalModules.Audit` for access control | The System Logs Viewer is an administration tool closely related to auditing. Reusing the existing audit module permission avoids adding a new module constant for a single page. |
| PageSize max 200 (vs 100 for Audit) | Log entries are typically smaller than audit records (no OldValues/NewValues JSON blobs), so a higher page size is acceptable for operational browsing. |
| No tenant scoping | System logs are platform-wide operational data. SuperAdmins need to see all logs regardless of business context for debugging cross-tenant issues. |
| EF Core LINQ (not stored procedure) | Follows the AuditLogQueryRepository pattern. Dynamic filter composition is cleaner with LINQ than building dynamic SQL. |

## Architecture

```mermaid
graph TD
    A[SystemLogsController] --> B[ISystemLogQueryService]
    B --> C[SystemLogQueryService]
    C --> D[SystemLogQueryRepository]
    D --> E[LoggingDbContext]
    E --> F[(Portal.Logging DB<br/>[dbo].[Logs])]

    A --> G[Views/SystemLogs/Index.cshtml]
    G --> H[fetch /Admin/SystemLogs/Search]
    H --> A
```

### Request Flow

1. **Page Load**: `GET /Admin/SystemLogs` → Controller returns `Index` view with dropdown data (distinct levels, source contexts)
2. **Search**: `GET /Admin/SystemLogs/Search?level=Error&page=1&pageSize=50` → Controller validates → Service clamps pagination → Repository queries → JSON response
3. **UI Render**: JavaScript receives JSON, renders table rows with expandable detail, updates pagination controls

## Components and Interfaces

### LogEntry Entity

Maps to `[dbo].[Logs]` in the `Portal.Logging` database. Read-only — no navigation properties needed.

```csharp
namespace Portal.Infrastructure.Entities;

/// <summary>
/// A single Serilog log record from the [dbo].[Logs] table in Portal.Logging.
/// Read-only entity — no inserts, updates, or deletes from the application.
/// </summary>
public class LogEntry
{
    public long Id { get; set; }
    public string? Message { get; set; }
    public string? MessageTemplate { get; set; }
    public string? Level { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public int? BusinessId { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestPath { get; set; }
    public string? MachineName { get; set; }
}
```

### LoggingDbContext

A dedicated, read-only DbContext for the `Portal.Logging` database.

```csharp
namespace Portal.Infrastructure.Data;

/// <summary>
/// Read-only DbContext for the Portal.Logging database.
/// Configured with NoTracking since we only read log data.
/// </summary>
public class LoggingDbContext : DbContext
{
    public LoggingDbContext(DbContextOptions<LoggingDbContext> options) : base(options) { }

    public DbSet<LogEntry> Logs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.ToTable("Logs", "dbo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Message).HasColumnName("Message");
            entity.Property(e => e.MessageTemplate).HasColumnName("MessageTemplate");
            entity.Property(e => e.Level).HasColumnName("Level").HasMaxLength(128);
            entity.Property(e => e.TimeStamp).HasColumnName("TimeStamp");
            entity.Property(e => e.Exception).HasColumnName("Exception");
            entity.Property(e => e.Properties).HasColumnName("Properties");
            entity.Property(e => e.CorrelationId).HasColumnName("CorrelationId").HasMaxLength(128);
            entity.Property(e => e.UserId).HasColumnName("UserId").HasMaxLength(450);
            entity.Property(e => e.BusinessId).HasColumnName("BusinessId");
            entity.Property(e => e.SourceContext).HasColumnName("SourceContext").HasMaxLength(512);
            entity.Property(e => e.RequestPath).HasColumnName("RequestPath").HasMaxLength(512);
            entity.Property(e => e.MachineName).HasColumnName("MachineName").HasMaxLength(128);
        });
    }
}
```

### SystemLogQueryRepository

Queries the `[dbo].[Logs]` table via `LoggingDbContext`. Follows the existing `AuditLogQueryRepository` pattern with EF Core LINQ for dynamic filter composition.

```csharp
namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for querying LogEntry records from the Portal.Logging database.
/// Uses EF Core LINQ against [dbo].[Logs] — read-only, no inserts or updates.
/// </summary>
public class SystemLogQueryRepository : GenericStoredProcedureRepository<LogEntry>
{
    public SystemLogQueryRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Returns a paged, filtered, and ordered slice of LogEntry records.
    /// All non-null filter parameters are applied with AND logic.
    /// Results are ordered by TimeStamp DESC.
    /// </summary>
    public async Task<(List<LogEntry> Items, int TotalCount)> GetPagedAsync(
        string? level,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? userId,
        string? correlationId,
        string? sourceContext,
        string? requestPath,
        int skip,
        int take)
    {
        try
        {
            var query = _context.Set<LogEntry>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(l => l.Level != null && l.Level.ToLower() == level.ToLower());

            if (dateFrom.HasValue)
                query = query.Where(l => l.TimeStamp >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(l => l.TimeStamp <= dateTo.Value);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(l => l.UserId == userId);

            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(l => l.CorrelationId == correlationId);

            if (!string.IsNullOrWhiteSpace(sourceContext))
                query = query.Where(l => l.SourceContext == sourceContext);

            if (!string.IsNullOrWhiteSpace(requestPath))
                query = query.Where(l => l.RequestPath != null && l.RequestPath.Contains(requestPath));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.TimeStamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (items, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns distinct Level values from the Logs table, sorted alphabetically.
    /// </summary>
    public async Task<List<string>> GetDistinctLevelsAsync()
    {
        try
        {
            return await _context.Set<LogEntry>()
                .Where(l => l.Level != null)
                .Select(l => l.Level!)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns distinct SourceContext values from the Logs table, sorted alphabetically.
    /// </summary>
    public async Task<List<string>> GetDistinctSourceContextsAsync()
    {
        try
        {
            return await _context.Set<LogEntry>()
                .Where(l => l.SourceContext != null)
                .Select(l => l.SourceContext!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
```

### ISystemLogQueryService / SystemLogQueryService

Service layer providing filtered, paginated access to log records. Handles pagination clamping.

```csharp
namespace Portal.Infrastructure.Services;

public interface ISystemLogQueryService
{
    /// <summary>
    /// Returns a paginated, filtered list of log entries.
    /// PageSize is clamped to [1, 200]; PageNumber is clamped to minimum 1.
    /// </summary>
    Task<PagedResult<LogEntry>> GetLogsAsync(SystemLogFilter filter);

    /// <summary>
    /// Returns distinct log levels present in the Logs table.
    /// </summary>
    Task<List<string>> GetDistinctLevelsAsync();

    /// <summary>
    /// Returns distinct source contexts present in the Logs table.
    /// </summary>
    Task<List<string>> GetDistinctSourceContextsAsync();
}
```

```csharp
namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated access to system log entries.
/// No tenant scoping — SuperAdmins see all platform logs.
/// PageSize is clamped to [1, 200]; PageNumber is clamped to minimum 1.
/// </summary>
public class SystemLogQueryService : ISystemLogQueryService
{
    private readonly SystemLogQueryRepository _repository;

    public SystemLogQueryService(SystemLogQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LogEntry>> GetLogsAsync(SystemLogFilter filter)
    {
        try
        {
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);
            var pageNumber = Math.Max(filter.PageNumber, 1);
            var skip = (pageNumber - 1) * pageSize;

            var (items, totalCount) = await _repository.GetPagedAsync(
                filter.Level,
                filter.DateFrom,
                filter.DateTo,
                filter.UserId,
                filter.CorrelationId,
                filter.SourceContext,
                filter.RequestPath,
                skip,
                pageSize);

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            if (pageNumber > totalPages && totalCount > 0)
            {
                return new PagedResult<LogEntry>
                {
                    Items = new List<LogEntry>(),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            return new PagedResult<LogEntry>
            {
                Items = items,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<string>> GetDistinctLevelsAsync()
    {
        try
        {
            return await _repository.GetDistinctLevelsAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<string>> GetDistinctSourceContextsAsync()
    {
        try
        {
            return await _repository.GetDistinctSourceContextsAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
```

### SystemLogFilter Model

```csharp
namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for querying system logs. Clamping of PageNumber and PageSize
/// is handled by the service layer.
/// </summary>
public class SystemLogFilter
{
    public string? Level { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestPath { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
```

### SystemLogsController

```csharp
namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]
[Route("Admin/SystemLogs")]
public class SystemLogsController : Controller
{
    private readonly ISystemLogQueryService _systemLogQueryService;

    public SystemLogsController(ISystemLogQueryService systemLogQueryService)
    {
        _systemLogQueryService = systemLogQueryService;
    }

    // GET /Admin/SystemLogs
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var levels = await _systemLogQueryService.GetDistinctLevelsAsync();
        var sourceContexts = await _systemLogQueryService.GetDistinctSourceContextsAsync();

        ViewBag.Levels = levels;
        ViewBag.SourceContexts = sourceContexts;

        return View();
    }

    // GET /Admin/SystemLogs/Search?level=&dateFrom=&dateTo=&userId=&correlationId=&sourceContext=&requestPath=&page=1&pageSize=50
    [HttpGet("Search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? level,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? userId,
        [FromQuery] string? correlationId,
        [FromQuery] string? sourceContext,
        [FromQuery] string? requestPath,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            {
                return Json(new { success = false, message = "Date From cannot be greater than Date To." });
            }

            var filter = new SystemLogFilter
            {
                Level = level,
                DateFrom = dateFrom,
                DateTo = dateTo,
                UserId = userId,
                CorrelationId = correlationId,
                SourceContext = sourceContext,
                RequestPath = requestPath,
                PageNumber = page,
                PageSize = pageSize
            };

            var pagedResult = await _systemLogQueryService.GetLogsAsync(filter);

            var data = pagedResult.Items.Select(item => new
            {
                id = item.Id,
                timeStamp = item.TimeStamp,
                level = item.Level,
                message = item.Message,
                exception = item.Exception,
                properties = item.Properties,
                userId = item.UserId,
                correlationId = item.CorrelationId,
                sourceContext = item.SourceContext,
                requestPath = item.RequestPath,
                machineName = item.MachineName
            }).ToList();

            return Json(new
            {
                success = true,
                data,
                totalCount = pagedResult.TotalCount,
                currentPage = pagedResult.CurrentPage,
                totalPages = pagedResult.TotalPages
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching system logs");
            return Json(new { success = false, message = "The search could not be completed. Please try again." });
        }
    }
}
```

### Navigation Integration

Add a "System Logs" link in the Administration section of `ModuleNavigation/Default.cshtml`, positioned between the Audit Log link and the Users link:

```html
<a class="nav-item @(isSystemLogsActive ? "active" : "")" href="/Admin/SystemLogs">
    <span class="nav-icon"><svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M4 6h16M4 12h16M4 18h10"/><circle cx="20" cy="18" r="2"/></svg></span>
    <span class="nav-text">System Logs</span>
</a>
```

The active state check:
```csharp
var isSystemLogsActive = currentController.Equals("SystemLogs", StringComparison.OrdinalIgnoreCase);
```

### DI Registration (Program.cs)

```csharp
// --- Logging DbContext (read-only, Portal.Logging database) ---
builder.Services.AddDbContext<LoggingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("LoggingDb"));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// --- System Logs ---
builder.Services.AddScoped<SystemLogQueryRepository>(sp =>
    new SystemLogQueryRepository(sp.GetRequiredService<LoggingDbContext>()));
builder.Services.AddScoped<ISystemLogQueryService, SystemLogQueryService>();
```

## Data Models

### [dbo].[Logs] Table (Portal.Logging Database)

This table already exists — it is created by the Serilog MSSqlServer sink (configured in `Program.cs`). The System Logs Viewer reads from it but never writes.

| Column | SQL Type | Nullable | Description |
|--------|----------|----------|-------------|
| Id | BIGINT (Identity) | NOT NULL | Primary key |
| Message | NVARCHAR(MAX) | YES | Rendered log message |
| MessageTemplate | NVARCHAR(MAX) | YES | Serilog message template |
| Level | NVARCHAR(128) | YES | Log level (Information, Warning, Error, Fatal, Debug) |
| TimeStamp | DATETIME2 | NOT NULL | UTC timestamp of the log event |
| Exception | NVARCHAR(MAX) | YES | Full exception text + stack trace |
| Properties | NVARCHAR(MAX) | YES | Serialized structured properties (XML format from Serilog) |
| CorrelationId | NVARCHAR(128) | YES | Request correlation identifier |
| UserId | NVARCHAR(450) | YES | Authenticated user ID (from LoggingEnrichmentMiddleware) |
| BusinessId | INT | YES | Current tenant business ID |
| SourceContext | NVARCHAR(512) | YES | Fully qualified class name that produced the log |
| RequestPath | NVARCHAR(512) | YES | HTTP request path |
| MachineName | NVARCHAR(128) | YES | Server machine name |

### LogEntry Entity (C#)

Maps 1:1 to the table above. No navigation properties — this is a standalone read-only entity in a separate DbContext.

### SystemLogFilter Model

| Property | Type | Default | Constraints |
|----------|------|---------|-------------|
| Level | string? | null | One of: Debug, Information, Warning, Error, Fatal |
| DateFrom | DateTime? | null | Inclusive (>=) |
| DateTo | DateTime? | null | Inclusive (<=) |
| UserId | string? | null | Max 450 chars |
| CorrelationId | string? | null | Max 128 chars |
| SourceContext | string? | null | Max 512 chars |
| RequestPath | string? | null | Max 512 chars (partial match with Contains) |
| PageNumber | int | 1 | Clamped to minimum 1 |
| PageSize | int | 50 | Clamped to [1, 200] |

### PagedResult<LogEntry>

Reuses the existing `PagedResult<T>` generic class from `Portal.Infrastructure.Models`.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Ordering invariant

*For any* set of log entries returned by the service (regardless of filter combination), the TimeStamp values in the result list SHALL be in non-increasing order (each entry's TimeStamp is greater than or equal to the next entry's TimeStamp).

**Validates: Requirements 2.2, 2.6**

### Property 2: Filter AND composition

*For any* combination of non-null filter parameters and any set of log entries, every record in the returned result SHALL satisfy ALL active filter predicates simultaneously (Level matches case-insensitively, TimeStamp is within [DateFrom, DateTo], UserId matches exactly, CorrelationId matches exactly, SourceContext matches exactly, RequestPath is contained).

**Validates: Requirements 2.3, 2.9**

### Property 3: Pagination clamping

*For any* integer value of PageSize, the effective page size used by the service SHALL be equal to `Math.Clamp(PageSize, 1, 200)`. *For any* integer value of PageNumber, the effective page number SHALL be equal to `Math.Max(PageNumber, 1)`.

**Validates: Requirements 2.4, 2.8**

### Property 4: Page size bounds result count

*For any* query where results exist, the number of items returned SHALL be less than or equal to the effective (clamped) PageSize, and the TotalCount SHALL equal the total number of records matching the filter regardless of pagination.

**Validates: Requirements 2.4, 2.5**

## Error Handling

| Scenario | Handler | Behaviour |
|----------|---------|-----------|
| DateFrom > DateTo | Controller (Search action) | Returns `{ success: false, message: "Date From cannot be greater than Date To." }` before calling service |
| Service/Repository exception | Controller catch block | Logs via `Serilog.Log.Error(...)`, returns `{ success: false, message: "The search could not be completed. Please try again." }` |
| PageNumber exceeds total pages | Service layer | Returns empty Items list with correct TotalCount and TotalPages metadata |
| PageSize out of bounds | Service layer | Silently clamps to [1, 200] — no error returned |
| LoggingDbContext connection failure | Repository (rethrown) → Controller catch | Same as service exception — logged and generic error returned |
| Invalid Level value (not matching any records) | Service/Repository | Returns empty result set — not an error condition |

### Client-Side Error Handling

- `BlockUI.show()` before fetch, `BlockUI.hide()` in both success and catch paths
- SweetAlert2 for error display: `Swal.fire({ title: 'Error', text: data.message, icon: 'error', confirmButtonColor: '#0D5EA6' })`
- Client-side date validation before submitting (SweetAlert2 warning if DateFrom > DateTo)

## Testing Strategy

### Unit Tests (Example-Based)

| Test | What it verifies |
|------|-----------------|
| Controller returns 403 for non-SuperAdmin | Authorization attribute enforcement |
| Controller Index populates ViewBag with levels and source contexts | Dropdown data loading |
| Controller Search returns error JSON when DateFrom > DateTo | Input validation |
| Controller Search returns error JSON when service throws | Exception handling |
| Service returns empty items when page exceeds total | Edge case: over-pagination |
| Repository applies all filters correctly for a known dataset | Filter correctness with concrete examples |

### Property-Based Tests

**Library**: [FsCheck.Xunit](https://github.com/fscheck/FsCheck) (C# property-based testing with xUnit integration)

**Configuration**: Minimum 100 iterations per property test.

| Property Test | Tag |
|---------------|-----|
| Ordering invariant | Feature: system-logs-viewer, Property 1: For any set of log entries returned, TimeStamp values are in non-increasing order |
| Filter AND composition | Feature: system-logs-viewer, Property 2: For any filter combination, every returned record satisfies all active predicates |
| Pagination clamping | Feature: system-logs-viewer, Property 3: For any PageSize/PageNumber input, effective values are clamped to bounds |
| Page size bounds result count | Feature: system-logs-viewer, Property 4: For any query, items count <= effective PageSize and TotalCount reflects full filtered set |

### Integration Tests

| Test | What it verifies |
|------|-----------------|
| LoggingDbContext resolves from DI with NoTracking | DI registration and configuration |
| Full search flow against test database | End-to-end: Controller → Service → Repository → DB |
| Navigation link visible for SuperAdmin, hidden for others | Authorization-gated UI |

### Manual/Visual Tests

| Test | What it verifies |
|------|-----------------|
| Level badges display correct colors | UI rendering (Error=red, Warning=amber, Info=blue, Debug=grey) |
| Expandable detail rows show full exception/properties | UI interaction |
| Only one detail row expanded at a time | UI accordion behaviour |
| BlockUI shows/hides during requests | Loading state UX |
| Empty state message displays when no results | UI empty state |
