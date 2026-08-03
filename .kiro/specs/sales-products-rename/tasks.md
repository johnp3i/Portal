# Implementation Plan: Sales Products Rename & Catalog Linking

## Overview

Renames the Sales Pipeline "Products" to "Products & Services" across UI, and adds an optional FK linking sales products to the Product Catalog for reference pricing.

## Tasks

- [ ] 1. Database migration
  - [ ] 1.1 Create migration to add `ProductId` (INT NULL, FK → [product].[Product]) to `[sales].[SalesProduct]`
  - [ ] 1.2 Add nonclustered index on `ProductId`

- [ ] 2. Entity and repository updates
  - [ ] 2.1 Add `ProductId` property to `SalesProduct` entity
  - [ ] 2.2 Update `SalesProductRepository` INSERT/UPDATE queries to include `ProductId`
  - [ ] 2.3 Update SELECT queries to include `ProductId`

- [ ] 3. Service layer
  - [ ] 3.1 Update `SalesProductService` to accept optional `ProductId` on create/edit
  - [ ] 3.2 Add method to get linked catalog product details (name, code, price, VAT rate)

- [ ] 4. UI rename
  - [ ] 4.1 Rename navigation item from "Products" to "Products & Services"
  - [ ] 4.2 Rename page heading and subheading
  - [ ] 4.3 Rename "New Product" button to "New Product / Service"
  - [ ] 4.4 Update all SweetAlert2 messages to use "product/service" terminology
  - [ ] 4.5 Update the lead board product selection dropdown label

- [ ] 5. Catalog linking UI
  - [ ] 5.1 Add "Link to Catalog" dropdown to create/edit form (populated from Product Catalog)
  - [ ] 5.2 Show linked catalog info on detail view (code, price, VAT rate) as read-only reference
  - [ ] 5.3 Add "Unlink" option to remove the association

- [ ] 6. Verification
  - [ ] 6.1 Verify existing sales products still work without a link (backward compatible)
  - [ ] 6.2 Verify navigation clearly distinguishes Catalog "Products" from Opportunities "Products & Services"

## Notes

- The rename is cosmetic — no schema changes for the rename itself
- The linking is additive — existing sales products get `ProductId = NULL` (no migration data needed)
- The catalog dropdown only shows active products from the business
- Linking is reference-only — no live price sync

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4", "4.5", "5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["6.1", "6.2"] }
  ]
}
```
