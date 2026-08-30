# Design Document

## Overview

Add two optional date fields to the Purchase entity — `SupplierDueDate` (the supplier's actual deadline) and `TargetPaymentDate` (the business owner's internal target). The system uses an "effective due date" (`TargetPaymentDate ?? SupplierDueDate`) for all dashboard, indicator, and future email logic. This creates a two-tier payment tracking model: the business manages against their own commitment, with the supplier deadline as the hard backstop.

---

## Data Model

### Altered Table: `[purchase].[Purchase]`

Add columns:
```sql
[SupplierDueDate] DATE NULL
[TargetPaymentDate] DATE NULL
```

Nullable — existing purchases remain without payment tracking. No backfill. No FK constraints.

### Entity Change: `Purchase.cs`

Add properties after `InvoiceDate`:
```csharp
public DateOnly? SupplierDueDate { get; set; }
public DateOnly? TargetPaymentDate { get; set; }
```

### Effective Due Date

Not stored — computed at read time:
```csharp
// In service/view logic:
var effectiveDueDate = purchase.TargetPaymentDate ?? purchase.SupplierDueDate;
```

---

## Repository Changes — PurchaseRepository

### SELECT Queries (4 methods)

Add `[SupplierDueDate], [TargetPaymentDate]` to the column list in:
- `GetAllByBusinessIdAsync`
- `GetByIdAndBusinessIdAsync`
- `GetFilteredAsync`
- `GetUnassignedByDateRangeAsync`

### INSERT Query

Add `[SupplierDueDate], [TargetPaymentDate]` to the column list and `@SupplierDueDate, @TargetPaymentDate` to the values in `InsertAsync`. Both null-safe:
```csharp
new SqlParameter("@SupplierDueDate", entity.SupplierDueDate ?? (object)DBNull.Value),
new SqlParameter("@TargetPaymentDate", entity.TargetPaymentDate ?? (object)DBNull.Value)
```

### UPDATE Query

Add to the SET clause in `UpdateAsync`:
```sql
[SupplierDueDate] = @SupplierDueDate,
[TargetPaymentDate] = @TargetPaymentDate
```

### New Query: GetUpcomingDueByBusinessIdAsync

```sql
SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
       [InvoiceNumber], [InvoiceDate], [SupplierDueDate], [TargetPaymentDate], [Description],
       [AmountExcludingVat], [VatAmount], [TotalAmount],
       [Country], [Notes], [IsCancelled], [CancelledAtUtc], [CancelledByUserId], [PayslipPeriodId], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc]
FROM [purchase].[Purchase]
WHERE [purchase].[Purchase].[BusinessId] = @BusinessId
  AND [purchase].[Purchase].[IsCancelled] = 0
  AND (
      ([purchase].[Purchase].[TargetPaymentDate] IS NOT NULL AND [purchase].[Purchase].[TargetPaymentDate] <= @CutoffDate)
      OR
      ([purchase].[Purchase].[TargetPaymentDate] IS NULL AND [purchase].[Purchase].[SupplierDueDate] IS NOT NULL AND [purchase].[Purchase].[SupplierDueDate] <= @CutoffDate)
  )
ORDER BY COALESCE([purchase].[Purchase].[TargetPaymentDate], [purchase].[Purchase].[SupplierDueDate]) ASC
```

Where `@CutoffDate` = today + 14 days. Returns all non-cancelled purchases with an effective due date within the window (including overdue from the past). Ordered by effective due date ascending.

---

## Validation

### TargetPaymentDate vs SupplierDueDate

When both dates are provided:
- `TargetPaymentDate` must be on or before `SupplierDueDate`
- If `TargetPaymentDate > SupplierDueDate`, show an inline amber warning below the Target Payment Date field: "Target date is after the supplier deadline."
- Not a hard block — the form still submits. The warning is purely informational.

Implementation: Client-side JS — attach a `change` event listener to both date inputs. When both have values and target > supplier, show/hide an inline `<div>` with the warning styled in amber. No server-side validation needed (the data is valid, just unusual).

---

## Form Changes

### PurchaseFormViewModel

Add:
```csharp
public DateOnly? SupplierDueDate { get; set; }
public DateOnly? TargetPaymentDate { get; set; }
```

### MapFormToEntity

Add:
```csharp
SupplierDueDate = model.SupplierDueDate,
TargetPaymentDate = model.TargetPaymentDate
```

### Edit GET — Controller

Add:
```csharp
model.SupplierDueDate = purchase.SupplierDueDate;
model.TargetPaymentDate = purchase.TargetPaymentDate;
```

### Create/Edit Views

Add after the Invoice Date field, as a two-column row:
```html
<div class="field">
    <label asp-for="SupplierDueDate">Supplier Due Date</label>
    <input asp-for="SupplierDueDate" type="date" />
    <small class="muted">The actual payment deadline from the supplier's invoice.</small>
</div>
<div class="field">
    <label asp-for="TargetPaymentDate">Target Payment Date</label>
    <input asp-for="TargetPaymentDate" type="date" />
    <small class="muted">When you want to pay — can be earlier than the supplier deadline.</small>
</div>
```

Both optional. No `required` attribute.

---

## Purchase List — Due Column with Indicators

### Table Column

Add `<th>Due</th>` after the existing Date column. Each row computes effective due date and renders:

| Condition | Display |
|---|---|
| Both dates NULL | `—` |
| Effective date < today, not cancelled | Red date text + `Overdue` red pill |
| Effective date == today, not cancelled | `Today` in amber |
| Effective date within 7 days | Amber date text |
| Effective date > 7 days | Normal date text |
| Only SupplierDueDate set (no target) | Date + `(supplier)` muted label |
| TargetPaymentDate set | Date shown as primary |

---

## Dashboard Widget — Upcoming Supplier Payments

### DTO

```csharp
public class UpcomingSupplierPaymentDto
{
    public int PurchaseId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly EffectiveDueDate { get; set; }
    public DateOnly? SupplierDueDate { get; set; }
    public DateOnly? TargetPaymentDate { get; set; }
    public string Status { get; set; } = null!; // "overdue", "today", "due_soon", "upcoming"
}
```

### DashboardViewModel

Add:
```csharp
public List<UpcomingSupplierPaymentDto> UpcomingSupplierPayments { get; set; } = new();
```

### HomeController

In the `if (scope.ShowPurchase)` block:
1. Call `GetUpcomingDueByBusinessIdAsync(businessId, today.AddDays(14))`
2. Collect distinct `SupplierId` values from the results
3. Batch-load supplier names: query `_dbContext.Suppliers.Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToDictionaryAsync(s => s.Id, s => s.Name)`
4. Map to `UpcomingSupplierPaymentDto`, resolving `SupplierName` from the dictionary
5. Compute effective due date and status per entry
6. Take top 5
7. Populate `model.UpcomingSupplierPayments`

### Dashboard View

New section in purchase-scoped area:

```html
<section class="glass card-pad" style="margin-top:22px;">
    <div style="display:flex;justify-content:space-between;align-items:center;">
        <div>
            <div class="label">Payables</div>
            <h3>Upcoming Supplier Payments</h3>
        </div>
        <a href="/Purchase" class="btn btn-secondary" style="font-size:13px;">View All</a>
    </div>
    <!-- Each entry: Supplier, Description, Amount, Due Date (effective), Status pill -->
    <!-- If TargetPaymentDate differs from SupplierDueDate, show "(supplier: DD MMM)" below -->
    <!-- Empty state: "No upcoming supplier payments." -->
</section>
```

---

## CSV Export

Add two columns after "Invoice Date":
- "Supplier Due Date" — `yyyy-MM-dd` when present, empty when null
- "Target Payment Date" — `yyyy-MM-dd` when present, empty when null

## PDF Export

In `_ExportPdf.cshtml`, add two columns after the existing "Date" column:
- "Supplier Due" — `dd MMM yyyy` when present, empty when null
- "Target Date" — `dd MMM yyyy` when present, empty when null

## Known Limitations

- **Bulk Entry:** The `BulkEntry` view and `BulkPurchaseRowDto` do NOT include the new date fields. Purchases created via bulk entry will have both dates as NULL.
- **CSV Import:** The `CsvImport` flow and `CsvPurchaseRowDto` do NOT parse the new date fields. Imported purchases will have both dates as NULL.
- Both can be enhanced in a later phase if users request deadline tracking for bulk-entered purchases.

---

## Future Integration Note: Weekly Financial Snapshot Email

This feature establishes the data model for Proposal #7's escalation pattern:

```
Target Payment Date passes → "Missed Payment Target" in weekly email
  ↓ repeats weekly
Supplier Due Date approaches (within 3 days) → escalate to "Critical — Supplier Deadline"
  ↓
Supplier Due Date passes → "OVERDUE — Service at Risk"
```

The two-date model enables this progressive urgency without requiring new fields later. The weekly email feature (when built) reads `TargetPaymentDate` and `SupplierDueDate` directly from the Purchase entity.

---

## Files Summary

| File | Change |
|------|--------|
| `Portal.Database/Migrations/181_AddPaymentDatesToPurchase.sql` | New migration (2 columns) |
| `Portal.Infrastructure/Entities/Purchase.cs` | Add 2 properties |
| `Portal.Infrastructure/Repositories/PurchaseRepository.cs` | 4 SELECTs + INSERT + UPDATE + new query |
| `Portal.Web/Models/PurchaseFormViewModel.cs` | Add 2 properties |
| `Portal.Web/Controllers/PurchaseController.cs` | MapFormToEntity + Edit GET + CSV export |
| `Portal.Web/Views/Purchase/Create.cshtml` | Add 2 date fields + inline validation warning |
| `Portal.Web/Views/Purchase/Edit.cshtml` | Add 2 date fields + inline validation warning |
| `Portal.Web/Views/Purchase/Index.cshtml` | Add Due column with indicators |
| `Portal.Web/Views/Purchase/_ExportPdf.cshtml` | Add 2 date columns to PDF export |
| `Portal.Infrastructure/Models/DashboardViewModel.cs` | Add DTO + list property |
| `Portal.Web/Controllers/HomeController.cs` | Populate widget data |
| `Portal.Web/Views/Home/Index.cshtml` | Render widget section |
