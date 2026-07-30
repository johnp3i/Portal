# Requirements: Future-Dated Payment Handling

## Problem Statement

When a user records a payment with a future `PaymentDateUtc` (common with cheque payments), the system immediately counts it toward the invoice's "Paid" total. This incorrectly shifts the invoice financial status (e.g., from Unpaid to Paid), inflates KPI totals on the Revenue Dashboard, and misrepresents cash position.

## Requirements

### 1. Paid Amount Exclusion

- 1.1 THE system SHALL exclude payments where `PaymentDateUtc > GETUTCDATE()` from all "total paid" calculations.
- 1.2 THE `FinancialStatusEngine` SHALL NOT count future-dated payments when computing outstanding balance or determining financial status.
- 1.3 THE Revenue Dashboard KPI "Paid This Month" SHALL only include payments where `PaymentDateUtc <= GETUTCDATE()`.
- 1.4 THE Revenue Dashboard "Outstanding Receivables" calculation SHALL treat future-dated payments as not yet received.
- 1.5 THE invoice detail "Total Paid" and progress bar SHALL exclude future-dated payments.

### 2. UI Indication

- 2.1 WHEN a payment is displayed and its `PaymentDateUtc > DateTime.UtcNow`, the system SHALL visually mark it with an "Upcoming" badge (amber/gold colour).
- 2.2 THE "Upcoming" badge SHALL appear on the invoice detail payment history table.
- 2.3 THE "Upcoming" badge SHALL appear on the Revenue Dashboard recent payments list.
- 2.4 THE payment SHALL still be visible in all lists where it currently appears — it is not hidden, only marked.

### 3. Status Reconciliation

- 3.1 WHEN a future-dated payment's date arrives (i.e., `PaymentDateUtc <= GETUTCDATE()`), the next invocation of `RecalculateStatusAsync` for that invoice SHALL include the payment in its calculation.
- 3.2 THE system SHALL NOT require a separate background job for MVP — any user interaction that triggers recalculation (recording a payment, viewing invoice detail, voiding a payment) will naturally pick up matured payments.
- 3.3 THE monthly revenue chart (`GetMonthlyTotalsAsync`) SHALL continue to group payments by their `PaymentDateUtc` month — future payments appearing in their correct future month is acceptable and expected behaviour.

### 4. Recording Behaviour

- 4.1 THE system SHALL continue to allow recording payments with a future date (no blocking).
- 4.2 THE system SHALL NOT add any new columns or flags — the existing `PaymentDateUtc` field provides sufficient semantics.
- 4.3 THE global payment recording (`RecordGlobalPaymentAsync`) already rejects future dates — this behaviour SHALL remain unchanged.

### 5. Non-Functional

- 5.1 THE fix SHALL be implemented as query-level filters (`PaymentDateUtc <= GETUTCDATE()`) — no schema changes required.
- 5.2 THE fix SHALL not break any existing unit or property-based tests.
- 5.3 THE fix SHALL be backward-compatible — existing payments with past dates continue to function identically.
