# Implementation Plan: Purchase Import Automation

## Overview

This plan implements a self-service bulk purchase import workflow: **Upload → Preview & Review → Confirm Import**. The system uses configurable parser templates to handle diverse supplier file formats (CSV/Excel), with supplier profiles providing default values for recurring imports.

The implementation creates a new `[import]` SQL schema with 3 tables, 5 new services, 2 new controllers, and a custom RFC 4180-compliant CSV parser. The feature is gated behind the Professional+ tier (module key: `purchase_import`).

## Tasks

- [x] 1. Database schema and migrations
  - [x] 1.1 Create `[import]` schema and `ParserTemplate` table migration
    - Create migration file `Portal.Database/Migrations/XXX_CreateImportSchema.sql`
    - `CREATE SCHEMA [import]` followed by `CREATE TABLE [import].[ParserTemplate]`
    - Columns: Id (INT IDENTITY PK), BusinessId (INT FK → Business), SupplierId (INT FK → purchase.Supplier), Name (NVARCHAR(200)), FileFormatType (NVARCHAR(10)), HeaderRow (INT DEFAULT 1), DataStartRow (INT DEFAULT 2), SheetName (NVARCHAR(100) NULL), ColumnMappingsJson (NVARCHAR(MAX)), IsManaged (BIT DEFAULT 0), IsActive (BIT DEFAULT 1), CreatedAtUtc (DATETIME DEFAULT GETUTCDATE()), UpdatedAtUtc (DATETIME DEFAULT GETUTCDATE())
    - Include `USE [Guardian]` header per SQL standards
    - _Requirements: 2.1, 2.2, 9.1_

  - [x] 1.2 Create `SupplierImportProfile` table migration
    - Create migration file `Portal.Database/Migrations/XXX_CreateSupplierImportProfileTable.sql`
    - Columns: Id (INT IDENTITY PK), BusinessId (INT FK), SupplierId (INT FK), DefaultExpenseCategoryId (INT NULL FK), DefaultPurchaseOriginTypeId (INT NULL FK), DefaultCountry (NVARCHAR(100) NULL), CreatedAtUtc, UpdatedAtUtc
    - Add UNIQUE constraint on (BusinessId, SupplierId)
    - _Requirements: 4.1_

  - [x] 1.3 Create `ImportSession` table migration
    - Create migration file `Portal.Database/Migrations/XXX_CreateImportSessionTable.sql`
    - Columns: Id (INT IDENTITY PK), BusinessId (INT FK), SupplierId (INT FK), ParserTemplateId (INT NULL FK), FileName (NVARCHAR(500)), TotalRows (INT), ValidRows (INT), InvalidRows (INT), RowDataJson (NVARCHAR(MAX)), IsConfirmed (BIT DEFAULT 0), CreatedAtUtc (DATETIME DEFAULT GETUTCDATE())
    - _Requirements: 6.1, 7.1_

- [x] 2. Entity models and EF Core configuration
  - [x] 2.1 Create C# entity classes for import schema
    - Create `ParserTemplate.cs`, `SupplierImportProfile.cs`, `ImportSession.cs` in the Models/Import folder
    - Include navigation properties (Business, Supplier, ExpenseCategory, PurchaseOriginType)
    - Follow existing entity patterns (nullable annotations, property defaults)
    - _Requirements: 2.2, 4.1_

  - [x] 2.2 Add EF Core DbSet and configuration for import entities
    - Register DbSets in the portal DbContext: `DbSet<ParserTemplate>`, `DbSet<SupplierImportProfile>`, `DbSet<ImportSession>`
    - Add entity configuration (table name, schema, FK relationships, column types)
    - Configure `CreatedAtUtc` with `HasDefaultValueSql("GETUTCDATE()")`
    - _Requirements: 2.2, 4.1_

  - [x] 2.3 Create intermediate DTOs and enums
    - Create `ColumnMapping.cs` — source column/index, target field, format, isSkipped
    - Create `ParsedRow.cs` — row number, invoice date, amounts, raw values dictionary
    - Create `ValidatedRow.cs` — data, status enum, errors, warnings, isDuplicate, isRemoved
    - Create `ImportSessionResult.cs` — session id, counts, batch total, rows list
    - Create `ImportConfirmationResult.cs` — imported count, total amount
    - Create `ImportTargetFields.cs` — static constants for target field names, required fields array
    - Create `RowValidationStatus` enum — Valid, Warning, Invalid
    - _Requirements: 3.1, 5.1, 6.1_

- [x] 3. Checkpoint — Verify schema and models compile
  - Run `dotnet build` and ensure no compilation errors

- [x] 4. Module gating registration
  - [x] 4.1 Register `purchase_import` module key in `PortalModules.cs`
    - Add `public const string PurchaseImport = "purchase_import";` constant
    - Ensure it follows the existing module registration pattern
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 5. CSV and Excel parsing components
  - [x] 5.1 Implement RFC 4180 CSV parser
    - Create `CsvParser.cs` in a Parsing folder (Services/Import/Parsing)
    - Handle quoted fields with embedded commas, newlines, and escaped double-quotes
    - Preserve whitespace in quoted fields, trim whitespace in unquoted fields
    - Configurable field delimiter (default comma)
    - Return `List<string[]>` — each row as an array of field values
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [x] 5.2 Write property tests for CSV parser
    - **Property 9: CSV Round-Trip** — format→parse→format produces equivalent output
    - **Property 10: CSV Quoted Field Parsing** — quoted fields with commas/newlines/quotes extracted correctly
    - **Property 11: CSV Unquoted Field Trimming** — unquoted fields are trimmed
    - **Validates: Requirements 11.1, 11.2, 11.3, 11.4**

  - [x] 5.3 Implement Excel parser using ClosedXML
    - Create `ExcelParser.cs` in Parsing folder
    - Read specified worksheet (or first by default)
    - Support .xlsx format via ClosedXML
    - Extract cell values with type-aware conversion (dates, numbers, text)
    - Return `List<string[]>` consistent with CSV parser output
    - _Requirements: 5.4, 3.11_

  - [x] 5.4 Implement ColumnMapper
    - Create `ColumnMapper.cs` in Parsing folder
    - Resolve source columns by header name or positional index
    - Apply date format parsing using configured patterns
    - Apply decimal parsing using configured separator (period/comma)
    - Skip columns marked as "skip"
    - Return `List<ParsedRow>` from raw string arrays
    - _Requirements: 2.3, 3.1, 3.2, 3.3, 3.5_

  - [x] 5.5 Write property tests for column mapping and format parsing
    - **Property 3: Column Mapping Resolution** — header/index mapping extracts correct value
    - **Property 14: Date Format Round-Trip** — format→parse produces original date
    - **Property 15: Decimal Separator Round-Trip** — format→parse produces original decimal
    - **Validates: Requirements 2.3, 3.2, 3.3**

- [x] 6. Checkpoint — Verify parsing components compile
  - Run `dotnet build` and ensure no compilation errors

- [x] 7. FileParsingService
  - [x] 7.1 Implement `IFileParsingService` and `FileParsingService`
    - Create interface and implementation
    - `ParseCsv(Stream, ParserTemplate)` — use CsvParser + ColumnMapper
    - `ParseExcel(Stream, ParserTemplate)` — use ExcelParser + ColumnMapper
    - `AutoDetectAndParse(Stream, string fileExtension)` — header name matching for files without a template
    - Apply header row / data start row from template configuration
    - _Requirements: 5.1, 5.2, 5.4_

- [x] 8. ImportValidationService
  - [x] 8.1 Implement `IImportValidationService` and `ImportValidationService`
    - `ValidateRowsAsync(List<ParsedRow>, int supplierId, int businessId)` — validate all rows
    - `ValidateRowAsync(ParsedRow, int supplierId, int businessId)` — single row validation
    - Validation rules: InvoiceDate is valid, AmountExcludingVat > 0, VatAmount >= 0
    - EU Reverse Charge: VatAmount must be 0 AND Country required
    - Non-EU: Country required
    - Expense category resolution: case-insensitive name matching
    - Compute TotalAmount = AmountExcludingVat + VatAmount when not provided
    - Apply supplier profile defaults for missing fields
    - _Requirements: 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 4.2, 4.3, 4.4, 4.5_

  - [x] 8.2 Write property tests for validation logic
    - **Property 4: Supplier Profile Default Resolution** — file value takes precedence, profile default used when absent
    - **Property 5: Origin-Type Validation Constraints** — EU RC fails with VAT>0 or no Country; Non-EU fails without Country
    - **Property 6: Expense Category Case-Insensitive Resolution** — any casing returns same category ID
    - **Property 7: TotalAmount Computation Invariant** — computed total equals excl + VAT
    - **Validates: Requirements 4.2, 4.3, 4.4, 4.5, 5.6, 5.7, 5.8, 5.9**

- [x] 9. DuplicateDetectionService
  - [x] 9.1 Implement `IDuplicateDetectionService` and `DuplicateDetectionService`
    - `CheckDuplicatesAsync(List<ValidatedRow>, int businessId)` — check each row against existing purchases
    - Match criteria: SupplierId + InvoiceNumber + InvoiceDate + TotalAmount
    - Return `List<DuplicateCheckResult>` with flag and matched purchase reference
    - Advisory only — does not block import
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [x] 9.2 Write property test for duplicate detection
    - **Property 8: Duplicate Detection** — row flagged as duplicate iff matching purchase exists on all 4 fields
    - **Validates: Requirements 10.1, 10.2**

- [x] 10. Checkpoint — Verify services compile
  - Run `dotnet build` and ensure no compilation errors

- [x] 11. ParserTemplateService and Repository
  - [x] 11.1 Implement `ParserTemplateRepository`
    - CRUD operations using raw SQL with SqlParameter
    - `GetTemplatesForSupplierAsync(int supplierId, int businessId)` — includes managed templates
    - `GetByIdAsync(int templateId)`
    - `InsertAsync(ParserTemplate)` — INSERT INTO [import].[ParserTemplate]
    - `UpdateAsync(ParserTemplate)` — UPDATE [import].[ParserTemplate]
    - `DeleteAsync(int templateId)` — soft-delete (set IsActive = 0)
    - Follow repository-standards: try/catch (Exception ex) { throw; }, full table names, null-safe params
    - _Requirements: 2.1, 2.5, 2.6, 2.7_

  - [x] 11.2 Implement `IParserTemplateService` and `ParserTemplateService`
    - `GetTemplatesForSupplierAsync(int supplierId)` — filter by business, include managed
    - `GetTemplateByIdAsync(int templateId)` — with ownership validation
    - `CreateTemplateAsync(ParserTemplate)` — validate required mappings, persist
    - `UpdateTemplateAsync(ParserTemplate)` — block updates to managed templates by non-admin
    - `DeleteTemplateAsync(int templateId)` — block deletion of managed templates by non-admin
    - _Requirements: 2.1, 2.5, 2.6, 9.3, 9.4_

  - [x] 11.3 Write property tests for parser template operations
    - **Property 2: Parser Template Round-Trip** — persist and retrieve produces equivalent config
    - **Property 13: Required Field Validation** — template without InvoiceDate + AmountExcludingVat/TotalAmount fails validation
    - **Validates: Requirements 2.2, 3.6, 3.7**

- [x] 12. ImportSessionRepository
  - [x] 12.1 Implement `ImportSessionRepository`
    - `CreateSessionAsync(ImportSession)` — INSERT, return new Id
    - `GetByIdAsync(int sessionId, int businessId)` — with business scoping
    - `UpdateRowDataAsync(int sessionId, string rowDataJson)` — update JSON after edits
    - `DeleteAsync(int sessionId)` — hard delete (transient data)
    - `DeleteExpiredSessionsAsync(DateTime cutoff)` — cleanup sessions older than 24 hours
    - Follow repository-standards pattern
    - _Requirements: 6.4, 6.7, 7.1_

- [x] 13. ImportEngineService
  - [x] 13.1 Implement `IImportEngineService` and `ImportEngineService`
    - `ParseFileAsync(Stream, string fileName, int supplierId, int? templateId)`:
      - Validate file extension (.csv, .xlsx, .xls)
      - Validate file size (≤ 5 MB)
      - Resolve or auto-detect template
      - Call FileParsingService to parse rows
      - Validate row count (≤ 500)
      - Call ImportValidationService
      - Call DuplicateDetectionService
      - Persist ImportSession with RowDataJson
      - Return ImportSessionResult
    - `RevalidateRowAsync(int sessionId, int rowIndex, string field, string value)`:
      - Load session, update field value, re-validate single row, persist
    - `RemoveRowAsync(int sessionId, int rowIndex)`:
      - Load session, mark row as removed, update counts, persist
    - `ConfirmImportAsync(int sessionId)`:
      - Load session, filter to valid non-removed rows
      - Require at least 1 valid row
      - BEGIN TRANSACTION → bulk INSERT into [purchase].[Purchase] → INSERT audit log → COMMIT
      - Delete ImportSession on success
      - On failure: rollback, preserve session, return error
    - _Requirements: 1.1–1.7, 5.1, 6.4, 6.5, 6.7, 6.8, 7.1–7.6_

  - [x] 13.2 Write property tests for file validation and preview summary
    - **Property 1: File Extension Validation** — accept/reject equals membership in {csv, xlsx, xls}
    - **Property 12: Preview Summary Invariant** — TotalRows = ValidRows + InvalidRows
    - **Validates: Requirements 1.1, 1.2, 6.6**

- [x] 14. Checkpoint — Verify full service layer compiles
  - Run `dotnet build` and ensure no compilation errors

- [x] 15. SupplierImportProfile CRUD
  - [x] 15.1 Implement `SupplierImportProfileRepository`
    - `GetBySupplierAsync(int supplierId, int businessId)` — single profile per supplier per business
    - `UpsertAsync(SupplierImportProfile)` — INSERT or UPDATE (MERGE pattern)
    - Follow repository-standards pattern
    - _Requirements: 4.1, 4.6_

  - [x] 15.2 Wire supplier profile into ImportValidationService
    - During validation, load supplier profile via repository
    - Apply defaults for missing ExpenseCategoryId, PurchaseOriginTypeId, Country
    - File-provided values always take precedence
    - _Requirements: 4.2, 4.3, 4.4, 4.5_

- [x] 16. PurchaseImportController
  - [x] 16.1 Create `PurchaseImportController` with page actions
    - `[Authorize]` and `[ModuleAccess(PortalModules.PurchaseImport)]` attributes
    - `Index()` — Upload page (Step 1): supplier dropdown, template selection, file upload area
    - `Preview(int sessionId)` — Preview page (Step 2): grid with validation status
    - Load available suppliers and templates for the current business
    - _Requirements: 1.7, 6.1, 8.1_

  - [x] 16.2 Add AJAX endpoints to `PurchaseImportController`
    - `AxPostParseFile(IFormFile file, int supplierId, int? templateId)`:
      - Call ImportEngineService.ParseFileAsync
      - Return JSON with session ID and preview data
    - `AxPostConfirmImport(int sessionId)`:
      - Call ImportEngineService.ConfirmImportAsync
      - Return JSON success with count
    - `AxPostUpdateRow(int sessionId, int rowIndex, string field, string value)`:
      - Call ImportEngineService.RevalidateRowAsync
      - Return JSON with updated row status
    - `AxPostRemoveRow(int sessionId, int rowIndex)`:
      - Call ImportEngineService.RemoveRowAsync
      - Return JSON success
    - All endpoints use ValidateAntiForgeryToken, BlockUI pattern on client, SweetAlert2 for results
    - _Requirements: 6.4, 6.5, 6.7, 7.1, 7.4, 7.5_

- [x] 17. ParserTemplateController
  - [x] 17.1 Create `ParserTemplateController` with CRUD endpoints
    - `[Authorize]` and `[ModuleAccess(PortalModules.PurchaseImport)]` attributes
    - `Index()` — Template management page (list all templates for business)
    - `AxPostCreateTemplate(ParserTemplateFormModel model)` — validate and create
    - `AxPostUpdateTemplate(ParserTemplateFormModel model)` — validate, block managed template edits
    - `AxPostDeleteTemplate(int templateId)` — block managed template deletion
    - Return Json(new { success, message }) pattern
    - _Requirements: 2.1, 2.5, 2.6, 9.3_

- [x] 18. Checkpoint — Verify controllers compile
  - Run `dotnet build` and ensure no compilation errors

- [x] 19. Views — Upload page (Step 1)
  - [x] 19.1 Create Upload view (`PurchaseImport/Index.cshtml`)
    - Topbar: eyebrow "PURCHASES", heading "Import Purchases", description
    - Supplier dropdown (required) — populated from business suppliers
    - Template dropdown (optional, populated via AJAX after supplier selection)
    - Supplier profile defaults display (shows current defaults for selected supplier)
    - File upload area with drag-and-drop support
    - Accepted formats hint: "CSV, XLSX, XLS — max 5 MB, 500 rows"
    - "Upload & Preview" button — triggers AxPostParseFile with BlockUI
    - On success: redirect to Preview page with sessionId
    - On error: SweetAlert2 error message
    - Soft-gate message for Foundation-tier users (if access denied)
    - _Requirements: 1.1–1.7, 2.4, 2.8, 8.2_

- [x] 20. Views — Preview page (Step 2)
  - [x] 20.1 Create Preview view (`PurchaseImport/Preview.cshtml`)
    - Topbar: heading "Review Import", file name and summary stats
    - Summary card: Total rows, Valid (green), Invalid (red), Warnings (amber), Batch total
    - Preview grid table:
      - Columns: #, Date, Invoice No, Description, Excl VAT, VAT, Total, Origin, Country, Category, Status
      - Row colour coding: green stripe for valid, red stripe for invalid, amber for warning
      - Inline error messages below invalid cells
      - Duplicate warning badge on flagged rows
    - Inline editing: click cell to edit, AJAX call to AxPostUpdateRow, re-validate and update status
    - Remove row button per row (AxPostRemoveRow)
    - "Confirm Import" button — enabled only when ≥1 valid row exists
    - Confirm triggers SweetAlert2 confirmation dialog, then AxPostConfirmImport with BlockUI
    - On success: SweetAlert2 success with count, redirect to purchase list
    - On failure: SweetAlert2 error, session preserved for retry
    - _Requirements: 6.1–6.8, 7.1–7.6, 10.2, 10.3, 10.4_

- [x] 21. Views — Template management
  - [x] 21.1 Create Template management view (`ParserTemplate/Index.cshtml`)
    - List of templates grouped by supplier
    - Each template shows: name, format type, column count, managed badge
    - "Create Template" button → modal/form with:
      - Supplier dropdown, name, file format (CSV/Excel), header row, data start row
      - Column mappings builder: add/remove mappings, source column/index, target field dropdown, format
      - Sheet name (Excel only)
    - Edit/Delete actions (disabled for managed templates)
    - Supplier import profile section: set defaults per supplier (category, origin type, country)
    - _Requirements: 2.1–2.8, 3.1–3.11, 4.1, 4.6, 9.3_

- [x] 22. Checkpoint — Verify views compile and render
  - Run `dotnet build` and ensure no compilation errors
  - Ensure all views have correct model references and no Razor syntax errors

- [x] 23. Integration wiring and navigation
  - [x] 23.1 Wire navigation and DI registration
    - Register all new services in DI container (Startup/Program.cs): IImportEngineService, IFileParsingService, IImportValidationService, IDuplicateDetectionService, IParserTemplateService
    - Register repositories: ParserTemplateRepository, ImportSessionRepository, SupplierImportProfileRepository
    - Add navigation link to Purchase Import in sidebar menu (gated behind module access)
    - Add ClosedXML NuGet package reference to the project
    - _Requirements: 8.1_

  - [x] 23.2 Wire supplier profile management into import flow
    - On Upload page: when supplier is selected, AJAX-load existing profile defaults and display them
    - Allow inline editing of supplier profile from Upload page (save via AxPost)
    - _Requirements: 4.1, 4.6_

- [x] 24. Audit logging
  - [x] 24.1 Add audit log entry on successful import confirmation
    - Log to existing AuditLog table: user ID, timestamp, "PurchaseImport" action, file name, row count
    - Follow existing audit logging patterns in the codebase
    - _Requirements: 7.6_

- [x] 25. Final checkpoint — Full build and integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional property-based tests and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation throughout implementation
- The `[import]` schema isolates import tables from core `[purchase]` and `[dbo]` schemas
- ClosedXML (MIT license) is the only new NuGet dependency
- Custom CSV parser is preferred over CsvHelper for full RFC 4180 control and round-trip property
- Import sessions are transient — deleted after confirmation or 24-hour expiry
- All AJAX endpoints follow `AxPost`/`AxGet` naming convention per coding golden rules
- BlockUI + SweetAlert2 pattern used for all meaningful AJAX operations per UI feedback standards
- Repository layer follows repository-standards: try/catch(Exception ex){throw;}, full table names, SqlParameter

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "4.1"] },
    { "id": 2, "tasks": ["5.1", "5.3", "5.4"] },
    { "id": 3, "tasks": ["5.2", "5.5", "7.1"] },
    { "id": 4, "tasks": ["8.1", "9.1"] },
    { "id": 5, "tasks": ["8.2", "9.2", "11.1", "12.1"] },
    { "id": 6, "tasks": ["11.2", "11.3", "15.1"] },
    { "id": 7, "tasks": ["13.1", "15.2"] },
    { "id": 8, "tasks": ["13.2", "16.1", "16.2", "17.1"] },
    { "id": 9, "tasks": ["19.1", "20.1", "21.1"] },
    { "id": 10, "tasks": ["23.1", "23.2", "24.1"] }
  ]
}
```
