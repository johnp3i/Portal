# Implementation Plan: Table Paging & Search

## Overview

This plan implements server-side pagination and text search for the Invoice and Quotation list pages, fixes the filter dropdown chevron positioning, and ensures filter state is preserved across page navigation. The implementation follows the existing MVC + Service + Repository pattern, adding a shared `PagedResult<T>` model and a reusable `_PagingControl.cshtml` partial view.

## Tasks

- [x] 1. Create shared PagedResult model and update infrastructure
  - [x] 1.1 Create the `PagedResult<T>` generic model in `Portal.Infrastructure/Models/PagedResult.cs`
    - Implement `Items`, `CurrentPage`, `PageSize`, `TotalCount`, computed `TotalPages`, `HasPreviousPage`, `HasNextPage`
    - _Requirements: 1.3, 1.6, 1.7, 2.3, 2.6, 2.7_

  - [ ]* 1.2 Write property test for PagedResult metadata correctness
    - **Property 2: Paging Metadata Correctness**
    - Generate random (totalCount, pageSize, currentPage) tuples and verify `TotalPages == ⌈N/S⌉`, `HasPreviousPage == (CurrentPage > 1)`, `HasNextPage == (CurrentPage < TotalPages)`
    - **Validates: Requirements 1.3, 1.6, 1.7, 2.3, 2.6, 2.7**

- [x] 2. Implement Invoice paging and search in the repository layer
  - [x] 2.1 Add `GetPagedByBusinessIdAsync` method to `Portal.Infrastructure/Repositories/InvoiceRepository.cs`
    - Implement SQL query with `OFFSET`/`FETCH NEXT`, `COUNT(*) OVER()`, and parameterized `@SearchTerm`, `@StatusFilter`, `@FinancialStatusFilter`, `@CustomerFilter` filters
    - Use full table names (no aliases) per repository standards
    - Escape SQL wildcards in search term
    - Return `(List<InvoiceListDto> Items, int TotalCount)` tuple
    - _Requirements: 1.1, 1.2, 1.8, 3.2, 3.3, 3.5_

- [x] 3. Implement Quotation paging and search in the repository layer
  - [x] 3.1 Add `GetPagedByBusinessIdAsync` method to `Portal.Infrastructure/Repositories/QuotationRepository.cs`
    - Implement SQL query with `OFFSET`/`FETCH NEXT`, `COUNT(*) OVER()`, and parameterized `@SearchTerm`, `@StatusFilter`, `@CustomerFilter`, `@DateFrom`, `@DateTo` filters
    - Use full table names (no aliases) per repository standards
    - Escape SQL wildcards in search term
    - Return `(List<QuotationListDto> Items, int TotalCount)` tuple
    - _Requirements: 2.1, 2.2, 2.8, 4.2, 4.3, 4.5_

- [x] 4. Update service layer with paged methods
  - [x] 4.1 Add `GetInvoicesPagedAsync` method to `IInvoiceService` and `InvoiceService`
    - Accept `statusFilter`, `financialStatusFilter`, `customerFilter`, `searchTerm`, `page`, `pageSize` parameters
    - Compute offset from page/pageSize, clamp page to valid range
    - Call repository `GetPagedByBusinessIdAsync` and wrap result in `PagedResult<InvoiceListDto>`
    - _Requirements: 1.1, 1.2, 1.8, 1.9, 3.2, 3.5_

  - [x] 4.2 Add `GetQuotationsPagedAsync` method to `IQuotationService` and `QuotationService`
    - Accept `statusFilter`, `customerFilter`, `dateFrom`, `dateTo`, `searchTerm`, `page`, `pageSize` parameters
    - Compute offset from page/pageSize, clamp page to valid range
    - Call repository `GetPagedByBusinessIdAsync` and wrap result in `PagedResult<QuotationListDto>`
    - _Requirements: 2.1, 2.2, 2.8, 2.9, 4.2, 4.5_

- [x] 5. Checkpoint - Ensure infrastructure and service layers compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update controllers to accept paging and search parameters
  - [x] 6.1 Update `InvoiceController.Index` to accept `search` and `page` parameters
    - Change signature to `Index(int? status, int? financialStatus, int? customer, string? search, int page = 1)`
    - Call `GetInvoicesPagedAsync` instead of `GetInvoicesAsync`
    - Pass `PagedResult`, `SearchTerm`, and all filter values to ViewBag
    - _Requirements: 1.1, 1.2, 1.9, 3.4, 6.1, 6.2_

  - [x] 6.2 Update `QuotationController.Index` to accept `search` and `page` parameters
    - Change signature to `Index(int? status, int? customer, DateTime? dateFrom, DateTime? dateTo, string? search, int page = 1)`
    - Call `GetQuotationsPagedAsync` instead of `GetQuotationsAsync`
    - Update `QuotationListViewModel` to include `PagedQuotations` (as `PagedResult<QuotationListDto>`) and `SearchTerm`
    - _Requirements: 2.1, 2.2, 2.9, 4.4, 6.1, 6.3_

- [x] 7. Create shared paging partial view and update list views
  - [x] 7.1 Create `_PagingControl.cshtml` partial in `Portal.Web/Views/Shared/`
    - Render "Previous" button (disabled when `HasPreviousPage` is false)
    - Render page number indicators
    - Render "Next" button (disabled when `HasNextPage` is false)
    - Render "Showing X–Y of Z records" text
    - Accept all current filter values and search term to build navigation URLs with query string preservation
    - Style using MyChair Design System (Primary Blue buttons, border-radius, Manrope/Inter fonts)
    - _Requirements: 1.3, 1.4, 1.5, 1.6, 1.7, 2.3, 2.4, 2.5, 2.6, 2.7, 6.1_

  - [x] 7.2 Update `Views/Invoice/Index.cshtml` to include search input and paging control
    - Change `@model` from `List<InvoiceListDto>` to use ViewBag-based `PagedResult`
    - Add search input field above the table within the filter form
    - Render `_PagingControl` partial below the table
    - Pre-populate search input from ViewBag on page load
    - _Requirements: 3.1, 3.4, 1.3, 6.2_

  - [x] 7.3 Update `Views/Quotation/Index.cshtml` to include search input and paging control
    - Add `SearchTerm` property usage from updated `QuotationListViewModel`
    - Add search input field above the table within the filter form
    - Render `_PagingControl` partial below the table
    - Iterate over `Model.PagedQuotations.Items` instead of `Model.Quotations`
    - Pre-populate search input from model on page load
    - _Requirements: 4.1, 4.4, 2.3, 6.3_

- [x] 8. Fix filter dropdown arrow (chevron) CSS positioning
  - [x] 8.1 Update `.field select` styles in `Portal.Web/wwwroot/css/site.css`
    - Add `appearance: none; -webkit-appearance: none; -moz-appearance: none;`
    - Add custom SVG chevron via `background-image` positioned `right 14px center`
    - Add `padding-right: 38px` to prevent text overlap with chevron
    - Add `max-width: 100%` to constrain within grid column
    - Ensure chevron remains vertically centered
    - Add responsive rule for screens narrower than 1100px to stack filters vertically
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 9. Checkpoint - Ensure full application compiles and renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 10. Write property tests for pagination and search logic
  - [ ]* 10.1 Write property test for correct page slice
    - **Property 1: Correct Page Slice**
    - Generate random lists of DTOs and valid (page, pageSize) values, apply pagination logic, verify the returned items match positions `[(P-1)*S .. min(P*S, totalCount)-1]` in the ordered dataset
    - **Validates: Requirements 1.1, 1.2, 2.1, 2.2**

  - [ ]* 10.2 Write property test for search filter correctness
    - **Property 3: Search Filter Correctness**
    - Generate random datasets and search terms, verify every returned record contains the search term (case-insensitive) in invoice number/customer name (or reference/customer name for quotations), and no matching record is excluded
    - **Validates: Requirements 3.2, 3.3, 4.2, 4.3**

  - [ ]* 10.3 Write property test for combined filters with AND logic
    - **Property 4: Combined Filters with AND Logic**
    - Generate random filter combinations (status, financial status, customer, date range, search term), verify results satisfy ALL predicates simultaneously
    - **Validates: Requirements 1.8, 2.8, 3.5, 4.5, 1.9, 2.9**

  - [ ]* 10.4 Write property test for URL query string filter state preservation
    - **Property 5: URL Query String Preserves Filter State**
    - Generate random filter state (status, customer, search, page), build URL query string, parse it back, verify all values round-trip correctly
    - **Validates: Requirements 6.1, 6.2, 6.3**

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck (already in Portal.Tests project)
- Unit tests validate specific examples and edge cases
- The existing `GetInvoicesAsync` and `GetQuotationsAsync` methods are preserved for backward compatibility — new paged methods are added alongside them
- SQL queries use full table names with schema prefixes per repository standards (no short aliases)
- The filter dropdown CSS fix is independent of the paging/search work and can be done in parallel

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "8.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "3.1"] },
    { "id": 2, "tasks": ["4.1", "4.2"] },
    { "id": 3, "tasks": ["6.1", "6.2"] },
    { "id": 4, "tasks": ["7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3"] },
    { "id": 6, "tasks": ["10.1", "10.2", "10.3", "10.4"] }
  ]
}
```
