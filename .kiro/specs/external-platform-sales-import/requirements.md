# Requirements Document

## Introduction

This document defines the External Platform Sales Import feature. It enables a Business to import line-level sales records from external systems into the Portal so those sales are declared in the correct VAT submission period.

The primary internal use case: **3 Inventors Limited** operates several live platforms (the Portal being one of them). Each platform generates its own invoices using the standard Portal invoice-number format (`{PlatformCode}-INV-{yyyy}-{NNNN}`). Because all platforms belong to a single legal entity with one VAT registration, every platform's sales must be consolidated into 3 Inventors' VAT return. Each external platform will run its own export service (built to a published canonical contract) that produces a sales file; 3 Inventors imports that file into the Portal.

The feature is designed as a **generic capability**: any Business can register an external platform (e.g., an online store, a marketplace channel, a secondary billing system) and import its sales the same way.

This feature **extends the existing Revenue Ingestion pipeline** (`ExternalSalesRecord`, `SalesImportService`, `SalesImportController`) rather than introducing a parallel system. It adds a first-class `ExternalPlatform` concept, a canonical fixed-schema import format, prefix validation, and automatic VAT-period assignment on import.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 multi-tenant back-office web application.
- **Business**: A registered organization on the Portal. For the primary use case, the Business is 3 Inventors Limited.
- **External_Platform**: A configurable entity representing an external system that produces sales for a Business (e.g., "Guardian", "MyChair", an online store). Distinct from a Revenue_Source (which represents a POS device/register).
- **Platform_Code**: The short prefix embedded in an external invoice number (e.g., `GRD` in `GRD-INV-2026-0042`). Matches the `{PlatformCode}` segment produced by `InvoiceNumberGenerator`.
- **External_Sales_Record**: An existing transaction-level sales record (`[revenue].ExternalSalesRecord`). One record = one external invoice/transaction.
- **Canonical_Import_Contract**: The published fixed-schema file format (CSV) that every external platform's export service must produce for import into the Portal.
- **VAT_Submission_Period**: A defined time period for which a VAT return is prepared (`[vat].VatSubmissionPeriod`).
- **Output_VAT**: Total VAT collected on sales that a business reports to tax authorities.
- **Import_Batch**: A single import operation (one uploaded file), tracked for audit and for grouping imported records.

## Requirements

### Requirement 1: External Platform Data Model

**User Story:** As a system architect, I want external platforms stored in a dedicated table, so that each external sales source (other than POS devices) is identifiable, selectable at import time, and validatable by its invoice prefix.

#### Acceptance Criteria

1. THE Portal database SHALL contain an `ExternalPlatform` table in the `[revenue]` schema with columns: `Id` (INT IDENTITY NOT NULL), `BusinessId` (INT NOT NULL), `Name` (NVARCHAR(200) NOT NULL), `PlatformCode` (NVARCHAR(10) NOT NULL), `Description` (NVARCHAR(500) NULL), `IsActive` (BIT NOT NULL DEFAULT 1), `CreatedAtUtc` (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE `ExternalPlatform` table SHALL have a primary key constraint on `Id`.
3. THE `ExternalPlatform` table SHALL have a foreign key from `BusinessId` to the `Business` table.
4. THE `ExternalPlatform` table SHALL enforce a unique constraint on (`BusinessId`, `PlatformCode`) so a Business cannot register two platforms with the same code.
5. THE `PlatformCode` column SHALL store the value uppercased and SHALL match the pattern `^[A-Za-z0-9]{1,10}$` (consistent with `InvoiceNumberGenerator.Parse`).
6. ALL queries against the `ExternalPlatform` table SHALL filter by the authenticated user's `BusinessId` to enforce tenant isolation.
7. THE `[revenue].ExternalSalesRecord` table SHALL add a nullable `ExternalPlatformId` (INT NULL) column with a foreign key to `ExternalPlatform`, so imported records are tagged to their originating platform.

### Requirement 2: External Platform CRUD

**User Story:** As a business user, I want to create, view, edit, and deactivate external platforms, so that I can manage the external systems I import sales from.

#### Acceptance Criteria

1. THE External Platforms management page SHALL list all external platforms for the current Business showing Name, Platform Code, Description, Status (Active/Inactive), and Created date.
2. WHEN the user submits the Create form with a valid Name and Platform Code, THE Portal SHALL insert a new `ExternalPlatform` record scoped to the current `BusinessId`, storing `PlatformCode` uppercased.
3. WHEN the user submits the Edit form, THE Portal SHALL update the Name, Platform Code, and Description on the existing record, re-validating uniqueness of Platform Code within the Business.
4. WHEN the user deactivates a platform, THE Portal SHALL set `IsActive` to 0.
5. WHEN the user reactivates a platform, THE Portal SHALL set `IsActive` to 1.
6. THE Create and Edit forms SHALL validate that Name is not empty (max 200 chars) and Platform Code matches `^[A-Za-z0-9]{1,10}$`.
7. IF the user submits a Platform Code that already exists for the Business, THEN THE Portal SHALL reject the submission with a clear message.
8. THE platform dropdown on the import form SHALL display only active platforms for the current Business.
9. IF the user deactivates a platform that has associated `ExternalSalesRecord` rows, THEN THE Portal SHALL allow deactivation and display an advisory that existing imported records retain their association.

### Requirement 3: Canonical Import Contract

**User Story:** As a platform operator, I want a single fixed import file format, so that every external platform's export service produces a consistent file the Portal can import without per-file column mapping.

#### Acceptance Criteria

1. THE Portal SHALL accept a CSV file conforming to the Canonical Import Contract with the following required columns, in any column order, matched case-insensitively by header name: `InvoiceNumber`, `InvoiceDate`, `NetAmount`, `VatAmount`, `TotalAmount`.
2. THE Canonical Import Contract SHALL define the following optional columns: `VatRate`, `CustomerName`, `Description`, `PaymentMethod`, `Currency`.
3. THE `InvoiceDate` value SHALL be parsed as an ISO date (`yyyy-MM-dd`); the parser SHALL also accept `dd/MM/yyyy` for tolerance but the contract SHALL specify `yyyy-MM-dd` as canonical.
4. THE monetary columns (`NetAmount`, `VatAmount`, `TotalAmount`) SHALL be parsed as decimals using `.` as the decimal separator per the contract.
5. WHEN `TotalAmount` is absent or non-positive on a row, THE Portal SHALL compute it as `NetAmount + VatAmount`.
6. THE Portal SHALL reject a file whose header is missing any required column, reporting which column is missing.
7. THE canonical contract SHALL be documented in a shared guideline file that can be handed to external platforms' engineering teams (see Requirement 10).

### Requirement 4: Import — Upload, Parse, and Preview

**User Story:** As a business user, I want to upload an external platform's sales file and preview what will be imported before committing, so that I can catch errors and duplicates first.

#### Acceptance Criteria

1. THE import page SHALL require the user to select a target External Platform before uploading a file.
2. THE Portal SHALL accept only `.csv` files up to 5 MB and up to 1000 data rows per file.
3. WHEN a file is uploaded, THE Portal SHALL parse it against the Canonical Import Contract and produce a preview containing: total rows, valid rows, duplicate count, invalid rows with reasons, prefix-mismatch warnings, and the batch total.
4. THE preview SHALL cache server-side (keyed by BusinessId) for at least 30 minutes and SHALL NOT persist any record until the user confirms.
5. WHEN a row's `InvoiceNumber` does not begin with the selected platform's `PlatformCode` followed by `-INV-`, THE Portal SHALL flag that row with a prefix-mismatch warning but SHALL NOT block import of that row.
6. THE preview SHALL mark a row invalid WHEN: `InvoiceDate` is unparseable, `NetAmount` is negative, or `VatAmount` is negative.
7. THE preview SHALL detect duplicates using the existing rule (same `InvoiceNumber` + `TransactionDate`, scoped to the platform) and exclude duplicates from the import count.

### Requirement 5: Import — Confirm and Persist

**User Story:** As a business user, I want to confirm the previewed import, so that valid rows are saved as line-level sales records assigned to the correct VAT period.

#### Acceptance Criteria

1. WHEN the user confirms the import, THE Portal SHALL insert one `ExternalSalesRecord` per valid, non-duplicate, non-excluded row within a single database transaction.
2. EACH inserted record SHALL set `BusinessId` from the authenticated session, `ExternalPlatformId` to the selected platform, `InvoiceNumber`, `TransactionDate` (from `InvoiceDate`), `NetAmount`, `VatAmount`, `TotalAmount`, `Description`, `PaymentMethod`, `IsActive = 1`, and `CreatedAtUtc = UtcNow`.
3. THE Portal SHALL allow the user to exclude specific previewed rows from the commit (by row index).
4. WHEN the transaction fails for any row, THE Portal SHALL roll back the entire batch and report that no records were created.
5. WHEN the import completes, THE Portal SHALL write an `AuditLog` entry recording the platform, file name, imported count, and batch total.
6. WHEN the import completes, THE Portal SHALL return the imported count and total amount for display.

### Requirement 6: VAT Period Auto-Assignment on Import

**User Story:** As a business user, I want imported sales automatically assigned to the VAT period that covers their invoice date, so that they appear in the correct VAT return without manual work.

#### Acceptance Criteria

1. WHEN inserting each `ExternalSalesRecord`, THE Portal SHALL attempt to assign `VatSubmissionPeriodId` to the VAT period whose date range (`PeriodStartDate` to `PeriodEndDate`) contains the record's `TransactionDate`, for the current Business.
2. IF the covering period has a `VatSubmission` with `IsSubmitted = 1`, THEN THE Portal SHALL NOT assign the record to that period and SHALL leave `VatSubmissionPeriodId` NULL (so it cannot alter an already-filed return).
3. IF no VAT period covers the `TransactionDate`, THEN `VatSubmissionPeriodId` SHALL remain NULL.
4. THE preview SHALL surface, per row, the period label the record will be assigned to (or "Unassigned" / "Locked — period submitted").
5. WHEN a row would be assigned to a submitted period, THE preview SHALL mark it with a warning and the record SHALL be imported as Unassigned.

### Requirement 7: VAT Integration — Output VAT Contribution

**User Story:** As a business user, I want imported external platform sales included in Output VAT for each period, so that my consolidated VAT return is accurate.

#### Acceptance Criteria

1. WHEN computing Output VAT for a VAT period, THE Portal SHALL include the sum of `ExternalSalesRecord.VatAmount` for all active records (`IsActive = 1`) assigned to that period, consistent with how Revenue Summaries contribute.
2. THE imported external platform sales SHALL contribute to Output VAT identically whether they originated from an `ExternalPlatform` or a `RevenueSource` (both are `ExternalSalesRecord` rows).
3. THE VAT Detail and VAT Period Report pages SHALL include imported external platform sales in the external revenue totals for the period.

### Requirement 8: Sales Records List — Platform Visibility

**User Story:** As a business user, I want to see and filter imported records by external platform, so that I can review each platform's contribution.

#### Acceptance Criteria

1. THE existing Sales Records list SHALL display the originating External Platform name (or Revenue Source name) per row.
2. THE list SHALL support filtering by External Platform in addition to the existing filters (date range, revenue source).
3. THE list SHALL continue to support soft-delete (cancel) and restore of individual records.
4. WHEN a record is cancelled, THE record SHALL NOT contribute to Output VAT or revenue aggregations.

### Requirement 9: Tenant Isolation and Access Control

**User Story:** As a business user, I want all external platform and import data scoped to my business, so that my data remains private.

#### Acceptance Criteria

1. ALL queries against `ExternalPlatform` and `ExternalSalesRecord` SHALL filter by the authenticated user's `BusinessId`.
2. THE import endpoints SHALL set `BusinessId` from the authenticated session; the client SHALL NOT provide `BusinessId`.
3. THE import and platform-management features SHALL be gated behind the same module access used by Sales Import today (`PortalModules.ZReportImport` / the `zreport_import` plan feature) OR a dedicated capability toggle — the exact gate SHALL be decided in design, but access SHALL require an active subscription.
4. THE platform dropdown and import shall verify the selected `ExternalPlatformId` belongs to the current Business before parsing or persisting.

### Requirement 10: External Platform Export Guideline (Documentation Deliverable)

**User Story:** As a platform operator, I want a written guideline describing the canonical export contract, so that each external platform's engineering team (and their Kiro agents) can build a conforming export service.

#### Acceptance Criteria

1. THE feature SHALL include a documentation deliverable describing the Canonical Import Contract: exact column names, required vs optional, data types, date and decimal formats, encoding (UTF-8), delimiter, invoice-number format, and a worked example file.
2. THE guideline SHALL state that invoice numbers must follow `{PlatformCode}-INV-{yyyy}-{NNNN}` and that the `PlatformCode` must match the platform registered in the Portal.
3. THE guideline SHALL specify VAT semantics: `NetAmount + VatAmount = TotalAmount`, amounts in the platform's reporting currency, and how zero-VAT / reverse-charge lines should be represented.
4. THE guideline SHALL define de-duplication expectations: exports should be stable and idempotent so re-importing an overlapping date range does not create duplicates (the Portal keys duplicates on invoice number + date).
5. THE guideline SHALL live under `.kiro/docs/` so it can be shared with external teams.

## Out of Scope (Future Phases)

- A push API/webhook for real-time ingestion (this phase is file import only).
- Automatic periodic pull/scheduled import from external platforms.
- Excel (.xlsx) parsing (CSV only this phase).
- Multi-currency conversion (records stored in the submitted currency; conversion is future work).
- Roll-up summary reporting per platform (line-level import now; summaries later, per user decision).
- Editing individual imported records (import + list + cancel/restore only).
