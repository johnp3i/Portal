# Implementation Plan: VAT Submissions

## Overview

Implement the VAT Submissions module (Module 6) following the MVC → Service → Repository architecture. The implementation builds incrementally: data models and repositories first, then services with business logic, then the controller, and finally the Razor views. Property-based tests validate the period generation algorithm and VAT computation logic using FsCheck.Xunit.

## Tasks

- [x] 1. Set up data models, view models, and repository layer
  - [x] 1.1 Create VatSubmissionPeriod and VatSubmission entity classes and configure EF Core mappings
    - Create `VatSubmissionPeriod` entity with Id, BusinessId, PeriodStartDate (DateOnly), PeriodEndDate (DateOnly), PeriodLabel, CreatedAtUtc
    - Create `VatSubmission` entity with Id, BusinessId, VatSubmissionPeriodId, TotalOutputVat, TotalInputVat, NetVatPayable, IsSubmitted, SubmittedAtUtc, Notes, CreatedAtUtc
    - Add DbSet properties to PortalDbContext for both entities
    - Configure global query filters on BusinessId for tenant isolation
    - Add unique constraints: (BusinessId, PeriodStartDate) on VatSubmissionPeriod, (BusinessId, VatSubmissionPeriodId) on VatSubmission
    - _Requirements: 9.1, 9.2_

  - [x] 1.2 Create VatPeriodsListViewModel and VatSubmissionDetailViewModel
    - Create `VatPeriodsListViewModel` with `List<VatPeriodRowViewModel>` containing PeriodId, PeriodLabel, PeriodStartDate, PeriodEndDate, Status, SubmittedAtUtc
    - Create `VatSubmissionDetailViewModel` with SubmissionId, PeriodId, PeriodLabel, PeriodStartDate, PeriodEndDate, TotalOutputVat, TotalInputVat, NetVatPayable, IsSubmitted, SubmittedAtUtc, CurrencySymbol
    - _Requirements: 7.2, 7.3, 8.1, 8.2_

  - [x] 1.3 Implement VatSubmissionPeriodRepository
    - Extend `GenericStoredProcedureRepository<VatSubmissionPeriod>`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — returns all periods ordered by PeriodStartDate descending, using full table names in SQL
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)`
    - Implement `GetLatestByBusinessIdAsync(int businessId)` — returns the period with the most recent PeriodEndDate
    - Implement `InsertAsync(VatSubmissionPeriod entity)` — INSERT into `[vat].[VatSubmissionPeriod]`
    - Use null-safe parameters (`?? (object)DBNull.Value`), try/catch with rethrow, full table names without aliases
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8_

  - [x] 1.4 Implement VatSubmissionRepository
    - Extend `GenericStoredProcedureRepository<VatSubmission>`
    - Implement `GetAllByBusinessIdAsync(int businessId)`
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)`
    - Implement `GetByPeriodIdAndBusinessIdAsync(int vatSubmissionPeriodId, int businessId)`
    - Implement `InsertAsync(VatSubmission entity)` — INSERT into `[vat].[VatSubmission]`
    - Implement `UpdateValuesAsync(VatSubmission entity)` — UPDATE TotalOutputVat, TotalInputVat, NetVatPayable
    - Implement `MarkAsSubmittedAsync(int id, int businessId)` — UPDATE IsSubmitted = 1, SubmittedAtUtc = GETUTCDATE()
    - Use null-safe parameters, try/catch with rethrow, full table names without aliases
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [x] 1.5 Create ServiceResult&lt;T&gt; generic class
    - Extend existing `ServiceResult` with a generic variant `ServiceResult<T>` carrying a `Data` property
    - Implement `static ServiceResult<T> Ok(T data)` and `static new ServiceResult<T> Fail(string message)`
    - _Requirements: 4.9_

- [x] 2. Implement service layer
  - [x] 2.1 Implement IVatPeriodGenerationService interface and VatPeriodGenerationService
    - Create `IVatPeriodGenerationService` with `Task<List<VatSubmissionPeriod>> GeneratePeriodsAsync()`
    - Implement `VatPeriodGenerationService` as a scoped service
    - Inject ICurrentTenantService, VatSubmissionPeriodRepository, and PortalDbContext (for BusinessProfile access)
    - Implement period generation algorithm: validate VatRegistrationDate (return empty if default), validate VatPeriodLengthInMonths ∈ {1,2,3,4,6,12}, determine start date from latest existing period or VatRegistrationDate, generate periods up to current date
    - Assign PeriodLabel in format "dd MMM yyyy – dd MMM yyyy"
    - Only generate new periods that don't already exist (idempotent)
    - Register in DI container as scoped
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 3.11_

  - [x]* 2.2 Write property tests for period generation — no overlapping periods
    - **Property 1: No overlapping periods**
    - **Validates: Requirements 11.1**
    - Use FsCheck.Xunit with `[Property(MaxTest = 100)]`
    - Generate arbitrary valid VatRegistrationDate and VatPeriodLengthInMonths ∈ {1,2,3,4,6,12}
    - Assert no date belongs to more than one generated period

  - [x]* 2.3 Write property tests for period generation — contiguous periods
    - **Property 2: Contiguous periods (no gaps)**
    - **Validates: Requirements 3.5, 11.2**
    - For all consecutive period pairs, assert PeriodStartDate of N+1 equals PeriodEndDate of N plus one day

  - [x]* 2.4 Write property tests for period generation — period duration
    - **Property 3: Period duration equals configured months**
    - **Validates: Requirements 3.4, 11.3, 11.5**
    - Assert every period's end date equals start date plus VatPeriodLengthInMonths months minus one day

  - [x]* 2.5 Write property tests for period generation — first period anchor
    - **Property 4: First period anchored to VatRegistrationDate**
    - **Validates: Requirements 3.3, 11.4**
    - Assert first generated period's PeriodStartDate equals VatRegistrationDate

  - [x]* 2.6 Write property tests for period generation — coverage and label format
    - **Property 5: Coverage up to current date**
    - **Validates: Requirements 3.6, 3.7**
    - Assert last period contains current date and no period starts after current date
    - **Property 6: Period label format consistency**
    - **Validates: Requirements 3.8**
    - Assert PeriodLabel matches "{PeriodStartDate:dd MMM yyyy} – {PeriodEndDate:dd MMM yyyy}"

  - [x]* 2.7 Write property tests for period generation — idempotence and invalid length
    - **Property 7: Generation idempotence**
    - **Validates: Requirements 3.9**
    - Assert calling generation multiple times produces same set of periods
    - **Property 8: Invalid period length rejection**
    - **Validates: Requirements 3.11**
    - Assert values not in {1,2,3,4,6,12} return empty collection or throw

  - [x] 2.8 Implement IVatSubmissionService interface and VatSubmissionService
    - Create `IVatSubmissionService` with `CreateOrRecalculateAsync`, `MarkAsSubmittedAsync`, `GetByPeriodIdAsync`
    - Implement `VatSubmissionService` as a scoped service
    - Inject ICurrentTenantService, VatSubmissionRepository, VatSubmissionPeriodRepository, InvoiceRepository (or DbContext for invoice queries), PurchaseRepository (or DbContext for purchase queries), AuditLogRepository
    - Implement `CreateOrRecalculateAsync(int vatSubmissionPeriodId)`:
      - Validate period belongs to current tenant
      - Compute TotalOutputVat: SUM(TaxAmount) from invoices where InvoiceStatusTypeId=2, IsDeleted=false, InvoiceDate within period
      - Compute TotalInputVat: SUM(VatAmount) from purchases where PurchaseOriginTypeId≠2, IsCancelled=false, InvoiceDate within period
      - Compute NetVatPayable = TotalOutputVat - TotalInputVat
      - If submission exists and IsSubmitted=false: update values; else if no submission: insert new
      - Write audit log entry (Created or Recalculated)
      - Return ServiceResult<VatSubmission>.Ok(submission)
    - Implement `MarkAsSubmittedAsync(int vatSubmissionId)`:
      - Validate submission exists and belongs to tenant
      - Reject if already submitted
      - Set IsSubmitted=true, SubmittedAtUtc=DateTime.UtcNow
      - Write audit log entry (MarkedAsSubmitted)
      - Return ServiceResult.Ok()
    - Implement `GetByPeriodIdAsync(int vatSubmissionPeriodId)`
    - Register in DI container as scoped
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 5.1, 5.2, 5.3, 5.4, 10.1, 10.2, 10.3, 10.4_

  - [x]* 2.9 Write property tests for VAT computation logic
    - **Property 9: Output VAT computation correctness**
    - **Validates: Requirements 4.3**
    - Generate arbitrary sets of invoices, assert TotalOutputVat equals sum of TaxAmount from issued, non-deleted invoices within period
    - **Property 10: Input VAT computation correctness**
    - **Validates: Requirements 4.4**
    - Generate arbitrary sets of purchases, assert TotalInputVat equals sum of VatAmount excluding EU Reverse Charge and cancelled purchases within period
    - **Property 11: Net VAT payable is the difference**
    - **Validates: Requirements 4.5**
    - Assert NetVatPayable always equals TotalOutputVat minus TotalInputVat

  - [x]* 2.10 Write unit tests for VatSubmissionService edge cases
    - Test: already-submitted submission returns failure on MarkAsSubmitted
    - Test: period not belonging to tenant returns failure
    - Test: zero invoices/purchases in period produces zero VAT values
    - Test: negative NetVatPayable (refund scenario) is correctly computed
    - Test: recalculation updates existing submission, does not create duplicate
    - _Requirements: 4.6, 4.8, 5.2, 5.3_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement controller and DI registration
  - [x] 4.1 Implement VatController
    - Create `VatController` with `[Authorize]` and `[ModuleAccess(PortalModules.Vat)]` attributes
    - Inject `IVatPeriodGenerationService`, `IVatSubmissionService`, `ICurrentTenantService`
    - Implement `Index()` (GET): call `GeneratePeriodsAsync()`, fetch all submissions for tenant, build `VatPeriodsListViewModel` with status logic (Submitted/Pending/Not Started), return view ordered by PeriodStartDate descending
    - Implement `Detail(int periodId)` (GET): call `CreateOrRecalculateAsync(periodId)`, if failure return NotFound, build `VatSubmissionDetailViewModel`, return view
    - Implement `MarkAsSubmitted(int submissionId)` (POST): add `[ValidateAntiForgeryToken]` and `[ModuleAccess(PortalModules.Vat, AccessLevels.Full)]`, call `MarkAsSubmittedAsync(submissionId)`, return JSON `{ success, message }`
    - Handle service failures: return JSON error for POST, NotFound for GET when period/submission not found
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 9.4_

  - [x] 4.2 Register services and repositories in DI container
    - Register `VatSubmissionPeriodRepository` as scoped
    - Register `VatSubmissionRepository` as scoped
    - Register `IVatPeriodGenerationService` → `VatPeriodGenerationService` as scoped
    - Register `IVatSubmissionService` → `VatSubmissionService` as scoped
    - _Requirements: 3.2, 4.2_

- [x] 5. Implement Razor views
  - [x] 5.1 Create VAT Periods List view (Views/Vat/Index.cshtml)
    - Display periods in a table layout following MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body)
    - Show columns: PeriodLabel, PeriodStartDate, PeriodEndDate, Status badge, SubmittedAtUtc (when applicable)
    - Status badges: "Submitted" (green #129867), "Pending" (orange #C8912E), "Not Started" (grey)
    - Order periods by PeriodStartDate descending
    - Provide action link to Detail view for each period
    - Display SubmittedAtUtc date alongside status badge for submitted periods
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 5.2 Create VAT Submission Detail view (Views/Vat/Detail.cshtml)
    - Display period label and date range at top
    - Display TotalOutputVat, TotalInputVat, NetVatPayable in clear breakdown layout (MyChair Design System)
    - Conditional labels: positive NetVatPayable = "Tax Owed", negative = "Refund Due", zero = "No Payment Due"
    - Show "Mark as Submitted" button when IsSubmitted = false; hide when already submitted (show SubmittedAtUtc instead)
    - Implement SweetAlert2 confirmation dialog on "Mark as Submitted" click (confirmButtonColor: '#0D5EA6')
    - Use BlockUI.show() before AJAX request, BlockUI.hide() after completion in both success and catch paths
    - Use vanilla fetch API with antiforgery token for POST to `/Vat/MarkAsSubmitted`
    - Display SweetAlert2 success/error messages after operation
    - Provide "Back to Periods" navigation link
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11_

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit with minimum 100 iterations
- Unit tests validate specific examples and edge cases
- The database tables already exist (migrations 017, 018) — no schema changes needed
- All repositories follow the GenericStoredProcedureRepository pattern with try/catch rethrow, full table names, and null-safe parameters
- UI follows MyChair Design System with SweetAlert2 for confirmations and BlockUI for AJAX loading states

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.5"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7"] },
    { "id": 4, "tasks": ["2.8"] },
    { "id": 5, "tasks": ["2.9", "2.10"] },
    { "id": 6, "tasks": ["4.1", "4.2"] },
    { "id": 7, "tasks": ["5.1", "5.2"] }
  ]
}
```
