# Implementation Plan: Profit & Loss Summary

## Overview

Implement a period-based Profit & Loss reporting module that computes Revenue, COGS, Operating Expenses, Gross Profit, and Net Profit from existing Payment and Purchase data. The module includes summary cards, expense category breakdown, year-over-year trend comparison, PDF export, and dashboard soft-gate teaser for Starter users. All computation is done in a stateless service layer with no new database tables required.

## Tasks

- [x] 1. Create DTOs and data models
  - [x] 1.1 Create P&L enums, request/response DTOs, and view models
    - Create `Portal.Infrastructure/Models/Pnl/PnlPeriodType.cs` (enum: CurrentMonth, PreviousMonth, CurrentQuarter, CurrentYear, Custom)
    - Create `Portal.Infrastructure/Models/Pnl/PnlPeriodRequest.cs` (PeriodType, CustomStartDate, CustomEndDate)
    - Create `Portal.Infrastructure/Models/Pnl/PnlDateRange.cs` (StartDate, EndDate)
    - Create `Portal.Infrastructure/Models/Pnl/PnlValidationResult.cs` (IsValid, ErrorMessage)
    - Create `Portal.Infrastructure/Models/Pnl/PnlSummaryDto.cs` (Revenue, Cogs, GrossProfit, OperatingExpenses, NetProfit, GrossMargin, NetMargin, Trend, CategoryBreakdown, HasData)
    - Create `Portal.Infrastructure/Models/Pnl/PnlTrendDto.cs` (previous values + percentage changes)
    - Create `Portal.Infrastructure/Models/Pnl/PnlCategoryBreakdownDto.cs` (ExpenseCategoryId, CategoryName, ExpenseTypeName, PurchaseTypeId, PurchaseTypeName, TotalAmount, PercentageOfTotal)
    - Create `Portal.Web/Models/PnlViewModel.cs` (Summary, SelectedPeriod, CustomStartDate, CustomEndDate, CurrencySymbol)
    - Create `Portal.Web/Models/PnlPdfModel.cs` (BusinessName, CurrencySymbol, Summary)
    - _Requirements: 1.1–1.7, 2.1–2.5, 3.2, 4.1, 5.2, 9.1–9.4_

- [x] 2. Implement core P&L service
  - [x] 2.1 Create IPnlService interface and PnlService implementation
    - Create `Portal.Infrastructure/Services/IPnlService.cs` with methods: GetSummaryAsync, ResolvePeriod, ValidateCustomRange
    - Create `Portal.Infrastructure/Services/PnlService.cs` implementing:
      - Period resolution logic (CurrentMonth, PreviousMonth, CurrentQuarter, CurrentYear, Custom)
      - Custom date range validation (start <= end)
      - Revenue computation: sum non-voided Payment.Amount within period for current tenant
      - COGS computation: sum non-cancelled Purchase.TotalAmount where PurchaseTypeId == 2 within period
      - Operating Expenses computation: sum non-cancelled Purchase.TotalAmount where PurchaseTypeId == 3 within period
      - Derived figures: GrossProfit = Revenue - COGS, NetProfit = GrossProfit - OperatingExpenses
      - Margin calculations with zero-revenue protection (return 0 when Revenue == 0)
      - Expense category breakdown: group by ExpenseCategory, calculate percentages, order descending
      - Trend comparison: shift period back 12 months, compute percentage change with null when previous == 0
      - HasData flag: true when any Revenue, COGS, or OpEx is non-zero
    - Inject ICurrentTenantService and PortalDbContext
    - _Requirements: 1.1–1.7, 2.1–2.5, 4.1–4.4, 8.1–8.3, 9.1–9.4_

  - [x] 2.2 Write property test: Revenue computation (Property 1)
    - **Property 1: Revenue computation includes only valid payments for the current tenant and period**
    - Generate random Payment lists with varying IsVoided, PaymentDateUtc, BusinessId, Amount
    - Assert computed Revenue equals manual sum of qualifying payments
    - **Validates: Requirements 1.1, 8.1**

  - [x] 2.3 Write property test: Purchase classification (Property 2)
    - **Property 2: Purchase classification separates COGS and Operating Expenses correctly with tenant isolation**
    - Generate random Purchase lists with varying PurchaseTypeId, IsCancelled, InvoiceDate, BusinessId
    - Assert COGS and OpEx match expected sums for PurchaseTypeId 2 and 3 respectively
    - **Validates: Requirements 1.2, 1.3, 8.2**

  - [x] 2.4 Write property test: Arithmetic invariants (Property 3)
    - **Property 3: Derived profit figures maintain arithmetic invariants**
    - Generate random Revenue, COGS, OpEx values
    - Assert GrossProfit == Revenue - COGS AND NetProfit == GrossProfit - OperatingExpenses
    - **Validates: Requirements 1.4, 1.5**

  - [x] 2.5 Write property test: Margin formulas (Property 4)
    - **Property 4: Margin formulas are correctly applied with zero-revenue protection**
    - Generate random Revenue (including zero), COGS, OpEx
    - Assert correct margin calculation; assert both margins == 0 when Revenue == 0
    - **Validates: Requirements 1.6, 1.7**

  - [x] 2.6 Write property test: Period resolution (Property 5)
    - **Property 5: Predefined period resolution produces correct date boundaries**
    - Generate random DateTime values across different months/years including leap years
    - Assert correct first/last day boundaries for each period type
    - **Validates: Requirements 2.1, 2.3**

  - [x] 2.7 Write property test: Custom date validation (Property 6)
    - **Property 6: Custom date range validation accepts valid ranges and rejects invalid ones**
    - Generate random DateOnly pairs
    - Assert validation passes iff start <= end
    - **Validates: Requirements 2.4, 2.5**

  - [x] 2.8 Write property test: Comparison period shift (Property 7)
    - **Property 7: Comparison period is exactly one year earlier than the selected period**
    - Generate random date ranges including leap year boundaries
    - Assert comparison start/end are each shifted back by one year
    - **Validates: Requirements 4.1**

  - [x] 2.9 Write property test: Trend percentage change (Property 8)
    - **Property 8: Trend percentage change formula is correctly applied**
    - Generate random current/previous decimal pairs including zero previous
    - Assert percentage change == ((current - previous) / |previous|) * 100 when previous != 0, null when previous == 0
    - **Validates: Requirements 4.2, 4.4**

  - [x] 2.10 Write property test: Expense breakdown percentages (Property 9)
    - **Property 9: Expense breakdown percentages sum to 100%**
    - Generate random lists of positive decimal amounts (1-20 items)
    - Assert sum of PercentageOfTotal values == 100% within ±0.1% tolerance
    - **Validates: Requirements 3.4, 9.2**

  - [x] 2.11 Write property test: Expense breakdown ordering (Property 10)
    - **Property 10: Expense breakdown is ordered by amount descending**
    - Generate random expense breakdown results
    - Assert each item's TotalAmount >= next item's TotalAmount
    - **Validates: Requirements 9.3**

  - [x] 2.12 Write property test: Breakdown completeness (Property 11)
    - **Property 11: Expense breakdown includes category name and expense type classification**
    - Generate random ExpenseCategory/ExpenseType combinations
    - Assert every category with purchases includes CategoryName and ExpenseTypeName
    - **Validates: Requirements 9.4**

- [x] 3. Checkpoint - Core service validation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement ProfitLoss controller
  - [x] 4.1 Create ProfitLossController with Index, AxGetPnlData, and ExportPdf actions
    - Create `Portal.Web/Controllers/ProfitLossController.cs`
    - Decorate with `[Authorize]` and `[ModuleAccess(PortalModules.Pnl)]`
    - Inject IPnlService, IPnlPdfService, ICurrentTenantService, PortalDbContext
    - `Index(string? period, string? startDate, string? endDate)` — parse period, call service, return View with PnlViewModel
    - `AxGetPnlData(string period, string? startDate, string? endDate)` — AJAX endpoint returning JSON with computed data
    - `ExportPdf(string period, string? startDate, string? endDate)` — generate PDF and return FileContentResult
    - Handle validation errors for custom date ranges (JSON for AJAX, TempData redirect for page load)
    - Handle empty state (HasData == false)
    - PDF filename format: `PnL_{BusinessName}_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}.pdf` with spaces replaced by underscores
    - _Requirements: 2.1–2.5, 3.1–3.6, 4.2–4.4, 5.1–5.4, 6.3_

  - [x] 4.2 Write property test: PDF filename format (Property 13)
    - **Property 13: PDF filename follows the specified format**
    - Generate random business names (with spaces, special chars) and date ranges
    - Assert filename matches pattern `PnL_{BusinessName}_{StartDate}_{EndDate}.pdf` with dates as yyyyMMdd and spaces as underscores
    - **Validates: Requirements 5.3**

- [x] 5. Implement PDF service
  - [x] 5.1 Create IPnlPdfService interface and PnlPdfService implementation
    - Create `Portal.Infrastructure/Services/IPnlPdfService.cs` with GenerateAsync method
    - Create `Portal.Web/Services/PnlPdfService.cs` implementing:
      - Accept PnlPdfModel with computed data + business name + currency symbol
      - Render `Views/ProfitLoss/PdfExport.cshtml` via IViewRenderService
      - Embed business logo as base64 (same pattern as InvoicePdfService)
      - Convert HTML to PDF via PuppeteerSharp (A4, portrait, print background)
      - Return PDF byte array
    - Handle PuppeteerSharp timeout with appropriate error response
    - _Requirements: 5.1–5.4_

  - [x] 5.2 Write property test: PDF content completeness (Property 12)
    - **Property 12: PDF rendered content contains all required fields**
    - Generate random PnlPdfModel instances
    - Assert rendered HTML contains business name, period dates, all financial figures, and at least one category row
    - **Validates: Requirements 5.2**

- [x] 6. Checkpoint - Controller and PDF service validation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Create views
  - [x] 7.1 Create P&L Index view with period selector, summary cards, breakdown table, and trend display
    - Create `Portal.Web/Views/ProfitLoss/Index.cshtml`
    - Include period selector (Current Month, Previous Month, Current Quarter, Current Year, Custom date range)
    - Summary cards: Revenue, COGS, Gross Profit (with Gross Margin %), Operating Expenses, Net Profit (with Net Margin %)
    - Trend comparison: percentage change badges with up/down indicators; "No comparison data" message when null
    - Expense category breakdown table: category name, expense type, amount, percentage — grouped by PurchaseType (COGS / OpEx sections)
    - Empty state message when HasData == false
    - Export PDF button
    - Custom date range picker with validation feedback
    - Follow mockup at `.kiro/docs/mockups/pnl-summary.html`
    - Apply MyChair Design System: glass cards, Manrope headings, Inter body, standard colours
    - _Requirements: 3.1–3.6, 4.2–4.4_

  - [x] 7.2 Create _PnlContent partial view for AJAX reload
    - Create `Portal.Web/Views/ProfitLoss/_PnlContent.cshtml`
    - Contains summary cards + breakdown table + trend display (no layout, no period selector)
    - Rendered as partial for AJAX period switching
    - _Requirements: 3.2–3.6, 4.2–4.4_

  - [x] 7.3 Create PdfExport view for PDF rendering
    - Create `Portal.Web/Views/ProfitLoss/PdfExport.cshtml`
    - Self-contained HTML with inline styles (no external CSS/JS dependencies)
    - Business logo (base64), business name, period dates
    - Full P&L statement: Revenue, COGS, Gross Profit, Gross Margin, Operating Expenses, Net Profit, Net Margin
    - Expense category breakdown table
    - Trend comparison section (when available)
    - A4 portrait layout optimised for PuppeteerSharp rendering
    - _Requirements: 5.2, 5.4_

  - [x] 7.4 Add AJAX period-switching JavaScript to Index view
    - Implement period change via AJAX with BlockUI overlay
    - Call `/ProfitLoss/AxGetPnlData` on period selector change
    - Re-render `_PnlContent` partial on success
    - Show SweetAlert2 error on failure
    - Custom date range: show/hide date inputs, validate before submitting
    - PDF export button: BlockUI → fetch ExportPdf → download blob → unblock
    - _Requirements: 3.1, 5.1_

- [x] 8. Register services and wire navigation
  - [x] 8.1 Register IPnlService and IPnlPdfService in DI container
    - Add `builder.Services.AddScoped<IPnlService, PnlService>()` in Program.cs
    - Add `builder.Services.AddScoped<IPnlPdfService, PnlPdfService>()` in Program.cs
    - _Requirements: 1.1, 5.1_

  - [x] 8.2 Add P&L navigation link to sidebar
    - Add "Profit & Loss" link to the sidebar navigation partial view
    - Link to `/ProfitLoss`
    - Position in Financial section alongside existing items (Revenue, Purchases, VAT)
    - Show/hide based on module access (pnl module key)
    - _Requirements: 6.2, 6.3_

  - [x] 8.3 Add dashboard soft-gate teaser for Starter users
    - Add a locked P&L teaser card to the Dashboard view
    - Show only when business is on Starter plan (no pnl module access)
    - Preview text explaining what the P&L module provides
    - Click navigates to soft-gate upgrade view for pnl module
    - Hide when business is on Professional or Enterprise plan
    - _Requirements: 7.1–7.3_

- [x] 9. Final checkpoint - Full integration validation
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (13 properties total)
- The `[ModuleAccess(PortalModules.Pnl)]` attribute and ModuleControllerMap entry already exist — no changes needed for plan gating infrastructure
- PnlService uses PortalDbContext which has global query filters on BusinessId via ICurrentTenantService
- PDF generation follows the same IViewRenderService + PuppeteerSharp pattern as IInvoicePdfService
- All AJAX calls must use BlockUI + SweetAlert2 per project standards
- No native `alert()` or `confirm()` — use Swal.fire exclusively

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9", "2.10", "2.11", "2.12", "4.1", "5.1"] },
    { "id": 3, "tasks": ["4.2", "5.2", "7.1", "7.3"] },
    { "id": 4, "tasks": ["7.2", "7.4", "8.1"] },
    { "id": 5, "tasks": ["8.2", "8.3"] }
  ]
}
```
