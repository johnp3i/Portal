# Implementation Plan: System Logs Viewer

## Overview

Implement a SuperAdmin-only System Logs Viewer that reads from the `Portal.Logging` database (`[dbo].[Logs]` table) via a dedicated read-only `LoggingDbContext`. The feature follows the established Audit Log viewer pattern: Controller → Service → Repository with a Razor view featuring filter card + data table + expandable detail rows + pagination. No database migration is needed — the table is auto-created by Serilog's MSSqlServer sink.

## Tasks

- [x] 1. Create LogEntry entity and LoggingDbContext
  - [x] 1.1 Create the LogEntry entity class
    - Create file `Portal.Infrastructure/Entities/LogEntry.cs`
    - Map all columns from `[dbo].[Logs]`: Id (long), Message, MessageTemplate, Level, TimeStamp (DateTime), Exception, Properties, CorrelationId, UserId, BusinessId (int?), SourceContext, RequestPath, MachineName
    - All string properties nullable except Id and TimeStamp
    - No navigation properties — standalone read-only entity
    - _Requirements: 1.3_

  - [x] 1.2 Create the LoggingDbContext class
    - Create file `Portal.Infrastructure/Data/LoggingDbContext.cs`
    - Inherit from `DbContext`, accept `DbContextOptions<LoggingDbContext>`
    - Expose `DbSet<LogEntry> Logs` property
    - In `OnModelCreating`: map entity to table `"Logs"` in schema `"dbo"`, configure `HasKey(e => e.Id)`, set `HasMaxLength` for Level (128), CorrelationId (128), UserId (450), SourceContext (512), RequestPath (512), MachineName (128)
    - This context will be configured with `QueryTrackingBehavior.NoTracking` at registration time
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Create SystemLogFilter model and SystemLogQueryRepository
  - [x] 2.1 Create the SystemLogFilter model
    - Create file `Portal.Infrastructure/Models/SystemLogFilter.cs`
    - Properties: Level (string?), DateFrom (DateTime?), DateTo (DateTime?), UserId (string?), CorrelationId (string?), SourceContext (string?), RequestPath (string?), PageNumber (int, default 1), PageSize (int, default 50)
    - _Requirements: 2.1, 2.4_

  - [x] 2.2 Create the SystemLogQueryRepository class
    - Create file `Portal.Infrastructure/Repositories/SystemLogQueryRepository.cs`
    - Inherit from `GenericStoredProcedureRepository<LogEntry>`
    - Constructor accepts `DbContext context`
    - Implement `GetPagedAsync(string? level, DateTime? dateFrom, DateTime? dateTo, string? userId, string? correlationId, string? sourceContext, string? requestPath, int skip, int take)` returning `Task<(List<LogEntry> Items, int TotalCount)>`
    - Apply filters with AND logic using EF Core LINQ; Level filter uses case-insensitive comparison (`.ToLower() == level.ToLower()`); RequestPath uses `.Contains()`
    - Order by `TimeStamp` descending, apply Skip/Take
    - Implement `GetDistinctLevelsAsync()` returning `Task<List<string>>` — distinct non-null Level values sorted alphabetically
    - Implement `GetDistinctSourceContextsAsync()` returning `Task<List<string>>` — distinct non-null SourceContext values sorted alphabetically
    - Wrap all data access in try/catch with rethrow per repository standards
    - _Requirements: 1.5, 2.2, 2.3, 2.6, 2.9_

- [x] 3. Create ISystemLogQueryService and SystemLogQueryService
  - [x] 3.1 Create the ISystemLogQueryService interface
    - Create file `Portal.Infrastructure/Services/ISystemLogQueryService.cs`
    - Methods: `Task<PagedResult<LogEntry>> GetLogsAsync(SystemLogFilter filter)`, `Task<List<string>> GetDistinctLevelsAsync()`, `Task<List<string>> GetDistinctSourceContextsAsync()`
    - _Requirements: 2.1, 2.4, 2.5_

  - [x] 3.2 Implement SystemLogQueryService
    - Create file `Portal.Infrastructure/Services/SystemLogQueryService.cs`
    - Inject `SystemLogQueryRepository` via constructor
    - In `GetLogsAsync`: clamp PageSize to [1, 200] using `Math.Clamp`, clamp PageNumber to minimum 1 using `Math.Max`, calculate skip = (pageNumber - 1) * pageSize
    - Call repository `GetPagedAsync` with clamped values
    - Calculate totalPages; if pageNumber > totalPages and totalCount > 0, return empty Items with correct metadata
    - Return `PagedResult<LogEntry>` (reuse existing generic class from `Portal.Infrastructure.Models`)
    - No tenant scoping — SuperAdmins see all platform logs
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [ ]* 3.3 Write property test for pagination clamping (Property 3)
    - **Property 3: Pagination clamping**
    - For any integer PageSize, effective value equals `Math.Clamp(PageSize, 1, 200)`; for any PageNumber, effective value equals `Math.Max(PageNumber, 1)`
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.4, 2.8**

  - [ ]* 3.4 Write property test for page size bounds result count (Property 4)
    - **Property 4: Page size bounds result count**
    - For any query where results exist, items count <= effective PageSize and TotalCount equals total matching records regardless of pagination
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.4, 2.5**

- [x] 4. Checkpoint - Ensure infrastructure layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Register dependencies in Program.cs
  - [x] 5.1 Add DI registrations for LoggingDbContext, SystemLogQueryRepository, and ISystemLogQueryService
    - In `Portal.Web/Program.cs`, add after the existing Database Contexts section:
    - Register `LoggingDbContext` with `AddDbContext<LoggingDbContext>` using `builder.Configuration.GetConnectionString("LoggingDb")` and `options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`
    - Register `SystemLogQueryRepository` as scoped: `new SystemLogQueryRepository(sp.GetRequiredService<LoggingDbContext>())`
    - Register `ISystemLogQueryService` → `SystemLogQueryService` as scoped
    - Place registrations in the "Audit & User Admin" section or a new "System Logs" comment section
    - Add required `using Portal.Infrastructure.Data;` if not already present (for LoggingDbContext)
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 6. Create SystemLogsController
  - [x] 6.1 Implement the SystemLogsController
    - Create file `Portal.Web/Controllers/SystemLogsController.cs`
    - Attributes: `[Authorize(Roles = "SuperAdmin")]`, `[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]`, `[Route("Admin/SystemLogs")]`
    - Inject `ISystemLogQueryService` via constructor
    - `Index` action (`[HttpGet("")]`): call `GetDistinctLevelsAsync()` and `GetDistinctSourceContextsAsync()`, populate `ViewBag.Levels` and `ViewBag.SourceContexts`, return `View()`
    - `Search` action (`[HttpGet("Search")]`): accept query params (level, dateFrom, dateTo, userId, correlationId, sourceContext, requestPath, page, pageSize)
    - Validate dateFrom > dateTo → return `Json(new { success = false, message = "Date From cannot be greater than Date To." })`
    - Build `SystemLogFilter`, call service, project results to anonymous type with camelCase properties (id, timeStamp, level, message, exception, properties, userId, correlationId, sourceContext, requestPath, machineName)
    - Return `Json(new { success = true, data, totalCount, currentPage, totalPages })`
    - Catch exceptions: log with `Serilog.Log.Error(ex, ...)`, return `Json(new { success = false, message = "The search could not be completed. Please try again." })`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [ ]* 6.2 Write unit tests for SystemLogsController
    - Test authorization attribute presence (SuperAdmin role)
    - Test Search returns error JSON when dateFrom > dateTo
    - Test Search returns error JSON when service throws exception
    - Test Index populates ViewBag with levels and source contexts
    - _Requirements: 3.1, 3.4, 3.5, 3.6_

- [x] 7. Create the System Logs Viewer view
  - [x] 7.1 Create Views/SystemLogs/Index.cshtml
    - Create file `Portal.Web/Views/SystemLogs/Index.cshtml`
    - **Topbar**: eyebrow "Administration", heading "System Logs", subtitle "Browse and investigate application-level log entries."
    - **Filter card** (`.glass.card-pad`, `margin-bottom:22px`): Log Level dropdown (All, Error, Warning, Information, Debug, Fatal), Date From (date input), Date To (date input), User (text input), Correlation ID (text input), Source Context dropdown (populated from `ViewBag.SourceContexts`), Request Path (text input), Filter button (`.btn.btn-primary`), Clear button (`.btn.btn-secondary`)
    - **Data table card** (`.glass.card-pad`): table with columns — expand control, TimeStamp (monospace, `yyyy-MM-dd HH:mm:ss`), Level (colored badge), Message (truncated 120 chars + ellipsis), User, Source Context, Correlation ID
    - **Level badge colors**: Error = `#C24A4A` bg `#fceaea`, Warning = `#C8912E` bg `#fdf4e6`, Information = `#0D5EA6` bg `#e8f2fa`, Debug = `#6B7B8D` bg `#eef2f5`, Fatal = `#C24A4A` bg `#fceaea`
    - **Expandable detail rows**: show full Message, Exception + stack trace (hidden if null), Properties (formatted), Request Path, Machine Name; only one row expanded at a time (accordion behavior)
    - **Pagination**: below table, `margin-top:18px`, "Showing X–Y of Z" info + page buttons (Prev/Next + numbered), default pageSize 50
    - **Empty state**: "No log entries found matching the selected filters." inside table card
    - **JavaScript** (`@section Scripts`): `loadSystemLogs(page)` function with `BlockUI.show()` / `BlockUI.hide()`, fetch to `/Admin/SystemLogs/Search`, client-side date validation with SweetAlert2, `clearFilters()`, `toggleDetail(idx)` with accordion, `renderTable()`, `renderPagination()`, auto-load on `DOMContentLoaded`
    - Follow MyChair Design System: Manrope headings, Inter body, glass card containers
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11, 4.12, 4.13, 4.14, 4.15_

- [x] 8. Add navigation link for System Logs
  - [x] 8.1 Add "System Logs" link to ModuleNavigation ViewComponent
    - Edit `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml`
    - In the Administration section (inside the `@if (Model.TryGetValue("audit", ...))` block), add a "System Logs" nav link between the Audit Log link and the Users link
    - Add active state check: `var isSystemLogsActive = currentController.Equals("SystemLogs", StringComparison.OrdinalIgnoreCase);`
    - Use an SVG icon (log/terminal style) and href `/Admin/SystemLogs`
    - Link only visible within the audit access block (SuperAdmin-gated)
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 10. Write property tests for ordering and filter composition
  - [ ]* 10.1 Write property test for ordering invariant (Property 1)
    - **Property 1: Ordering invariant**
    - For any set of log entries returned by the service (regardless of filter combination), TimeStamp values are in non-increasing order
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.2, 2.6**

  - [ ]* 10.2 Write property test for filter AND composition (Property 2)
    - **Property 2: Filter AND composition**
    - For any combination of non-null filter parameters, every returned record satisfies ALL active predicates simultaneously
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 2.3, 2.9**

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `[dbo].[Logs]` table already exists (created by Serilog MSSqlServer sink) — no migration needed
- Reuses existing `PagedResult<T>` from `Portal.Infrastructure.Models`
- Reuses existing `GenericStoredProcedureRepository<T>` base class
- No tenant scoping — SuperAdmins see all platform logs for cross-tenant debugging
- `LoggingDbContext` is configured with `QueryTrackingBehavior.NoTracking` for performance (read-only)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2", "5.1"] },
    { "id": 4, "tasks": ["3.3", "3.4", "6.1"] },
    { "id": 5, "tasks": ["6.2", "7.1", "8.1"] },
    { "id": 6, "tasks": ["10.1", "10.2"] }
  ]
}
```
