# Design Document: System Logs Quick Wins

## Overview

This design adds four enhancements to the existing System Logs Viewer (`/Admin/SystemLogs`):

1. **Error Count KPI Cards** — Three summary metric cards (Errors 24h, Warnings 24h, Total Today) displayed above the filter panel, providing at-a-glance application health.
2. **Auto-Refresh Toggle** — A 30-second polling mechanism that keeps the log view current without manual interaction.
3. **Copy Correlation ID Button** — One-click clipboard copy of correlation IDs for faster debugging workflows.
4. **Export to CSV** — Download filtered log results as an RFC 4180-compliant CSV file for offline analysis and incident reporting.

All enhancements are SuperAdmin-only and build on the existing controller/service/repository architecture without introducing new dependencies or breaking changes.

## Architecture

The existing layered architecture remains unchanged. New functionality is added at each layer:

```
┌─────────────────────────────────────────────────────────────┐
│                    Index.cshtml (Razor View)                  │
│  ┌──────────┐ ┌──────────────┐ ┌────────┐ ┌─────────────┐  │
│  │ KPI Cards│ │ Auto-Refresh │ │  Copy  │ │ Export CSV  │  │
│  └──────────┘ └──────────────┘ └────────┘ └─────────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │ fetch API
┌─────────────────────────▼───────────────────────────────────┐
│              SystemLogsController (MVC)                       │
│  Index() — returns KPI counts in ViewBag                     │
│  Search() — existing (unchanged)                             │
│  ExportCsv() — new endpoint, returns JSON with records       │
│  GetKpiCounts() — new AJAX endpoint for refresh updates      │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│           ISystemLogQueryService / SystemLogQueryService      │
│  GetLogsAsync(filter) — existing (unchanged)                 │
│  GetKpiCountsAsync() — new: returns 3 counts                 │
│  GetExportLogsAsync(filter, maxRows) — new: unpaged results  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│              SystemLogQueryRepository                         │
│  GetPagedAsync() — existing (unchanged)                      │
│  GetKpiCountsAsync() — new: single query with conditional    │
│  GetAllMatchingAsync() — new: unpaged filtered query         │
└─────────────────────────────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│         LoggingDbContext → [dbo].[Logs] (read-only)          │
└─────────────────────────────────────────────────────────────┘
```

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| KPI counts via single query with conditional aggregation | Minimises database round-trips; a single `SELECT COUNT(CASE...)` is more efficient than 3 separate queries |
| CSV generated client-side from JSON response | Avoids server-side file I/O and temp file cleanup; keeps the controller stateless; allows client to add BOM and set filename |
| Auto-refresh uses `setInterval` with manual reset | Simple, well-understood pattern; no external dependencies; easy to clear on unload |
| Export endpoint returns JSON (not file stream) | Consistent with existing AJAX pattern; allows client to show truncation warning before download; avoids Content-Disposition complexity |
| 10,000 record export cap | Prevents memory exhaustion on server and browser; provides safety net for unfiltered queries |

## Components and Interfaces

### 1. Repository Layer — `SystemLogQueryRepository`

**New method: `GetKpiCountsAsync()`**

```csharp
/// <summary>
/// Returns KPI counts in a single database round-trip using conditional aggregation.
/// </summary>
public async Task<(int ErrorCount24h, int WarningCount24h, int TotalToday)> GetKpiCountsAsync()
```

Implementation uses a single LINQ query with conditional counts:
- Errors: `Level == "Error"` AND `TimeStamp >= DateTime.UtcNow.AddHours(-24)`
- Warnings: `Level == "Warning"` AND `TimeStamp >= DateTime.UtcNow.AddHours(-24)`
- Total Today: `TimeStamp >= today's UTC midnight`

**New method: `GetAllMatchingAsync()`**

```csharp
/// <summary>
/// Returns all matching records (up to maxRows) without pagination, ordered by TimeStamp DESC.
/// </summary>
public async Task<(List<LogEntry> Items, int TotalCount)> GetAllMatchingAsync(
    string? level,
    DateTime? dateFrom,
    DateTime? dateTo,
    string? userId,
    string? correlationId,
    string? sourceContext,
    string? requestPath,
    int maxRows)
```

Reuses the same filter logic as `GetPagedAsync` but replaces Skip/Take with a single `.Take(maxRows)` and returns the unfiltered total count for truncation detection.

### 2. Service Layer — `ISystemLogQueryService`

**New interface methods:**

```csharp
/// <summary>
/// Returns KPI counts (error 24h, warning 24h, total today) in a single round-trip.
/// </summary>
Task<SystemLogKpiCounts> GetKpiCountsAsync();

/// <summary>
/// Returns all matching log entries up to maxRows for CSV export.
/// </summary>
Task<ExportResult<LogEntry>> GetExportLogsAsync(SystemLogFilter filter, int maxRows = 10000);
```

### 3. New Models

**`SystemLogKpiCounts`** — DTO for KPI card values:

```csharp
namespace Portal.Infrastructure.Models;

public class SystemLogKpiCounts
{
    public int ErrorCount24h { get; set; }
    public int WarningCount24h { get; set; }
    public int TotalToday { get; set; }
}
```

**`ExportResult<T>`** — Generic wrapper for export responses with truncation flag:

```csharp
namespace Portal.Infrastructure.Models;

public class ExportResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IsTruncated { get; set; }
}
```

### 4. Controller Layer — `SystemLogsController`

**Modified action: `Index()`**
- Calls `GetKpiCountsAsync()` and passes counts via `ViewBag.KpiCounts` (wrapped in try/catch — null on failure).

**New endpoint: `GET /Admin/SystemLogs/KpiCounts`**
- Returns JSON `{ success, errorCount24h, warningCount24h, totalToday }` for auto-refresh KPI updates.

**New endpoint: `GET /Admin/SystemLogs/ExportCsv`**
- Accepts same filter parameters as `Search` (minus page/pageSize).
- Calls `GetExportLogsAsync(filter, 10000)`.
- Returns JSON `{ success, data, totalCount, isTruncated }`.

### 5. View Layer — `Index.cshtml`

**KPI Cards Section** — Rendered between topbar and filter card using server-side values from `ViewBag.KpiCounts`. Falls back to "—" if null.

**Auto-Refresh Toggle** — Button after Clear button. JavaScript manages `setInterval` with 30-second period. Active state shows pulsing dot via CSS animation.

**Copy Correlation ID** — Clipboard icon button rendered inline in the correlation ID cell. Uses `navigator.clipboard.writeText()`. Shows tooltip on success, SweetAlert2 on failure. `event.stopPropagation()` prevents row expansion.

**Export CSV** — Button after auto-refresh toggle. Client-side CSV generation from JSON response using a helper function that handles RFC 4180 escaping. Creates a Blob with UTF-8 BOM, triggers download via temporary `<a>` element.

## Data Models

### Existing Entity (unchanged)

```csharp
// Portal.Infrastructure.Entities.LogEntry
// Maps to [dbo].[Logs] in Portal.Logging database
public class LogEntry
{
    public long Id { get; set; }
    public string? Message { get; set; }
    public string? MessageTemplate { get; set; }
    public string? Level { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
    public int? BusinessId { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestPath { get; set; }
    public string? MachineName { get; set; }
}
```

### New DTOs

```csharp
// Portal.Infrastructure.Models.SystemLogKpiCounts
public class SystemLogKpiCounts
{
    public int ErrorCount24h { get; set; }
    public int WarningCount24h { get; set; }
    public int TotalToday { get; set; }
}

// Portal.Infrastructure.Models.ExportResult<T>
public class ExportResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IsTruncated { get; set; }
}
```

### CSV Column Schema

| # | Column Name | Source Property | Notes |
|---|-------------|----------------|-------|
| 1 | TimeStamp | `LogEntry.TimeStamp` | ISO 8601 format |
| 2 | Level | `LogEntry.Level` | Raw string |
| 3 | Message | `LogEntry.Message` | RFC 4180 escaped |
| 4 | Exception | `LogEntry.Exception` | RFC 4180 escaped |
| 5 | UserId | `LogEntry.UserId` | Raw string |
| 6 | CorrelationId | `LogEntry.CorrelationId` | Raw string |
| 7 | SourceContext | `LogEntry.SourceContext` | Raw string |
| 8 | RequestPath | `LogEntry.RequestPath` | Raw string |
| 9 | MachineName | `LogEntry.MachineName` | Raw string |

### KPI Query Logic (Conditional Aggregation)

```sql
SELECT
    COUNT(CASE WHEN [Logs].[Level] = 'Error' AND [Logs].[TimeStamp] >= @twentyFourHoursAgo THEN 1 END) AS ErrorCount24h,
    COUNT(CASE WHEN [Logs].[Level] = 'Warning' AND [Logs].[TimeStamp] >= @twentyFourHoursAgo THEN 1 END) AS WarningCount24h,
    COUNT(CASE WHEN [Logs].[TimeStamp] >= @todayMidnightUtc THEN 1 END) AS TotalToday
FROM [dbo].[Logs]
WHERE [Logs].[TimeStamp] >= @twentyFourHoursAgo
   OR [Logs].[TimeStamp] >= @todayMidnightUtc
```

Note: The WHERE clause limits the scan to relevant rows only (last 24h or today), avoiding a full table scan.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: KPI count correctness

*For any* set of log entries with arbitrary Level values and TimeStamp values, `GetKpiCountsAsync()` SHALL return an error count equal to the number of entries where Level equals "Error" and TimeStamp is within the last 24 hours, a warning count equal to the number of entries where Level equals "Warning" and TimeStamp is within the last 24 hours, and a total-today count equal to the number of entries where TimeStamp falls on the current UTC calendar day.

**Validates: Requirements 1.3, 1.4, 1.5**

### Property 2: CSV RFC 4180 formatting correctness

*For any* log entry record containing arbitrary string values (including commas, double quotes, newlines, and empty strings), the CSV formatting function SHALL produce output where: (a) fields containing commas, double quotes, or line breaks are enclosed in double quotes, (b) embedded double-quote characters are escaped by doubling them, (c) the column order matches the defined schema (TimeStamp, Level, Message, Exception, UserId, CorrelationId, SourceContext, RequestPath, MachineName), and (d) parsing the generated CSV row back produces the original field values.

**Validates: Requirements 4.6**

## Error Handling

| Scenario | Handling Strategy |
|----------|-------------------|
| KPI count query fails on page load | Controller catches exception, sets `ViewBag.KpiCounts = null`. View renders "—" for all three cards. Page continues loading normally. |
| KPI count AJAX refresh fails | Client-side catch block silently retains current displayed values. No user notification. |
| Auto-refresh poll fails (network/server error) | Silent skip — retain current data, do not disable auto-refresh, attempt next poll at regular interval. |
| Clipboard API unavailable or write fails | SweetAlert2 error: "Failed to copy to clipboard." |
| Export endpoint returns server error | BlockUI.hide(), SweetAlert2 error: "Export failed. Please try again." |
| Export returns 0 records | SweetAlert2 info: "No records to export." No CSV generated. |
| Export exceeds 10,000 records | Return 10,000 records with `isTruncated: true`. Client shows SweetAlert2 info before download. |
| Export in progress + auto-refresh active | Suspend auto-refresh interval before export request. Resume (restart 30s timer) after export completes or fails. |
| Page unload while auto-refresh active | `beforeunload` event listener calls `clearInterval()`. |

### Error Handling Patterns

```javascript
// Auto-refresh: silent failure
async function autoRefreshTick() {
    try {
        const response = await fetch(`/Admin/SystemLogs/Search?${params}`);
        const data = await response.json();
        if (data.success) { renderTable(data.data); renderPagination(...); }
        // On failure: do nothing, retain current data
    } catch (e) {
        // Silent skip — next tick will retry
    }
}

// Export: explicit error handling with BlockUI
async function exportCsv() {
    suspendAutoRefresh();
    BlockUI.show('Exporting logs...');
    try {
        const response = await fetch(`/Admin/SystemLogs/ExportCsv?${params}`);
        const data = await response.json();
        BlockUI.hide();
        if (!data.success) {
            Swal.fire({ title: 'Error', text: 'Export failed. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' });
        } else if (data.data.length === 0) {
            Swal.fire({ title: 'No Data', text: 'No records to export.', icon: 'info', confirmButtonColor: '#0D5EA6' });
        } else {
            if (data.isTruncated) {
                await Swal.fire({ title: 'Export Truncated', text: 'Export limited to 10,000 records...', icon: 'info', confirmButtonColor: '#0D5EA6' });
            }
            downloadCsv(data.data);
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'Export failed. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' });
    } finally {
        resumeAutoRefresh();
    }
}
```

## Testing Strategy

### Unit Tests

Unit tests cover specific examples and edge cases:

| Test | What it verifies |
|------|-----------------|
| KPI counts with empty database | Returns 0, 0, 0 |
| KPI counts with mixed levels and timestamps | Correct filtering by level and time window |
| Export with exactly 10,000 records | `IsTruncated = false` |
| Export with 10,001 records | Returns 10,000 items, `IsTruncated = true` |
| Export with 0 matching records | Returns empty list, `IsTruncated = false` |
| CSV escaping: field with comma | Field wrapped in double quotes |
| CSV escaping: field with double quote | Quote doubled and field wrapped |
| CSV escaping: field with newline | Field wrapped in double quotes |
| CSV escaping: null/empty fields | Empty string in output |
| CSV header row | Correct column names in correct order |
| CSV UTF-8 BOM | Output starts with `\xEF\xBB\xBF` |
| Filename pattern | Matches `SystemLogs_YYYY-MM-DD_HHmmss.csv` |
| Copy button renders only for non-null correlation IDs | Button present/absent based on data |
| Auto-refresh timer reset on manual filter | Timer restarts from 0 |
| Auto-refresh cleanup on page unload | Interval cleared |

### Property-Based Tests

Property-based testing is appropriate for this feature because:
- The KPI count logic is a pure aggregation function with clear input/output behavior
- The CSV formatting is a pure data transformation with a wide input space (arbitrary strings with special characters)

**Library**: [FsCheck](https://fscheck.github.io/FsCheck/) for .NET (C# xUnit integration) for server-side properties, or fast-check for client-side CSV generation if tested in JavaScript.

**Configuration**: Minimum 100 iterations per property test.

| Property | Tag | Iterations |
|----------|-----|-----------|
| KPI count correctness | Feature: system-logs-quick-wins, Property 1: KPI count correctness | 100 |
| CSV RFC 4180 formatting | Feature: system-logs-quick-wins, Property 2: CSV RFC 4180 formatting correctness | 100 |

**Property 1 — KPI count correctness**: Generate random lists of `LogEntry` objects with varying `Level` values (Error, Warning, Information, Debug, Fatal, null) and `TimeStamp` values spanning the last 48 hours. Seed an in-memory database, call `GetKpiCountsAsync()`, and assert the returned counts match a simple LINQ filter applied to the same data.

**Property 2 — CSV RFC 4180 formatting**: Generate random `LogEntry` objects with fields containing arbitrary Unicode strings (including commas, double quotes, CR/LF, tabs, empty strings, and null). Run the CSV formatting function, then parse the output back using a standards-compliant CSV parser. Assert the parsed values match the original input values.

### Integration Tests

| Test | What it verifies |
|------|-----------------|
| `GET /Admin/SystemLogs` returns KPI counts in response | End-to-end controller → service → repository |
| `GET /Admin/SystemLogs/KpiCounts` returns JSON with counts | AJAX endpoint works |
| `GET /Admin/SystemLogs/ExportCsv` with filters returns matching records | Export endpoint respects filters |
| `GET /Admin/SystemLogs/ExportCsv` without auth returns 401/403 | SuperAdmin-only access enforced |
