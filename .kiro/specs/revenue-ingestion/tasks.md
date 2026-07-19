# Implementation Plan: Revenue Ingestion — Phase 1

## Overview

This plan implements the Revenue Ingestion feature following the established Portal architecture (Controller → Service → Repository). Work is structured as: database layer first (migrations + EF Core), then service/repository layer, then controller + views, then integration with existing VAT/Dashboard systems, and finally client-side JS for auto-computation.

## Tasks

- [x] 1. Database migrations and EF Core entity configuration
  - [x] 1.1 Create SQL migration: Add IsZReportEnabled to BusinessProfile
    - Create migration file `Portal.Database/Migrations/XXX_AddIsZReportEnabledToBusinessProfile.sql`
    - `ALTER TABLE [dbo].[BusinessProfile] ADD [IsZReportEnabled] BIT NOT NULL DEFAULT 0;`
    - Add property `public bool IsZReportEnabled { get; set; }` to existing `BusinessProfile` entity
    - Add EF Core configuration: `.IsRequired().HasDefaultValue(false)`
    - _Requirements: 1.1_

  - [x] 1.2 Create SQL migration: RevenueSource table
    - Create migration file `Portal.Database/Migrations/XXX_CreateRevenueSourceTable.sql`
    - Table with columns: Id, BusinessId, Name, Description, IsActive, CreatedAtUtc
    - PK on Id, FK BusinessId → Business(Id)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 1.3 Create SQL migration: RevenueSummary table
    - Create migration file `Portal.Database/Migrations/XXX_CreateRevenueSummaryTable.sql`
    - Table with all columns per design: Id, BusinessId, RevenueSourceId, SummaryDate, PeriodEndDate, ZReportNumber, TotalNet, TotalVat, TotalGross, TotalDiscount, TransactionCount, Reference, Notes, ExportedAtUtc, VatSubmissionPeriodId, ImportSessionId, IsActive, CreatedAtUtc
    - PK on Id, FK BusinessId → Business, RevenueSourceId → RevenueSource, VatSubmissionPeriodId → VatSubmissionPeriod
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 1.4 Create SQL migration: RevenueSummaryLine table
    - Create migration file `Portal.Database/Migrations/XXX_CreateRevenueSummaryLineTable.sql`
    - Table with columns: Id, RevenueSummaryId, VatRate, NetAmount, VatAmount, TotalAmount, DiscountAmount, Description, CreatedAtUtc
    - PK on Id, FK RevenueSummaryId → RevenueSummary(Id)
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 1.5 Create EF Core entity classes and DbContext configuration
    - Create `Portal.Infrastructure/Entities/RevenueSource.cs`
    - Create `Portal.Infrastructure/Entities/RevenueSummary.cs`
    - Create `Portal.Infrastructure/Entities/RevenueSummaryLine.cs`
    - Add `DbSet<RevenueSource>`, `DbSet<RevenueSummary>`, `DbSet<RevenueSummaryLine>` to PortalDbContext
    - Add all entity configurations in `OnModelCreating` per design (decimal precision, FK relationships, defaults)
    - _Requirements: 2.1, 4.1, 5.1_

- [ ] 2. Repository layer
  - [x] 2.1 Create RevenueSourceRepository
    - Create `Portal.Infrastructure/Repositories/RevenueSourceRepository.cs`
    - Extend `GenericStoredProcedureRepository<RevenueSource>`
    - Methods: `GetAllByBusinessIdAsync`, `GetActiveByBusinessIdAsync`, `GetByIdAndBusinessIdAsync`, `InsertAsync`, `UpdateAsync`, `SetIsActiveAsync`, `HasSummariesAsync`
    - All queries use full table names, filter by BusinessId, use SqlParameter with null-safety
    - _Requirements: 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 16.1_

  - [~] 2.2 Create RevenueSummaryRepository
    - Create `Portal.Infrastructure/Repositories/RevenueSummaryRepository.cs`
    - Extend `GenericStoredProcedureRepository<RevenueSummary>`
    - Methods: `InsertWithLinesAsync` (transactional with BEGIN TRAN), `UpdateWithLinesAsync` (transactional — delete old lines, update header, insert new lines), `SoftDeleteAsync`, `GetByIdWithLinesAsync`, `GetFilteredAsync`, `CountFilteredAsync`, `SumTotalVatForPeriodAsync`, `SumTotalGrossForDateRangeAsync`, `GetByPeriodIdAsync`
    - All queries filter by BusinessId, use OUTPUT INSERTED.Id for insert
    - _Requirements: 4.5, 5.4, 6.5, 7.3, 9.2, 16.1_

- [ ] 3. Service layer — RevenueSummaryService
  - [~] 3.1 Create IRevenueSummaryService interface and RevenueSummaryService class
    - Create `Portal.Infrastructure/Services/IRevenueSummaryService.cs`
    - Create `Portal.Infrastructure/Services/RevenueSummaryService.cs`
    - Inject: RevenueSourceRepository, RevenueSummaryRepository, VatSubmissionPeriodRepository, CurrentTenantService, PortalDbContext
    - Register in DI container
    - _Requirements: 3.1–3.8, 6.1–6.9, 7.1–7.6_

  - [~] 3.2 Implement Revenue Source CRUD methods
    - `GetActiveSourcesAsync` — filters by current BusinessId and IsActive = 1
    - `GetAllSourcesAsync` — all sources for business (active + inactive)
    - `GetSourceByIdAsync` — validates tenant ownership
    - `CreateSourceAsync` — validates name (not empty, ≤200 chars), inserts
    - `UpdateSourceAsync` — validates name, validates ownership, updates
    - `ToggleSourceActiveAsync` — sets IsActive, checks ownership
    - `SourceHasSummariesAsync` — checks if source has linked summaries (for advisory)
    - _Requirements: 3.1–3.8, 16.2_

  - [~] 3.3 Implement Revenue Summary validation and computation
    - `ValidateSummaryAsync` — checks: ≥1 line, all amounts non-negative, valid RevenueSourceId belongs to business
    - `RecomputeHeaderTotals` — TotalNet = SUM(NetAmount), TotalVat = SUM(VatAmount), TotalGross = SUM(TotalAmount), TotalDiscount = SUM(DiscountAmount)
    - Line TotalAmount = NetAmount + VatAmount (computed per line)
    - _Requirements: 5.4, 6.3, 6.4, 6.7, 6.8, 18.3, 18.4_

  - [~] 3.4 Implement VAT period assignment logic
    - `AssignVatPeriodAsync` — if explicit VatSubmissionPeriodId provided, use it; otherwise find unsubmitted period whose date range covers SummaryDate; if none found, leave NULL
    - _Requirements: 10.1–10.6_

  - [~] 3.5 Implement Revenue Summary CRUD methods
    - `CreateSummaryAsync` — validate, compute totals, assign period, insert (transactional)
    - `UpdateSummaryAsync` — validate ownership, check not locked, validate, compute totals, reassign period if needed, update (transactional)
    - `SoftDeleteSummaryAsync` — validate ownership, check not locked, set IsActive = 0
    - `GetSummaryByIdAsync` — with lines, tenant-filtered
    - `GetFilteredSummariesAsync` — paged, filtered by source/period/date range
    - `IsSummaryLockedAsync` — private helper checking submitted VatSubmission for the period
    - _Requirements: 6.1–6.9, 7.1–7.6, 8.1–8.6, 9.1–9.4_

  - [~] 3.6 Implement VAT integration and dashboard methods
    - `GetTotalVatForPeriodAsync` — SUM(TotalVat) WHERE IsActive=1 AND VatSubmissionPeriodId=period
    - `GetSummariesForPeriodAsync` — list items for period report section
    - `GetTotalGrossForDateRangeAsync` — SUM(TotalGross) WHERE IsActive=1 AND SummaryDate in range
    - _Requirements: 12.1, 13.1–13.6, 15.1–15.2_

  - [ ]* 3.7 Write property tests for validation and computation logic
    - **Property 3: Revenue Source Name Validation** — generate valid/invalid names, verify acceptance/rejection
    - **Property 4: Minimum One Line Required** — generate submissions with 0..N lines
    - **Property 5: Line Total Computation** — generate random non-negative decimal pairs, verify NetAmount + VatAmount = TotalAmount
    - **Property 6: Header Totals Invariant** — generate random line sets, verify SUM matches header
    - **Property 7: Non-Negative Amounts Validation** — generate positive/negative amounts, verify rejection/acceptance
    - **Validates: Requirements 3.8, 5.4, 6.3, 6.4, 6.7, 6.8, 7.4, 7.6, 18.1, 18.3, 18.4**

- [~] 4. Checkpoint — Database + Service layer
  - Ensure all migrations are syntactically correct, EF entities compile, service methods build successfully.
  - Run property tests (if implemented). Ask the user if questions arise.

- [ ] 5. View models and controller
  - [~] 5.1 Create view models
    - Create `Portal.Web/Models/ZReport/ZReportFormModel.cs`
    - Create `Portal.Web/Models/ZReport/ZReportLineFormModel.cs`
    - Create `Portal.Web/Models/ZReport/ZReportFilterModel.cs`
    - Create `Portal.Web/Models/ZReport/RevenueSummaryListItem.cs`
    - Create `Portal.Web/Models/ZReport/RevenueSourceFormModel.cs`
    - _Requirements: 6.1, 6.2, 8.2, 8.3_

  - [~] 5.2 Create ZReportController with page actions
    - Create `Portal.Web/Controllers/ZReportController.cs`
    - `[Authorize]` + `[ModuleAccess(PortalModules.Revenue)]`
    - Page actions: `Index()` (list), `Create()` (form), `Edit(int id)` (form), `Sources()` (manage sources)
    - Each page action checks `IsZReportEnabled` — redirect or show "feature not enabled" if disabled
    - For `Create()`: populate dropdowns (active sources, unsubmitted periods)
    - For `Edit(int id)`: load summary with lines, check not locked, populate dropdowns
    - For `Index()`: initial page load (filtering done via AJAX)
    - _Requirements: 1.5, 1.8, 1.9, 8.1, 8.6, 17.1–17.5_

  - [~] 5.3 Create ZReportController AJAX endpoints
    - `AxPostCreateZReport(ZReportFormModel model)` — calls service.CreateSummaryAsync, returns JSON
    - `AxPostUpdateZReport(ZReportFormModel model)` — calls service.UpdateSummaryAsync, returns JSON
    - `AxPostDeleteZReport(int id)` — calls service.SoftDeleteSummaryAsync, returns JSON
    - `AxPostCreateRevenueSource(RevenueSourceFormModel model)` — calls service.CreateSourceAsync
    - `AxPostUpdateRevenueSource(RevenueSourceFormModel model)` — calls service.UpdateSourceAsync
    - `AxPostToggleRevenueSource(int id, bool isActive)` — calls service.ToggleSourceActiveAsync
    - `AxGetZReportList(ZReportFilterModel filter)` — returns paged list JSON
    - `AxGetRevenueSourceList()` — returns source list JSON
    - All follow try/catch pattern with JSON responses: `{ success, message, data? }`
    - _Requirements: 3.2–3.5, 6.5, 7.3, 9.1–9.3, 16.3, 16.4_

- [ ] 6. Razor views — Z-Reports list and forms
  - [~] 6.1 Create Z-Reports list page (Index.cshtml)
    - Reference mockup: `revenue-zreports-list.html`
    - Filter panel (glass card-pad, margin-bottom:22px): Revenue Source dropdown, VAT Period dropdown, Date From/To pickers, Filter/Clear buttons
    - Data table (glass card-pad): columns per Req 8.2, clickable rows → Edit, pagination
    - "New Z-Report" button navigating to Create
    - Empty state when no records
    - AJAX loading via `AxGetZReportList` with BlockUI
    - _Requirements: 8.1–8.6_

  - [~] 6.2 Create Z-Report form page (Create.cshtml / Edit.cshtml — shared partial or single view with mode)
    - Reference mockup: `revenue-zreport-form.html`
    - Header fields: Revenue Source dropdown (required), Summary Date, Period End Date, Z-Report Number, Transaction Count, Reference, Notes, Exported At, VAT Period dropdown
    - Dynamic VAT Lines section: add/remove rows, each row with VatRate, NetAmount, VatAmount, DiscountAmount, Description, computed LineTotal
    - Header totals display (read-only computed): TotalNet, TotalVat, TotalGross, TotalDiscount
    - Form submission via AJAX (AxPostCreateZReport or AxPostUpdateZReport) with BlockUI + SweetAlert2
    - Locked state: all fields disabled + message when assigned to submitted period
    - _Requirements: 6.1–6.9, 7.1–7.6, 10.1–10.7_

  - [~] 6.3 Create Revenue Sources management page (Sources.cshtml)
    - Reference mockup: `revenue-sources.html`
    - List of all sources: Name, Description, Status badge, Created date
    - Inline create/edit modal or form panel
    - Toggle active/inactive via AJAX (AxPostToggleRevenueSource) — BlockUI + reload
    - Advisory message when deactivating a source with summaries (check via `SourceHasSummariesAsync`)
    - _Requirements: 3.1–3.8_

- [ ] 7. Client-side JavaScript — auto-computation and form interactions
  - [~] 7.1 Create `wwwroot/js/zreport-form.js`
    - `recalculateTotals()` — iterates VAT line rows, computes per-line TotalAmount = Net + Vat, updates header totals (SUM)
    - Attach event listeners on `.net-amount`, `.vat-amount`, `.discount-amount` inputs (input event)
    - Add Line button: appends a new VAT line row with empty fields
    - Remove Line button: removes the row, recalculates, enforces ≥1 line
    - Client-side validation: at least one line, non-negative amounts, required fields
    - _Requirements: 6.2, 6.3, 6.4, 6.7, 6.8, 18.1, 18.2_

  - [~] 7.2 Create `wwwroot/js/zreport-list.js`
    - Filter form submission → `AxGetZReportList` with BlockUI
    - Pagination controls (previous/next/page numbers)
    - Row click → navigate to Edit page
    - Clear filter resets dropdowns and date fields
    - _Requirements: 8.3, 8.4, 8.5_

  - [~] 7.3 Create `wwwroot/js/revenue-sources.js`
    - Create/Edit source via AJAX (AxPostCreateRevenueSource / AxPostUpdateRevenueSource) with BlockUI + SweetAlert2
    - Toggle active state via AJAX (AxPostToggleRevenueSource) — BlockUI + reload
    - Confirmation dialog (SweetAlert2) before deactivating a source that has summaries
    - _Requirements: 3.2–3.7_

- [~] 8. Checkpoint — Core Z-Report functionality
  - Verify: list page loads, create form saves a Z-Report with lines, edit pre-populates and saves, delete (soft) works, revenue source CRUD works.
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Feature toggle and navigation integration
  - [~] 9.1 Add IsZReportEnabled toggle to MyBusiness settings page
    - Add toggle control in the existing MyBusiness settings view
    - AJAX handler to update BusinessProfile.IsZReportEnabled (existing BusinessController pattern)
    - BlockUI + SweetAlert2 on success
    - _Requirements: 1.2, 1.3, 1.4_

  - [~] 9.2 Update sidebar navigation — conditional Z-Reports menu item
    - Add "Z-Reports" sub-item under Revenue section in the sidebar partial/ViewComponent
    - Conditional render: only when `IsZReportEnabled = true` for current business
    - Link to `ZReport/Index`
    - Position after existing Revenue sub-items
    - _Requirements: 1.5, 1.8, 17.1–17.5_

  - [~] 9.3 Add "Manage Sources" link on Z-Reports page
    - Link or settings icon on the Z-Reports list page navigating to Sources page
    - No separate sidebar item for Revenue Sources
    - _Requirements: 17.5_

  - [~] 9.4 Handle empty state — no Revenue Sources prompt
    - When IsZReportEnabled = true but no Revenue Sources exist, show prompt on Z-Reports Index: "Create your first Revenue Source to get started"
    - Link to Sources page
    - _Requirements: 1.9_

- [ ] 10. VAT integration
  - [~] 10.1 Extend VatSubmissionService — Output VAT contribution
    - Modify existing `CreateOrRecalculateAsync` (or equivalent method) to add Revenue Summary VAT contribution
    - Only add contribution when `IsZReportEnabled = true` for the business
    - Formula: existing output VAT + SUM(RevenueSummary.TotalVat WHERE IsActive=1 AND VatSubmissionPeriodId = period)
    - _Requirements: 12.1, 12.2, 12.3_

  - [~] 10.2 Extend VAT Period Report — Z-Reports section
    - Modify `VatController.BuildPeriodReportModelAsync` (or equivalent) to include Z-Report data
    - Add "External Revenue (Z-Reports)" section between Sales Invoices and Purchases
    - Table columns: Revenue Source Name, Z-Report Number, Period dates, Net, VAT, Total, Discount
    - Period Total row summing all Z-Reports
    - Conditional render: only when IsZReportEnabled = true
    - Empty state: "No Z-Reports assigned to this period."
    - _Requirements: 13.1–13.6_

  - [~] 10.3 Extend VAT Detail page — Z-Reports section
    - Add "External Revenue (Z-Reports)" section to VAT Detail page
    - Display each assigned Revenue Summary: Source Name, Z-Report Number, Period dates, Total VAT, Assignment type
    - Subtotal row for Z-Report VAT contribution
    - Conditional render: only when IsZReportEnabled = true
    - _Requirements: 14.1–14.4_

  - [ ]* 10.4 Write property tests for VAT integration
    - **Property 9: VAT Period Date-Range Fallback** — generate dates and period configurations, verify correct assignment or NULL
    - **Property 10: Output VAT Formula** — generate invoice/summary/credit note combinations, verify formula holds
    - **Validates: Requirements 10.5, 10.6, 12.1, 12.2, 12.3**

- [ ] 11. Revenue Dashboard integration
  - [~] 11.1 Extend DashboardService — include Z-Report revenue
    - Modify dashboard KPI aggregation to include `RevenueSummary.TotalGross` for active summaries within date range
    - Only include when `IsZReportEnabled = true`
    - Add indicator label/tooltip: "Includes POS revenue"
    - _Requirements: 15.1–15.4_

- [ ] 12. Document Attachment integration
  - [~] 12.1 Wire existing Document Attachment partial to Z-Report Edit page
    - Include the reusable Document Attachment panel (partial view) on the Z-Report Edit page
    - Pass `EntityType = "RevenueSummary"` and `EntityId = RevenueSummary.Id`
    - Only show after first save (not on Create before initial save)
    - Supports existing file types and limits (PDF, PNG, JPG, JPEG, WEBP; max 5 MB; max 5 files)
    - _Requirements: 11.1–11.5_

- [ ] 13. Tenant isolation enforcement
  - [~] 13.1 Verify tenant isolation across all endpoints
    - Review all repository methods: ensure BusinessId filter is applied in every query
    - Verify controller endpoints set BusinessId from authenticated session (never from client)
    - Verify Edit/Delete endpoints check `RevenueSummary.BusinessId == currentBusinessId` before processing
    - Verify Revenue Source and VAT Period dropdowns filter by current business
    - _Requirements: 16.1–16.6_

  - [ ]* 13.2 Write property test for tenant isolation
    - **Property 1: Tenant Isolation** — generate multi-tenant data sets, query from each business context, verify only own data returned
    - **Validates: Requirements 2.4, 16.1–16.6**

- [ ] 14. Subscription tier gating
  - [~] 14.1 Verify subscription access controls
    - Ensure Z-Report pages are gated by active subscription check (existing `[ModuleAccess]` attribute or subscription middleware)
    - IsZReportEnabled toggle only functional for active subscription tiers
    - No new code needed if existing subscription middleware covers Revenue module — verify and document
    - _Requirements: 19.1–19.3_

- [ ] 15. Final checkpoint — Full feature integration
  - Verify end-to-end: toggle enable → create source → create Z-Report → appears in list → VAT period report shows contribution → Output VAT includes Z-Report totals → Dashboard shows combined revenue → Document attachment works.
  - Run all property tests and unit tests. Ensure all pass.
  - Ask the user if questions arise.

  - [ ]* 15.1 Write remaining property tests
    - **Property 2: Active Source Filtering** — generate mix of active/inactive sources, verify dropdown returns only active
    - **Property 8: Soft Delete Exclusion** — generate active/inactive summaries, verify excluded from list/VAT/dashboard
    - **Property 11: Transactional Save Integrity** — verify 1 header + N lines exist after save
    - **Property 12: Edit Form Round-Trip** — save, load, save without changes, verify identical
    - **Validates: Requirements 3.6, 8.1, 9.4, 6.5, 6.9, 7.1**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at logical boundaries
- Property tests validate universal correctness properties using FsCheck with xUnit (min 100 iterations)
- Unit tests validate specific scenarios (locking, toggle, empty state)
- SQL migrations should be numbered sequentially following existing `Portal.Database/Migrations/` convention
- All AJAX flows follow: BlockUI.show → fetch → BlockUI.hide → Swal.fire
- All controller AJAX methods use `AxPost`/`AxGet` prefix convention
- Repository methods use full table names in SQL (no aliases)
- Client-side totals are preview only — server always recomputes
