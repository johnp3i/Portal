# Bugfix Requirements Document

## Introduction

When a customer account is credited and that credit is subsequently applied to pay an invoice, the Revenue Dashboard double-counts the credited amount in the "Paid This Month" KPI, the "Revenue Collected" chart, and the "Invoiced vs Collected" chart. This occurs because the dashboard queries sum ALL non-voided payment records without distinguishing between real money inflows (parent payments) and ledger-transfer child allocations that redistribute already-counted money to specific invoices.

The system uses a parent-child payment model:
- **Parent payment** (`ParentPaymentId = NULL`): Represents real money entering the business (cash, bank transfer, card, cheque).
- **Child allocation** (`ParentPaymentId != NULL`): A ledger transfer that applies part (or all) of a parent payment's amount to a specific invoice. No new money enters the business.

The dashboard must only count parent payments (real money inflows) to avoid double-counting.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a parent payment of amount X is recorded and subsequently allocated to one or more invoices (creating child payment records with `ParentPaymentId != NULL`) THEN the system counts both the parent amount X AND the child allocation amounts in the "Paid This Month" KPI, resulting in double-counted revenue.

1.2 WHEN child allocation payments exist within the current calendar month THEN the "Revenue Collected" monthly chart includes their amounts alongside the parent payment amounts, inflating the reported collected revenue.

1.3 WHEN child allocation payments exist within the last 12 months THEN the "Invoiced vs Collected" chart's `CollectedAmount` includes both parent and child payment amounts, overstating the collected totals per month.

### Expected Behavior (Correct)

2.1 WHEN a parent payment of amount X is recorded and subsequently allocated to invoices THEN the system SHALL only count the parent payment amount X in the "Paid This Month" KPI, excluding all child allocations (records where `ParentPaymentId IS NOT NULL`).

2.2 WHEN calculating the "Revenue Collected" monthly chart THEN the system SHALL only sum payment amounts where `ParentPaymentId IS NULL` (parent payments and legacy per-invoice payments), excluding child allocations from the totals.

2.3 WHEN calculating the "Invoiced vs Collected" chart's `CollectedAmount` THEN the system SHALL only sum payment amounts where `ParentPaymentId IS NULL`, excluding child allocations from the collected totals.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a payment is recorded directly against a single invoice without a parent-child relationship (`ParentPaymentId IS NULL` and `InvoiceId IS NOT NULL`) THEN the system SHALL CONTINUE TO include that payment in all dashboard KPIs and charts as before.

3.2 WHEN a payment is voided (`IsVoided = 1`) THEN the system SHALL CONTINUE TO exclude that payment from all dashboard KPIs and charts regardless of whether it is a parent or child payment.

3.3 WHEN a parent payment has a `CreditAmount > 0` (unallocated overpayment remainder) THEN the system SHALL CONTINUE TO count the full parent `Amount` in dashboard totals (the CreditAmount is part of the original payment, not a separate transaction).

3.4 WHEN Z-Report revenue data is present and the feature is enabled THEN the system SHALL CONTINUE TO add Z-Report totals to the "Revenue Collected" chart values as currently implemented.

3.5 WHEN calculating "Outstanding Receivables" and "Overdue Amount" KPIs THEN the system SHALL CONTINUE TO use all non-voided payments (including child allocations) for determining how much has been paid against each invoice, since those KPIs measure invoice settlement status, not cash inflow.

---

### Bug Condition (Formal)

```pascal
FUNCTION isBugCondition(payment)
  INPUT: payment of type Payment
  OUTPUT: boolean
  
  // Returns true when the payment is a child allocation (ledger transfer, not new money)
  RETURN payment.ParentPaymentId IS NOT NULL
END FUNCTION
```

### Fix Property

```pascal
// Property: Fix Checking — Child allocations excluded from revenue KPIs
FOR ALL payment WHERE isBugCondition(payment) DO
  result ← DashboardKpiQuery'(payments)
  ASSERT payment.Amount is NOT included in PaidThisMonth
  ASSERT payment.Amount is NOT included in RevenueCollected monthly totals
  ASSERT payment.Amount is NOT included in InvoicedVsCollected CollectedAmount
END FOR
```

### Preservation Property

```pascal
// Property: Preservation Checking — Parent and legacy payments still counted
FOR ALL payment WHERE NOT isBugCondition(payment) DO
  ASSERT F(payments) = F'(payments)
  // Dashboard KPIs produce identical results for non-child-allocation payment sets
END FOR
```
