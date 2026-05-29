# Implementation Plan: Scoped Dashboard

## Overview

This plan implements permission-based dashboard scoping by introducing a `DashboardScopeDto` that resolves user permissions at the controller level, conditionally fetches data for visible sections only, and renders the Razor view with adaptive grid layout. The approach modifies the existing `HomeController.Index`, extends `DashboardViewModel` with visibility flags, and updates `Index.cshtml` for conditional rendering. Property-based tests validate the scoping logic using FsCheck + xUnit.

## Tasks

- [x] 1. Create DashboardScopeDto and add visibility flags to DashboardViewModel
  - [x] 1.1 Create the DashboardScopeDto class
    - Create `Portal.Infrastructure/Models/DashboardScopeDto.cs`
    - Implement `ShowRevenue`, `ShowInvoice`, `ShowQuotation`, `ShowPurchase`, `ShowVat`, `ShowCustomer` boolean properties
    - Implement computed `HasAnyKpiSection` property (true if any of ShowRevenue, ShowInvoice, ShowQuotation, ShowPurchase, ShowVat is true)
    - Implement `FullAccess()` static factory method returning all flags as true
    - Implement `FromPermissions(Dictionary<string, string> permissions)` static factory method using `AccessLevels.None` check and `PortalModules` constants
    - _Requirements: 1.1, 1.2, 9.1, 9.2_

  - [x] 1.2 Add scope visibility flags to DashboardViewModel
    - Add `ShowRevenue`, `ShowInvoice`, `ShowQuotation`, `ShowPurchase`, `ShowVat`, `ShowCustomer` bool properties (default `true`)
    - Add `HasAnyKpiSection` bool property (default `true`)
    - Add `BusinessName` string? property for empty state display
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 4.1, 4.2, 5.1, 5.2, 6.1, 6.2, 8.1, 8.2_

- [x] 2. Modify HomeController.Index to resolve scope and conditionally fetch data
  - [x] 2.1 Inject IPermissionService into HomeController
    - Add `IPermissionService` to the constructor dependencies
    - Store as `_permissionService` field
    - _Requirements: 1.1_

  - [x] 2.2 Implement scope resolution logic in Index action
    - Check `User.HasClaim("IsOwner", "true")` or `User.IsInRole("SuperAdmin")` for privileged user detection
    - If privileged: call `DashboardScopeDto.FullAccess()`
    - If not privileged: get userId from `ClaimTypes.NameIdentifier`, call `_permissionService.GetAllAccessLevelsAsync(userId, businessId)`, then `DashboardScopeDto.FromPermissions(permissions)`
    - Wrap permission retrieval in try/catch — on failure, return view with `HasAnyKpiSection = false` and `BusinessName` populated
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 2.3 Implement conditional data fetching based on scope flags
    - Only call `_dashboardService.GetKpiDataAsync`, `GetOverdueInvoicesAsync`, `GetRecentPaymentsAsync`, `GetRevenueVsExpensesAsync`, `GetTopCustomersAsync` when `scope.ShowRevenue` is true
    - Only call `_dashboardService.GetInvoiceStatusBreakdownAsync`, `GetRecentInvoicesAsync` when `scope.ShowInvoice` is true
    - Only call `_quotationService.GetQuotationsAsync` and `_customerService.GetCustomersAsync` when `scope.ShowQuotation` is true
    - Only call `_dashboardService.GetExpensesThisMonthAsync` when `scope.ShowPurchase` is true
    - Only call `_dashboardService.GetVatSummaryAsync` when `scope.ShowVat` is true
    - Map scope flags onto the `DashboardViewModel` visibility properties before returning the view
    - _Requirements: 2.3, 2.4, 3.3, 4.3, 5.3, 6.3_

  - [ ]* 2.4 Write unit tests for HomeController.Index scope resolution
    - Test privileged user (IsOwner claim) bypasses permission checks and gets full scope
    - Test privileged user (SuperAdmin role) bypasses permission checks and gets full scope
    - Test regular user with partial permissions only triggers relevant service calls
    - Test IPermissionService failure returns empty state view model with BusinessName
    - Use Moq to mock IPermissionService, IDashboardService, IQuotationService, ICustomerService, IBusinessService, ICurrentTenantService
    - _Requirements: 1.1, 1.2, 1.3, 2.4, 3.3, 4.3, 5.3, 6.3_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement conditional rendering in Index.cshtml
  - [x] 4.1 Add empty state rendering and wrap dashboard content
    - Wrap all existing dashboard content in `@if (Model.HasAnyKpiSection) { ... }`
    - Add `else` block with empty state: welcome message, business name display, and administrator contact suggestion
    - Style empty state using the glass card-pad pattern with centered content
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 4.2 Scope the quick action links
    - Wrap "New Quotation" link in `@if (Model.ShowQuotation)`
    - Wrap "Record Payment" and "Customer Statement" links in `@if (Model.ShowRevenue)`
    - Wrap "Create Invoice" link in `@if (Model.ShowInvoice)`
    - Wrap "Record Purchase" link in `@if (Model.ShowPurchase)`
    - Wrap "New Customer" link in `@if (Model.ShowCustomer)`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 4.3 Scope the KPI gauges row
    - Wrap Revenue, Outstanding, and Overdue gauges in `@if (Model.ShowRevenue)`
    - Wrap Expenses gauge in `@if (Model.ShowPurchase)`
    - Hide the entire gauge row div if neither ShowRevenue nor ShowPurchase is true
    - _Requirements: 2.1, 2.2, 5.1, 5.2_

  - [x] 4.4 Scope the quotation stats strip and VAT section
    - Wrap quotation stats (Drafts, Sent, Accepted, Customers) in `@if (Model.ShowQuotation)`
    - Wrap VAT summary (output, input, net) in `@if (Model.ShowVat)`
    - Hide the entire strip div if neither ShowQuotation nor ShowVat is true
    - _Requirements: 4.1, 4.2, 6.1, 6.2_

  - [x] 4.5 Scope the charts row with adaptive grid layout
    - Revenue vs Expenses chart: render only if `Model.ShowRevenue`
    - Invoice Status Breakdown chart: render only if `Model.ShowInvoice`
    - If both visible: render in `grid-2` layout
    - If only one visible: render at full width (no grid-2 wrapper)
    - If neither visible: hide the entire row
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 10.1, 10.2_

  - [x] 4.6 Scope the tables rows with adaptive grid layout
    - Recent Invoices + Overdue Invoices row: show Recent Invoices if `Model.ShowInvoice`, show Overdue Invoices if `Model.ShowRevenue`
    - Recent Payments + Recent Quotations row: show Recent Payments if `Model.ShowRevenue`, show Recent Quotations if `Model.ShowQuotation`
    - Top Customers + Revenue by Customer row: show both if `Model.ShowRevenue`
    - Apply full-width rendering when only one section in a row is visible
    - Hide entire row when both sections are hidden
    - _Requirements: 2.1, 3.1, 4.1, 10.1, 10.2, 10.3_

  - [x] 4.7 Scope the Chart.js script blocks
    - Only emit `revenueVsExpensesData` JSON and chart initialization when `Model.ShowRevenue`
    - Only emit `invoiceStatusData` JSON and chart initialization when `Model.ShowInvoice`
    - Only emit `topCustomersData` JSON and chart initialization when `Model.ShowRevenue`
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Write property-based tests for DashboardScopeDto
  - [x]* 6.1 Write property test for permission-to-visibility biconditional
    - **Property 1: Permission-to-visibility mapping is a biconditional on access level**
    - Generate arbitrary permission dictionaries with random access levels ("full", "readonly", "none") for each module
    - Assert: for each module, the corresponding scope flag is `true` iff the access level is not "none"
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - **Validates: Requirements 2.1, 2.2, 3.1, 3.2, 4.1, 4.2, 5.1, 5.2, 6.1, 6.2, 9.1, 9.2**

  - [x]* 6.2 Write property test for privileged user full access
    - **Property 2: Privileged users always receive full access scope**
    - Assert: `DashboardScopeDto.FullAccess()` always returns all flags as `true` regardless of any input
    - Verify all six module flags and `HasAnyKpiSection` are true
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - **Validates: Requirements 1.2**

  - [x]* 6.3 Write property test for HasAnyKpiSection correctness
    - **Property 3: HasAnyKpiSection is true iff at least one KPI-bearing module is visible**
    - Generate arbitrary `DashboardScopeDto` instances with random boolean flags
    - Assert: `HasAnyKpiSection` equals `(ShowRevenue || ShowInvoice || ShowQuotation || ShowPurchase || ShowVat)`
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - **Validates: Requirements 8.1, 8.3**

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design does not require any database schema changes — all scoping uses existing IPermissionService
- IDashboardService interface remains unchanged; the controller simply skips calling methods for hidden sections
- FsCheck + xUnit is already configured in Portal.Tests project (FsCheck 2.16.6, FsCheck.Xunit 2.16.6)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["2.3"] },
    { "id": 4, "tasks": ["2.4", "4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3", "4.4", "4.5"] },
    { "id": 6, "tasks": ["4.6", "4.7"] },
    { "id": 7, "tasks": ["6.1", "6.2", "6.3"] }
  ]
}
```
