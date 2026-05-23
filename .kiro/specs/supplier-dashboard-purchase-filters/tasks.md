# Implementation Plan: Supplier Dashboard Purchase Filters

## Overview

This plan implements granular filtering controls (description search, category dropdown, date range) for the purchases table on the Supplier Dashboard. Filters apply only to the purchases table query — KPIs and charts remain scoped solely by the period filter. The implementation follows the existing MVC + Service pattern with server-side filtering via EF Core LINQ queries.

## Tasks

- [x] 1. Update ViewModel and add supporting model
  - [x] 1.1 Add filter state properties and ExpenseCategoryOption class to SupplierDashboardViewModel
    - Add `FilterDescription` (string?), `FilterCategoryId` (int?), `FilterDateFrom` (DateOnly?), `FilterDateTo` (DateOnly?) properties to `SupplierDashboardViewModel`
    - Add `ExpenseCategories` (List<ExpenseCategoryOption>) property to `SupplierDashboardViewModel`
    - Create new `ExpenseCategoryOption` class with `Id` (int) and `Name` (string) properties in the same file
    - _Requirements: 1.7, 2.8, 3.7, 4.3_

- [x] 2. Update service interface and implementation
  - [x] 2.1 Update ISupplierDashboardService interface signature
    - Add optional parameters `string? description`, `int? categoryId`, `DateOnly? dateFrom`, `DateOnly? dateTo` to `GetDashboardAsync`
    - _Requirements: 4.4_

  - [x] 2.2 Implement filter logic in SupplierDashboardService
    - Validate `categoryId` — query `ExpenseCategories` to confirm it is active and belongs to the current business; if invalid, treat as null
    - Validate date range — if both `dateFrom` and `dateTo` are provided and `dateFrom > dateTo`, treat both as null
    - Build `purchaseQuery` from `baseQuery` by appending filter predicates: description `.Contains()`, categoryId equality, dateFrom `>=`, dateTo `<=`
    - Pass `purchaseQuery` (not `baseQuery`) to `GetPurchasesPageAsync`
    - Fetch active expense categories for the current business sorted alphabetically by name
    - Populate new ViewModel filter state properties (`FilterDescription`, `FilterCategoryId`, `FilterDateFrom`, `FilterDateTo`, `ExpenseCategories`)
    - _Requirements: 1.3, 1.4, 2.5, 2.6, 3.4, 3.5, 3.6, 3.8, 4.1, 4.6_

  - [ ]* 2.3 Write property test: Description filter returns only matching purchases
    - **Property 1: Description filter returns only matching purchases**
    - **Validates: Requirements 1.3**
    - Create test in `Portal.Tests/PropertyBased/SupplierDashboardPurchaseFilterPropertyTests.cs`
    - Generate random purchase collections with varied descriptions and random filter strings
    - Assert filtered results contain only purchases whose Description contains the filter as case-insensitive substring

  - [ ]* 2.4 Write property test: Category filter returns only purchases with matching category
    - **Property 4: Category filter returns only purchases with matching category**
    - **Validates: Requirements 2.5**
    - Generate random purchases with varied ExpenseCategoryIds and a random valid categoryId
    - Assert filtered results contain only purchases whose ExpenseCategoryId equals the provided value

  - [ ]* 2.5 Write property test: Date range filter bounds are inclusive
    - **Property 5: Date range filter bounds are inclusive**
    - **Validates: Requirements 3.4, 3.5**
    - Generate random DateOnly values for dateFrom/dateTo and random purchase InvoiceDates
    - Assert filtered results contain only purchases with InvoiceDate >= dateFrom AND <= dateTo

  - [ ]* 2.6 Write property test: Filter combination is a logical AND intersection
    - **Property 6: Filter combination is a logical AND intersection**
    - **Validates: Requirements 4.1, 3.8**
    - Generate random multi-filter combinations applied to random purchase sets
    - Assert combined result equals intersection of applying each filter independently

  - [ ]* 2.7 Write property test: Purchase filters do not affect KPIs or charts
    - **Property 7: Purchase filters do not affect KPIs or charts**
    - **Validates: Requirements 4.6**
    - Call `GetDashboardAsync` with and without purchase filters (same supplierId/periodId)
    - Assert TotalSpend, TotalPurchases, AverageMonthlySpend, SpendShareData, MonthlySpendData, PeriodSpendData are identical

- [x] 3. Checkpoint - Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update controller
  - [x] 4.1 Update SupplierController.Dashboard action to accept filter parameters
    - Add `string? description`, `int? categoryId`, `DateOnly? dateFrom`, `DateOnly? dateTo` parameters to the action signature
    - Trim and nullify whitespace-only description values
    - Truncate description to 200 characters max (same pattern as `Index` action)
    - Pass all filter parameters to `_dashboardService.GetDashboardAsync`
    - _Requirements: 1.2, 1.4, 1.5, 1.6, 2.3, 3.2, 3.3, 4.4_

  - [ ]* 4.2 Write property test: Description truncation preserves first 200 characters
    - **Property 2: Description truncation preserves first 200 characters**
    - **Validates: Requirements 1.5**
    - Generate random strings with length 1–500
    - Assert that when length > 200, the applied filter value equals exactly the first 200 characters

  - [ ]* 4.3 Write property test: Category dropdown contains only active categories sorted alphabetically
    - **Property 3: Category dropdown contains only active categories sorted alphabetically**
    - **Validates: Requirements 2.2**
    - Generate random ExpenseCategory records with varied IsActive and BusinessId values
    - Assert the returned list contains exactly those where IsActive=true AND BusinessId matches, ordered alphabetically by Name

  - [ ]* 4.4 Write unit tests for controller and service validation edge cases
    - Test invalid categoryId (non-existent) treated as null
    - Test invalid date range (dateFrom > dateTo) treated as null
    - Test whitespace-only description treated as null
    - Test page resets to 1 when filters are submitted
    - _Requirements: 1.4, 2.6, 3.6, 4.2_

- [x] 5. Checkpoint - Ensure controller compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update the view
  - [x] 6.1 Add filter panel section to Dashboard.cshtml
    - Insert a new `<section class="glass card-pad" style="margin-bottom:22px;">` between the charts section and the purchases table section
    - Add a GET form with hidden `periodId` input (when active), description text input, category dropdown, dateFrom date input, dateTo date input
    - Add "Filter" button (`btn btn-primary`) and "Clear" link (`btn btn-secondary`) that preserves only periodId
    - Use flexbox layout with `gap:14px`, `align-items:flex-end`, `flex-wrap:wrap`
    - Each field in a `.field` wrapper with `min-width:180px`
    - Preserve filter values in inputs using ViewModel properties
    - _Requirements: 1.1, 1.7, 1.8, 2.1, 2.2, 2.8, 3.1, 3.7, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 6.2 Update pagination links to include all active filter parameters
    - Build a pagination query string that includes `periodId`, `description`, `categoryId`, `dateFrom`, `dateTo` when active
    - Update Previous/Next link hrefs to use the full query string with all filter parameters
    - Use `Uri.EscapeDataString` for the description value
    - _Requirements: 1.7, 2.8, 3.7, 4.3_

- [x] 7. Final checkpoint - Ensure all tests pass and application compiles
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- No database migrations are needed — all changes are in application code
- The existing test project (`Portal.Tests`) already has FsCheck.Xunit, xUnit, Moq, and EF Core InMemory configured
- Filter isolation (Property 7) is the most critical correctness guarantee — filters must never affect KPIs/charts

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "2.6", "2.7", "4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2"] }
  ]
}
```
