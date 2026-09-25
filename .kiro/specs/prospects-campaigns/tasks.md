# Implementation Plan — Prospects & Campaigns

## Overview

Build the Prospects & Campaigns module under Sales/Opportunities: 3 new `[sales]` tables
(ProspectCampaign, Prospect, ProspectActivity), repositories/services/controller/views mirroring
existing Sales conventions, an Excel import for the Chaplin workbook, and a Convert-to-Lead that
reuses the existing contact + lead services and keeps a prospect→lead reference. Assistant is
backlogged (static weekly-objective panel only). No pricing change (rides `sales` gate).

Order: DB → Entities/EF → Repositories → Services → Convert flow → Import → Controller/DI →
Views/Sidebar → Verification.

## Tasks

- [ ] 1. Migration
  - [ ] 1.1 Migration `210_CreateProspectsCampaigns.sql` — create `[sales].[ProspectCampaign]`,
    `[sales].[Prospect]`, `[sales].[ProspectActivity]`. Idempotent, `USE [Portal]`, header
    comment block, `IF NOT EXISTS` guards, `IDENTITY(1,1)` PKs, named FKs
    (`FK_Prospect_ProspectCampaign`, `FK_Prospect_LeadRequest` on `ConvertedLeadRequestId`,
    `FK_ProspectActivity_Prospect`, `FK_ProspectCampaign_Product`), named indexes, and
    `CreatedAtUtc DATETIME NOT NULL DEFAULT (GETUTCDATE())`.
    - _Requirements: 1, 2, 3, 4, 8.3, 8.4, 9.1_

- [ ] 2. Entities + EF
  - [ ] 2.1 Create entities `ProspectCampaign`, `Prospect`, `ProspectActivity` in
    `Entities/Sales/` (fields per design; scores as `byte`, status as `byte`).
    - _Requirements: 1, 2, 3, 4_
  - [ ] 2.2 Add `ConfigureProspectCampaign` / `ConfigureProspect` / `ConfigureProspectActivity`
    to `PortalDbContext`, wire the calls in the Sales block of `OnModelCreating`, add the three
    DbSets, and add all three to the tenant query-filter block.
    - _Requirements: 8.3, 9.3_

- [ ] 3. Repositories (raw SQL, over `GenericStoredProcedureRepository`)
  - [ ] 3.1 `ProspectCampaignRepository` — Insert/Update/GetById/GetAllByBusiness + `GetFunnelCountsAsync(campaignId, businessId)` (grouped COUNT by prospect Status).
    - _Requirements: 1, 7.3_
  - [ ] 3.2 `ProspectRepository` — Insert (SCOPE_IDENTITY), Update, GetById, `GetPagedByCampaignAsync` (filters: priority-from-score, status, segment, next-action), `UpdateStatusAsync`, `SetConvertedAsync(id, leadRequestId)`, `FindForImportAsync(campaignId, importRowId, name, website, email)`.
    - _Requirements: 2, 5.5, 6.5, 7.1_
  - [ ] 3.3 `ProspectActivityRepository` — Insert, `GetByProspectAsync` (desc), `GetLastActivityByProspectIdsAsync(ids)` (batched, for the list column).
    - _Requirements: 4_

- [ ] 4. Services + DTOs
  - [ ] 4.1 DTOs in `Models/Sales/`: `ProspectCampaignDto`/`CreateProspectCampaignRequest`,
    `ProspectListRowDto`/`ProspectDetailDto`/`CreateProspectRequest`/`UpdateProspectRequest`,
    `ProspectActivityDto`/`AddProspectActivityRequest`, `ConvertProspectRequest`,
    `ProspectImportResultDto`, `CampaignFunnelDto`, `WeeklyObjectiveDto`. Total + Priority are
    computed in the DTO mapping (never stored).
    - _Requirements: 3.2, 3.3, 5, 6.6_
  - [ ] 4.2 `ProspectCampaignService` — CRUD, `GetFunnelAsync`, `GetWeeklyObjectiveAsync`
    (static: weekly target, A-ready count, follow-ups-due count — simple queries, no assistant).
    - _Requirements: 1, 7.3, 7.4_
  - [ ] 4.3 `ProspectService` — CRUD, paged list mapping (compute Total/Priority + attach last
    activity), status changes, validation (scores 0–5).
    - _Requirements: 2, 3, 7.1, 7.2_

- [ ] 5. Convert-to-Lead (reuse existing services)
  - [ ] 5.1 `ProspectService.ConvertToLeadAsync(prospectId, ConvertProspectRequest)` — guard
    already-converted; call `IContactService.CreateContactAsync` (handle dedup-blocked →
    reuse `ExistingContactId`); call `ILeadRequestService.CreateLeadRequestAsync` with
    `LeadSourceTypeId = 4 (Cold Call)` and campaign product; `SetConvertedAsync(id, leadId)`.
    Inject `IContactService`, `ILeadRequestService`, `ICurrentTenantService`.
    - _Requirements: 5, 9.1, 9.2_

- [ ] 6. Excel import
  - [ ] 6.1 `ProspectImportService` — reuse `ExcelParser` + `ColumnMapper`; select the
    "30 Prospects" sheet, ignore Summary/Tracker; map the 19 columns; validate scores 0–5,
    recompute Total (flag mismatches as NeedsReview), derive/validate Priority.
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.8_
  - [ ] 6.2 Duplicate detection within campaign (ImportRowId, else Name + Website/Email) →
    MatchedSkipped; confirm-stage inserts non-duplicates transactionally; safe re-run.
    - _Requirements: 6.5, 6.6, 6.7_

- [ ] 7. Controller + DI
  - [ ] 7.1 `ProspectingController : Controller` with `[Authorize] [ModuleAccess(PortalModules.Sales)]`
    — page GETs (Campaigns, CampaignDashboard, Prospects, ProspectDetail, Import) + AJAX
    (`AxPostCreateCampaign`, `AxGetProspectsPaged`, `AxPostCreate/UpdateProspect`,
    `AxPostSetProspectStatus`, `AxPostAddProspectActivity`, `AxGetProspectActivities`,
    `AxPostConvertProspect`, `AxPostImportProspects`, `AxPostConfirmImport`). `Json(new{success,message})`.
    - _Requirements: 5, 6, 7, 8.1_
  - [ ] 7.2 Register repositories (factory `new X(sp.GetRequiredService<PortalDbContext>())`)
    and services (by interface) in `Program.cs` Sales block.
    - _Requirements: 8.3_

- [ ] 8. Views + sidebar (per approved mockup)
  - [ ] 8.1 Views: `Campaigns`, `CampaignDashboard` (funnel + static weekly-objective panel
    tagged "Prospecting Assistant · coming soon"), `Prospects` (list + filters + row actions,
    with the "not a Sales contact" notice), `ProspectDetail` (identity/research + computed
    score + activity timeline + Convert), `Import` (upload → preview summary → confirm).
    + `wwwroot/js/sales/prospecting.js` (BlockUI + SweetAlert2 per UI standards).
    - _Requirements: 6, 7, 5.7_
  - [ ] 8.2 Add a "Prospecting" `nav-sub-item` under the "Opportunities" section in
    `ModuleNavigation/Default.cshtml` with active-state handling.
    - _Requirements: 8.2_

- [ ] 9. Verification
  - [ ] 9.1 Build solution — 0 errors.
    - _Requirements: all_
  - [ ] 9.2 Acceptance (real Chaplin campaign): create campaign (Product = Chaplin Pro, weekly
    target 15) → import the workbook (30 valid; tampered-Total row → NeedsReview; re-run →
    all MatchedSkipped) → work the list, log activities without creating leads → Convert an
    engaged prospect (Contact + Lead created, Source = Cold Call, prospect marked Converted with
    `ConvertedLeadRequestId`) → second convert rejected → confirm tenant isolation.
    - _Requirements: 5, 6, 9_

## Notes

- Migrations start at **210** (current highest is 209).
- "Cold Call" lead source already exists (`LeadSourceType` Id 4) — no new source seed.
- Campaign Product references `[sales].[Product]` (SalesProduct), matching `LeadRequest.ProductId`.
- Prospect activities need their OWN table — `[sales].[ActivityFeed]` is lead-keyed.
- Reuse `IContactService.CreateContactAsync` + `ILeadRequestService.CreateLeadRequestAsync` for
  convert — do NOT duplicate contact/lead creation.
- Status sets are TINYINT enums in V1 (open decision: lookup tables if the team prefers).
- Follow repo/service/AJAX conventions; `try/catch (Exception ex) { throw; }`; null-safe SQL
  params; tenant scoping via `@BusinessId` in WHERE.

## Backlog (explicitly deferred — not V1)

> **Full rationale for each deferred item is documented in
> `.kiro/docs/future-features/prospects-campaigns-future.md`.** Summary below.

- **Prospecting Assistant** — a scheduled/scan digital assistant that generates the weekly
  cold-call objective from the campaign target + prospect statuses. Build after the module is
  used and tested, on the existing digital-assistants framework. V1 ships the static panel +
  the `GetWeeklyObjectiveAsync` seam it will later enrich.
- `ProspectCampaignEntry` join to let one organisation participate in multiple campaigns (with a
  per-campaign score/priority/status), without duplicating the organisation. Deferred because V1
  is one-campaign-per-prospect (spreadsheet parity); add only if overlapping campaigns hitting
  the same orgs become a real need. Reversible seam.
- Scoring-performance report (predicted Priority vs. actual conversion) — the data
  (`ConvertedLeadRequestId`) is captured in V1; the report/visualisation is a fast follow-on.
- Per-business Prospect display number (like `LeadNumber`).

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1", "6.1", "6.2"] },
    { "id": 5, "tasks": ["7.1", "7.2"] },
    { "id": 6, "tasks": ["8.1", "8.2"] },
    { "id": 7, "tasks": ["9.1", "9.2"] }
  ]
}
```
