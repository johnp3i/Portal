# Implementation Plan: Activity Log (Business Manager View)

## Overview

This plan implements the business-facing Activity Log — a timeline-style activity feed that transforms raw audit data into plain-English summaries for business managers. The existing infrastructure (`[audit].[AuditLog]` table, `AuditLogQueryService`, `AuditLogQueryRepository`, `AuditInterceptor`) remains unchanged. The SuperAdmin audit viewer at `/Admin/Audit` is preserved. This feature builds a new presentation layer and controller at `/Activity` with business-friendly filters, relative timestamps, quick stats, and expandable detail panels.

## Tasks

- [x] 1. DTOs and Models

  - [x] 1.1 Create ActivityItemDto
    - Create `Portal.Infrastructure/Models/ActivityItemDto.cs`
    - Properties: `Id` (long), `Summary` (string — plain-English description), `ActorName` (string — resolved display name), `ActionType` (string — "Created", "Edited", "Deleted", "StatusChanged"), `EntityType` (string — business-friendly name like "Invoice", "Customer"), `EntityId` (string — RecordId), `EntityDisplayRef` (string? — human-readable identifier like INV-2026-0089), `EntityDetailUrl` (string? — link to detail page, null if deleted or no route), `RelativeTimestamp` (string — formatted relative time), `TimestampUtc` (DateTime), `OldValues` (string? — raw JSON), `NewValues` (string? — raw JSON), `ChangedFields` (List<FieldChangeDto>? — parsed field changes for detail panel)
    - Create nested `FieldChangeDto`: `FieldName` (string), `OldValue` (string?), `NewValue` (string?)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 6.6, 6.7, 6.8, 8.1–8.6_

  - [x] 1.2 Create ActivityStatsDto
    - Create `Portal.Infrastructure/Models/ActivityStatsDto.cs`
    - Properties: `ChangesThisWeek` (int), `ActiveTeamMembers` (int), `MostActiveArea` (string), `LastActivityRelative` (string)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 1.3 Create ActivityFilterDto
    - Create `Portal.Infrastructure/Models/ActivityFilterDto.cs`
    - Properties: `WhatChanged` (string? — maps to TableName group), `WhoChanged` (string? — UserId or "system"), `ChangeType` (string? — "Created", "Edited", "Deleted", "StatusChanged"), `DateFrom` (DateTime?), `DateTo` (DateTime?), `PageNumber` (int, default 1), `PageSize` (int, default 8)
    - _Requirements: 7.1, 7.2, 7.3, 7.5_


- [x] 2. Services — Activity Summary and Supporting Components

  - [x] 2.1 Create IActivitySummaryService interface
    - Create `Portal.Infrastructure/Services/IActivitySummaryService.cs`
    - Methods: `Task<List<ActivityItemDto>> TransformAsync(List<AuditLog> records)`, `Task<ActivityStatsDto> GetQuickStatsAsync()`
    - _Requirements: 2.1, 5.1_

  - [x] 2.2 Create UserNameResolver
    - Create `Portal.Infrastructure/Services/UserNameResolver.cs`
    - Inject `MembershipDbContext` and `ICurrentTenantService`
    - Method: `Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<string> userIds)` — single query to MembershipDbContext joining UserBusiness + AspNetUsers scoped to current BusinessId, returns dictionary of userId → "{FirstName} {LastInitial}." format
    - When userId is null → return "System"
    - When userId not found → return "Unknown User"
    - Batch all unique userIds in one query to avoid N+1
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 2.3 Create RelativeTimestampFormatter
    - Create `Portal.Infrastructure/Services/RelativeTimestampFormatter.cs`
    - Static method: `string Format(DateTime timestampUtc, DateTime? nowUtc = null)` — accepts optional now for testability
    - Rules: <60s → "Just now"; 1–59min → "{N} min ago"; 1–23h → "{N} hour ago" / "{N} hours ago"; yesterday → "Yesterday at {HH:mm}"; 2–6 days → "{N} days ago"; 7+ days → "dd MMM yyyy"
    - All comparisons use UTC
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [x] 2.4 Implement ActivitySummaryService
    - Create `Portal.Infrastructure/Services/ActivitySummaryService.cs`
    - Inject `ICurrentTenantService`, `UserNameResolver`, `AuditLogQueryRepository`
    - `TransformAsync`: resolve actor names via UserNameResolver batch call; for each AuditLog record: determine ActionType (Insert→"Created", Update→"Edited" or "StatusChanged" if status field changed, Delete→"Deleted"); map TableName to entity type; resolve entity display ref from RecordId + JSON values; build summary string; parse field changes for detail panel; generate entity detail URL; format relative timestamp
    - `GetQuickStatsAsync`: query AuditLog records for current tenant within last 7 days; compute total count, distinct non-null UserIds, most active TableName mapped to friendly name, last activity timestamp formatted with RelativeTimestampFormatter; return zeros/defaults if no records
    - TableName mapping: Invoice/InvoiceLine→"Invoice", Quotation/QuotationLine/QuotationContact→"Quotation", Customer→"Customer", Purchase→"Purchase", Payment→"Payment", CreditNote/CreditNoteLine→"Credit Note", Business/BusinessProfile→"Settings"
    - Status detection: changed fields ending in "StatusTypeId" or named "Status"
    - Fallback: if JSON parsing fails or identifier can't be resolved, use raw TableName + RecordId
    - Entity detail URLs: Invoice→`/Invoice/Details/{id}`, Customer→`/Customer/Details/{id}`, Quotation→`/Quotation/Details/{id}`, Purchase→`/Purchase/Details/{id}`; deleted entities or unknown types → null
    - `try/catch (Exception ex) { throw; }` per repository standards
    - _Requirements: 2.1–2.8, 3.1, 4.1–4.7, 5.1–5.5, 8.1–8.6_


- [x] 3. Checkpoint — Service Layer Complete
  - Ensure all DTOs and services compile cleanly
  - Verify `UserNameResolver` batch resolution logic
  - Verify `RelativeTimestampFormatter` covers all time brackets
  - Verify `ActivitySummaryService` handles all action types and TableName mappings
  - Ask the user if questions arise


- [x] 4. Web — Activity Controller

  - [x] 4.1 Create ActivityController
    - Create `Portal.Web/Controllers/ActivityController.cs`
    - Apply `[Authorize]`, `[ModuleAccess(PortalModules.Audit, AccessLevels.ReadOnly)]`, `[Route("Activity")]`
    - Do NOT require SuperAdmin role — any user with `audit_log` module at ReadOnly or Full level has access
    - Constructor injects `IActivitySummaryService`, `IAuditLogQueryService`, `ICurrentTenantService`, `MembershipDbContext`, `ILogger<ActivityController>`
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 4.2 Implement Index action
    - `[HttpGet("")]` `Index()`: load team members for filter dropdown (query MembershipDbContext UserBusiness + AspNetUsers scoped to current business); pass to ViewBag.TeamMembers as list of `{ UserId, DisplayName }` objects; return `View()`
    - _Requirements: 1.1, 7.2_

  - [x] 4.3 Implement AxGetActivity endpoint
    - `[HttpGet("AxGetActivity")]` — accepts `ActivityFilterDto` from query string
    - Map `WhatChanged` filter to list of TableName values (e.g., "Invoices" → ["Invoice", "InvoiceLine"])
    - Map `WhoChanged` filter: "system" → filter UserId IS NULL; specific userId → filter exact match; null/empty → no filter
    - Map `ChangeType` filter: "Created"→Action "Insert", "Edited"→Action "Update", "Deleted"→Action "Delete", "StatusChanged"→Action "Update" + post-filter for status fields in JSON
    - Build `AuditLogFilter` from mapped values; call `IAuditLogQueryService.GetAuditLogsAsync`
    - Transform results via `IActivitySummaryService.TransformAsync`
    - For "StatusChanged" filter: post-filter transformed results to only include items where ActionType == "StatusChanged"
    - Return `Json(new { success = true, data = activityItems, totalCount, currentPage, totalPages })`
    - On exception: log, return `Json(new { success = false, message = "Could not load activity data. Please try again." })`
    - _Requirements: 6.10, 6.11, 6.13, 6.14, 7.1, 7.3, 7.4, 7.5_

  - [x] 4.4 Implement AxGetStats endpoint
    - `[HttpGet("AxGetStats")]` — no parameters
    - Call `IActivitySummaryService.GetQuickStatsAsync()`
    - Return `Json(new { success = true, data = statsDto })`
    - On exception: log, return `Json(new { success = false, message = "Could not load statistics." })`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_


- [x] 5. Checkpoint — Controller Layer Complete
  - Ensure ActivityController compiles
  - Verify all AJAX endpoints follow the `AxGet` prefix naming convention
  - Verify ModuleAccess attribute uses ReadOnly (not Full, not SuperAdmin)
  - Ask the user if questions arise


- [x] 6. Web — Activity Log View

  - [x] 6.1 Create Views/Activity/Index.cshtml
    - Topbar: eyebrow "Business Operations", heading "Activity Log" (42px Manrope), muted description "Track what your team changed and when."
    - Quick stats row: 4 stat cards in a flex/grid row — "Changes this week" (number), "By team members" (number + "people"), "Most active area" (text), "Last activity" (relative timestamp); cards use `glass` styling with subtle icon/color per stat
    - Filter card (`<section class="glass card-pad" style="margin-bottom:22px;">`): flex row (gap 14px, align-items flex-end, flex-wrap wrap) with: "What changed" select (min-width 180px, options: Everything, Invoices, Quotations, Customers, Purchases, Payments, Credit Notes, Settings), "Who" select (min-width 180px, options: Everyone, System, then team members from ViewBag.TeamMembers), "Type" select (min-width 160px, options: All changes, Created, Edited, Deleted, Status changed), Date From input, Date To input, Filter button (btn-primary), Clear button (btn-secondary) — buttons in padding-bottom 2px wrapper
    - Timeline container (`<section class="glass card-pad">`): vertical timeline line via CSS pseudo-element; each activity entry: colored dot (green=Created, blue=Edited, red=Deleted, amber=StatusChanged), summary text with entity links, relative timestamp, expand/collapse chevron
    - Detail panel (expandable, slide-down): Created → "Created with values" table; Edited → "What changed" table (old values strikethrough red, new values bold green); Deleted → "Deleted record" table
    - Pagination row (margin-top 18px): "Showing X–Y of Z" left, page buttons right, default 8 per page
    - Empty state: centered message "No activity found." within the timeline card
    - _Requirements: 6.1–6.16, 9.1–9.4_

  - [x] 6.2 Implement client-side JavaScript
    - `loadStats()`: fetch `/Activity/AxGetStats`; populate stat cards; call on DOMContentLoaded
    - `loadActivity(page)`: `BlockUI.show('Loading activity...')`; build query params from filter fields; fetch `/Activity/AxGetActivity?...`; `BlockUI.hide()`; on success render timeline + pagination; on error show SweetAlert2 error (title "Error", text "Could not load activity data. Please try again.", confirmButtonColor '#0D5EA6')
    - `renderTimeline(items)`: build timeline HTML with colored dots, summaries, relative timestamps, expand controls, entity links (anchor tags for EntityDetailUrl, plain text if null); expand/collapse on row click (one at a time with slide animation)
    - `renderDetailPanel(item)`: parse ChangedFields; Created: single-column values table; Edited: two-column old/new with styling; Deleted: single-column deleted values table
    - `renderPagination(currentPage, totalPages, totalCount)`: "Showing X–Y of Z", page buttons, 8 per page
    - Filter button click → `loadActivity(1)`; Clear button click → reset all selects to first option, clear date inputs, `loadActivity(1)`
    - `DOMContentLoaded` → `loadStats()` then `loadActivity(1)`
    - _Requirements: 6.1, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10, 6.11, 6.12, 6.13, 6.14, 6.15_

  - [x] 6.3 Add responsive CSS for mobile
    - Media query `@media (max-width: 640px)`: stats row → 2-column grid; filters → vertical stack full-width; hide timeline vertical line; reduce activity row left padding; detail panels full-width without left offset
    - _Requirements: 9.1, 9.2, 9.3, 9.4_


- [x] 7. Sidebar Navigation Update

  - [x] 7.1 Add Activity Log to sidebar
    - Add "Activity Log" link in the "Business Operations" sidebar section pointing to `/Activity`
    - Use an appropriate timeline/activity icon (e.g., clock or list icon from existing icon set)
    - Conditionally show based on subscription plan having `audit_log` feature key (use existing plan feature check pattern)
    - Keep the existing "Audit Log" link in the "Administration" section at `/Admin/Audit` — do NOT remove it
    - _Requirements: 1.5, 10.1, 10.3, 10.4_


- [x] 8. DI Registration

  - [x] 8.1 Register new services in Program.cs
    - Register `UserNameResolver` as scoped
    - Register `IActivitySummaryService` → `ActivitySummaryService` as scoped
    - Do NOT modify existing `IAuditLogQueryService`, `AuditLogQueryRepository`, or `AuditInterceptor` registrations — they are already in place
    - Add required `using` directives
    - _Requirements: 1.1, 2.1, 3.1, 5.1_


- [x] 9. Final Checkpoint
  - Ensure full project compiles without errors
  - Verify `/Activity` route is accessible with `audit_log` module at ReadOnly level
  - Verify `/Admin/Audit` SuperAdmin route still works (no regression)
  - Verify sidebar shows Activity Log in Business Operations section
  - Verify quick stats load on page entry
  - Verify timeline renders with expand/collapse
  - Verify all filters map correctly to underlying query parameters
  - Ask the user if questions arise


- [ ]* 10. Property-Based Tests

  - [ ]* 10.1 PBT — RelativeTimestampFormatter
    - Create test class for `RelativeTimestampFormatter`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`
    - **Property: Timestamp bracket correctness** — *For any* UTC timestamp and reference "now" timestamp where now >= timestamp, the formatted output SHALL match the correct bracket (Just now, N min ago, N hours ago, Yesterday at HH:mm, N days ago, dd MMM yyyy) based on the time difference
    - Generate random (timestamp, now) pairs ensuring now >= timestamp; verify output matches expected bracket
    - _Requirements: 4.1–4.7_

  - [ ]* 10.2 PBT — ActivitySummaryService transformation
    - Create test class for `ActivitySummaryService.TransformAsync`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`
    - **Property: Action type mapping invariant** — *For any* AuditLog record with Action "Insert"/"Update"/"Delete", the resulting ActivityItemDto.ActionType SHALL be "Created"/"Edited" (or "StatusChanged")/"Deleted" respectively
    - **Property: TableName mapping invariant** — *For any* AuditLog record with a known TableName, the resulting ActivityItemDto.EntityType SHALL be the corresponding business-friendly name from the mapping dictionary
    - **Property: Deleted entities have no detail URL** — *For any* AuditLog record with Action "Delete", the resulting ActivityItemDto.EntityDetailUrl SHALL be null
    - Generate random AuditLog records with varied Action, TableName, OldValues, NewValues; mock UserNameResolver to return predictable names
    - _Requirements: 2.1–2.8, 8.5_

  - [ ]* 10.3 PBT — UserNameResolver batch resolution
    - Create test class for `UserNameResolver.ResolveNamesAsync`
    - Use FsCheck.Xunit `[Property(MaxTest = 100)]`
    - **Property: All input userIds appear in output** — *For any* list of userIds, the returned dictionary SHALL contain an entry for every non-null userId in the input (with "Unknown User" fallback for unresolved)
    - **Property: Null userId maps to "System"** — *For any* input set containing null, resolving null SHALL yield "System"
    - Use in-memory MembershipDbContext seeded with generated users
    - _Requirements: 3.1, 3.2, 3.3, 3.4_


## Notes

- The existing SuperAdmin Audit Log at `/Admin/Audit` (`AuditController`) is **already implemented and working** — do NOT modify or remove it.
- The existing `AuditLogQueryService`, `AuditLogQueryRepository`, `AuditLogFilter`, and `AuditInterceptor` are **already implemented** — reuse them as-is.
- The `audit_log` module key is already seeded in the database and recognized by `PortalModules.Audit`.
- No database migrations are needed — the `[audit].[AuditLog]` table and indexes are already in place.
- `PagedResult<T>` and `ServiceResult` already exist in `Portal.Infrastructure/Models/`.
- `PortalModules`, `AccessLevels`, and `ModuleAccessAttribute` are already implemented.
- The Activity Log uses a page size of 8 (not 20 like the admin audit view) to keep the timeline scannable.
- The "Status changed" filter requires post-filtering after the database query because it inspects JSON field names — this is acceptable for the small page sizes involved.
- Entity detail URL generation should gracefully handle unknown entity types by returning null (plain text rendering in UI).
- The `RelativeTimestampFormatter` is a static utility class to allow easy unit testing without DI — inject-ability is not needed.
- Tasks marked with `*` are optional property-based tests that can be skipped for faster delivery.
