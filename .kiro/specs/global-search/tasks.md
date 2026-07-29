# Implementation Plan: Global Search

## Overview

Implement a cross-module global search feature embedded in the topbar. The backend consists of DTOs, a service interface, a service implementation with parallel per-entity queries, and a controller endpoint. The frontend adds a debounced search bar with a keyboard-navigable dropdown to the shared layout. Registration in DI and permission filter exemption wire everything together.

## Tasks

- [x] 1. Create DTOs and service interface
  - [x] 1.1 Create GlobalSearchResultDto, SearchResultGroup, and SearchResultItem DTOs
    - Create file `Portal.Infrastructure/Services/GlobalSearchResultDto.cs`
    - Define `GlobalSearchResultDto` with `List<SearchResultGroup> Groups`
    - Define `SearchResultGroup` with `Type`, `Label`, and `List<SearchResultItem> Items`
    - Define `SearchResultItem` with `Id`, `Primary`, `Secondary`, `Url`
    - _Requirements: 9.1, 10.1, 10.2_

  - [x] 1.2 Create IGlobalSearchService interface
    - Create file `Portal.Infrastructure/Services/IGlobalSearchService.cs`
    - Define `Task<GlobalSearchResultDto> SearchAsync(string query, int businessId, HashSet<string> permittedModules)`
    - _Requirements: 4.3, 7.1_

- [x] 2. Implement GlobalSearchService
  - [x] 2.1 Implement GlobalSearchService with SearchAsync orchestrator
    - Create file `Portal.Infrastructure/Services/GlobalSearchService.cs`
    - Inject `PortalDbContext`
    - Implement `SearchAsync` that checks permitted modules and runs `Task.WhenAll` for parallel execution
    - Aggregate non-null, non-empty groups into the result DTO
    - _Requirements: 5.1, 6.1, 6.2, 7.1, 7.2, 7.3_

  - [x] 2.2 Implement SearchInvoicesAsync
    - Query `Invoices` filtered by `BusinessId`, `!IsDeleted`, LIKE on `InvoiceNumber` and `Customer.Name`
    - Take 5, select into `SearchResultItem` with URL `/Invoice/Detail/{id}`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.1, 9.2, 5.1_

  - [x] 2.3 Implement SearchCustomersAsync
    - Query `Customers` filtered by `BusinessId`, `IsActive`, LIKE on `Name` and `Email`
    - Take 5, select into `SearchResultItem` with URL `/Customer/Detail/{id}`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.2, 9.2, 5.1_

  - [x] 2.4 Implement SearchPurchasesAsync
    - Query `Purchases` filtered by `BusinessId`, `!IsCancelled`, LIKE on `InvoiceNumber`, `Description`, `Supplier.Name`
    - Take 5, select into `SearchResultItem` with URL `/Purchase/Edit/{id}`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.3, 9.2, 5.1_

  - [x] 2.5 Implement SearchQuotationsAsync
    - Query `Quotations` filtered by `BusinessId`, `!IsDeleted`, LIKE on `Reference` and `Customer.Name`
    - Take 5, select into `SearchResultItem` with URL `/Quotation/Detail/{id}`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.4, 9.2, 5.1_

  - [x] 2.6 Implement SearchSuppliersAsync
    - Query `Suppliers` filtered by `BusinessId`, `IsActive`, LIKE on `Name`
    - Take 5, select into `SearchResultItem` with URL `/Supplier/Dashboard/{id}`
    - Gate behind `PortalModules.Purchase`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.5, 9.2, 5.1_

  - [x] 2.7 Implement SearchProductsAsync
    - Query `Products` filtered by `BusinessId`, `IsActive`, LIKE on `Description` and `ProductCode`
    - Take 5, select into `SearchResultItem` with URL `/Product/Edit/{id}`
    - Gate behind `PortalModules.Products`
    - Wrap in try/catch returning null on failure
    - _Requirements: 8.6, 9.2, 5.1_

  - [ ]* 2.8 Write property tests for GlobalSearchService
    - **Property 1: Short query rejection** — verify empty result for queries < 2 chars
    - **Property 2: Tenant isolation** — verify no cross-business results
    - **Property 3: Module permission filtering** — verify only permitted groups returned
    - **Property 4: Search field matching** — verify returned items match on designated fields
    - **Property 5: Result limit invariant** — verify max 5 items per group
    - **Property 6: Empty groups omitted** — verify no zero-item groups in response
    - **Property 7: Fault tolerance** — verify partial results on query failure
    - **Validates: Requirements 4.2, 5.1, 5.2, 6.2, 6.3, 7.3, 8.1-8.6, 9.1, 9.2, 10.3**

- [x] 3. Checkpoint - Ensure service layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement SearchController
  - [x] 4.1 Create SearchController with AxGetGlobalSearch endpoint
    - Create file `Portal.Web/Controllers/SearchController.cs`
    - Add `[Authorize]` attribute at class level
    - Inject `IGlobalSearchService` and `ICurrentTenantService`
    - Implement `[HttpGet] AxGetGlobalSearch(string? query)` that validates query length, reads business ID and plan permissions, delegates to service
    - Return `Json(new { success = true, data = result })` on success
    - Return `Json(new { success = false, message = "..." })` on exception
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 6.1_

  - [ ]* 4.2 Write unit tests for SearchController
    - Test empty/short query returns empty groups
    - Test valid query delegates to service and returns result
    - Test exception returns `success: false`
    - _Requirements: 4.2, 4.3, 4.4_

- [x] 5. Register in DI and configure permissions
  - [x] 5.1 Register GlobalSearchService in Program.cs
    - Add `builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();` in service registration section
    - _Requirements: 4.3_

  - [x] 5.2 Add "Search" to PlanPermissionFilter.NonModuleControllers
    - Locate `PlanPermissionFilter.cs` and add `"Search"` to the `NonModuleControllers` HashSet
    - This allows the controller to be accessed while still populating `HttpContext.Items["PlanPermissions"]`
    - _Requirements: 6.1_

- [x] 6. Implement frontend search bar and dropdown
  - [x] 6.1 Add search bar HTML and dropdown container to _Layout.cshtml
    - Insert search bar with magnifying glass icon, placeholder text, and Ctrl+K hint inside the topbar area
    - Add hidden dropdown panel below the input
    - Style according to design (12px border-radius, Inter font, soft shadow)
    - _Requirements: 1.1, 1.2, 1.3, 13.1_

  - [x] 6.2 Implement JavaScript: debounced search, keyboard navigation, dropdown rendering
    - Implement Ctrl+K focus shortcut
    - Implement 300ms debounce on input
    - Implement `executeSearch` using fetch to `/Search/AxGetGlobalSearch`
    - Render grouped results with escapeHtml for XSS protection
    - Implement arrow key navigation (Up/Down) with highlight wrapping
    - Implement Enter to navigate, Escape to close
    - Implement click-outside dismissal
    - Show loading state, empty state, no-results state, and error state inline (no BlockUI, no alert)
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 11.1, 11.2, 11.3, 11.4, 11.5, 12.1, 12.2, 13.1, 13.2, 13.3, 13.4, 14.1, 14.2, 14.3, 15.1, 15.2, 15.3_

- [x] 7. Final checkpoint - Build verification
  - Run `dotnet build` to verify the solution compiles without errors.
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The design explicitly states no BlockUI for search (uses inline loading indicator) — this is an intentional deviation from the standard AJAX pattern per Requirement 15.1
- Supplier search is gated behind `PortalModules.Purchase` (suppliers belong to the purchase module)
- Product uses `ProductCode` (SKU) and `Description` (Name) as per existing schema
- Quotation uses `Reference` (quotation number) as per existing schema
- `escapeHtml()` in frontend prevents XSS from entity data rendered in dropdown

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7"] },
    { "id": 3, "tasks": ["2.8", "4.1"] },
    { "id": 4, "tasks": ["4.2", "5.1", "5.2"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2"] }
  ]
}
```
