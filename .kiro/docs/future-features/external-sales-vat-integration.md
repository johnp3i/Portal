# External Sales Records — VAT Integration (Parked)

**Decision Date:** 18 July 2026
**Status:** Parked — no immediate demand
**Module:** Revenue / External Sales
**Table:** `[revenue].[ExternalSalesRecord]`

## Current State

External Sales Records can be imported from CSV, viewed, filtered, cancelled, and restored. They are stored in `[revenue].[ExternalSalesRecord]` with full audit trail.

**What works today:**
- CSV import with validation and duplicate detection
- List page with filtering, pagination, cancel/restore
- Revenue Source assignment (optional)
- Cross-source duplicate warning
- Tier-gated to Professional plan

**What is NOT wired up:**
- No VAT period assignment during import or after
- No contribution to Output VAT calculations
- No section on the VAT Detail or Period Report pages
- No Revenue Dashboard aggregation
- No CSV export from VAT context

## Why It's Parked

1. No client has requested VAT integration for imported sales records yet
2. The primary use cases so far are: onboarding migration (historical data) and record-keeping
3. Premature VAT integration risks users accidentally including historical data in active filings
4. The Z-Report feature already covers the hospitality/POS VAT reporting need

## What's Ready for Future Activation

The infrastructure is 90% complete:

- `ExternalSalesRecord.VatSubmissionPeriodId` column exists (nullable FK)
- Global query filter for tenant isolation is in place
- The VAT integration pattern is proven (Z-Reports already does it)
- The entity is registered in the DbContext with full EF configuration

## When to Activate

Activate this feature when a client needs one of these scenarios:

1. **Online platform sales** — A business sells via an external platform (e.g., Shopify, WooCommerce) and needs those sales included in their VAT filing
2. **Multi-system consolidation** — A business uses Portal for some invoicing but has another system generating sales that need VAT reporting
3. **POS transaction-level VAT** — A business prefers importing individual transactions instead of Z-Report summaries for more granular VAT control

## Implementation Plan (When Needed)

Estimated effort: 1-2 days (the pattern exists from Z-Reports)

### Step 1: Period Assignment UI
- Add "VAT Period" dropdown to the Sales Records list (bulk assign)
- Or add period selection at import time
- Same pattern as Z-Report Entry form

### Step 2: VAT Calculation Integration
Add to `VatSubmissionService.CreateOrRecalculateAsync`:
```csharp
// External Sales: sum of ExternalSalesRecord.VatAmount assigned to this period
if (businessProfile?.IsZReportEnabled == true)
{
    var externalSalesVat = await _portalDbContext.ExternalSalesRecords
        .Where(r => r.BusinessId == businessId
            && r.IsActive
            && r.VatSubmissionPeriodId == vatSubmissionPeriodId)
        .SumAsync(r => (decimal?)r.VatAmount) ?? 0m;
    zReportOutputVat += externalSalesVat; // or separate variable
}
```

### Step 3: VAT Detail Page Section
Add "External Sales" section between Z-Reports and Purchases (same pattern as the Z-Reports section in `Detail.cshtml`).

### Step 4: VAT Period Report Section
Add table showing individual sales records assigned to the period.

### Step 5: Revenue Dashboard
Include `ExternalSalesRecord.TotalAmount` in monthly revenue chart (same as Z-Report TotalGross integration in `DashboardService`).

## Design Decisions to Make Later

- Should external sales be separate from Z-Reports in the VAT report, or combined into one "External Revenue" section?
- Should there be a toggle per import batch: "Include in VAT calculations" vs "Record-keeping only"?
- Should the feature use a separate toggle from `IsZReportEnabled`, or share it?
- Export format: should external sales be exportable as formal invoices (PDF) for audit?

## Related Documents

- Revenue Ingestion Brief: `.kiro/docs/upcoming/Revenue_Ingestion_Brief.md`
- Z-Reports VAT Integration Scenarios: `.kiro/docs/scenarios/revenue-ingestion-vat-integration-testing.md`
- Sales Import Testing Scenarios: `.kiro/docs/scenarios/revenue-ingestion-sales-import-testing.md`
