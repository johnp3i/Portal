# Credit Payment Double-Count Fix — Bugfix Design

## Overview

The Revenue Dashboard double-counts payments when a credit balance is applied to an invoice. The system uses a parent-child payment model where parent payments represent real money inflows and child allocations (`ParentPaymentId IS NOT NULL`) are ledger transfers that redistribute already-counted money to specific invoices. Seven revenue-aggregation queries incorrectly include child allocations in revenue totals. The fix adds a `ParentPaymentId IS NULL` filter to each affected query (SQL WHERE clause or LINQ `.Where()`).

## Glossary

- **Bug_Condition (C)**: A payment record where `ParentPaymentId IS NOT NULL` — a child allocation (ledger transfer, not new money) being counted in revenue KPIs
- **Property (P)**: Revenue KPIs shall only sum parent payments (`ParentPaymentId IS NULL`) — records representing real cash inflow
- **Preservation**: Outstanding Receivables, Overdue Amount, and Partially Paid KPIs must continue using ALL payments (including child allocations) to correctly calculate invoice settlement status
- **Parent Payment**: A payment with `ParentPaymentId = NULL` representing real money entering the business (cash, bank transfer, card, cheque)
- **Child Allocation**: A payment with `ParentPaymentId != NULL` representing a ledger transfer that applies part of a parent payment to a specific invoice
- **`DashboardService.GetKpiDataAsync`**: The method in `Portal.Infrastructure/Services/DashboardService.cs` that calculates all KPI tiles including "Paid This Month"
- **`PaymentRepository.GetMonthlyTotalsAsync`**: The method in `Portal.Infrastructure/Repositories/PaymentRepository.cs` that returns monthly payment sums for the "Revenue Collected" chart
- **`DashboardService.GetInvoicedVsCollectedAsync`**: The method in `Portal.Infrastructure/Services/DashboardService.cs` that calculates the "Invoiced vs Collected" comparison chart
- **`DashboardService.GetRevenueVsExpensesAsync`**: The method in `Portal.Infrastructure/Services/DashboardService.cs` (line ~720) that calculates the "Revenue vs Expenses" chart on the Home Dashboard
- **`PnlService.ComputeRevenueAsync`**: The method in `Portal.Infrastructure/Services/PnlService.cs` (line ~124) that computes the revenue figure for the Profit & Loss report using LINQ
- **`DashboardService.GetCollectionRateAsync`**: The method in `Portal.Infrastructure/Services/DashboardService.cs` (line ~406) that calculates the collection rate percentage
- **`PaymentRepository.GetPaidInPeriodAsync`**: The method in `Portal.Infrastructure/Repositories/PaymentRepository.cs` (line ~241) that sums all non-voided payments in a given date range (generic reusable helper)

## Bug Details

### Bug Condition

The bug manifests when a parent payment is recorded and subsequently allocated to invoices (creating child allocation records). The dashboard revenue queries sum ALL non-voided payments without filtering out child allocations, causing the same money to be counted twice: once as the parent payment and again as the child allocation(s).

**Formal Specification:**
```
FUNCTION isBugCondition(payment)
  INPUT: payment of type Payment
  OUTPUT: boolean
  
  RETURN payment.ParentPaymentId IS NOT NULL
         AND payment.IsVoided = 0
         AND payment IS included in revenue KPI/chart calculation
END FUNCTION
```

### Examples

- **Example 1**: Customer pays EUR 500 (parent payment created). EUR 300 is allocated to Invoice A, EUR 200 to Invoice B (two child allocations created). "Paid This Month" shows EUR 1,000 instead of EUR 500.
- **Example 2**: Customer has EUR 200 credit balance from a previous overpayment. Credit is applied to a new invoice (child allocation created). "Revenue Collected" chart inflates the month's total by EUR 200.
- **Example 3**: A single legacy payment of EUR 100 recorded directly against an invoice (`ParentPaymentId = NULL`, `InvoiceId != NULL`) — correctly counted once. No double-counting occurs.
- **Edge Case**: Parent payment of EUR 500 where EUR 400 is allocated and EUR 100 remains as credit. Only EUR 500 (the parent) should appear in revenue totals.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Outstanding Receivables KPI must continue to use ALL non-voided payments (including child allocations) to compute how much has been paid against each invoice
- Overdue Amount KPI must continue to use ALL non-voided payments (including child allocations) to determine which invoices have remaining balances past due date
- Partially Paid KPI must continue to use ALL non-voided payments (including child allocations) to calculate partial payment amounts
- Voided payment exclusion (`IsVoided = 0`) must continue to apply across all queries
- Z-Report revenue additions to the "Revenue Collected" and "Invoiced vs Collected" charts must remain unchanged
- Legacy direct payments (`ParentPaymentId IS NULL`, `InvoiceId IS NOT NULL`) must continue to be counted in revenue KPIs

**Scope:**
All queries that calculate invoice settlement status (Outstanding Receivables, Overdue Amount, Partially Paid) are explicitly UNAFFECTED by this fix. They correctly need child allocations to determine how much has been paid toward each invoice. Only revenue inflow aggregations (Paid This Month, Revenue Collected, Invoiced vs Collected) require the parent-only filter.

## Hypothesized Root Cause

The root cause is confirmed (not hypothesized) — seven revenue-aggregation queries lack a filter for `ParentPaymentId IS NULL`:

1. **Paid This Month KPI** (`DashboardService.cs`, line ~160): The `paidThisMonthQuery` sums all non-voided payments within the current month without checking `ParentPaymentId`. When a credit allocation occurs in the same month as its parent payment, both amounts are summed.

2. **Revenue Collected Monthly Totals** (`PaymentRepository.cs`, `GetMonthlyTotalsAsync`): The query groups payments by year/month and sums amounts, but includes child allocations in the aggregation.

3. **Invoiced vs Collected Chart** (`DashboardService.cs`, `GetInvoicedVsCollectedAsync`): The `CollectedData` subquery and the `Months` UNION subquery for payments both include child allocations, inflating the collected amounts.

4. **Revenue vs Expenses Chart** (`DashboardService.cs`, `GetRevenueVsExpensesAsync`, line ~720): The revenue query sums ALL non-voided payments per month without excluding child allocations, inflating the revenue side of the comparison.

5. **P&L Revenue** (`PnlService.cs`, `ComputeRevenueAsync`, line ~124): Uses LINQ `_dbContext.Payments.Where(p => !p.IsVoided).SumAsync(p => p.Amount)` without filtering out child allocations, inflating the Profit & Loss revenue figure.

6. **Collection Rate** (`DashboardService.cs`, `GetCollectionRateAsync`, line ~406): The `CollectedWithin30` subquery sums ALL non-voided payments within 30 days of invoice date. Child allocations inflate the numerator, producing an artificially high collection rate percentage.

7. **Paid In Period Helper** (`PaymentRepository.cs`, `GetPaidInPeriodAsync`, line ~241): Generic helper that sums all non-voided payments in a date range. Currently not called from the dashboard but is a reusable utility that any future caller would expect to return actual cash inflow, not inflated figures.

The Outstanding Receivables, Overdue Amount, and Partially Paid queries are NOT affected because they use `ValidPayments` subqueries that correctly sum ALL payments against each invoice to determine settlement status — this is correct behavior for those KPIs.

## Correctness Properties

Property 1: Bug Condition - Child Allocations Excluded From Revenue KPIs

_For any_ payment where `ParentPaymentId IS NOT NULL` (child allocation / ledger transfer), the fixed dashboard queries SHALL NOT include that payment's amount in the "Paid This Month" KPI, the "Revenue Collected" monthly chart totals, the "Invoiced vs Collected" collected amounts, the "Revenue vs Expenses" chart revenue figures, the P&L revenue figure, the collection rate calculation, or the "Paid In Period" helper result.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7**

Property 2: Preservation - Invoice Settlement Queries Unchanged

_For any_ payment set (including child allocations), the fixed code SHALL produce identical results for Outstanding Receivables, Overdue Amount, and Partially Paid KPIs, preserving the correct invoice settlement calculations that require ALL non-voided payments.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

**File 1**: `Portal.Infrastructure/Services/DashboardService.cs`

**Query**: `paidThisMonthQuery` (inside `GetKpiDataAsync`)

**Change**: Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause.

```sql
-- Before:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[PaymentDateUtc] >= @MonthStart
  AND [revenue].[Payment].[PaymentDateUtc] < @MonthEnd

-- After:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @MonthStart
  AND [revenue].[Payment].[PaymentDateUtc] < @MonthEnd
```

---

**File 2**: `Portal.Infrastructure/Repositories/PaymentRepository.cs`

**Method**: `GetMonthlyTotalsAsync`

**Change**: Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause.

```sql
-- Before:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc

-- After:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
```

---

**File 3**: `Portal.Infrastructure/Services/DashboardService.cs`

**Query**: Inside `GetInvoicedVsCollectedAsync` — TWO subqueries reference `[revenue].[Payment]`:

1. The `Months` UNION subquery (provides the month axis from payment dates)
2. The `CollectedData` LEFT JOIN subquery (sums collected amounts per month)

**Change**: Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to BOTH payment subqueries.

```sql
-- Months UNION subquery — After:
SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
       MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month]
FROM [revenue].[Payment]
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromDateUtc

-- CollectedData subquery — After:
SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
       MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month],
       SUM([revenue].[Payment].[Amount]) AS [CollectedAmount]
FROM [revenue].[Payment]
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromDateUtc
GROUP BY YEAR([revenue].[Payment].[PaymentDateUtc]), MONTH([revenue].[Payment].[PaymentDateUtc])
```

---

**File 4**: `Portal.Infrastructure/Services/DashboardService.cs`

**Method**: `GetRevenueVsExpensesAsync` (line ~720)

**Change**: The revenue query sums ALL non-voided payments per month. Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause.

```sql
-- Before:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc

-- After:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
```

---

**File 5**: `Portal.Infrastructure/Services/PnlService.cs`

**Method**: `ComputeRevenueAsync` (line ~124)

**Change**: The LINQ query sums all non-voided payments without filtering out child allocations. Add `.Where(p => p.ParentPaymentId == null)` to the LINQ chain.

```csharp
// Before:
_dbContext.Payments.Where(p => !p.IsVoided).SumAsync(p => p.Amount)

// After:
_dbContext.Payments
    .Where(p => !p.IsVoided)
    .Where(p => p.ParentPaymentId == null)
    .SumAsync(p => p.Amount)
```

---

**File 6**: `Portal.Infrastructure/Services/DashboardService.cs`

**Method**: `GetCollectionRateAsync` (line ~406)

**Change**: The `CollectedWithin30` subquery sums ALL non-voided payments within 30 days of invoice date. Child allocations inflate the collection rate. Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the `CollectedWithin30` subquery WHERE clause.

```sql
-- CollectedWithin30 subquery — Before:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND DATEDIFF(DAY, [revenue].[Invoice].[InvoiceDateUtc], [revenue].[Payment].[PaymentDateUtc]) <= 30

-- CollectedWithin30 subquery — After:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND DATEDIFF(DAY, [revenue].[Invoice].[InvoiceDateUtc], [revenue].[Payment].[PaymentDateUtc]) <= 30
```

---

**File 7**: `Portal.Infrastructure/Repositories/PaymentRepository.cs`

**Method**: `GetPaidInPeriodAsync` (line ~241)

**Change**: Generic helper that sums all non-voided payments in a date range. Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause.

```sql
-- Before:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
  AND [revenue].[Payment].[PaymentDateUtc] < @ToUtc

-- After:
WHERE [revenue].[Payment].[BusinessId] = @BusinessId
  AND [revenue].[Payment].[IsVoided] = 0
  AND [revenue].[Payment].[ParentPaymentId] IS NULL
  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
  AND [revenue].[Payment].[PaymentDateUtc] < @ToUtc
```

### What Is NOT Changed

The following queries in `GetKpiDataAsync` deliberately include child allocations and MUST NOT be modified:
- `outstandingQuery` — uses `ValidPayments` subquery to sum ALL payments per invoice for settlement calculation
- `overdueQuery` — uses `ValidPayments` subquery to determine remaining balance past due date
- `partiallyPaidQuery` — uses `ValidPayments` subquery to determine partial payment status

These queries answer "how much has been paid toward this invoice?" — a question that MUST include child allocations because the child allocation is what actually settles the invoice debt.

## Testing Strategy

### Validation Approach

This is a simple SQL filter addition (not an algorithmic change). The testing approach is manual verification with a known scenario and a build-passing check. Property-based testing is not warranted because:
- The fix is a deterministic single-line filter added to each affected query's WHERE clause (or LINQ chain)
- There is no algorithmic logic to exercise with random inputs
- The bug condition is binary: `ParentPaymentId IS NULL` or it is not
- Manual test with known data provides full confidence

### Exploratory Bug Condition Checking

**Goal**: Confirm the double-counting using a known data scenario on UNFIXED code.

**Test Plan**: Use the existing database data (or seed test data) where a parent payment of EUR X has been allocated to invoices (child records exist). Query the "Paid This Month" KPI and observe that the total exceeds the actual cash inflow.

**Test Cases**:
1. **Single Parent + Two Children**: Parent EUR 500, Child EUR 300 + Child EUR 200. Unfixed "Paid This Month" shows EUR 1,000; should show EUR 500.
2. **Credit Applied to Invoice**: Parent EUR 0 (credit application) with child allocation EUR 150. Unfixed total inflates by EUR 150; should show EUR 0 additional (the original credit payment was already counted when it was first received).
3. **Mixed Month**: Two parent payments (EUR 200, EUR 300) + three child allocations (EUR 100, EUR 200, EUR 200). Unfixed total: EUR 1,000. Correct total: EUR 500.

### Fix Checking

**Goal**: Verify that after adding the filter, only parent payments appear in revenue KPIs.

**Pseudocode:**
```
FOR ALL payment WHERE isBugCondition(payment) DO
  result := GetKpiDataAsync_fixed(businessId)
  ASSERT payment.Amount is NOT included in result.PaidThisMonth
  ASSERT payment.Amount is NOT included in GetRevenueCollectedAsync_fixed monthly totals
  ASSERT payment.Amount is NOT included in GetInvoicedVsCollectedAsync_fixed CollectedAmount
  ASSERT payment.Amount is NOT included in GetRevenueVsExpensesAsync_fixed revenue totals
  ASSERT payment.Amount is NOT included in ComputeRevenueAsync_fixed result
  ASSERT payment.Amount is NOT included in GetCollectionRateAsync_fixed CollectedWithin30
  ASSERT payment.Amount is NOT included in GetPaidInPeriodAsync_fixed result
END FOR
```

### Preservation Checking

**Goal**: Verify that Outstanding Receivables, Overdue Amount, and Partially Paid KPIs remain unchanged.

**Pseudocode:**
```
FOR ALL payment WHERE NOT isBugCondition(payment) DO
  ASSERT GetKpiDataAsync_original(businessId).OutstandingReceivables 
       = GetKpiDataAsync_fixed(businessId).OutstandingReceivables
  ASSERT GetKpiDataAsync_original(businessId).OverdueAmount 
       = GetKpiDataAsync_fixed(businessId).OverdueAmount
END FOR
```

### Unit Tests

- Existing `DashboardService` unit tests (if any) should continue passing — verify with `dotnet test`
- No new unit tests required for a WHERE clause filter addition

### Property-Based Tests

Not applicable for this fix. The change is a deterministic SQL filter, not an algorithmic computation. PBT would not provide meaningful additional coverage beyond manual verification.

### Integration Tests

- **Manual Verification**: Execute the seven affected queries before and after the fix against test data containing parent+child payment records. Confirm totals decrease by exactly the sum of child allocation amounts.
- **Regression Check**: Verify Outstanding Receivables and Overdue Amount KPIs produce identical results before and after the fix.
- **Build Check**: Run `dotnet build` to confirm compilation succeeds after the query modifications.
