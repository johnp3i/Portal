# Design Document: Reminder History Page

## Architecture Overview

The Reminder History page follows the established MVC + Service layer pattern:

```
History.cshtml → (fetch) → PaymentReminderController.AxGetAllReminderHistory
                                    ↓
                          IPaymentReminderService.GetAllReminderHistoryAsync(...)
                                    ↓
                          EF Core LINQ query on PaymentReminderLog + joins
                                    ↓
                          JSON response { success, data, totalCount, page, pageSize }
```

The page renders server-side as a Razor view with an empty table shell, then immediately fetches data client-side on `DOMContentLoaded`. All subsequent filter/page interactions use the same AJAX endpoint.

---

## Components

### 1. Controller Action — `History()` (Page)

A new `[HttpGet]` action on `PaymentReminderController` that returns the `History` view. No model is required since data is loaded via AJAX.

```csharp
[HttpGet]
public IActionResult History()
{
    return View();
}
```

This action inherits the class-level `[Authorize]` and `[ModuleAccess(PortalModules.PaymentReminderManual)]` attributes.

### 2. Controller Action — `AxGetAllReminderHistory()` (AJAX Endpoint)

A new `[HttpGet]` action returning paginated, filtered reminder history as JSON.

```csharp
[HttpGet]
public async Task<IActionResult> AxGetAllReminderHistory(
    string? tier = null,
    string? status = null,
    string? method = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string? customer = null,
    int page = 1,
    int pageSize = 20)
{
    try
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var result = await _reminderService.GetAllReminderHistoryAsync(
            businessId, tier, status, method, dateFrom, dateTo, customer, page, pageSize);

        return Json(new
        {
            success = true,
            data = result.Items,
            totalCount = result.TotalCount,
            page,
            pageSize
        });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Failed to load reminder history." });
    }
}
```

### 3. Service Interface Extension

Add a new method to `IPaymentReminderService`:

```csharp
/// <summary>
/// Gets paginated reminder history for the business with optional filters.
/// Returns a page of results and the total matching count for pagination metadata.
/// </summary>
Task<ReminderHistoryPageResult> GetAllReminderHistoryAsync(
    int businessId,
    string? tier = null,
    string? status = null,
    string? method = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string? customer = null,
    int page = 1,
    int pageSize = 20);
```

### 4. Service Implementation

The service method builds a LINQ query against the `PaymentReminderLog` DbSet with joins to `Customer` and `Invoice`, applies filters conditionally, counts the total, then applies Skip/Take for the requested page.

```csharp
public async Task<ReminderHistoryPageResult> GetAllReminderHistoryAsync(
    int businessId,
    string? tier = null,
    string? status = null,
    string? method = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string? customer = null,
    int page = 1,
    int pageSize = 20)
{
    try
    {
        var query = _context.PaymentReminderLogs
            .Where(log => log.BusinessId == businessId)
            .Join(_context.Customers,
                log => log.CustomerId,
                cust => cust.Id,
                (log, cust) => new { log, cust })
            .Join(_context.Invoices,
                lc => lc.log.InvoiceId,
                inv => inv.Id,
                (lc, inv) => new { lc.log, lc.cust, inv });

        // Apply filters
        if (!string.IsNullOrEmpty(tier) && tier != "All")
            query = query.Where(x => x.log.EscalationTier == tier);

        if (!string.IsNullOrEmpty(status))
        {
            if (status == "Sent")
                query = query.Where(x => x.log.IsSentSuccessfully == true);
            else if (status == "Failed")
                query = query.Where(x => x.log.IsSentSuccessfully == false);
        }

        if (!string.IsNullOrEmpty(method))
        {
            if (method == "Auto")
                query = query.Where(x => x.log.IsManualTrigger == false && x.log.IsTestSend == false);
            else if (method == "Manual")
                query = query.Where(x => x.log.IsManualTrigger == true && x.log.IsTestSend == false);
            else if (method == "Test")
                query = query.Where(x => x.log.IsTestSend == true);
        }

        if (dateFrom.HasValue)
            query = query.Where(x => x.log.SentAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
        {
            var endOfDay = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.log.SentAtUtc < endOfDay);
        }

        if (!string.IsNullOrEmpty(customer))
            query = query.Where(x => x.cust.Name.Contains(customer));

        // Order by most recent first
        var ordered = query.OrderByDescending(x => x.log.SentAtUtc);

        // Count total matching
        var totalCount = await ordered.CountAsync();

        // Page slice
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReminderHistoryItemDto
            {
                Id = x.log.Id,
                SentAtUtc = x.log.SentAtUtc,
                InvoiceId = x.log.InvoiceId,
                InvoiceNumber = x.inv.InvoiceNumber,
                CustomerName = x.cust.Name,
                EscalationTier = x.log.EscalationTier,
                RecipientEmail = x.log.RecipientEmail,
                IsManualTrigger = x.log.IsManualTrigger,
                IsTestSend = x.log.IsTestSend,
                IsSentSuccessfully = x.log.IsSentSuccessfully,
                IsOpened = x.log.IsOpened
            })
            .ToListAsync();

        return new ReminderHistoryPageResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }
    catch (Exception ex)
    {
        throw;
    }
}
```

### 5. DTOs

```csharp
namespace Portal.Infrastructure.Models.PaymentReminders;

public class ReminderHistoryPageResult
{
    public List<ReminderHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public class ReminderHistoryItemDto
{
    public int Id { get; set; }
    public DateTime SentAtUtc { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string EscalationTier { get; set; } = null!;
    public string RecipientEmail { get; set; } = null!;
    public bool IsManualTrigger { get; set; }
    public bool IsTestSend { get; set; }
    public bool IsSentSuccessfully { get; set; }
    public bool IsOpened { get; set; }
}
```

---

## View — `History.cshtml`

The view follows the exact same pattern as `Upcoming.cshtml`:

1. **Topbar** — eyebrow "Payment Reminders", heading "Reminder History", subtitle
2. **Filter Card** — `.glass.card-pad` with `margin-bottom:22px`, flex layout with 14px gap
3. **Data Table Card** — `.glass.card-pad` with table, empty state, and pagination

### Filter Controls Layout

```html
<div style="display:flex;gap:14px;align-items:flex-end;flex-wrap:wrap;">
    <div class="field" style="min-width:180px;">Tier (select)</div>
    <div class="field" style="min-width:180px;">Status (select)</div>
    <div class="field" style="min-width:180px;">Method (select)</div>
    <div class="field" style="min-width:160px;">Date From (input type=date)</div>
    <div class="field" style="min-width:160px;">Date To (input type=date)</div>
    <div class="field" style="min-width:200px;">Customer (text input)</div>
    <div style="padding-bottom:2px;">
        <button class="btn btn-primary">Filter</button>
        <button class="btn btn-secondary">Clear</button>
    </div>
</div>
```

### Table Columns

| Column | Source Field | Rendering |
|--------|-------------|-----------|
| Date | `SentAtUtc` | `dd MMM yyyy` formatted |
| Invoice Number | `InvoiceNumber` + `InvoiceId` | Hyperlink to `/Invoice/Detail/{InvoiceId}` |
| Customer Name | `CustomerName` | Plain text (escaped) |
| Tier | `EscalationTier` | Coloured badge (Friendly=green, Firm=amber, Formal=red) |
| Recipient | `RecipientEmail` | Plain text (escaped) |
| Method | `IsManualTrigger` + `IsTestSend` | Badge: Auto/Manual/Test |
| Status | `IsSentSuccessfully` | Badge: Sent (green) / Failed (red) |
| Opened | `IsOpened` + `IsSentSuccessfully` | Badge: Opened (green) / Not opened (muted) / — |

### Method Badge Logic

| IsTestSend | IsManualTrigger | Badge |
|-----------|----------------|-------|
| true | (any) | "Test" |
| false | true | "Manual" |
| false | false | "Auto" |

### Opened Badge Logic

| IsSentSuccessfully | IsOpened | Badge |
|-------------------|---------|-------|
| false | (any) | "—" (dash) |
| true | false | "Not opened" (muted) |
| true | true | "Opened" (green) |

---

## Data Flow

### Initial Page Load

```
DOMContentLoaded → loadHistory(page=1) → BlockUI.show() → fetch(AxGetAllReminderHistory)
                                            → BlockUI.hide() → renderTable() + renderPagination()
```

### Filter Action

```
Click "Filter" → loadHistory(page=1, filters) → same flow as above
```

### Page Navigation

```
Click page button → loadHistory(page=N, filters) → same flow as above
```

### Clear Action

```
Click "Clear" → reset all filter DOM values to defaults → loadHistory(page=1)
```

---

## Client-Side JavaScript Functions

| Function | Purpose |
|----------|---------|
| `loadHistory(page)` | Builds URLSearchParams from filter values, calls BlockUI.show(), fetches endpoint, handles response |
| `renderTable(data)` | Clears tbody, builds rows from data array using badge helpers |
| `renderPagination(totalCount, page, pageSize)` | Computes total pages, renders info text and page buttons |
| `tierBadge(tier)` | Returns HTML badge span with tier-specific colors |
| `methodBadge(isManualTrigger, isTestSend)` | Returns HTML badge span for delivery method |
| `statusBadge(isSentSuccessfully)` | Returns HTML badge for Sent/Failed |
| `openedBadge(isOpened, isSentSuccessfully)` | Returns HTML badge for open status |
| `formatDate(dateStr)` | Formats ISO date string to `dd MMM yyyy` |
| `escapeHtml(str)` | XSS-safe text escaping |
| `clearFilters()` | Resets all filter controls and calls `loadHistory(1)` |

---

## Sidebar Navigation

Add a new `nav-sub-item` under Payment Reminders in the shared layout, guarded by `hasPaymentReminderAccess`:

```html
@if (hasPaymentReminderAccess)
{
    <a class="nav-sub-item" href="/PaymentReminder/History">Reminder History</a>
}
```

---

## Error Handling

| Layer | Error Strategy |
|-------|---------------|
| Controller | try/catch → return `{ success: false, message: "Failed to load reminder history." }` |
| Service | try/catch → rethrow (let controller handle) |
| Client JS | catch → `BlockUI.hide()` → `Swal.fire({ icon: 'error', ... })` |

---

## Performance Considerations

- **Indexes used**: `IX_PaymentReminderLog_BusinessId_SentAtUtc` supports the BusinessId filter + SentAtUtc ordering
- **Pagination**: Server-side Skip/Take prevents loading all records into memory
- **Count query**: `CountAsync()` runs before the page slice to provide total count
- **Default page size**: 20 records — balances UX responsiveness with data volume

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Tenant Isolation

*For any* set of filter parameters, page number, and page size, all records returned by `GetAllReminderHistoryAsync` SHALL have a `BusinessId` equal to the provided `businessId` parameter. No records belonging to other businesses shall appear in the results.

**Validates: Requirements 7.3, 8.3**

### Property 2: Tier Filter Correctness

*For any* non-null tier filter value that is not "All", all records in the returned page SHALL have `EscalationTier` equal to the provided tier value. When the tier filter is null or "All", records of any tier may appear.

**Validates: Requirements 8.4**

### Property 3: Status Filter Correctness

*For any* status filter value ("Sent" or "Failed"), all records in the returned page SHALL satisfy the corresponding `IsSentSuccessfully` condition: true when status is "Sent", false when status is "Failed". When no status filter is applied, records with either value may appear.

**Validates: Requirements 8.5, 8.6**

### Property 4: Method Filter Correctness

*For any* method filter value ("Auto", "Manual", or "Test"), all records in the returned page SHALL satisfy the corresponding boolean conditions: Auto requires `IsManualTrigger=false AND IsTestSend=false`, Manual requires `IsManualTrigger=true AND IsTestSend=false`, Test requires `IsTestSend=true`. When no method filter is applied, records with any method may appear.

**Validates: Requirements 8.7, 8.8, 8.9**

### Property 5: Date Range Filter Correctness

*For any* provided dateFrom and/or dateTo values, all records in the returned page SHALL have `SentAtUtc >= dateFrom` (when dateFrom is present) and `SentAtUtc < dateTo + 1 day` (when dateTo is present). Records outside the specified range SHALL NOT appear.

**Validates: Requirements 8.10, 8.11**

### Property 6: Customer Text Search Correctness

*For any* non-empty customer search string, all records in the returned page SHALL have a `CustomerName` that contains the search string using case-insensitive comparison. Records whose customer name does not contain the search term SHALL NOT appear.

**Validates: Requirements 8.12**

### Property 7: Descending Chronological Order

*For any* query result containing two or more records, for all consecutive pairs of records (i, i+1) in the returned data array, the `SentAtUtc` of record i SHALL be greater than or equal to the `SentAtUtc` of record i+1.

**Validates: Requirements 8.13**

### Property 8: Pagination Slice Correctness

*For any* totalCount ≥ 0, valid page number (1 ≤ page ≤ totalPages), and pageSize > 0, the number of records returned SHALL equal `min(pageSize, totalCount - (page - 1) * pageSize)`, and the reported `TotalCount` SHALL equal the actual number of records matching all applied filters.

**Validates: Requirements 5.1, 5.2, 5.3, 8.14**
