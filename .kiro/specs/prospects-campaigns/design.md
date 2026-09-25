# Design — Prospects & Campaigns

## Overview

A new sub-area under Sales/Opportunities that replaces the prospecting spreadsheet. It adds
three new `[sales]` tables (Campaign, Prospect, ProspectActivity) and a Convert-to-Lead action
that **reuses the existing** contact-creation and lead-creation services, keeping a
prospect→lead back-reference for scoring-performance measurement.

Design is grounded in the existing Sales module (verified): raw-SQL repositories over
`GenericStoredProcedureRepository`, tenant scoping via `ICurrentTenantService`, EF `Configure*`
methods + query filters in `PortalDbContext`, `AxGet`/`AxPost` + `Json(new{success,message})`
controller conventions, `[ModuleAccess(PortalModules.Sales)]` gating, and the "Opportunities"
sidebar section.

### Key reuse decisions (do not duplicate)

| Need | Reuse (existing) |
|------|------------------|
| Create contact on convert | `IContactService.CreateContactAsync(CreateContactRequest)` — incl. email/phone dedup |
| Create lead on convert | `ILeadRequestService.CreateLeadRequestAsync(CreateLeadRequestDto)` |
| Lead source = cold call | `LeadSourceType` Id **4 = Cold Call** (already seeded) — no new source |
| Campaign product | `[sales].[Product]` (SalesProduct) — same entity `LeadRequest.ProductId` references |
| Module gate | `[ModuleAccess(PortalModules.Sales)]` |
| Activity write precedent | `ActivityFeedService` pattern (but prospect activities need their own table — the feed is lead-keyed) |
| Excel parsing | `Services/Import/Parsing/ExcelParser` + `ColumnMapper` (ClosedXML) |
| Cross-entity convert precedent | `ContactService.ConvertToCustomerAsync` returns the new id |

### Deliberate simplifications vs. the original KIRO doc

- **No `ProspectCampaignEntry` join table.** A prospect belongs to one campaign (V1). The
  four-dimension score lives directly on the Prospect. Re-targeting the same org in a future
  campaign is a later seam, not a V1 need. (This is the biggest simplification and matches
  "simple enough to replace Excel.")
- **Assistant backlogged.** The weekly-objective panel is static in V1.

## Data Model (new — `[sales]` schema)

### `[sales].[ProspectCampaign]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT NOT NULL | tenant scope |
| Name | NVARCHAR(200) NOT NULL | |
| SalesProductId | INT NULL | FK → `[sales].[Product].Id` |
| Description | NVARCHAR(1000) NULL | objective |
| Market | NVARCHAR(200) NULL | geography |
| StartDate | DATE NULL | |
| EndDate | DATE NULL | optional |
| WeeklyCallTarget | INT NOT NULL DEFAULT 0 | |
| OwnerUserId | NVARCHAR(450) NULL | Identity user |
| Status | TINYINT NOT NULL DEFAULT 1 | 1 Draft, 2 Active, 3 Closed |
| Notes | NVARCHAR(MAX) NULL | |
| CreatedAtUtc | DATETIME NOT NULL DEFAULT (GETUTCDATE()) | |

> Status is a small fixed enum modelled as TINYINT (no lookup table — mirrors how the codebase
> treats small fixed sets; documented in code). Alternatively a `ProspectCampaignStatusType`
> lookup if the team prefers consistency with `LeadStatusType` — **open decision (design), pick
> at task time.**

### `[sales].[Prospect]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT NOT NULL | tenant scope |
| ProspectCampaignId | INT NOT NULL | FK → ProspectCampaign |
| Name | NVARCHAR(200) NOT NULL | organisation/prospect |
| Segment | NVARCHAR(100) NULL | |
| Location | NVARCHAR(150) NULL | |
| BusinessType | NVARCHAR(150) NULL | |
| PublicContactRole | NVARCHAR(200) NULL | |
| Phone | NVARCHAR(60) NULL | research field, NOT a contact |
| Email | NVARCHAR(320) NULL | research field, NOT a contact |
| Website | NVARCHAR(300) NULL | |
| PublicEvidence | NVARCHAR(MAX) NULL | research notes |
| WhyFit | NVARCHAR(MAX) NULL | fit rationale |
| ResearchSourceUrl | NVARCHAR(500) NULL | |
| RecommendedFirstContact | NVARCHAR(300) NULL | |
| IcpFitScore | TINYINT NOT NULL DEFAULT 0 | 0–5 |
| PainProbabilityScore | TINYINT NOT NULL DEFAULT 0 | 0–5 |
| AccessibilityScore | TINYINT NOT NULL DEFAULT 0 | 0–5 |
| LearningValueScore | TINYINT NOT NULL DEFAULT 0 | 0–5 |
| Status | TINYINT NOT NULL DEFAULT 1 | 1 Research, 2 Ready, 3 Contacting, 4 Engaged, 5 Converted, 6 Disqualified |
| AssignedToUserId | NVARCHAR(450) NULL | |
| NextAction | NVARCHAR(300) NULL | |
| NextActionDate | DATE NULL | |
| ConvertedLeadRequestId | INT NULL | FK → `[sales].[LeadRequest].Id`; the measurement link |
| ConvertedAtUtc | DATETIME NULL | |
| ImportRowId | NVARCHAR(40) NULL | the workbook `ID` (e.g. A01) for idempotent re-import; NOT the PK |
| CreatedAtUtc | DATETIME NOT NULL DEFAULT (GETUTCDATE()) | |

- **Total (0–20) and Priority are computed, not stored** — derived in the service/DTO from the
  four score columns (`Total = sum`; `Priority = A (16–20) / B (12–15) / Hold (<12)`). Keeps
  one source of truth and satisfies Req 3.2/3.3.
- `ConvertedLeadRequestId` is the measurement anchor (Req 9.1).
- `ImportRowId` + `(BusinessId, ProspectCampaignId, Name)` support idempotent re-import (Req 6.7).

### `[sales].[ProspectActivity]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT NOT NULL | tenant scope |
| ProspectId | INT NOT NULL | FK → Prospect |
| ActivityType | TINYINT NOT NULL | 1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note, 7 Social, 8 Other |
| OccurredAtUtc | DATETIME NOT NULL DEFAULT (GETUTCDATE()) | |
| PerformedByUserId | NVARCHAR(450) NULL | |
| Outcome | NVARCHAR(200) NULL | |
| Notes | NVARCHAR(MAX) NULL | |
| NextAction | NVARCHAR(300) NULL | |
| NextActionDate | DATE NULL | |
| CreatedAtUtc | DATETIME NOT NULL DEFAULT (GETUTCDATE()) | |

> A separate table is required because the existing `[sales].[ActivityFeed]` is **lead-keyed**
> (`LeadRequestId` NOT NULL) and cannot represent a pre-conversion prospect. Activities survive
> conversion (Req 4.5) — they are never deleted on convert.

### EF & tenancy

- Add `ConfigureProspectCampaign`, `ConfigureProspect`, `ConfigureProspectActivity` (mirroring
  `ConfigureLeadRequest`/`ConfigureSalesContact`): `ToTable(..., "sales")`, keys, max-lengths,
  `CreatedAtUtc` default SQL, named FKs `OnDelete(ClientSetNull)`, named indexes
  (`IX_Prospect_Campaign`, `IX_Prospect_Business_Status`, `IX_ProspectActivity_Prospect`).
- Add the three entities to the tenant **query-filter** block (`BusinessId == CurrentBusinessId`),
  consistent with `SalesContact`/`LeadRequest`. (Prospect/Campaign are user data — filter them;
  ProspectActivity can filter too or scope in SQL like ActivityFeed — filter for safety.)
- DbSets on `PortalDbContext`: `ProspectCampaigns`, `Prospects`, `ProspectActivities`.

## Components

### Repositories (raw SQL, over `GenericStoredProcedureRepository<T>`)

- `ProspectCampaignRepository` — Insert, Update, GetById, GetAllByBusiness, funnel counts
  (a grouped COUNT by prospect Status for one campaign).
- `ProspectRepository` — Insert, Update, GetById, GetPagedByCampaign (with Priority/Status/
  Segment/next-action filters), UpdateStatus, SetConverted(leadId), and a
  `FindForImportAsync(campaignId, name, website/email)` for duplicate detection. Follow the
  `LeadRequestRepository` raw-SQL + `SCOPE_IDENTITY()` pattern; tenant via `@BusinessId` in WHERE.
- `ProspectActivityRepository` — Insert, GetByProspect (timeline desc), GetLastActivity (for the
  list's "Last Activity" column, batched by prospect ids).

### Services

- `IProspectCampaignService` / `ProspectCampaignService` — CRUD + `GetFunnelAsync(campaignId)`
  + `GetWeeklyObjectiveAsync(campaignId)` returning a static DTO (target, A-ready count,
  follow-ups-due count) — **computed by simple queries, no assistant**; this is the seam the
  future assistant will later enrich.
- `IProspectService` / `ProspectService` — CRUD, list/detail DTO mapping (computes Total +
  Priority), status changes, `ConvertToLeadAsync(prospectId, request)`.
- `IProspectImportService` / `ProspectImportService` — parse + validate + confirm for the
  workbook (see Import below). A standalone service (like `ZReportImportService`), reusing the
  parsing primitives.

### `ConvertToLeadAsync` (the important flow)

```
ConvertToLeadAsync(prospectId, ConvertProspectRequest req):
  load prospect (tenant-scoped); guard: if Status == Converted → Fail("Already converted")
  // 1. Contact (reuse existing service + its dedup)
  contactResult = _contactService.CreateContactAsync(new CreateContactRequest {
      FirstName = req.ContactFirstName ?? prospect.Name,   // best-effort split; UI supplies
      LastName  = req.ContactLastName,
      Email = prospect.Email, PhoneNumber = prospect.Phone,
      CompanyName = prospect.Name, Country = ..., Notes = prospect.PublicEvidence })
  if (!contactResult.Success):
      // dedup blocked → surface; UI offers "reuse existing contact" path (req.ExistingContactId)
      if req.ExistingContactId == null → return contactResult  (Fail w/ existing name)
      contactId = req.ExistingContactId
  else contactId = contactResult.Id
  // 2. Lead (reuse existing service)
  leadResult = _leadRequestService.CreateLeadRequestAsync(new CreateLeadRequestDto {
      ContactId = contactId,
      ProductId = campaign.SalesProductId,
      LeadSourceTypeId = 4,               // Cold Call (existing seed)
      SourceUrl = prospect.Website,
      RequestText = req.RequestText ?? prospect.WhyFit })
  // 3. Mark converted + keep the measurement link
  _prospectRepository.SetConverted(prospectId, leadResult.Id)   // Status=5, ConvertedLeadRequestId, ConvertedAtUtc
  return Ok(leadResult.Id)
```

Notes: no new source needed (Cold Call exists). The dedup-blocked case is surfaced exactly as
`CreateContactAsync` already does, and the UI can offer "use the existing contact" (passing
`ExistingContactId`). Double-convert is guarded by the status check + the fact
`ConvertedLeadRequestId` is already set.

### Controller

Extend `SalesController` (keeps the `sales` gate and existing DI) OR a dedicated
`ProspectingController : Controller` with `[Authorize] [ModuleAccess(PortalModules.Sales)]`.
**Recommendation: a dedicated `ProspectingController`** to keep SalesController from growing
further; it injects the three new services + `ISalesProductService` + `ICurrentTenantService`.
Endpoints (all `AxGet`/`AxPost`, `Json(new{success,message,...})`):

- Pages (GET, return views): `Campaigns()`, `CampaignDashboard(id)`, `Prospects(campaignId, ...filters)`, `ProspectDetail(id)`, `Import(campaignId)`.
- `AxPostCreateCampaign` / `AxPostUpdateCampaign`
- `AxGetProspectsPaged(campaignId, priority, status, segment, nextAction, page)`
- `AxPostCreateProspect` / `AxPostUpdateProspect` / `AxPostSetProspectStatus`
- `AxPostAddProspectActivity` / `AxGetProspectActivities(prospectId)`
- `AxPostConvertProspect([FromBody] ConvertProspectRequest)`
- `AxPostImportProspects` (multipart upload) → parse+validate returns preview summary;
  `AxPostConfirmImport` commits.

### Views (mirror the mockup + existing Sales views)

`Views/Prospecting/` (or `Views/Sales/` if on SalesController): `Campaigns.cshtml`,
`CampaignDashboard.cshtml`, `Prospects.cshtml`, `ProspectDetail.cshtml`, `Import.cshtml`, plus
JS under `wwwroot/js/sales/prospecting.js`. The approved mockup
`.kiro/docs/mockups/prospects-campaigns.html` is the visual spec. Sidebar: add a "Prospecting"
`nav-sub-item` in the Opportunities section (`ModuleNavigation/Default.cshtml`).

## Excel Import

Reuse the parsing primitives (`ExcelParser` + `ColumnMapper`, ClosedXML), not the
purchase-coupled `ImportEngineService` validation/confirm.

`ProspectImportService`:
1. **Parse** the workbook; select the **"30 Prospects"** sheet by name; ignore "Campaign
   Summary" and "Weekly Call Tracker". Enforce `.xlsx` + size limit (mirror existing 5 MB).
2. **Map** the 19 known columns (documented order in the workbook) to Prospect fields.
3. **Validate** per row: scores 0–5; recompute `Total`; if file Total ≠ computed → mark
   `NeedsReview` (use computed). Derive/validate Priority from computed Total.
4. **Duplicate detect** within the campaign by `ImportRowId` (workbook `ID`) first, else
   `Name` + (`Website` or `Email`) → mark `MatchedSkipped`.
5. **Preview** a summary: created / matched-skipped / needs-review / invalid (per-row reasons).
6. **Confirm** inserts the non-duplicate rows into the campaign in one transaction; re-running
   is safe because matches are skipped (idempotent, Req 6.7).

## Error Handling

- Services return `ServiceResult` / `ServiceResult<T>`; repositories `try/catch (Exception ex) { throw; }`.
- Controller AJAX returns `Json(new { success, message })`; UI uses BlockUI + SweetAlert2.
- Convert guards: already-converted, dedup-blocked (surface the existing contact), missing
  campaign product (allow lead with null product, warn).
- Import: never trust file Total; reject scores out of range; per-row reasons in the summary.

## Testing Strategy

- **Import:** the real `Chaplin-Pro-30-Cyprus-Prospects.xlsx` → 30 valid prospects; a tampered
  Total row → NeedsReview; a re-run → all MatchedSkipped (idempotent).
- **Convert:** creates Contact + Lead (Source = Cold Call, Product = campaign product), marks
  prospect Converted with `ConvertedLeadRequestId` set; second convert is rejected; dedup-blocked
  path reuses the existing contact.
- **Isolation:** all queries tenant-scoped; another business cannot see the campaign/prospects.
- **Score:** Total/Priority computed correctly at the A/B/Hold boundaries.
- **Regression:** existing Lead/Contact/Opportunity flows unaffected; build 0 errors.

## Migration & Rollout

1. Migration **210** — create `[sales].[ProspectCampaign]`, `[sales].[Prospect]`,
   `[sales].[ProspectActivity]` (idempotent, `USE [Portal]`, named FKs + indexes).
2. Deploy entities/EF config/repositories/services/controller/views/JS/sidebar.
3. Acceptance = the real Chaplin campaign: create campaign → import 30 → work list → activities
   → convert on interest → confirm the lead retains prospect+campaign attribution.

## Open Decisions (resolve at task time)

1. **Campaign/Prospect/Activity status as TINYINT enums vs. lookup tables.** Lean TINYINT for
   V1 (small fixed sets, fewer tables); revisit if the team wants `LeadStatusType`-style lookups.
2. **Controller placement:** dedicated `ProspectingController` (recommended) vs. extend
   `SalesController`.
3. **Contact name split on convert:** the Prospect has an org `Name` + `PublicContactRole`, not
   first/last. The convert UI should collect the person's first/last (prefilled best-effort);
   confirm the UX.
4. **Per-business Prospect display number** (like `LeadNumber`) — nice-to-have, likely skip in V1.
