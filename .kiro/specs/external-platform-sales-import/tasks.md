# Implementation Plan: External Platform Sales Import

## Overview

Add a first-class `ExternalPlatform` lookup and a canonical fixed-schema sales import that tags records to their platform and auto-assigns them to the covering (non-submitted) VAT period. Extends the existing Revenue Ingestion pipeline (`ExternalSalesRecord`, `SalesImportService`, `SalesImportController`) — no parallel system.

## Tasks

- [ ] 1. Database migrations
  - [ ] 1.1 Create `[revenue].[ExternalPlatform]` table
    - `USE [Portal]` header; idempotent `IF NOT EXISTS`
    - Columns: Id, BusinessId, Name(200), PlatformCode(10), Description(500), IsActive BIT DEFAULT 1, CreatedAtUtc DATETIME DEFAULT GETUTCDATE()
    - PK on Id; FK BusinessId → [dbo].[Business]; UNIQUE (BusinessId, PlatformCode)
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ] 1.2 Add `ExternalPlatformId` to `[revenue].[ExternalSalesRecord]`
    - Idempotent column add (INT NULL) + FK to [revenue].[ExternalPlatform]
    - _Requirements: 1.7_

  - [ ] 1.3 Seed `external_platform_import` module key for Professional + Enterprise
    - Idempotent INSERT into [portal].[PlanModulePermission] (IsIncluded=1, AccessLevel='full') for the Professional and Enterprise SubscriptionPlan ids
    - _Requirements: 9.3, 9a.1_

- [ ] 2. Entities and EF configuration
  - [ ] 2.1 Create `ExternalPlatform` entity
    - Properties + nav to Business and ExternalSalesRecords
    - _Requirements: 1.1_

  - [ ] 2.2 Add `ExternalPlatformId` + `ExternalPlatform` nav to `ExternalSalesRecord` entity
    - _Requirements: 1.7_

  - [ ] 2.3 Add `ConfigureExternalPlatform` in `PortalDbContext`
    - ToTable("ExternalPlatform","revenue"); lengths; CreatedAtUtc default GETUTCDATE(); FK; global BusinessId query filter; unique index (BusinessId, PlatformCode)
    - _Requirements: 1.1, 1.4, 1.5, 1.6_

  - [ ] 2.4 Extend `ConfigureExternalSalesRecord` to map `ExternalPlatformId` + optional FK
    - _Requirements: 1.7_

- [ ] 3. Repositories
  - [ ] 3.1 Create `ExternalPlatformRepository`
    - GetByBusinessIdAsync(includeInactive), GetByIdAndBusinessIdAsync, GetByCodeAndBusinessIdAsync, InsertAsync, UpdateAsync, SetActiveAsync
    - Table-repository standard; full table names; null-safe SqlParameters; catch (Exception ex) { throw; }
    - _Requirements: 2.1–2.9, 9.1_

  - [ ] 3.2 Extend `ExternalSalesRecordRepository.InsertAsync` for `ExternalPlatformId`
    - Add column to INSERT list/VALUES/params (null-safe)
    - _Requirements: 5.2_

  - [ ] 3.3 Extend `ExternalSalesRecordRepository.GetPagedAsync` for platform
    - Add `[ExternalPlatformId]` to SELECT + optional `@ExternalPlatformId` filter
    - _Requirements: 8.1, 8.2_

  - [ ] 3.4 Add `ExternalSalesRecordRepository.ExistsDuplicateByPlatformAsync` and `FindCrossSourceOrPlatformDuplicateAsync`
    - `ExistsDuplicateByPlatformAsync`: mirror `ExistsDuplicateAsync` but key on ExternalPlatformId (exact same-platform duplicate)
    - `FindCrossSourceOrPlatformDuplicateAsync`: same invoice+date under a different platform OR any POS source → return name for a warning
    - _Requirements: 4.7_

  - [ ] 3.5 Add `VatSubmissionPeriodRepository.GetCoveringUnsubmittedPeriodAsync(businessId, date)`
    - LEFT JOIN [vat].[VatSubmission]; PeriodStartDate <= date <= PeriodEndDate; exclude submitted; full table names
    - _Requirements: 6.1, 6.2, 6.3_

- [ ] 4. Request models and preview model extensions
  - [ ] 4.1 Create `CreateExternalPlatformRequest` / `UpdateExternalPlatformRequest`
    - _Requirements: 2.2, 2.3_

  - [ ] 4.2 Extend `SalesImportPreview` with `ExternalPlatformId`, `ExternalPlatformName`
    - _Requirements: 4.3_

  - [ ] 4.3 Extend `SalesImportRow` with `HasPrefixWarning`, `PrefixWarning`, `TargetPeriodLabel`
    - _Requirements: 4.5, 6.4, 6.5_

- [ ] 5. Services
  - [ ] 5.1 Create `ExternalPlatformService` (`IExternalPlatformService`)
    - CRUD + validation (name ≤200; code `^[A-Za-z0-9]{1,10}$` uppercased; uniqueness within business); tenant-scoped
    - _Requirements: 2.2–2.9, 9.1, 9.4_

  - [ ] 5.2 Add `SalesImportService.ParseAndPreviewForPlatformAsync`
    - Validate platform (tenant + active); parse canonical contract (required headers, tolerant helpers); 1000-row cap
    - Prefix validation per row (non-blocking warning)
    - VAT period resolution per distinct date (memoized) → TargetPeriodLabel
    - Duplicate detection via `ExistsDuplicateByPlatformAsync`
    - _Requirements: 3.1–3.6, 4.1–4.7, 6.4, 6.5_

  - [ ] 5.3 Add `SalesImportService.ConfirmImportForPlatformAsync` (or extend ConfirmImportAsync)
    - Transaction; set ExternalPlatformId; re-resolve VatSubmissionPeriodId at commit (respect submitted); audit "ExternalPlatformSalesImport"
    - Support ExcludeRowIndexes; roll back whole batch on failure
    - _Requirements: 5.1–5.6, 6.1, 6.2, 6.3_

  - [ ] 5.4 Create `ImportTemplateService` (`IImportTemplateService`)
    - `BuildCsvTemplate(platformCode)` — UTF-8, canonical header + two example rows (standard VAT + zero-VAT)
    - `BuildExcelTemplate(platformCode)` — ClosedXML; "Sales" sheet (bold header, fill #0D5EA6, white font; InvoiceNumber column text-formatted) + "Instructions" sheet (per-column reference)
    - Example rows use selected platform code or `ABC` placeholder
    - _Requirements: 3a.1–3a.8_

- [ ] 6. Controllers and access control
  - [ ] 6.1 Add `PortalModules.ExternalPlatformImport = "external_platform_import"` constant
    - _Requirements: 9.3, 9a.4_

  - [ ] 6.2 Create `ExternalPlatformController`
    - Index; AxPostCreatePlatform; AxPostUpdatePlatform; AxPostSetPlatformActive
    - `[Authorize]` + class-level `[ModuleAccess(PortalModules.ExternalPlatformImport)]`
    - _Requirements: 2.1–2.9, 9.3, 9a.2, 9a.3_

  - [ ] 6.3 Extend `SalesImportController` for platform import
    - Index: add ExternalPlatforms to ViewData (server-side check the new key to show the platform path)
    - `AxPostParseFileForPlatform` with action-level `[ModuleAccess(PortalModules.ExternalPlatformImport)]`; dispatch confirm to platform path when preview.ExternalPlatformId set
    - _Requirements: 4.1, 4.2, 5.1, 9.2, 9a.3_

  - [ ] 6.4 Add `AxGetDownloadTemplate(format, externalPlatformId?)` to `SalesImportController`
    - Resolve/validate platform code (tenant-owned when id supplied); return CSV or .xlsx via ImportTemplateService; module-gated
    - _Requirements: 3a.1, 3a.4, 3a.7_

- [ ] 7. Checkpoint — backend build
  - Build; verify 0 errors before UI work

- [ ] 8. Views
  - [ ] 8.1 `Views/ExternalPlatform/Index.cshtml` — list + create/edit modal
    - Filter/table cards per layout steering; BlockUI + Swal + fetch + antiforgery
    - _Requirements: 2.1–2.7_

  - [ ] 8.2 Extend `Views/SalesImport/Index.cshtml` — platform selector + upload path + template download
    - "Download template" split control (CSV / Excel) linking to `AxGetDownloadTemplate`, passing the selected platform id
    - _Requirements: 4.1, 4.2, 3a.1, 3a.4_

  - [ ] 8.3 Extend `Views/SalesImport/Preview.cshtml` — Platform prefix (✓/⚠) + VAT period columns
    - _Requirements: 4.5, 6.4, 6.5_

  - [ ] 8.4 Extend `Views/SalesImport/Records.cshtml` — Platform column + filter
    - _Requirements: 8.1, 8.2_

- [ ] 9. Wiring and VAT integration verification
  - [ ] 9.1 Register `ExternalPlatformRepository`, `IExternalPlatformService`, `IImportTemplateService` in Program.cs
    - _Requirements: (wiring)_

  - [ ] 9.2 Verify Output VAT includes platform-imported records
    - Confirm existing Output-VAT aggregation counts ExternalSalesRecord VAT for assigned periods (no code change expected; verify)
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 10. Documentation deliverable
  - [ ] 10.1 Write the Canonical Import Contract guideline under `.kiro/docs/`
    - Columns, types, date/decimal formats, encoding, delimiter, invoice-number format, VAT semantics, idempotency, worked example file
    - _Requirements: 10.1–10.5_

- [ ] 11. Final checkpoint
  - Build 0 errors; run unit tests
  - Manual: register a platform (e.g. GRD) → import sample file → records tagged + assigned to correct period → Output VAT reflects it
  - Test prefix-mismatch warning path; submitted-period lock path; duplicate re-import skip
  - Test: download CSV template and Excel template → headers + two example rows correct; re-import the downloaded template unchanged imports cleanly (round-trip)
  - Verify existing revenue-source imports and existing records still work

## Notes

- No parallel tables — external platform sales are `ExternalSalesRecord` rows and feed Output VAT exactly like Z-Report imports.
- A record is tagged by EITHER `RevenueSourceId` (POS) OR `ExternalPlatformId` (external system); both nullable.
- The invoice `PlatformCode` is both the platform label and the prefix-validation key (matches `InvoiceNumberGenerator`).
- VAT-period auto-assignment refuses submitted periods and is re-checked at commit to avoid a preview→confirm race.
- Fixed canonical contract for controlled exporters; tolerant parse helpers retained for generic third-party files.
- CSV only, ≤5 MB, ≤1000 rows this phase. API/scheduled pull, Excel, multi-currency, per-platform summaries are future phases.
- All AJAX: BlockUI + SweetAlert2 + fetch + antiforgery. All catch blocks: `catch (Exception ex)`.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "4.1", "4.2", "4.3"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 5, "tasks": ["7"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 7, "tasks": ["9.1", "9.2"] },
    { "id": 8, "tasks": ["10.1"] },
    { "id": 9, "tasks": ["11"] }
  ]
}
```
