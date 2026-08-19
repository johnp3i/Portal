# Requirements Document

## Introduction

This feature introduces named price tiers to products, enabling businesses to maintain multiple pricing profiles (e.g., Retail, Wholesale, VIP) for a single product. When creating quotations or invoices, users can select which price tier to apply. This eliminates the current workaround of duplicating products with different prices to represent different pricing levels.

## Glossary

- **Product**: A master catalog record representing a sellable item or service, scoped to a business tenant. Stored in `[product].[Product]`.
- **Price_Tier**: A named pricing level associated with a product, containing a selling price and cost price. Stored in `[product].[ProductPriceTier]`.
- **Tier_Selector**: A UI control displayed during product selection in quotation/invoice creation that allows the user to choose which price tier to apply.
- **Quotation_Line**: An individual priced item within a quotation, storing a snapshot of the price at time of selection. Stored in `[quotation].[QuotationLine]`.
- **Default_Tier**: The price tier marked as the primary pricing level for a product, used when no explicit tier selection is made.
- **Tier_Service**: The service layer component responsible for business logic related to price tier management.
- **Price_History**: A historical record capturing each change to a product's selling or cost price. Stored in `[product].[ProductPriceHistory]`.

## Requirements

### Requirement 1: Create Price Tiers for a Product

**User Story:** As a business user, I want to create named price tiers for a product, so that I can maintain multiple pricing profiles without duplicating the product.

#### Acceptance Criteria

1. WHEN a user adds a price tier to a product, THE Tier_Service SHALL create a Price_Tier record with a TierName, SellingPrice, CostPrice, and IsDefault flag scoped to that product.
2. THE Tier_Service SHALL enforce that each Price_Tier TierName is unique within the same product.
3. THE Tier_Service SHALL enforce that exactly one Price_Tier per product is marked as the Default_Tier at any time.
4. WHEN a user creates the first Price_Tier for a product, THE Tier_Service SHALL mark that tier as the Default_Tier.
5. THE Tier_Service SHALL allow a product to have zero or more price tiers (zero tiers means the product uses DefaultSellingPrice directly).

### Requirement 2: Edit and Manage Price Tiers

**User Story:** As a business user, I want to edit tier names and prices, so that I can keep pricing profiles accurate over time.

#### Acceptance Criteria

1. WHEN a user updates the SellingPrice or CostPrice of a Price_Tier, THE Tier_Service SHALL persist the new values and record the change in Price_History with a reference to the Price_Tier.
2. WHEN a user renames a Price_Tier, THE Tier_Service SHALL update the TierName and enforce uniqueness within the same product.
3. WHEN a user changes which Price_Tier is the Default_Tier, THE Tier_Service SHALL remove the IsDefault flag from the previously default tier and set it on the newly designated tier in a single atomic operation.
4. THE Tier_Service SHALL prevent a user from removing the IsDefault flag without designating another tier as default.

### Requirement 3: Deactivate Price Tiers (Soft Delete)

**User Story:** As a business user, I want to deactivate a price tier that is no longer needed, so that it stops appearing in tier selection without affecting historical records.

#### Acceptance Criteria

1. WHEN a user deactivates a Price_Tier, THE Tier_Service SHALL set the IsActive flag to false on that tier.
2. THE Tier_Service SHALL prevent deactivation of a Price_Tier that is currently marked as the Default_Tier until another tier is designated as default.
3. WHILE a Price_Tier has IsActive set to false, THE Tier_Selector SHALL exclude that tier from the list of selectable options.
4. WHEN a Price_Tier is deactivated, THE Tier_Service SHALL retain the tier record and all associated Price_History entries unchanged.

### Requirement 4: Tier Selection During Quotation/Invoice Creation

**User Story:** As a business user, I want to choose which price tier to apply when adding a product to a quotation or invoice, so that I can issue correct pricing for different customer segments.

#### Acceptance Criteria

1. WHEN a user selects a product that has one or more active price tiers, THE Tier_Selector SHALL display the list of active tier names with their SellingPrice values.
2. WHEN a user selects a product that has one or more active price tiers, THE Tier_Selector SHALL pre-select the Default_Tier.
3. WHEN a user confirms tier selection, THE Quotation_Line SHALL store the SellingPrice from the selected Price_Tier as the UnitPrice snapshot.
4. WHEN a user confirms tier selection, THE Quotation_Line SHALL store the CostPrice from the selected Price_Tier as the CostPrice snapshot.
5. THE Quotation_Line SHALL store the ProductPriceTierId as a reference to identify which tier was used at time of selection.

### Requirement 5: Backward Compatibility for Products Without Tiers

**User Story:** As a business user, I want existing products without price tiers to continue working exactly as they do today, so that the new feature does not disrupt current workflows.

#### Acceptance Criteria

1. WHEN a user selects a product that has zero active price tiers, THE Tier_Selector SHALL not be displayed.
2. WHEN a user selects a product that has zero active price tiers, THE Quotation_Line SHALL use the product DefaultSellingPrice as the UnitPrice and DefaultCostPrice as the CostPrice.
3. THE Tier_Service SHALL not require any price tiers to be created for a product to function in quotations and invoices.

### Requirement 6: Price Snapshot Integrity

**User Story:** As a business user, I want historical quotations and invoices to retain the exact prices they were created with, so that financial records remain accurate regardless of future price changes.

#### Acceptance Criteria

1. WHEN a Price_Tier SellingPrice or CostPrice is updated, THE Tier_Service SHALL not modify the UnitPrice or CostPrice on any existing Quotation_Line that references that tier.
2. WHEN a Price_Tier is deactivated, THE Tier_Service SHALL not modify the UnitPrice or CostPrice on any existing Quotation_Line that references that tier.
3. WHILE a quotation is in draft state, WHEN a user re-selects the same product and tier, THE Quotation_Line SHALL use the current SellingPrice of the Price_Tier at the time of re-selection.
4. THE Quotation_Line SHALL store the ProductPriceTierId for audit traceability, independent of whether the tier is later deactivated or repriced.

### Requirement 7: Price History Tracking for Tiers

**User Story:** As a business user, I want to see the history of price changes per tier, so that I can audit when and by whom prices were modified.

#### Acceptance Criteria

1. WHEN the SellingPrice or CostPrice of a Price_Tier changes, THE Tier_Service SHALL insert a new Price_History record with the ProductId, ProductPriceTierId, new SellingPrice, new CostPrice, EffectiveFromUtc, and ChangedByUserId.
2. THE Price_History SHALL retain all previous records for a Price_Tier, providing a complete chronological audit trail.
3. THE Tier_Service SHALL record Price_History entries for tier price changes using the same structure as existing product-level price history, extended with the ProductPriceTierId reference.

### Requirement 8: Data Migration for Existing Products

**User Story:** As a system administrator, I want existing products to remain fully functional without any migration action, so that the feature rollout does not require data transformation.

#### Acceptance Criteria

1. THE Product entity SHALL retain the DefaultSellingPrice and DefaultCostPrice columns as the source of truth for products that have no price tiers.
2. THE Tier_Service SHALL treat a product with zero Price_Tier records as a single-price product using DefaultSellingPrice and DefaultCostPrice.
3. WHEN a user chooses to add tiers to an existing product, THE Tier_Service SHALL create the first Price_Tier using the current DefaultSellingPrice and DefaultCostPrice values and mark it as the Default_Tier.

### Requirement 9: Multi-Tenant Data Isolation

**User Story:** As a platform operator, I want price tiers to be scoped to the owning business, so that tenants cannot access or modify each other's pricing data.

#### Acceptance Criteria

1. THE Tier_Service SHALL scope all Price_Tier queries by the authenticated user's BusinessId through the parent Product relationship.
2. THE Tier_Service SHALL verify that the target Product belongs to the authenticated user's BusinessId before creating, updating, or deactivating a Price_Tier.
3. IF a request references a ProductId that does not belong to the authenticated user's BusinessId, THEN THE Tier_Service SHALL reject the operation and return an authorization error.
