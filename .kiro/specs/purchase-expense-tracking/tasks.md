# Implementation Plan: Purchase & Expense Tracking

## Overview

This plan implements the Purchase & Expense Tracking module (Module 5) following the established MVC + Service + Repository architecture. Tasks are ordered to build from the data layer up through services, controllers, and UI, with each step building on the previous. The database tables and EF Core entities already exist; this plan covers repositories, services, controllers, view models, views, bulk entry, and CSV import.

## Tasks

- [x] 1. Database migration for PurchaseOriginType lookup table
  - [x] 1.1 Create SQL migration to add `[purchase].[PurchaseOriginType]` table and seed data
    - Create migration file `045_CreatePurchaseOriginTypeTable.sql` in `Portal.Database/Migrations/`
    - Create the `[purchase].[PurchaseOriginType]` table with columns `Id INT NOT NULL PRIMARY KEY` and `Name NVARCHAR(50) NOT NULL`
    - Insert seed rows: (1, 'Domestic'), (2, 'EuReverseCharge'), (3, 'NonEu')
    - Add migration to replace `IsEuReverseCharge` BIT column with `PurchaseOriginTypeId INT NOT NULL DEFAULT 1` FK on `[purchase].[Purchase]` if not already present
    - _Requirements: 7.1_

- [x] 2. Repository layer implementation
  - [x] 2.1 Implement SupplierRepository
    - Create `Portal.Infrastructure/Repositories/SupplierRepository.cs` extending `GenericStoredProcedureRepository<Supplier>`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — SELECT from `[purchase].[Supplier]` WHERE BusinessId matches
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single record
    - Implement `InsertAsync(Supplier entity)` — INSERT with BusinessId, Name, IsActive, CreatedAtUtc
    - Implement `UpdateAsync(Supplier entity)` — UPDATE Name by Id and BusinessId
    - Implement `DeactivateAsync(int id, int businessId)` — UPDATE IsActive = 0
    - Use full table names, null-safe SQL parameters, try/catch with rethrow
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9_

  - [x] 2.2 Implement ExpenseCategoryRepository
    - Create `Portal.Infrastructure/Repositories/ExpenseCategoryRepository.cs` extending `GenericStoredProcedureRepository<ExpenseCategory>`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — SELECT from `[purchase].[ExpenseCategory]` WHERE BusinessId matches
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single record
    - Implement `InsertAsync(ExpenseCategory entity)` — INSERT with BusinessId, Name, IsActive
    - Implement `UpdateAsync(ExpenseCategory entity)` — UPDATE Name by Id and BusinessId
    - Implement `DeactivateAsync(int id, int businessId)` — UPDATE IsActive = 0
    - Use full table names, null-safe SQL parameters, try/catch with rethrow
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [x] 2.3 Implement PurchaseRepository
    - Create `Portal.Infrastructure/Repositories/PurchaseRepository.cs` extending `GenericStoredProcedureRepository<Purchase>`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — SELECT from `[purchase].[Purchase]` WHERE BusinessId matches
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single record
    - Implement `InsertAsync(Purchase entity)` — INSERT with all required fields including PurchaseOriginTypeId and Country
    - Implement `UpdateAsync(Purchase entity)` — UPDATE all mutable fields and set UpdatedAtUtc
    - Implement `GetFilteredAsync(int businessId, int? supplierId, int? expenseCategoryId, DateOnly? dateFrom, DateOnly? dateTo)` — Dynamic WHERE clause with optional filters
    - Use full table names, null-safe SQL parameters, try/catch with rethrow
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9_

- [x] 3. Service layer implementation
  - [x] 3.1 Implement ISupplierService interface and SupplierService
    - Create `Portal.Infrastructure/Services/ISupplierService.cs` with methods: GetSuppliersAsync, GetActiveSuppliersAsync, GetSupplierByIdAsync, CreateSupplierAsync, UpdateSupplierAsync, DeactivateSupplierAsync
    - Create `Portal.Infrastructure/Services/SupplierService.cs` implementing ISupplierService
    - Inject ICurrentTenantService for BusinessId, SupplierRepository, and AuditLogRepository
    - Validate Name is not null/whitespace — return `ServiceResult.Fail` on failure
    - On create: overwrite BusinessId from tenant, set IsActive=true, set CreatedAtUtc=UTC now
    - On update: verify supplier belongs to current tenant before updating
    - On deactivate: set IsActive=false, write audit log entry
    - Write audit log entries for create and deactivate operations
    - Register as scoped service in DI container
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 16.3_

  - [x] 3.2 Implement IExpenseCategoryService interface and ExpenseCategoryService
    - Create `Portal.Infrastructure/Services/IExpenseCategoryService.cs` with methods: GetExpenseCategoriesAsync, GetActiveExpenseCategoriesAsync, GetExpenseCategoryByIdAsync, CreateExpenseCategoryAsync, UpdateExpenseCategoryAsync, DeactivateExpenseCategoryAsync
    - Create `Portal.Infrastructure/Services/ExpenseCategoryService.cs` implementing IExpenseCategoryService
    - Inject ICurrentTenantService for BusinessId, ExpenseCategoryRepository, and AuditLogRepository
    - Validate Name is not null/whitespace — return `ServiceResult.Fail` on failure
    - On create: overwrite BusinessId from tenant, set IsActive=true
    - On update: verify category belongs to current tenant before updating
    - On deactivate: set IsActive=false, write audit log entry
    - Write audit log entries for create and deactivate operations
    - Register as scoped service in DI container
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 16.4_

  - [x] 3.3 Implement IPurchaseService interface and PurchaseService
    - Create `Portal.Infrastructure/Services/IPurchaseService.cs` with methods: GetPurchasesAsync, GetFilteredPurchasesAsync, GetPurchaseByIdAsync, CreatePurchaseAsync, UpdatePurchaseAsync, BulkCreatePurchasesAsync
    - Create `Portal.Infrastructure/Services/PurchaseService.cs` implementing IPurchaseService
    - Inject ICurrentTenantService, PurchaseRepository, SupplierRepository, ExpenseCategoryRepository, AuditLogRepository
    - Implement validation: AmountExcludingVat > 0, VatAmount >= 0, Description not null/whitespace, valid SupplierId (active, same tenant), valid ExpenseCategoryId (active, same tenant), valid PurchaseOriginTypeId (1, 2, or 3)
    - Implement Purchase Origin Type logic: EU RC (Id=2) forces VatAmount=0 and TotalAmount=AmountExcludingVat; EU RC and Non-EU (Id=2,3) require non-whitespace Country; Domestic (Id=1) does not require Country
    - Compute TotalAmount = AmountExcludingVat + VatAmount for Domestic and Non-EU
    - On create: overwrite BusinessId from tenant, set CreatedAtUtc and UpdatedAtUtc to UTC now, write audit log
    - On update: set UpdatedAtUtc to UTC now, write audit log
    - Implement BulkCreatePurchasesAsync: validate all rows first, if any fail return errors without saving, if all pass wrap inserts in transaction
    - Register as scoped service in DI container
    - _Requirements: 6.1–6.12, 7.1–7.8, 8.4, 16.1, 16.2_

  - [x] 3.4 Write property tests for PurchaseService VAT logic (Properties 1, 2, 4, 12)
    - **Property 1: TotalAmount equals AmountExcludingVat plus VatAmount** — Random decimal AmountExcludingVat (0.01–999999.99), random VatAmount (0–999999.99), PurchaseOriginTypeId in {1,3}
    - **Property 2: EU Reverse Charge forces VatAmount to zero** — Random VatAmount (including large values), PurchaseOriginTypeId=2
    - **Property 4: Domestic/Non-EU preserves user-provided VatAmount** — Random valid VatAmount with PurchaseOriginTypeId in {1,3}
    - **Property 12: Domestic allows null Country** — Random null/whitespace Country with PurchaseOriginTypeId=1
    - Create `Portal.Tests/Unit/Properties/PurchaseVatPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 6.6, 7.2, 7.3, 7.5, 7.6**

  - [x] 3.5 Write property tests for validation rules (Properties 3, 5, 6)
    - **Property 3: EU RC/Non-EU requires non-whitespace Country** — Random whitespace strings with PurchaseOriginTypeId in {2,3}
    - **Property 5: Whitespace rejection for required text fields** — Random whitespace strings for Supplier Name, ExpenseCategory Name, Purchase Description
    - **Property 6: Numeric validation bounds** — Random non-positive AmountExcludingVat, random negative VatAmount
    - Create `Portal.Tests/Unit/Properties/ValidationPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 4.7, 5.7, 6.7, 6.8, 6.11, 7.4**

  - [x] 3.6 Write property test for tenant isolation (Property 7)
    - **Property 7: Tenant BusinessId assignment** — Random BusinessId values in input, verify overwritten by ICurrentTenantService.CurrentBusinessId
    - Create `Portal.Tests/Unit/Properties/TenantIsolationPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 4.4, 5.4, 6.4, 8.4**

  - [x] 3.7 Write property test for purchase filter correctness (Property 8)
    - **Property 8: Filter correctness** — Random purchase sets + random filter combinations, verify all returned purchases satisfy all criteria and no qualifying purchase is excluded
    - Create `Portal.Tests/Unit/Properties/FilterPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 3.6**

  - [x] 3.8 Write property test for batch atomicity (Property 9)
    - **Property 9: Batch save atomicity** — Random batches with 0–N invalid rows; if any invalid then zero persisted, if all valid then all persisted
    - Create `Portal.Tests/Unit/Properties/BulkEntryPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 17.7**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. View models and DTOs
  - [x] 5.1 Create Purchase view models and DTOs
    - Create `Portal.Web/Models/PurchaseFormViewModel.cs` with properties for form binding and dropdown lists (Suppliers, ExpenseCategories, OriginTypes)
    - Create `Portal.Web/Models/PurchaseListViewModel.cs` with purchase list, filter state, and dropdown lists
    - Create `Portal.Web/Models/BulkPurchaseRowDto.cs` for bulk entry JSON payload
    - Create `Portal.Web/Models/CsvPurchaseRowDto.cs` for CSV import row with validation state
    - _Requirements: 11.10, 14.3, 15.1, 17.2, 18.2_

- [x] 6. Controller layer implementation
  - [x] 6.1 Implement SupplierController
    - Create `Portal.Web/Controllers/SupplierController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Purchase)]`
    - Implement GET Index — return view with all suppliers for current tenant
    - Implement POST Create — validate, call service, return JSON `{ success, message }`
    - Implement POST Edit — validate, call service, return JSON `{ success, message }`
    - Implement POST Deactivate — call service, return JSON `{ success, message }`
    - Use `[ValidateAntiForgeryToken]` on all POST actions
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

  - [x] 6.2 Implement ExpenseCategoryController
    - Create `Portal.Web/Controllers/ExpenseCategoryController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Purchase)]`
    - Implement GET Index — return view with all expense categories for current tenant
    - Implement POST Create — validate, call service, return JSON `{ success, message }`
    - Implement POST Edit — validate, call service, return JSON `{ success, message }`
    - Implement POST Deactivate — call service, return JSON `{ success, message }`
    - Use `[ValidateAntiForgeryToken]` on all POST actions
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8_

  - [x] 6.3 Implement PurchaseController (CRUD and filtering)
    - Create `Portal.Web/Controllers/PurchaseController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Purchase)]`
    - Implement GET Index — return view with purchases, accept optional filter params (SupplierId, ExpenseCategoryId, DateFrom, DateTo), populate dropdowns
    - Implement GET Create — return form view with populated dropdowns (active suppliers, categories, origin types)
    - Implement POST Create — validate, call service, redirect to Index on success, redisplay form with errors on failure
    - Implement GET Edit/{id} — load purchase, return pre-populated form view
    - Implement POST Edit/{id} — validate, call service, redirect to Index on success, redisplay form with errors on failure
    - Use `[ValidateAntiForgeryToken]` on all POST actions
    - _Requirements: 11.1–11.10_

  - [x] 6.4 Implement PurchaseController bulk entry and CSV import endpoints
    - Implement GET BulkEntry — return bulk entry grid view with dropdown data
    - Implement POST BulkCreate — accept JSON array of `BulkPurchaseRowDto`, call `BulkCreatePurchasesAsync`, return JSON with success/errors including row-level error details
    - Implement GET CsvImport — return CSV upload view
    - Implement POST CsvImport — parse uploaded CSV file, match supplier/category names (case-insensitive), return JSON preview with validation status per row
    - Implement POST CsvConfirm — accept confirmed rows, call `BulkCreatePurchasesAsync`, return JSON with import count
    - Enforce 500-row CSV limit before parsing
    - Use `[ValidateAntiForgeryToken]` on all POST actions
    - _Requirements: 17.7, 17.8, 18.1, 18.2, 18.3, 18.4, 18.5, 18.6, 18.7, 18.8_

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Supplier and Expense Category UI views
  - [x] 8.1 Create Supplier list view with modal CRUD
    - Create `Views/Supplier/Index.cshtml` with table displaying Name, IsActive status, CreatedAtUtc
    - Add action buttons for Edit and Deactivate per row
    - Add "Create Supplier" button that opens a modal with Name input
    - Implement AJAX create/edit via modal form using fetch API with BlockUI.show()/hide() and SweetAlert2 feedback
    - Implement deactivation with SweetAlert2 confirmation dialog (confirmButtonColor: '#C24A4A')
    - Follow MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body)
    - Include antiforgery token in all POST requests
    - _Requirements: 12.1–12.8_

  - [x] 8.2 Create ExpenseCategory list view with modal CRUD
    - Create `Views/ExpenseCategory/Index.cshtml` with table displaying Name and IsActive status
    - Add action buttons for Edit and Deactivate per row
    - Add "Create Category" button that opens a modal with Name input
    - Implement AJAX create/edit via modal form using fetch API with BlockUI.show()/hide() and SweetAlert2 feedback
    - Implement deactivation with SweetAlert2 confirmation dialog (confirmButtonColor: '#C24A4A')
    - Follow MyChair Design System styling
    - Include antiforgery token in all POST requests
    - _Requirements: 13.1–13.8_

- [x] 9. Purchase UI views
  - [x] 9.1 Create Purchase list view with filters
    - Create `Views/Purchase/Index.cshtml` with table displaying InvoiceDate, Supplier Name, ExpenseCategory Name, Description, AmountExcludingVat, VatAmount, TotalAmount, and Origin Type badge
    - Add filter panel with dropdowns for Supplier, ExpenseCategory, Origin Type, and date pickers for start/end date
    - Display origin type badges: "EU RC" (blue) for EuReverseCharge, "Non-EU" (gold) for NonEu, no badge for Domestic
    - Add action links to Edit each purchase, and buttons for Create, Bulk Entry, and CSV Import
    - Follow MyChair Design System styling
    - _Requirements: 14.1–14.7_

  - [x] 9.2 Create Purchase Create/Edit form views
    - Create `Views/Purchase/Create.cshtml` and `Views/Purchase/Edit.cshtml` with form fields: SupplierId dropdown, ExpenseCategoryId dropdown, InvoiceNumber, InvoiceDate (date picker), Description, AmountExcludingVat, VatAmount, PurchaseOriginType (radio group: Domestic/EU RC/Non-EU), Country, Notes
    - Mark required fields with visual indicators (SupplierId, ExpenseCategoryId, InvoiceDate, Description, AmountExcludingVat)
    - Implement client-side Origin Type logic: EU RC disables VatAmount and sets to 0, shows Country as required; Non-EU keeps VatAmount enabled, shows Country as required; Domestic keeps VatAmount enabled, hides Country requirement
    - Add computed read-only TotalAmount field that updates dynamically
    - Pre-populate all fields on Edit view
    - Display validation errors adjacent to relevant fields
    - Populate dropdowns with only active records from current tenant
    - Follow MyChair Design System styling
    - _Requirements: 15.1–15.9_

  - [x] 9.3 Create Bulk Entry view
    - Create `Views/Purchase/BulkEntry.cshtml` with spreadsheet-style inline editable grid
    - Grid columns: Date, Invoice Number, Supplier (dropdown), Expense Category (dropdown), Description, Amount Excl. VAT, VAT Amount, Origin Type (dropdown), Country, computed Total
    - Implement "Add Row" (single) and "Add 5 Rows" buttons
    - Implement "Duplicate Row" and "Remove Row" per-row buttons
    - Implement EU RC logic per row: disable VAT Amount when EU RC selected, set to 0
    - Display live batch summary: filled row count, error count, batch total, origin type breakdown
    - Implement "Save All" button with BlockUI.show()/hide() and SweetAlert2 feedback
    - On validation failure: highlight invalid cells, display error count, do not save any rows
    - Implement keyboard navigation: Tab moves to next cell, Enter saves batch, Ctrl+D duplicates row
    - Follow MyChair Design System styling
    - _Requirements: 17.1–17.12_

  - [x] 9.4 Create CSV Import view
    - Create `Views/Purchase/CsvImport.cshtml` with file upload input accepting CSV files
    - On upload: POST file to controller, display preview grid with parsed rows and validation status per row
    - Flag invalid rows (unmatched supplier/category names, validation errors) with error messages
    - Implement "Confirm Import" button to commit valid rows via POST CsvConfirm
    - Use BlockUI.show()/hide() during parsing and import, SweetAlert2 for success/error showing imported count
    - Display error if file exceeds 500 rows
    - Follow MyChair Design System styling
    - _Requirements: 18.1–18.9_

- [x] 10. CSV parsing service
  - [x] 10.1 Implement CSV parsing and name-matching logic
    - Create a `CsvImportService` (or add to PurchaseService) that parses CSV content into `CsvPurchaseRowDto` list
    - Implement column mapping: InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName, Description, AmountExcludingVat, VatAmount, PurchaseOriginType, Country, Notes
    - Implement case-insensitive name matching for SupplierName → SupplierId and ExpenseCategoryName → ExpenseCategoryId against active records for current tenant
    - Validate each row: required fields, numeric bounds, origin type logic (EU RC → VatAmount=0, Country required for EU RC/Non-EU)
    - Flag unmatched names with descriptive error messages per row
    - Reject files exceeding 500 rows before parsing
    - _Requirements: 18.2, 18.4, 18.5, 18.7, 18.8_

  - [x] 10.2 Write property test for CSV round-trip (Property 10)
    - **Property 10: CSV parse round-trip** — Random valid purchase data → serialize to CSV format → parse back → verify field values preserved and correct column mapping
    - Create `Portal.Tests/Unit/Properties/CsvImportPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 18.2**

  - [x] 10.3 Write property test for case-insensitive name matching (Property 11)
    - **Property 11: Case-insensitive name matching** — Random case transformations of known supplier/category names → verify resolves to same record
    - Add to `Portal.Tests/Unit/Properties/CsvImportPropertyTests.cs` using FsCheck.Xunit
    - **Validates: Requirements 18.4**

- [x] 11. DI registration and module access wiring
  - [x] 11.1 Register all services and repositories in DI container
    - Register SupplierRepository, ExpenseCategoryRepository, PurchaseRepository as scoped services
    - Register SupplierService (ISupplierService), ExpenseCategoryService (IExpenseCategoryService), PurchaseService (IPurchaseService) as scoped services
    - Register CsvImportService if created as separate service
    - Add `Purchase = 5` to `PortalModules` constants if not already present
    - Verify `[ModuleAccess(PortalModules.Purchase)]` attribute works on controllers
    - _Requirements: 4.2, 5.2, 6.2, 9.1, 10.1, 11.1_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit
- Unit tests validate specific examples and edge cases
- The database tables and EF Core entities already exist — this plan covers the application layer only
- All UI follows the MyChair Design System and uses BlockUI + SweetAlert2 for AJAX interactions
- All repositories follow the established GenericStoredProcedureRepository pattern with full table names and null-safe SQL parameters

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "5.1"] },
    { "id": 3, "tasks": ["3.4", "3.5", "3.6", "3.7", "3.8", "6.1", "6.2"] },
    { "id": 4, "tasks": ["6.3", "6.4", "10.1"] },
    { "id": 5, "tasks": ["8.1", "8.2", "9.1", "10.2", "10.3"] },
    { "id": 6, "tasks": ["9.2", "9.3", "9.4"] },
    { "id": 7, "tasks": ["11.1"] }
  ]
}
```
