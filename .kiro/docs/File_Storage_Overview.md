# File Storage Overview

_How and where the Portal persists uploaded files, per upload facility, with the folder structure and a recommendation on logo storage._

> **One big fact:** No uploaded file bytes are stored in the database, and there is **no cloud/blob/S3 storage**. Every upload lands on the **local filesystem** of the web server; SQL stores only **metadata + a path/URL string**. There are **two different disk roots** (see below).

---

## Storage roots

### Root A — `FileStorage:BasePath` (private, outside the website)
A configured folder that is **not** directly web-accessible. Files here are served only through authenticated streaming controller actions.

| Environment | Config file | Value |
|-------------|-------------|-------|
| Development | `Portal.Web/appsettings.json` | `C:/BusinessPortal/Uploads` |
| Production  | `Portal.Web/appsettings.Production.json` | `C:/BusinessPortalUploads/Uploads` |

- Read centrally by `LocalFileStorageService` (throws on startup if the key is missing).
- `SignatureService` reads the same key directly (with a hardcoded fallback of `C:/BusinessPortal/Uploads`).

### Root B — `wwwroot/uploads/logos` (public, inside the website)
Inside the web root, served as **public static files**. Used **only** for business logos.

---

## Upload facilities

### 1. Document attachments — the most common case
Invoice PDFs on purchases, plus invoices, credit notes, quotations, payments, suppliers, customers, Z-Reports.

- **Entry point:** `AttachmentController.AxPostUpload(IFormFile, entityType, entityId)`
- **Service:** `DocumentAttachmentService.UploadAsync` → `LocalFileStorageService.UploadAsync`
- **Limits:** 5 MB; 5 files/entity (1 for Z-Reports); extension + content-type + magic-byte validation.
- **Storage (disk, private root):**
  ```
  {FileStorage:BasePath}/{businessId}/{entityType}/{entityId}/{guid:N}_{originalFileName}
  ```
  Example: `C:/BusinessPortal/Uploads/42/Purchase/1187/a3f9…_invoice.pdf`
- **DB metadata:** `[document].DocumentAttachment` — `BusinessId`, `EntityType`, `EntityId`, `FileName`, `OriginalFileName`, `ContentType`, `StoragePath` (path string, **no bytes**), `FileSizeBytes`, `UploadedByUserId`, `IsDeleted`, `CreatedAtUtc`.
- **Tenant isolation:** Yes — `businessId` is the first path segment **and** a row column.
- **Retrieval:** `AttachmentController.AxGetDownload(id)` streams the file (`File(stream, contentType, originalFileName)`).
- **Delete:** **Soft delete** — the DB row is flagged `IsDeleted`, but the file **remains on disk** (orphaned files accumulate).

### 2. Compliance attachments (PDF filings)
- **Entry point:** `ComplianceController.AxPostUploadAttachment(id, IFormFile)` → `ComplianceService.UploadAttachmentAsync`
- **Limits:** PDF only; 5 MB; 3 attachments/application.
- **Storage (disk, private root):**
  ```
  {FileStorage:BasePath}/{businessId}/compliance/{applicationId}/{guid:N}_{fileName}
  ```
- **DB metadata:** `[compliance].ApplicationAttachment` (path in `FilePath`, no bytes).
- **Tenant isolation:** Yes (path + row).
- **Retrieval:** `ComplianceService.DownloadAttachmentAsync` streams the file.
- **Delete:** Physically removes the file from disk.

### 3. Digital signatures
- **Entry point:** `SignatureController.AxPostUpload(IFormFile, label, position)` → `SignatureService.UploadAsync`
- **Limits:** PNG/SVG; 2 MB; 10/business; PNG magic-byte check; filename sanitized.
- **Storage (disk, private root — written directly, bypassing `LocalFileStorageService`):**
  ```
  {FileStorage:BasePath}/signatures/{businessId}/{guid:N}_{sanitizedFileName}
  ```
  ⚠️ **Path layout is inconsistent** with #1/#2: here `signatures` comes **before** `businessId`, because this service writes directly instead of using the shared storage service.
- **DB metadata:** `[portal].Signature` (path in `FilePath`, no bytes).
- **Tenant isolation:** Yes (path + row).
- **Retrieval:** `SignatureController.GetImage(id)` streams the file; also embedded in receipts.

### 4. Business logos
- **Entry points (three):** `LogoController.Upload`, `MyBusinessController.UploadLogo`, and the Setup Wizard — all funnel into `LogoService.UploadAsync`.
- **Limits:** PNG/JPG/SVG/WebP; 2 MB; 20/business.
- **Storage (disk, private root — tenant-isolated):**
  ```
  {FileStorage:BasePath}/{businessId}/logos/{guid}{ext}
  ```
- **DB metadata:** `[portal].BusinessLogo` — `BusinessId`, `DisplayName`, `FileName`, `ContentType`, `FileSizeBytes`, `PublicUrl` (= `/logo/{businessId}/{filename}`), `IsPrimary`, `CreatedAtUtc`.
- **Tenant isolation:** Yes — `businessId` is a path segment **and** a row column (matching attachments/compliance/signatures).
- **Retrieval:** Served through the **public-but-unguessable streaming route** `GET /logo/{businessId}/{file}` (`LogoImageController.Image`, `[AllowAnonymous]`). This is intentionally public (unauthenticated) so that customer-facing shared invoice/proposal snapshots render the logo with a plain `<img src>`; it is safe because filenames are random GUIDs (same threat model as before, now redeploy-safe and tenant-isolated). PDF services (invoice, proposal, statement, payslip, P&L) embed the logo as base64 by reading `{FileStorage:BasePath}/{businessId}/logos/{FileName}` directly.

> **History:** Logos originally lived in `wwwroot/uploads/logos/{guid}{ext}` (public static files, redeploy-fragile, not path-isolated). Migrated to the private root via migration **212** + a one-time file move (see runbook below).

### 5. CSV / XLSX imports — NOT persisted
Purchase CSV import, Purchase import engine, Z-Report import, Sales import, **Prospect import**, and parser-template test all read the upload stream, parse it **in memory**, and discard the raw file. Only the parsed row data is persisted downstream.

### 6. Generated PDFs — NOT persisted
Proposals, invoices, credit notes, receipts, statements, payslips, and P&L PDFs are generated on demand (PuppeteerSharp) and returned as a `byte[]` download or email attachment. They are **never** written to `wwwroot`, `BasePath`, or the DB.

---

## Summary table

| Feature | Accepts | Storage | Location / path convention | Tenant-scoped in path? | Retrieval | Delete |
|---------|---------|---------|-----------------------------|------------------------|-----------|--------|
| Document attachments | IFormFile | Disk (private) | `{BasePath}/{businessId}/{entityType}/{entityId}/{guid}_{name}` | Yes | Streamed | Soft (file stays) |
| Compliance PDFs | IFormFile | Disk (private) | `{BasePath}/{businessId}/compliance/{appId}/{guid}_{name}` | Yes | Streamed | Hard (file removed) |
| Signatures | IFormFile | Disk (private) | `{BasePath}/signatures/{businessId}/{guid}_{name}` | Yes | Streamed | — |
| Business logos | IFormFile | Disk (private) | `{BasePath}/{businessId}/logos/{guid}{ext}` | Yes | Public route `/logo/{businessId}/{file}` (unguessable); base64 in PDFs | Hard (file removed) |
| CSV/XLSX imports | IFormFile | **Not stored** | parsed in memory | — | — | — |
| Generated PDFs | n/a (output) | **Not stored** | streamed / emailed | — | — | — |

---

## Operational risks to be aware of

1. **Backups are one root now.** A database backup alone does **not** back up files. **All** uploaded files (attachments, compliance, signatures, and — since migration 212 — logos) live under the single `FileStorage:BasePath` root, which must be backed up separately. (Nothing business-uploaded remains under `wwwroot`.)
2. **Server migration.** The `FileStorage:BasePath` folder must move with the app when changing servers, or files 404.
3. **Orphaned attachment files.** Document-attachment deletes are soft — files accumulate on disk over time. A cleanup job could reconcile disk against non-deleted rows.
4. **Path-layout inconsistency.** Signatures use `signatures/{businessId}/…` while the shared service and logos use `{businessId}/…`. Cosmetic, but worth normalising if signatures are ever migrated onto the shared service.

---

## Logo migration runbook (migration 212)

Logos were moved from `wwwroot/uploads/logos` to `{FileStorage:BasePath}/{businessId}/logos`. This
requires **two steps in one deployment** — a physical file move **and** the SQL URL rewrite — because
the bytes live on disk while the URL lives in the DB.

### Recommended order
1. **Deploy the new build** (logos now write to `BasePath` and serve via `/logo/{businessId}/{file}`).
2. **Move the existing files** from the web root to the private root (PowerShell below).
3. **Run `212_MigrateLogosToBasePath.sql`** to rewrite `BusinessLogo.PublicUrl`.

> Doing the file move + SQL together avoids a window where a rewritten URL points at a file that
> hasn't been moved yet. If a logo 404s briefly, re-run the file move; both steps are idempotent.

### PowerShell script — move files based on the DB rows
The script lives at **`Portal.Database/Migrations/212_MigrateLogosToBasePath.ps1`** (next to the SQL
half). It reads every logo row from the DB and copies each file from the web root to the private root.
It takes `-WebRoot`, `-BasePath`, `-ConnectionString`, and an optional `-WhatIf` dry-run.

Requires the SqlServer module once: `Install-Module SqlServer -Scope CurrentUser`.

```powershell
# 1) Preview (dry run) — reports what WOULD move, copies nothing
.\212_MigrateLogosToBasePath.ps1 `
    -WebRoot "C:\inetpub\Portal\wwwroot" `
    -BasePath "C:/BusinessPortalUploads/Uploads" `
    -ConnectionString "Server=.;Database=Portal;Integrated Security=true;TrustServerCertificate=true" `
    -WhatIf

# 2) Real run
.\212_MigrateLogosToBasePath.ps1 `
    -WebRoot "C:\inetpub\Portal\wwwroot" `
    -BasePath "C:/BusinessPortalUploads/Uploads" `
    -ConnectionString "Server=.;Database=Portal;Integrated Security=true;TrustServerCertificate=true"
```

The script copies (not moves) so re-runs are safe and skips files already at the destination.
After verifying logos render (management screens, a generated invoice PDF, and a shared proposal
snapshot), the old `wwwroot/uploads/logos` folder can be deleted.

### PowerShell helper — running the script

**1. Execution policy.** Windows blocks unsigned `.ps1` files by default (`PSSecurityException: running scripts is disabled`). Run via a one-off bypass — it changes nothing permanently and is scoped to that single invocation:

```powershell
powershell -ExecutionPolicy Bypass -File .\212_MigrateLogosToBasePath.ps1 <params>
```

Alternatives if you prefer: `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser` (persistent, per-user) or `Set-ExecutionPolicy Bypass -Scope Process` (this window only).

**2. SqlServer module.** The script uses `Invoke-Sqlcmd`. If you get "term not recognized", install it once:

```powershell
Install-Module SqlServer -Scope CurrentUser
```

**3. Always dry-run first.** Run with `-WhatIf` — it connects, resolves each source/destination, and reports what *would* copy without touching any file. Confirm "Missing source : 0" and the destination paths look right, then re-run without `-WhatIf`.

**4. Concrete parameter values by environment.**

| Parameter | Development | Production |
|-----------|-------------|------------|
| `-WebRoot` | `C:\Users\user\Documents\GitHub\Portal\Portal.Web\wwwroot` | the deployed wwwroot, e.g. `C:\inetpub\Portal\wwwroot` |
| `-BasePath` | `C:/BusinessPortal/Uploads` | `C:/BusinessPortalUploads/Uploads` |
| `-ConnectionString` | `PortalDb` from `appsettings.json` | the production `PortalDb` connection string |

**Dev example (full):**

```powershell
powershell -ExecutionPolicy Bypass -File .\212_MigrateLogosToBasePath.ps1 `
    -WebRoot "C:\Users\user\Documents\GitHub\Portal\Portal.Web\wwwroot" `
    -BasePath "C:/BusinessPortal/Uploads" `
    -ConnectionString "Server=127.0.0.1;Database=Portal;User ID=sa;Password=***;TrustServerCertificate=True;MultipleActiveResultSets=true" `
    -WhatIf
```

**5. Reading the summary.** `Would copy` / `Copied` = files actioned; `Already present` = destination already had it (safe skip on re-run); `Missing source` = a DB row whose file wasn't found in `wwwroot` (investigate before deleting the old folder).

> **Prod deployment order:** deploy build → dry-run the script → real run → run `212_MigrateLogosToBasePath.sql` → restart app → verify logos (screen + invoice PDF + shared snapshot) → delete old `wwwroot/uploads/logos`.

### What the code change covered
- `LogoService` writes to `{BasePath}/{businessId}/logos/{guid}{ext}` and stores `PublicUrl = /logo/{businessId}/{file}`.
- `LogoImageController` (`[AllowAnonymous]`, route `/logo/{businessId}/{file}`) streams the file from `BasePath` with path-traversal guards.
- PDF services (invoice, proposal, P&L, payslip) embed the logo as base64 from `BasePath` and match `/logo/…` src attributes.
- The P&L PDF view previously had a hardcoded, non-existent `/uploads/logos/logo.png` — now uses the real primary logo via `PnlPdfModel.LogoUrl`.

---

_Last reviewed: 2026-02-04. Update this document when a new upload facility is added or when storage moves (e.g. logos to BasePath, or a move to blob/S3)._
