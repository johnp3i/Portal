# Implementation Plan: Invoice Line Product Type & Reverse Charge

## Overview

This plan implements two classification properties across the sales pipeline: a ProductType lookup (Services/Goods) on the Product master record with read-time derivation on quotation lines and immutable snapshot on invoice lines, plus a Reverse Charge boolean flag on both line tables enforcing VatRate=0% at the service layer. Implementation follows the established migration → entity → repository → service → controller → UI layering.

## Tasks

- [x] 1. Database migrations and lookup table
  - [x] 1.1 Create migration 071_CreateProductTypeTable.sql
    - Create `[product].[ProductType]` table with Id (INT, PK, no IDENTITY) and Name (NVARCHAR(50), NOT NULL)
    - Seed Services (Id=1) and Goods (Id=2) with idempotent IF NOT EXISTS checks
    - Add nullable `ProductTypeId` INT column to `[product].[Product]`
    - Add FK constraint `[FK_Product_ProductType]` referencing `[product].[ProductType]`
    - Follow idempotent pattern from 067_CreateExpenseTypeTable.sql
    - _Requirements: 1.1, 1.2, 2.1, 8.3, 8.4_

  - [x] 1.2 Create migration 072_AddIsReverseChargeToLines.sql
    - Add `[IsReverseCharge]` BIT NOT NULL DEFAULT 0 to `[quotation].[QuotationLine]`
    - Add `[IsReverseCharge]` BIT NOT NULL DEFAULT 0 to `[invoice].[InvoiceLine]`
    - Add `[ProductTypeId]` INT NULL to `[invoice].[InvoiceLine]`
    - Add FK constraint `[FK_InvoiceLine_ProductType]` referencing `[product].[ProductType]`
    - Use idempotent IF NOT EXISTS checks for columns and constraints
    - _Requirements: 5.1, 6.1, 8.4_

- [x] 2. Entity and ViewModel changes
  - [x] 2.1 Create ProductType entity and update Product, QuotationLine, InvoiceLine entities
    - Create `Portal.Infrastructure/Entities/ProductType.cs` with Id and Name properties
    - Add `ProductTypeId` (int?) and `ProductType` navigation property to `Product.cs`
    - Add `IsReverseCharge` (bool) to `QuotationLine.cs`
    - Add `IsReverseCharge` (bool) and `ProductTypeId` (int?) with `ProductType` navigation to `InvoiceLine.cs`
    - _Requirements: 1.1, 2.1, 5.1, 6.1_

  - [x] 2.2 Update QuotationLineFormViewModel and add display model support
    - Add `IsReverseCharge` (bool) property to `QuotationLineFormViewModel.cs`
    - Add `ProductTypeName` (string?) property to the quotation line display model used in `QuotationEditViewModel`
    - _Requirements: 3.1, 5.2_

- [x] 3. Repository changes
  - [x] 3.1 Create ProductTypeRepository (read-only)
    - Create `Portal.Infrastructure/Repositories/ProductTypeRepository.cs`
    - Implement `GetAllAsync()` returning all rows ordered by Id
    - Implement `GetByIdAsync(int id)` returning single record
    - Follow GenericStoredProcedureRepository pattern with try/catch rethrow
    - Use full table name `[product].[ProductType]` in queries
    - _Requirements: 1.1, 2.2_

  - [x] 3.2 Update ProductRepository to include ProductTypeId
    - Add `[ProductTypeId]` to all SELECT column lists
    - Add `@ProductTypeId` parameter to INSERT query
    - Add `[ProductTypeId] = @ProductTypeId` to UPDATE SET clause
    - Use `product.ProductTypeId ?? (object)DBNull.Value` for null safety
    - _Requirements: 2.1, 2.3_

  - [x] 3.3 Update QuotationLineRepository to include IsReverseCharge
    - Add `[IsReverseCharge]` to all SELECT column lists
    - Add `[IsReverseCharge]` column and `@IsReverseCharge` parameter to INSERT
    - Add `[IsReverseCharge] = @IsReverseCharge` to UPDATE SET clause
    - Add `new SqlParameter("@IsReverseCharge", entity.IsReverseCharge)` parameter
    - _Requirements: 5.1, 5.3_

  - [x] 3.4 Update InvoiceLineRepository to include IsReverseCharge and ProductTypeId
    - Add `[IsReverseCharge]`, `[ProductTypeId]` to all SELECT column lists
    - Add both columns and parameters to INSERT query
    - Add both to UPDATE SET clause with null-safe ProductTypeId parameter
    - _Requirements: 6.1, 6.4_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer changes
  - [x] 5.1 Update QuotationService with reverse charge validation
    - Add `bool isReverseCharge = false` parameter to `AddLineAsync` and `UpdateLineAsync`
    - Add validation: if `isReverseCharge && vatRate > 0` throw `ArgumentException("Reverse charge lines require 0% VAT")`
    - Set `IsReverseCharge` on the QuotationLine entity before persistence
    - _Requirements: 5.3, 5.6, 8.1, 8.5_

  - [x] 5.2 Update InvoiceService conversion to copy IsReverseCharge and snapshot ProductTypeId
    - In `ConvertFromQuotationAsync`, for each quotation line: resolve ProductTypeId from product via ProductCode lookup
    - Copy `IsReverseCharge` from quotation line to invoice line
    - Enforce `VatRate = 0` on invoice line when `IsReverseCharge = true`
    - Set `ProductTypeId` snapshot on invoice line from resolved product
    - Add RC validation to `AddLineAsync` / `UpdateLineAsync`: reject if `isReverseCharge && vatRate > 0`
    - _Requirements: 6.4, 7.1, 7.2, 7.3, 7.4, 8.2, 8.5_

  - [x] 5.3 Update ProductService to require ProductTypeId for new products
    - In `CreateAsync`, validate that `ProductTypeId` is provided (not null) — throw `ArgumentException("Product Type is required for new products")`
    - Validate `ProductTypeId` is 1 or 2 — throw `ArgumentException("Product Type must be Services (1) or Goods (2)")`
    - In `UpdateAsync`, allow ProductTypeId change; allow NULL for legacy products
    - _Requirements: 2.2, 2.3, 2.4, 8.3_

  - [x] 5.4 Write property test: Reverse charge invariant (quotation lines)
    - **Property 1: Reverse charge invariant (quotation lines)**
    - Generate random `isReverseCharge=true` with `vatRate > 0` — assert ArgumentException thrown and no persistence
    - Generate random valid combinations — assert success
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 5.3, 5.6, 8.1, 8.5**

  - [x] 5.5 Write property test: Reverse charge invariant (invoice lines)
    - **Property 2: Reverse charge invariant (invoice lines)**
    - Generate random `isReverseCharge=true` with `vatRate > 0` for invoice line add/update — assert ArgumentException
    - Generate random valid combinations — assert success
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 6.4, 8.2, 8.5**

  - [x] 5.6 Write property test: Conversion preserves reverse charge semantics
    - **Property 3: Conversion preserves reverse charge semantics**
    - Generate N quotation lines with arbitrary IsReverseCharge and VatRate values
    - Assert: each invoice line has same IsReverseCharge as source; RC=true lines have VatRate=0; RC=false lines preserve source VatRate
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 7.1, 7.2, 7.3**

  - [x] 5.7 Write property test: New product creation requires ProductTypeId
    - **Property 4: New product creation requires ProductTypeId**
    - Generate product creation requests with null ProductTypeId — assert ArgumentException
    - Generate requests with ProductTypeId=1 or 2 — assert success
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 2.2**

  - [x] 5.8 Write property test: ProductTypeId accepts only valid values
    - **Property 5: ProductTypeId accepts only valid values**
    - Generate random integers outside {1, 2} — assert rejection via service validation
    - Generate NULL, 1, 2 — assert acceptance
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 8.3**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Controller changes
  - [x] 7.1 Update QuotationController to pass IsReverseCharge
    - In `AddLine` action, read `model.IsReverseCharge` and pass to `_quotationService.AddLineAsync`
    - In `UpdateLine` action, read `model.IsReverseCharge` and pass to `_quotationService.UpdateLineAsync`
    - Handle ArgumentException from service layer — return `Json(new { success = false, message })` for AJAX or redirect with error for form posts
    - _Requirements: 5.3, 8.1_

  - [x] 7.2 Update ProductController to pass ProductTypeId
    - In `Create` action, read ProductTypeId from form and pass to `_productService.CreateAsync`
    - In `Edit` action, read ProductTypeId from form and pass to `_productService.UpdateAsync`
    - Populate ViewBag/ViewData with ProductType list from `_productTypeRepository.GetAllAsync()` for dropdown
    - Handle ArgumentException — return appropriate error response
    - _Requirements: 2.2, 2.3, 2.4_

- [x] 8. UI changes — Quotation Edit
  - [x] 8.1 Add Reverse Charge checkbox and Product Type badge to _SectionCards.cshtml
    - Add `<input type="checkbox" name="IsReverseCharge" value="true">` with label to each line form (both existing lines and add-line forms)
    - Pre-check the checkbox for existing lines where `IsReverseCharge == true`
    - Add Product Type badge (`<span>` with uppercase styling) derived from the line's linked product
    - Show badge only when ProductTypeName is not null/empty
    - _Requirements: 3.1, 3.2, 3.3, 3.5, 5.2_

  - [x] 8.2 Implement JavaScript toggleReverseCharge function
    - When checkbox checked: store current VatRate in `dataset.previousVatRate`, set VatRate to 0, make VatRate input readonly with reduced opacity
    - When checkbox unchecked: restore VatRate from `dataset.previousVatRate` (or product DefaultVatRate, or 0), remove readonly and restore opacity
    - Attach `onchange="toggleReverseCharge(this)"` to each RC checkbox
    - _Requirements: 5.3, 5.4, 5.5, 5.7_

  - [x] 8.3 Write property test: Reverse charge VatRate restoration
    - **Property 7: Reverse charge VatRate restoration**
    - Generate quotation lines with various DefaultVatRate values and toggle RC on/off
    - Assert: disabling RC restores to product DefaultVatRate if product linked, or 0% if no product
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 5.4, 5.7**

- [x] 9. UI changes — Invoice Detail
  - [x] 9.1 Add Reverse Charge label and Product Type badge to Invoice Detail view
    - Conditionally render "Reverse Charge" label (amber badge) when `line.IsReverseCharge == true`
    - Conditionally render Product Type badge when `line.ProductTypeId` is not null (resolve name from lookup)
    - Both displays are read-only — no edit controls
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.2, 6.3, 6.5_

- [x] 10. UI changes — Product Form
  - [x] 10.1 Add Product Type dropdown to Product Create/Edit forms
    - Add a `<select name="ProductTypeId">` dropdown populated with ProductType lookup values
    - On Create form: make selection required (client-side validation)
    - On Edit form: pre-select current ProductTypeId; allow empty for legacy products
    - _Requirements: 2.2, 2.3, 2.4_

- [x] 11. Integration wiring and product type derivation
  - [x] 11.1 Wire product type derivation for quotation line display
    - When loading quotation lines for display, resolve each line's ProductTypeName from the linked product's ProductTypeId
    - If line has no ProductCode or product has null ProductTypeId, set ProductTypeName to null
    - Ensure the product autocomplete/selection response includes ProductTypeName for immediate UI display
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 2.5_

  - [x] 11.2 Write property test: Product type derivation on quotation lines
    - **Property 6: Product type derivation on quotation lines**
    - Generate quotation lines with/without ProductCode, products with/without ProductTypeId
    - Assert: no ProductCode → no type shown; null ProductTypeId → no type shown; valid ProductTypeId → correct name displayed
    - Use FsCheck with minimum 100 iterations
    - **Validates: Requirements 2.5, 3.2, 3.3**

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck (.NET)
- Unit tests validate specific examples and edge cases
- Migrations follow the idempotent pattern established in 067_CreateExpenseTypeTable.sql
- The ProductType lookup is read-only at runtime — no CRUD UI needed
- Quotation lines derive ProductType at read-time from the linked product; invoice lines store an immutable snapshot

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["5.4", "5.5", "5.6", "5.7", "5.8"] },
    { "id": 5, "tasks": ["7.1", "7.2"] },
    { "id": 6, "tasks": ["8.1", "8.2", "9.1", "10.1"] },
    { "id": 7, "tasks": ["8.3", "11.1"] },
    { "id": 8, "tasks": ["11.2"] }
  ]
}
```
