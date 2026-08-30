# Design Document: External Platform Sales Import

## Overview

This feature lets a Business import line-level sales from external systems into `[revenue].ExternalSalesRecord` so they roll into the correct VAT period and Output VAT. It extends the existing Revenue Ingestion pipeline (`SalesImportService`, `SalesImportController`, `ExternalSalesRecordRepository`) rather than duplicating it.

Two things are genuinely new:
1. A first-class **`ExternalPlatform`** lookup (`[revenue].ExternalPlatform`) that identifies an external system by name + invoice `PlatformCode`, distinct from a POS `RevenueSource`.
2. A **canonical fixed-schema import** with prefix validation and **VAT-period auto-assignment on import** (closing the current gap where `SalesImportService` inserts records with `VatSubmissionPeriodId = null`).

Everything else — upload → parse → preview → confirm, duplicate detection, soft delete/restore, tenant isolation, audit logging, and Output-VAT consumption — is reused.

### Design Principles

- **One revenue pipeline.** External platform sales are `ExternalSalesRecord` rows, exactly like Z-Report imports. They contribute to Output VAT the same way. No parallel tables, no second VAT path.
- **The invoice prefix is the platform identity.** Because every platform uses `{PlatformCode}-INV-{yyyy}-{NNNN}`, the `PlatformCode` is both the human label and the validation key. We do not invent a separate source identity.
- **Contract over configuration.** For 3 Inventors' own platforms we control the export, so we publish one fixed schema instead of relying on flexible header mapping. The parser still tolerates header aliases and `dd/MM/yyyy` dates for the generic case.
- **Never touch a filed return.** Auto-assignment refuses to attach a record to a period whose `VatSubmission.IsSubmitted = 1`.

## Architecture

```
External Platform (Guardian, MyChair, …)
        │  (export service built to Canonical Import Contract)
        ▼
   sales-export.csv  (UTF-8, InvoiceNumber, InvoiceDate, NetAmount, VatAmount, TotalAmount, …)
        │  upload
        ▼
SalesImportController ──► ISalesImportService.ParseAndPreviewAsync(stream, fileName, platformId)
        │                        │
        │                        ├─ ExternalPlatformRepository (validate platform, prefix)
        │                        ├─ ExternalSalesRecordRepository (duplicate detection)
        │                        └─ VatSubmissionPeriodRepository (resolve covering period)
        │  preview cached (IMemoryCache, 30 min)
        ▼
   Preview view ──► confirm (with excluded rows)
        │
        ▼
ISalesImportService.ConfirmImportAsync(preview, excludes)
        │  transaction
        ├─ ExternalSalesRecordRepository.InsertAsync (per row, with ExternalPlatformId + VatSubmissionPeriodId)
        └─ AuditLogRepository.InsertAsync (batch entry)
        ▼
[revenue].ExternalSalesRecord ──► VAT submission Output VAT (existing consumption)
```

### Layering

Consistent with the project's Controller → Service → Repository pattern:

- **Controller**: `SalesImportController` gains a platform-aware overload path; new `ExternalPlatformController` for CRUD. Both `[Authorize]` + module gate.
- **Service**: `SalesImportService` extended to accept a platform id, run prefix validation, and resolve the VAT period. New `ExternalPlatformService` for CRUD + validation.
- **Repository**: New `ExternalPlatformRepository` (table repository). Extend `ExternalSalesRecordRepository` (new column) and reuse `VatSubmissionPeriodRepository`.

## Components and Interfaces

### 1. Database Changes

#### 1a. New table `[revenue].ExternalPlatform`

```sql
CREATE TABLE [revenue].[ExternalPlatform] (
    [Id]           INT IDENTITY(1,1) NOT NULL,
    [BusinessId]   INT               NOT NULL,
    [Name]         NVARCHAR(200)     NOT NULL,
    [PlatformCode] NVARCHAR(10)      NOT NULL,
    [Description]  NVARCHAR(500)     NULL,
    [IsActive]     BIT               NOT NULL CONSTRAINT [DF_ExternalPlatform_IsActive] DEFAULT (1),
    [CreatedAtUtc] DATETIME          NOT NULL CONSTRAINT [DF_ExternalPlatform_CreatedAtUtc] DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_ExternalPlatform] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ExternalPlatform_Business] FOREIGN KEY ([BusinessId]) REFERENCES [dbo].[Business]([Id]),
    CONSTRAINT [UQ_ExternalPlatform_Business_Code] UNIQUE ([BusinessId], [PlatformCode])
);
```

Naming complies with steering: `Id` PK, `<Table>Id` FKs, `Is`-prefixed bit, mandatory `CreatedAtUtc` with `GETUTCDATE()` default, `[revenue]` schema for the module.

#### 1b. Alter `[revenue].ExternalSalesRecord`

```sql
ALTER TABLE [revenue].[ExternalSalesRecord]
    ADD [ExternalPlatformId] INT NULL;

ALTER TABLE [revenue].[ExternalSalesRecord]
    ADD CONSTRAINT [FK_ExternalSalesRecord_ExternalPlatform]
        FOREIGN KEY ([ExternalPlatformId]) REFERENCES [revenue].[ExternalPlatform]([Id]);
```

Idempotent with `IF NOT EXISTS` column/constraint checks per migration conventions. A record is tagged by **either** `RevenueSourceId` (POS) **or** `ExternalPlatformId` (external system); both nullable.

### 2. Entities

#### `ExternalPlatform` (new)

```csharp
namespace Portal.Infrastructure.Entities;

/// <summary>
/// An external system that produces sales for a Business (e.g., another 3 Inventors platform,
/// an online store). Identified by its invoice PlatformCode. Schema: [revenue].ExternalPlatform
/// </summary>
public class ExternalPlatform
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Name { get; set; } = null!;
    public string PlatformCode { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Business Business { get; set; } = null!;
    public ICollection<ExternalSalesRecord> ExternalSalesRecords { get; set; } = new List<ExternalSalesRecord>();
}
```

#### `ExternalSalesRecord` (extended)

Add:
```csharp
public int? ExternalPlatformId { get; set; }
public ExternalPlatform? ExternalPlatform { get; set; }
```

#### EF configuration in `PortalDbContext`

`ConfigureExternalPlatform`:
- `ToTable("ExternalPlatform", "revenue")`, key `Id`.
- `Name` max 200 required; `PlatformCode` max 10 required; `Description` max 500.
- `CreatedAtUtc` `IsRequired().HasDefaultValueSql("GETUTCDATE()")`.
- FK to `Business`; global query filter on `BusinessId` (mirroring `ConfigureExternalSalesRecord` at ~4431).
- Unique index `(BusinessId, PlatformCode)`.

Extend `ConfigureExternalSalesRecord`: map `ExternalPlatformId` and its optional FK relationship.

### 3. Repositories

#### `ExternalPlatformRepository` (new — table repository)

Follows the JDS table-repository standard (entity-named, `Repository` suffix, `SqlParameter` + null-safe, `try/catch (Exception ex) { throw; }`, full table names in SQL, transaction-aware connection handling like `ExternalSalesRecordRepository`).

```csharp
public class ExternalPlatformRepository : GenericStoredProcedureRepository<ExternalPlatform>
{
    public ExternalPlatformRepository(PortalDbContext context) : base(context) { }

    Task<List<ExternalPlatform>> GetByBusinessIdAsync(int businessId, bool includeInactive);
    Task<ExternalPlatform?> GetByIdAndBusinessIdAsync(int id, int businessId);
    Task<ExternalPlatform?> GetByCodeAndBusinessIdAsync(int businessId, string platformCode);
    Task<int> InsertAsync(ExternalPlatform entity);
    Task UpdateAsync(ExternalPlatform entity);      // Name, PlatformCode, Description
    Task SetActiveAsync(int id, int businessId, bool isActive);
}
```

`GetByCodeAndBusinessIdAsync` supports the uniqueness check on create/edit.

#### `ExternalSalesRecordRepository` (extend)

- Add `[ExternalPlatformId]` to the `InsertAsync` column list, VALUES, and SqlParameters (null-safe).
- Add `[ExternalPlatformId]` to the `GetPagedAsync` SELECT and an optional `@ExternalPlatformId` filter parameter.
- Duplicate detection: keep the existing `ExistsDuplicateAsync`, but the import will pass the platform id so duplicates are scoped correctly. Add an overload/param so `ExistsDuplicateAsync` can key on `ExternalPlatformId` (the current signature keys on `RevenueSourceId`). To avoid churn, add `ExistsDuplicateByPlatformAsync(businessId, externalPlatformId, invoiceNumber, transactionDate)` mirroring the existing method.

#### `VatSubmissionPeriodRepository` (reuse + add lookup)

Add a method to resolve the covering, non-submitted period for a date:

```csharp
/// Returns the VatSubmissionPeriod whose [PeriodStartDate, PeriodEndDate] contains the date,
/// for the business, only when that period has no submitted VatSubmission. Null otherwise.
Task<VatSubmissionPeriod?> GetCoveringUnsubmittedPeriodAsync(int businessId, DateOnly date);
```

SQL joins `[vat].VatSubmissionPeriod` against `[vat].VatSubmission` (LEFT JOIN) and filters `PeriodStartDate <= @Date <= PeriodEndDate` AND `(submission is null OR submission.IsSubmitted = 0)`. Full table names, no aliases, per steering.

### 4. Services

#### `ExternalPlatformService` (new)

```csharp
public interface IExternalPlatformService
{
    Task<List<ExternalPlatform>> GetAllAsync(bool includeInactive);
    Task<List<ExternalPlatform>> GetActiveAsync();
    Task<ExternalPlatform?> GetByIdAsync(int id);
    Task<ServiceResult<int>> CreateAsync(string name, string platformCode, string? description);
    Task<ServiceResult> UpdateAsync(int id, string name, string platformCode, string? description);
    Task<ServiceResult> SetActiveAsync(int id, bool isActive);
}
```

Validation: name non-empty (≤200), `PlatformCode` matches `^[A-Za-z0-9]{1,10}$` (uppercased before persist), uniqueness of code within business. All reads/writes scoped to `ICurrentTenantService.CurrentBusinessId`.

#### `SalesImportService` (extend)

Add an overload that accepts an external platform (keeping the existing revenue-source path intact for backward compatibility):

```csharp
Task<ServiceResult<SalesImportPreview>> ParseAndPreviewForPlatformAsync(
    Stream fileStream, string fileName, int externalPlatformId);
```

Behavior differences from the existing `ParseAndPreviewAsync`:
- Validate the platform belongs to the tenant and is active (via `ExternalPlatformRepository`).
- Parse against the **Canonical Import Contract** (fixed required headers) using the existing tolerant `FindCol`/`TryParseDate`/`TryParseDec` helpers.
- Raise the row cap to 1000 (contract-controlled files are larger and trusted).
- **Prefix validation**: for each row, if `InvoiceNumber` does not start with `{PlatformCode}-INV-` (case-insensitive), set a new `SalesImportRow.HasPrefixWarning` + message. Non-blocking.
- **VAT period resolution (preview)**: call `GetCoveringUnsubmittedPeriodAsync` per distinct `TransactionDate` (memoized in a local dictionary to avoid N queries), set a `SalesImportRow.TargetPeriodLabel` ("Q3 2026" / "Unassigned" / "Locked — period submitted").
- Duplicate detection uses `ExistsDuplicateByPlatformAsync`.

`ConfirmImportAsync` extended (or a `ConfirmImportForPlatformAsync`) to:
- Set `ExternalPlatformId` on each inserted record.
- Set `VatSubmissionPeriodId` by re-resolving the covering unsubmitted period at commit time (re-checked, not trusting cached preview, so a period submitted between preview and confirm is respected).
- Audit log `Action = "ExternalPlatformSalesImport"`, `TableName = "revenue.ExternalSalesRecord"`, `NewValues` includes platform name, file, count, total.

To keep the preview model serializable in `IMemoryCache`, extend `SalesImportPreview` with `ExternalPlatformId`, `ExternalPlatformName`, and `SalesImportRow` with `HasPrefixWarning`, `PrefixWarning`, `TargetPeriodLabel`.

### 5. Controllers

#### `ExternalPlatformController` (new)

CRUD UI + AJAX endpoints, `[Authorize]` + module gate. Methods follow the `AxPost`/`AxGet` naming rule:
- `Index()` — list view.
- `AxPostCreatePlatform(CreateExternalPlatformRequest)` → `Json({success,message})`.
- `AxPostUpdatePlatform(UpdateExternalPlatformRequest)`.
- `AxPostSetPlatformActive(int id, bool isActive)`.

#### `SalesImportController` (extend)

Add platform-based endpoints alongside the existing source-based ones:
- `Index` gains an `ExternalPlatforms` view-data list (in addition to `RevenueSources`), and the UI lets the user choose "Import from external platform".
- `AxPostParseFileForPlatform(IFormFile file, int externalPlatformId)` → caches preview, returns cache key.
- Reuse `Preview` and `AxPostConfirmImport` (the preview object already carries the platform id; confirm dispatches to the platform path when `ExternalPlatformId` is set).

### 6. Request Models

```csharp
public class CreateExternalPlatformRequest
{
    public string Name { get; set; } = null!;
    public string PlatformCode { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateExternalPlatformRequest : CreateExternalPlatformRequest
{
    public int Id { get; set; }
}
```

### 7. Views (MyChair Design System)

- `Views/ExternalPlatform/Index.cshtml` — filter card + table card per layout steering (`.glass.card-pad`, `margin-bottom:22px` between filter and table). Create/Edit via modal. AJAX uses `BlockUI.show/hide` + `Swal.fire`, vanilla `fetch`, antiforgery token — per UI feedback + project-overview steering.
- Extend `Views/SalesImport/Index.cshtml` — a platform selector and file upload path. Preview view (`Preview.cshtml`) extended to show two new columns: **Platform prefix** (✓ / ⚠ mismatch) and **VAT period** (target label / "Unassigned" / "Locked"). Duplicate and invalid rows already render.
- Extend `Views/SalesImport/Records.cshtml` — add a Platform column and a Platform filter.

### 8. Program.cs Registrations

Register `ExternalPlatformRepository`, `IExternalPlatformService → ExternalPlatformService`. `ExternalSalesRecordRepository`, `ISalesImportService`, `VatSubmissionPeriodRepository`, `AuditLogRepository`, `IMemoryCache` already registered.

## Data Models

### Canonical Import Contract (CSV)

UTF-8, comma-delimited (semicolon tolerated), first row = header.

| Column | Required | Type | Notes |
|---|---|---|---|
| `InvoiceNumber` | Yes | string | `{PlatformCode}-INV-{yyyy}-{NNNN}` |
| `InvoiceDate` | Yes | date | Canonical `yyyy-MM-dd` |
| `NetAmount` | Yes | decimal | `.` separator, ≥ 0 |
| `VatAmount` | Yes | decimal | `.` separator, ≥ 0 |
| `TotalAmount` | Yes | decimal | `= NetAmount + VatAmount`; recomputed if missing |
| `VatRate` | No | decimal | e.g. `19` or `0` |
| `CustomerName` | No | string | free text (not linked to Customer table this phase) |
| `Description` | No | string | ≤ 500 |
| `PaymentMethod` | No | string | ≤ 50 |
| `Currency` | No | string | ISO 4217; informational this phase |

`CustomerName` is captured into `Description` context if needed but is **not** resolved to a `CustomerId` (external customers aren't Portal customers). `ExternalSalesRecord.CustomerId` stays null for platform imports.

## Error Handling

- Repositories: `try/catch (Exception ex) { throw; }` (golden rule — variable named).
- Services return `ServiceResult` / `ServiceResult<T>` with user-safe messages; controllers surface them via `Json({success,message})`.
- Import commit wrapped in a transaction; any failure rolls back the whole batch and reports "no records were created."
- Parse failures (missing required header, oversized file, too many rows, wrong extension) fail the whole file with a specific message before preview.
- Row-level problems (bad date, negative amount) mark the row invalid and exclude it; the rest still import.
- Prefix mismatch and submitted-period are **warnings**, not errors.

## Concurrency / Failure-Mode Analysis

- **Period submitted between preview and confirm**: confirm re-resolves the covering unsubmitted period, so a race cannot attach a record to a just-filed return.
- **Duplicate re-import**: keyed on invoice number + date (+ platform); re-importing an overlapping export skips duplicates. The published contract requires idempotent exports.
- **Large files**: 5 MB / 1000-row cap bounds memory and transaction size. If volumes grow, a future phase can batch inserts or move to the durable `[import].ImportSession` table instead of `IMemoryCache`.
- **Cache expiry**: preview cached 30 min; expiry returns a friendly "upload again" message (existing behavior).
- **Tenant isolation**: every query filters `BusinessId`; the selected platform is re-validated against the tenant before parse and before persist.

## Testing Strategy

- **Unit (service)**: contract parsing (all required columns, missing column rejection, alias tolerance); `TotalAmount` recomputation; prefix validation (match/mismatch); duplicate detection; VAT-period resolution (covering / none / submitted-locked); confirm re-resolution respecting a period submitted after preview.
- **Unit (repository)**: `ExternalPlatformRepository` uniqueness/tenant scoping; `ExternalSalesRecordRepository` new column round-trip; `GetCoveringUnsubmittedPeriodAsync` boundary dates (start = end inclusivity).
- **Integration**: end-to-end import of a sample contract file → records created, tagged to platform, assigned to correct period; Output VAT for that period reflects the new VAT amounts.
- **Backward compatibility**: existing revenue-source imports still work unchanged; existing `ExternalSalesRecord` rows (null `ExternalPlatformId`) read/paginate correctly.
- Follow the project's existing property-based VAT tests style for Output-VAT contribution.

## Migration & Rollout

1. Migration N: create `[revenue].ExternalPlatform` (idempotent).
2. Migration N+1: add `ExternalPlatformId` to `[revenue].ExternalSalesRecord` (idempotent).
3. Deploy code (entities, repos, services, controllers, views).
4. 3 Inventors registers its platforms (e.g., Guardian `GRD`, MyChair `MYC`) via the new UI.
5. Publish the export guideline to each platform's team.
6. First import validated against a sample file per platform.

## Design Decisions & Rationale

- **New `ExternalPlatform` lookup vs reusing `RevenueSource`** (user chose new): `RevenueSource` semantically means a POS device/register for a hospitality business. Overloading it for "another billing platform" muddies both concepts and can't validate an invoice prefix. A dedicated lookup gives a clean, long-term, reusable concept and enables prefix validation.
- **Extend `ExternalSalesRecord` vs new table** (reuse): the table is already transaction-level with `NetAmount/VatAmount/TotalAmount/InvoiceNumber/VatSubmissionPeriodId` and already feeds Output VAT. A new table would fork the VAT pipeline for no benefit.
- **Line-level vs summary** (user chose line-level): preserves each external invoice number for auditability; summaries can be derived later.
- **Fixed contract vs flexible mapping**: since 3 Inventors controls the exporters, a fixed schema removes per-file mapping friction and makes validation deterministic. Tolerant parsing helpers remain for the generic third-party case.
- **Auto-assign VAT period on import**: closes the existing gap (records were inserted unassigned) so imported sales actually appear in the return without a manual step, while strictly refusing submitted periods.
