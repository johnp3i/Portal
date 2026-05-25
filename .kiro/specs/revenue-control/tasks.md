# Implementation Plan: Revenue Control

## Overview

Implement the Revenue Control module for the Portal platform. This module adds payment recording, financial status computation, receivables tracking, and a revenue dashboard with KPI cards, charts, and data tables. The implementation follows the established MVC + Service + Repository pattern with raw SQL, FsCheck property-based tests, and the MyChair Design System for UI.

## Tasks

- [x] 1. Create DTOs and data models
  - [x] 1.1 Create Revenue Control DTOs in Portal.Infrastructure/Models
    - Create `RecordPaymentDto.cs` with InvoiceId, PaymentMethodTypeId, PaymentDateUtc, Amount, Reference, Notes
    - Create `DashboardKpiDto.cs` with OutstandingReceivables, OutstandingInvoiceCount, OverdueAmount, OverdueInvoiceCount, PaidThisMonth, PaidThisMonthCount, PartiallyPaidAmount, PartiallyPaidCount
    - Create `MonthlyRevenueDto.cs` with Year, Month, Label, Amount
    - Create `InvoicedVsCollectedDto.cs` with Year, Month, Label, InvoicedAmount, CollectedAmount
    - Create `OverdueInvoiceDto.cs` with Id, InvoiceNumber, CustomerName, DueDate, DaysOverdue, OutstandingBalance
    - Create `RecentPaymentDto.cs` with Id, PaymentDateUtc, InvoiceNumber, CustomerName, PaymentMethodName, Amount, IsFullPayment
    - Create `ReceivableDto.cs` with Id, InvoiceNumber, CustomerName, InvoiceDate, DueDate, TotalAmount, TotalPaid, OutstandingBalance, InvoiceFinancialStatusTypeId, FinancialStatusName, HasOutstandingBalance
    - Create `PaymentHistoryDto.cs` with Id, PaymentDateUtc, Amount, PaymentMethodName, Reference, Notes, IsVoided
    - Create `VatSummaryDto.cs` with OutputVatCollected, InputVat, NetVatPayable, OutputInputRatio, PeriodLabel
    - Create `VatPeriodLiabilityDto.cs` with PeriodLabel, OutputVat, InputVat, NetPayable
    - _Requirements: 1.5, 2.1, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 6.1, 6.2, 6.3, 6.4, 7.2, 8.2, 9.2, 10.3_

  - [x] 1.2 Create Payment entity in Portal.Infrastructure/Entities
    - Create `Payment.cs` entity with Id, BusinessId, InvoiceId, PaymentMethodTypeId, PaymentDateUtc, Amount, Reference, Notes, IsVoided, CreatedByUserId, CreatedAtUtc
    - Register entity in `PortalDbContext` with `DbSet<Payment>` and configure table mapping to `[revenue].[Payment]`
    - _Requirements: 1.5, 3.1, 3.2_

- [x] 2. Implement PaymentRepository
  - [x] 2.1 Create PaymentRepository with core CRUD operations
    - Create `Portal.Infrastructure/Repositories/PaymentRepository.cs` extending `GenericStoredProcedureRepository<Payment>`
    - Implement `InsertAsync(Payment entity)` — INSERT into [revenue].[Payment] with all fields, return new Id via SCOPE_IDENTITY()
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single payment with BusinessId filter
    - Implement `VoidAsync(int paymentId)` — UPDATE [revenue].[Payment] SET IsVoided = 1 WHERE Id = @PaymentId
    - Implement `GetValidPaymentsByInvoiceIdAsync(int invoiceId, int businessId)` — SELECT WHERE IsVoided = 0 AND InvoiceId AND BusinessId
    - Implement `GetAllPaymentsByInvoiceIdAsync(int invoiceId, int businessId)` — SELECT all payments including voided
    - Use full table names in SQL (no aliases), SqlParameter with null-safety, try/catch rethrow pattern
    - _Requirements: 1.5, 3.1, 3.2, 12.2_

  - [x] 2.2 Add dashboard and aggregation query methods to PaymentRepository
    - Implement `GetTotalPaidAsync(int invoiceId, int businessId)` — SUM(Amount) WHERE IsVoided = 0
    - Implement `GetPaidInPeriodAsync(int businessId, DateTime fromUtc, DateTime toUtc)` — SUM(Amount) for date range
    - Implement `GetMonthlyTotalsAsync(int businessId, DateTime fromUtc)` — GROUP BY Year/Month for last 12 months
    - Implement `GetRecentPaymentsPagedAsync(int businessId, string? searchTerm, int offset, int pageSize)` — JOIN Invoice + Customer + PaymentMethodType, filter voided, search, paginate, return tuple (List, TotalCount)
    - _Requirements: 4.3, 5.1, 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 3. Implement FinancialStatusEngine (pure function)
  - [x] 3.1 Create IFinancialStatusEngine interface and FinancialStatusEngine implementation
    - Create `Portal.Infrastructure/Services/IFinancialStatusEngine.cs` interface
    - Create `Portal.Infrastructure/Services/FinancialStatusEngine.cs` implementation
    - Implement `ComputeOutstandingBalance(decimal totalAmount, IEnumerable<Payment> payments)` — TotalAmount minus sum of non-voided payment amounts
    - Implement `DetermineFinancialStatus(decimal totalAmount, decimal outstandingBalance, bool hasValidPayments, DateOnly dueDate, int currentStatusId)` — pure deterministic function following the decision tree: WrittenOff preserved → Paid → Overdue → PartiallyPaid → Unpaid
    - Implement `RecalculateStatusAsync(int invoiceId, int businessId)` — fetch payments, compute balance, determine status, update invoice
    - Register in DI container
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [x] 3.2 Write property test: Deterministic financial status computation (Property 4)
    - **Property 4: Deterministic financial status computation**
    - Generate random totalAmount, payment lists (some voided), dueDate, currentStatusId
    - Verify the engine returns the correct status for all combinations per the decision tree
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

  - [x] 3.3 Write property test: Outstanding balance computation correctness (Property 5)
    - **Property 5: Outstanding balance computation correctness**
    - Generate random TotalAmount T and list of payments with valid amounts summing to S
    - Verify OutstandingBalance equals T - S (only non-voided payments counted)
    - **Validates: Requirements 2.1**

  - [x] 3.4 Write property test: Status recalculation idempotence (Property 6)
    - **Property 6: Status recalculation idempotence**
    - Generate random invoice state, compute status, then compute again
    - Verify OutstandingBalance is identical after double computation
    - **Validates: Requirements 2.7**

- [x] 4. Implement PaymentService
  - [x] 4.1 Create IPaymentService interface and PaymentService implementation
    - Create `Portal.Infrastructure/Services/IPaymentService.cs` interface
    - Create `Portal.Infrastructure/Services/PaymentService.cs` implementation
    - Implement `RecordPaymentAsync(RecordPaymentDto dto, int businessId, string userId)`:
      - Validate invoice exists and belongs to businessId
      - Validate InvoiceStatusTypeId = 2 (Issued)
      - Validate amount > 0
      - Compute outstanding balance and validate amount ≤ outstanding
      - Insert payment record
      - Trigger FinancialStatusEngine.RecalculateStatusAsync
      - Return ServiceResult.Ok(paymentId) or ServiceResult.Fail(message)
    - Implement `VoidPaymentAsync(int paymentId, int businessId)`:
      - Validate payment exists and belongs to businessId
      - Check if already voided (return informational message)
      - Set IsVoided = 1
      - Trigger FinancialStatusEngine.RecalculateStatusAsync on parent invoice
    - Implement `GetPaymentHistoryAsync(int invoiceId, int businessId)` — return all payments mapped to PaymentHistoryDto
    - Implement `GetPaymentMethodTypesAsync()` — return active PaymentMethodType records
    - Register in DI container
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 3.1, 3.2, 3.3, 3.4_

  - [x] 4.2 Write property test: Payment validation rejects non-Issued invoices (Property 1)
    - **Property 1: Payment validation rejects non-Issued invoices**
    - Generate random InvoiceStatusTypeId ≠ 2, verify RecordPaymentAsync returns failure
    - Use Moq to mock repositories
    - **Validates: Requirements 1.1, 1.2**

  - [x] 4.3 Write property test: Payment amount boundary validation (Property 2)
    - **Property 2: Payment amount boundary validation**
    - Generate random amounts ≤ 0 or > OutstandingBalance, verify rejection
    - **Validates: Requirements 1.3, 1.4**

  - [x] 4.4 Write property test: Void preserves payment record (Property 7)
    - **Property 7: Void preserves payment record and sets flag**
    - Generate random payment, void it, verify IsVoided = 1 and record still exists
    - **Validates: Requirements 3.1, 3.2**

- [x] 5. Checkpoint - Ensure core business logic tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement DashboardService
  - [x] 6.1 Create IDashboardService interface and DashboardService implementation
    - Create `Portal.Infrastructure/Services/IDashboardService.cs` interface
    - Create `Portal.Infrastructure/Services/DashboardService.cs` implementation
    - Implement `GetKpiDataAsync(int businessId)` — compute Outstanding Receivables, Overdue Amount, Paid This Month, Partially Paid using repository queries scoped to businessId
    - Implement `GetRevenueCollectedAsync(int businessId)` — monthly payment totals for last 12 months
    - Implement `GetInvoicedVsCollectedAsync(int businessId)` — paired monthly totals of invoiced vs collected
    - Implement `GetCollectionRateAsync(int businessId)` — percentage collected within 30 days of invoice date
    - Implement `GetOverdueInvoicesAsync(int businessId, string? searchTerm, int page, int pageSize)` — overdue invoices sorted by days overdue descending, with search and pagination
    - Implement `GetRecentPaymentsAsync(int businessId, string? searchTerm, int page, int pageSize)` — recent non-voided payments sorted by PaymentDateUtc descending, with search and pagination
    - Register in DI container
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 6.2 Write property test: Dashboard KPI Outstanding Receivables (Property 8)
    - **Property 8: Dashboard KPI Outstanding Receivables correctness**
    - Generate random invoices with various statuses, verify sum matches expected
    - **Validates: Requirements 4.1**

  - [x] 6.3 Write property test: Dashboard KPI Overdue Amount (Property 9)
    - **Property 9: Dashboard KPI Overdue Amount correctness**
    - Generate random invoices with various due dates, verify overdue sum
    - **Validates: Requirements 4.2**

  - [x] 6.4 Write property test: Dashboard KPI Paid This Month (Property 10)
    - **Property 10: Dashboard KPI Paid This Month correctness**
    - Generate random payments with various dates, verify current month sum
    - **Validates: Requirements 4.3**

  - [x] 6.5 Write property test: Overdue invoices sorted and complete (Property 11)
    - **Property 11: Overdue invoices sorted and complete**
    - Generate random overdue invoices, verify descending sort by days overdue and all fields present
    - **Validates: Requirements 7.1, 7.2**

  - [x] 6.6 Write property test: Recent payments sorted and void-excluded (Property 12)
    - **Property 12: Recent payments sorted, complete, and void-excluded**
    - Generate random payments (some voided), verify sort order, field completeness, and voided exclusion
    - **Validates: Requirements 8.1, 8.2, 8.5**

- [x] 7. Implement ReceivablesQueryService
  - [x] 7.1 Create IReceivablesQueryService interface and ReceivablesQueryService implementation
    - Create `Portal.Infrastructure/Services/IReceivablesQueryService.cs` interface
    - Create `Portal.Infrastructure/Services/ReceivablesQueryService.cs` implementation
    - Implement `GetReceivablesAsync(int businessId, string? searchTerm, int? financialStatusFilter, int? customerFilter, DateOnly? dueFrom, DateOnly? dueTo, int page, int pageSize)`:
      - Query non-deleted invoices with InvoiceStatusTypeId = 2 (Issued) for businessId
      - JOIN Customer for name, compute TotalPaid via subquery on valid payments
      - Apply search filter (InvoiceNumber or CustomerName LIKE)
      - Apply financial status filter
      - Apply customer filter
      - Apply date range filter (DueDate between dueFrom and dueTo)
      - Return PagedResult<ReceivableDto> with total count
    - Register in DI container
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

  - [x] 7.2 Write property test: Receivables base query correctness (Property 13)
    - **Property 13: Receivables base query correctness**
    - Generate random invoices with various statuses, verify only Issued non-deleted returned with all fields
    - **Validates: Requirements 9.1, 9.2**

  - [x] 7.3 Write property test: Receivables filter correctness (Property 14)
    - **Property 14: Receivables filter correctness**
    - Generate random invoices, apply random filter combinations, verify all results satisfy all active filters
    - **Validates: Requirements 9.3, 9.4, 9.5, 9.6**

  - [x] 7.4 Write property test: Pagination respects page size and total count (Property 15)
    - **Property 15: Pagination respects page size and total count**
    - Generate random data sets, verify page size limits and TotalCount accuracy
    - **Validates: Requirements 7.4, 8.4, 9.7**

- [x] 8. Implement VatIntegrationService
  - [x] 8.1 Create IVatIntegrationService interface and VatIntegrationService implementation
    - Create `Portal.Infrastructure/Services/IVatIntegrationService.cs` interface
    - Create `Portal.Infrastructure/Services/VatIntegrationService.cs` implementation
    - Implement `GetCurrentPeriodSummaryAsync(int businessId)`:
      - Find current VAT period from VatSubmissionPeriod where today falls between PeriodStartDate and PeriodEndDate
      - Compute Output VAT: sum of Invoice.TaxAmount for fully paid invoices (InvoiceFinancialStatusTypeId = 3) with InvoiceDate in current period
      - Compute Input VAT: sum of Purchase.VatAmount for non-cancelled purchases with InvoiceDate in current period
      - Compute Net VAT Payable: Output - Input
      - Compute Output/Input Ratio: Output / Input (return 0 when Input = 0)
    - Implement `GetVatLiabilityByPeriodAsync(int businessId)`:
      - Get last 6 VAT periods
      - For each period compute Output VAT, Input VAT, Net Payable
      - Return List<VatPeriodLiabilityDto>
    - Register in DI container
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 8.2 Write property test: VAT Output/Input ratio zero-guard (Property 17)
    - **Property 17: VAT Output/Input ratio zero-guard**
    - Generate scenarios where Input VAT = 0, verify ratio returns 0 without exception
    - **Validates: Requirements 6.5**

- [x] 9. Checkpoint - Ensure all service layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Implement RevenueController
  - [x] 10.1 Create RevenueController with page actions and AJAX endpoints
    - Create `Portal.Web/Controllers/RevenueController.cs` with [Authorize] attribute
    - Inject IPaymentService, IDashboardService, IReceivablesQueryService, IVatIntegrationService, ICurrentTenantService, ICustomerService
    - Implement `Dashboard()` GET action — call DashboardService and VatIntegrationService, pass view model to Dashboard view
    - Implement `Receivables(string? search, int? financialStatus, int? customer, string? dueFrom, string? dueTo, int page = 1)` GET action — call ReceivablesQueryService, pass results to Receivables view
    - Implement `InvoiceDetail(int id)` GET action — load invoice with lines, payment history, compute progress bar data
    - Implement `RecordPayment(...)` POST AJAX action — [ValidateAntiForgeryToken], call PaymentService.RecordPaymentAsync, return Json({ success, message })
    - Implement `VoidPayment(int paymentId)` POST AJAX action — [ValidateAntiForgeryToken], call PaymentService.VoidPaymentAsync, return Json({ success, message })
    - Implement `GetOverdueInvoices(string? search, int page, int pageSize)` GET AJAX action — return Json with paged overdue data
    - Implement `GetRecentPayments(string? search, int page, int pageSize)` GET AJAX action — return Json with paged recent payments
    - All actions scoped to ICurrentTenantService.CurrentBusinessId for tenant isolation
    - Wrap AJAX actions in try/catch returning Json({ success = false, message = "An unexpected error occurred." })
    - _Requirements: 1.1, 1.5, 3.1, 4.1, 5.1, 7.1, 8.1, 9.1, 10.1, 11.5, 12.1, 12.2, 12.3, 12.4_

  - [x] 10.2 Write property test: Tenant isolation invariant (Property 16)
    - **Property 16: Tenant isolation invariant**
    - Generate multi-tenant data, verify controller actions only return data for authenticated BusinessId
    - Use in-memory database with multiple business records
    - **Validates: Requirements 12.1, 12.2, 12.3, 12.4**

- [x] 11. Implement Views
  - [x] 11.1 Create Dashboard view (Views/Revenue/Dashboard.cshtml)
    - KPI cards row: Outstanding Receivables, Overdue Amount, Paid This Month, Partially Paid — using `.kpi-grid` with 4 columns, color-coded left borders (blue, danger, success, warning)
    - Charts row: Revenue Collected line chart (Chart.js canvas), Invoiced vs Collected bar chart, Collection Rate gauge card
    - VAT section row: 3 stacked VAT KPI cards (Output VAT, Input VAT, Net VAT Payable), VAT Liability by Period bar chart, Output/Input Ratio card
    - Overdue Invoices table with search input, pagination, "View" action links
    - Recent Payments table with search input, pagination, Full/Partial pill badges
    - Follow MyChair Design System: `.glass.card-pad` sections, `margin-bottom:22px` between cards
    - Include `_PaymentModal` partial at bottom
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 11.2 Create Receivables view (Views/Revenue/Receivables.cshtml)
    - Filter panel: Search input, Financial Status dropdown, Customer dropdown, Due From date, Due To date, Filter + Clear buttons
    - Receivables table: InvoiceNumber, Customer, InvoiceDate, DueDate, Total, Paid, Outstanding, Status pill, Actions (View + Pay link when HasOutstandingBalance)
    - Pagination controls below table
    - Follow filter + table card pattern with `margin-bottom:22px` between filter and table cards
    - Include `_PaymentModal` partial at bottom
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

  - [x] 11.3 Create InvoiceDetail view (Views/Revenue/InvoiceDetail.cshtml)
    - Financial summary KPI cards: Invoice Total, Total Paid, Outstanding Balance, Due Date with overdue indicator
    - Payment progress bar showing paid vs outstanding percentage
    - Invoice line items table: Description, Quantity, UnitPrice, LineTotal, SortOrder
    - Payment history table: PaymentDateUtc, Amount, PaymentMethod, Reference, Notes, Void button (for non-voided), strikethrough styling for voided
    - "Record Payment" button opening payment modal pre-populated with invoice context
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

  - [x] 11.4 Create _PaymentModal partial view (Views/Revenue/_PaymentModal.cshtml)
    - Modal overlay with `.modal` card styling
    - Invoice context bar: InvoiceNumber, Customer name, remaining balance
    - Form fields: Payment Date (required, date input), Amount (required, numeric, min=0.01), Payment Method (required, dropdown from PaymentMethodType), Reference (optional), Notes (optional)
    - Client-side validation: amount > 0, amount ≤ outstanding balance (from data attribute)
    - Anti-forgery token included in form
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 12. Implement client-side JavaScript
  - [x] 12.1 Create revenue-dashboard.js for Dashboard page interactions
    - Initialize Chart.js charts: Revenue Collected (line), Invoiced vs Collected (bar), VAT Liability by Period (bar)
    - Implement overdue invoices table search and pagination via fetch to `/Revenue/GetOverdueInvoices`
    - Implement recent payments table search and pagination via fetch to `/Revenue/GetRecentPayments`
    - Follow BlockUI + SweetAlert2 pattern for all AJAX calls
    - Use vanilla fetch API with antiforgery token headers
    - _Requirements: 5.1, 5.2, 7.3, 7.4, 8.3, 8.4_

  - [x] 12.2 Create revenue-payment.js for Payment Modal and void interactions
    - Implement `openPaymentModal(invoiceId, invoiceNumber, customerName, outstandingBalance)` — populate modal context bar and set data attributes
    - Implement form submission: validate amount > 0 and ≤ outstanding, BlockUI.show(), fetch POST to `/Revenue/RecordPayment`, BlockUI.hide(), Swal.fire() success/error
    - Implement `voidPayment(paymentId)` — Swal.fire confirmation dialog (destructive, red button), BlockUI.show(), fetch POST to `/Revenue/VoidPayment`, BlockUI.hide(), Swal.fire() result, refresh page data
    - Include antiforgery token in all POST requests
    - _Requirements: 11.3, 11.4, 11.5, 11.6, 10.6_

  - [x] 12.3 Write property test: Payment progress bar percentage correctness (Property 18)
    - **Property 18: Payment progress bar percentage correctness**
    - Generate random TotalAmount and TotalPaid values, verify percentage = (TotalPaid / TotalAmount) × 100 clamped 0-100
    - Test as a unit test on the view model computation helper
    - **Validates: Requirements 10.4**

- [x] 13. Register services in DI and wire routing
  - [x] 13.1 Register all new services and repositories in Program.cs / DI configuration
    - Register PaymentRepository as scoped
    - Register IFinancialStatusEngine / FinancialStatusEngine as scoped
    - Register IPaymentService / PaymentService as scoped
    - Register IDashboardService / DashboardService as scoped
    - Register IReceivablesQueryService / ReceivablesQueryService as scoped
    - Register IVatIntegrationService / VatIntegrationService as scoped
    - Verify routing maps to RevenueController actions
    - _Requirements: 1.1, 4.1, 6.1, 9.1_

- [x] 14. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Integration tests
  - [x] 15.1 Write integration test: End-to-end payment recording flow
    - Record payment → verify status update → verify balance change → verify payment in history
    - Use in-memory database with seeded invoice data
    - _Requirements: 1.5, 1.6, 2.1_

  - [x] 15.2 Write integration test: End-to-end void flow
    - Record payment → void payment → verify status recalculation → verify balance restored
    - _Requirements: 3.1, 3.3_

  - [x] 15.3 Write integration test: Dashboard data accuracy
    - Seed multiple invoices and payments, verify all KPI values match expected computations
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 15.4 Write integration test: VAT integration with period data
    - Seed invoices, payments, and purchases across VAT periods, verify Output/Input/Net calculations
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 15.5 Write integration test: Tenant isolation with multi-business data
    - Seed data for two businesses, verify each business only sees its own data across all service methods
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases
- All repository methods use full table names (no aliases), SqlParameter with null-safety, and try/catch rethrow
- All AJAX interactions follow BlockUI + SweetAlert2 + vanilla fetch pattern
- Views follow MyChair Design System with `.glass.card-pad` sections and consistent spacing

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "3.4", "4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "6.1", "7.1", "8.1"] },
    { "id": 5, "tasks": ["6.2", "6.3", "6.4", "6.5", "6.6", "7.2", "7.3", "7.4", "8.2"] },
    { "id": 6, "tasks": ["10.1"] },
    { "id": 7, "tasks": ["10.2", "11.1", "11.2", "11.3", "11.4", "13.1"] },
    { "id": 8, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 9, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5"] }
  ]
}
```
