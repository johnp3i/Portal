# Implementation Plan: Prospect Activity View

Read-only, paged, filterable prospect activity log (overall + per-campaign). No schema changes.
Reuses existing `[sales].[ProspectActivity]` / `[sales].[Prospect]` / `[sales].[ProspectCampaign]`.

---

- [x] 1. DTOs and filter model
  - Add `ProspectActivityFilter` (CampaignId?, ActivityType?, Sentiment?, City?, Search?, DateFrom?, DateTo?).
  - Add `ProspectActivityLogItemDto`, `ProspectActivitySummaryDto`, `ProspectActivityLogResultDto`
    (per design). Item DTO carries `HasNextActionContact`/`HasNextActionPhone` presence flags ONLY —
    never the raw name/phone.
  - Location: `Portal.Infrastructure/Models/Sales/ProspectDtos.cs`.
  - _Requirements: 4, 5, 6, 7_

- [x] 2. Repository query — paged, filtered activity log
  - Add `GetActivityLogPagedAsync(businessId, filter, page, pageSize)` to `ProspectActivityRepository`:
    JOIN ProspectActivity → Prospect → ProspectCampaign; WHERE built from the filter; tenant-scoped;
    `ORDER BY OccurredAtUtc DESC, Id DESC`; OFFSET/FETCH pagination; return rows + total count.
  - Use FULL table names (no short aliases) per SQL steering. Read TIME via
    `TimeOnly.FromTimeSpan((TimeSpan)reader.GetValue(...))`.
  - _Requirements: 2, 3, 4, 5, 7, 9_

- [x] 3. Repository query — period summary
  - Add `GetActivitySummaryAsync(businessId, filter)` using conditional aggregation over the SAME
    filtered set (ignore pagination): Total, Calls, Emails, Meetings, FollowUps, Positive, Negative.
  - _Requirements: 6_

- [x] 4. Repository query — distinct cities
  - Add `GetDistinctCitiesAsync(businessId, campaignId?)` → distinct non-empty `Prospect.Location`
    for the scope, ordered alphabetically.
  - _Requirements: 4.1_

- [x] 5. Service
  - Add `IProspectActivityLogService` + `ProspectActivityLogService`: resolve business via
    `ICurrentTenantService`; call the repo paged query + summary; map rows → item DTOs (resolve
    ActivityTypeName / SentimentName; compute presence flags; resolve PerformedByName best-effort;
    never expose raw name/phone). Expose a cities helper.
  - Register in `Program.cs` beside the other prospecting services.
  - _Requirements: 2, 5, 6, 9_

- [x] 6. Controller — page action
  - Add `ProspectingController.Activity(int? campaignId)`: load campaigns for the dropdown + prefill
    the selected campaign; return the view.
  - _Requirements: 1, 2.4_

- [x] 7. Controller — AJAX list endpoint
  - Add `AxGetProspectActivity(campaignId?, activityType?, sentiment?, city?, search?, dateFrom?,
    dateTo?, page=1)`: build filter, call service, return `{ success, data, summary, pagination }`.
    Try/catch with `(Exception ex)`, log, return `{ success=false, message }`.
  - _Requirements: 2, 3, 4, 5, 6, 7, 9_

- [x] 8. Controller — cities endpoint (optional)
  - Add `AxGetProspectActivityCities(campaignId?)` returning distinct cities, OR render cities
    server-side on page load. Pick one; keep it consistent.
  - _Requirements: 4.1_

- [x] 9. View — Activity.cshtml
  - Build `Views/Prospecting/Activity.cshtml` per the mockup and layout-standards: topbar, filter card
    (`margin-bottom:22px`), summary strip, table card with empty state + pagination (`margin-top:18px`).
  - Filters: Campaign, Activity type, Interaction signal, City, Date from/to, Search, Filter/Clear,
    quick-period chips (Today default).
  - _Requirements: 1, 2, 3, 4, 5, 6, 7_

- [x] 10. JS — list loading & rendering
  - Extend `prospecting.js`: `initActivityLog()` (read `campaignId`, default Today, load page 1),
    `loadActivityLog(page)` (collect filters → BlockUI → fetch → render table + summary + pagination →
    unblock; SweetAlert2 on error), quick-period chip handlers, Clear handler.
  - Reuse the next-action render helpers (date + `fmtTime` + muted person/phone icon badges gated on
    the presence flags). Bump `prospecting.js?v=` in all Prospecting views.
  - _Requirements: 3, 4, 5, 6, 7, 9_

- [x] 11. Nav entry points
  - Add "View Activity" → `/Prospecting/Activity` on the Campaigns landing topbar.
  - Add "View Activity" → `/Prospecting/Activity?campaignId={id}` on the campaign dashboard.
  - _Requirements: 1.2, 1.3_

- [x] 12. CSV export
  - Add `ExportProspectActivityCsv(... same filter ...)` returning a CSV of the full filtered set,
    EXCLUDING next-action contact name/phone. Wire an "Export CSV" button.
  - _Requirements: 8_

- [x] 13. Verify
  - Build (no CS/RZ errors). Manual pass (pending deploy): default Today/all loads; chips change
    range; each filter narrows table AND summary; prospect links open detail; privacy — inspect the
    AJAX payload and confirm NO raw contact name/phone is present (only presence flags).
  - _Requirements: all_

---

## Notes

- Read-only feature: no new write paths, no schema changes, no changes to existing activity logging.
- Prospecting-only: do NOT merge in lead-pipeline `[sales].[ActivityFeed]` events.
- Privacy is a hard requirement: the next-action contact name / phone must never reach the browser or
  the CSV — only presence flags drive the icon badges, with generic tooltips.
- Mockup reference: `.kiro/docs/mockups/prospect-activity-view.html`.
