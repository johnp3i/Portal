# Implementation Plan: Credit Note Module

## Overview

This plan implements the Credit Note module following the established Controller → Service → Repository architecture. Tasks are ordered by dependency: database schema first, then entities and EF Core configuration, repositories, service layer with business logic, controller endpoints, views, JavaScript interactions, and finally integration with existing systems (Financial Status Engine, VAT Submission Service, Audit Log). Property-based tests validate correctness properties defined in the design document.

## Tasks

- [x] 1. Database schema and migrations
  - [x] 1.1 Create the `[credit]` schema migration (062_CreateCreditSchema.sql)
    - Create `Portal.Database/Migrations/062_CreateCreditSchema.sql`
    - SQL: `IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'credit') BEGIN EXEC('CREATE SCHEMA [credit]'); END`
    - _Requirements: 1.12_

  - [x] 1.2 Create the CreditNoteStatusType table migration (063_CreateCreditNoteStatusTypeTable.sql)
    - Create `Portal.Database/Migrations/063_CreateCreditNoteStatusTypeTable.sql`
    - Table `[credit].[CreditNoteStatusType]` with Id (INT PK), Name (NVARCHAR(50))
    - Seed data: (1, 'Draft'), (2, 'Issued'), (3, 'Applied'), (4, 'Voided')
    - _Requirements: 3.1_

  - [x] 1.3 Create the CreditNote table migration (064_CreateCreditNoteTable.sql)
    - Create `Portal.Database/Migrations/064_CreateCreditNoteTable.sql`
    - Table `[credit].[CreditNote]` with all columns per design (Id, BusinessId, InvoiceId, CustomerId, CreditNoteStatusTypeId, VatSubmissionPeriodId, CreditNoteNumber, IssueDate, Reason, Subtotal, TaxAmount, TotalAmount, IssuedAtUtc, VoidedAtUtc, CreatedByUserId, CreatedAtUtc)
    - Foreign keys to `[portal].[Business]`, `[invoice].[Invoice]`, `[customer].[Customer]`, `[credit].[CreditNoteStatusType]`, `[vat].[VatSubmissionPeriod]`
    - Indexes: IX_CreditNote_BusinessId, IX_CreditNote_InvoiceId, UX_CreditNote_BusinessId_CreditNoteNumber (unique filtered, excludes Voided)
    - _Requirements: 1.11, 1.12, 2.4, 6.1_

  - [x] 1.4 Create the CreditNoteLine table migration (065_CreateCreditNoteLineTable.sql)
    - Create `Portal.Database/Migrations/065_CreateCreditNoteLineTable.sql`
    - Table `[credit].[CreditNoteLine]` with Id, CreditNoteId, Description, Quantity, UnitPrice, VatRate, LineTotal, SortOrder
    - Foreign key to `[credit].[CreditNote]` with ON DELETE CASCADE
    - Index: IX_CreditNoteLine_CreditNoteId
    - _Requirements: 1.6_

  - [x] 1.5 Create the CreditNoteApplication table migration (066_CreateCreditNoteApplicationTable.sql)
    - Create `Portal.Database/Migrations/066_CreateCreditNoteApplicationTable.sql`
    - Table `[credit].[CreditNoteApplication]` with Id, CreditNoteId, InvoiceId, AmountApplied, AppliedAtUtc, AppliedByUserId, IsVoided, CreatedAtUtc
    - Foreign keys to `[credit].[CreditNote]` and `[invoice].[Invoice]`
    - Indexes: IX_CreditNoteApplication_CreditNoteId, IX_CreditNoteApplication_InvoiceId
    - _Requirements: 4.1, 5.5_

- [ ] 2. Entity classes and EF Core configuration
  - [x] 2.1 Create entity classes for the credit note module
    - Create `Portal.Infrastructure/Entities/CreditNoteStatusType.cs`
    - Create `Portal.Infrastructure/Entities/CreditNote.cs`
    - Create `Portal.Infrastructure/Entities/CreditNoteLine.cs`
    - Create `Portal.Infrastructure/Entities/CreditNoteApplication.cs`
    - All entities follow existing patterns with navigation properties as defined in design
    - _Requirements: 1.6, 1.11, 3.1, 4.1_

  - [ ] 2.2 Add DbSet declarations and EF Core configuration to PortalDbContext
    - Add `DbSet<CreditNoteStatusType>`, `DbSet<CreditNote>`, `DbSet<CreditNoteLine>`, `DbSet<CreditNoteApplication>` to PortalDbContext
    - Add `ConfigureCreditNoteStatusType`, `ConfigureCreditNote`, `ConfigureCreditNoteLine`, `ConfigureCreditNoteApplication` methods
    - Call configuration methods from `OnModelCreating`
    - Configure table mappings to `[credit]` schema, precision, max lengths, indexes, relationships, and seed data per design
    - _Requirements: 1.12, 2.4, 3.1_

- [ ] 3. DTO models
  - [ ] 3.1 Create DTO classes for the credit note module
    - Create `Portal.Infrastructure/Models/CreditNote/CreateCreditNoteDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/UpdateCreditNoteDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreateCreditNoteLineDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteListDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteDetailDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteLineDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteApplicationDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteKpiDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNoteFilterDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/EligibleInvoiceDto.cs`
    - Create `Portal.Infrastructure/Models/CreditNote/CreditNotePdfModel.cs`
    - All DTOs follow the design document specifications
    - _Requirements: 1.1, 7.1, 8.1, 9.1, 10.1_

- [x] 4. Repository layer
  - [x] 4.1 Implement CreditNoteRepository
    - Create `Portal.Infrastructure/Repositories/CreditNoteRepository.cs`
    - Extends `GenericStoredProcedureRepository<CreditNote>`
    - Methods: InsertAsync, GetByIdAndBusinessIdAsync, UpdateStatusAsync, GetHighestNumberForYearAsync, GetPagedAsync, GetKpiDataAsync, GetTotalAppliedCreditAsync, UpdateAsync
    - Use full table names in SQL queries (no aliases), parameterized queries with SqlParameter
    - Try/catch with rethrow pattern per repository standards
    - _Requirements: 1.11, 2.2, 2.4, 7.1, 9.1_

  - [x] 4.2 Implement CreditNoteLineRepository
    - Create `Portal.Infrastructure/Repositories/CreditNoteLineRepository.cs`
    - Extends `GenericStoredProcedureRepository<CreditNoteLine>`
    - Methods: InsertBatchAsync, GetByCreditNoteIdAsync, DeleteByCreditNoteIdAsync
    - _Requirements: 1.6, 3.7_

  - [x] 4.3 Implement CreditNoteApplicationRepository
    - Create `Portal.Infrastructure/Repositories/CreditNoteApplicationRepository.cs`
    - Extends `GenericStoredProcedureRepository<CreditNoteApplication>`
    - Methods: InsertAsync, GetByCreditNoteIdAsync, VoidByCreditNoteIdAsync
    - _Requirements: 4.1, 5.5_

- [x] 5. Checkpoint - Ensure data layer compiles
  - Ensure all entity classes, DbContext configuration, DTOs, and repositories compile without errors. Ask the user if questions arise.

- [x] 6. Service layer — ICreditNoteService
  - [x] 6.1 Create ICreditNoteService interface and CreditNoteService class with DI registration
    - Create `Portal.Infrastructure/Services/ICreditNoteService.cs` with all method signatures per design
    - Create `Portal.Infrastructure/Services/CreditNoteService.cs` with constructor injection of repositories, IFinancialStatusEngine, tenant service, and DbContext
    - Register `ICreditNoteService` → `CreditNoteService` in DI container (Program.cs or service registration)
    - _Requirements: 1.12_

  - [x] 6.2 Implement CreateCreditNoteAsync with validation pipeline
    - Implement full validation: invoice status check, reason validation, line item count (1–50), line item field validation (description, quantity, unit price, VAT rate ranges), total vs outstanding balance check
    - Return ALL validation errors in a single response (not fail-fast)
    - Compute amounts: Subtotal, TaxAmount, TotalAmount per design formulas
    - Generate credit note number with retry logic (up to 3 attempts on uniqueness violation)
    - Insert credit note and lines, set status to Draft
    - Write audit log entry (Action = "CreditNoteCreated")
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 1.13, 2.1, 2.2, 2.3, 2.5, 2.6, 11.1, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8, 12.9, 12.10_

  - [x] 6.3 Implement UpdateCreditNoteAsync (Draft-only editing)
    - Validate credit note exists and belongs to current business
    - Reject edit if status is not Draft (CreditNoteStatusTypeId ≠ 1)
    - Re-validate all fields (same pipeline as create)
    - Delete existing lines and re-insert updated lines
    - Update credit note fields (IssueDate, Reason, VatSubmissionPeriodId, Subtotal, TaxAmount, TotalAmount)
    - _Requirements: 3.7, 3.8_

  - [x] 6.4 Implement IssueCreditNoteAsync (Draft → Issued transition)
    - Validate credit note exists, belongs to current business, and is in Draft status
    - Validate state transition is allowed (Draft → Issued)
    - Update status to Issued, set IssuedAtUtc = DateTime.UtcNow
    - Write audit log entry (Action = "CreditNoteStatusChanged", OldValues = "Draft", NewValues = "Issued")
    - _Requirements: 3.2, 3.5, 11.2_

  - [x] 6.5 Implement ApplyCreditNoteAsync (Issued → Applied with financial impact)
    - Validate credit note is in Issued status
    - Validate invoice eligibility (InvoiceFinancialStatusTypeId not in {3, 5})
    - Validate credit note TotalAmount does not exceed current outstanding balance
    - Create CreditNoteApplication record within a transaction
    - Update credit note status to Applied
    - Call FinancialStatusEngine.RecalculateStatusAsync to update invoice financial status
    - Write audit log entry (Action = "CreditNoteApplied")
    - All operations within a single database transaction
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 11.3_

  - [x] 6.6 Implement VoidCreditNoteAsync (with financial reversal for Applied)
    - Validate credit note exists and is in Draft, Issued, or Applied status
    - Check VAT period submission lock (reject void if period is submitted and credit note is not Draft)
    - If previously Applied: void CreditNoteApplication records (IsVoided = true), recalculate invoice financial status, write "CreditNoteReversed" audit entry
    - If Draft or Issued: no financial reversal needed
    - Update status to Voided, set VoidedAtUtc = DateTime.UtcNow
    - Write "CreditNoteStatusChanged" audit entry
    - All operations within a single database transaction with rollback on failure
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 6.6, 11.2, 11.4_

  - [x] 6.7 Implement query methods (GetCreditNotesPagedAsync, GetCreditNoteDetailAsync, GetKpiAsync, GetEligibleInvoicesAsync, GetInvoiceOutstandingBalanceAsync)
    - GetCreditNotesPagedAsync: paginated list with AND-logic filters (status, customer, date range, search term), scoped by BusinessId
    - GetCreditNoteDetailAsync: full detail with lines and application history
    - GetKpiAsync: compute Total Issued count, Total Value sum, Pending Application count per design KPI queries
    - GetEligibleInvoicesAsync: return invoices in Issued status with outstanding balance > 0 for current business
    - GetInvoiceOutstandingBalanceAsync: Invoice.TotalAmount - non-voided payments - applied credit totals
    - _Requirements: 7.1, 7.2, 7.3, 8.1, 8.2, 8.3, 8.4, 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 6.8 Write property test: Amount Computation Chain (Property 1)
    - **Property 1: Amount Computation Chain**
    - For any set of valid credit note lines, verify Subtotal = sum(Quantity × UnitPrice), TaxAmount = sum(LineTotal × VatRate / 100), TotalAmount = Subtotal + TaxAmount
    - Generator: random lines with Quantity (0.0001–999999), UnitPrice (0.01–999999999.99), VatRate (0–100)
    - Use FsCheck.Xunit with minimum 100 iterations
    - **Validates: Requirements 1.6, 1.7, 1.8, 1.9**

  - [x] 6.9 Write property test: Credit Note Number Format and Sequencing (Property 2)
    - **Property 2: Credit Note Number Format and Sequencing**
    - For any BusinessId and year, verify generated numbers match `CN-YYYY-NNNN` pattern and sequential numbers increment by exactly 1
    - Generator: random years (2020–2030), random existing counts (0–9998)
    - **Validates: Requirements 2.1, 2.2, 2.3**

  - [x] 6.10 Write property test: State Machine Validity (Property 3)
    - **Property 3: State Machine Validity**
    - For any (currentStatus, targetStatus) pair from {1,2,3,4}×{1,2,3,4}, verify transition succeeds iff pair is in allowed set; verify editing succeeds iff status is Draft
    - Generator: random status pairs
    - **Validates: Requirements 3.2, 3.4, 3.5, 3.6, 3.7, 3.8, 4.2**

  - [x] 6.11 Write property test: Balance Ceiling Validation (Property 4)
    - **Property 4: Balance Ceiling Validation**
    - For any credit note where TotalAmount > outstanding balance, verify creation/application is rejected
    - Generator: random TotalAmount and OutstandingBalance where TotalAmount > OutstandingBalance
    - **Validates: Requirements 1.10, 4.7**

  - [x] 6.12 Write property test: Application Creates Correct Financial Impact (Property 5)
    - **Property 5: Application Creates Correct Financial Impact**
    - For any issued credit note applied to its source invoice, verify new outstanding = previous outstanding - TotalAmount, and financial status is Paid (if 0) or PartiallyPaid (if > 0)
    - Generator: random invoice amounts, payment sums, credit amounts
    - **Validates: Requirements 4.1, 4.3, 4.5, 4.6**

  - [x] 6.13 Write property test: Void Reversal Round-Trip (Property 6)
    - **Property 6: Void Reversal Round-Trip**
    - For any previously applied credit note that is voided, verify outstanding balance is restored to pre-application value and all CreditNoteApplication records have IsVoided = true
    - Generator: random applied credit notes with known pre-application balance
    - **Validates: Requirements 5.3, 5.4, 5.5**

  - [x] 6.14 Write property test: Draft/Issued Void Has No Financial Side-Effect (Property 7)
    - **Property 7: Draft/Issued Void Has No Financial Side-Effect**
    - For any credit note in Draft or Issued status that is voided, verify invoice outstanding balance and financial status remain unchanged
    - Generator: random Draft/Issued credit notes
    - **Validates: Requirements 5.9**

  - [x] 6.15 Write property test: Validation Pipeline Returns All Errors (Property 10)
    - **Property 10: Validation Pipeline Returns All Errors**
    - For any credit note submission with multiple simultaneous violations, verify ALL applicable error messages are returned in a single response
    - Generator: DTOs with random combinations of invalid fields (empty reason, zero lines, invalid quantities, exceeding balance)
    - **Validates: Requirements 12.10**

  - [x] 6.16 Write property test: Invoice Eligibility Gate (Property 11)
    - **Property 11: Invoice Eligibility Gate**
    - For any invoice with InvoiceStatusTypeId ≠ 2 or InvoiceFinancialStatusTypeId in {3, 5}, verify credit note creation/application is rejected
    - Generator: random invoices with various status combinations
    - **Validates: Requirements 1.3, 4.9, 12.1**

- [x] 7. Checkpoint - Ensure service layer compiles and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Financial Status Engine and VAT Submission integration
  - [x] 8.1 Extend FinancialStatusEngine to account for applied credit notes
    - Update `ComputeOutstandingBalance` to accept `appliedCreditTotal` parameter
    - Update `RecalculateStatusAsync` to fetch applied credit totals from `CreditNoteRepository.GetTotalAppliedCreditAsync()` and pass to computation
    - Outstanding = TotalAmount - validPaymentSum - appliedCreditTotal
    - Update financial status: Paid (3) if outstanding = 0, PartiallyPaid (2) if outstanding > 0 but < TotalAmount
    - _Requirements: 4.5, 4.6, 5.3, 5.4_

  - [x] 8.2 Extend VatSubmissionService to subtract credit note TaxAmount from Output VAT
    - In `CreateOrRecalculateAsync`, query credit notes in Issued or Applied status for the period
    - Subtract sum of TaxAmount from total Output VAT
    - Exclude Draft and Voided credit notes from the calculation
    - _Requirements: 6.2, 6.3_

  - [x] 8.3 Write property test: VAT Output Reduction (Property 8)
    - **Property 8: VAT Output Reduction**
    - For any VAT period computation, verify Output VAT = invoice tax sum - credit note tax sum (Issued/Applied only); Draft/Voided excluded
    - Generator: random sets of credit notes with mixed statuses per period
    - **Validates: Requirements 6.2, 6.3**

  - [x] 8.4 Write property test: VAT Period Submission Lock (Property 9)
    - **Property 9: VAT Period Submission Lock**
    - For any credit note assigned to a submitted VAT period, verify creation is rejected; for non-Draft credit notes in submitted periods, verify voiding is rejected
    - Generator: random credit notes assigned to submitted/unsubmitted periods
    - **Validates: Requirements 6.5, 6.6**

- [x] 9. Controller layer — CreditNoteController
  - [x] 9.1 Create CreditNoteController with Index, Create (GET/POST), Detail, Edit (GET/POST) actions
    - Create `Portal.Web/Controllers/CreditNoteController.cs`
    - `[Authorize]` and `[ModuleAccess(PortalModules.Invoice)]` attributes
    - Inject ICreditNoteService, ICreditNoteRenderer, tenant service
    - GET Index: load KPI data, customers for filter dropdown, render Index view
    - GET Create: load eligible invoices, VAT periods (unsubmitted only, default to latest), render Create view
    - POST Create: map form to CreateCreditNoteDto, call service, redirect to Detail on success or return errors
    - GET Detail: load full detail DTO, render Detail view with action buttons based on status
    - GET Edit: validate Draft status, load credit note for editing, render Edit view
    - POST Edit: map form to UpdateCreditNoteDto, call service, redirect to Detail on success
    - _Requirements: 1.1, 1.2, 6.4, 7.1, 8.1, 8.6, 8.7, 8.8, 8.9_

  - [x] 9.2 Implement AJAX endpoints (Issue, Apply, Void, GetInvoiceBalance, GetEligibleInvoices, GetKpi)
    - POST Issue: call IssueCreditNoteAsync, return JSON { success, message }
    - POST Apply: call ApplyCreditNoteAsync, return JSON { success, message }
    - POST Void: call VoidCreditNoteAsync, return JSON { success, message }
    - GET GetInvoiceBalance: call GetInvoiceOutstandingBalanceAsync, return JSON { balance }
    - GET GetEligibleInvoices: call GetEligibleInvoicesAsync, return JSON list
    - GET GetKpi: call GetKpiAsync, return JSON KPI data
    - All POST endpoints use `[ValidateAntiForgeryToken]`
    - All endpoints wrapped in try/catch returning JSON error on failure
    - _Requirements: 3.2, 4.1, 5.1, 9.1, 9.6_

  - [x] 9.3 Implement paginated list endpoint and PDF preview endpoint
    - GET/POST paginated list: accept filter parameters (status, customer, dateFrom, dateTo, search, page), call GetCreditNotesPagedAsync, return JSON { items, totalCount, page, pageSize }
    - GET PreviewPdf: validate Issued/Applied status, build CreditNotePdfModel, render HTML via ICreditNoteRenderer, generate PDF via PuppeteerSharp (30s timeout), return file download named `CreditNote_{CreditNoteNumber}.pdf`
    - Handle PDF timeout with OperationCanceledException → JSON error
    - _Requirements: 7.1, 7.5, 10.1, 10.3, 10.4, 10.5, 10.6_

- [x] 10. Views — Razor pages
  - [x] 10.1 Create Index view (Views/CreditNote/Index.cshtml)
    - KPI cards row: 3-column grid (18px gap), cards with left border colours (#0D5EA6, #C24A4A, #C8912E)
    - Filter panel: glass card-pad with margin-bottom:22px, flex layout with gap:14px, fields for status/customer/date range/search
    - Data table: glass card-pad, columns for credit note number, customer, invoice ref, issue date, total (2dp with currency), status pill, reason
    - Status pills with colour mapping: Draft=#C8912E, Issued=#129867, Applied=#0D5EA6, Voided=#C24A4A
    - Pagination: flex layout with info text and page controls, margin-top:18px
    - Empty state message when no results
    - _Requirements: 7.1, 7.2, 7.4, 7.5, 7.6, 7.7, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 10.2 Create Create view (Views/CreditNote/Create.cshtml)
    - Form with: source invoice dropdown (eligible invoices), auto-generated credit note number (read-only), issue date, VAT period dropdown (unsubmitted periods, default to latest), reason textarea (max 1000 chars)
    - Dynamic line items table: description (max 250 chars), quantity, unit price, VAT rate, line total (computed), add/remove row buttons
    - Display invoice outstanding balance when invoice selected (AJAX call)
    - Pre-populate customer name as read-only when invoice selected
    - Running totals: subtotal, VAT amount, total
    - _Requirements: 1.1, 1.2, 6.4_

  - [x] 10.3 Create Detail view (Views/CreditNote/Detail.cshtml)
    - Header: credit note number, customer name, status pill, issue date, source invoice link, VAT period, reason
    - Line items table: description, quantity, unit price, VAT rate, line total
    - Totals section: subtotal, VAT amount, credit total (negative, styled #C24A4A)
    - Application history table: date applied, invoice link, amount applied, applying user
    - Empty state for no applications
    - Action buttons conditional on status (Draft: Issue/Edit/Void; Issued: Apply/PDF/Void; Applied: PDF/Void; Voided: none)
    - "Fully applied — no remaining balance" indicator when Applied
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10_

  - [x] 10.4 Create Edit view (Views/CreditNote/Edit.cshtml)
    - Same form layout as Create but pre-populated with existing data
    - Invoice selection is read-only (cannot change source invoice)
    - Dynamic line items pre-populated with existing lines
    - Only accessible for Draft status credit notes
    - _Requirements: 3.7_

  - [x] 10.5 Create PDF partial view (Views/CreditNote/_CreditNotePdf.cshtml)
    - Razor partial for HTML-to-PDF rendering via PuppeteerSharp
    - Layout: business details (name, address, VAT number, logo), customer details, credit note number, issue date, source invoice reference, reason
    - Line items table: description, quantity, unit price, VAT rate, line total
    - Totals: subtotal, VAT amount, total (formatted with currency symbol)
    - Styled for print/PDF output (inline CSS, no external dependencies)
    - _Requirements: 10.1, 10.4_

- [x] 11. JavaScript — AJAX interactions and UI behaviour
  - [x] 11.1 Implement credit note list page JavaScript (filtering, pagination, KPI loading)
    - AJAX filter submission: collect filter values, call paginated list endpoint, render table rows dynamically
    - Pagination: handle page navigation clicks, update table and pagination info
    - KPI loading: fetch KPI data on page load, update card values (handle error with "—" fallback)
    - Clear button: reset all filters to defaults, reload unfiltered list
    - Use BlockUI.show/hide pattern for all AJAX calls
    - Use vanilla fetch API (no jQuery)
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 7.7, 9.1, 9.5, 9.6_

  - [x] 11.2 Implement create/edit form JavaScript (dynamic line items, invoice selection, running totals)
    - Add/remove line item rows dynamically
    - Compute line totals (quantity × unit price) on input change
    - Compute running subtotal, VAT amount, and total as user edits lines
    - On invoice selection: AJAX call to GetInvoiceBalance, display outstanding balance, populate customer name
    - Client-side validation feedback (description required, quantity/price ranges, VAT rate range)
    - Use BlockUI.show/hide for AJAX calls
    - _Requirements: 1.1, 1.2_

  - [x] 11.3 Implement detail page JavaScript (Issue, Apply, Void actions with SweetAlert2)
    - Issue button: BlockUI + fetch POST to /CreditNote/Issue, Swal success/error
    - Apply button: BlockUI + fetch POST to /CreditNote/Apply, Swal success/error
    - Void button: two-step SweetAlert2 confirmation (step 1: warning with consequences, step 2: danger confirm with #C24A4A button), then BlockUI + fetch POST to /CreditNote/Void
    - PDF preview button: navigate to /CreditNote/PreviewPdf (file download)
    - Include antiforgery token in all POST requests
    - _Requirements: 5.1, 8.6, 8.7, 8.8, 10.1_

- [x] 12. Checkpoint - Ensure full application compiles and renders
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. ICreditNoteRenderer and PDF generation wiring
  - [x] 13.1 Implement ICreditNoteRenderer and register in DI
    - Create `Portal.Web/Services/ICreditNoteRenderer.cs` interface
    - Create `Portal.Web/Services/CreditNoteRenderer.cs` implementation using IViewRenderService to render `_CreditNotePdf.cshtml` to HTML string
    - Register in DI container
    - Wire PDF generation in controller: render HTML → PuppeteerSharp with 30s CancellationTokenSource → return File result
    - Filename pattern: `CreditNote_{CreditNoteNumber}.pdf`
    - _Requirements: 10.1, 10.3, 10.4, 10.5, 10.6_

- [x] 14. Audit logging integration
  - [x] 14.1 Implement explicit audit log entries for credit note business events
    - Ensure CreditNoteCreated audit entry is written on creation (credit note number, userId in NewValues)
    - Ensure CreditNoteStatusChanged audit entry is written on all status transitions (old status in OldValues, new status + userId in NewValues)
    - Ensure CreditNoteApplied audit entry is written on application (invoiceId, amount, userId in NewValues)
    - Ensure CreditNoteReversed audit entry is written on void of applied credit note (invoiceId, reversed amount, userId in NewValues)
    - Verify existing EF Core SaveChanges interceptor captures entity-level field changes automatically
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [ ] 15. Unit tests
  - [ ] 15.1 Write unit tests for CreditNoteService (creation, lifecycle, validation)
    - Test number generation: first of year → CN-YYYY-0001, sequential increment, 9999 limit rejection
    - Test number generation retry on uniqueness conflict (mock DbUpdateException)
    - Test validation pipeline: empty reason, zero lines, >50 lines, invalid quantity/price/VAT ranges, exceeding balance
    - Test state transitions: all valid transitions succeed, all invalid transitions rejected
    - Test Draft-only editing: edit succeeds for Draft, rejected for Issued/Applied/Voided
    - _Requirements: 1.3, 1.4, 1.5, 1.10, 1.13, 2.1, 2.3, 2.5, 2.6, 3.2, 3.4, 3.5, 3.6, 3.7, 3.8, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8, 12.9_

  - [ ] 15.2 Write unit tests for KPI computation and eligible invoices
    - Test KPI with known data: verify counts and sums match expected values
    - Test KPI with no data: verify zeros returned
    - Test eligible invoices: only Issued invoices with outstanding > 0 returned
    - Test VAT period dropdown: only unsubmitted periods returned, default to latest
    - _Requirements: 9.2, 9.3, 9.4, 9.5, 6.4_

  - [ ] 15.3 Write unit tests for Apply and Void operations
    - Test apply: verify application record created, status transitions to Applied, financial status recalculated
    - Test apply rejection: non-Issued status, Paid/WrittenOff invoice, exceeding balance
    - Test void of Draft/Issued: no financial reversal, status → Voided
    - Test void of Applied: financial reversal, application records voided, invoice balance restored
    - Test void rejection: VAT period submitted (non-Draft)
    - _Requirements: 4.1, 4.2, 4.7, 4.9, 5.2, 5.3, 5.5, 5.7, 5.9, 6.6_

- [ ] 16. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at key integration points
- Property tests validate universal correctness properties defined in the design document
- Unit tests validate specific examples and edge cases
- The implementation uses C# with ASP.NET Core MVC 8, EF Core (Database-First), and SQL Server
- All repositories follow the GenericStoredProcedureRepository pattern with full table names in SQL (no aliases)
- All AJAX calls follow the BlockUI.show → fetch → BlockUI.hide → Swal.fire pattern
- All confirmation dialogs use SweetAlert2 (never native alert/confirm)
- Status pills use the MyChair Design System colour palette
- PDF generation reuses the existing PuppeteerSharp pipeline (same as Customer Statement)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["1.4", "1.5"] },
    { "id": 4, "tasks": ["2.1"] },
    { "id": 5, "tasks": ["2.2", "3.1"] },
    { "id": 6, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 7, "tasks": ["6.1"] },
    { "id": 8, "tasks": ["6.2", "6.3", "6.4"] },
    { "id": 9, "tasks": ["6.5", "6.6", "6.7"] },
    { "id": 10, "tasks": ["6.8", "6.9", "6.10", "6.11"] },
    { "id": 11, "tasks": ["6.12", "6.13", "6.14", "6.15", "6.16"] },
    { "id": 12, "tasks": ["8.1", "8.2"] },
    { "id": 13, "tasks": ["8.3", "8.4"] },
    { "id": 14, "tasks": ["9.1"] },
    { "id": 15, "tasks": ["9.2", "9.3"] },
    { "id": 16, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5"] },
    { "id": 17, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 18, "tasks": ["13.1", "14.1"] },
    { "id": 19, "tasks": ["15.1", "15.2", "15.3"] }
  ]
}
```
