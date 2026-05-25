# Implementation Plan: Product Catalog

## Overview

This plan implements the Product Catalog module for the Portal application. It introduces a `[product]` schema with `Product` and `ProductPriceHistory` tables, a full management UI with search/KPIs/charting, autocomplete integration into Invoice and Quotation line item forms, and auto-population logic that grows the catalog organically. The implementation follows the established MVC + Service Layer pattern with Database-First EF Core, consistent with all existing Portal modules.

## Tasks

- [x] 1. Database schema and migrations
  - [x] 1.1 Create migration script 051_CreateProductSchema.sql
    - Create the `[product]` schema if it does not already exist
    - _Requirements: 1.1_

  - [x] 1.2 Create migration script 052_CreateProductTable.sql
    - Create `[product].[Product]` table with all columns: Id, BusinessId, ProductCode, Description, DefaultSellingPrice, DefaultCostPrice, DefaultVatRate, SupplierId, IsActive, LastUsedDate, CreatedAtUtc
    - Add PK, FK constraints (Business, Supplier with ON DELETE SET NULL), unique constraint on (BusinessId, ProductCode)
    - Add CHECK constraints for DefaultSellingPrice >= 0, DefaultCostPrice >= 0, DefaultVatRate BETWEEN 0.00 AND 99.99
    - Add nonclustered indexes: IX_Product_BusinessId, IX_Product_BusinessId_ProductCode
    - _Requirements: 1.2, 1.3, 1.9, 1.10_

  - [x] 1.3 Create migration script 053_CreateProductPriceHistoryTable.sql
    - Create `[product].[ProductPriceHistory]` table with all columns: Id, ProductId, SellingPrice, CostPrice, EffectiveFromUtc, ChangedByUserId, CreatedAtUtc
    - Add PK, FK constraint (ProductId → Product with ON DELETE CASCADE)
    - Add nonclustered index: IX_ProductPriceHistory_ProductId
    - _Requirements: 1.4, 1.5, 1.11_

  - [x] 1.4 Create migration script 054_AddProductCodeToInvoiceLine.sql
    - Add nullable `ProductCode NVARCHAR(50) NULL` column to `[invoice].[InvoiceLine]`
    - _Requirements: 1.7_

  - [x] 1.5 Create migration script 055_AddProductCodeToQuotationLine.sql
    - Add nullable `ProductCode NVARCHAR(50) NULL` column to `[quotation].[QuotationLine]`
    - _Requirements: 1.8_

  - [x] 1.6 Create migration script 056_MigrateLineItemCatalogToProduct.sql
    - Insert records from `[quotation].[LineItemCatalog]` into `[product].[Product]`
    - Map Description, UnitPrice → DefaultSellingPrice, VatRate → DefaultVatRate, DefaultCostPrice = 0.00
    - Generate sequential ProductCode per business (e.g., "MIGRATED-001", "MIGRATED-002")
    - Preserve CreatedAtUtc (fallback to UpdatedAtUtc)
    - Ensure no duplicate ProductCode values within a BusinessId
    - Retain the LineItemCatalog table in deprecated state (no deletion)
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 2. Entity models and DbContext configuration
  - [x] 2.1 Create Product entity class
    - Create `Portal.Infrastructure/Entities/Product.cs` with all properties matching the database schema
    - Include navigation properties: Business, Supplier, PriceHistory collection
    - _Requirements: 1.2_

  - [x] 2.2 Create ProductPriceHistory entity class
    - Create `Portal.Infrastructure/Entities/ProductPriceHistory.cs` with all properties matching the database schema
    - Include navigation property: Product
    - _Requirements: 1.4_

  - [x] 2.3 Register entities in PortalDbContext
    - Add `DbSet<Product>` and `DbSet<ProductPriceHistory>` to `PortalDbContext`
    - Configure entity mappings: schema `product`, table names, column types, constraints, relationships
    - Configure unique index on (BusinessId, ProductCode)
    - Configure cascade delete from Product to ProductPriceHistory
    - Set `CreatedAtUtc` default value via `HasDefaultValueSql("GETUTCDATE()")`
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

  - [x] 2.4 Add ProductCode property to InvoiceLine and QuotationLine entities
    - Add nullable `string? ProductCode` property to existing `InvoiceLine` entity
    - Add nullable `string? ProductCode` property to existing `QuotationLine` entity
    - Update DbContext configuration for the new columns
    - _Requirements: 1.7, 1.8_

- [x] 3. Checkpoint - Verify schema and entity setup
  - Ensure all migration scripts are syntactically correct and entities compile. Ask the user if questions arise.

- [x] 4. Repository layer
  - [x] 4.1 Create ProductRepository
    - Create `Portal.Infrastructure/Repositories/ProductRepository.cs`
    - Implement methods: GetPagedByBusinessIdAsync, GetByIdAndBusinessIdAsync, GetByProductCodeAndBusinessIdAsync, GetByDescriptionAndBusinessIdAsync, InsertAsync, UpdateAsync, DeactivateAsync, GetKpiDataAsync, GetTopByUsageAsync, SearchForAutocompleteAsync
    - Use raw SQL with full table names (no aliases), SqlParameter for all parameters, null-safe with `?? (object)DBNull.Value`
    - Follow try/catch with rethrow pattern
    - _Requirements: 1.2, 1.9, 1.10, 2.1, 2.4, 2.6, 3.3, 3.5, 3.6, 3.7, 4.1_

  - [x] 4.2 Create ProductPriceHistoryRepository
    - Create `Portal.Infrastructure/Repositories/ProductPriceHistoryRepository.cs`
    - Implement methods: InsertAsync, GetByProductIdAsync
    - Follow established repository patterns with try/catch rethrow
    - _Requirements: 1.4, 1.6, 2.5, 2.8, 8.1_

- [x] 5. Service layer - Core product management
  - [x] 5.1 Create IProductService interface and ProductService implementation
    - Create `Portal.Infrastructure/Services/IProductService.cs` interface
    - Create `Portal.Infrastructure/Services/ProductService.cs` implementation
    - Inject ICurrentTenantService, ProductRepository, ProductPriceHistoryRepository, SupplierRepository, ILogger
    - Implement CreateProductAsync: validate inputs, check duplicate ProductCode, validate SupplierId belongs to same business, insert product, insert initial price history record
    - Implement UpdateProductAsync: validate inputs, check product belongs to business, update product, insert price history if prices changed
    - Implement DeactivateProductAsync: set IsActive to false
    - Implement GetProductByIdAsync: retrieve by id scoped to business
    - Implement GetProductsPagedAsync: search by ProductCode/Description, paginate with page size 15
    - Implement GetKpisAsync: return Total Products, Active Products, Average Selling Price, Best Seller
    - Implement GetTopProductsByUsageAsync: return top N products by usage count
    - Implement GetPriceHistoryAsync: return price history ordered by EffectiveFromUtc descending
    - All queries filtered by CurrentBusinessId for tenant isolation
    - Return ServiceResult for create/update/deactivate operations
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 3.3, 3.5, 3.6, 3.7, 7.1, 7.2, 7.5, 7.6, 7.7, 8.1_

  - [x] 5.2 Write property tests for product creation (Properties 1, 2, 3, 5)
    - **Property 1: Product creation persists with correct defaults**
    - **Property 2: Duplicate ProductCode rejection**
    - **Property 3: Invalid input rejection**
    - **Property 5: Product creation includes initial price history**
    - **Validates: Requirements 2.1, 2.3, 2.7, 2.8**

  - [x] 5.3 Write property tests for product update and deactivation (Properties 4, 6)
    - **Property 4: Price update creates history record**
    - **Property 6: Deactivation sets IsActive to false**
    - **Validates: Requirements 1.6, 2.5, 2.6, 5.8**

  - [x] 5.4 Write property tests for tenant isolation (Property 20)
    - **Property 20: Tenant isolation**
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.6, 7.7**

- [x] 6. Service layer - Autocomplete
  - [x] 6.1 Create IProductAutocompleteService interface and ProductAutocompleteService implementation
    - Create `Portal.Infrastructure/Services/IProductAutocompleteService.cs` interface
    - Create `Portal.Infrastructure/Services/ProductAutocompleteService.cs` implementation
    - Inject ICurrentTenantService, ProductRepository, InvoiceLineRepository, QuotationLineRepository, ILogger
    - Implement SearchAsync: search Product table by ProductCode/Description (case-insensitive partial match), search InvoiceLine/QuotationLine history, combine results sorted by most recent date, limit to 20 results
    - Return empty array on exception (suppress errors, log them)
    - Enforce minimum 2-character query length (return empty for shorter queries)
    - All queries scoped to authenticated BusinessId
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.9, 4.11, 7.3, 7.4_

  - [x] 6.2 Write property tests for autocomplete (Properties 11, 12, 13, 14)
    - **Property 11: Autocomplete minimum query length**
    - **Property 12: Autocomplete result completeness**
    - **Property 13: Autocomplete results sorted by most recent date**
    - **Property 14: Autocomplete result limit**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6**

- [x] 7. Service layer - Auto-population logic
  - [x] 7.1 Implement AutoPopulateFromLineItemAsync in ProductService
    - Implement matching priority: first by ProductCode (case-insensitive), then by Description (case-insensitive exact match)
    - On match: update LastUsedDate only (do NOT update prices)
    - On no match with ProductCode present: create new Product with DefaultCostPrice=0.00, insert initial price history
    - On no match without ProductCode: take no action
    - Wrap in try/catch: log failures, never throw (fire-and-forget pattern)
    - Execute after line item has been persisted
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.9, 5.10_

  - [x] 7.2 Write property tests for auto-population (Properties 15, 16, 17, 18, 19)
    - **Property 15: Auto-population matching priority**
    - **Property 16: LastUsedDate update on match**
    - **Property 17: Auto-creation when no match exists and ProductCode is present**
    - **Property 18: No auto-creation without ProductCode**
    - **Property 19: Existing product prices preserved on auto-population match**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5, 5.7**

- [x] 8. Checkpoint - Verify service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. DTOs and ViewModels
  - [x] 9.1 Create DTO and ViewModel classes
    - Create `Portal.Infrastructure/Models/ProductKpiDto.cs`
    - Create `Portal.Infrastructure/Models/ProductUsageDto.cs`
    - Create `Portal.Infrastructure/Models/AutocompleteResultDto.cs`
    - Create `Portal.Infrastructure/Models/PagedResult.cs` (generic, if not already existing)
    - _Requirements: 3.6, 3.7, 4.2, 4.3, 4.4_

- [x] 10. Module registration and DI wiring
  - [x] 10.1 Register Products module constant and DI services
    - Add `public const string Products = "products";` to `PortalModules.cs` and update the `All` array
    - Register `IProductService` → `ProductService` in DI container
    - Register `IProductAutocompleteService` → `ProductAutocompleteService` in DI container
    - Register `ProductRepository` and `ProductPriceHistoryRepository` in DI container
    - _Requirements: 3.1, 7.1_

- [x] 11. Controller layer
  - [x] 11.1 Create ProductController with Index action
    - Create `Portal.Web/Controllers/ProductController.cs`
    - Apply `[Authorize]` and `[ModuleAccess(PortalModules.Products)]` attributes
    - Implement Index action: retrieve paginated products, KPIs, top products by usage, pass to view
    - Support search query parameter for filtering
    - Support page parameter for pagination
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x] 11.2 Implement CRUD AJAX endpoints on ProductController
    - Implement Create (POST): accept form data, call ProductService.CreateProductAsync, return JSON `{ success, message }`
    - Implement Edit (POST): accept form data, call ProductService.UpdateProductAsync, return JSON `{ success, message }`
    - Implement Deactivate (POST): accept productId, call ProductService.DeactivateProductAsync, return JSON `{ success, message }`
    - Implement GetProduct (GET): return product JSON for edit form pre-population
    - Implement PriceHistory (GET): return partial view or JSON with price history for a product
    - All endpoints validate antiforgery token on POST
    - _Requirements: 2.1, 2.4, 2.6, 3.8, 3.9, 3.10, 8.1, 8.2, 8.3_

  - [x] 11.3 Implement Autocomplete API endpoint
    - Add `[HttpGet] Autocomplete` action to ProductController (or a shared API controller)
    - Accept query string parameter, call ProductAutocompleteService.SearchAsync
    - Return JSON array of AutocompleteResultDto
    - Return empty array on exception (suppress errors)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.11_

- [x] 12. Views and UI
  - [x] 12.1 Create Products Index view (Products.cshtml)
    - Create `Portal.Web/Views/Product/Index.cshtml`
    - Implement topbar with eyebrow label, heading (42px), and muted description
    - Implement 4 KPI cards: Total Products, Active Products, Average Selling Price, Best Seller
    - Implement bar chart (top 10 products by usage) using Chart.js or equivalent
    - Implement search input with filter functionality
    - Implement product table with columns: ProductCode, Description, DefaultSellingPrice, DefaultCostPrice, DefaultVatRate, Supplier Name, IsActive status, LastUsedDate
    - Implement pagination (page size 15) with "Showing X-Y of Z" info
    - Implement Edit and Deactivate action buttons per row
    - Implement Create Product button opening a modal form
    - Follow MyChair Design System: glass card-pad sections, layout-standards spacing, Manrope/Inter fonts
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10_

  - [x] 12.2 Create Product form modal (Create/Edit)
    - Implement modal form with fields: ProductCode (required), Description (required), DefaultSellingPrice, DefaultCostPrice, DefaultVatRate, Supplier dropdown (active suppliers), IsActive toggle
    - Pre-populate fields on Edit
    - Include price history section on Edit (ordered by EffectiveFromUtc descending)
    - Show "No price changes recorded" when no history exists
    - Use SweetAlert2 for deactivate confirmation dialog
    - Use BlockUI.show/hide pattern for all AJAX calls
    - Use fetch API with antiforgery token for POST requests
    - _Requirements: 3.8, 3.9, 3.10, 8.1, 8.2, 8.3_

  - [x] 12.3 Add Products link to sidebar navigation
    - Add navigation entry for Products module in the sidebar partial view
    - Ensure it respects module access permissions
    - _Requirements: 3.1_

- [x] 13. Autocomplete integration into Invoice and Quotation forms
  - [x] 13.1 Implement autocomplete JavaScript for line item forms
    - Add autocomplete behavior to Description/ProductCode fields on Invoice and Quotation line item forms
    - Trigger search after 2+ characters with 300ms debounce
    - Display dropdown with product results (ProductCode, Description, Price, Supplier) and historical results (Description, UnitPrice, Date, Source indicator)
    - On selection: auto-fill Description, UnitPrice, VatRate, CostPrice, ProductCode
    - Hide dropdown when no matches found
    - Suppress errors gracefully (no UI disruption)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [x] 13.2 Wire auto-population into Invoice and Quotation save flows
    - After InvoiceLine is persisted, call `ProductService.AutoPopulateFromLineItemAsync` with the line item's ProductCode, Description, UnitPrice, VatRate, and authenticated userId
    - After QuotationLine is persisted, call `ProductService.AutoPopulateFromLineItemAsync` with the same parameters
    - Ensure auto-population runs after successful line item persistence (not before)
    - Ensure failures are logged but do not roll back the line item
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.9, 5.10_

- [x] 14. Checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Search, pagination, and KPI property tests
  - [x] 15.1 Write property tests for search and pagination (Properties 7, 8)
    - **Property 7: Search filter correctness**
    - **Property 8: Pagination correctness**
    - **Validates: Requirements 3.3, 3.4, 3.5**

  - [x] 15.2 Write property tests for KPIs and ordering (Properties 9, 10, 21)
    - **Property 9: KPI calculation correctness**
    - **Property 10: Top products by usage ordering**
    - **Property 21: Price history ordered descending**
    - **Validates: Requirements 3.6, 3.7, 8.1**

- [x] 16. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit
- Unit tests validate specific examples and edge cases
- The implementation uses C# / ASP.NET Core MVC 8 with SQL Server and Entity Framework Core (Database-First)
- All repositories follow the established try/catch rethrow pattern with full table names in SQL (no aliases)
- All UI follows MyChair Design System conventions (SweetAlert2, BlockUI, fetch API, glass card-pad layout)
- Tenant isolation is enforced via ICurrentTenantService.CurrentBusinessId on every query

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["1.4", "1.5", "1.6", "2.1", "2.2"] },
    { "id": 3, "tasks": ["2.3", "2.4"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["5.1", "9.1"] },
    { "id": 6, "tasks": ["5.2", "5.3", "5.4", "6.1"] },
    { "id": 7, "tasks": ["6.2", "7.1"] },
    { "id": 8, "tasks": ["7.2", "10.1"] },
    { "id": 9, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 11, "tasks": ["13.1", "13.2"] },
    { "id": 12, "tasks": ["15.1", "15.2"] }
  ]
}
```
