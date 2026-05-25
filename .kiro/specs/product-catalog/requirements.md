# Requirements Document

## Introduction

The Product Catalog module introduces a centralised product registry that replaces the existing `[quotation].[LineItemCatalog]` table (migration 030). It provides a dedicated `[product]` schema with a Product table and a ProductPriceHistory table, enabling businesses to maintain a master list of products with pricing, supplier associations, and usage tracking. The module includes a full management UI with search, KPIs, and charting, as well as autocomplete integration into Invoice and Quotation line item forms. When line items are created, the system automatically links them to existing products or creates new product records, maintaining a living catalog that grows with business activity.

The existing database schema contains `[invoice].[InvoiceLine]`, `[quotation].[QuotationLine]`, `[quotation].[LineItemCatalog]`, and `[purchase].[Supplier]`. This module builds on those foundations and extends InvoiceLine and QuotationLine with a nullable ProductCode column for catalog linkage.

## Glossary

- **Product_Service**: The service responsible for product CRUD operations, search, autocomplete, auto-population logic, and price history tracking
- **Product_Controller**: The ASP.NET Core MVC controller that handles product management page requests, CRUD actions, and autocomplete API endpoints
- **Product**: A master catalog record representing a sellable item or service, scoped to a business tenant
- **Product_Price_History**: A historical record capturing each change to a product's selling price or cost price, with the effective date and the user who made the change
- **Product_Code**: A short alphanumeric identifier for a product, unique within a business tenant
- **Autocomplete_Service**: The component responsible for searching products and historical line item usages to provide autocomplete suggestions on Invoice and Quotation forms
- **Line_Item**: A single row on an Invoice or Quotation representing a priced product or service
- **Usage_Count**: The number of times a product has been referenced across InvoiceLine and QuotationLine records for a given business
- **Page_Size**: The number of records displayed per page in the Products management view, fixed at 15

## Requirements

### Requirement 1: Product Schema and Data Model

**User Story:** As a platform operator, I want a dedicated product schema with proper tables and relationships, so that the system has a normalised, extensible product catalog foundation.

#### Acceptance Criteria

1. THE Portal_Database SHALL create a `[product]` schema if it does not already exist, prior to creating any tables within that schema
2. THE Portal_Database SHALL contain a `[product].[Product]` table with columns: Id (PK, int identity), BusinessId (FK to [portal].[Business], required), ProductCode (nvarchar(50), required), Description (nvarchar(500), required), DefaultSellingPrice (decimal(18,2), required, minimum value 0.00), DefaultCostPrice (decimal(18,2), required, minimum value 0.00), DefaultVatRate (decimal(5,2), required, range 0.00 to 99.99), SupplierId (FK to [purchase].[Supplier], nullable), IsActive (bit, default 1), LastUsedDate (datetime2, nullable), CreatedAtUtc (datetime2, default GETUTCDATE())
3. THE Portal_Database SHALL enforce a unique constraint on the combination of BusinessId and ProductCode in the `[product].[Product]` table
4. THE Portal_Database SHALL contain a `[product].[ProductPriceHistory]` table with columns: Id (PK, int identity), ProductId (FK to [product].[Product], required), SellingPrice (decimal(18,2), required, minimum value 0.00), CostPrice (decimal(18,2), required, minimum value 0.00), EffectiveFromUtc (datetime2, required), ChangedByUserId (nvarchar(450), required), CreatedAtUtc (datetime2, default GETUTCDATE())
5. THE Portal_Database SHALL enforce cascading delete from `[product].[Product]` to `[product].[ProductPriceHistory]`
6. WHEN a product's DefaultSellingPrice or DefaultCostPrice is updated, THE Portal_Database SHALL have a corresponding row inserted into `[product].[ProductPriceHistory]` recording the new SellingPrice, CostPrice, EffectiveFromUtc, and ChangedByUserId
7. THE Portal_Database SHALL add a nullable ProductCode column (nvarchar(50)) to the `[invoice].[InvoiceLine]` table
8. THE Portal_Database SHALL add a nullable ProductCode column (nvarchar(50)) to the `[quotation].[QuotationLine]` table
9. THE Portal_Database SHALL create a nonclustered index on `[product].[Product]` for BusinessId to optimise tenant-scoped queries
10. THE Portal_Database SHALL create a nonclustered index on `[product].[Product]` for the combination of BusinessId and ProductCode to optimise autocomplete lookups
11. THE Portal_Database SHALL create a nonclustered index on `[product].[ProductPriceHistory]` for ProductId to optimise price history retrieval

### Requirement 2: Product Management CRUD

**User Story:** As a business operator, I want to create, edit, and deactivate products in my catalog, so that I can maintain an accurate and up-to-date product registry.

#### Acceptance Criteria

1. WHEN a create product request is submitted with a valid ProductCode, Description, DefaultSellingPrice, DefaultCostPrice, and DefaultVatRate, THE Product_Service SHALL insert a new Product record with IsActive set to true and CreatedAtUtc set to the current UTC time
2. WHEN a create product request includes a SupplierId, THE Product_Service SHALL validate that the Supplier exists and belongs to the same BusinessId before associating the supplier with the product
3. IF a create product request specifies a ProductCode that already exists for the same BusinessId, THEN THE Product_Service SHALL return an error indicating that the ProductCode is already in use
4. WHEN an edit product request is submitted with valid field values, THE Product_Service SHALL update the Product record with the new values
5. WHEN an edit product request changes the DefaultSellingPrice or DefaultCostPrice, THE Product_Service SHALL insert a new ProductPriceHistory record with the new prices, EffectiveFromUtc set to the current UTC time, and ChangedByUserId set to the authenticated user's identifier
6. WHEN a deactivate product request is submitted, THE Product_Service SHALL set IsActive to false on the Product record
7. IF a create or edit request is submitted with an empty ProductCode or an empty Description, THEN THE Product_Service SHALL return a validation error indicating the required fields
8. WHEN a create product request is submitted, THE Product_Service SHALL insert an initial ProductPriceHistory record with the DefaultSellingPrice and DefaultCostPrice values, EffectiveFromUtc set to the current UTC time, and ChangedByUserId set to the authenticated user's identifier

### Requirement 3: Products Management View

**User Story:** As a business operator, I want a dedicated Products page with search, paging, and summary statistics, so that I can efficiently browse and manage my product catalog.

#### Acceptance Criteria

1. THE Product_Controller SHALL expose an Index action that renders the Products management page accessible via sidebar navigation
2. THE Product_Controller SHALL display a searchable table of products with columns: ProductCode, Description, DefaultSellingPrice, DefaultCostPrice, DefaultVatRate, Supplier Name, IsActive status, and LastUsedDate
3. THE Product_Controller SHALL paginate the product table with a Page_Size of 15 records per page
4. THE Product_Controller SHALL display pagination information showing "Showing X-Y of Z" where X is the first record number, Y is the last record number, and Z is the total record count
5. WHEN a search term is entered, THE Product_Controller SHALL filter products whose ProductCode or Description contains the search term (case-insensitive partial match), apply pagination to the filtered result set, and update the total count accordingly
6. THE Product_Controller SHALL display four KPI cards: Total Products (count of all products for the business), Active Products (count where IsActive is true), Average Selling Price (mean of DefaultSellingPrice across all active products), and Best Seller (the product with the highest Usage_Count)
7. THE Product_Controller SHALL display a bar chart showing the top 10 products by Usage_Count, with product Description on the axis and usage count as the bar value
8. THE Product_Controller SHALL provide a Create Product button that opens a form with fields: ProductCode (required), Description (required), DefaultSellingPrice, DefaultCostPrice, DefaultVatRate, Supplier (optional dropdown populated with active suppliers for the business), and IsActive toggle (defaulting to active)
9. THE Product_Controller SHALL provide an Edit action for each product row that opens the same form pre-populated with the product's current values
10. THE Product_Controller SHALL provide a Deactivate action for each active product row that triggers a SweetAlert2 confirmation dialog before setting IsActive to false

### Requirement 4: Product Autocomplete on Line Item Forms

**User Story:** As a business operator, I want autocomplete suggestions when entering line items on Invoices and Quotations, so that I can quickly select existing products and maintain consistency.

#### Acceptance Criteria

1. WHEN the user types at least 2 characters in the Description field or ProductCode field on an Invoice or Quotation line item form, THE Autocomplete_Service SHALL search the Product table for records where ProductCode or Description contains the typed text (case-insensitive partial match), scoped to the current BusinessId
2. THE Autocomplete_Service SHALL return matching products displaying: ProductCode, Description, DefaultSellingPrice, and Supplier name (or empty if no supplier is associated)
3. THE Autocomplete_Service SHALL also search historical usages from InvoiceLine and QuotationLine tables where Description or ProductCode contains the typed text (case-insensitive partial match), scoped to the current BusinessId via the parent Invoice or Quotation record
4. THE Autocomplete_Service SHALL display historical usage results with: the line Description, UnitPrice used, the date of the parent document (InvoiceDate for invoice lines, CreatedAtUtc for quotation lines), and a visual indicator distinguishing the source as either Invoice or Quotation
5. THE Autocomplete_Service SHALL sort all results by most recent date first (LastUsedDate for products, InvoiceDate for invoice lines, CreatedAtUtc for quotation lines)
6. THE Autocomplete_Service SHALL limit the total number of autocomplete results to 20 entries
7. WHEN the user selects a product from the autocomplete results, THE Autocomplete_Service SHALL auto-fill the line item form with: Description from Product.Description, UnitPrice from Product.DefaultSellingPrice, VatRate from Product.DefaultVatRate, CostPrice from Product.DefaultCostPrice, and ProductCode from Product.ProductCode
8. WHEN the user selects a historical line item from the autocomplete results, THE Autocomplete_Service SHALL auto-fill the line item form with: Description, UnitPrice, VatRate, CostPrice (if present), and ProductCode (if present) from that specific historical line record
9. IF no matching products or historical lines are found, THEN THE Autocomplete_Service SHALL display no dropdown and the user continues typing freely
10. THE Autocomplete_Service SHALL return autocomplete results within 500 milliseconds of the user pausing input for 300 milliseconds (debounce), to avoid excessive queries during active typing
11. IF the autocomplete search request fails due to a service or database error, THEN THE Autocomplete_Service SHALL suppress the dropdown without interrupting the user's manual text entry

### Requirement 5: Auto-Population Logic

**User Story:** As a business operator, I want the system to automatically link line items to existing products and create new product records when appropriate, so that my catalog grows organically without manual data entry.

#### Acceptance Criteria

1. WHEN a new InvoiceLine or QuotationLine is created with a ProductCode value, THE Product_Service SHALL search for an existing Product with a matching ProductCode (case-insensitive) for the same BusinessId
2. WHEN a new InvoiceLine or QuotationLine is created without a ProductCode but with a Description, THE Product_Service SHALL search for an existing Product with an exact Description match (case-insensitive) for the same BusinessId
3. WHEN a matching Product is found, THE Product_Service SHALL update the Product's LastUsedDate to the current UTC time
4. WHEN no matching Product is found and the line item has a ProductCode value, THE Product_Service SHALL create a new Product record with: ProductCode from the line item, Description from the line item, DefaultSellingPrice from the line item's UnitPrice, DefaultCostPrice set to 0.00, DefaultVatRate from the line item's VatRate (or 0.00 if not available), IsActive set to true, LastUsedDate set to the current UTC time, and BusinessId from the authenticated user's tenant
5. WHEN no matching Product is found and the line item has no ProductCode, THE Product_Service SHALL take no auto-creation action (the line item remains unlinked)
6. WHEN a new Product is created via auto-population, THE Product_Service SHALL insert an initial ProductPriceHistory record with the DefaultSellingPrice and DefaultCostPrice values, EffectiveFromUtc set to the current UTC time, and ChangedByUserId set to the authenticated user's identifier
7. WHEN a matching Product is found and the line item's UnitPrice differs from the Product's current DefaultSellingPrice, THE Product_Service SHALL NOT update the Product's DefaultSellingPrice (the existing product prices are preserved; only LastUsedDate is updated)
8. WHEN a Product's DefaultSellingPrice or DefaultCostPrice is updated via the edit product flow, THE Product_Service SHALL insert a new ProductPriceHistory record with the updated prices, EffectiveFromUtc set to the current UTC time, and ChangedByUserId set to the authenticated user's identifier
9. IF the auto-population logic fails due to a database constraint violation or unexpected error after the line item has been persisted, THEN THE Product_Service SHALL log the failure and allow the line item to remain unlinked without rolling back the line item persistence
10. THE Product_Service SHALL execute the auto-population check after the line item has been successfully persisted to the database

### Requirement 6: LineItemCatalog Migration

**User Story:** As a platform operator, I want existing LineItemCatalog data migrated to the new Product table, so that historical catalog entries are preserved and the system transitions cleanly to the new schema.

#### Acceptance Criteria

1. THE Portal_Database SHALL provide a migration script that inserts records from `[quotation].[LineItemCatalog]` into `[product].[Product]` with: Description mapped from LineItemCatalog.Description, DefaultSellingPrice mapped from LineItemCatalog.UnitPrice, DefaultCostPrice set to 0.00, DefaultVatRate mapped from LineItemCatalog.VatRate, ProductCode generated as a sequential code (e.g., "MIGRATED-001", "MIGRATED-002") per business, IsActive set to true, and BusinessId preserved from LineItemCatalog.BusinessId
2. THE Portal_Database SHALL ensure no duplicate ProductCode values are created within a BusinessId during migration
3. THE Portal_Database SHALL preserve the CreatedAtUtc value from LineItemCatalog records where available (using UpdatedAtUtc as a fallback)
4. WHEN the migration is complete, THE Portal_Database SHALL retain the `[quotation].[LineItemCatalog]` table in a deprecated state (no deletion) to allow rollback if needed

### Requirement 7: Tenant Isolation

**User Story:** As a platform operator, I want all product catalog queries scoped to the authenticated business tenant, so that businesses cannot view or modify each other's product data.

#### Acceptance Criteria

1. THE Product_Service SHALL filter all product queries by the authenticated user's BusinessId resolved from the current authentication claims
2. THE Product_Service SHALL filter all ProductPriceHistory queries by joining through the Product table's BusinessId
3. THE Autocomplete_Service SHALL filter all product search queries by the authenticated user's BusinessId
4. THE Autocomplete_Service SHALL filter all historical line item queries by the BusinessId of the parent Invoice or Quotation
5. IF the authenticated user's BusinessId cannot be resolved from the authentication claims, THEN THE Product_Service SHALL return zero results for all queries
6. IF a request references a Product that does not belong to the authenticated user's BusinessId, THEN THE Product_Service SHALL treat the resource as not found and return no data for that resource
7. WHEN the Product_Service creates a new Product record (via manual creation or auto-population), THE Product_Service SHALL stamp the record with the authenticated user's BusinessId

### Requirement 8: Product Price History View

**User Story:** As a business operator, I want to view the price change history for a product, so that I can track how pricing has evolved over time.

#### Acceptance Criteria

1. WHEN the user views a product's detail or edit form, THE Product_Controller SHALL display a price history section showing all ProductPriceHistory records for that product ordered by EffectiveFromUtc descending (most recent first)
2. THE Product_Controller SHALL display each price history entry with: SellingPrice, CostPrice, EffectiveFromUtc formatted as a readable date and time, and the name of the user who made the change (resolved from ChangedByUserId)
3. IF a product has no price history records, THEN THE Product_Controller SHALL display a message indicating no price changes have been recorded

