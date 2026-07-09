# Implementation Plan: Payment Schedules (Instalment Plans)

## Overview

This plan implements the full Payment Schedules feature for the Portal's revenue module. The implementation proceeds bottom-up: database migrations → entities → DbContext → repositories → pure engines → service → controller → UI. Property-based tests target the pure computation engines (InstalmentStatusEngine and InstalmentMatchingEngine) where FsCheck provides the highest value.

## Tasks

- [x] 1. Database migrations and schema setup
  - [x] 1.1 Create migration 106_CreatePaymentScheduleInstalmentStatusTypeTable.sql
    - Create reference table `[revenue].[PaymentScheduleInstalmentStatusType]` with IF NOT EXISTS guard
    - Seed values: Pending (1), Due (2), Overdue (3), Paid (4), PartiallyPaid (5)
    - _Requirements: 10.3_

  - [x] 1.2 Create migration 107_CreatePaymentScheduleTable.sql
    - Create `[revenue].[PaymentSchedule]` with Id, BusinessId, InvoiceId, IsActive, CreatedAtUtc, CreatedByUserId
    - Add foreign keys to `[portal].[Business]` and `[invoice].[Invoice]`
    - Add unique filtered index `UX_PaymentSchedule_InvoiceId_Active` (WHERE IsActive = 1)
    - Add nonclustered index on BusinessId
    - _Requirements: 10.1, 10.2, 10.6, 10.7_

  - [x] 1.3 Create migration 108_CreatePaymentScheduleInstalmentTable.sql
    - Create `[revenue].[PaymentScheduleInstalment]` with Id, PaymentScheduleId, SequenceNumber, Amount, MatchedAmount, DueDate, PaymentId, ParentInstalmentId, IsRemainder, CreatedAtUtc
    - Add foreign keys to PaymentSchedule, Payment (nullable), and self-referencing ParentInstalmentId (nullable)
    - Add covering nonclustered index on PaymentScheduleId
    - _Requirements: 10.1, 10.5, 10.6_

  - [x] 1.4 Create migration 109_CreatePaymentScheduleHistoryTable.sql
    - Create `[revenue].[PaymentScheduleHistory]` with Id, PaymentScheduleId, FieldChanged, OldValue, NewValue, ChangedByUserId, ChangedAtUtc
    - Add foreign key to PaymentSchedule
    - Add nonclustered index on PaymentScheduleId with ChangedAtUtc include
    - _Requirements: 10.4, 10.6_

- [x] 2. Entity classes and DbContext configuration
  - [x] 2.1 Create entity classes in Portal.Infrastructure/Entities
    - Create `PaymentSchedule.cs` with navigation properties (Business, Invoice, Instalments, History)
    - Create `PaymentScheduleInstalment.cs` with navigation properties (PaymentSchedule, Payment, ParentInstalment)
    - Create `PaymentScheduleInstalmentStatusType.cs`
    - Create `PaymentScheduleHistory.cs` with navigation property (PaymentSchedule)
    - _Requirements: 10.1, 10.3, 10.4, 10.5_

  - [x] 2.2 Add DbSet properties and EF Core configuration to PortalDbContext
    - Add `DbSet<PaymentSchedule>`, `DbSet<PaymentScheduleInstalment>`, `DbSet<PaymentScheduleInstalmentStatusType>`, `DbSet<PaymentScheduleHistory>`
    - Configure table mappings to `[revenue]` schema
    - Configure relationships, default values (CreatedAtUtc, IsActive, MatchedAmount, IsRemainder)
    - Configure unique filtered index for single active schedule per invoice
    - _Requirements: 10.1, 10.2, 10.6_

- [x] 3. Repository layer
  - [x] 3.1 Create PaymentScheduleRepository
    - Extend `GenericStoredProcedureRepository<PaymentSchedule>`
    - Implement `InsertAsync`, `GetByInvoiceIdAsync`, `GetByIdAndBusinessIdAsync`, `DeleteAsync`
    - Use full table names in SQL queries, null-safe parameters, try/catch with rethrow
    - _Requirements: 1.5, 11.3_

  - [x] 3.2 Create PaymentScheduleInstalmentRepository
    - Extend `GenericStoredProcedureRepository<PaymentScheduleInstalment>`
    - Implement `InsertAsync`, `GetByScheduleIdAsync`, `UpdateMatchedAmountAsync`, `UpdateAmountAsync`, `UpdateDueDateAsync`, `DeleteAsync`, `DeleteByScheduleIdAsync`, `GetByIdAsync`
    - _Requirements: 1.5, 3.2, 3.4, 5.2, 5.3, 5.5_

  - [x] 3.3 Create PaymentScheduleHistoryRepository
    - Extend `GenericStoredProcedureRepository<PaymentScheduleHistory>`
    - Implement `InsertAsync`, `GetByScheduleIdAsync`
    - _Requirements: 5.1, 5.8_

- [x] 4. Pure computation engines
  - [x] 4.1 Implement InstalmentStatusEngine
    - Create `IInstalmentStatusEngine` interface with `DetermineStatus(DateOnly? dueDate, decimal instalmentAmount, decimal matchedTotal)` method
    - Create `InstalmentStatusEngine` implementation following priority rules: Paid(4) → PartiallyPaid(5) → Pending(1)/Due(2)/Overdue(3) based on date
    - Register in DI container
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [ ]* 4.2 Write property test for InstalmentStatusEngine
    - **Property 1: Instalment Status Determination**
    - Use FsCheck to generate arbitrary DateOnly?, decimal amount, decimal matchedTotal
    - Assert status follows priority rules for all input combinations
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.6, 2.7**

  - [x] 4.3 Implement InstalmentMatchingEngine
    - Create `IInstalmentMatchingEngine` interface with `AllocatePayment(decimal paymentAmount, List<InstalmentMatchCandidate> candidates)` method
    - Create `InstalmentMatchingEngine` implementation with priority: Due(2) → Overdue(3) → Pending(1), then SequenceNumber ASC
    - Create `InstalmentMatchCandidate`, `MatchResult`, `MatchAllocation`, `RemainderInstalment` models
    - Register in DI container
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 4.4 Write property test for InstalmentMatchingEngine
    - **Property 2: Payment Matching Correctness**
    - Use FsCheck to generate arbitrary payment amounts and candidate lists
    - Assert: sum of allocations + remainder == payment amount; each allocation ≤ remaining balance; priority order respected; remainder only on partial fill
    - **Validates: Requirements 3.1, 3.3, 3.4, 3.5**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Service layer implementation
  - [x] 6.1 Create VatWarningService
    - Implement VAT period lookup for invoice
    - Implement deadline comparison logic against first instalment due date
    - Return `VatWarningDto` with ShowWarning, HighlightVatAmount, TaxAmount, SubmissionDeadline, Message
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [ ]* 6.2 Write property test for VatWarningService logic
    - **Property 6: VAT Warning Logic**
    - Use FsCheck to generate arbitrary invoice tax amounts, due dates, and submission deadlines
    - Assert: warning shown only when due date > deadline; highlight only when amount < TaxAmount AND due date > deadline; no warning when no VAT period
    - **Validates: Requirements 7.2, 7.4, 7.5**

  - [x] 6.3 Create IPaymentScheduleService interface and PaymentScheduleService implementation
    - Implement `CreateScheduleAsync` — validate sum == outstanding balance, insert schedule + instalments in transaction, record history
    - Implement `UpdateInstalmentAsync` — validate not Paid, update amount/date, record history, revalidate sum
    - Implement `AddInstalmentAsync` — insert new instalment, record history, revalidate sum
    - Implement `RemoveInstalmentAsync` — validate no matched payment, delete instalment, record history, revalidate sum
    - Implement `DeleteScheduleAsync` — validate no matched payments on any instalment, confirm via ServiceResult, delete schedule + instalments, record history
    - Implement `GetScheduleByInvoiceIdAsync` — fetch schedule with instalments, compute statuses via InstalmentStatusEngine, build PaymentScheduleDetailDto with progress summary
    - Implement `GetScheduleHistoryAsync` — fetch history entries ordered by most recent first
    - Implement `GetVatWarningAsync` — delegate to VatWarningService
    - Implement `MatchPaymentToScheduleAsync` — fetch schedule, build candidates with computed statuses, call InstalmentMatchingEngine, update matched amounts, create remainder if needed, recalculate financial status
    - Implement `RevertPaymentMatchAsync` — find instalments matched to payment, reset matched amounts, delete remainder instalments created by that payment, recalculate financial status
    - Register in DI container
    - _Requirements: 1.1–1.6, 3.1–3.7, 4.1–4.4, 5.1–5.8, 6.1–6.4, 8.1–8.4, 11.1–11.4_

  - [ ]* 6.4 Write property test for schedule balance invariant
    - **Property 3: Schedule Balance Invariant**
    - Use FsCheck to generate arbitrary instalment configurations
    - Assert: sum of leaf instalment amounts == outstanding balance
    - **Validates: Requirements 1.4, 4.4, 5.6**

  - [ ]* 6.5 Write property test for progress summary correctness
    - **Property 7: Progress Summary Correctness**
    - Use FsCheck to generate arbitrary schedule states
    - Assert: TotalPaid + TotalRemaining == total schedule amount; CompletedCount == count of Paid instalments
    - **Validates: Requirements 8.3**

  - [ ]* 6.6 Write property test for deletion protection
    - **Property 8: Deletion Protection**
    - Use FsCheck to generate arbitrary schedules with varying MatchedAmount values
    - Assert: deletion blocked iff any instalment has MatchedAmount > 0
    - **Validates: Requirements 11.2**

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. DTOs and permission constant
  - [x] 8.1 Create request and response DTO classes
    - Create `CreatePaymentScheduleDto`, `CreateInstalmentDto`, `UpdateInstalmentDto`, `AddInstalmentDto` in Models folder
    - Create `PaymentScheduleDetailDto`, `InstalmentDetailDto`, `PaymentScheduleHistoryDto`, `VatWarningDto` in Models folder
    - _Requirements: 1.2, 8.2, 8.3_

  - [x] 8.2 Add SchedulePayments permission constant and ModuleControllerMap entry
    - Add `public const string SchedulePayments = "schedule_payments";` to `PortalModules.cs`
    - Include in the `All` array
    - Add entry to `ModuleControllerMap` for the Revenue controller schedule endpoints
    - _Requirements: 9.1, 9.2, 9.3_

- [x] 9. Controller endpoints
  - [x] 9.1 Add payment schedule endpoints to RevenueController
    - Implement `AxPostCreatePaymentSchedule` — permission check, call service, return JSON result
    - Implement `AxPostUpdateInstalment` — permission check, call service, return JSON result
    - Implement `AxPostAddInstalment` — permission check, call service, return JSON result
    - Implement `AxPostRemoveInstalment` — permission check, call service, return JSON result
    - Implement `AxPostDeletePaymentSchedule` — permission check, call service, return JSON result
    - Implement `AxGetPaymentSchedule` — call service, return schedule detail JSON
    - Implement `AxGetScheduleHistory` — call service, return history JSON
    - Implement `AxGetVatWarning` — call service, return VAT warning JSON
    - All methods use try/catch with `(Exception ex)`, return `Json(new { success, message })`
    - _Requirements: 1.1, 5.1, 8.1, 9.1, 9.2, 9.3, 11.1_

- [x] 10. Integration with existing PaymentService
  - [x] 10.1 Modify PaymentService to trigger instalment matching on payment record
    - After inserting payment in `RecordPaymentAsync`, check for active schedule on invoice
    - If active schedule exists, call `PaymentScheduleService.MatchPaymentToScheduleAsync`
    - _Requirements: 3.1, 3.7_

  - [x] 10.2 Modify PaymentService to revert instalment match on payment void
    - In `VoidPaymentAsync`, call `PaymentScheduleService.RevertPaymentMatchAsync` before financial status recalculation
    - _Requirements: 6.4_

  - [ ]* 10.3 Write property test for invoice financial status derivation
    - **Property 4: Invoice Financial Status Derivation**
    - Use FsCheck to generate arbitrary instalment states (all Paid, mixed, none paid)
    - Assert: correct InvoiceFinancialStatusTypeId derived for each scenario
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4**

- [x] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Partial view and JavaScript module
  - [x] 12.1 Create _PaymentScheduleSection.cshtml partial view
    - Render progress summary bar (total paid / total / completion fraction)
    - Render instalment table with columns: #, Amount, Due Date, Status (colour-coded badge), Actions
    - Render create form (dynamic instalment rows with amount + optional due date)
    - Render edit controls (inline amount/date modification) visible only with `schedule_payments` permission
    - Render delete button with SweetAlert2 confirmation dialog
    - Render history accordion showing modification log (most recent first)
    - Show read-only view for users without `schedule_payments` permission
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 9.4, 9.5_

  - [x] 12.2 Create payment-schedule.js JavaScript module
    - Implement dynamic instalment row addition/removal in create form
    - Implement real-time balance validation (sum of instalments vs outstanding balance)
    - Implement VAT warning fetch on first instalment date change (AxGetVatWarning)
    - Implement create schedule AJAX call: BlockUI.show() → fetch AxPostCreatePaymentSchedule → BlockUI.hide() → Swal.fire()
    - Implement update instalment AJAX call with same BlockUI + Swal pattern
    - Implement add instalment AJAX call with same pattern
    - Implement remove instalment AJAX call with same pattern
    - Implement delete schedule AJAX call: Swal.fire confirmation → BlockUI.show() → fetch AxPostDeletePaymentSchedule → BlockUI.hide() → Swal.fire result
    - Implement load schedule (AxGetPaymentSchedule) and render to DOM
    - Implement load history (AxGetScheduleHistory) and render to accordion
    - _Requirements: 1.2, 1.3, 1.6, 4.1, 5.8, 7.2, 7.3, 11.1_

- [x] 13. Integration into invoice detail pages
  - [x] 13.1 Integrate _PaymentScheduleSection into Revenue/InvoiceDetail.cshtml
    - Add partial view reference in the appropriate section of the page
    - Pass invoice ID and permission flag to partial
    - _Requirements: 8.1_

  - [x] 13.2 Integrate _PaymentScheduleSection into Invoice/Detail.cshtml
    - Add partial view reference in the appropriate section of the page
    - Pass invoice ID and permission flag to partial
    - _Requirements: 8.1_

- [ ] 14. Unit and integration tests
  - [ ]* 14.1 Write unit tests for PaymentScheduleService
    - Test CreateScheduleAsync — valid creation, sum mismatch rejection, duplicate schedule rejection
    - Test UpdateInstalmentAsync — valid update, paid instalment rejection, sum revalidation
    - Test DeleteScheduleAsync — valid deletion, matched payment rejection
    - Test MatchPaymentToScheduleAsync — single match, overflow match, partial match with remainder
    - Test RevertPaymentMatchAsync — matched amounts reset, remainder removed
    - _Requirements: 1.4, 1.5, 1.6, 5.2, 5.6, 11.2, 11.3_

  - [ ]* 14.2 Write unit tests for VatWarningService
    - Test warning shown when due date > deadline
    - Test highlight when amount < TaxAmount and due date > deadline
    - Test no warning when no VAT period assigned
    - _Requirements: 7.2, 7.4, 7.5_

  - [ ]* 14.3 Write property test for modification history completeness
    - **Property 5: Modification History Completeness**
    - Use FsCheck to generate arbitrary modification operations
    - Assert: every modification produces a history entry with non-null FieldChanged, ChangedByUserId, ChangedAtUtc
    - **Validates: Requirements 5.1, 11.4**

  - [ ]* 14.4 Write integration tests for controller endpoints
    - Test AxPostCreatePaymentSchedule — success and validation error responses
    - Test AxGetPaymentSchedule — returns schedule with computed statuses
    - Test AxPostDeletePaymentSchedule — deletion protection enforcement
    - Test permission enforcement — unauthorized users receive appropriate response
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck
- Unit tests validate specific examples and edge cases
- The design uses C# throughout — all implementations use ASP.NET Core 8, EF Core, and Portal conventions
- Pure engines (InstalmentStatusEngine, InstalmentMatchingEngine) have no I/O — ideal for property-based testing
- Status is computed at read time, not stored — the InstalmentStatusEngine is called when building DTOs
- SQL migrations use `USE [Portal]` + `IF NOT EXISTS` guards per project convention
- All AJAX calls follow BlockUI.show() → fetch → BlockUI.hide() → Swal.fire() pattern
- JavaScript uses vanilla fetch API (no jQuery for AJAX) per project standards

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2"] },
    { "id": 4, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 5, "tasks": ["4.1", "4.3", "8.1", "8.2"] },
    { "id": 6, "tasks": ["4.2", "4.4", "6.1"] },
    { "id": 7, "tasks": ["6.2", "6.3"] },
    { "id": 8, "tasks": ["6.4", "6.5", "6.6", "9.1"] },
    { "id": 9, "tasks": ["10.1", "10.2"] },
    { "id": 10, "tasks": ["10.3", "12.1", "12.2"] },
    { "id": 11, "tasks": ["13.1", "13.2"] },
    { "id": 12, "tasks": ["14.1", "14.2", "14.3", "14.4"] }
  ]
}
```
