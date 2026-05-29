# Implementation Plan: Purchase Classification Enhancements

## Overview

This plan implements three new purchase classification dimensions (EU Paid origin type, Expense Type on categories, Purchase Type on purchases) across the database, entities, services, controllers, and views. Tasks are ordered to build foundational schema first, then entities and repositories, then service logic, then controllers and views, finishing with CSV import and list display updates.

## Tasks

- [x] 1. Database migrations and entity setup
  - [x] 1.1 Create migration script 067_CreateExpenseTypeTable.sql
    - Create `[purchase].[ExpenseType]` table with `Id INT NOT NULL PK` and `Name NVARCHAR(50) NOT NULL`
    - Seed rows: Id=1 Name="Services", Id=2 Name="Goods"
    - Add `ExpenseTypeId INT NULL` column to `[purchase].[ExpenseCategory]` with FK constraint to `[purchase].[ExpenseType](Id)`
    - Use `IF NOT EXISTS` for idempotency
    - _Requirements: 2.1, 2.2_

  - [x] 1.2 Create migration script 068_CreatePurchaseTypeTable.sql
    - Create `[purchase].[PurchaseType]` table with `Id INT NOT NULL PK` and `Name NVARCHAR(50) NOT NULL`
    - Seed rows: Id=1 Name="Asset", Id=2 Name="Stock", Id=3 Name="Expense"
    - Add `PurchaseTypeId INT NOT NULL DEFAULT(3)` column to `[purchase].[Purchase]` with FK constraint to `[purchase].[PurchaseType](Id)`
    - Use `IF NOT EXISTS` for idempotency; existing purchases get default value 3 (Expense)
    - _Requirements: 3.1, 3.2, 3.8_

  - [x] 1.3 Create migration script 069_AddEuPaidOriginType.sql
    - Insert Id=4, Name="EuPaid" into `[purchase].[PurchaseOriginType]`
    - Use `IF NOT EXISTS` for idempotency
    - _Requirements: 1.1, 1.6_

  - [x] 1.4 Create ExpenseType and PurchaseType entity classes
    - Create `Portal.Infrastructure/Entities/ExpenseType.cs` with `Id` and `Name` properties
    - Create `Portal.Infrastructure/Entities/PurchaseType.cs` with `Id` and `Name` properties
    - _Requirements: 2.1, 3.1_

  - [x] 1.5 Update existing entities with new FK properties
    - Add `ExpenseTypeId` (int?) and `ExpenseType` navigation property to `ExpenseCategory.cs`
    - Add `PurchaseTypeId` (int) and `PurchaseType` navigation property to `Purchase.cs`
    - _Requirements: 2.2, 3.2_

  - [x] 1.6 Register new entities in PortalDbContext
    - Add `DbSet<ExpenseType>` and `DbSet<PurchaseType>` to `PortalDbContext`
    - Configure EF Core mappings for new entities and the new FK relationships on `ExpenseCategory` and `Purchase`
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

- [x] 2. Service layer validation updates
  - [x] 2.1 Update PurchaseService validation for origin type range 1–4
    - Extend `PurchaseOriginTypeId` validation to accept values 1 through 4
    - Ensure EU Paid (Id=4) requires non-empty Country (same as EU RC and Non-EU)
    - Return `ServiceResult.Fail("Country is required for EU Paid purchases.")` when Country is missing
    - _Requirements: 6.1, 6.3, 1.3, 1.5_

  - [x] 2.2 Add PurchaseTypeId validation to PurchaseService
    - Validate `PurchaseTypeId` is in {1, 2, 3} on every create and update
    - Return `ServiceResult.Fail("Purchase type is required. Select Asset, Stock, or Expense.")` for invalid values
    - _Requirements: 6.4, 3.7_

  - [x] 2.3 Update TotalAmount computation for EU Paid origin type
    - For `PurchaseOriginTypeId` 4 (EU Paid): `TotalAmount = AmountExcludingVat + VatAmount` (same as Domestic and Non-EU)
    - Ensure EU Reverse Charge (Id=2) still forces `VatAmount = 0` and `TotalAmount = AmountExcludingVat`
    - _Requirements: 6.2, 1.2, 1.4_

  - [x] 2.4 Add ExpenseTypeId validation to ExpenseCategoryService
    - On create: reject if `ExpenseTypeId` is null or not in {1, 2}
    - On update: reject if `ExpenseTypeId` is null or not in {1, 2}
    - Return `ServiceResult.Fail("Expense Type is required. Select Services or Goods.")` for invalid values
    - _Requirements: 2.3, 2.4, 2.7, 7.3_

  - [x] 2.5 Write property test: Origin Type Validation Range (Property 1)
    - **Property 1: Origin Type Validation Range**
    - For any PurchaseOriginTypeId in {1,2,3,4} validation accepts; for any value outside that set, validation rejects
    - **Validates: Requirements 6.1**

  - [x] 2.6 Write property test: Country Required for Non-Domestic Origin Types (Property 2)
    - **Property 2: Country Required for Non-Domestic Origin Types**
    - For any PurchaseOriginTypeId in {2,3,4} with empty/null Country, validation rejects; with non-empty Country, validation accepts
    - **Validates: Requirements 1.3, 1.5, 6.3**

  - [x] 2.7 Write property test: TotalAmount Computation by Origin Type (Property 3)
    - **Property 3: TotalAmount Computation by Origin Type**
    - EU RC → VatAmount=0, TotalAmount=AmountExcludingVat; Domestic/Non-EU/EU Paid → TotalAmount=AmountExcludingVat+VatAmount
    - **Validates: Requirements 6.2**

  - [x] 2.8 Write property test: PurchaseTypeId Validation Range (Property 6)
    - **Property 6: PurchaseTypeId Validation Range**
    - For any PurchaseTypeId in {1,2,3} validation accepts; for any value outside that set, validation rejects
    - **Validates: Requirements 3.3, 3.4, 6.4**

  - [x] 2.9 Write property test: ExpenseTypeId Required on Category Save (Property 7)
    - **Property 7: ExpenseTypeId Required on Category Save**
    - For any ExpenseTypeId null or not in {1,2}, service rejects; for ExpenseTypeId in {1,2}, service accepts
    - **Validates: Requirements 2.3, 2.4, 2.7**

- [x] 3. Checkpoint - Ensure all service layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Controller and view model updates
  - [x] 4.1 Update PurchaseController form building to load new lookups
    - Load `PurchaseType` list from database and populate `PurchaseFormViewModel.PurchaseTypes`
    - Load `ExpenseType` data alongside expense categories for display
    - Include EU Paid (Id=4) in origin type options
    - _Requirements: 4.1, 4.3, 2.5_

  - [x] 4.2 Update PurchaseFormViewModel and BulkPurchaseRowDto
    - Add `PurchaseTypeId` (default 3) and `PurchaseTypes` list to `PurchaseFormViewModel`
    - Add `PurchaseTypeId` (default 3) to `BulkPurchaseRowDto`
    - _Requirements: 3.3, 3.4, 3.5, 3.6_

  - [x] 4.3 Update PurchaseController validation for bulk entry
    - Extend `ValidateBulkRow` to validate `PurchaseOriginTypeId` range 1–4
    - Add `PurchaseTypeId` validation (1–3) to `ValidateBulkRow`
    - Validate Country required for EU Paid rows in bulk entry
    - _Requirements: 1.5, 4.2, 4.4, 6.1, 6.4_

  - [x] 4.4 Update MapFormToEntity to map PurchaseTypeId
    - Map `PurchaseTypeId` from view model to `Purchase` entity in both single form and bulk entry paths
    - _Requirements: 4.5_

  - [x] 4.5 Update ExpenseCategoryController for ExpenseType
    - Accept `expenseTypeId` parameter in Create and Edit actions
    - Reject if `expenseTypeId` is missing or invalid
    - Pass to service layer for persistence
    - _Requirements: 2.3, 2.4, 7.1, 7.2, 7.3_

  - [x] 4.6 Write property test: Form Mapping Consistency (Property 8)
    - **Property 8: Form Mapping Consistency**
    - For any valid purchase input data, mapping through single-form and bulk-entry paths produces entities with identical field values
    - **Validates: Requirements 4.5**

- [x] 5. Checkpoint - Ensure controller tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. View updates (Purchase Form and Bulk Entry)
  - [x] 6.1 Update Purchase Form view with Purchase Type radio buttons
    - Add radio button group for Purchase Type (Asset, Stock, Expense) with Expense pre-selected
    - Display all four origin type options in order: Domestic, EU Reverse Charge, EU Paid, Non-EU
    - Enable VAT Amount field when EU Paid is selected
    - Make Country field mandatory when EU Paid is selected
    - _Requirements: 1.2, 1.3, 1.7, 3.3, 3.5, 4.1, 4.3_

  - [x] 6.2 Update Purchase Form to display Expense Type as read-only
    - When user selects an expense category, show associated Expense Type (Services/Goods) as read-only text
    - Show no indicator if category has no ExpenseTypeId assigned
    - _Requirements: 2.5, 4.6_

  - [x] 6.3 Update Bulk Entry Form with Purchase Type dropdown and EU Paid option
    - Add Purchase Type dropdown column (Asset, Stock, Expense) with Expense pre-selected per row
    - Add EU Paid to origin type dropdown in order: Domestic, EU RC, EU Paid, Non-EU
    - Enable VAT Amount column when EU Paid is selected for a row
    - Require Country when EU Paid is selected for a row
    - _Requirements: 1.4, 1.5, 3.4, 3.6, 4.2, 4.4_

  - [x] 6.4 Update Bulk Entry Form to display Expense Type as read-only
    - When user selects an expense category in a row, show associated Expense Type as read-only text
    - Show no indicator if category has no ExpenseTypeId assigned
    - _Requirements: 2.6, 4.6_

  - [x] 6.5 Update inline expense category creation with Expense Type selection
    - Add Expense Type dropdown (Services/Goods) to inline category creation modal on Purchase Form
    - Add Expense Type dropdown to inline category creation on Bulk Entry Form
    - Reject creation if no Expense Type is selected
    - _Requirements: 7.1, 7.2, 7.3_

- [x] 7. CSV Import updates
  - [x] 7.1 Update ResolvePurchaseOriginTypeId for EU Paid mapping
    - Add case-insensitive mappings: "eupaid", "eu paid" → PurchaseOriginTypeId 4
    - Maintain existing mappings for Domestic, EU RC, Non-EU
    - Return null for unrecognised strings
    - _Requirements: 1.9, 6.5_

  - [x] 7.2 Implement PurchaseType resolver for CSV import
    - Create resolver function: "asset" → 1, "stock" → 2, "expense" → 3 (all case-insensitive)
    - Default to 3 (Expense) if column is absent or empty
    - Return null for unrecognised non-empty strings (mark row invalid)
    - _Requirements: 6.6_

  - [x] 7.3 Update ParseCsvRow to support column 11 (PurchaseType)
    - Parse column 11 as PurchaseType value
    - Apply PurchaseType resolver; default to Expense if column absent
    - Add `CsvPurchaseRowDto.PurchaseType` and `ResolvedPurchaseTypeId` fields
    - _Requirements: 6.6_

  - [x] 7.4 Write property test: CSV Origin Type Resolver (Property 4)
    - **Property 4: CSV Origin Type Resolver**
    - Any case variation of "EuPaid"/"eu paid"/"eupaid" → 4; existing mappings preserved; unknown strings → null
    - **Validates: Requirements 1.9, 6.5**

  - [x] 7.5 Write property test: CSV PurchaseType Resolver (Property 5)
    - **Property 5: CSV PurchaseType Resolver**
    - "Asset" (any case) → 1, "Stock" → 2, "Expense" → 3, empty/null → 3, unrecognised → null
    - **Validates: Requirements 6.6**

- [x] 8. Checkpoint - Ensure CSV import tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Purchase List display and filtering
  - [x] 9.1 Add EU Paid badge and Purchase Type column to Purchase List
    - Display pill badge for EU Paid origin type (distinct styling from EU RC and Non-EU badges)
    - Display Purchase Type (Asset, Stock, Expense) as badge or column for each row
    - Add EU Paid count to batch summary section
    - _Requirements: 5.1, 5.3, 5.5_

  - [x] 9.2 Add Purchase Type filter and EU Paid filter option to Purchase List
    - Add "EU Paid" option to origin type filter dropdown
    - Add Purchase Type filter dropdown (Asset, Stock, Expense)
    - Wire filters to query parameters and reload list
    - _Requirements: 5.2, 5.4_

  - [x] 9.3 Write property test: Purchase Type Filtering (Property 9)
    - **Property 9: Purchase Type Filtering**
    - For any list of purchases and filter value in {1,2,3}, result contains only and all purchases matching that PurchaseTypeId
    - **Validates: Requirements 5.4**

- [x] 10. Expense Category management UI updates
  - [x] 10.1 Update Expense Category edit form with Expense Type field
    - Display Expense Type dropdown (Services/Goods) on edit form
    - Pre-select current value; show unset for legacy categories with null ExpenseTypeId
    - Require selection before save; reject with validation error if unselected
    - _Requirements: 2.4, 2.7_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (fast-check)
- Unit tests validate specific examples and edge cases (xUnit)
- All migrations use `IF NOT EXISTS` for idempotency
- Lookup tables (ExpenseType, PurchaseType) are exempt from `CreatedAtUtc` per steering rules (static seed data)
- Follow established patterns: Controller → Service → Repository, try/catch with rethrow, Json(new { success, message }) for AJAX

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5"] },
    { "id": 2, "tasks": ["1.6"] },
    { "id": 3, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 4, "tasks": ["2.5", "2.6", "2.7", "2.8", "2.9"] },
    { "id": 5, "tasks": ["4.1", "4.2", "4.5"] },
    { "id": 6, "tasks": ["4.3", "4.4"] },
    { "id": 7, "tasks": ["4.6", "6.1", "6.2", "6.3", "6.4", "6.5"] },
    { "id": 8, "tasks": ["7.1", "7.2"] },
    { "id": 9, "tasks": ["7.3"] },
    { "id": 10, "tasks": ["7.4", "7.5"] },
    { "id": 11, "tasks": ["9.1", "9.2", "10.1"] },
    { "id": 12, "tasks": ["9.3"] }
  ]
}
```
