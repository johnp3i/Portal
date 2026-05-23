# Implementation Plan: Supplier Table Search & Paging

## Overview

This plan implements server-side pagination and name-based search for the Supplier Index page (`/Supplier`). The implementation follows the existing MVC + Service + Repository pattern, reusing the shared `PagedResult<T>` model and `_PagingControl.cshtml` partial view established in the table-paging-search spec. The existing `GetSuppliersAsync()` method is preserved for backward compatibility (used by supplier dropdown in Purchase creation).

## Tasks

- [x] 1. Add paged query method to SupplierRepository
  - [x] 1.1 Add `GetPagedByBusinessIdAsync` method to `Portal.Infrastructure/Repositories/SupplierRepository.cs`
    - Implement SQL query with `OFFSET`/`FETCH NEXT`, `COUNT(*) OVER()`, and parameterized `@SearchTerm` filter
    - Use full table names (no aliases) per repository standards
    - Escape SQL wildcards (`%`, `_`, `[`) in search term before passing to `LIKE`
    - Apply case-insensitive `LIKE` filter on `[purchase].[Supplier].[Name]` when search term is provided
    - Order by `[purchase].[Supplier].[Name] ASC`
    - Use `DataReader` pattern (matching InvoiceRepository)
    - Return `(List<Supplier> Items, int TotalCount)` tuple
    - Preserve existing `GetAllByBusinessIdAsync` method unchanged
    - _Requirements: 1.1, 1.2, 1.9, 2.2, 2.3_

  - [ ]* 1.2 Write property test for search filter correctness
    - **Property 3: Search Filter Correctness**
    - Generate random supplier datasets (with random names) and random search terms, apply case-insensitive contains filter, verify all returned items match and no matching items are excluded from the complete filtered result set
    - **Validates: Requirements 1.9, 2.2**

- [x] 2. Update service layer with paged method
  - [x] 2.1 Add `GetSuppliersPagedAsync` method to `ISupplierService` interface in `Portal.Infrastructure/Services/ISupplierService.cs`
    - Add method signature: `Task<PagedResult<Supplier>> GetSuppliersPagedAsync(string? searchTerm = null, int page = 1, int pageSize = 15)`
    - Preserve all existing method signatures unchanged
    - _Requirements: 1.1, 1.2_

  - [x] 2.2 Implement `GetSuppliersPagedAsync` in `Portal.Infrastructure/Services/SupplierService.cs`
    - Clamp page to minimum 1; clamp pageSize to range [1, 100] defaulting to 15
    - Compute offset as `(page - 1) * pageSize`
    - Call repository `GetPagedByBusinessIdAsync` and wrap result in `PagedResult<Supplier>`
    - If requested page exceeds total pages and total count > 0, re-query with offset 0 and set CurrentPage to 1
    - _Requirements: 1.1, 1.2, 1.3, 1.9_

  - [ ]* 2.3 Write property test for paging metadata correctness
    - **Property 2: Paging Metadata Correctness**
    - Generate random `(totalCount, pageSize, currentPage)` tuples and verify `TotalPages == ⌈N/S⌉`, `HasPreviousPage == (CurrentPage > 1)`, `HasNextPage == (CurrentPage < TotalPages)`
    - **Validates: Requirements 1.4, 1.7, 1.8**

- [x] 3. Checkpoint - Ensure infrastructure and service layers compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update controller to accept paging and search parameters
  - [x] 4.1 Update `SupplierController.Index` in `Portal.Web/Controllers/SupplierController.cs`
    - Change signature to `Index(string? search, int page = 1)`
    - Trim and nullify empty/whitespace search; truncate to 200 characters max
    - Call `GetSuppliersPagedAsync` instead of `GetSuppliersAsync`
    - Set ViewData keys: `CurrentPage`, `TotalPages`, `TotalCount`, `PageSize`, `HasPreviousPage`, `HasNextPage`, `SearchTerm`
    - Return `View(pagedResult)` with `PagedResult<Supplier>` as the model
    - _Requirements: 1.1, 1.2, 1.11, 2.2, 2.4, 3.1, 3.2, 3.3_

- [x] 5. Update Supplier Index view with search filter and paging control
  - [x] 5.1 Update `Portal.Web/Views/Supplier/Index.cshtml` to use `PagedResult<Supplier>` model and add filter panel
    - Change `@model` from `List<Supplier>` to `PagedResult<Portal.Infrastructure.Entities.Supplier>`
    - Add filter panel section (`.glass.card-pad` with `margin-bottom:22px`) above the data table card
    - Filter panel contains a `<form method="get" action="/Supplier">` with search input (`min-width:180px`, `maxlength="200"`) and Search/Clear buttons
    - Filter panel uses flexbox layout with `gap:14px`, `align-items:flex-end`, `flex-wrap:wrap`; buttons wrapped in container with `padding-bottom:2px`
    - Pre-populate search input from `ViewData["SearchTerm"]`
    - Clear button links to `/Supplier` (removes all query params)
    - Iterate over `Model.Items` instead of `Model`
    - Render `@await Html.PartialAsync("_PagingControl")` below the table within the data table card
    - Update empty state message to reflect search context ("No suppliers found for the current search.")
    - Preserve existing modal, scripts, and AJAX functionality unchanged
    - _Requirements: 1.4, 1.5, 1.6, 1.7, 1.8, 1.10, 1.11, 1.12, 2.1, 2.4, 2.5, 2.6, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 5.2 Write property test for correct page slice
    - **Property 1: Correct Page Slice**
    - Generate random lists of suppliers (varying sizes 0–500) and valid (page, pageSize) values, apply pagination logic, verify the returned items match positions `[(P-1)*S .. min(P*S, totalCount)-1]` in the name-sorted dataset
    - **Validates: Requirements 1.1, 1.2**

  - [ ]* 5.3 Write property test for URL query string filter state preservation
    - **Property 4: URL Query String Preserves Filter State**
    - Generate random search terms and page numbers, build URL query string, parse it back, verify the search parameter is preserved across page navigation
    - **Validates: Requirements 1.11, 3.1**

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck (already in Portal.Tests project)
- The existing `GetSuppliersAsync()` and `GetActiveSuppliersAsync()` methods are preserved for backward compatibility — the new paged method is added alongside them
- SQL queries use full table names with schema prefixes per repository standards (no short aliases)
- The shared `PagedResult<T>` model and `_PagingControl.cshtml` partial view already exist and require no changes

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["2.3", "4.1"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3"] }
  ]
}
```
