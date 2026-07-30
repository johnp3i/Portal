# Tasks: Future-Dated Payment Handling (Bug Fix)

## Overview

Fix the system to exclude future-dated payments (`PaymentDateUtc > GETUTCDATE()`) from all "paid" calculations and mark them as "Upcoming" in the UI.

## Tasks

- [x] 1. PaymentRepository — Add date filter to paid-amount queries
  - [x] 1.1 Add `AND [PaymentDateUtc] <= GETUTCDATE()` to `GetTotalPaidAsync`
    - _Requirements: 1.1, 1.5_

  - [x] 1.2 Add `AND [PaymentDateUtc] <= GETUTCDATE()` to `GetValidPaymentsByInvoiceIdAsync`
    - _Requirements: 1.2, 3.1_

  - [x] 1.3 Add `AND [PaymentDateUtc] <= GETUTCDATE()` to `GetPaidInPeriodAsync`
    - _Requirements: 1.1_

- [x] 2. DashboardService — Add date filter to KPI queries
  - [x] 2.1 Add date filter to Outstanding Receivables sub-query (ValidPayments CTE)
    - _Requirements: 1.4_

  - [x] 2.2 Add date filter to Overdue Amount sub-query (ValidPayments CTE)
    - _Requirements: 1.4_

  - [x] 2.3 Add date filter to Paid This Month query
    - _Requirements: 1.3_

  - [x] 2.4 Add date filter to Partially Paid sub-query (ValidPayments CTE)
    - _Requirements: 1.4_

- [x] 3. PaymentHistoryDto — Add IsUpcoming flag
  - [x] 3.1 Add `bool IsUpcoming` property to `PaymentHistoryDto`
    - _Requirements: 2.1_

  - [x] 3.2 Map `IsUpcoming = p.PaymentDateUtc > DateTime.UtcNow` in `PaymentService.GetPaymentHistoryAsync`
    - _Requirements: 2.1_

- [x] 4. UI — Render Upcoming badge
  - [x] 4.1 Add amber "Upcoming" badge to invoice detail payment history table
    - _Requirements: 2.2_

  - [x] 4.2 Add amber "Upcoming" badge to Revenue Dashboard recent payments list
    - _Requirements: 2.3_

- [x] 5. Verification
  - [x] 5.1 Build passes with zero errors
    - _Requirements: 5.2_

  - [x] 5.2 Verify existing tests still pass
    - _Requirements: 5.2, 5.3_

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["5.1", "5.2"] }
  ]
}
```

## Notes

- Waves 0 and 1 are independent and can be done in parallel
- Wave 2–3 (DTO + mapping) must precede wave 4 (UI)
- `GetMonthlyTotalsAsync` is intentionally NOT changed — future payments appearing in their future month on the chart is correct
- No schema changes, no new migrations
- The `FinancialStatusEngine` requires no code changes — it consumes the output of `GetValidPaymentsByInvoiceIdAsync` which will now be correctly filtered
