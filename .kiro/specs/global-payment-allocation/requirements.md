# Requirements Document

## Introduction

This feature introduces Global Payment Allocation — the ability to record a customer-level payment that is automatically distributed across multiple outstanding invoices. Currently, payments can only be recorded against a specific invoice from the Invoice Detail page. This is limiting when a customer sends a lump payment covering multiple invoices.

The new workflow allows the user to record a payment from the Customer Statement view (or any customer context), specifying just the amount and payment method. The system then allocates the amount across outstanding invoices using a FIFO strategy (oldest first), or the user can manually select which invoices to apply the payment to.

Each allocation creates a separate child payment record linked to the original parent payment, ensuring clear auditability and correct per-invoice financial status tracking.

## Glossary

- **Global_Payment**: A customer-level payment recorded without targeting a specific invoice. Stored as a parent payment with `InvoiceId = NULL` and `CustomerId` set.
- **Parent_Payment**: The original payment record representing the actual money received. Has no InvoiceId. All child allocations reference this via `ParentPaymentId`.
- **Child_Payment (Allocation)**: A payment record created by the allocation engine, linked to a specific invoice via `InvoiceId` and back to the parent via `ParentPaymentId`. Marked as `IsAutoAllocated = true`.
- **FIFO_Allocation**: Default allocation strategy — apply payment to the oldest outstanding invoice first, then the next oldest, until the payment amount is exhausted.
- **Manual_Allocation**: User-directed allocation — the user selects which invoices receive what portion of the payment.
- **Overpayment**: When the payment amount exceeds the total outstanding across all invoices for the customer. The excess is held as a credit balance.
- **Credit_Balance**: The portion of a global payment that could not be allocated to any invoice because all invoices are fully paid. Shown as a positive credit on the customer's account.

## Requirements

### Requirement 1: Payment Entity Schema Changes

**User Story:** As a platform developer, I want the Payment entity to support parent-child relationships and customer-level recording, so that global payments can be tracked with full allocation lineage.

#### Acceptance Criteria

1. THE `[revenue].[Payment]` table SHALL include a new column `ParentPaymentId` (INT, NULL, FK to self `[revenue].[Payment].[Id]`) — links child allocations to their parent.
2. THE `[revenue].[Payment]` table SHALL include a new column `IsAutoAllocated` (BIT, NOT NULL, DEFAULT 0) — distinguishes system-created allocations from user-recorded payments.
3. THE `[revenue].[Payment]` table SHALL include a new column `CustomerId` (INT, NULL, FK to `[customer].[Customer].[Id]`) — for parent payments that are not tied to a specific invoice.
4. THE existing `InvoiceId` column SHALL be changed to nullable (INT, NULL) — parent payments have no invoice, child allocations have InvoiceId set.
5. THE `[revenue].[Payment]` table SHALL include a new column `CreditAmount` (DECIMAL(18,2), NOT NULL, DEFAULT 0) — holds any unallocated remainder from an overpayment.
6. Existing payment records SHALL NOT be affected — they retain their current InvoiceId values and have `ParentPaymentId = NULL`, `IsAutoAllocated = 0`, `CustomerId = NULL`, `CreditAmount = 0`.

### Requirement 2: FIFO Allocation Strategy

**User Story:** As a business user, I want the system to automatically distribute a global payment across my customer's outstanding invoices starting from the oldest, so that invoices are settled in chronological order.

#### Acceptance Criteria

1. WHEN a global payment is recorded without manual invoice selection, THE system SHALL allocate using FIFO: oldest outstanding invoice first (ordered by InvoiceDate ASC, then Id ASC).
2. THE system SHALL only consider invoices that are: issued (InvoiceStatusTypeId = 2), not fully paid (InvoiceFinancialStatusTypeId NOT IN (3, 5) — excludes Paid and WrittenOff), belong to the same customer and business, and are not deleted.
3. FOR each invoice in FIFO order, THE system SHALL allocate the minimum of: remaining payment amount OR invoice outstanding balance.
4. WHEN an allocation fully covers an invoice's outstanding balance, THE system SHALL move to the next invoice with any remaining amount.
5. WHEN the payment amount is exhausted, THE allocation SHALL stop (remaining invoices stay at their current status).
6. FOR each allocation, THE system SHALL create a child Payment record with: `InvoiceId` = the target invoice, `ParentPaymentId` = the parent payment, `IsAutoAllocated = true`, `Amount` = allocated portion.

### Requirement 3: Manual Invoice Selection

**User Story:** As a business user, I want to optionally choose which invoices a global payment applies to and in what amounts, so that I can override FIFO when I know the customer's intent.

#### Acceptance Criteria

1. THE Global Payment form SHALL provide an option to manually select invoices.
2. WHEN manual allocation is chosen, THE form SHALL display all outstanding invoices for the selected customer with their outstanding balances.
3. THE user SHALL be able to enter a specific amount per invoice (up to that invoice's outstanding balance).
4. THE sum of manual allocations SHALL NOT exceed the total payment amount.
5. IF the sum of manual allocations is less than the total payment amount, THE remainder SHALL be stored as credit on the parent payment. Manual mode means the user is being explicit — any unallocated amount is treated as credit.
6. FOR each manual allocation, THE system SHALL create a child Payment record with `IsAutoAllocated = false` (user-directed), plus `ParentPaymentId` pointing to the parent.

### Requirement 4: Overpayment and Credit Balance

**User Story:** As a business user, I want to be notified when a payment exceeds the total outstanding and have the excess held as a credit, so that I can track customer overpayments.

#### Acceptance Criteria

1. WHEN the payment amount exceeds the total outstanding balance for the customer, THE system SHALL notify the user before confirming: "This payment exceeds the total outstanding by X. The excess will be recorded as a credit."
2. THE user SHALL confirm to proceed or cancel.
3. WHEN confirmed, THE system SHALL fully allocate to all outstanding invoices and store the remainder in the parent payment's `CreditAmount` field.
4. THE Customer Statement SHALL display the credit balance for the customer.
5. THE credit amount SHALL NOT be automatically applied to future invoices — it is informational only (future enhancement: credit application workflow).

### Requirement 5: Financial Status Auto-Update

**User Story:** As a business user, I want each invoice's financial status to update automatically as allocations are applied, so that I always see accurate payment progress.

#### Acceptance Criteria

1. AFTER each child allocation is created, THE system SHALL trigger `FinancialStatusEngine.RecalculateStatusAsync` for the affected invoice.
2. WHEN an allocation fully covers an invoice's outstanding balance, THE invoice status SHALL transition to Paid (3).
3. WHEN an allocation partially covers an invoice's outstanding balance, THE invoice status SHALL transition to Partially Paid (2) or remain Overdue (4) if past due date.
4. THE financial status recalculation SHALL consider ALL non-voided payments for the invoice (both manual and auto-allocated).

### Requirement 6: Payment Audit Trail and Lineage

**User Story:** As a business user, I want to trace each allocation back to the original payment, so that I can understand how money was distributed.

#### Acceptance Criteria

1. EVERY child payment SHALL have `ParentPaymentId` set to the parent payment's Id.
2. FROM the parent payment, THE system SHALL be able to retrieve all child allocations via `ParentPaymentId = parent.Id`.
3. THE parent payment SHALL store: total amount received, CustomerId, payment date, method, reference, notes, and CreditAmount (if any).
4. EACH child payment SHALL store: allocated amount, InvoiceId, ParentPaymentId, IsAutoAllocated flag, and inherit PaymentDateUtc and PaymentMethodTypeId from the parent.
5. THE Invoice Detail payment history SHALL clearly distinguish auto-allocated payments (show "Auto-allocated from payment [reference]" label) from direct payments.
6. THE Customer Statement SHALL show parent payments as the primary transaction line, with child allocations available as expandable detail.

### Requirement 7: Statement View Integration

**User Story:** As a business user, I want to record global payments directly from the Customer Statement page, so that I can manage payments in the context of the customer's full account.

#### Acceptance Criteria

1. THE Statement page SHALL include a "Record Payment" button (visible when a customer is selected and a statement is generated).
2. THE payment form SHALL pre-fill the customer and show their total outstanding balance.
3. THE form SHALL include: Amount (required), Payment Date (required, defaults to today), Payment Method (dropdown), Reference (optional), Notes (optional).
4. THE form SHALL include an allocation mode toggle: "Auto (FIFO)" or "Manual".
5. WHEN Manual is selected, THE form SHALL display outstanding invoices with amount input per invoice.
6. AFTER successful recording, THE Statement SHALL refresh to show the new payment and updated balances.

### Requirement 8: Voiding a Global Payment

**User Story:** As a business user, I want to void a global payment and have all its allocations reversed, so that I can correct mistakes.

#### Acceptance Criteria

1. WHEN a parent payment is voided, ALL child allocations (where ParentPaymentId = parent.Id) SHALL also be voided automatically.
2. THE system SHALL trigger `FinancialStatusEngine.RecalculateStatusAsync` for each affected invoice.
3. THE CreditAmount on the parent SHALL be zeroed.
4. THE user SHALL NOT be able to void individual child allocations independently — only the parent can be voided (which cascades).
5. THE void action SHALL show a SweetAlert2 confirmation warning: "Voiding this payment will also reverse X allocation(s) across Y invoice(s)."

### Requirement 9: Existing Per-Invoice Payment (Preserved)

**User Story:** As a business user, I want the existing per-invoice payment recording to continue working unchanged, so that I can still record payments directly against a single invoice when appropriate.

#### Acceptance Criteria

1. THE existing RecordPayment flow on Invoice Detail SHALL continue to work as before.
2. Per-invoice payments SHALL have: `InvoiceId` set, `ParentPaymentId = NULL`, `IsAutoAllocated = false`, `CustomerId = NULL`, `CreditAmount = 0`.
3. THE system SHALL NOT create parent-child relationships for per-invoice payments — they remain standalone records.
4. THE financial status engine SHALL treat per-invoice payments identically to child allocations when computing outstanding balance.

### Requirement 10: Instalment Schedule Integration

**User Story:** As a business user, I want global payment allocations to respect instalment schedules when they exist, so that payments are correctly matched to scheduled instalments.

#### Acceptance Criteria

1. WHEN a child allocation is created for an invoice that has an active payment schedule, THE system SHALL call `PaymentScheduleService.MatchPaymentToScheduleAsync` for the child payment.
2. THE matching logic SHALL follow the existing instalment matching rules (priority: Due → Overdue → Pending).
3. IF no instalment schedule exists for the invoice, THE child allocation SHALL be recorded without schedule matching (normal flow).

### Requirement 11: Tenant Isolation

**User Story:** As a business user, I want global payments scoped to my business, so that my financial data remains private.

#### Acceptance Criteria

1. ALL global payment operations SHALL filter by the authenticated user's BusinessId.
2. THE allocation engine SHALL only consider invoices belonging to the same business.
3. THE customer dropdown and invoice lists SHALL only show records from the current business.
4. THE void operation SHALL verify business ownership before cascading.

### Requirement 12: Validation Rules

**User Story:** As a business user, I want the system to prevent invalid payment recordings, so that my financial data stays accurate.

#### Acceptance Criteria

1. THE payment amount SHALL be greater than zero.
2. THE customer SHALL have at least one outstanding invoice (unless the user explicitly confirms a credit-only payment).
3. THE payment date SHALL NOT be in the future.
4. IN manual allocation mode, no individual allocation SHALL exceed the invoice's outstanding balance.
5. IN manual allocation mode, the sum of allocations SHALL NOT exceed the payment amount.
6. THE system SHALL prevent recording a global payment for a customer that belongs to a different business.

### Requirement 13: Concurrency Safety

**User Story:** As a platform developer, I want concurrent global payment recordings for the same customer to be safe, so that no invoice can be overpaid due to a race condition.

#### Acceptance Criteria

1. THE allocation engine SHALL execute within a serializable database transaction to prevent concurrent reads from seeing stale outstanding balances.
2. IF two global payments are recorded simultaneously for the same customer, THE second transaction SHALL wait for the first to complete (or fail with a concurrency error) rather than producing duplicate allocations.
3. THE outstanding balance calculation SHALL use row-level locking (UPDLOCK or equivalent) when reading invoice data during allocation.

### Requirement 14: Nullable InvoiceId Backward Compatibility

**User Story:** As a platform developer, I want the InvoiceId nullability change to not break existing payment flows.

#### Acceptance Criteria

1. ALL existing code that accesses `Payment.InvoiceId` SHALL be updated to handle the nullable (`int?`) type safely.
2. THE `VoidPaymentAsync` method SHALL check `if (payment.InvoiceId.HasValue)` before calling `RecalculateStatusAsync` and `RevertPaymentMatchAsync`.
3. THE `FinancialStatusEngine.RecalculateStatusAsync` SHALL only be called with a valid (non-null) invoiceId.
4. ALL repository SELECT queries for the Payment entity SHALL include the new columns (`ParentPaymentId`, `IsAutoAllocated`, `CustomerId`, `CreditAmount`) to prevent EF Core mapping errors.
5. THE global query filter on Payment (if any) SHALL continue to work with parent payments that have no InvoiceId.

### Requirement 15: Credit Balance Application

**User Story:** As a business user, I want to apply existing credit to outstanding invoices, so that overpayments are used before I record new money.

#### Acceptance Criteria

1. THE Customer Statement SHALL display any credit balance from global payments prominently.
2. THE payment modal SHALL show available credit and an "Apply Credit" button when credit > 0 and outstanding invoices exist.
3. WHEN "Apply Credit" is clicked, THE system SHALL allocate credit to outstanding invoices using FIFO.
4. THE parent payment's CreditAmount SHALL be reduced by the applied amount.
5. THE system SHALL recalculate financial status for each affected invoice.
