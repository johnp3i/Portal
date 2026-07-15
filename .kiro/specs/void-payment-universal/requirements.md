# Requirements: Universal Void Payment

## Introduction

This feature adds a consistent "Void Payment" action across all three locations where payments are displayed: Revenue Dashboard (Recent Payments table), Invoice Detail (Payment History section), and Customer Statement (Transaction History). It also introduces the ability to void individual child allocations from global payments, returning the voided amount back to the parent's credit balance.

## Requirements

### Requirement 1: Void Button Placement

**User Story:** As a business user, I want to void any payment from wherever I see it, so that I don't have to navigate to a specific page to correct a mistake.

#### Acceptance Criteria

1. THE Revenue Dashboard "Recent Payments" table SHALL include a "Void" action button on each non-voided payment row.
2. THE Invoice Detail "Payment History" section SHALL include a "Void" action button on each non-voided payment row.
3. THE Statement "Transaction History" SHALL include a "Void" action button on each non-voided payment line.
4. VOIDED payments SHALL NOT show a Void button — they show a "Voided" badge instead.
5. THE Void button SHALL trigger a SweetAlert2 confirmation dialog before executing.

### Requirement 2: Void a Standalone Payment

**User Story:** As a business user, I want to void a regular per-invoice payment, reversing its effect on the invoice.

#### Acceptance Criteria

1. WHEN a standalone payment (ParentPaymentId = NULL, InvoiceId set) is voided, THE system SHALL set IsVoided = 1 on the payment.
2. THE system SHALL recalculate the financial status of the affected invoice.
3. THE system SHALL revert any instalment schedule match for the voided payment.
4. This is the existing behaviour — no change needed to the backend logic.

### Requirement 3: Void a Parent (Global) Payment — Cascade

**User Story:** As a business user, I want to void an entire global payment and have all its allocations reversed automatically.

#### Acceptance Criteria

1. WHEN a parent payment (InvoiceId = NULL, CustomerId set) is voided, ALL child allocations SHALL be voided automatically.
2. THE parent's CreditAmount SHALL be zeroed.
3. THE system SHALL recalculate financial status for every affected invoice.
4. THE confirmation dialog SHALL warn: "Voiding this payment will also reverse X allocation(s) across Y invoice(s). Continue?"
5. THIS is the existing VoidGlobalPaymentAsync behaviour.

### Requirement 4: Void a Child Allocation Individually

**User Story:** As a business user, I want to void a single allocation from a global payment, returning that amount to the parent's credit balance, so that I can correct a partial mis-allocation without voiding the entire payment.

#### Acceptance Criteria

1. WHEN a child allocation (ParentPaymentId != NULL) is voided individually, THE system SHALL set IsVoided = 1 on the child payment.
2. THE voided amount SHALL be returned to the parent payment's CreditAmount (parent.CreditAmount += child.Amount).
3. THE system SHALL recalculate the financial status of the affected invoice.
4. THE system SHALL revert any instalment schedule match for the voided child.
5. THE parent payment SHALL remain valid (not voided) — only the child is voided.
6. THE confirmation dialog SHALL inform: "This will reverse the €X allocation to INV-Y and return it as credit on the parent payment. Continue?"

### Requirement 5: Confirmation Dialogs

**User Story:** As a business user, I want clear confirmation before voiding, so I don't accidentally reverse a payment.

#### Acceptance Criteria

1. ALL void actions SHALL show a SweetAlert2 confirmation dialog with `confirmButtonColor: '#C24A4A'` (destructive action).
2. THE dialog text SHALL vary based on payment type:
   - Standalone: "Void this payment of €X? The invoice will be recalculated."
   - Parent: "Void this payment of €X? This will also reverse N allocation(s)."
   - Child: "Reverse this €X allocation to INV-Y? The amount will return as credit."
3. AFTER successful void, THE system SHALL show a success SweetAlert2 and refresh the relevant view.

### Requirement 6: Visual Indicators

**User Story:** As a business user, I want to clearly see which payments are active vs voided.

#### Acceptance Criteria

1. VOIDED payments SHALL display with a strikethrough or muted style (opacity: 0.5).
2. VOIDED payments SHALL show a "Voided" badge (red pill) instead of a Void button.
3. THE Transaction History, Payment History, and Recent Payments tables SHALL consistently apply this styling.
