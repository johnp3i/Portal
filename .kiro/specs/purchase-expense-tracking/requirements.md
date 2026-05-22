# Requirements Document

## Introduction

Purchase & Expense Tracking (Module 5) enables business managers to record and manage business expenses with full VAT tracking, categorised by supplier and expense type. The module provides tenant-scoped CRUD operations for suppliers, expense categories, and purchase entries. It includes EU Reverse Charge handling where VAT is not applied to qualifying cross-border transactions, and supports filtering purchases by supplier, category, and date range.

The database tables (`[purchase].[Supplier]`, `[purchase].[ExpenseCategory]`, `[purchase].[Purchase]`) and EF Core entities already exist. This module implements the application layer (repositories, services, controllers, and UI) following the established patterns from Module 0 (Platform Foundation) and Module 1 (Customer Registry).

## Glossary

- **Supplier**: A vendor entity from whom purchases are made, stored in `[purchase].[Supplier]`. Each Supplier belongs to exactly one Business tenant.
- **ExpenseCategory**: A classification label for purchase entries, stored in `[purchase].[ExpenseCategory]`. Each ExpenseCategory belongs to exactly one Business tenant.
- **Purchase**: An expense entry representing money spent by the Business, with VAT tracking, stored in `[purchase].[Purchase]`.
- **SupplierRepository**: A table repository extending GenericStoredProcedureRepository for Supplier CRUD operations against `[purchase].[Supplier]`.
- **ExpenseCategoryRepository**: A table repository extending GenericStoredProcedureRepository for ExpenseCategory CRUD operations against `[purchase].[ExpenseCategory]`.
- **PurchaseRepository**: A table repository extending GenericStoredProcedureRepository for Purchase CRUD operations against `[purchase].[Purchase]`.
- **SupplierService**: A scoped service implementing ISupplierService that contains business logic for supplier management.
- **ExpenseCategoryService**: A scoped service implementing IExpenseCategoryService that contains business logic for expense category management.
- **PurchaseService**: A scoped service implementing IPurchaseService that contains business logic for purchase management including VAT calculation.
- **SupplierController**: An MVC controller handling HTTP requests for supplier list, create, edit, and deactivate operations.
- **ExpenseCategoryController**: An MVC controller handling HTTP requests for expense category list, create, edit, and deactivate operations.
- **PurchaseController**: An MVC controller handling HTTP requests for purchase list, create, edit, and filtering operations.
- **PurchaseOriginType**: A lookup table (`[purchase].[PurchaseOriginType]`) classifying the geographic origin of a purchase. Values: Domestic (Id=1), EuReverseCharge (Id=2), NonEu (Id=3).
- **Domestic**: A purchase from a local/national supplier where VAT is handled normally.
- **EU_Reverse_Charge**: A VAT mechanism for cross-border EU B2B transactions where the buyer accounts for VAT instead of the seller. When PurchaseOriginTypeId is 2 (EuReverseCharge), VatAmount is set to zero and TotalAmount equals AmountExcludingVat.
- **NonEu**: A purchase from a supplier outside the EU (e.g., UK, US, China). VAT is entered as-is (import VAT or zero). Country is required for statistical tracking.
- **Tenant_Isolation**: The enforcement that users can only access records belonging to their own Business, implemented via EF Core global query filters on BusinessId.
- **Deactivation**: Setting IsActive to false on a Supplier or ExpenseCategory record. Records are never hard-deleted because they may be referenced by existing Purchases.
- **AuditLog**: A record in `[audit].[AuditLog]` capturing significant data changes for traceability.

## Requirements

### Requirement 1: Supplier Repository

**User Story:** As a developer, I want a SupplierRepository with CRUD operations, so that the service layer can persist and retrieve supplier data following established repository patterns.

#### Acceptance Criteria

1. THE SupplierRepository SHALL extend GenericStoredProcedureRepository with Supplier as the type parameter.
2. THE SupplierRepository SHALL provide a method to retrieve all suppliers for a given BusinessId.
3. THE SupplierRepository SHALL provide a method to retrieve a single supplier by Id and BusinessId.
4. WHEN a new supplier is created, THE SupplierRepository SHALL insert a record into `[purchase].[Supplier]` with BusinessId, Name, IsActive, and CreatedAtUtc.
5. WHEN a supplier is updated, THE SupplierRepository SHALL update the Name on the matching record.
6. WHEN a supplier is deactivated, THE SupplierRepository SHALL set IsActive to false on the matching record.
7. THE SupplierRepository SHALL use full table names in SQL queries without aliases.
8. THE SupplierRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE SupplierRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 2: Expense Category Repository

**User Story:** As a developer, I want an ExpenseCategoryRepository with CRUD operations, so that the service layer can persist and retrieve expense category data following established repository patterns.

#### Acceptance Criteria

1. THE ExpenseCategoryRepository SHALL extend GenericStoredProcedureRepository with ExpenseCategory as the type parameter.
2. THE ExpenseCategoryRepository SHALL provide a method to retrieve all expense categories for a given BusinessId.
3. THE ExpenseCategoryRepository SHALL provide a method to retrieve a single expense category by Id and BusinessId.
4. WHEN a new expense category is created, THE ExpenseCategoryRepository SHALL insert a record into `[purchase].[ExpenseCategory]` with BusinessId, Name, and IsActive.
5. WHEN an expense category is updated, THE ExpenseCategoryRepository SHALL update the Name on the matching record.
6. WHEN an expense category is deactivated, THE ExpenseCategoryRepository SHALL set IsActive to false on the matching record.
7. THE ExpenseCategoryRepository SHALL use full table names in SQL queries without aliases.
8. THE ExpenseCategoryRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE ExpenseCategoryRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 3: Purchase Repository

**User Story:** As a developer, I want a PurchaseRepository with CRUD operations and filtering support, so that the service layer can persist, retrieve, and query purchase data following established repository patterns.

#### Acceptance Criteria

1. THE PurchaseRepository SHALL extend GenericStoredProcedureRepository with Purchase as the type parameter.
2. THE PurchaseRepository SHALL provide a method to retrieve all purchases for a given BusinessId.
3. THE PurchaseRepository SHALL provide a method to retrieve a single purchase by Id and BusinessId.
4. WHEN a new purchase is created, THE PurchaseRepository SHALL insert a record into `[purchase].[Purchase]` with all required fields including BusinessId, SupplierId, ExpenseCategoryId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, PurchaseOriginTypeId, Country, Notes, CreatedAtUtc, and UpdatedAtUtc.
5. WHEN a purchase is updated, THE PurchaseRepository SHALL update SupplierId, ExpenseCategoryId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, PurchaseOriginTypeId, Country, Notes, and UpdatedAtUtc on the matching record.
6. THE PurchaseRepository SHALL provide a method to retrieve purchases filtered by SupplierId, ExpenseCategoryId, and date range (start date and end date on InvoiceDate).
7. THE PurchaseRepository SHALL use full table names in SQL queries without aliases.
8. THE PurchaseRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE PurchaseRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 4: Supplier Service

**User Story:** As a developer, I want an ISupplierService interface and implementation, so that business logic for supplier management is encapsulated in a testable service layer.

#### Acceptance Criteria

1. THE SupplierService SHALL implement the ISupplierService interface.
2. THE SupplierService SHALL be registered as a scoped service in the DI container.
3. WHEN retrieving suppliers, THE SupplierService SHALL return only suppliers belonging to the current tenant's BusinessId.
4. WHEN creating a supplier, THE SupplierService SHALL set BusinessId from the current tenant, set IsActive to true, and set CreatedAtUtc to the current UTC time.
5. WHEN updating a supplier, THE SupplierService SHALL validate that the supplier belongs to the current tenant before updating.
6. WHEN deactivating a supplier, THE SupplierService SHALL set IsActive to false on the matching record.
7. THE SupplierService SHALL validate that Name is not null or whitespace before creating or updating a supplier.
8. IF validation fails, THEN THE SupplierService SHALL return a ServiceResult with success false and a descriptive error message.

### Requirement 5: Expense Category Service

**User Story:** As a developer, I want an IExpenseCategoryService interface and implementation, so that business logic for expense category management is encapsulated in a testable service layer.

#### Acceptance Criteria

1. THE ExpenseCategoryService SHALL implement the IExpenseCategoryService interface.
2. THE ExpenseCategoryService SHALL be registered as a scoped service in the DI container.
3. WHEN retrieving expense categories, THE ExpenseCategoryService SHALL return only expense categories belonging to the current tenant's BusinessId.
4. WHEN creating an expense category, THE ExpenseCategoryService SHALL set BusinessId from the current tenant and set IsActive to true.
5. WHEN updating an expense category, THE ExpenseCategoryService SHALL validate that the expense category belongs to the current tenant before updating.
6. WHEN deactivating an expense category, THE ExpenseCategoryService SHALL set IsActive to false on the matching record.
7. THE ExpenseCategoryService SHALL validate that Name is not null or whitespace before creating or updating an expense category.
8. IF validation fails, THEN THE ExpenseCategoryService SHALL return a ServiceResult with success false and a descriptive error message.

### Requirement 6: Purchase Service

**User Story:** As a developer, I want an IPurchaseService interface and implementation, so that business logic for purchase management including VAT calculation is encapsulated in a testable service layer.

#### Acceptance Criteria

1. THE PurchaseService SHALL implement the IPurchaseService interface.
2. THE PurchaseService SHALL be registered as a scoped service in the DI container.
3. WHEN retrieving purchases, THE PurchaseService SHALL return only purchases belonging to the current tenant's BusinessId.
4. WHEN creating a purchase, THE PurchaseService SHALL set BusinessId from the current tenant, set CreatedAtUtc and UpdatedAtUtc to the current UTC time.
5. WHEN updating a purchase, THE PurchaseService SHALL set UpdatedAtUtc to the current UTC time.
6. THE PurchaseService SHALL compute TotalAmount as AmountExcludingVat plus VatAmount.
7. THE PurchaseService SHALL validate that AmountExcludingVat is greater than zero.
8. THE PurchaseService SHALL validate that VatAmount is greater than or equal to zero.
9. THE PurchaseService SHALL validate that SupplierId references an active supplier belonging to the current tenant.
10. THE PurchaseService SHALL validate that ExpenseCategoryId references an active expense category belonging to the current tenant.
11. THE PurchaseService SHALL validate that Description is not null or whitespace.
12. IF validation fails, THEN THE PurchaseService SHALL return a ServiceResult with success false and a descriptive error message.

### Requirement 7: Purchase Origin Type and VAT Handling

**User Story:** As a business manager, I want to classify purchases by geographic origin (Domestic, EU Reverse Charge, or Non-EU), so that VAT is handled correctly per origin type and I can track spending by geographic region for reporting and VAT submissions.

#### Acceptance Criteria

1. THE system SHALL provide a `[purchase].[PurchaseOriginType]` lookup table with three entries: Domestic (Id=1), EuReverseCharge (Id=2), NonEu (Id=3).
2. WHEN PurchaseOriginTypeId is 2 (EuReverseCharge), THE PurchaseService SHALL set VatAmount to zero regardless of any user-provided VatAmount value.
3. WHEN PurchaseOriginTypeId is 2 (EuReverseCharge), THE PurchaseService SHALL compute TotalAmount as equal to AmountExcludingVat.
4. WHEN PurchaseOriginTypeId is 2 (EuReverseCharge) or 3 (NonEu), THE PurchaseService SHALL require that Country is not null or whitespace.
5. WHEN PurchaseOriginTypeId is 1 (Domestic), THE PurchaseService SHALL allow VatAmount to be any value greater than or equal to zero and SHALL NOT require Country.
6. WHEN PurchaseOriginTypeId is 3 (NonEu), THE PurchaseService SHALL allow VatAmount to be any value greater than or equal to zero (import VAT or zero if exempt).
7. WHEN PurchaseOriginTypeId is changed from 2 (EuReverseCharge) to 1 (Domestic) or 3 (NonEu) on an existing purchase, THE PurchaseService SHALL allow the user to provide a new VatAmount and recalculate TotalAmount.
8. THE PurchaseService SHALL validate that PurchaseOriginTypeId is one of the valid values (1, 2, or 3).

### Requirement 8: Tenant Isolation

**User Story:** As a business user, I want to see only my own business's suppliers, expense categories, and purchases, so that financial data remains private between tenants.

#### Acceptance Criteria

1. THE PortalDbContext SHALL apply a global query filter on Supplier ensuring that only records matching the current tenant's BusinessId are returned.
2. THE PortalDbContext SHALL apply a global query filter on ExpenseCategory ensuring that only records matching the current tenant's BusinessId are returned.
3. THE PortalDbContext SHALL apply a global query filter on Purchase ensuring that only records matching the current tenant's BusinessId are returned.
4. WHEN creating any record, THE respective service SHALL assign the BusinessId from ICurrentTenantService, preventing users from creating records under a different tenant.
5. IF a user attempts to access a record belonging to a different Business, THEN THE respective controller SHALL return a NotFound response.

### Requirement 9: Supplier Controller

**User Story:** As a business manager, I want to list, create, edit, and deactivate suppliers through the web interface, so that I can manage my supplier registry.

#### Acceptance Criteria

1. THE SupplierController SHALL require authentication via the Authorize attribute.
2. THE SupplierController SHALL delegate all business logic to ISupplierService.
3. WHEN a user navigates to the supplier list, THE SupplierController SHALL return a view displaying all suppliers for the current tenant.
4. WHEN a user submits a valid supplier creation request, THE SupplierController SHALL create the supplier and return a JSON success response.
5. WHEN a user submits a valid supplier edit request, THE SupplierController SHALL update the supplier and return a JSON success response.
6. WHEN a user requests supplier deactivation, THE SupplierController SHALL deactivate the supplier and return a JSON success response.
7. IF service validation fails, THEN THE SupplierController SHALL return a JSON error response with the validation message.
8. THE SupplierController SHALL use ValidateAntiForgeryToken on all POST actions.

### Requirement 10: Expense Category Controller

**User Story:** As a business manager, I want to list, create, edit, and deactivate expense categories through the web interface, so that I can manage my expense classification system.

#### Acceptance Criteria

1. THE ExpenseCategoryController SHALL require authentication via the Authorize attribute.
2. THE ExpenseCategoryController SHALL delegate all business logic to IExpenseCategoryService.
3. WHEN a user navigates to the expense category list, THE ExpenseCategoryController SHALL return a view displaying all expense categories for the current tenant.
4. WHEN a user submits a valid expense category creation request, THE ExpenseCategoryController SHALL create the expense category and return a JSON success response.
5. WHEN a user submits a valid expense category edit request, THE ExpenseCategoryController SHALL update the expense category and return a JSON success response.
6. WHEN a user requests expense category deactivation, THE ExpenseCategoryController SHALL deactivate the expense category and return a JSON success response.
7. IF service validation fails, THEN THE ExpenseCategoryController SHALL return a JSON error response with the validation message.
8. THE ExpenseCategoryController SHALL use ValidateAntiForgeryToken on all POST actions.

### Requirement 11: Purchase Controller

**User Story:** As a business manager, I want to list, create, and edit purchases through the web interface with filtering capabilities, so that I can record and review business expenses.

#### Acceptance Criteria

1. THE PurchaseController SHALL require authentication via the Authorize attribute.
2. THE PurchaseController SHALL delegate all business logic to IPurchaseService.
3. WHEN a user navigates to the purchase list, THE PurchaseController SHALL return a view displaying all purchases for the current tenant.
4. WHEN a user submits a valid purchase creation form, THE PurchaseController SHALL create the purchase and redirect to the purchase list.
5. WHEN a user submits a valid purchase edit form, THE PurchaseController SHALL update the purchase and redirect to the purchase list.
6. IF service validation fails on create or edit, THEN THE PurchaseController SHALL redisplay the form with validation error messages.
7. THE PurchaseController SHALL accept optional filter parameters for SupplierId, ExpenseCategoryId, date range start, and date range end.
8. WHEN filter parameters are provided, THE PurchaseController SHALL return only purchases matching the specified filters.
9. THE PurchaseController SHALL use ValidateAntiForgeryToken on all POST actions.
10. THE PurchaseController SHALL populate ViewBag or ViewData with active suppliers and expense categories for dropdown selection on create/edit forms.

### Requirement 12: Supplier Management UI

**User Story:** As a business manager, I want a supplier management screen following the MyChair Design System, so that I can view, create, edit, and deactivate suppliers in a consistent interface.

#### Acceptance Criteria

1. THE Supplier list view SHALL display suppliers in a table layout following the MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body).
2. THE Supplier list view SHALL display Name, IsActive status, and CreatedAtUtc for each supplier.
3. THE Supplier list view SHALL provide action buttons to edit and deactivate each supplier.
4. THE Supplier list view SHALL provide a button to create a new supplier.
5. WHEN a user clicks create or edit, THE Supplier UI SHALL display a modal or inline form with a Name input field.
6. WHEN a user confirms deactivation, THE Supplier UI SHALL display a SweetAlert2 confirmation dialog with danger styling (confirmButtonColor: '#C24A4A') before proceeding.
7. THE Supplier UI SHALL use BlockUI.show() before AJAX requests and BlockUI.hide() after completion.
8. THE Supplier UI SHALL use SweetAlert2 to display success and error messages after operations.

### Requirement 13: Expense Category Management UI

**User Story:** As a business manager, I want an expense category management screen following the MyChair Design System, so that I can view, create, edit, and deactivate expense categories in a consistent interface.

#### Acceptance Criteria

1. THE ExpenseCategory list view SHALL display expense categories in a table layout following the MyChair Design System.
2. THE ExpenseCategory list view SHALL display Name and IsActive status for each expense category.
3. THE ExpenseCategory list view SHALL provide action buttons to edit and deactivate each expense category.
4. THE ExpenseCategory list view SHALL provide a button to create a new expense category.
5. WHEN a user clicks create or edit, THE ExpenseCategory UI SHALL display a modal or inline form with a Name input field.
6. WHEN a user confirms deactivation, THE ExpenseCategory UI SHALL display a SweetAlert2 confirmation dialog with danger styling (confirmButtonColor: '#C24A4A') before proceeding.
7. THE ExpenseCategory UI SHALL use BlockUI.show() before AJAX requests and BlockUI.hide() after completion.
8. THE ExpenseCategory UI SHALL use SweetAlert2 to display success and error messages after operations.

### Requirement 14: Purchase List UI

**User Story:** As a business manager, I want a purchase list screen with filtering capabilities, so that I can review expenses filtered by supplier, category, and date range.

#### Acceptance Criteria

1. THE Purchase list view SHALL display purchases in a table layout following the MyChair Design System.
2. THE Purchase list view SHALL display InvoiceDate, Supplier Name, ExpenseCategory Name, Description, AmountExcludingVat, VatAmount, TotalAmount, and Purchase Origin Type indicator for each purchase.
3. THE Purchase list view SHALL provide a filter panel with dropdown selectors for Supplier, ExpenseCategory, and Purchase Origin Type, and date pickers for start date and end date.
4. WHEN filter values are selected, THE Purchase list view SHALL submit the filter parameters to the PurchaseController and display the filtered results.
5. THE Purchase list view SHALL provide action links to edit each purchase.
6. THE Purchase list view SHALL provide a button to create a new purchase.
7. THE Purchase list view SHALL visually indicate the purchase origin type with a distinct badge: "EU RC" (blue) for EuReverseCharge, "Non-EU" (gold) for NonEu, and no badge for Domestic.

### Requirement 15: Purchase Create/Edit Form UI

**User Story:** As a business manager, I want a purchase create/edit form with EU Reverse Charge toggle, so that I can record expenses with correct VAT handling.

#### Acceptance Criteria

1. THE Purchase form view SHALL display input fields for: SupplierId (dropdown), ExpenseCategoryId (dropdown), InvoiceNumber, InvoiceDate (date picker), Description, AmountExcludingVat, VatAmount, PurchaseOriginType (radio group or dropdown with options: Domestic, EU Reverse Charge, Non-EU), Country, and Notes.
2. THE Purchase form view SHALL mark SupplierId, ExpenseCategoryId, InvoiceDate, Description, and AmountExcludingVat as required with visual indicators.
3. WHEN the user selects "EU Reverse Charge" as the origin type, THE Purchase form view SHALL disable the VatAmount field, set its value to zero, and display the Country field as required.
4. WHEN the user selects "Non-EU" as the origin type, THE Purchase form view SHALL keep the VatAmount field enabled and display the Country field as required.
5. WHEN the user selects "Domestic" as the origin type, THE Purchase form view SHALL keep the VatAmount field enabled and remove the required indicator from the Country field.
5. THE Purchase form view SHALL display a computed TotalAmount field (read-only) that updates dynamically as AmountExcludingVat and VatAmount change.
6. THE Purchase form view SHALL pre-populate all fields with existing values when editing a purchase.
7. WHEN validation errors exist, THE Purchase form view SHALL display error messages adjacent to the relevant fields.
8. THE Purchase form view SHALL follow the MyChair Design System styling (input fields, buttons, spacing, typography).
9. THE Purchase form view SHALL populate Supplier and ExpenseCategory dropdowns with only active records from the current tenant.

### Requirement 16: Audit Logging

**User Story:** As a business manager, I want all purchase-related changes to be audit logged, so that there is a traceable record of expense entries for compliance and review.

#### Acceptance Criteria

1. WHEN a purchase is created, THE PurchaseService SHALL write an audit log entry recording the action, user, and purchase details.
2. WHEN a purchase is updated, THE PurchaseService SHALL write an audit log entry recording the action, user, and changed fields.
3. WHEN a supplier is created or deactivated, THE SupplierService SHALL write an audit log entry recording the action and user.
4. WHEN an expense category is created or deactivated, THE ExpenseCategoryService SHALL write an audit log entry recording the action and user.
5. THE audit log entries SHALL include the BusinessId, table name, record Id, action type, and timestamp.

### Requirement 17: Bulk Purchase Entry UI

**User Story:** As a business manager, I want a spreadsheet-style bulk entry view for purchases, so that I can record many expenses quickly without navigating to a separate form for each one.

#### Acceptance Criteria

1. THE Purchase module SHALL provide a "Bulk Entry" view accessible from the purchase list page.
2. THE Bulk Entry view SHALL display an inline editable grid where each row represents a new purchase with columns for: Date, Invoice Number, Supplier (dropdown), Expense Category (dropdown), Description, Amount Excl. VAT, VAT Amount, Origin Type (dropdown: Domestic/EU RC/Non-EU), Country, and computed Total.
3. WHEN the user clicks "Add Row", THE Bulk Entry view SHALL append a new empty row to the grid and focus the first input field of that row.
4. WHEN the user clicks "Add 5 Rows", THE Bulk Entry view SHALL append five empty rows to the grid.
5. WHEN the user selects "EU Reverse Charge" as the origin type on a row, THE Bulk Entry view SHALL disable the VAT Amount field for that row, set its value to zero, and compute Total as equal to Amount Excl. VAT.
6. THE Bulk Entry view SHALL display a live batch summary showing: filled row count, error count, batch total amount, and origin type breakdown (Domestic/EU RC/Non-EU counts).
7. WHEN the user clicks "Save All", THE PurchaseController SHALL validate all filled rows and create purchases for all valid rows in a single batch operation.
8. IF any row fails validation, THEN THE Bulk Entry view SHALL highlight the invalid cells with error styling and display the error count in the batch summary without saving any rows.
9. THE Bulk Entry view SHALL provide a "Duplicate Row" button per row that clones the row's values into a new row below it.
10. THE Bulk Entry view SHALL provide a "Remove Row" button per row that deletes the row from the grid.
11. THE Bulk Entry view SHALL support keyboard navigation: Tab moves to the next cell, Enter saves the current batch, and Ctrl+D duplicates the current row.
12. THE Bulk Entry view SHALL use BlockUI.show() before the batch save request and BlockUI.hide() after completion, with SweetAlert2 for success/error feedback.

### Requirement 18: CSV Purchase Import

**User Story:** As a business manager, I want to import purchases from a CSV file, so that I can bulk-load historical expense data or data exported from other systems without manual entry.

#### Acceptance Criteria

1. THE Purchase module SHALL provide a "CSV Import" action accessible from the purchase list page.
2. THE CSV Import view SHALL accept a file upload of CSV format with columns mapping to: InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName, Description, AmountExcludingVat, VatAmount, PurchaseOriginType (Domestic/EuReverseCharge/NonEu), Country, Notes.
3. WHEN a CSV file is uploaded, THE PurchaseController SHALL parse the file and display a preview grid showing the parsed rows with validation status per row.
4. THE CSV Import SHALL match SupplierName to existing active suppliers (case-insensitive) and ExpenseCategoryName to existing active expense categories (case-insensitive) for the current tenant.
5. IF a SupplierName or ExpenseCategoryName does not match an existing record, THEN THE preview SHALL flag that row as invalid with a descriptive error message.
6. WHEN the user confirms the import, THE PurchaseService SHALL create all valid purchases in a single batch operation.
7. THE CSV Import SHALL apply the same Purchase Origin Type logic as single-entry: when PurchaseOriginType is EuReverseCharge, VatAmount is set to zero and TotalAmount equals AmountExcludingVat; when NonEu, Country is required but VatAmount is preserved.
8. THE CSV Import SHALL reject files exceeding 500 rows and display an error message indicating the maximum allowed.
9. THE CSV Import SHALL use BlockUI.show() during file parsing and import, with SweetAlert2 for success/error feedback showing the count of imported records.
