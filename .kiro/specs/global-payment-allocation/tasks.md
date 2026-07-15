# Implementation Plan: Global Payment Allocation

## Overview

This plan adds global payment recording and automatic FIFO allocation to the Portal's revenue module. Users can record a customer-level payment that distributes across outstanding invoices. The implementation extends the existing Payment entity with parent-child linkage and adds a new allocation engine service.

## Tasks

- [x] 1. Database migration
  - [x] 1.1 Create migration `115_AddGlobalPaymentColumns.sql`
    - ALTER `[revenue].[Payment]`: add `ParentPaymentId` (INT NULL, FK self), `IsAutoAllocated` (BIT NOT NULL DEFAULT 0), `CustomerId` (INT NULL, FK Customer), `CreditAmount` (DECIMAL(18,2) NOT NULL DEFAULT 0)
    - ALTER `[revenue].[Payment].[InvoiceId]` to INT NULL
    - Add FK constraints, non-clustered indexes on ParentPaymentId and CustomerId
    - Idempotent (IF NOT EXISTS checks)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 2. Entity and model updates
  - [x] 2.1 Update `Payment.cs` entity
    - Add: ParentPaymentId (int?), IsAutoAllocated (bool), CustomerId (int?), CreditAmount (decimal)
    - Change InvoiceId to int? (nullable)
    - Change Invoice navigation to Invoice? (nullable)
    - Add navigation: ParentPayment, ChildAllocations (ICollection), Customer
    - _Requirements: 1.1–1.6, 14.1_

  - [x] 2.2 Update `PortalDbContext.cs` configuration
    - Configure self-referencing FK for ParentPaymentId
    - Configure Customer FK, update InvoiceId as optional
    - Update global query filter if needed
    - _Requirements: 1.1, 1.3, 1.4_

  - [x] 2.3 Create DTOs
    - `RecordGlobalPaymentDto`: CustomerId, Amount, PaymentDateUtc, PaymentMethodTypeId, Reference, Notes, AllocationMode, ManualAllocations (no RemainderMode — manual remainder always = credit)
    - `ManualAllocationItem`: InvoiceId, Amount
    - `AllocationResult`: Allocations list, CreditAmount, AllocatedCount, TotalAllocated
    - `AllocationDetail`: ChildPaymentId, InvoiceId, InvoiceNumber, AllocatedAmount, InvoiceOutstandingAfter
    - _Requirements: 3.2, 7.3, 12.1_

  - [x] 2.4 Update `PaymentRepository.cs`
    - Update InsertAsync to handle nullable InvoiceId (SqlDbType.Int with DBNull.Value) and new columns
    - Update ALL SELECT queries to include new columns (ParentPaymentId, IsAutoAllocated, CustomerId, CreditAmount)
    - Add `GetChildAllocationsAsync(int parentPaymentId, int businessId)`
    - Add `VoidChildrenAsync(int parentPaymentId)` — bulk void all children
    - Add `GetOutstandingInvoicesForCustomerAsync(int customerId, int businessId)` — FIFO-ordered with outstanding balance, WITH (UPDLOCK), excludes Paid/WrittenOff
    - _Requirements: 2.1, 2.2, 6.2, 8.1, 13.3, 14.4_

  - [x] 2.5 Update existing code for nullable InvoiceId
    - `PaymentService.VoidPaymentAsync`: add `if (payment.InvoiceId.HasValue)` guard before RecalculateStatusAsync and RevertPaymentMatchAsync
    - `PaymentService.RecordPaymentAsync`: unchanged (always non-null InvoiceId)
    - Any EF Core queries with `.Include(p => p.Invoice)`: add null checks
    - _Requirements: 14.1, 14.2, 14.3_

- [x] 3. Checkpoint — Verify entity changes compile
  - Run `dotnet build`

- [x] 4. Allocation engine
  - [x] 4.1 Create `IPaymentAllocationEngine` interface
    - `AllocateFifoAsync(int parentPaymentId, int customerId, decimal amount, int businessId, string userId)`
    - `AllocateManualAsync(int parentPaymentId, List<ManualAllocationItem> allocations, int businessId, string userId)`
    - _Requirements: 2.1, 3.1_

  - [x] 4.2 Create `PaymentAllocationEngine` implementation
    - FIFO: load outstanding invoices ordered by InvoiceDate ASC, Id ASC (using UPDLOCK); iterate and allocate min(remaining, outstanding); create child payments; recalculate status; track credit remainder
    - Manual: validate each allocation ≤ outstanding; create children; recalculate statuses; any remainder stored as credit on parent (no FIFO fallback in manual mode)
    - All within a SERIALIZABLE transaction — rollback on failure
    - Prevents race conditions: concurrent payments for the same customer will serialize, not overlap
    - Excludes invoices with InvoiceFinancialStatusTypeId IN (3, 5) — Paid and WrittenOff
    - _Requirements: 2.1–2.6, 3.1–3.6, 4.3, 5.1–5.4, 10.1–10.3, 13.1–13.3_

- [x] 5. Service layer updates
  - [x] 5.1 Add `RecordGlobalPaymentAsync` to `PaymentService`
    - Validate: amount > 0, customer exists and belongs to business, date not future
    - Check total outstanding for customer; if amount > outstanding, compute credit and warn (return warning for UI confirmation)
    - Insert parent payment (InvoiceId=NULL, CustomerId set)
    - Call allocation engine (FIFO or manual based on dto.AllocationMode)
    - Update parent CreditAmount if overpayment
    - Return result with allocation details
    - _Requirements: 4.1–4.5, 7.3, 12.1–12.6_

  - [x] 5.2 Add `VoidGlobalPaymentAsync` to `PaymentService`
    - Validate parent exists, belongs to business, not already voided
    - Load children, collect affected invoice IDs (only non-null InvoiceIds)
    - Void parent + all children (single transaction)
    - Zero CreditAmount
    - Recalculate status for each affected invoice (only where InvoiceId.HasValue)
    - Revert instalment matches for each child (only where InvoiceId.HasValue)
    - Note: parent payment has InvoiceId = NULL — do NOT call RecalculateStatusAsync on it
    - _Requirements: 8.1–8.5, 14.2, 14.3_

  - [x] 5.3 Add `GetOutstandingForCustomerAsync` to `PaymentService`
    - Returns list of outstanding invoices with balances for the manual allocation UI
    - _Requirements: 3.2, 7.2_

- [x] 6. Checkpoint — Verify service layer compiles
  - Run `dotnet build`

- [x] 7. Controller endpoints
  - [x] 7.1 Add `AxPostRecordGlobalPayment` to `RevenueController`
    - [HttpPost][ValidateAntiForgeryToken]
    - Calls RecordGlobalPaymentAsync
    - Returns Json with success, allocationCount, creditAmount, message
    - _Requirements: 7.1–7.6_

  - [x] 7.2 Add `AxGetOutstandingInvoicesForCustomer` to `RevenueController`
    - [HttpGet] accepts customerId
    - Returns invoice list with InvoiceNumber, InvoiceDate, TotalAmount, OutstandingBalance
    - _Requirements: 3.2_

  - [x] 7.3 Add `AxPostVoidGlobalPayment` to `RevenueController`
    - [HttpPost][ValidateAntiForgeryToken]
    - Calls VoidGlobalPaymentAsync
    - _Requirements: 8.1–8.5_

- [x] 8. Checkpoint — Verify controllers compile
  - Run `dotnet build`

- [x] 9. DI registration
  - [x] 9.1 Register IPaymentAllocationEngine / PaymentAllocationEngine as scoped

- [x] 10. UI: Statement page integration
  - [x] 10.1 Add "Record Payment" button to Statement view (visible when customer selected + statement generated)
  - [x] 10.2 Create global payment modal/form with: amount, date, method, reference, notes, allocation mode toggle
  - [x] 10.3 Manual mode: AJAX-load outstanding invoices, show amount input per row
  - [x] 10.4 Overpayment warning via SweetAlert2 before confirmation
  - [x] 10.5 BlockUI during save, refresh statement on success
  - _Requirements: 7.1–7.6, 4.1–4.2_

- [x] 11. UI: Invoice Detail updates
  - [x] 11.1 Update payment history display to show "Auto-allocated" badge for child payments
  - [x] 11.2 Show "from Payment [reference]" link to parent context
  - [x] 11.3 Prevent individual void on child allocations — show "Void the parent payment" message
  - _Requirements: 6.5, 8.4, 9.1–9.4_

- [x] 12. UI: Revenue Dashboard updates
  - [x] 12.1 Update openPaymentModalWithSelector to support global mode (customer selector instead of invoice selector)
  - _Requirements: 7.1_

- [x] 13. Checkpoint — Full integration test
  - Manual flow: record global payment → verify children created → verify statuses → void → verify reversed

- [x] 14. Property-based tests
  - [x]* 14.1 FIFO ordering determinism
  - [x]* 14.2 Sum of allocations + credit = parent amount
  - [x]* 14.3 No allocation exceeds invoice outstanding
  - [x]* 14.4 Void cascade completeness
  - [x]* 14.5 Tenant isolation
  - [x]* 14.6 Financial status consistency after allocation

- [x] 15. Final checkpoint
  - Run `dotnet test` and `dotnet build`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"], "description": "Database migration" },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5"], "description": "Entity and models" },
    { "id": 2, "tasks": ["3"], "description": "Checkpoint: compile" },
    { "id": 3, "tasks": ["4.1", "4.2"], "description": "Allocation engine" },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"], "description": "Service layer" },
    { "id": 5, "tasks": ["6"], "description": "Checkpoint: compile" },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "9.1"], "description": "Controller + DI" },
    { "id": 7, "tasks": ["8"], "description": "Checkpoint: compile" },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "11.1", "11.2", "11.3", "12.1"], "description": "UI layer" },
    { "id": 9, "tasks": ["13"], "description": "Checkpoint: integration" },
    { "id": 10, "tasks": ["14.1", "14.2", "14.3", "14.4", "14.5", "14.6"], "description": "Property tests" },
    { "id": 11, "tasks": ["15"], "description": "Final checkpoint" }
  ]
}
```
