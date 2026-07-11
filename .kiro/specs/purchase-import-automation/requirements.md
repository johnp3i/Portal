# Requirements Document

## Introduction

Purchase Import Automation enables businesses on the Portal platform to bulk-record purchases by uploading CSV or Excel files from their suppliers. The system supports supplier-specific parser templates with configurable column mappings, so recurring imports from the same supplier require minimal manual correction. The feature replaces the current manual SQL-script-based bulk import process with a user-facing, self-service workflow available to Professional+ tier subscribers.

## Glossary

- **Import_Engine**: The server-side component that orchestrates file upload, parsing, validation, and bulk insertion of purchase records.
- **Parser_Template**: A reusable, supplier-specific configuration that defines how columns in an uploaded file map to purchase fields.
- **Column_Mapping**: A single mapping entry within a Parser_Template that associates a source column (by name or position) to a target purchase field.
- **Supplier_Profile**: Default values (ExpenseCategoryId, PurchaseOriginTypeId, Country) stored against a supplier to auto-populate fields during import.
- **Import_Session**: A transient record representing one upload-parse-review-confirm cycle, holding the parsed rows and user corrections.
- **Preview_Grid**: The UI table that displays parsed rows for user review before confirmation.
- **Business**: The multi-tenant entity representing a subscribing company on the platform.
- **Purchase**: An expense entry in the [purchase].Purchase table with invoice, amount, VAT, supplier, and category details.
- **Supplier**: A vendor entity from whom purchases are made, belonging to a Business.
- **Expense_Category**: A classification for purchase entries, belonging to a Business.
- **Platform_Admin**: A super admin who can create managed parser templates on behalf of businesses (monetisation pathway).

## Requirements

### Requirement 1: File Upload

**User Story:** As a business user, I want to upload a CSV or Excel file containing purchase records, so that I can record multiple purchases in a single operation.

#### Acceptance Criteria

1. WHEN a user uploads a file, THE Import_Engine SHALL accept files with extensions .csv, .xlsx, and .xls.
2. WHEN a file with an unsupported extension is uploaded, THE Import_Engine SHALL reject the file and return an error message indicating the accepted formats.
3. THE Import_Engine SHALL enforce a maximum file size of 5 MB per upload.
4. IF a file exceeds 5 MB, THEN THE Import_Engine SHALL reject the file and return an error message stating the size limit.
5. THE Import_Engine SHALL enforce a maximum of 500 data rows per file (excluding header rows).
6. IF a file exceeds 500 data rows, THEN THE Import_Engine SHALL reject the file and return an error message stating the row limit.
7. WHEN a file is uploaded, THE Import_Engine SHALL associate the import with the authenticated user's BusinessId.

### Requirement 2: Supplier-Specific Parser Templates

**User Story:** As a business user, I want to configure parser templates for my suppliers, so that future imports from the same supplier are parsed automatically without manual column mapping each time.

#### Acceptance Criteria

1. THE Import_Engine SHALL allow a Business user to create a Parser_Template for a specific Supplier.
2. WHEN a Parser_Template is created, THE Import_Engine SHALL store the template name, associated SupplierId, file format type (CSV or Excel), and an ordered list of Column_Mappings.
3. THE Import_Engine SHALL support mapping source columns by header name or by positional index (zero-based).
4. WHEN a Parser_Template exists for a Supplier, THE Import_Engine SHALL automatically apply the template when a file is uploaded for that Supplier.
5. THE Import_Engine SHALL allow a Business user to update an existing Parser_Template.
6. THE Import_Engine SHALL allow a Business user to delete a Parser_Template.
7. THE Import_Engine SHALL allow multiple Parser_Templates per Supplier (to handle different file formats from the same supplier).
8. WHEN multiple templates exist for a Supplier, THE Import_Engine SHALL present the user with a template selection option during upload.

### Requirement 3: Column Mapping Configuration

**User Story:** As a business user, I want to define which columns in my supplier's file correspond to which purchase fields, so that the system can parse diverse file formats correctly.

#### Acceptance Criteria

1. THE Column_Mapping SHALL support mapping to the following target fields: InvoiceDate, InvoiceNumber, Description, AmountExcludingVat, VatAmount, TotalAmount, PurchaseOriginType, Country, and Notes.
2. THE Column_Mapping SHALL allow specifying a date format pattern for the InvoiceDate field (e.g., "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy").
3. THE Column_Mapping SHALL allow specifying a decimal separator for numeric fields (period or comma).
4. WHEN a Column_Mapping includes TotalAmount but not AmountExcludingVat and VatAmount separately, THE Import_Engine SHALL require the user to specify whether TotalAmount is VAT-inclusive or VAT-exclusive.
5. THE Column_Mapping SHALL allow marking source columns as "skip" to ignore columns not relevant to purchase records.
6. WHEN a required target field has no Column_Mapping defined, THE Import_Engine SHALL report a validation error identifying the missing mapping.
7. THE Import_Engine SHALL treat InvoiceDate and AmountExcludingVat (or TotalAmount) as required target fields; all other target fields are optional.
8. THE Column_Mapping SHALL allow specifying the header row number (1-based) indicating which row contains column headers in the source file.
9. THE Column_Mapping SHALL allow specifying the data start row number (1-based) indicating the first row of actual data to parse (all rows above are skipped).
10. WHEN header row and data start row are not specified, THE Import_Engine SHALL default to header row = 1 and data start row = 2 (standard single-header format).
11. FOR Excel files, THE Column_Mapping SHALL allow specifying a worksheet name; WHEN not specified, THE Import_Engine SHALL read the first worksheet.

### Requirement 4: Supplier Profile Defaults

**User Story:** As a business user, I want to store default values for my suppliers (expense category, origin type, country), so that imported rows auto-populate these fields without requiring them in the file.

#### Acceptance Criteria

1. THE Import_Engine SHALL allow a Business user to configure a Supplier_Profile containing default values for ExpenseCategoryId, PurchaseOriginTypeId, and Country.
2. WHEN a Supplier_Profile exists and the uploaded file does not provide a value for ExpenseCategoryId, THE Import_Engine SHALL apply the Supplier_Profile default.
3. WHEN a Supplier_Profile exists and the uploaded file does not provide a value for PurchaseOriginTypeId, THE Import_Engine SHALL apply the Supplier_Profile default.
4. WHEN a Supplier_Profile exists and the uploaded file does not provide a value for Country, THE Import_Engine SHALL apply the Supplier_Profile default.
5. WHEN the uploaded file provides an explicit value for a field, THE Import_Engine SHALL use the file value and ignore the Supplier_Profile default.
6. THE Import_Engine SHALL allow a Business user to update a Supplier_Profile at any time.

### Requirement 5: File Parsing and Validation

**User Story:** As a business user, I want the system to parse my uploaded file and validate each row, so that I can see errors before committing the import.

#### Acceptance Criteria

1. WHEN a file is uploaded with a selected Parser_Template, THE Import_Engine SHALL parse each data row using the template's Column_Mappings.
2. WHEN a file is uploaded without a Parser_Template, THE Import_Engine SHALL attempt auto-detection by matching header names to target fields (case-insensitive).
3. IF auto-detection fails to match required columns, THEN THE Import_Engine SHALL prompt the user to create or select a Parser_Template.
4. WHEN parsing an Excel file, THE Import_Engine SHALL read the first worksheet by default.
5. THE Import_Engine SHALL validate each parsed row against the following rules: InvoiceDate is a valid date, AmountExcludingVat is greater than zero, VatAmount is zero or positive, and SupplierName (if present in file) matches an active Supplier.
6. WHEN a row contains an ExpenseCategoryName, THE Import_Engine SHALL resolve the category using case-insensitive name matching against active Expense_Categories for the Business.
7. WHEN PurchaseOriginTypeId resolves to EU Reverse Charge (2), THE Import_Engine SHALL enforce that VatAmount is zero and Country is provided.
8. WHEN PurchaseOriginTypeId resolves to Non-EU (3), THE Import_Engine SHALL enforce that Country is provided.
9. THE Import_Engine SHALL compute TotalAmount as AmountExcludingVat + VatAmount for each row where TotalAmount is not explicitly provided.
10. THE Import_Engine SHALL assign PurchaseTypeId as Expense (3) by default for all imported rows unless a mapping or configuration overrides the value.

### Requirement 6: Preview and User Review

**User Story:** As a business user, I want to review parsed purchases in a preview grid before confirming the import, so that I can correct errors and verify the data.

#### Acceptance Criteria

1. WHEN parsing completes, THE Import_Engine SHALL display all parsed rows in a Preview_Grid showing: row number, InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName, Description, AmountExcludingVat, VatAmount, TotalAmount, PurchaseOriginType, Country, Notes, and validation status.
2. THE Preview_Grid SHALL visually distinguish valid rows from invalid rows using colour coding (green for valid, red for invalid).
3. THE Preview_Grid SHALL display the specific error message for each invalid row.
4. THE Import_Engine SHALL allow the user to edit individual cell values in the Preview_Grid to correct errors.
5. WHEN a user edits a cell in the Preview_Grid, THE Import_Engine SHALL re-validate the row and update the validation status in real time.
6. THE Preview_Grid SHALL display a summary showing total rows, valid rows, and invalid rows.
7. THE Import_Engine SHALL allow the user to remove individual rows from the Preview_Grid before confirmation.
8. THE Import_Engine SHALL allow the user to confirm the import only when at least one valid row exists.

### Requirement 7: Bulk Import Confirmation

**User Story:** As a business user, I want to confirm the reviewed import so that all valid purchases are created in the system in a single transaction.

#### Acceptance Criteria

1. WHEN the user confirms the import, THE Import_Engine SHALL insert all valid rows as Purchase records in a single database transaction.
2. THE Import_Engine SHALL assign the authenticated user's BusinessId to each created Purchase.
3. THE Import_Engine SHALL set CreatedAtUtc and UpdatedAtUtc to the current UTC timestamp for each created Purchase.
4. IF the database transaction fails, THEN THE Import_Engine SHALL roll back all inserts and return an error message to the user.
5. WHEN the import completes successfully, THE Import_Engine SHALL display a success message showing the count of imported purchases.
6. WHEN the import completes successfully, THE Import_Engine SHALL log an audit entry recording the user, timestamp, file name, and number of rows imported.

### Requirement 8: Subscription Tier Gating

**User Story:** As a platform operator, I want the purchase import feature restricted to Professional+ subscribers, so that it serves as a value driver for paid tiers.

#### Acceptance Criteria

1. THE Import_Engine SHALL be gated behind the Professional subscription tier (module key: `purchase_import`).
2. WHEN a Foundation-tier user navigates to the import page, THE Import_Engine SHALL display a soft-gate message explaining the feature and the required tier.
3. WHEN a Foundation-tier user attempts to access the import API endpoints directly, THE Import_Engine SHALL return a 403 response with a plan-upgrade message.

### Requirement 9: Platform Admin Managed Templates

**User Story:** As a platform admin, I want to create parser templates on behalf of businesses, so that complex supplier formats can be handled as a managed service (monetisation pathway).

#### Acceptance Criteria

1. THE Import_Engine SHALL allow a Platform_Admin to create a Parser_Template tagged as "managed" for any Business and Supplier combination.
2. WHEN a managed template exists, THE Import_Engine SHALL make the template available to the Business user alongside their own custom templates.
3. THE Import_Engine SHALL prevent Business users from editing or deleting managed templates.
4. THE Import_Engine SHALL allow a Platform_Admin to update or deactivate a managed template.

### Requirement 10: Duplicate Detection

**User Story:** As a business user, I want the system to warn me about potential duplicate purchases during import, so that I avoid recording the same expense twice.

#### Acceptance Criteria

1. WHEN previewing import rows, THE Import_Engine SHALL check each row against existing Purchase records for the same Business, matching on SupplierId, InvoiceNumber, InvoiceDate, and TotalAmount.
2. WHEN a potential duplicate is detected, THE Import_Engine SHALL flag the row with a warning indicator in the Preview_Grid.
3. THE Import_Engine SHALL allow the user to proceed with importing a flagged duplicate row if they explicitly confirm the row.
4. THE Import_Engine SHALL not block the import of flagged rows — duplicate detection is advisory only.

### Requirement 11: CSV Round-Trip (Export Compatibility)

**User Story:** As a developer, I want to ensure that the CSV parser and a corresponding CSV formatter produce consistent results, so that data integrity is preserved across import/export cycles.

#### Acceptance Criteria

1. THE Import_Engine SHALL parse quoted CSV fields containing commas, newlines, and escaped quotes correctly per RFC 4180.
2. FOR ALL valid Purchase row data, parsing a formatted CSV line and then re-formatting the parsed result SHALL produce an equivalent CSV line (round-trip property).
3. THE Import_Engine SHALL preserve leading and trailing whitespace within quoted fields during parsing.
4. THE Import_Engine SHALL trim unquoted field values during parsing.
