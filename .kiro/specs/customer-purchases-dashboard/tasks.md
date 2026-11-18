# Implementation Plan: Customer Purchases Dashboard

## Overview

The implementation language is C# (ASP.NET Core MVC 8, EF Core) plus vanilla JavaScript, as set by `design.md`. The three open design decisions (list-price currency fallback, which credit notes count, and the treatment of written-off invoices) were confirmed and are recorded in `design.md` and `requirements.md`, which are the source of truth wherever this file differs. The order is: unblock the test project, database scripts, entities and DTOs, pure calculation logic, repositories, service, controller and export, DI wiring, client UI, Customers list button, and a final build checkpoint.

All new code follows the workspace rules: full table names with no aliases in SQL, `catch (Exception ex)` in every catch block, `AxGet`/`AxPost` prefixes on Ajax actions, BlockUI then SweetAlert2 on every fetch, and no native `alert`, `confirm` or `prompt`.

Automated tests are intentionally not part of this plan (user decision). Correctness is verified by code review and the manual scenarios in `.kiro/docs/scenarios/customer-purchases-dashboard-testing.md`.

## Tasks

- [x] 1. Unblock the test project
  - [x] 1.1 Unblock the Portal.Tests project so tests can run
    - `Portal.Tests` has 4 pre-existing compile errors unrelated to this feature: `InvoiceController` and `ProposalController` constructor mismatches (missing `creditNoteService` and `logger` arguments) in `InvoiceControllerDownloadPdfTests`, `InvoiceControllerAcceptanceStatusTests`, `ProposalControllerAcceptanceTests` and `ProposalControllerDownloadPdfTests`
    - Update those four test constructions to the current constructor signatures, supplying the missing arguments as Moq mocks
    - Do not change any production controller
    - Confirm `dotnet build Portal.Tests` succeeds
    - Every task that runs tests depends on this task
    - _Requirements: none (test infrastructure)_

- [x] 2. Database migrations
  - [x] 2.1 Create migration `233_CreateInsightSchemaAndFeatureInterest.sql`
    - Create `Portal.Database/Migrations/233_CreateInsightSchemaAndFeatureInterest.sql` exactly as specified in the design (comment block, `USE [Portal]`, `GO`)
    - Idempotently create the `[insight]` schema and `[insight].[FeatureInterest]` (`Id`, `BusinessId` with FK to `[portal].[Business]`, `UserId NVARCHAR(450)`, `FeatureKey NVARCHAR(50)`, `CreatedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE()`) and the unique index `UX_FeatureInterest_Business_User_Feature`
    - _Requirements: 15.9, 15.10_

  - [x] 2.2 Create migration `234_AddCustomerPurchasesIndexes.sql`
    - Create `Portal.Database/Migrations/234_AddCustomerPurchasesIndexes.sql` beginning with `USE [Portal]` and `GO`
    - Idempotently add `IX_Invoice_BusinessId_CustomerId_InvoiceDate`, `IX_InvoiceLine_InvoiceId` and `IX_Payment_InvoiceId` with the INCLUDE columns from the design
    - _Requirements: 20.4, 20.5_

- [x] 3. Entities, models and DTOs
  - [x] 3.1 Add the `FeatureInterest` entity and EF mapping
    - Create `Portal.Infrastructure/Entities/Insight/FeatureInterest.cs`
    - In `Data/PortalDbContext.cs` add `DbSet<FeatureInterest> FeatureInterests`, `ConfigureFeatureInterest` (table `insight.FeatureInterest`, `HasDefaultValueSql("GETUTCDATE()")`, unique index, FK to Business), call it from `OnModelCreating`, and add the `HasQueryFilter` entry in `ApplyGlobalQueryFilters`
    - _Requirements: 15.9, 15.10, 2.4_

  - [x] 3.2 Create query, row and DTO models
    - Create under `Portal.Infrastructure/Models/CustomerPurchases/`: `PurchasesQuery.cs` (record, `Default`, `Parse` returning `ServiceResult<PurchasesQuery>` with the allow-list, strict `yyyy-MM-dd`, `from <= to` and `^[A-Z]{3}$` rules, plus a never-throwing `NormalizeCurrency`), `PurchasesRows.cs`, `PurchasesDashboardDto.cs` with all nested DTOs, `ProductPurchaseLinesDto`, and `PurchasesFeatureKeys.cs` (`discounts`, `price-drift`)
    - `InvoiceFinancialRow` carries exactly one status-related field, `bool IsWrittenOff`, and never `InvoiceFinancialStatusTypeId` or any other status id, so the calculator cannot read a persisted overdue status
    - `ProductLineRow` has no `ProductTypeId`, price tier id or price tier name fields
    - Add a `PurchasesJson.Options` helper using `JsonSerializerDefaults.Web`
    - _Requirements: 4.4, 4.11, 4.13, 8.4, 11.12, 15.7_

  - [x] 3.3 Create `KpiCollapseCookie` and the page view models
    - Create `Portal.Web/Models/KpiCollapseCookie.cs` with `Name`, `ValidIds` and a never-throwing `Parse` (case-sensitive, unknown and empty entries dropped, percent-decoding guarded)
    - Create `CustomerPurchasesPageViewModel.cs` and `RegisterFeatureInterestRequest.cs`
    - _Requirements: 9.9, 9.10, 9.11, 9.12_

- [x] 4. Core calculator and invoice-derived figures
  - [x] 4.1 Implement the core of `CustomerPurchasesCalculator`
    - Create `Portal.Infrastructure/Services/CustomerPurchases/CustomerPurchasesCalculator.cs` (static partial class)
    - Implement `Outstanding`, `IsPending` (not written off and balance > 0), `IsOverdue` (`IsPending` and `DueDate < today`, derived from outstanding balance and due date only), `ResolvePeriod`, `DistinctCurrencies`, `SelectCurrency` and `MedianGapDays` (integer half-up for an even count), with money rounded half away from zero
    - Written-off rows (`IsWrittenOff`) are never pending or overdue; no persisted status is read for the overdue decision
    - _Requirements: 4.6, 4.7, 5.1, 5.2, 5.3, 5.8, 7.4, 8.1, 8.4, 8.7, 14.3, 19.5, 19.7_

  - [x] 4.2 Implement revenue, last-purchase, pending, overdue and trend calculations
    - Create `CustomerPurchasesCalculator.Kpis.cs` and `CustomerPurchasesCalculator.Invoices.cs`
    - Net revenue 12 months and comparison, percentage change and direction, lifetime, average invoice, last purchase (days ago, frequency rule), the Pending and Overdue sets built from `IsPending` and `IsOverdue` over one shared list (written-off rows excluded), `DaysLate`, pending pill text (uses "1 day" when N is 1, "due today" when N is 0), invoice line status (checks "Written off" first, per Requirement 13.4, then Paid, Overdue, Partially paid, Unpaid), and the 12 trend buckets with prior-year values
    - Net revenue is always `TotalAmount - credited`, and only the Selected_Currency is summed
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 7.1, 7.2, 7.3, 7.5, 7.6, 7.7, 7.8, 8.2, 8.3, 8.5, 8.6, 8.7, 8.8, 10.1, 10.2, 10.3, 10.4, 10.7, 10.12, 13.4, 16.1, 16.4, 16.5, 16.6, 16.7, 16.8, 16.9, 19.7, 19.8_

- [x] 5. Product mix calculations
  - [x] 5.1 Implement product grouping, mix, donut, price, margin and drill-down logic
    - Create `CustomerPurchasesCalculator.Products.cs`
    - `GroupKey` (code trimmed and upper-cased, else description, Custom_Item flag), display name from the most recent line, quantity, revenue and share (zero-total guard), ordering, `LastBought`, average price paid, margin, partial flag and coverage, the donut (top 5 plus "Other"), and the drill-down lines (newest first, cap 200, `TotalLineCount`, line status including "Written off" evaluated first)
    - List price and difference use the Catalogue_Currency rule: the list price applies only for code groups when the Selected_Currency equals the business's most-used invoice currency (passed in as the catalogue currency) and the active catalogue price is greater than 0; otherwise the cell is "-"
    - Exclude adjustment lines and ignore product type and price tier
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.9, 11.10, 11.12, 11.14, 11.15, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8, 12.9, 12.10, 13.1, 13.3, 13.4, 13.8, 13.9, 13.10_

- [x] 6. Behaviour calculations and dashboard assembly
  - [x] 6.1 Implement rhythm, payment behaviour and `BuildDashboard` assembly
    - Create `CustomerPurchasesCalculator.Behaviour.cs`
    - Rhythm per group key from all-time distinct purchase dates (at least 2), integer 50% boundary and 7-day boundary for the chips, top 5 by all-time revenue then key, and payment behaviour (average days to pay, average terms, on-time count and percentage, the "no settled invoices" state)
    - The settled condition is `outstanding <= 0` (an overpaid invoice is settled) with at least one valid payment
    - Create `CustomerPurchasesCalculator.Build.cs`
    - Combine every section into `PurchasesDashboardDto`: `HasAnyInvoices`, resolved period, currencies and switch flag, KPIs, trend, mix, donut, behaviour (including "Currently overdue" from the Overdue KPI and credit notes from the credit summary), pending list and `PendingTotal`
    - Use a catalogue currency inference helper that takes the business primary currency
    - _Requirements: 5.4, 5.5, 5.7, 8.9, 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8, 14.9, 14.10, 14.11, 14.12, 14.13, 14.14, 18.1, 18.2, 19.7, 19.8_

- [x] 7. Checkpoint - Calculator builds
  - Build `Portal.Infrastructure` only (for example `dotnet build Portal.Infrastructure`). Do not run any tests.
  - Ask the user if questions arise.

- [x] 8. Repositories
  - [x] 8.1 Implement `CustomerPurchasesRepository`
    - Create `Portal.Infrastructure/Repositories/CustomerPurchasesRepository.cs` extending `GenericStoredProcedureRepository` style, using `GetDbConnection()` with the transaction enlistment pattern from `CreditNoteRepository.GetTotalAppliedCreditAsync`
    - Methods: `GetInvoiceFinancialsAsync`, `GetProductLinesAsync`, `GetCreditNoteSummaryAsync`, `GetCatalogPricesAsync`, `GetBusinessPrimaryCurrencyAsync`, with the SQL from the design
    - SQL 1 returns `[IsWrittenOff]` computed as `CASE WHEN [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = 5 THEN 1 ELSE 0 END` and returns no other status column
    - SQL 3 filters credit notes to Issued and Applied with `[credit].[CreditNote].[CreditNoteStatusTypeId] IN (@StatusIssued, @StatusApplied)` (values 2 and 3), so Draft and Voided are excluded
    - Parameterised with `SqlParameter` and `?? (object)DBNull.Value`, full table names with no aliases, every query filtered by `@BusinessId`, no quotation table referenced, `catch (Exception ex) { throw; }`
    - Expose each SQL text as an `internal const string` so tests can inspect it. Check whether `Portal.Infrastructure` already exposes internals to `Portal.Tests` (an `InternalsVisibleTo("Portal.Tests")` in the csproj or an `AssemblyInfo.cs`); if none exists, add `<InternalsVisibleTo Include="Portal.Tests" />` to `Portal.Infrastructure.csproj`, or as a last resort make the constants `public`
    - _Requirements: 2.4, 2.5, 8.1, 8.7, 11.7, 14.13, 18.7, 19.2, 19.3, 19.4, 19.5, 19.6, 20.2, 20.3_

  - [x] 8.2 Implement `FeatureInterestRepository`
    - Create `Portal.Infrastructure/Repositories/Insight/FeatureInterestRepository.cs`
    - `GetFeatureKeysAsync(businessId, userId)` and `InsertIfNotExistsAsync(businessId, userId, featureKey)` using the atomic `UPDLOCK, HOLDLOCK` insert from the design; rethrow with `throw;`; expose the SQL text as `internal const string`
    - _Requirements: 15.5, 15.6, 15.9, 18.7_

- [x] 9. Service
  - [x] 9.1 Implement `ICustomerPurchasesService` and `CustomerPurchasesService`
    - Create `Services/ICustomerPurchasesService.cs` and `Services/CustomerPurchasesService.cs` in `Portal.Infrastructure`
    - `GetDashboardAsync` (tenant check returns null before any query for `BusinessId <= 0` or an unknown customer, `today` from `TimeProvider`, at most five set-based reads, no reads beyond the invoice read for a customer with no invoices, an unknown requested currency treated as auto), `GetProductPurchaseLinesAsync`, `GetRegisteredFeatureKeysAsync`, `RegisterFeatureInterestAsync` (exact allow-list, length 1 to 50, idempotent)
    - The service holds no `DbContext`, connection or SQL
    - _Requirements: 2.4, 2.5, 2.6, 2.7, 4.6, 4.11, 15.5, 15.6, 15.7, 19.1, 19.2, 20.1, 20.3_

- [x] 10. Controller and export
  - [x] 10.1 Implement the Excel export service, `CustomerPurchasesController` and DI registration in `Program.cs`
    - Create `Portal.Web/Services/ICustomerPurchasesExcelService.cs` and `CustomerPurchasesExcelService.cs` following `InvoiceExcelService` (ClosedXML, `catch (Exception ex)` logs and rethrows)
    - "Product mix" sheet with the 10 columns of Requirement 17.3 in this order: Product, Code, Qty, Revenue, Share, Avg price paid, List price, Difference %, Last bought, Margin; money, price and percentage values are numeric cells (money and prices with a 2-decimal format, percentages numeric), and "-" cases are empty cells
    - "Pending invoices" sheet with the same rows, columns and order as the on-screen table plus the pending total row
    - Customer name, period, currency and UTC generation time above each table
    - Call `ICustomerPurchasesService.GetDashboardAsync` so the tenant check applies, returning null when not found
    - Add a static `ExportFileName.Build(customerName, utcDate)` producing `Purchases_{name}_{yyyyMMdd}.xlsx`, stripping invalid file name characters and using `Customer` when empty
    - Create `Portal.Web/Controllers/CustomerPurchasesController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Customer)]`
    - Actions: `Index`, `AxGetPurchasesData`, `AxGetProductPurchaseLines`, `AxGetExportPurchases`, `AxPostRegisterFeatureInterest` (`[ValidateAntiForgeryToken]`, user id from `ClaimTypes.NameIdentifier`, 403 when missing)
    - `Index` accepts `period`, `from`, `to` and `currency`: an invalid period falls back to `last12m` with a warning flag, and `currency` goes through the normalising parser (`PurchasesQuery.NormalizeCurrency`), so a malformed or unknown value is silently treated as auto
    - Ajax actions parse with `PurchasesQuery.Parse`, return validation failures as 400 with `kind = "validation"`, catch `Exception ex`, log with `ILogger`, return 500 with a constant message and no exception text
    - Every missing or other-tenant customer case returns the one shared `NotFound()` on the page and on every Ajax action, including `AxGetExportPurchases`; only an exception returns the 500 JSON
    - The page action reads `KpiCollapseCookie`; no repository reference and no monetary arithmetic in the controller
    - In `Program.cs` add scoped registrations for `CustomerPurchasesRepository`, `FeatureInterestRepository`, `ICustomerPurchasesService` (with `TimeProvider.System`) and `ICustomerPurchasesExcelService`, next to the existing Revenue Control and Credit Notes registrations
    - _Requirements: 2.1, 2.2, 2.3, 2.6, 2.7, 2.8, 2.9, 4.11, 4.13, 8.9, 15.3, 15.4, 15.7, 15.8, 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7, 17.8, 17.9, 17.11, 17.12, 18.3, 18.8, 19.1, 19.2, 19.9, 20.1_

- [x] 12. Client UI
  - [x] 12.1 Create the view, CSS and `customer-purchases.js` (filters, KPI cards, chart, donut, mix, drill-down, behaviour, pending table, teasers, export)
    - Create `Views/CustomerPurchases/Index.cshtml`: breadcrumb, topbar with eyebrow, heading, subtitle and the actions "Back to Customers", "Statement" (`/Statement?customerId={id}`) and "Export Excel", filter card (`margin-bottom:22px`), currency segmented control, KPI bar with "Collapse all" and "Expand all", six KPI cards rendered collapsed server-side from `CollapsedKpiIds`, content cards (`margin-top:24px`), teaser cards rendered disabled when already registered, and the single empty-state card when `HasAnyInvoices` is false
    - Add `@Html.AntiForgeryToken()`, the `#purchasesData` JSON island using the default `System.Text.Json` encoder, `Head` and `Scripts` sections, and the period fallback warning flag
    - Follow `.kiro/steering/layout-standards.md`
    - Create `wwwroot/css/customer-purchases.css`: KPI grid breakpoints (3 columns above 900px, 2 from 561 to 900px, 1 at 560px and below), stacked two-column sections at 900px and below, horizontally scrollable table containers, wrapping header actions, long-name wrapping, `.sr-only`, pills with visible text, and MyChair colours and fonts
    - Create `wwwroot/js/customer-purchases.js` core, filters and KPI cards: IIFE with `'use strict'`, vanilla `fetch`, no jQuery; state with `applied` selections changed only after a successful response
    - `request` wrapper: `BlockUI.show`, 30 second `AbortController` timeout, checks `response.status === 404` before parsing JSON (the shared `NotFound()` has an empty body; hide BlockUI and show a `warning` Swal "This customer could not be found.", with the previous data and selections kept), `BlockUI.hide` before any `Swal.fire` on both success and failure paths, SweetAlert2 with `confirmButtonColor: '#0D5EA6'`
    - `safeRender(name, containerId, fn)` helper that runs each render function in its own `try/catch`, logs to `console.error`, clears only that section and writes an in-place error message there, so one failing section never stops the others
    - Period select with custom range validation (inverted range raises a warning and sends nothing), currency switch, `history.replaceState` for period and dates, writing `currency` to the URL only after an explicit user choice (removed again on a period change), revert on failure, `fmtMoney` with fallback, `renderAll` (every section through `safeRender`), `renderKpis`, KPI toggles with `aria-expanded` and `aria-label`, collapse all and expand all, and `cp_kpi_collapsed` cookie writing inside `try/catch` that leaves the cookie and card state unchanged on refresh
    - Use `textContent` for all data; show the period fallback warning
    - Add the SVG trend chart and donut to `customer-purchases.js`: 12 bars with value labels and "N inv" under each month, current month in a different colour with an asterisk, dashed prior-year line with the compare checkbox (no request, under 500 ms), focusable bars with a shared tooltip, nice y-axis maximum, `role="img"`, `aria-label` and a visually hidden data table, horizontal scroll with minimum width
    - `renderChart` validates the trend data (exactly 12 items, finite numbers) and runs through `safeRender`; on invalid data or a drawing error the chart card shows "The chart could not be displayed." in place, no partial bars are drawn, and the other sections still render
    - Donut with the six-colour palette, centre total and a legend with name and share
    - Add mix, drill-down, behaviour, pending table, teasers and export to `customer-purchases.js`: product mix table with the seven columns, the adjustment-lines note, empty states, the margin coverage line and the "partial" label; drill-down toggle buttons with `aria-expanded` that load through `AxGetProductPurchaseLines` once and cache the result, showing the status as one of Unpaid, Partially paid, Paid, Overdue or Written off with its text label, the 200-line notice and failure handling
    - Behaviour card (rhythm chips, payment stats, "No settled invoices yet", credit notes, overdue note), pending table with the footer total and "No pending invoices" state
    - Teaser "Notify me when available" via `AxPostRegisterFeatureInterest` with the antiforgery header (success, warning and error SweetAlert2 outcomes), and Export Excel through `AxGetExportPurchases` with blob download; a 404 (empty body) hides BlockUI and shows the `warning` Swal without parsing JSON, a 500 parses the JSON and shows an `error` Swal
    - All render functions run through `safeRender`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 4.8, 4.9, 4.10, 4.11, 4.12, 4.13, 5.1, 5.2, 5.4, 5.6, 5.9, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9, 9.10, 9.14, 9.15, 10.1, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.10, 10.11, 10.12, 10.13, 11.5, 11.11, 11.13, 11.15, 12.8, 12.10, 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7, 13.8, 13.9, 13.10, 13.11, 14.9, 14.12, 14.13, 14.14, 14.15, 15.1, 15.2, 15.3, 15.4, 15.6, 15.7, 15.8, 16.2, 16.3, 16.9, 16.10, 16.11, 17.1, 17.9, 17.10, 17.12, 18.1, 18.2, 18.4, 18.5, 18.6, 18.8, 20.7, 20.8, 21.1, 21.2, 21.3, 21.4, 21.5, 21.6, 21.7, 21.8, 21.9, 21.10_

- [x] 13. Customers list entry point
  - [x] 13.1 Add the Purchases button to `Views/Customer/Index.cshtml`
    - Insert `<a href="/Customer/Purchases/@customer.Id" class="tbl-action tbl-action--primary">Purchases</a>` immediately after the Statement link and before the Deactivate form, outside the `@if (customer.IsActive)` block, and skip it when the customer id is not valid
    - Leave the existing Deactivate `confirm()` untouched (noted as out of scope in the design)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 15. Final checkpoint - Build the solution
  - Build the production projects (`dotnet build Portal.Infrastructure` and `dotnet build Portal.Web`). Do not build or run `Portal.Tests` and do not run any tests.
  - Ask the user if questions arise.

## Notes

- Each task references specific requirements for traceability
- The three design decisions (list-price currency fallback, credit notes counted, written-off invoices) were confirmed and are recorded in `design.md` and `requirements.md`
- Automated tests are intentionally not part of this plan (user decision); correctness is verified by code review and the manual scenarios in `.kiro/docs/scenarios/customer-purchases-dashboard-testing.md`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["6.1"] },
    { "id": 1, "tasks": ["9.1"] },
    { "id": 2, "tasks": ["10.1"] },
    { "id": 3, "tasks": ["12.1"] }
  ]
}
```