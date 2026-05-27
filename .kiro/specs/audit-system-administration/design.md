# Design Document: Audit & System Administration

## Overview

The Audit & System Administration module adds three capabilities to the Portal platform:

1. **Automatic Audit Logging** — an EF Core `SaveChangesInterceptor` that captures every Insert, Update, and Delete across `PortalDbContext` and writes `AuditLog` records without requiring manual service-layer calls.
2. **Audit Log Viewer** — a SuperAdmin-only MVC controller and Razor view that provides filtered, paginated, expandable access to the audit trail.
3. **User & Permission Management** — a SuperAdmin-only interface for listing business users, toggling their active status, and managing per-module access levels via `UserBusinessPermission` records.

All three capabilities are scoped to the SuperAdmin role. The audit interceptor runs transparently on every `SaveChangesAsync` call; the viewer and admin screens are accessible at `/Admin/Audit` and `/Admin/Users` respectively.


## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Portal.Web                                                             │
│                                                                         │
│  ┌──────────────────┐    ┌──────────────────────────────────────────┐  │
│  │  AuditController │    │  AdminController                         │  │
│  │  /Admin/Audit    │    │  /Admin/Users                            │  │
│  │  [SuperAdmin]    │    │  [SuperAdmin]                            │  │
│  └────────┬─────────┘    └──────────────┬───────────────────────────┘  │
│           │                             │                               │
└───────────┼─────────────────────────────┼───────────────────────────────┘
            │                             │
┌───────────┼─────────────────────────────┼───────────────────────────────┐
│  Portal.Infrastructure                  │                               │
│           │                             │                               │
│  ┌────────▼──────────────┐   ┌──────────▼──────────────────────────┐   │
│  │ IAuditLogQueryService │   │ IUserAdminService                   │   │
│  │ AuditLogQueryService  │   │ UserAdminService                    │   │
│  └────────┬──────────────┘   └──────────┬──────────────────────────┘   │
│           │                             │                               │
│  ┌────────▼──────────────┐   ┌──────────▼──────────────────────────┐   │
│  │ AuditLogQueryRepo     │   │ UserAdminRepository                 │   │
│  │ (PortalDbContext)     │   │ (MembershipDbContext)               │   │
│  └────────┬──────────────┘   └──────────┬──────────────────────────┘   │
│           │                             │                               │
│  ┌────────▼──────────────────────────────▼──────────────────────────┐   │
│  │  AuditInterceptor : SaveChangesInterceptor                       │   │
│  │  Registered on PortalDbContext                                   │   │
│  │  Reads: ICurrentTenantService, IHttpContextAccessor              │   │
│  │  Writes: AuditLogRepository (direct INSERT)                      │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘

  Portal DB (PortalDbContext)          Membership DB (MembershipDbContext)
  ┌──────────────────────────┐         ┌──────────────────────────────────┐
  │ [audit].[AuditLog]       │         │ [membership].[UserBusiness]      │
  │ (append-only)            │         │ [membership].[UserBusinessPerm.] │
  └──────────────────────────┘         │ [dbo].[AspNetUsers]              │
                                       └──────────────────────────────────┘
```

### Key Design Decisions

**Interceptor writes via raw SQL, not EF tracking.** The `AuditInterceptor` calls `AuditLogRepository.InsertAsync` (which uses `ExecuteSqlRawAsync`) rather than adding `AuditLog` entities to the change tracker. This avoids any risk of the interceptor triggering itself and keeps the audit write outside the main transaction boundary — audit records are written after the save succeeds.

**Separate read repository from write repository.** `AuditLogRepository` (existing) handles append-only inserts. `AuditLogQueryRepository` (new) handles LINQ-based read queries against `PortalDbContext.AuditLogs`. This separation keeps the write path simple and the read path flexible.

**EF Core LINQ for query service, raw SQL for write.** The query service uses EF Core LINQ (not raw SQL) because the filter combinations are dynamic and LINQ composes cleanly. The write path uses raw SQL to stay consistent with the existing `AuditLogRepository` pattern.

**UserAdminService uses MembershipDbContext directly.** User and permission management operates entirely on the Membership DB. No cross-database joins are needed — the service fetches users and permissions from `MembershipDbContext` and writes audit entries via `AuditLogRepository` (Portal DB).


## Components and Interfaces

### AuditInterceptor

**Location:** `Portal.Infrastructure/Interceptors/AuditInterceptor.cs`

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantService _tenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditLogRepository _auditLogRepository;

    public AuditInterceptor(
        ICurrentTenantService tenantService,
        IHttpContextAccessor httpContextAccessor,
        AuditLogRepository auditLogRepository) { }

    // Captures pre-save state for Modified/Deleted entities.
    // Returns a list of AuditEntry (pending records, some without RecordId yet).
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default);

    // After save: fills in identity-generated PKs for Added entries, then writes all records.
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result);

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default);
}
```

**Internal helper type** (private, within the interceptor file):

```csharp
private sealed class AuditEntry
{
    public EntityEntry Entry { get; init; }
    public string Action { get; init; }        // "Insert" | "Update" | "Delete"
    public string TableName { get; init; }
    public string? OldValues { get; init; }    // JSON, null for Insert
    public string? NewValues { get; set; }     // JSON, null for Delete; set after save for Insert
    public string RecordId { get; set; }       // Set after save for identity PKs
    public int? BusinessId { get; init; }
    public string? UserId { get; init; }
    public DateTime Timestamp { get; init; }
}
```

### IAuditLogQueryService / AuditLogQueryService

**Location:** `Portal.Infrastructure/Services/IAuditLogQueryService.cs` and `AuditLogQueryService.cs`

```csharp
public interface IAuditLogQueryService
{
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter);

    /// <summary>Returns distinct table names present in AuditLog for the current business.</summary>
    Task<List<string>> GetDistinctTableNamesAsync();
}
```

### AuditLogFilter

**Location:** `Portal.Infrastructure/Models/AuditLogFilter.cs`

```csharp
public class AuditLogFilter
{
    public string? TableName { get; set; }      // optional, max 200 chars
    public string? Action { get; set; }         // optional: "Insert" | "Update" | "Delete"
    public string? UserId { get; set; }         // optional, max 450 chars
    public DateTime? DateFrom { get; set; }     // inclusive >=
    public DateTime? DateTo { get; set; }       // inclusive <=
    public int PageNumber { get; set; } = 1;    // min 1
    public int PageSize { get; set; } = 20;     // min 1, max 100
}
```

### AuditLogQueryRepository

**Location:** `Portal.Infrastructure/Repositories/AuditLogQueryRepository.cs`

```csharp
public class AuditLogQueryRepository : GenericStoredProcedureRepository<AuditLog>
{
    public AuditLogQueryRepository(DbContext context) : base(context) { }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int businessId,
        string? tableName,
        string? action,
        string? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int skip,
        int take);

    public async Task<List<string>> GetDistinctTableNamesAsync(int businessId);
}
```

This repository uses EF Core LINQ via `_context.Set<AuditLog>()` (not raw SQL) because the filter combinations are dynamic and LINQ composes cleanly without string concatenation risks.


### IUserAdminService / UserAdminService

**Location:** `Portal.Infrastructure/Services/IUserAdminService.cs` and `UserAdminService.cs`

```csharp
public interface IUserAdminService
{
    // User listing — MembershipDbContext
    Task<PagedResult<UserAdminDto>> GetUsersAsync(UserAdminFilter filter);

    // Activate/deactivate — MembershipDbContext + AuditLog write
    Task<ServiceResult> DeactivateUserAsync(int userBusinessId, string performedByUserId);
    Task<ServiceResult> ReactivateUserAsync(int userBusinessId, string performedByUserId);

    // Module permissions — MembershipDbContext + AuditLog write
    Task<List<UserModulePermissionDto>> GetUserPermissionsAsync(int userBusinessId);
    Task<ServiceResult> UpdatePermissionAsync(
        int userBusinessId, string module, string accessLevel, string performedByUserId);
}
```

**Supporting DTOs** (in `Portal.Infrastructure/Models/`):

```csharp
public class UserAdminFilter
{
    public string? SearchTerm { get; set; }     // name/email contains, case-insensitive
    public string? StatusFilter { get; set; }   // "Active" | "Inactive" | null = All
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UserAdminDto
{
    public int UserBusinessId { get; set; }
    public string UserId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? LastLoginUtc { get; set; }  // from AspNetUsers.LastLoginUtc if available
}

public class UserModulePermissionDto
{
    public int? PermissionId { get; set; }       // null if no record exists yet
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;  // "full" | "readonly" | "none"
    public bool IsActive { get; set; }
}
```

### UserAdminRepository

**Location:** `Portal.Infrastructure/Repositories/UserAdminRepository.cs`

```csharp
public class UserAdminRepository : GenericStoredProcedureRepository<UserBusiness>
{
    public UserAdminRepository(DbContext context) : base(context) { }

    public async Task<(List<UserBusiness> Items, int TotalCount)> GetUsersPagedAsync(
        int businessId, string? searchTerm, bool? isActive, int skip, int take);

    public async Task<UserBusiness?> GetByIdAsync(int userBusinessId);

    public async Task DeactivateAsync(int userBusinessId, DateTime deactivatedAtUtc);

    public async Task ReactivateAsync(int userBusinessId);

    public async Task<List<UserBusinessPermission>> GetPermissionsAsync(int userBusinessId);

    public async Task UpsertPermissionAsync(
        int userBusinessId, string module, string accessLevel, bool isActive, DateTime? deactivatedAtUtc);
}
```

`GetUsersPagedAsync` uses EF Core LINQ against `MembershipDbContext` with `.Include(ub => ub.User)` to load the `ApplicationUser` navigation property for name/email. `UpsertPermissionAsync` checks for an existing `UserBusinessPermission` record and either inserts or updates it using `ExecuteSqlRawAsync`.


## Data Models

### AuditLog (existing — no schema changes required)

The `[audit].[AuditLog]` table already exists (migration `019_CreateAuditLogTable.sql`). No schema changes are needed. The existing `AuditLog` entity and `AuditLogRepository` are unchanged.

```
[audit].[AuditLog]
  Id            BIGINT IDENTITY PK
  BusinessId    INT NULL FK → [portal].[Business]
  UserId        NVARCHAR(450) NULL
  Action        NVARCHAR(50) NOT NULL       -- "Insert" | "Update" | "Delete"
  TableName     NVARCHAR(200) NOT NULL
  RecordId      NVARCHAR(50) NOT NULL
  OldValues     NVARCHAR(MAX) NULL          -- JSON
  NewValues     NVARCHAR(MAX) NULL          -- JSON
  Timestamp     DATETIME2 NOT NULL DEFAULT GETUTCDATE()
```

Indexes already in place: `PK_AuditLog` (clustered on Id), `IX_AuditLog_BusinessId` (non-clustered).

**Recommended additional index** (new migration `060_AddAuditLogQueryIndexes.sql`):

```sql
-- Composite index to support the most common query pattern:
-- WHERE BusinessId = @b AND Timestamp BETWEEN @from AND @to ORDER BY Timestamp DESC
CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Timestamp]
    ON [audit].[AuditLog] ([BusinessId], [Timestamp] DESC);

-- Covering index for action-filtered queries
CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Action]
    ON [audit].[AuditLog] ([BusinessId], [Action])
    INCLUDE ([Timestamp], [TableName], [UserId], [RecordId]);
```

### UserBusiness (existing — no schema changes)

```
[membership].[UserBusiness]
  Id                INT IDENTITY PK
  UserId            NVARCHAR(450) NOT NULL FK → AspNetUsers
  BusinessId        INT NOT NULL
  IsDefault         BIT NOT NULL
  IsActive          BIT NOT NULL DEFAULT 1
  DeactivatedAtUtc  DATETIME NULL
  CreatedAtUtc      DATETIME NOT NULL DEFAULT GETUTCDATE()
```

### UserBusinessPermission (existing — no schema changes)

```
[membership].[UserBusinessPermission]
  Id                INT IDENTITY PK
  UserBusinessId    INT NOT NULL FK → UserBusiness
  Module            NVARCHAR(50) NOT NULL
  AccessLevel       NVARCHAR(20) NOT NULL   -- "full" | "readonly" | "none"
  IsActive          BIT NOT NULL DEFAULT 1
  DeactivatedAtUtc  DATETIME NULL
  CreatedAtUtc      DATETIME NOT NULL DEFAULT GETUTCDATE()
  UNIQUE (UserBusinessId, Module)
```

### AuditLog PortalDbContext Configuration (existing — verify mapping)

The existing `ConfigureAuditLog` method in `PortalDbContext` must map to `[audit].[AuditLog]`. Confirm it includes:

```csharp
entity.ToTable("AuditLog", "audit");
entity.HasKey(e => e.Id);
entity.Property(e => e.Id).ValueGeneratedOnAdd();
entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
entity.Property(e => e.TableName).IsRequired().HasMaxLength(200);
entity.Property(e => e.RecordId).IsRequired().HasMaxLength(50);
entity.Property(e => e.UserId).HasMaxLength(450);
entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("GETUTCDATE()");
```


## EF Core Audit Interceptor — Detailed Design

### Lifecycle: Two-Phase Capture

The interceptor operates in two phases to handle identity-generated PKs correctly:

**Phase 1 — `SavingChangesAsync` (pre-save):**
- Iterate `eventData.Context.ChangeTracker.Entries()`.
- Skip entries where `entry.Entity is AuditLog` (prevents recursion).
- Skip entries in `EntityState.Unchanged` or `EntityState.Detached`.
- For each qualifying entry, build an `AuditEntry`:
  - `Action`: `Added → "Insert"`, `Modified → "Update"`, `Deleted → "Delete"`.
  - `TableName`: resolved from `entry.Metadata.GetTableName()`.
  - `OldValues`: for `Modified` and `Deleted`, serialize only properties where `entry.Property(p.Name).IsModified == true` (for Modified) or all scalar properties (for Deleted) from `entry.OriginalValues`.
  - `NewValues`: for `Added` and `Modified`, serialize only modified scalar properties from `entry.CurrentValues`.
  - `RecordId`: for `Modified` and `Deleted`, read the PK value now (it exists). For `Added`, leave empty — will be filled post-save.
  - `BusinessId`: `_tenantService.CurrentBusinessId`.
  - `UserId`: `_httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)`.
  - `Timestamp`: `DateTime.UtcNow`.
- Store the list of `AuditEntry` objects in a thread-local or `AsyncLocal<List<AuditEntry>>` field keyed by the `DbContext` instance.

**Phase 2 — `SavedChangesAsync` (post-save):**
- Retrieve the pending `AuditEntry` list for this context instance.
- For entries with `Action == "Insert"`, read the generated PK from `entry.Entity` via reflection or `entry.CurrentValues[pkPropertyName].ToString()`.
- For each `AuditEntry`, call `await _auditLogRepository.InsertAsync(new AuditLog { ... })`.
- Clear the pending list.

### Property Serialization

Only scalar (non-navigation, non-shadow) properties are serialized. The helper method:

```csharp
private static string? SerializeProperties(IEnumerable<PropertyEntry> properties)
{
    var dict = properties
        .Where(p => !p.Metadata.IsShadowProperty() && p.Metadata.ClrType.IsValueType
                    || p.Metadata.ClrType == typeof(string))
        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

    return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
}
```

For `Update` operations, only properties where `p.IsModified == true` are included in both `OldValues` (using `p.OriginalValue`) and `NewValues` (using `p.CurrentValue`).

### Recursion Guard

```csharp
// In SavingChangesAsync — first filter applied before any other processing:
var entries = eventData.Context.ChangeTracker.Entries()
    .Where(e => e.Entity is not AuditLog
             && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
    .ToList();
```

### Failure Safety

The interceptor writes audit records in `SavedChangesAsync`, which only fires when the main save succeeds. If `SaveChangesAsync` throws, `SavedChangesAsync` is never called, so no orphaned audit records are written. The pending `AuditEntry` list is cleared regardless of outcome to prevent stale state on context reuse.

### Context Instance Isolation

Because `PortalDbContext` is scoped (one per request), a simple instance field `private List<AuditEntry>? _pendingEntries` on the interceptor is sufficient — but only if the interceptor is also registered as scoped. If registered as singleton, use `ConditionalWeakTable<DbContext, List<AuditEntry>>` to key pending entries by context instance.

**Decision:** Register `AuditInterceptor` as **scoped** to match `PortalDbContext` lifetime. This is the simplest and safest approach.


## AuditController

**Location:** `Portal.Web/Controllers/AuditController.cs`

**Route prefix:** `/Admin/Audit`

```csharp
[Authorize(Roles = "SuperAdmin")]
[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]
[Route("Admin/Audit")]
public class AuditController : Controller
{
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly ICurrentTenantService _tenantService;

    // GET /Admin/Audit
    [HttpGet("")]
    public async Task<IActionResult> Index();

    // GET /Admin/Audit/Search?tableName=&action=&userId=&dateFrom=&dateTo=&page=1&pageSize=20
    [HttpGet("Search")]
    public async Task<IActionResult> Search(
        string? tableName, string? action, string? userId,
        DateTime? dateFrom, DateTime? dateTo,
        int page = 1, int pageSize = 20);
}
```

**Index action:** Loads distinct table names and business users for filter dropdowns, passes them to the view via `ViewBag`. Returns `View()`.

**Search action:**
- Validates `dateFrom <= dateTo` when both are provided; returns `Json(new { success = false, message = "Date From cannot be greater than Date To." })` if invalid.
- Builds `AuditLogFilter` and calls `_auditLogQueryService.GetAuditLogsAsync(filter)`.
- On success: returns `Json(new { success = true, data = pagedResult.Items, totalCount, currentPage, totalPages })`.
- On exception: logs via Serilog, returns `Json(new { success = false, message = "The search could not be completed. Please try again." })`.

**Response shape for Search:**

```json
{
  "success": true,
  "data": [
    {
      "id": 1042,
      "timestamp": "2026-05-26T14:32:18",
      "userId": "abc123",
      "userDisplayName": "John Papamichael",
      "action": "Update",
      "tableName": "Invoice",
      "recordId": "1042",
      "oldValues": "{\"StatusTypeId\":1}",
      "newValues": "{\"StatusTypeId\":2}"
    }
  ],
  "totalCount": 142,
  "currentPage": 1,
  "totalPages": 8
}
```

The `userDisplayName` is resolved by the service by joining against `MembershipDbContext.Users` using the `UserId` values in the result set.


## AdminController

**Location:** `Portal.Web/Controllers/AdminController.cs`

**Route prefix:** `/Admin/Users`

```csharp
[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Users")]
public class AdminController : Controller
{
    private readonly IUserAdminService _userAdminService;
    private readonly ICurrentTenantService _tenantService;
    private readonly UserManager<ApplicationUser> _userManager;

    // GET /Admin/Users
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? searchTerm, string? statusFilter, int page = 1);

    // GET /Admin/Users/ModuleAccess/{userBusinessId}
    [HttpGet("ModuleAccess/{userBusinessId:int}")]
    public async Task<IActionResult> ModuleAccess(int userBusinessId);

    // POST /Admin/Users/UpdatePermission
    [HttpPost("UpdatePermission")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermission(
        [FromBody] UpdatePermissionRequest request);

    // POST /Admin/Users/ToggleStatus
    [HttpPost("ToggleStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        [FromBody] ToggleStatusRequest request);
}
```

**Request models** (in `Portal.Web/Models/`):

```csharp
public class UpdatePermissionRequest
{
    public int UserBusinessId { get; set; }
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;
}

public class ToggleStatusRequest
{
    public int UserBusinessId { get; set; }
    public bool Activate { get; set; }  // true = reactivate, false = deactivate
}
```

**Index action:** Builds `UserAdminFilter`, calls `_userAdminService.GetUsersAsync(filter)`, returns view with `PagedResult<UserAdminDto>` as model.

**ModuleAccess action:** Loads user info and all module permissions via `_userAdminService.GetUserPermissionsAsync(userBusinessId)`. Passes `PortalModules.All` and the permission list to the view. Returns `View()`.

**UpdatePermission action:** Validates module and access level against constants. Calls `_userAdminService.UpdatePermissionAsync(...)`. Returns `Json(new { success, message })`.

**ToggleStatus action:** Calls `DeactivateUserAsync` or `ReactivateUserAsync` based on `request.Activate`. Guards against self-deactivation by comparing `request.UserBusinessId` against the current user's `UserBusiness`. Returns `Json(new { success, message })`.


## Audit Log Viewer UI

**Location:** `Portal.Web/Views/Audit/Index.cshtml`

### View Structure

```
Page Header (eyebrow: "Administration", title: "Audit Log")
│
├── Filter Card  <section class="glass card-pad" style="margin-bottom:22px;">
│   └── Filter row (flex, gap:14px, align-items:flex-end)
│       ├── Table Name (select, min-width:180px) — populated from ViewBag.TableNames
│       ├── Action (select: All / Insert / Update / Delete)
│       ├── User (select, min-width:180px) — populated from ViewBag.Users
│       ├── Date From (input[type=date])
│       ├── Date To (input[type=date])
│       └── [Filter] [Clear] buttons (padding-bottom:2px wrapper)
│
└── Data Table Card  <section class="glass card-pad">
    ├── <table class="data-table">
    │   ├── thead: [expand] [Timestamp] [User] [Action] [Table] [Record ID]
    │   └── tbody: data rows + detail rows (injected by JS)
    └── Pagination row (margin-top:18px, flex, space-between)
        ├── "Showing X–Y of Z" info
        └── [← Prev] [1] [2] ... [N] [Next →] buttons
```

### JavaScript AJAX Pattern

```javascript
// State
let currentPage = 1;
const pageSize = 20;

async function loadAuditLogs(page) {
    // Client-side date validation
    const dateFrom = document.getElementById('dateFrom').value;
    const dateTo = document.getElementById('dateTo').value;
    if (dateFrom && dateTo && new Date(dateFrom) > new Date(dateTo)) {
        Swal.fire({ title: 'Invalid Date Range',
            text: 'Date From cannot be greater than Date To.',
            icon: 'warning', confirmButtonColor: '#0D5EA6' });
        return;
    }

    BlockUI.show('Loading audit logs...');
    try {
        const params = new URLSearchParams({
            tableName: document.getElementById('tableName').value,
            action: document.getElementById('action').value,
            userId: document.getElementById('userId').value,
            dateFrom, dateTo, page, pageSize
        });
        const response = await fetch(`/Admin/Audit/Search?${params}`);
        const data = await response.json();
        BlockUI.hide();

        if (data.success) {
            renderTable(data.data);
            renderPagination(data.currentPage, data.totalPages, data.totalCount);
            currentPage = data.currentPage;
        } else {
            Swal.fire({ title: 'Error', text: data.message,
                icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.',
            icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}

// Called on page load
document.addEventListener('DOMContentLoaded', () => loadAuditLogs(1));
```

### Table Row Rendering

Each data row is rendered as two `<tr>` elements: the summary row and a hidden detail row.

**Action badge classes:** `badge--insert` (green), `badge--update` (blue), `badge--delete` (red).

**Detail row expand/collapse:** Clicking the summary row toggles the detail row. Only one detail row is open at a time (close others on open). The expand button rotates 180° when open (CSS `transform: rotate(180deg)`).

**Detail panel layout:**
- `Update`: two-column grid — Old Values | New Values. Changed properties highlighted with `background: rgba(13,94,166,0.08); border-left: 3px solid #0D5EA6`.
- `Insert`: full-width New Values panel only (`grid-column: 1 / -1`).
- `Delete`: full-width Old Values panel only (`grid-column: 1 / -1`).

JSON values are parsed and rendered as `key: value` lines. For `Update`, the keys present in both `oldValues` and `newValues` are highlighted as changed.

**Empty state:** When `data.data.length === 0`, render a single row spanning all columns:
```html
<tr><td colspan="6" style="text-align:center;padding:32px;color:#5a6b7c;">
    No audit records found matching the selected filters.
</td></tr>
```

### Pagination Rendering

```javascript
function renderPagination(currentPage, totalPages, totalCount) {
    const start = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
    const end = Math.min(currentPage * pageSize, totalCount);
    document.getElementById('paginationInfo').textContent =
        `Showing ${start}–${end} of ${totalCount}`;
    // Render page buttons: Prev, 1..totalPages (with ellipsis for large ranges), Next
    // Active page gets class page-btn--active; Prev/Next disabled at boundaries
}
```


## User Management UI

**Location:** `Portal.Web/Views/Admin/Index.cshtml`

### View Structure

```
Page Header (eyebrow: "Administration", title: "Users")
│
├── Filter Card  <section class="glass card-pad" style="margin-bottom:22px;">
│   └── Filter row (flex, gap:14px)
│       ├── Search (input[type=text], placeholder: "Name or email...", min-width:240px)
│       ├── Status (select: All / Active / Inactive, min-width:160px)
│       ├── [Filter] [Clear] buttons
│       └── [Invite User] button (links to /Invitation/Create, btn-primary, float right)
│
└── Data Table Card  <section class="glass card-pad">
    ├── <table class="data-table">
    │   ├── thead: [Full Name] [Email] [Role] [Status] [Last Login] [Actions]
    │   └── tbody: user rows (clickable → ModuleAccess)
    └── Pagination row (margin-top:18px)
```

**Status badge:** Active = green badge, Inactive = red badge (same badge pattern as action badges).

**Last Login:** Formatted as `dd MMM yyyy HH:mm` or `"Never"` if null.

**Actions column:** Deactivate button (danger style) for active users; Reactivate button (primary style) for inactive users. Self-row: buttons disabled with tooltip "You cannot modify your own account."

### SweetAlert2 Confirmation Flow

**Deactivate:**
```javascript
async function deactivateUser(userBusinessId, userName) {
    const confirm = await Swal.fire({
        title: 'Deactivate User',
        text: `Are you sure you want to deactivate ${userName}?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#C24A4A',
        confirmButtonText: 'Deactivate'
    });
    if (!confirm.isConfirmed) return;

    BlockUI.show('Deactivating user...');
    try {
        const response = await fetch('/Admin/Users/ToggleStatus', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({ userBusinessId, activate: false })
        });
        const data = await response.json();
        BlockUI.hide();
        if (data.success) {
            Swal.fire({ title: 'Done', text: 'User deactivated.',
                icon: 'success', confirmButtonColor: '#0D5EA6' })
                .then(() => location.reload());
        } else {
            Swal.fire({ title: 'Error', text: data.message,
                icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.',
            icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}
```

**Reactivate:** Same pattern with `confirmButtonColor: '#0D5EA6'` and `activate: true`.

**Self-deactivation guard:** Before showing the confirmation, check if `userBusinessId === currentUserBusinessId` (passed from server via `ViewBag.CurrentUserBusinessId`). If so, show informational Swal and return.


## Module Access Manager UI

**Location:** `Portal.Web/Views/Admin/ModuleAccess.cshtml`

### View Structure

```
Page Header (eyebrow: "Administration", title: "Module Access — {UserFullName}")
│
└── Permissions Card  <section class="glass card-pad">
    ├── User info row (name, email, status badge)
    ├── <table class="data-table">
    │   ├── thead: [Module] [Access Level] [Status]
    │   └── tbody: one row per module in PortalModules.All
    │       ├── Module name (display-friendly, e.g. "Customer", "Quotation")
    │       ├── Access level selector (radio group or segmented control: Full | ReadOnly | None)
    │       └── Status badge (Active / Inactive based on IsActive)
    └── Back link → /Admin/Users
```

### Permission Change Flow

Each module row has three radio/toggle buttons: Full, ReadOnly, None. Changing a selection triggers:

```javascript
async function updatePermission(userBusinessId, module, accessLevel, moduleName) {
    const isRevocation = accessLevel === 'none';
    const confirmColor = isRevocation ? '#C24A4A' : '#0D5EA6';
    const confirmText = isRevocation
        ? `Revoke ${moduleName} access?`
        : `Grant ${accessLevel} access to ${moduleName}?`;

    const confirm = await Swal.fire({
        title: 'Confirm Permission Change',
        text: confirmText,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: confirmColor,
        confirmButtonText: 'Confirm'
    });

    if (!confirm.isConfirmed) {
        // Revert the UI selection to previous value
        revertSelection(module);
        return;
    }

    BlockUI.show('Updating permission...');
    try {
        const response = await fetch('/Admin/Users/UpdatePermission', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify({ userBusinessId, module, accessLevel })
        });
        const data = await response.json();
        BlockUI.hide();

        if (data.success) {
            Swal.fire({ title: 'Updated',
                text: `${moduleName} access set to ${accessLevel}.`,
                icon: 'success', confirmButtonColor: '#0D5EA6' });
            updateStatusBadge(module, accessLevel);
        } else {
            revertSelection(module);
            Swal.fire({ title: 'Error', text: data.message,
                icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        revertSelection(module);
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.',
            icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}
```

**Self-protection:** The currently authenticated user's row has all access level controls disabled (`disabled` attribute) and a tooltip: "You cannot modify your own permissions."

**Previous value tracking:** Each module row stores the current access level in a `data-current-level` attribute. On cancel or error, `revertSelection` reads this attribute and resets the radio/toggle state.


## Database Migrations

### Migration 060 — Audit Log Query Indexes (new)

**File:** `Portal.Database/Migrations/060_AddAuditLogQueryIndexes.sql`

```sql
/*
    Migration: 060_AddAuditLogQueryIndexes
    Description: Adds composite indexes on [audit].[AuditLog] to support
                 the filtered, paginated query patterns used by AuditLogQueryService.
    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId_Timestamp'
      AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Timestamp]
        ON [audit].[AuditLog] ([BusinessId], [Timestamp] DESC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId_Action'
      AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Action]
        ON [audit].[AuditLog] ([BusinessId], [Action])
        INCLUDE ([Timestamp], [TableName], [UserId], [RecordId]);
END
GO
```

No other schema migrations are required. All tables (`AuditLog`, `UserBusiness`, `UserBusinessPermission`) already exist with the correct columns. The `UserBusiness` and `UserBusinessPermission` tables already have `IsActive`, `DeactivatedAtUtc`, and `CreatedAtUtc` columns per the existing entity definitions.


## Error Handling

### Interceptor

- If `ICurrentTenantService` or `IHttpContextAccessor` throws, the exception propagates and `SaveChangesAsync` fails — this is acceptable since a broken tenant/auth context is a fatal condition.
- If `AuditLogRepository.InsertAsync` throws in `SavedChangesAsync`, the exception is logged via Serilog and **swallowed** — the main save has already succeeded and we must not roll it back due to an audit write failure. This is the one place in the codebase where swallowing is intentional and documented.
- If `HttpContext` is null or the NameIdentifier claim is absent, `UserId` is set to `null` and the record is still written (Requirement 1.9).

### AuditLogQueryService

- All exceptions propagate to the controller (standard `try/catch; throw` pattern).
- The controller catches and returns `Json(new { success = false, message = ... })`.

### UserAdminService

- All exceptions propagate to the controller.
- The controller catches, logs via Serilog, and returns `Json(new { success = false, message = ... })`.
- Audit log write failures within `UserAdminService` (for permission changes and status toggles) are logged but do not fail the primary operation — same rationale as the interceptor.

### Controller Layer

All AJAX endpoints follow the standard pattern:

```csharp
try
{
    // ... service call
    return Json(new { success = true, ... });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in {Action}", nameof(ActionName));
    return Json(new { success = false, message = "Operation could not be completed. Please try again." });
}
```


## Registration (Program.cs)

The following additions are required in `Program.cs`:

```csharp
// --- Audit Interceptor ---
// Must be scoped to match PortalDbContext lifetime
builder.Services.AddScoped<AuditInterceptor>();

// Reconfigure PortalDbContext to add the interceptor
// Replace the existing AddDbContext<PortalDbContext> registration:
builder.Services.AddDbContext<PortalDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("PortalDb"));
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

// --- Audit Query ---
builder.Services.AddScoped<AuditLogQueryRepository>(sp =>
    new AuditLogQueryRepository(sp.GetRequiredService<PortalDbContext>()));
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();

// --- User Admin ---
builder.Services.AddScoped<UserAdminRepository>(sp =>
    new UserAdminRepository(sp.GetRequiredService<MembershipDbContext>()));
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
```

**Note on interceptor registration:** Using `sp.GetRequiredService<AuditInterceptor>()` inside the `AddDbContext` factory lambda correctly resolves the scoped interceptor from the same scope as the `PortalDbContext`. This is the recommended EF Core pattern for scoped interceptors.


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

**Property Reflection:** Before listing properties, redundancy was assessed. Requirements 1.3 and 1.4 (NewValues/OldValues serialization) are closely related but test different directions (current vs. original values) and different entity states, so they are kept separate. Requirements 2.3 and 2.7 (unfiltered returns all records, ordered descending) are combined into one ordering invariant property since 2.3 is subsumed by 2.7 when applied to the full result set. Requirements 2.4 and 2.5 (AND filter logic, pagination non-overlap) are distinct and kept separate. Requirements 1.7 and 1.8 (BusinessId and UserId resolution) are combined into one "context resolution" property since they test the same mechanism (dependency injection into the interceptor) with the same pattern.

---

### Property 1: One audit record per changed entity

*For any* invocation of `SaveChangesAsync` with N entities in Added, Modified, or Deleted state (excluding `AuditLog` entities), the interceptor SHALL produce exactly N `AuditLog` records — one per changed entity.

**Validates: Requirements 1.1**

---

### Property 2: Entity state maps to correct Action value

*For any* entity entry in `EntityState.Added`, `EntityState.Modified`, or `EntityState.Deleted`, the resulting `AuditLog.Action` SHALL be `"Insert"`, `"Update"`, or `"Delete"` respectively, and no other value.

**Validates: Requirements 1.2**

---

### Property 3: Modified-only properties appear in OldValues and NewValues for Updates

*For any* entity in `Modified` state with a subset of properties marked `IsModified = true`, the serialized `OldValues` and `NewValues` JSON SHALL contain exactly those modified properties — no more, no fewer — with `OldValues` containing original values and `NewValues` containing current values.

**Validates: Requirements 1.3, 1.4, 1.13**

---

### Property 4: TableName matches EF Core metadata

*For any* entity type tracked by `PortalDbContext`, the `AuditLog.TableName` written by the interceptor SHALL equal the table name returned by `entry.Metadata.GetTableName()` for that entity type.

**Validates: Requirements 1.5**

---

### Property 5: Context values (BusinessId, UserId) are resolved from injected services

*For any* `BusinessId` value provided by `ICurrentTenantService` and any `UserId` claim value provided by `IHttpContextAccessor`, the resulting `AuditLog` record SHALL have `BusinessId` equal to the tenant service value and `UserId` equal to the claim value. When `HttpContext` is null or the claim is absent, `UserId` SHALL be null and the record SHALL still be written.

**Validates: Requirements 1.7, 1.8, 1.9**

---

### Property 6: AuditLog entities are excluded from interception (no recursion)

*For any* save operation that includes `AuditLog` entities in the change tracker, the interceptor SHALL produce zero additional `AuditLog` records for those entries — the interceptor does not audit its own writes.

**Validates: Requirements 1.10**

---

### Property 7: Tenant isolation — all query results belong to the current business

*For any* `BusinessId` used as the current tenant, every `AuditLog` record returned by `IAuditLogQueryService.GetAuditLogsAsync` SHALL have `BusinessId` equal to that tenant's `BusinessId`. No records from other businesses SHALL appear in the result.

**Validates: Requirements 2.2**

---

### Property 8: Filter AND composition narrows results

*For any* two filter predicates A and B applied independently and together, the result set of (A AND B) SHALL be a subset of both result(A) and result(B). Specifically: applying `TableName` filter, `Action` filter, `UserId` filter, `DateFrom`, and `DateTo` filters in combination SHALL return only records satisfying all specified conditions simultaneously.

**Validates: Requirements 2.3, 2.4**

---

### Property 9: Results are ordered by Timestamp descending

*For any* result set returned by `GetAuditLogsAsync`, for all consecutive pairs of records `items[i]` and `items[i+1]`, `items[i].Timestamp >= items[i+1].Timestamp` SHALL hold.

**Validates: Requirements 2.7**

---

### Property 10: Pagination invariants

*For any* valid `PageNumber` and `PageSize` (after clamping), the following SHALL hold simultaneously:
- `items.Count <= PageSize`
- `TotalPages == Math.Ceiling(TotalCount / (double)PageSize)`
- Pages do not overlap: the record at position `(page-1)*pageSize + k` on page N is not present on any other page
- When `PageNumber > TotalPages`, `items` is empty and `TotalCount` and `TotalPages` are still correct

**Validates: Requirements 2.5, 2.6, 2.8**

---

### Property 11: PageSize clamping

*For any* `PageSize` value less than 1, the effective page size used in the query SHALL be 1. *For any* `PageSize` value greater than 100, the effective page size SHALL be 100. Values in [1, 100] SHALL be used as-is.

**Validates: Requirements 2.9**

---

### Property 12: Permission upsert correctness

*For any* user-module combination and any valid access level (`"full"`, `"readonly"`, `"none"`), after calling `UpdatePermissionAsync`, the stored `UserBusinessPermission` record SHALL reflect the new access level. Setting to `"none"` SHALL result in `IsActive = false` and `DeactivatedAtUtc` set to a non-null UTC timestamp. Setting to `"full"` or `"readonly"` SHALL result in `IsActive = true` and `DeactivatedAtUtc = null`.

**Validates: Requirements 5.3, 5.4, 5.5, 5.6**

---

### Property 13: User status toggle correctness

*For any* `UserBusiness` record, calling `DeactivateUserAsync` SHALL result in `IsActive = false` and `DeactivatedAtUtc` set to a non-null UTC timestamp. Calling `ReactivateUserAsync` SHALL result in `IsActive = true` and `DeactivatedAtUtc = null`. These operations are inverses of each other.

**Validates: Requirements 6.5, 6.6**


## Testing Strategy

### Property-Based Testing

The project uses C#. The recommended PBT library is **FsCheck** (via `FsCheck.Xunit` or `FsCheck.NUnit`), which is the most mature property-based testing library for .NET. Each property test is configured to run a minimum of 100 iterations.

Each property test is tagged with a comment referencing the design property:
```
// Feature: audit-system-administration, Property N: <property_text>
```

**Properties 1–6 (Interceptor):** Test the `AuditInterceptor` in isolation using an in-memory `DbContext` (or SQLite in-memory) with mocked `ICurrentTenantService` and `IHttpContextAccessor`. FsCheck generators produce random entity instances, random sets of modified properties, and random context values (BusinessId, UserId).

**Properties 7–11 (Query Service):** Test `AuditLogQueryService` against an in-memory or SQLite test database seeded with generated `AuditLog` records. FsCheck generators produce random filter combinations, page numbers, and page sizes.

**Properties 12–13 (Permission/Status):** Test `UserAdminService` against an in-memory `MembershipDbContext`. FsCheck generators produce random `UserBusiness` and `UserBusinessPermission` states.

### Unit Tests (Example-Based)

Unit tests cover:
- Specific examples for each action type (Insert/Update/Delete) in the interceptor.
- Edge cases: null `HttpContext`, missing claim, `AuditLog` entity in change tracker (recursion guard), `SaveChanges` failure (no audit records written).
- Controller validation: `dateFrom > dateTo` returns error JSON; service exception returns error JSON.
- Self-deactivation guard in `AdminController.ToggleStatus`.
- `PageSize` clamping at boundaries (0, 1, 100, 101).

### Integration Tests

Integration tests (using a real SQL Server test database or `Testcontainers`) cover:
- Identity-generated PK capture: insert an entity, verify `AuditLog.RecordId` equals the generated key.
- End-to-end audit flow: call a service method that modifies an entity, verify the `AuditLog` record is written with correct values.
- `AuditController.Search` returns correct JSON shape and HTTP 200.
- `AdminController.UpdatePermission` and `ToggleStatus` return correct JSON and persist changes.

### UI / Smoke Tests

- Audit Log Viewer page loads without error (HTTP 200).
- Filter dropdowns are populated (table names, users).
- User Management page loads and displays users.
- Module Access page loads for a valid `userBusinessId`.

