# Design Document: Prospect Activity View

## Overview

A read-only, paged, filterable log of prospect activities, served by the Prospecting module. It adds
one new page (`/Prospecting/Activity`), one AJAX list endpoint, one AJAX city-options endpoint
(optional), an optional CSV export, and the supporting repository query / service method / DTOs. It
reads existing `[sales].[ProspectActivity]` joined to `[sales].[Prospect]` and
`[sales].[ProspectCampaign]`. No schema changes. No changes to any write path.

Architecture follows the established pattern: Controller → Service → Repository, with a Razor page +
`prospecting.js` for the client, matching the working list already built.

## Data Sources (existing, unchanged)

- `[sales].[ProspectActivity]` — Id, BusinessId, ProspectId, ActivityType (byte), OccurredAtUtc,
  PerformedByUserId, Sentiment (byte?), IsFollowUp (bit), Outcome, Notes, NextAction, NextActionDate,
  NextActionTime (time), NextActionContactName, NextActionPhone, CreatedAtUtc.
- `[sales].[Prospect]` — Id, Name, Location (city), Segment, ProspectCampaignId, BusinessId.
- `[sales].[ProspectCampaign]` — Id, Name, BusinessId.

Reference existing code to mirror conventions:
- `ProspectActivityRepository.GetRecentByCampaignAsync` — existing join-to-prospect feed (capped,
  no filters). The new query generalises this: adds date/type/sentiment/city/search filters, the
  all-campaigns scope, pagination, and the extra columns (campaign name, segment, next-action fields).
- `ProspectActivityRepository.ReadProspect`-style hand-written `DbDataReader` mapping (note: TIME
  columns surface as `TimeSpan`; sentiment/type as `byte`).

## Component / Flow

```
Prospecting/Activity.cshtml  ──(page load, default Today/all)──►  AxGetProspectActivity (JSON)
   prospecting.js: loadActivityLog(filters,page)                      │
        │  BlockUI.show → fetch → render table + summary → BlockUI.hide
        ▼                                                             ▼
ProspectingController.AxGetProspectActivity  ──►  IProspectActivityLogService.GetAsync(filter,page,size)
                                                      │
                                                      ▼
                                   ProspectActivityRepository.GetActivityLogPagedAsync(...)
                                   ProspectActivityRepository.GetActivitySummaryAsync(...)
                                   ProspectActivityRepository.GetDistinctCitiesAsync(scope)   [optional]
```

## Backend

### 1. Filter model

New `ProspectActivityFilter` (in `Portal.Infrastructure/Models/Sales/ProspectDtos.cs` or a new file):

```
int? CampaignId;          // null = all campaigns
byte? ActivityType;       // null = all
byte? Sentiment;          // null = any
string? City;             // null/empty = all; exact match on Prospect.Location
string? Search;           // contains over Name / Outcome / Notes / NextAction
DateOnly? DateFrom;       // inclusive (local day start)
DateOnly? DateTo;         // inclusive (local day end)
```

The controller translates the quick-period chips into DateFrom/DateTo before building the filter (so
the service deals only with explicit bounds). "All time" ⇒ both null.

### 2. DTOs

`ProspectActivityLogItemDto` (one row):

```
int ActivityId;
int ProspectId;
string ProspectName;
string? City;            // Prospect.Location
string? Segment;
int ProspectCampaignId;
string CampaignName;
byte ActivityType;       // + ActivityTypeName resolved in service
string ActivityTypeName;
DateTime OccurredAtUtc;
byte? Sentiment;         // + SentimentName
string? SentimentName;
bool IsFollowUp;
string? Outcome;
string? NextAction;
DateOnly? NextActionDate;
TimeOnly? NextActionTime;
bool HasNextActionContact;   // presence flag only — never the name (privacy)
bool HasNextActionPhone;     // presence flag only — never the number (privacy)
string? PerformedByName;     // resolved display name, or null
```

PRIVACY: the DTO carries only `HasNextActionContact` / `HasNextActionPhone`, mirroring the working
list. The actual `NextActionContactName` / `NextActionPhone` are never serialised to the browser.

`ProspectActivitySummaryDto`:

```
int Total;
int Calls;      // ActivityType == 2
int Emails;     // ActivityType == 3
int Meetings;   // ActivityType == 4
int FollowUps;  // IsFollowUp == 1
int Positive;   // Sentiment == 1
int Negative;   // Sentiment == 3
```

`ProspectActivityLogResultDto` wraps items + pagination + summary for the single AJAX response:

```
List<ProspectActivityLogItemDto> Items;
int CurrentPage; int PageSize; int TotalCount; int TotalPages;
ProspectActivitySummaryDto Summary;
```

### 3. Repository (`ProspectActivityRepository`)

Add three methods (hand-written SQL + reader, matching the file's existing style):

- `GetActivityLogPagedAsync(int businessId, ProspectActivityFilter f, int page, int pageSize)`
  → `(List<row>, int totalCount)`. Builds a `WHERE` from the filter; joins
  `ProspectActivity act` → `Prospect pr` → `ProspectCampaign pc`. Uses full table names (no short
  aliases per SQL steering) in the final SQL. Date filter: `CAST(act.OccurredAtUtc AS DATE)` BETWEEN
  bounds. Search: `(pr.Name LIKE @q OR act.Outcome LIKE @q OR act.Notes LIKE @q OR act.NextAction LIKE @q)`
  with `@q = '%' + term + '%'`. Pagination via `OFFSET/FETCH`, `ORDER BY act.OccurredAtUtc DESC, act.Id DESC`.
- `GetActivitySummaryAsync(int businessId, ProspectActivityFilter f)` → counts via conditional
  aggregation (`SUM(CASE WHEN ... THEN 1 ELSE 0 END)`) over the SAME filtered set (date/campaign/
  city/type/sentiment/search), ignoring pagination.
- `GetDistinctCitiesAsync(int businessId, int? campaignId)` → distinct non-empty `Prospect.Location`
  for the scope, ordered alphabetically. (Used to populate the City dropdown.)

Note the TIME-column read gotcha already encountered: read `NextActionTime` via
`TimeOnly.FromTimeSpan((TimeSpan)reader.GetValue(...))`.

### 4. Service (`IProspectActivityLogService` / `ProspectActivityLogService`)

New service (co-located with the other Sales services). Responsibilities:
- Resolve current business via `ICurrentTenantService`.
- Call the repository paged query + summary (+ cities when requested).
- Map rows → `ProspectActivityLogItemDto`: resolve `ActivityTypeName` and `SentimentName` from the
  same maps the prospect code already uses (1 Research…8 Other; 1 Positive/2 Neutral/3 Negative);
  compute `HasNextActionContact` / `HasNextActionPhone` from the (server-side only) name/phone;
  resolve `PerformedByName` (best-effort; a lookup of user → display name, else null).
- Never place the raw contact name / phone in the DTO.

Register in `Program.cs` next to the other prospecting services.

### 5. Controller (`ProspectingController`)

- `GET Activity(int? campaignId)` — returns the view; passes `campaignId` to prefill the Campaign
  filter and the list of campaigns for the dropdown (via the existing campaign service).
- `GET AxGetProspectActivity(int? campaignId, byte? activityType, byte? sentiment, string? city,
  string? search, DateTime? dateFrom, DateTime? dateTo, int page = 1)` — builds the filter, calls the
  service, returns `Json(new { success, data = items, summary, pagination })`. Try/catch logs and
  returns `{ success = false, message }`.
- `GET AxGetProspectActivityCities(int? campaignId)` — returns distinct cities for the dropdown
  (optional; the view may also render cities server-side at page load).
- `GET ExportProspectActivityCsv(... same filter ...)` — optional; returns a CSV FileResult of the
  full filtered set, excluding the next-action name/phone columns.

All new AJAX methods follow the `AxGet` naming rule.

## Frontend

### Page: `Views/Prospecting/Activity.cshtml`

- Topbar (eyebrow "Opportunities · Prospecting", heading "Prospect Activity", subtitle), with an
  optional "Export CSV" button.
- Filter card (`.glass.card-pad`, `margin-bottom:22px`): Campaign, Activity type, Interaction signal,
  City, Date from, Date to, Search, then Filter / Clear; a quick-period chip row beneath. Field
  widths/gaps per layout-standards (min-width:180px, gap:14px, buttons wrapper padding-bottom:2px).
- Summary strip: a row of stat cards (Total, Calls, Emails, Meetings, Follow-ups, Positive/Negative).
- Table card (`.glass.card-pad`): columns When / Prospect / Campaign / Type / Outcome / Signal /
  Next action / By; empty state inside the card; pagination row (`margin-top:18px`).

### JS (reuse `prospecting.js`, bump its `?v=`)

- `initActivityLog()` reads any `campaignId` from the query string, sets the default Today period,
  loads page 1.
- `loadActivityLog(page)` collects filter values, `BlockUI.show`, `fetch('/Prospecting/AxGetProspectActivity?…')`,
  render rows + summary + pagination, `BlockUI.hide`. On error → SweetAlert2.
- Quick-period chips set Date-from/Date-to and reload (page 1).
- Row rendering reuses the next-action helpers: date + `fmtTime` + the muted person/phone icon badges
  gated on `hasNextActionContact` / `hasNextActionPhone` (identical to the working list).
- Clear resets controls to defaults and reloads.

### Nav entry points

- `Views/Prospecting/Campaigns.cshtml` topbar: add a "View Activity" link to `/Prospecting/Activity`.
- Campaign dashboard: add a "View Activity" link to `/Prospecting/Activity?campaignId={id}`.

## Privacy / Security

- Tenant scoping on every query (BusinessId filter), identical to existing prospecting queries.
- Next-action contact name / phone never leave the server for this view — only boolean presence flags;
  tooltips are generic. Matches the working-list rule already shipped.
- CSV export (if built) excludes name/phone columns.

## Testing (lightweight, no new framework)

- Repository: filtered paging returns correct rows/counts for: all-campaigns vs single campaign; date
  bounds inclusive; city/type/sentiment/search filters; empty result.
- Service: type/sentiment name resolution; presence-flag computation (name/phone present vs absent);
  summary counts match the filtered set.
- Manual: default Today/all loads; chips change the range; campaign/city/type/signal/search narrow
  both table and summary; privacy — inspect the AJAX payload and confirm no raw name/phone present.

## Out of Scope

- No new activity creation/edit here (read-only). Logging still happens on the prospect detail.
- No mixing in lead-pipeline `[sales].[ActivityFeed]` events — this view is prospecting-only.
- No schema changes.
