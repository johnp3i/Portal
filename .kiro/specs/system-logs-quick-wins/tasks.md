# Implementation Plan: System Logs Quick Wins

## Overview

This plan implements four enhancements to the existing System Logs Viewer: KPI Cards, Auto-Refresh Toggle, Copy Correlation ID Button, and Export to CSV. Each enhancement builds incrementally on the existing controller/service/repository architecture. The implementation starts with backend infrastructure (models, repository, service), then controller endpoints, and finally the view layer where all features are wired together.

## Tasks

- [x] 1. Create new model DTOs
  - [x] 1.1 Create `SystemLogKpiCounts` model
    - Create file `Portal.Infrastructure/Models/SystemLogKpiCounts.cs`
    - Define properties: `ErrorCount24h`, `WarningCount24h`, `TotalToday` (all `int`)
    - _Requirements: 1.7_

  - [x] 1.2 Create `ExportResult<T>` generic model
    - Create file `Portal.Infrastructure/Models/ExportResult.cs`
    - Define properties: `Items` (`List<T>`), `TotalCount` (`int`), `IsTruncated` (`bool`)
    - _Requirements: 4.4_

- [x] 2. Implement repository methods
  - [x] 2.1 Add `GetKpiCountsAsync()` to `SystemLogQueryRepository`
    - Add method to `Portal.Infrastructure/Repositories/SystemLogQueryRepository.cs`
    - Use a single LINQ query with conditional counts: Errors (Level == "Error" AND TimeStamp >= 24h ago), Warnings (Level == "Warning" AND TimeStamp >= 24h ago), Total Today (TimeStamp >= today's UTC midnight)
    - Return a tuple `(int ErrorCount24h, int WarningCount24h, int TotalToday)`
    - Wrap in try/catch with rethrow per repository standards
    - _Requirements: 1.3, 1.4, 1.5, 1.7_

  - [x] 2.2 Add `GetAllMatchingAsync()` to `SystemLogQueryRepository`
    - Add method to `Portal.Infrastructure/Repositories/SystemLogQueryRepository.cs`
    - Accept same filter parameters as `GetPagedAsync` plus `int maxRows`
    - Reuse the same filter logic (level, dateFrom, dateTo, userId, correlationId, sourceContext, requestPath)
    - Replace Skip/Take with `.Take(maxRows)`, order by TimeStamp DESC
    - Return `(List<LogEntry> Items, int TotalCount)` where TotalCount is the unfiltered-by-take count
    - Wrap in try/catch with rethrow per repository standards
    - _Requirements: 4.2, 4.3, 4.4_

- [x] 3. Implement service layer methods
  - [x] 3.1 Add `GetKpiCountsAsync()` to interface and service
    - Add `Task<SystemLogKpiCounts> GetKpiCountsAsync()` to `ISystemLogQueryService`
    - Implement in `SystemLogQueryService`: call repository method, map tuple to `SystemLogKpiCounts` DTO
    - _Requirements: 1.7_

  - [x] 3.2 Add `GetExportLogsAsync()` to interface and service
    - Add `Task<ExportResult<LogEntry>> GetExportLogsAsync(SystemLogFilter filter, int maxRows = 10000)` to `ISystemLogQueryService`
    - Implement in `SystemLogQueryService`: call `GetAllMatchingAsync`, map to `ExportResult<LogEntry>` with `IsTruncated = (TotalCount > maxRows)`
    - Clamp filter values (same validation as `GetLogsAsync`)
    - _Requirements: 4.3, 4.4_

- [x] 4. Checkpoint - Ensure all backend compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement controller endpoints
  - [x] 5.1 Modify `Index()` action to include KPI counts
    - In `SystemLogsController.Index()`, call `GetKpiCountsAsync()` wrapped in try/catch
    - On success: set `ViewBag.KpiCounts` to the `SystemLogKpiCounts` object
    - On failure: set `ViewBag.KpiCounts = null` (page continues loading)
    - _Requirements: 1.6, 1.9_

  - [x] 5.2 Add `GetKpiCounts()` AJAX endpoint
    - Add `[HttpGet("KpiCounts")]` action to `SystemLogsController`
    - Call `GetKpiCountsAsync()`, return JSON `{ success, errorCount24h, warningCount24h, totalToday }`
    - Wrap in try/catch, return `{ success: false, message }` on error
    - _Requirements: 1.8_

  - [x] 5.3 Add `ExportCsv()` endpoint
    - Add `[HttpGet("ExportCsv")]` action to `SystemLogsController`
    - Accept same filter query parameters as `Search` (minus page/pageSize)
    - Validate dateFrom/dateTo (same as Search)
    - Call `GetExportLogsAsync(filter, 10000)`
    - Return JSON `{ success, data, totalCount, isTruncated }` with same field mapping as Search
    - Wrap in try/catch, return `{ success: false, message }` on error
    - _Requirements: 4.2, 4.3, 4.4, 4.10_

- [x] 6. Checkpoint - Ensure controller compiles and endpoints are accessible
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement KPI Cards in the view
  - [x] 7.1 Add KPI Cards HTML section to `Index.cshtml`
    - Add a KPI cards row between the topbar and the filter card section
    - Render three cards using `glass card-pad` pattern with 4px left border accent
    - Card 1: "Errors (24h)" — border #C24A4A, value colour #C24A4A, subtitle "Last 24 hours"
    - Card 2: "Warnings (24h)" — border #C8912E, value colour #C8912E, subtitle "Last 24 hours"
    - Card 3: "Total Entries Today" — border #0D5EA6, value colour #0D5EA6, subtitle "Since midnight UTC"
    - Use server-side `ViewBag.KpiCounts` for initial values; display "—" if null
    - Format values with locale-appropriate thousands separators (`.toLocaleString()`)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.9, 1.10_

- [x] 8. Implement Auto-Refresh Toggle in the view
  - [x] 8.1 Add auto-refresh toggle button and JavaScript logic
    - Add toggle button after the Clear button in the filter card section
    - Default state: disabled (outline style, no active indicator)
    - Active state: primary colour background with pulsing dot CSS animation
    - On enable: start `setInterval` with 30-second period calling `loadSystemLogs(currentPage)` without BlockUI
    - On disable: `clearInterval`, revert to inactive style
    - On manual Filter click or page change: reset the 30-second timer (`clearInterval` + `setInterval`)
    - On page unload (`beforeunload`): `clearInterval` to prevent orphaned requests
    - On auto-refresh tick: also fetch `/Admin/SystemLogs/KpiCounts` and update KPI card values
    - On failure: silently skip, retain current data, do not disable auto-refresh
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 1.8_

- [x] 9. Implement Copy Correlation ID Button in the view
  - [x] 9.1 Add copy button rendering and clipboard logic
    - In `renderTable()`, render a clipboard icon button adjacent to correlation ID text when value is non-null/non-empty
    - Add `aria-label="Copy correlation ID"` for accessibility
    - Display "—" with no button when correlation ID is null/empty
    - On click: call `navigator.clipboard.writeText(correlationId)`
    - On success: show tooltip "Copied!" near the button, auto-dismiss after 2 seconds
    - On failure: show SweetAlert2 error "Failed to copy to clipboard."
    - Use `event.stopPropagation()` to prevent row expansion
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 10. Implement Export to CSV in the view
  - [x] 10.1 Add Export CSV button and client-side CSV generation logic
    - Add "Export CSV" button after the auto-refresh toggle in the filter card section
    - On click: suspend auto-refresh if active, show `BlockUI.show('Exporting logs...')`
    - Fetch `/Admin/SystemLogs/ExportCsv` with current filter params
    - On error response: `BlockUI.hide()`, SweetAlert2 error "Export failed. Please try again."
    - On 0 records: `BlockUI.hide()`, SweetAlert2 info "No records to export."
    - On truncated: show SweetAlert2 info "Export limited to 10,000 records. Apply more specific filters to narrow the result set." before download
    - Generate CSV client-side: RFC 4180 escaping (commas, double quotes, line breaks), header row, column order (TimeStamp, Level, Message, Exception, UserId, CorrelationId, SourceContext, RequestPath, MachineName)
    - Create Blob with UTF-8 BOM (`\xEF\xBB\xBF`), trigger download via temporary `<a>` element
    - Filename pattern: `SystemLogs_YYYY-MM-DD_HHmmss.csv` (client local time)
    - After completion or failure: `BlockUI.hide()`, resume auto-refresh (restart 30s timer)
    - _Requirements: 4.1, 4.2, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11, 4.12_

- [x] 11. Final checkpoint - Ensure all features work together
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 12. Write property test for KPI count correctness
  - [ ]* 12.1 Write property test for KPI count aggregation
    - **Property 1: KPI count correctness**
    - **Validates: Requirements 1.3, 1.4, 1.5**
    - Use FsCheck with xUnit integration
    - Generate random lists of LogEntry objects with varying Level values (Error, Warning, Information, Debug, Fatal, null) and TimeStamp values spanning the last 48 hours
    - Seed an in-memory database (or mock), call `GetKpiCountsAsync()`, assert returned counts match a simple LINQ filter applied to the same data
    - Minimum 100 iterations

  - [ ]* 12.2 Write property test for CSV RFC 4180 formatting
    - **Property 2: CSV RFC 4180 formatting correctness**
    - **Validates: Requirements 4.6**
    - Generate random LogEntry objects with fields containing arbitrary Unicode strings (commas, double quotes, CR/LF, tabs, empty strings, null)
    - Run the CSV formatting function, parse output back using a standards-compliant CSV parser
    - Assert parsed values match original input values
    - Minimum 100 iterations

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design uses C# for all backend code; client-side JavaScript is vanilla (no framework)
- All new endpoints are SuperAdmin-only (inherited from controller-level `[Authorize(Roles = "SuperAdmin")]`)
- No new NuGet packages required for core implementation; FsCheck needed only for optional property tests

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["7.1", "9.1"] },
    { "id": 5, "tasks": ["8.1", "10.1"] },
    { "id": 6, "tasks": ["12.1", "12.2"] }
  ]
}
```
