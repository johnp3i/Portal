# Implementation Plan: Invoice VAT Period Assignment

## Overview

This plan implements explicit VAT period assignment for invoices, mirroring the existing Purchase pattern. The implementation progresses from database schema changes through entity/repository updates, service logic, controller endpoints, and finally the UI reassignment dialog. Each step builds incrementally on the previous, ensuring no orphaned code.

## Tasks

- [x] 1. Database migration and entity foundation
  - [x] 1.1 Create migration 048_AddVatSubmissionPeriodIdToInvoice.sql
    - Add nullable `VatSubmissionPeriodId INT` column to `[invoice].[Invoice]`
    - Add FK constraint `FK_Invoice_VatSubmissionPeriod` referencing `[vat].[VatSubmissionPeriod].[Id]`
    - Add filtered non-clustered index `IX_Invoice_VatSubmissionPeriodId` (WHERE VatSubmissionPeriodId IS NOT NULL)
    - Add idempotent backfill logic: set VatSubmissionPeriodId for existing invoices by date-range matching (earliest PeriodStartDate wins), only where VatSubmissionPeriodId IS NULL and IsDeleted = 0
    - All DDL and DML wrapped in IF NOT EXISTS guards for idempotency
    - _Requirements: 1.1, 1.4, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 1.2 Update Invoice entity with VatSubmissionPeriodId property and navigation
    - Add `public int? VatSubmissionPeriodId { get; set; }` to `Invoice.cs`
    - Add `public VatSubmissionPeriod? VatSubmissionPeriod { get; set; }` navigation property
    - Add `public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();` to `VatSubmissionPeriod.cs`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.3 Add ReassignmentImpactDto model
    - Create `Portal.Infrastructure/Models/ReassignmentImpactDto.cs` with properties: InvoiceNumber, TaxAmount, SourcePeriodLabel, TargetPeriodLabel, SourcePeriodProjectedOutputVat, TargetPeriodProjectedOutputVat, CurrencySymbol
    - _Requirements: 4.2, 4.3, 4.4_

- [x] 2. Repository layer updates
  - [x] 2.1 Add VatSubmissionPeriodRepository query methods
    - Add `GetByDateAndBusinessIdAsync(DateOnly invoiceDate, int businessId)` — returns period where PeriodStartDate <= invoiceDate AND PeriodEndDate >= invoiceDate (earliest PeriodStartDate if multiple)
    - Add `GetUnsubmittedPeriodsFromAsync(int businessId, DateOnly fromDate)` — returns periods ordered by PeriodStartDate ASC where PeriodStartDate >= fromDate and period has no VatSubmission or has one with IsSubmitted = false
    - Follow existing repository patterns (try/catch, SqlParameter, full table names)
    - _Requirements: 2.1, 2.3_

  - [x] 2.2 Add InvoiceRepository.UpdateVatPeriodAsync method
    - Add `UpdateVatPeriodAsync(int invoiceId, int? vatSubmissionPeriodId)` — UPDATE [invoice].[Invoice] SET VatSubmissionPeriodId = @VatSubmissionPeriodId, UpdatedAtUtc = @UpdatedAtUtc WHERE Id = @InvoiceId
    - Follow existing repository patterns (try/catch, SqlParameter, null-safe with DBNull.Value)
    - _Requirements: 3.9_

  - [x] 2.3 Update InvoiceRepository InsertAsync to include VatSubmissionPeriodId
    - Add `@VatSubmissionPeriodId` parameter to the INSERT statement
    - Ensure null-safe handling with `?? (object)DBNull.Value`
    - _Requirements: 2.6_

  - [x] 2.4 Update InvoiceRepository GetByIdAndBusinessIdAsync to include VatSubmissionPeriodId
    - Add `VatSubmissionPeriodId` to the SELECT column list
    - Map the column in the reader to the entity property
    - _Requirements: 1.1_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Service layer — auto-assignment logic
  - [x] 4.1 Implement AssignVatPeriodAsync private helper in InvoiceService
    - Add private method `AssignVatPeriodAsync(int businessId, DateOnly invoiceDate)` returning `Task<int?>`
    - Logic: find natural period → check if submitted → if not submitted return period Id → if submitted, cascade forward to first unsubmitted → if none found return null
    - Inject `VatSubmissionPeriodRepository` and `VatSubmissionRepository` into InvoiceService constructor
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 4.2 Integrate AssignVatPeriodAsync into CreateInvoiceAsync
    - Call `AssignVatPeriodAsync` before insert and set `invoice.VatSubmissionPeriodId` with the result
    - Ensure it executes within the existing transaction
    - _Requirements: 2.6_

  - [x] 4.3 Integrate AssignVatPeriodAsync into ConvertFromQuotationAsync
    - Call `AssignVatPeriodAsync` before insert and set `invoice.VatSubmissionPeriodId` with the result
    - Ensure it executes within the existing transaction
    - _Requirements: 2.6_

  - [x] 4.4 Write property test for auto-assignment selects natural unsubmitted period
    - **Property 4: Auto-assignment selects natural unsubmitted period**
    - **Validates: Requirements 2.1, 2.2**

  - [x] 4.5 Write property test for cascading assignment finds first unsubmitted period forward
    - **Property 5: Cascading assignment finds first unsubmitted period forward**
    - **Validates: Requirements 2.3**

- [x] 5. Service layer — reassignment logic
  - [x] 5.1 Implement ReassignVatPeriodAsync in InvoiceService
    - Add public method `ReassignVatPeriodAsync(int invoiceId, int targetPeriodId)` returning `Task<ServiceResult>`
    - Implement 7-step validation: invoice exists, invoice not deleted, target period exists, target period same business, target period not submitted, invoice not already assigned to target
    - On success: call `UpdateVatPeriodAsync`, write audit log entry
    - Add method signature to `IInvoiceService` interface
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9_

  - [x] 5.2 Implement GetReassignmentImpactAsync in InvoiceService
    - Add public method returning `Task<ServiceResult<ReassignmentImpactDto>>`
    - Compute projected Output VAT for source period (current total minus TaxAmount) and target period (current total plus TaxAmount)
    - Fetch currency symbol from business profile
    - Add method signature to `IInvoiceService` interface
    - _Requirements: 4.2, 4.3, 4.4_

  - [x] 5.3 Write property test for reassignment rejects submitted target periods
    - **Property 6: Reassignment rejects submitted target periods**
    - **Validates: Requirements 3.6**

  - [x] 5.4 Write property test for reassignment rejects cross-business attempts
    - **Property 7: Reassignment rejects cross-business attempts**
    - **Validates: Requirements 3.4, 3.5**

  - [x] 5.5 Write property test for successful reassignment updates period and timestamp
    - **Property 8: Successful reassignment updates period and timestamp**
    - **Validates: Requirements 3.9**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Update Output VAT computation in VatSubmissionService
  - [x] 7.1 Replace single Output VAT query with two-part computation in CreateOrRecalculateAsync
    - Part 1: Sum TaxAmount of invoices explicitly assigned to this period (VatSubmissionPeriodId == periodId, StatusTypeId == 2, IsDeleted == false)
    - Part 2: Sum TaxAmount of invoices with NULL VatSubmissionPeriodId falling in date range (backward compat)
    - Combine: totalOutputVat = explicitOutputVat + dateRangeOutputVat
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 7.2 Write property test for explicit assignment determines period inclusion
    - **Property 1: Explicit assignment determines period inclusion**
    - **Validates: Requirements 1.3, 5.1**

  - [x] 7.3 Write property test for NULL assignment falls back to date-range matching
    - **Property 2: NULL assignment falls back to date-range matching**
    - **Validates: Requirements 1.2, 5.2**

  - [x] 7.4 Write property test for mutual exclusivity — no double-counting
    - **Property 3: Mutual exclusivity — no invoice counted in multiple periods**
    - **Validates: Requirements 5.3, 5.4**

  - [x] 7.5 Write property test for submitted period computation is immutable
    - **Property 9: Submitted period computation is immutable**
    - **Validates: Requirements 5.5**

  - [x] 7.6 Write property test for projected impact is arithmetic over current totals
    - **Property 11: Projected impact is arithmetic over current totals**
    - **Validates: Requirements 4.4**

- [x] 8. Controller endpoints
  - [x] 8.1 Add ReassignVatPeriod POST endpoint to InvoiceController
    - Add `[HttpPost][ValidateAntiForgeryToken][ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]` endpoint
    - Accept `int invoiceId, int targetPeriodId` parameters
    - Call `_invoiceService.ReassignVatPeriodAsync` and return `Json(new { success, message })`
    - Inject any new dependencies into InvoiceController constructor
    - _Requirements: 3.1, 4.6_

  - [x] 8.2 Add GetReassignmentImpact GET endpoint to InvoiceController
    - Add `[HttpGet]` endpoint accepting `int invoiceId, int targetPeriodId`
    - Call `_invoiceService.GetReassignmentImpactAsync` and return JSON with impact data
    - _Requirements: 4.2, 4.3, 4.4_

  - [x] 8.3 Add GetUnsubmittedPeriods GET endpoint to InvoiceController
    - Add `[HttpGet]` endpoint to fetch available (unsubmitted) periods for the dropdown
    - Return JSON array of `{ id, periodLabel }` for periods the invoice can be reassigned to
    - _Requirements: 4.1_

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. UI — Reassignment dialog in Invoice Detail view
  - [x] 10.1 Add VAT period reassignment UI to Invoice Detail view
    - Add "Reassign VAT Period" button/dropdown in the invoice detail view
    - Populate dropdown with unsubmitted periods via AJAX GET to `GetUnsubmittedPeriods`
    - On selection: AJAX GET to `GetReassignmentImpact` to fetch financial impact data
    - Display SweetAlert2 confirmation dialog with destructive styling (`confirmButtonColor: '#C24A4A'`)
    - Show invoice number, source/target period labels, tax amount, projected Output VAT totals
    - On confirm: BlockUI.show → POST to `ReassignVatPeriod` → BlockUI.hide → Swal.fire result
    - On success: refresh page to reflect updated assignment
    - On error: Swal.fire with error message
    - Follow standard AJAX pattern (BlockUI + fetch + Swal)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

  - [x] 10.2 Display current VAT period assignment on Invoice Detail view
    - Show the currently assigned period label (or "Unassigned" if NULL) in the invoice detail header/metadata section
    - _Requirements: 1.2, 1.3_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

  - [x] 11.1 Write property test for backfill assigns earliest matching period
    - **Property 10: Backfill assigns earliest matching period by date range**
    - **Validates: Requirements 6.4, 6.6**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- The migration (task 1.1) follows the exact pattern from migration 041 (Purchase VatSubmissionPeriodId)
- Repository methods follow the established pattern: try/catch with rethrow, SqlParameter, full table names in queries
- The two-part Output VAT query (task 7.1) ensures backward compatibility with existing NULL-assignment invoices

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["4.1", "5.1", "5.2"] },
    { "id": 3, "tasks": ["4.2", "4.3", "4.4", "4.5", "5.3", "5.4", "5.5"] },
    { "id": 4, "tasks": ["7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3", "7.4", "7.5", "7.6", "8.1", "8.2", "8.3"] },
    { "id": 6, "tasks": ["10.1", "10.2", "11.1"] }
  ]
}
```
