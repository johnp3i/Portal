# Implementation Plan: Audit & System Administration

## Overview

This plan implements the Audit & System Administration module for the Portal platform. It adds automatic audit logging via an EF Core `SaveChangesInterceptor`, a SuperAdmin-only audit log viewer with filtered/paginated search, and a SuperAdmin-only user management interface with per-module permission controls. The implementation follows the established MVC + Service Layer pattern with Database-First EF Core, consistent with all existing Portal modules.

## Tasks

- [x] 1. Infrastructure — Data Layer

  - [x] 1.1 SQL Migration — Audit Log Query Indexes
    - Create `Portal.Database/Migrations/060_AddAuditLogQueryIndexes.sql`
    - Add `IX_AuditLog_BusinessId_Timestamp` (BusinessId ASC, Timestamp DESC) on `[audit].[AuditLog]`
    - Add `IX_AuditLog_BusinessId_Action` (BusinessId, Action) with INCLUDE (Timestamp, TableName, UserId, RecordId)
    - Both `CREATE INDEX` statements wrapped in `IF NOT EXISTS` guards (idempotent)
    - _Requirements: 2.1, 2.7_

  - [x] 1.2 AuditLogFilter Model
    - Create `Portal.Infrastructure/Models/AuditLogFilter.cs`
    - Properties: `TableName` (string?), `Action` (string?), `UserId` (string?), `DateFrom` (DateTime?), `DateTo` (DateTime?), `PageNumber` (int, default 1), `PageSize` (int, default 20)
    - No validation attributes — clamping is handled by the service layer
    - _Requirements: 2.1, 2.5_

  - [x] 1.3 AuditLogQueryRepository
    - Create `Portal.Infrastructure/Repositories/AuditLogQueryRepository.cs`
    - Extends `GenericStoredProcedureRepository<AuditLog>`
    - Implement `GetPagedAsync(int businessId, string? tableName, string? action, string? userId, DateTime? dateFrom, DateTime? dateTo, int skip, int take)` — EF Core LINQ, all non-null filters applied with AND logic, ordered by Timestamp DESC, returns `(List<AuditLog> Items, int TotalCount)`
    - Implement `GetDistinctTableNamesAsync(int businessId)` — returns distinct TableName values alphabetically sorted
    - All methods use `try/catch { throw; }` per repository standards
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.7_

  - [x] 1.4 IAuditLogQueryService and AuditLogQueryService
    - Create `Portal.Infrastructure/Services/IAuditLogQueryService.cs` and `AuditLogQueryService.cs`
    - Interface: `Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter)` and `Task<List<string>> GetDistinctTableNamesAsync()`
    - Implementation: clamp PageSize to [1, 100] and PageNumber to minimum 1; scope all queries to `ICurrentTenantService.CurrentBusinessId`; return correctly populated `PagedResult<AuditLog>`
    - When PageNumber exceeds TotalPages, Items is empty but TotalCount and TotalPages remain accurate
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [x] 1.5 AuditInterceptor
    - Create `Portal.Infrastructure/Interceptors/AuditInterceptor.cs` (new `Interceptors/` folder)
    - Extends `SaveChangesInterceptor`; constructor injects `ICurrentTenantService`, `IHttpContextAccessor`, `AuditLogRepository`
    - Private `List<AuditEntry>? _pendingEntries` field (safe as scoped); private sealed `AuditEntry` inner class
    - Phase 1 (`SavingChanges`/`SavingChangesAsync`): filter to Added/Modified/Deleted, skip AuditLog entities (recursion guard), build AuditEntry per entity; for Modified/Deleted read RecordId now; for Added leave RecordId empty; serialize only scalar non-shadow properties; for Modified include only IsModified==true properties in OldValues/NewValues
    - Phase 2 (`SavedChanges`/`SavedChangesAsync`): fill identity PKs for Added entries from `entry.CurrentValues`; call `_auditLogRepository.InsertAsync` per entry; clear pending list; swallow and log InsertAsync failures (main save already succeeded — documented exception)
    - UserId from `ClaimTypes.NameIdentifier`; null when HttpContext is null or claim absent — record still written
    - Timestamp is `DateTime.UtcNow` captured at Phase 1
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 1.12, 1.13, 1.14_


- [x] 2. Infrastructure — User Admin Data Layer

  - [x] 2.1 UserAdminFilter, UserAdminDto, and UserModulePermissionDto Models
    - Create `Portal.Infrastructure/Models/UserAdminFilter.cs`: `SearchTerm` (string?), `StatusFilter` (string?), `PageNumber` (int, default 1), `PageSize` (int, default 20)
    - Create `Portal.Infrastructure/Models/UserAdminDto.cs`: `UserBusinessId` (int), `UserId` (string), `FullName` (string), `Email` (string), `Role` (string), `IsActive` (bool), `LastLoginUtc` (DateTime?)
    - Create `Portal.Infrastructure/Models/UserModulePermissionDto.cs`: `PermissionId` (int?), `Module` (string), `AccessLevel` (string), `IsActive` (bool)
    - _Requirements: 5.1, 5.2, 5.4, 6.1, 6.2_

  - [x] 2.2 UserAdminRepository
    - Create `Portal.Infrastructure/Repositories/UserAdminRepository.cs`
    - Extends `GenericStoredProcedureRepository<UserBusiness>`; constructor accepts `DbContext context`
    - Implement `GetUsersPagedAsync(int businessId, string? searchTerm, bool? isActive, int skip, int take)` — EF Core LINQ with `.Include(ub => ub.User)`, case-insensitive contains on full name or email, returns `(List<UserBusiness> Items, int TotalCount)`
    - Implement `GetByIdAsync(int userBusinessId)` — returns `UserBusiness?` with `.Include(ub => ub.User)`
    - Implement `DeactivateAsync(int userBusinessId, DateTime deactivatedAtUtc)` — parameterized `ExecuteSqlRawAsync`, sets IsActive=false, DeactivatedAtUtc=@value
    - Implement `ReactivateAsync(int userBusinessId)` — parameterized `ExecuteSqlRawAsync`, sets IsActive=true, DeactivatedAtUtc=NULL
    - Implement `GetPermissionsAsync(int userBusinessId)` — returns `List<UserBusinessPermission>`
    - Implement `UpsertPermissionAsync(int userBusinessId, string module, string accessLevel, bool isActive, DateTime? deactivatedAtUtc)` — checks for existing record, inserts or updates via `ExecuteSqlRawAsync`
    - All methods use `try/catch { throw; }`; SQL uses full table names `[membership].[UserBusiness]`, `[membership].[UserBusinessPermission]`; all nullable SQL params use `?? (object)DBNull.Value`
    - _Requirements: 5.1, 5.3, 5.5, 5.6, 6.1, 6.5, 6.6_

  - [x] 2.3 IUserAdminService and UserAdminService
    - Create `Portal.Infrastructure/Services/IUserAdminService.cs` and `UserAdminService.cs`
    - Interface: `GetUsersAsync`, `DeactivateUserAsync`, `ReactivateUserAsync`, `GetUserPermissionsAsync`, `UpdatePermissionAsync`
    - `GetUsersAsync`: clamp pagination, map UserBusiness+User to UserAdminDto (FullName = FirstName + " " + LastName), scope to current tenant
    - `GetUserPermissionsAsync`: return one UserModulePermissionDto per module in `PortalModules.All`, defaulting to AccessLevel="none", IsActive=false, PermissionId=null for modules with no existing record
    - `UpdatePermissionAsync`: validate module (`PortalModules.IsValid`) and access level (`AccessLevels.IsValid`); upsert permission; set IsActive=false+DeactivatedAtUtc=UtcNow for "none", IsActive=true+DeactivatedAtUtc=null for "full"/"readonly"; write AuditLog entry (failures logged and swallowed)
    - `DeactivateUserAsync`/`ReactivateUserAsync`: call repository, write AuditLog entry (failures logged and swallowed)
    - Return `ServiceResult.Fail` for validation errors; `ServiceResult.Ok()` on success
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.8, 6.1, 6.5, 6.6, 6.8_


- [x] 3. Web — Audit Controller & View

  - [x] 3.1 AuditController
    - Create `Portal.Web/Controllers/AuditController.cs`
    - Apply `[Authorize(Roles = "SuperAdmin")]`, `[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]`, `[Route("Admin/Audit")]`
    - Constructor injects `IAuditLogQueryService`, `MembershipDbContext`, `ICurrentTenantService`
    - `[HttpGet("")]` `Index()`: load distinct table names and business users for filter dropdowns; assign to `ViewBag.TableNames` and `ViewBag.Users`; return `View()`
    - `[HttpGet("Search")]` `Search(...)`: validate dateFrom <= dateTo; build AuditLogFilter; call `GetAuditLogsAsync`; resolve `userDisplayName` per result from loaded users dictionary; return `Json(new { success = true, data, totalCount, currentPage, totalPages })`
    - On dateFrom > dateTo: return `Json(new { success = false, message = "Date From cannot be greater than Date To." })`
    - On exception: log via Serilog, return `Json(new { success = false, message = "The search could not be completed. Please try again." })`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 3.2 Audit Log Viewer View
    - Create `Portal.Web/Views/Audit/Index.cshtml`
    - Topbar: eyebrow "Administration", heading "Audit Log"
    - Filter card (`<section class="glass card-pad" style="margin-bottom:22px;">`): flex row (gap 14px, align-items flex-end) with Table Name select (ViewBag.TableNames, min-width 180px), Action select (All/Insert/Update/Delete), User select (ViewBag.Users, min-width 180px), Date From date input, Date To date input, Filter and Clear buttons (padding-bottom 2px wrapper)
    - Data table card (`<section class="glass card-pad">`): `<table class="data-table">` with columns: expand toggle, Timestamp, User, Action, Table, Record ID; pagination row (margin-top 18px, flex space-between)
    - JS `loadAuditLogs(page)`: client-side date validation (Swal warning if invalid, no submit); `BlockUI.show()`; fetch `/Admin/Audit/Search?...`; `BlockUI.hide()`; call `renderTable` and `renderPagination`
    - `renderTable`: two `<tr>` per record (summary + hidden detail); action badges `badge--insert`/`badge--update`/`badge--delete`; expand/collapse on click (one open at a time, button rotates 180°); Update detail: two-column grid with changed properties highlighted (`background: rgba(13,94,166,0.08); border-left: 3px solid #0D5EA6`); Insert: full-width NewValues; Delete: full-width OldValues; empty state message when no records
    - `renderPagination`: "Showing X–Y of Z" info, page buttons with `page-btn--active`, Prev/Next disabled at boundaries
    - `document.addEventListener('DOMContentLoaded', () => loadAuditLogs(1))` triggers initial load
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11, 4.12_


- [x] 4. Web — Admin Controller & Views

  - [x] 4.1 UpdatePermissionRequest and ToggleStatusRequest Models
    - Create `Portal.Web/Models/UpdatePermissionRequest.cs`: `UserBusinessId` (int), `Module` (string), `AccessLevel` (string)
    - Create `Portal.Web/Models/ToggleStatusRequest.cs`: `UserBusinessId` (int), `Activate` (bool — true = reactivate, false = deactivate)
    - Plain POCOs in `Portal.Web.Models` namespace; no validation attributes
    - _Requirements: 5.3, 6.5, 6.6_

  - [x] 4.2 AdminController
    - Create `Portal.Web/Controllers/AdminController.cs`
    - Apply `[Authorize(Roles = "SuperAdmin")]`, `[Route("Admin/Users")]`
    - Constructor injects `IUserAdminService`, `ICurrentTenantService`, `UserManager<ApplicationUser>`
    - `[HttpGet("")]` `Index(string? searchTerm, string? statusFilter, int page = 1)`: build UserAdminFilter; call `GetUsersAsync`; set `ViewBag.CurrentUserBusinessId`; return `View(pagedResult)`
    - `[HttpGet("ModuleAccess/{userBusinessId:int}")]` `ModuleAccess(int userBusinessId)`: call `GetUserPermissionsAsync`; pass `PortalModules.All`, permissions, and user info via ViewBag; return `View()`
    - `[HttpPost("UpdatePermission")]` `[ValidateAntiForgeryToken]` `UpdatePermission([FromBody] UpdatePermissionRequest request)`: validate module and access level; call `UpdatePermissionAsync`; return `Json(new { success, message })`
    - `[HttpPost("ToggleStatus")]` `[ValidateAntiForgeryToken]` `ToggleStatus([FromBody] ToggleStatusRequest request)`: guard against self-deactivation; call `DeactivateUserAsync` or `ReactivateUserAsync`; return `Json(new { success, message })`
    - All AJAX actions catch exceptions, log via Serilog, return `Json(new { success = false, message = "..." })`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.7, 5.9, 5.10, 5.11, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.11, 6.12_

  - [x] 4.3 User Management View
    - Create `Portal.Web/Views/Admin/Index.cshtml` with model `PagedResult<UserAdminDto>`
    - Topbar: eyebrow "Administration", heading "Users"
    - Filter card (`<section class="glass card-pad" style="margin-bottom:22px;">`): flex row (gap 14px) with Search text input (placeholder "Name or email...", min-width 240px), Status select (All/Active/Inactive, min-width 160px), Filter and Clear buttons, Invite User button (btn-primary, links to `/Invitation/Create`, float right)
    - Data table card: `<table class="data-table">` (Full Name, Email, Role, Status, Last Login, Actions); clicking a row navigates to `/Admin/Users/ModuleAccess/{userBusinessId}`; Status badge green=Active, red=Inactive; Last Login formatted "dd MMM yyyy HH:mm" or "Never"; Actions: Deactivate (danger) for active, Reactivate (primary) for inactive; self-row buttons disabled with tooltip "You cannot modify your own account."
    - JS `deactivateUser`: Swal confirmation (`confirmButtonColor: '#C24A4A'`); `BlockUI.show()`; POST to `/Admin/Users/ToggleStatus` with `{ userBusinessId, activate: false }`; `BlockUI.hide()`; success Swal then `location.reload()` or error Swal
    - JS `reactivateUser`: same pattern with `confirmButtonColor: '#0D5EA6'` and `activate: true`
    - Filter form submits via GET (resets to page 1); pagination uses GET with `page` query parameter
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.9, 6.10, 6.11, 6.12_

  - [x] 4.4 Module Access Manager View
    - Create `Portal.Web/Views/Admin/ModuleAccess.cshtml`
    - Topbar: eyebrow "Administration", heading "Module Access — {UserFullName}"
    - Single permissions card (`<section class="glass card-pad">`): user info row (name, email, status badge); `<table class="data-table">` (Module, Access Level, Status); one row per module in `PortalModules.All` (from ViewBag.Modules); each row: module display name, segmented radio/toggle (Full | ReadOnly | None) pre-selected to current level, status badge (Active/Inactive); current user's row has all controls disabled with tooltip "You cannot modify your own permissions."; back link to `/Admin/Users`
    - Each row stores current level in `data-current-level` attribute
    - JS `updatePermission(userBusinessId, module, accessLevel, moduleName)`: Swal confirmation (`'#C24A4A'` for "none", `'#0D5EA6'` for grants); on cancel call `revertSelection(module)`; on confirm `BlockUI.show()`; POST to `/Admin/Users/UpdatePermission`; `BlockUI.hide()`; on success show success Swal and call `updateStatusBadge(module, accessLevel)`; on failure call `revertSelection(module)` and show error Swal
    - `revertSelection(module)`: reads `data-current-level` and resets radio/toggle state
    - `updateStatusBadge(module, accessLevel)`: updates status badge without page reload
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11_


- [x] 5. Registration

  - [x] 5.1 DI Registration in Program.cs
    - Register `AuditInterceptor` as scoped: `builder.Services.AddScoped<AuditInterceptor>()`
    - Replace existing `AddDbContext<PortalDbContext>` with factory lambda: `builder.Services.AddDbContext<PortalDbContext>((sp, options) => { options.UseSqlServer(...); options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>()); })`
    - Register `AuditLogQueryRepository` as scoped using `PortalDbContext`
    - Register `IAuditLogQueryService` → `AuditLogQueryService` as scoped
    - Register `UserAdminRepository` as scoped using `MembershipDbContext`
    - Register `IUserAdminService` → `UserAdminService` as scoped
    - Add required `using` directives for new namespaces
    - Do not duplicate or remove the existing `AuditLogRepository` registration
    - _Requirements: 1.1, 2.1, 5.1_


- [x] 6. Property-Based Tests

  - [x] 6.1 PBT — AuditInterceptor Properties (Properties 1–6)
    - Create `Portal.Tests/PropertyBased/AuditInterceptorPropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`; in-memory DbContext with Moq for `ICurrentTenantService` and `IHttpContextAccessor`
    - Each method includes `// Feature: audit-system-administration, Property N: <property_text>` comment
    - **Property 1**: for N entities in Added/Modified/Deleted state (excluding AuditLog), exactly N AuditLog records are written
    - **Property 2**: for any entity in Added/Modified/Deleted state, `AuditLog.Action` is "Insert"/"Update"/"Delete" respectively and no other value
    - **Property 3**: for a Modified entity with a random subset of IsModified=true properties, OldValues and NewValues JSON contain exactly those modified properties (original vs. current values)
    - **Property 4**: `AuditLog.TableName` equals `entry.Metadata.GetTableName()` for the entity type
    - **Property 5**: BusinessId and UserId are resolved from injected services; when HttpContext is null or claim absent, UserId is null and record is still written
    - **Property 6**: AuditLog entities in the change tracker produce zero additional AuditLog records
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.7, 1.8, 1.9, 1.10_

  - [x] 6.2 PBT — AuditLogQueryService Properties (Properties 7–11)
    - Create `Portal.Tests/PropertyBased/AuditLogQueryServicePropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`; in-memory `PortalDbContext` seeded with generated AuditLog records
    - Each method includes `// Feature: audit-system-administration, Property N: <property_text>` comment
    - **Property 7**: every record in the result has BusinessId equal to the current tenant's value; no records from other businesses appear
    - **Property 8**: for any combination of filter parameters, the result set satisfies all specified conditions simultaneously (AND logic) and is a subset of the unfiltered result
    - **Property 9**: for any result set, all consecutive pairs satisfy `items[i].Timestamp >= items[i+1].Timestamp` (descending order)
    - **Property 10**: for any valid PageNumber and PageSize (after clamping): `items.Count <= PageSize`; `TotalPages == Math.Ceiling(TotalCount / (double)PageSize)`; pages do not overlap; when PageNumber > TotalPages, items is empty and TotalCount/TotalPages are still correct
    - **Property 11**: PageSize < 1 is clamped to 1; PageSize > 100 is clamped to 100; values in [1, 100] are used as-is
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [x] 6.3 PBT — UserAdminService Properties (Properties 12–13)
    - Create `Portal.Tests/PropertyBased/UserAdminServicePropertyTests.cs`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`; in-memory `MembershipDbContext`
    - Each method includes `// Feature: audit-system-administration, Property N: <property_text>` comment
    - **Property 12**: for any user-module combination and any valid access level, after `UpdatePermissionAsync` the stored record reflects the new level; "none" → IsActive=false, DeactivatedAtUtc non-null; "full"/"readonly" → IsActive=true, DeactivatedAtUtc=null
    - **Property 13**: `DeactivateUserAsync` → IsActive=false, DeactivatedAtUtc non-null; `ReactivateUserAsync` → IsActive=true, DeactivatedAtUtc=null; operations are inverses (deactivate then reactivate yields IsActive=true, DeactivatedAtUtc=null)
    - _Requirements: 5.3, 5.4, 5.5, 5.6, 6.5, 6.6_


## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1", "4.1"] },
    { "id": 1, "tasks": ["1.3", "2.2"] },
    { "id": 2, "tasks": ["1.4", "2.3"] },
    { "id": 3, "tasks": ["1.5", "3.1", "6.2"] },
    { "id": 4, "tasks": ["4.2", "6.1", "6.3"] },
    { "id": 5, "tasks": ["3.2", "4.3", "4.4", "5.1"] }
  ]
}
```

## Notes

- `PagedResult<T>` and `ServiceResult` already exist in `Portal.Infrastructure/Models/` — do NOT create new files for them.
- `AuditLogRepository` already exists in `Portal.Infrastructure/Repositories/` — do NOT create a new file for it.
- The `AuditLog` entity, `UserBusiness`, `UserBusinessPermission`, and `ApplicationUser` entities already exist — no entity changes required.
- `PortalModules` and `AccessLevels` constants already exist in `Portal.Infrastructure/Constants/`.
- `ModuleAccessAttribute` already exists in `Portal.Web/Security/`.
- Migration `060` is the correct next number (last existing migration is `057`).
- `AuditInterceptor` must be registered as **scoped** (not singleton) to match `PortalDbContext` lifetime. Using a singleton interceptor with a scoped context causes DI lifetime violations.
- The `PortalDbContext` registration in `Program.cs` must be updated from `AddDbContext<PortalDbContext>(options => ...)` to the factory lambda `AddDbContext<PortalDbContext>((sp, options) => ...)` to allow the scoped interceptor to be resolved from the same scope.
- The existing `AuditLogRepository` DI registration in `Program.cs` must be preserved — it is used by existing services and by the new `AuditInterceptor`.
- PBT tests use FsCheck 2.16.6 with `FsCheck.Xunit` (already in `Portal.Tests.csproj`). Use `[Property(MaxTest = 100)]` attribute.
- PBT tests for the interceptor (6.1) should use a test double or in-memory list to capture `AuditLogRepository.InsertAsync` calls rather than querying the database, since the interceptor writes via raw SQL.
- The `UserAdminRepository` is backed by `MembershipDbContext` (not `PortalDbContext`) — pass `MembershipDbContext` in the DI factory lambda.
- `AuditLogQueryRepository` is backed by `PortalDbContext` — pass `PortalDbContext` in the DI factory lambda.
- Audit log write failures within `UserAdminService` (for permission changes and status toggles) must be logged via Serilog and swallowed — they must not fail the primary operation.
