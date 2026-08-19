# Implementation Plan: Product Price Tiers

## Overview

This feature adds named pricing tiers to products (e.g., Retail, Wholesale, VIP), allowing multiple price profiles per product. During quotation/invoice creation, users select a tier from a dropdown and the selected tier's price is snapshotted into the line item. Products without tiers continue working via DefaultSellingPrice/DefaultCostPrice. Implementation proceeds bottom-up: DB migrations → Entity + DbContext → Repository → Service → Controller → Views/JS → Integration with quotation/invoice flows.

## Tasks

- [x] 1. Database migrations
  - [x] 1.1 Create database migration: CreateProductPriceTierTable
    - Create SQL migration script with `USE [Portal]` header
    - Create `[product].[ProductPriceTier]` table with columns: Id (int identity PK), ProductId (int NOT NULL FK), TierName (nvarchar(100) NOT NULL), SellingPrice (decimal(18,2) NOT NULL), CostPrice (decimal(18,2) NOT NULL), IsDefault (bit NOT NULL DEFAULT 0), IsActive (bit NOT NULL DEFAULT 1), CreatedAtUtc (datetime NOT NULL DEFAULT GETUTCDATE()), UpdatedAtUtc (datetime NOT NULL DEFAULT GETUTCDATE())
    - Add PK constraint `PK_ProductPriceTier`, FK constraint `FK_ProductPriceTier_Product` referencing `[product].[Product](Id)`
    - Create filtered unique index `UQ_ProductPriceTier_ActiveName` on (ProductId, TierName) WHERE IsActive = 1
    - Create covering index `IX_ProductPriceTier_ProductId_IsActive` on (ProductId, IsActive) INCLUDE (TierName, SellingPrice, CostPrice, IsDefault)
    - _Requirements: 1.1, 1.2, 2.2, 3.1_

  - [x] 1.2 Create database migration: AddProductPriceTierIdToProductPriceHistory
    - Add nullable `ProductPriceTierId INT NULL` column to `[product].[ProductPriceHistory]`
    - Add FK constraint `FK_ProductPriceHistory_ProductPriceTier` referencing `[product].[ProductPriceTier](Id)`
    - _Requirements: 7.1, 7.3_

  - [x] 1.3 Create database migration: AddPriceTierColumnsToQuotationLine
    - Add nullable `ProductPriceTierId INT NULL` column to `[quotation].[QuotationLine]`
    - Add `PriceTierName NVARCHAR(100) NULL` column to `[quotation].[QuotationLine]`
    - Add FK constraint `FK_QuotationLine_ProductPriceTier` referencing `[product].[ProductPriceTier](Id)`
    - _Requirements: 4.5, 6.4_

  - [x] 1.4 Create database migration: AddPriceTierColumnsToInvoiceLine
    - Add nullable `ProductPriceTierId INT NULL` column to `[invoice].[InvoiceLine]`
    - Add `PriceTierName NVARCHAR(100) NULL` column to `[invoice].[InvoiceLine]`
    - Add FK constraint `FK_InvoiceLine_ProductPriceTier` referencing `[product].[ProductPriceTier](Id)`
    - _Requirements: 4.5, 6.4_

- [ ] 2. Entity and DbContext configuration
  - [-] 2.1 Create ProductPriceTier entity class
    - Create `Portal.Infrastructure.Entities.ProductPriceTier` with properties: Id, ProductId, TierName, SellingPrice, CostPrice, IsDefault, IsActive, CreatedAtUtc, UpdatedAtUtc
    - Add navigation property `Product Product`
    - Register entity in DbContext with `[product].[ProductPriceTier]` table mapping
    - Configure filtered unique index and FK relationships in OnModelCreating
    - _Requirements: 1.1_

  - [-] 2.2 Extend existing entities with tier reference properties
    - Add `ProductPriceTierId` (int?), `ProductPriceTier?` navigation to ProductPriceHistory entity
    - Add `ProductPriceTierId` (int?), `PriceTierName` (string?), `ProductPriceTier?` navigation to QuotationLine entity
    - Add `ProductPriceTierId` (int?), `PriceTierName` (string?), `ProductPriceTier?` navigation to InvoiceLine entity
    - Update DbContext configuration for new columns and FK relationships
    - **CRITICAL:** Update ALL existing repository SELECT statements for QuotationLine to include `[ProductPriceTierId]` and `[PriceTierName]` in the column list (EF FromSqlRaw requires all mapped columns)
    - **CRITICAL:** Update ALL existing repository SELECT statements for InvoiceLine to include `[ProductPriceTierId]` and `[PriceTierName]` in the column list
    - **CRITICAL:** Update ALL existing repository SELECT statements for ProductPriceHistory to include `[ProductPriceTierId]` in the column list
    - _Requirements: 4.5, 6.4, 7.3_

  - [-] 2.3 Create request/response DTOs
    - Create `CreateTierRequest` (ProductId, TierName, SellingPrice, CostPrice, IsDefault)
    - Create `UpdateTierRequest` (TierId, ProductId, TierName, SellingPrice, CostPrice)
    - Create `SetDefaultTierRequest` (TierId, ProductId)
    - Create `DeactivateTierRequest` (TierId, ProductId)
    - Create `ReactivateTierRequest` (TierId, ProductId)
    - Create `ProductTierSelectionResponse` (HasTiers, DefaultTierId, Tiers list)
    - Create `TierOption` (Id, TierName, SellingPrice, CostPrice, IsDefault)
    - _Requirements: 1.1, 4.1, 4.2_

- [x] 3. Repository layer
  - [x] 3.1 Create ProductPriceTierRepository
    - Extend `GenericStoredProcedureRepository<ProductPriceTier>`
    - Implement `InsertAsync(ProductPriceTier tier)` — INSERT into `[product].[ProductPriceTier]` with all columns, return inserted Id
    - Implement `UpdateAsync(ProductPriceTier tier)` — UPDATE TierName, SellingPrice, CostPrice, UpdatedAtUtc
    - Implement `SetDefaultFlagAsync(int tierId, bool isDefault)` — UPDATE IsDefault and UpdatedAtUtc
    - Implement `DeactivateAsync(int tierId)` — UPDATE IsActive = 0 and UpdatedAtUtc
    - Implement `ReactivateAsync(int tierId)` — UPDATE IsActive = 1 and UpdatedAtUtc
    - Implement `GetByProductIdAsync(int productId)` — SELECT all tiers for a product
    - Implement `GetActiveByProductIdAsync(int productId)` — SELECT WHERE IsActive = 1
    - Implement `GetByIdAsync(int tierId)` — SELECT single tier by Id
    - Implement `GetActiveCountAsync(int productId)` — SELECT COUNT WHERE IsActive = 1
    - Use full table names in SQL (no aliases), parameterised queries, catch `Exception ex`, rethrow
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 3.4_

  - [x] 3.2 Extend ProductPriceHistory repository with tier-aware insert
    - Implement `InsertTierPriceHistoryAsync(int productId, int productPriceTierId, decimal sellingPrice, decimal costPrice, string userId)` — INSERT into `[product].[ProductPriceHistory]` with ProductPriceTierId populated
    - Use full table names, parameterised queries, catch `Exception ex`, rethrow
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 3.3 Extend ProductRepository with DefaultSellingPrice/DefaultCostPrice sync method
    - Implement `UpdateDefaultPricesAsync(int productId, decimal sellingPrice, decimal costPrice)` — UPDATE `[product].[Product]` SET DefaultSellingPrice, DefaultCostPrice WHERE Id = @productId
    - Use full table names, parameterised queries, catch `Exception ex`, rethrow
    - _Requirements: 2.1, 2.3_

- [x] 4. Checkpoint — Ensure all migrations, entities, and repositories compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer — IProductPriceTierService
  - [x] 5.1 Create IProductPriceTierService interface and ProductPriceTierService class
    - Define interface with methods: CreateTierAsync, UpdateTierAsync, SetDefaultTierAsync, DeactivateTierAsync, ReactivateTierAsync, GetTiersForProductAsync, GetActiveTiersForProductAsync, GetTierByIdAsync
    - Register ProductPriceTierRepository in DI container (Program.cs or ServiceCollectionExtensions)
    - Register ProductPriceTierService in DI container
    - Inject ProductPriceTierRepository, ProductRepository (for default price sync), ProductPriceHistory repository, ICurrentTenantService for BusinessId scoping
    - _Requirements: 1.1, 9.1, 9.2_

  - [x] 5.2 Implement CreateTierAsync
    - Validate TierName (required, max 100 chars), SellingPrice/CostPrice (≥ 0)
    - Verify ProductId belongs to authenticated user's BusinessId
    - Enforce max 20 active tiers per product
    - Enforce tier name uniqueness among active tiers for the product
    - If first tier for product: set IsDefault = true (UI pre-fills from DefaultSellingPrice/DefaultCostPrice but user can override — the service does NOT force-seed values)
    - If IsDefault = true: clear IsDefault on existing default tier
    - Insert tier, insert initial price history record
    - If new tier is default: sync Product.DefaultSellingPrice and DefaultCostPrice
    - Return ServiceResult with created tier data
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 8.3, 9.2_

  - [ ]* 5.3 Write property test for tier creation round-trip
    - **Property 1: Tier Creation Round-Trip**
    - Test: For any valid tier name and prices (≥ 0), creating a tier and reading it back produces identical TierName, SellingPrice, CostPrice, ProductId, IsActive=true
    - **Validates: Requirements 1.1**

  - [ ]* 5.4 Write property test for tier name uniqueness enforcement
    - **Property 2: Tier Name Uniqueness Enforcement (Active Tiers Only)**
    - Test: For any product and active tier name, attempting to create another active tier with the same name is rejected; a deactivated tier's name may be reused
    - **Validates: Requirements 1.2, 2.2**

  - [ ]* 5.5 Write property test for exactly one default tier invariant
    - **Property 3: Exactly One Default Tier Invariant**
    - Test: For any product with one or more active tiers, after any sequence of tier operations, exactly one tier has IsDefault=true
    - **Validates: Requirements 1.3, 2.3, 2.4**

  - [ ]* 5.6 Write property test for first tier seeds from product defaults
    - **Property 4: First Tier Seeds From Product Defaults**
    - Test: For any product with zero tiers, creating the first tier produces SellingPrice = DefaultSellingPrice, CostPrice = DefaultCostPrice, IsDefault = true
    - **Validates: Requirements 1.4, 8.3**

  - [x] 5.7 Implement UpdateTierAsync
    - Validate TierName (required, max 100 chars), SellingPrice/CostPrice (≥ 0)
    - Verify tier exists and ProductId belongs to authenticated user's BusinessId
    - Enforce tier name uniqueness among active tiers (excluding current tier)
    - Update tier record
    - Insert price history record with ProductPriceTierId
    - If tier is default: sync Product.DefaultSellingPrice and DefaultCostPrice
    - Return ServiceResult
    - _Requirements: 2.1, 2.2, 7.1, 9.2_

  - [ ]* 5.8 Write property test for price update creates append-only history
    - **Property 5: Price Update Creates Append-Only History**
    - Test: After N price updates to a tier, ProductPriceHistory contains exactly N records for that tier with correct values and monotonically increasing EffectiveFromUtc
    - **Validates: Requirements 2.1, 7.1, 7.2**

  - [x] 5.9 Implement SetDefaultTierAsync
    - Verify tier exists, is active, and ProductId belongs to authenticated user's BusinessId
    - Remove IsDefault from current default tier
    - Set IsDefault on new tier
    - Sync Product.DefaultSellingPrice and DefaultCostPrice to new default tier's values
    - All within a single transaction
    - Return ServiceResult
    - _Requirements: 2.3, 2.4_

  - [ ]* 5.10 Write property test for DefaultSellingPrice sync on default tier change
    - **Property 13: DefaultSellingPrice Sync on Default Tier Change**
    - Test: After updating default tier's price or designating a new default, Product.DefaultSellingPrice equals current default tier's SellingPrice and Product.DefaultCostPrice equals current default tier's CostPrice
    - **Validates: Requirements 2.1, 2.3**

  - [x] 5.11 Implement DeactivateTierAsync
    - Verify tier exists and ProductId belongs to authenticated user's BusinessId
    - Reject if tier is the default tier: "Cannot deactivate the default tier. Set another tier as default first."
    - Set IsActive = false, update UpdatedAtUtc
    - Return ServiceResult
    - _Requirements: 3.1, 3.2, 3.4_

  - [x] 5.12 Implement ReactivateTierAsync
    - Verify tier exists and ProductId belongs to authenticated user's BusinessId
    - Reject if active tier count would exceed 20
    - Reject if tier name conflicts with existing active tier
    - Set IsActive = true, update UpdatedAtUtc
    - Return ServiceResult
    - _Requirements: 3.4_

  - [ ]* 5.13 Write property test for active tier filter excludes deactivated
    - **Property 6: Active Tier Filter Excludes Deactivated**
    - Test: For any mix of active/inactive tiers, GetActiveTiersForProductAsync returns only tiers with IsActive=true
    - **Validates: Requirements 3.1, 3.3**

  - [ ]* 5.14 Write property test for deactivation preserves all data
    - **Property 7: Deactivation Preserves All Data**
    - Test: After deactivating a tier, the record still exists with all original values preserved (except IsActive=false) and all associated history records remain
    - **Validates: Requirements 3.4**

  - [ ]* 5.15 Write property test for active tier count limit
    - **Property 14: Active Tier Count Limit**
    - Test: The count of active tiers never exceeds 20; any create/reactivate that would exceed 20 is rejected
    - **Validates: Requirements 1.1, 3.1**

  - [ ]* 5.16 Write property test for reactivation preserves tier data
    - **Property 15: Reactivation Preserves Tier Data**
    - Test: After reactivation, tier's TierName, SellingPrice, CostPrice, ProductId remain unchanged (only IsActive and UpdatedAtUtc change)
    - **Validates: Requirements 3.4**

  - [ ]* 5.17 Write property test for multi-tenant data isolation
    - **Property 12: Multi-Tenant Data Isolation**
    - Test: Any tier operation targeting a ProductId that does not belong to the authenticated BusinessId is rejected and no data is returned/modified/created
    - **Validates: Requirements 9.1, 9.2, 9.3**

- [x] 6. Checkpoint — Ensure service layer compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Controller layer — Tier management endpoints
  - [x] 7.1 Add tier management endpoints to ProductController
    - Implement `AxPostCreateTier([FromBody] CreateTierRequest request)` — call CreateTierAsync, return Json {success, message, tier}
    - Implement `AxPostUpdateTier([FromBody] UpdateTierRequest request)` — call UpdateTierAsync, return Json {success, message}
    - Implement `AxPostSetDefaultTier([FromBody] SetDefaultTierRequest request)` — call SetDefaultTierAsync, return Json {success, message}
    - Implement `AxPostDeactivateTier([FromBody] DeactivateTierRequest request)` — call DeactivateTierAsync, return Json {success, message}
    - Implement `AxPostReactivateTier([FromBody] ReactivateTierRequest request)` — call ReactivateTierAsync, return Json {success, message}
    - Implement `AxGetProductTiers(int productId)` — call GetTiersForProductAsync, return Json {success, data}
    - All methods: [ValidateAntiForgeryToken] on POST, catch `Exception ex`, log error, return Json {success: false, message: "An unexpected error occurred."}
    - _Requirements: 1.1, 2.1, 2.3, 3.1, 9.2_

  - [x] 7.2 Add tier selection endpoint to QuotationController
    - Implement `AxGetProductTiersForSelection(int productId)` — call GetActiveTiersForProductAsync, return Json with ProductTierSelectionResponse (hasTiers, defaultTierId, tiers[], currencySymbol from BusinessProfile)
    - Include `currencySymbol` in the response so the JS can format tier prices in the dropdown without a separate lookup
    - Catch `Exception ex`, return Json {success: false, message: "An unexpected error occurred."}
    - _Requirements: 4.1, 4.2, 5.1_

  - [x] 7.3 Add tier selection endpoint to InvoiceController
    - Implement `AxGetProductTiersForSelection(int productId)` — same logic as QuotationController endpoint, including currencySymbol in response
    - Catch `Exception ex`, return Json {success: false, message: "An unexpected error occurred."}
    - _Requirements: 4.1, 4.2, 5.1_

- [x] 8. Integration — Quotation and Invoice line creation with tier reference
  - [x] 8.1 Extend QuotationLine creation to include ProductPriceTierId and PriceTierName
    - Modify existing quotation line add logic (AxPostAddLine or equivalent) to accept optional ProductPriceTierId parameter
    - When ProductPriceTierId is provided: look up tier, store tier's TierName as PriceTierName snapshot on the QuotationLine
    - When ProductPriceTierId is null: leave PriceTierName null (legacy behavior)
    - UnitPrice/CostPrice already snapshotted from form — no change needed there
    - _Requirements: 4.3, 4.4, 4.5, 5.2, 5.3, 6.4_

  - [ ]* 8.2 Write property test for tier selection snapshot integrity
    - **Property 8: Tier Selection Snapshot Integrity**
    - Test: For any selected tier, the QuotationLine has UnitPrice = tier's SellingPrice, CostPrice = tier's CostPrice, ProductPriceTierId = selected tier's Id
    - **Validates: Requirements 4.3, 4.4, 4.5, 6.4**

  - [ ]* 8.3 Write property test for zero-tier backward compatibility
    - **Property 9: Zero-Tier Backward Compatibility**
    - Test: For any product with zero active tiers, QuotationLine uses DefaultSellingPrice/DefaultCostPrice with ProductPriceTierId=NULL
    - **Validates: Requirements 5.2, 8.2**

  - [x] 8.4 Extend InvoiceLine creation to include ProductPriceTierId and PriceTierName
    - Same pattern as QuotationLine: accept optional ProductPriceTierId, snapshot PriceTierName
    - **CRITICAL:** Ensure the InvoiceLine INSERT statement in InvoiceLineRepository includes the new `[ProductPriceTierId]` and `[PriceTierName]` columns
    - _Requirements: 4.3, 4.4, 4.5, 6.4_

  - [x] 8.5 Update quotation-to-invoice conversion to map tier fields
    - In `InvoiceService.ConvertFromQuotationAsync` (or equivalent conversion method): copy `QuotationLine.ProductPriceTierId` → `InvoiceLine.ProductPriceTierId` and `QuotationLine.PriceTierName` → `InvoiceLine.PriceTierName`
    - _Requirements: 4.5, 6.4_

  - [ ]* 8.6 Write property test for existing lines immutable after tier modification
    - **Property 10: Existing Lines Immutable After Tier Modification**
    - Test: Updating a tier's price or deactivating it does not change any existing QuotationLine/InvoiceLine's UnitPrice, CostPrice, or ProductPriceTierId
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 8.7 Write property test for PriceTierName snapshot immutability
    - **Property 16: PriceTierName Snapshot Immutability**
    - Test: Renaming a ProductPriceTier does not change PriceTierName on any existing QuotationLine/InvoiceLine
    - **Validates: Requirements 6.1, 6.4**

- [x] 9. Checkpoint — Ensure controller and integration logic compiles, all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Views — Tier management section on Product Edit page
  - [x] 10.1 Add "Price Tiers" card section to Product Edit view
    - Add a `.glass.card-pad` section below product details
    - If product is deactivated (IsActive = false): render tier table in read-only mode (no action buttons)
    - If no tiers exist (product active): show "Add Price Tiers" CTA button
    - If tiers exist: display table with columns — TierName, SellingPrice, CostPrice, IsDefault (badge), Status (Active/Inactive), Actions
    - Actions for active tiers: Edit, Set Default, Deactivate
    - Actions for inactive tiers: Reactivate (muted styling)
    - "Add Tier" button disabled/hidden when 20 active tiers exist
    - _Requirements: 1.1, 1.4, 3.3_

  - [x] 10.2 Implement tier management JavaScript (Product Edit page)
    - "Add Tier" button: SweetAlert2 modal with TierName, SellingPrice, CostPrice fields → BlockUI → AxPostCreateTier → reload tier table
    - "Edit" button: SweetAlert2 modal pre-filled → BlockUI → AxPostUpdateTier → reload tier table
    - "Set Default" button: SweetAlert2 confirmation → BlockUI → AxPostSetDefaultTier → reload tier table
    - "Deactivate" button: SweetAlert2 destructive confirmation (confirmButtonColor '#C24A4A') → BlockUI → AxPostDeactivateTier → tier shows as inactive
    - "Reactivate" button: SweetAlert2 confirmation → BlockUI → AxPostReactivateTier → tier becomes active
    - All AJAX: BlockUI.show() before, BlockUI.hide() in success + catch, Swal.fire result
    - Include antiforgery token in POST requests
    - _Requirements: 1.1, 2.1, 2.3, 3.1, 3.4_

- [x] 11. Views — Tier selector on Quotation/Invoice line item creation
  - [x] 11.1 Add tier selector dropdown to Quotation line item form
    - When product is selected from autocomplete: fetch AxGetProductTiersForSelection
    - If hasTiers = false: no dropdown shown, use DefaultSellingPrice (existing behavior)
    - If hasTiers = true: show dropdown with tier names and prices (e.g., "Retail — R150.00"), pre-select default tier
    - On tier change: update UnitPrice and CostPrice fields with selected tier's values
    - On line save: include ProductPriceTierId in request payload
    - For draft re-selection: fetch CURRENT tier price (not cached)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 6.3_

  - [ ]* 11.2 Write property test for draft re-selection uses current price
    - **Property 11: Draft Re-Selection Uses Current Price**
    - Test: For any draft quotation where tier price was updated since last save, re-selecting the same tier populates current SellingPrice/CostPrice (not cached values)
    - **Validates: Requirements 6.3**

  - [x] 11.3 Add tier selector dropdown to Invoice line item form
    - Same logic as quotation tier selector: fetch tiers on product select, show dropdown, pre-select default, update prices on change, include ProductPriceTierId on save
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2_

- [x] 12. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit, minimum 100 iterations)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex)` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- DefaultSellingPrice sync is critical: when default tier price changes, Product.DefaultSellingPrice/DefaultCostPrice must be updated atomically
- Quotation-to-invoice conversion must map ProductPriceTierId and PriceTierName
- Bottom-up ordering: DB → Entities → Repos → Services → Controller → Views → JS

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["4"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["5.2", "5.7", "5.9", "5.11", "5.12"] },
    { "id": 6, "tasks": ["5.3", "5.4", "5.5", "5.6", "5.8", "5.10", "5.13", "5.14", "5.15", "5.16", "5.17"] },
    { "id": 7, "tasks": ["6"] },
    { "id": 8, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 9, "tasks": ["8.1", "8.4", "8.5"] },
    { "id": 10, "tasks": ["8.2", "8.3", "8.6", "8.7"] },
    { "id": 11, "tasks": ["9"] },
    { "id": 12, "tasks": ["10.1", "10.2"] },
    { "id": 13, "tasks": ["11.1", "11.3"] },
    { "id": 14, "tasks": ["11.2"] },
    { "id": 15, "tasks": ["12"] }
  ]
}
```
