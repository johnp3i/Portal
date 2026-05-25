# Implementation Plan: Dashboard Upgrade

## Overview

Upgrade the Portal home dashboard from a quotation-only view to a comprehensive operational dashboard. Implementation follows a bottom-up approach: DTOs first, then service methods, ViewModel, controller refactor, and finally the view rebuild with Chart.js integration. Property-based tests validate correctness properties defined in the design.

## Tasks

- [x] 1. Create new DTO classes
  - [x] 1.1 Create ExpensesKpiDto, RevenueVsExpensesDto, InvoiceStatusBreakdownDto, RecentInvoiceDto, VatSummaryDto, and TopCustomerDto in Portal.Infrastructure/Models/
    - Create `ExpensesKpiDto.cs` with TotalExpenses (decimal) and PurchaseCount (int)
    - Create `RevenueVsExpensesDto.cs` with Year, Month, Label, Revenue, Expenses
    - Create `InvoiceStatusBreakdownDto.cs` with PaidCount, PartiallyPaidCount, UnpaidCount, OverdueCount, TotalCount
    - Create `RecentInvoiceDto.cs` with Id, InvoiceNumber, CustomerName, InvoiceFinancialStatusTypeId, FinancialStatusName, TotalAmount
    - Create `VatSummaryDto.cs` with TotalOutputVat, TotalInputVat, NetVatPayable, PeriodLabel, HasData
    - Create `TopCustomerDto.cs` with CustomerId, CustomerName, TotalInvoiced, TotalPaid
    - _Requirements: 1.1–1.5, 2.1–2.2, 3.1, 4.1–4.2, 7.1–7.2, 8.1–8.2_

  - [x] 1.2 Create DashboardViewModel in Portal.Infrastructure/Models/
    - Aggregate all section data: quotation KPIs, revenue KPIs, chart data, tables, VAT summary, top customers
    - Include CurrencySymbol property defaulting to "€"
    - Include HasVatData flag and VatPeriodLabel
    - Include TotalOverdueCount and TotalOverdueAmount for the warning banner
    - _Requirements: 1.1–1.7, 2.1–2.6, 3.1–3.4, 4.1–4.6, 5.1–5.7, 6.1–6.6, 7.1–7.5, 8.1–8.7, 9.1–9.6_

- [x] 2. Extend IDashboardService and implement new methods
  - [x] 2.1 Add new method signatures to IDashboardService interface
    - Add GetExpensesThisMonthAsync(int businessId) returning Task<ExpensesKpiDto>
    - Add GetRevenueVsExpensesAsync(int businessId) returning Task<List<RevenueVsExpensesDto>>
    - Add GetInvoiceStatusBreakdownAsync(int businessId) returning Task<InvoiceStatusBreakdownDto>
    - Add GetRecentInvoicesAsync(int businessId) returning Task<List<RecentInvoiceDto>>
    - Add GetVatSummaryAsync(int businessId) returning Task<VatSummaryDto>
    - Add GetTopCustomersAsync(int businessId) returning Task<List<TopCustomerDto>>
    - _Requirements: 1.4, 2.1, 2.2, 3.1, 4.1, 7.1, 8.1, 10.1_

  - [x] 2.2 Implement GetExpensesThisMonthAsync in DashboardService
    - Query [purchase].[Purchase] for non-cancelled purchases in current calendar month
    - Return sum of TotalAmount and count of matching purchases
    - Use @BusinessId parameter for tenant isolation
    - Follow existing raw SQL pattern with try/catch rethrow
    - _Requirements: 1.4, 1.5, 1.6, 1.7_

  - [x] 2.3 Write property test for Expenses This Month (Property 4)
    - **Property 4: Expenses This Month includes only valid in-month purchases**
    - Generate random purchases with varying amounts, dates, and IsCancelled flags
    - Assert computed total equals sum of TotalAmount where IsCancelled = 0 AND InvoiceDate in current month
    - Assert count equals number of qualifying purchases
    - Create file: Portal.Tests/PropertyBased/DashboardExpensesThisMonthPropertyTests.cs
    - **Validates: Requirements 1.4, 1.5, 1.7**

  - [x] 2.4 Implement GetRevenueVsExpensesAsync in DashboardService
    - Query revenue (non-voided payments) and expenses (non-cancelled purchases) grouped by month for last 6 months
    - Generate all 6 month entries (including months with zero data)
    - Set Label to abbreviated month name (e.g., "Jan", "Feb")
    - Order chronologically from oldest to newest
    - _Requirements: 2.1, 2.2, 2.4, 2.6, 2.7_

  - [x] 2.5 Write property test for Revenue vs Expenses chart data (Property 6)
    - **Property 6: Revenue vs Expenses chart data grouping**
    - Generate random payments and purchases spanning 6 months
    - Assert result contains exactly 6 entries ordered chronologically
    - Assert each entry's Revenue equals sum of non-voided payments in that month
    - Assert each entry's Expenses equals sum of non-cancelled purchases in that month
    - Create file: Portal.Tests/PropertyBased/DashboardRevenueVsExpensesPropertyTests.cs
    - **Validates: Requirements 2.1, 2.2, 2.4, 2.6**

  - [x] 2.6 Implement GetInvoiceStatusBreakdownAsync in DashboardService
    - Query [invoice].[Invoice] grouped by InvoiceFinancialStatusTypeId for issued, non-deleted invoices
    - Map counts to PaidCount, PartiallyPaidCount, UnpaidCount, OverdueCount
    - Compute TotalCount as sum of all status counts
    - _Requirements: 3.1, 3.5_

  - [x] 2.7 Write property test for Invoice Status Breakdown (Property 7)
    - **Property 7: Invoice status breakdown counts sum to total**
    - Generate random invoices with varying financial statuses
    - Assert PaidCount + PartiallyPaidCount + UnpaidCount + OverdueCount == TotalCount
    - Assert each count matches the number of invoices with that specific status
    - Create file: Portal.Tests/PropertyBased/DashboardInvoiceStatusBreakdownPropertyTests.cs
    - **Validates: Requirements 3.1**

  - [x] 2.8 Implement GetRecentInvoicesAsync in DashboardService
    - Query TOP 5 issued, non-deleted invoices ordered by InvoiceDate DESC
    - Join to Customer and InvoiceFinancialStatusType for names
    - Return RecentInvoiceDto list
    - _Requirements: 4.1, 4.5, 4.6_

  - [x] 2.9 Write property test for Recent Invoices (Property 8)
    - **Property 8: Recent invoices ordering and filtering**
    - Generate random invoices with varying statuses, dates, and deletion flags
    - Assert result contains at most 5 items
    - Assert all items have InvoiceStatusTypeId = 2 and IsDeleted = 0
    - Assert items are ordered by InvoiceDate descending
    - Create file: Portal.Tests/PropertyBased/DashboardRecentInvoicesPropertyTests.cs
    - **Validates: Requirements 4.1, 4.5**

  - [x] 2.10 Implement GetVatSummaryAsync in DashboardService
    - Query [vat].[VatSubmission] joined to [vat].[VatSubmissionPeriod]
    - Select open period (IsSubmitted = 0) with latest PeriodEndDate; fallback to most recent period
    - Return VatSummaryDto with OutputVat, InputVat, NetVatPayable, PeriodLabel, HasData
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 2.11 Write property test for VAT period selection (Property 11)
    - **Property 11: VAT period selection logic**
    - Generate random VAT submissions and periods with varying dates and submitted flags
    - Assert selected period is the open period with latest PeriodEndDate
    - Assert fallback to most recent period if no open period exists
    - Create file: Portal.Tests/PropertyBased/DashboardVatPeriodSelectionPropertyTests.cs
    - **Validates: Requirements 7.1**

  - [x] 2.12 Write property test for VAT Net Payable invariant (Property 12)
    - **Property 12: VAT Net Payable invariant**
    - Generate random VAT submission records with varying OutputVat and InputVat values
    - Assert NetVatPayable always equals OutputVat minus InputVat
    - Create file: Portal.Tests/PropertyBased/DashboardVatNetPayablePropertyTests.cs
    - **Validates: Requirements 7.2**

  - [x] 2.13 Implement GetTopCustomersAsync in DashboardService
    - Query TOP 5 customers ranked by total invoiced amount (sum of TotalAmount from issued, non-deleted invoices)
    - Include TotalPaid (sum of non-voided payments against that customer's invoices)
    - Use LEFT JOIN subquery for payment totals
    - _Requirements: 8.1, 8.2, 8.5, 8.6, 8.7_

  - [x] 2.14 Write property test for Top Customers (Property 13)
    - **Property 13: Top customers ranking and payment accuracy**
    - Generate random invoices and payments across multiple customers
    - Assert result contains at most 5 customers ordered by total invoiced descending
    - Assert TotalInvoiced equals sum of TotalAmount from issued non-deleted invoices per customer
    - Assert TotalPaid equals sum of non-voided payments against that customer's invoices
    - Create file: Portal.Tests/PropertyBased/DashboardTopCustomersPropertyTests.cs
    - **Validates: Requirements 8.1, 8.2, 8.5**

- [x] 3. Checkpoint - Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Refactor HomeController to use DashboardViewModel
  - [x] 4.1 Inject IDashboardService into HomeController and refactor Index action
    - Add IDashboardService as a constructor dependency
    - Resolve BusinessId via ICurrentTenantService with guard clause (redirect to error if 0)
    - Execute all service calls in parallel using Task.WhenAll
    - Construct DashboardViewModel from all results
    - Replace ViewBag usage with strongly-typed model
    - Retain existing quotation KPI logic (migrate to ViewModel properties)
    - Pass DashboardViewModel to View()
    - _Requirements: 9.1, 10.1, 10.2, 10.3, 10.4_

  - [-] 4.2 Write unit tests for HomeController dashboard logic
    - Test BusinessId = 0 redirects to error page
    - Test valid BusinessId returns ViewResult with DashboardViewModel
    - Test all service methods are called with correct BusinessId
    - Create file: Portal.Tests/Unit/Services/HomeControllerDashboardTests.cs
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 5. Rebuild Index.cshtml with full dashboard layout
  - [-] 5.1 Update Index.cshtml to use DashboardViewModel and render all sections
    - Add @model DashboardViewModel directive
    - Retain existing topbar and Quick Actions row unchanged
    - Render Quotation KPIs row (existing .grid-4, migrated from ViewBag to Model)
    - Render Revenue KPI Cards row (.grid-4 with coloured left borders: green Revenue, red Outstanding, gold Overdue, blue Expenses)
    - Render Charts row (.grid-2): Revenue vs Expenses bar chart canvas + Invoice Status donut chart canvas
    - Render Tables row 1 (.grid-2): Recent Invoices table + Overdue Invoices table with warning banner
    - Render Tables row 2 (.grid-2): Recent Payments table + Recent Quotations table (existing, relocated)
    - Render Bottom row (.grid-2): VAT Summary panel + Top Customers table
    - Apply .glass.card-pad styling to all new section cards
    - Use pill colours per requirements (green Paid, gold Partial, blue Unpaid, red Overdue)
    - Format all monetary values with CurrencySymbol and "N2"
    - Render empty-state messages for sections with no data (never hide sections)
    - _Requirements: 1.1–1.7, 2.3–2.5, 3.2–3.4, 4.2–4.6, 5.2–5.5, 6.2–6.6, 7.2–7.4, 8.2–8.6, 9.1–9.6_

  - [x] 5.2 Add Chart.js inline scripts for Revenue vs Expenses and Invoice Status charts
    - Serialize RevenueVsExpenses list to JSON in a <script> block
    - Serialize InvoiceStatusBreakdown to JSON in a <script> block
    - Render grouped bar chart (revenue green #129867, expenses blue #0D5EA6) with month labels on x-axis
    - Render donut chart with segments: green Paid, gold Partially Paid, blue Unpaid, red Overdue
    - Handle empty data: show "No invoice data available" message instead of empty chart
    - Include Chart.js CDN reference (or existing bundled version)
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 3.2, 3.3, 3.4_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Cross-cutting property tests
  - [x] 7.1 Write property test for Tenant Data Isolation (Property 5)
    - **Property 5: Tenant data isolation**
    - Generate data for two distinct BusinessIds with overlapping records
    - Call each dashboard service method with BusinessId A
    - Assert no records belonging to BusinessId B are returned
    - Repeat with BusinessId B and assert isolation from A
    - Create file: Portal.Tests/PropertyBased/DashboardTenantIsolationPropertyTests.cs
    - **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**

  - [x] 7.2 Write property test for Overdue Invoices (Property 9)
    - **Property 9: Overdue invoices filtering, ordering, and cap**
    - Generate random invoices and payments with varying due dates and balances
    - Assert result contains only invoices where DueDate < today AND outstanding > 0
    - Assert ordered by DueDate ascending, capped at 10 rows
    - Assert warning banner total equals sum of ALL overdue outstanding balances (not just displayed 10)
    - Create file: Portal.Tests/PropertyBased/DashboardOverdueInvoicesPropertyTests.cs
    - **Validates: Requirements 5.1, 5.4, 5.7**

  - [x] 7.3 Write property test for Recent Payments (Property 10)
    - **Property 10: Recent payments ordering and filtering**
    - Generate random payments with varying dates and voided flags
    - Assert result contains at most 5 items, all with IsVoided = 0
    - Assert ordered by PaymentDateUtc descending
    - Create file: Portal.Tests/PropertyBased/DashboardRecentPaymentsPropertyTests.cs
    - **Validates: Requirements 6.1, 6.5**

  - [x] 7.4 Write property test for Revenue This Month (Property 1)
    - **Property 1: Revenue This Month includes only valid in-month payments**
    - Generate random payments with varying amounts, dates, and IsVoided flags
    - Assert Revenue This Month equals sum of Amount where IsVoided = 0 AND PaymentDateUtc in current month
    - Assert count equals number of qualifying payments
    - Create file: Portal.Tests/PropertyBased/DashboardRevenueThisMonthPropertyTests.cs
    - **Validates: Requirements 1.1, 1.5, 1.7**

  - [x] 7.5 Write property test for Outstanding balance (Property 2)
    - **Property 2: Outstanding balance computation correctness**
    - Generate random invoices with statuses in (1, 2, 4) and associated payments
    - Assert Outstanding equals sum of (TotalAmount - sum of non-voided payments) per qualifying invoice
    - Assert count equals number of qualifying invoices
    - Create file: Portal.Tests/PropertyBased/DashboardOutstandingBalancePropertyTests.cs
    - **Validates: Requirements 1.2, 1.5**

  - [x] 7.6 Write property test for Overdue subset invariant (Property 3)
    - **Property 3: Overdue amount is a subset of outstanding**
    - Generate random invoices and payments
    - Assert Overdue amount <= Outstanding amount for any dataset
    - Create file: Portal.Tests/PropertyBased/DashboardOverdueSubsetPropertyTests.cs
    - **Validates: Requirements 1.3, 1.5**

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# with raw SQL queries following existing DashboardService patterns
- All new service methods follow the existing try/catch rethrow pattern with full table names in SQL
- Chart.js is rendered client-side from server-serialized JSON data (no AJAX round-trips)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.4", "2.6", "2.8", "2.10", "2.13"] },
    { "id": 3, "tasks": ["2.3", "2.5", "2.7", "2.9", "2.11", "2.12", "2.14"] },
    { "id": 4, "tasks": ["4.1"] },
    { "id": 5, "tasks": ["4.2", "5.1"] },
    { "id": 6, "tasks": ["5.2"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5", "7.6"] }
  ]
}
```
