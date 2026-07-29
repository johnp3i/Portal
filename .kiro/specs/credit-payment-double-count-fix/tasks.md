# Credit Payment Double-Count Fix — Tasks

## Task 1: Add ParentPaymentId IS NULL filter to Paid This Month KPI

- [ ] 1.1 Open `Portal.Infrastructure/Services/DashboardService.cs` and locate the `paidThisMonthQuery` inside `GetKpiDataAsync`
- [ ] 1.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause after the `IsVoided = 0` condition
- [ ] 1.3 Verify the query compiles and the method still returns `DashboardKpiDto` correctly

## Task 2: Add ParentPaymentId IS NULL filter to Revenue Collected chart query

- [ ] 2.1 Open `Portal.Infrastructure/Repositories/PaymentRepository.cs` and locate the `GetMonthlyTotalsAsync` method
- [ ] 2.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause after the `IsVoided = 0` condition
- [ ] 2.3 Verify the query compiles and the method still returns `List<MonthlyRevenueDto>` correctly

## Task 3: Add ParentPaymentId IS NULL filter to Invoiced vs Collected chart query

- [ ] 3.1 Open `Portal.Infrastructure/Services/DashboardService.cs` and locate the query inside `GetInvoicedVsCollectedAsync`
- [ ] 3.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the Months UNION payment subquery (the SELECT from `[revenue].[Payment]` inside the Months CTE)
- [ ] 3.3 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the CollectedData LEFT JOIN subquery
- [ ] 3.4 Verify the query compiles and the method still returns `List<InvoicedVsCollectedDto>` correctly

## Task 5: Add ParentPaymentId IS NULL filter to Revenue vs Expenses chart query

- [ ] 5.1 Open `Portal.Infrastructure/Services/DashboardService.cs` and locate the revenue query inside `GetRevenueVsExpensesAsync` (line ~720)
- [ ] 5.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause after the `IsVoided = 0` condition
- [ ] 5.3 Verify the query compiles and the method still returns the correct chart data

## Task 6: Add ParentPaymentId == null filter to PnlService.ComputeRevenueAsync (LINQ)

- [ ] 6.1 Open `Portal.Infrastructure/Services/PnlService.cs` and locate the LINQ query inside `ComputeRevenueAsync` (line ~124)
- [ ] 6.2 Add `.Where(p => p.ParentPaymentId == null)` to the LINQ chain after the `.Where(p => !p.IsVoided)` filter
- [ ] 6.3 Verify the query compiles and the method still returns the correct P&L revenue figure

## Task 7: Add ParentPaymentId IS NULL filter to GetCollectionRateAsync

- [ ] 7.1 Open `Portal.Infrastructure/Services/DashboardService.cs` and locate the `CollectedWithin30` subquery inside `GetCollectionRateAsync` (line ~406)
- [ ] 7.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the `CollectedWithin30` subquery WHERE clause
- [ ] 7.3 Verify the query compiles and the method still returns the correct collection rate percentage

## Task 8: Add ParentPaymentId IS NULL filter to GetPaidInPeriodAsync

- [ ] 8.1 Open `Portal.Infrastructure/Repositories/PaymentRepository.cs` and locate the query inside `GetPaidInPeriodAsync` (line ~241)
- [ ] 8.2 Add `AND [revenue].[Payment].[ParentPaymentId] IS NULL` to the WHERE clause after the `IsVoided = 0` condition
- [ ] 8.3 Verify the query compiles and the method still returns the correct sum

## Task 9: Build verification and manual test

- [ ] 9.1 Run `dotnet build` on the solution to confirm no compilation errors
- [ ] 9.2 Run `dotnet test` to confirm existing tests pass
- [ ] 9.3 Manually verify the fix using a known scenario: create or identify a business with a parent payment (EUR 500) that has child allocations (EUR 300 + EUR 200). Confirm "Paid This Month" shows EUR 500 (not EUR 1,000)
- [ ] 9.4 Confirm Outstanding Receivables and Overdue Amount KPIs are unchanged (still correctly account for child allocations in settlement calculations)
- [ ] 9.5 Verify "Revenue vs Expenses" chart, P&L revenue, and Collection Rate are no longer inflated by child allocations
