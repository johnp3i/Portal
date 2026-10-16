# Requirements Document: Prospect Activity View

## Introduction

Prospecting work is logged as a stream of activities (calls, emails, meetings, notes, follow-ups)
against individual prospects inside campaigns. Today there is no way to answer a simple operational
question: **"What did we do in prospecting today?"** — either overall or for one campaign. The only
activity surfaces that exist are a single prospect's timeline (on the prospect detail) and a small,
capped "recent activity" widget on the campaign dashboard. Neither is date-filterable, neither spans
all campaigns, and neither can be reviewed for an arbitrary period.

The **Prospect Activity View** is a dedicated, paged, filterable log of prospect activities. It lets
an operator review what was done across prospecting in any period, filter by campaign, city, activity
type and interaction signal, search free text, and see a per-period summary (counts by type). It
reuses the privacy-safe "next action" presentation already built for the working list — the time and
muted contact/phone icon badges, with the actual name/number never exposed in the list.

Mockup: `.kiro/docs/mockups/prospect-activity-view.html`

## Glossary

- **Prospect Activity**: A timeline entry on a prospect — row in `[sales].[ProspectActivity]`. Types:
  1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note, 7 Social, 8 Other.
- **Interaction signal / Sentiment**: Optional signal on an activity — 1 Positive, 2 Neutral, 3 Negative.
- **Overall view**: Activities across ALL campaigns for the business (the "All campaigns" scope).
- **Campaign view**: Activities for the prospects of a single campaign.
- **Occurred date**: `ProspectActivity.OccurredAtUtc` — when the activity actually happened. All date
  filtering and ordering use this (NOT `CreatedAtUtc`, which is when it was recorded).
- **City**: The prospect's `Location` free-text field (e.g. "Nicosia"). Used as a coarse geography filter.
- **Period summary**: Aggregate counts for the filtered set — total, by type, follow-ups set, positive/negative.

## Requirements

### Requirement 1: Dedicated Prospect Activity Page

**User Story:** As a business operator, I want a dedicated page listing prospect activities, so that I
can review what prospecting work was done without opening prospects one by one.

#### Acceptance Criteria

1. THE Prospecting module SHALL provide a `GET /Prospecting/Activity` page gated by the same module
   access as the rest of Prospecting.
2. THE page SHALL be reachable from the Prospecting Campaigns landing page via a visible entry point
   (e.g. a "View Activity" action in the topbar).
3. THE page SHALL be reachable from a campaign's dashboard, pre-filtered to that campaign, via a
   visible entry point (e.g. a "View Activity" action), passing `?campaignId={id}`.
4. THE page SHALL follow the existing filter-card + data-table layout conventions (topbar, a
   `.glass.card-pad` filter card with `margin-bottom:22px`, then a `.glass.card-pad` table card).

### Requirement 2: Scope — Overall and Per-Campaign

**User Story:** As an operator, I want to see activity across all campaigns or for a single campaign,
so that I can review prospecting both at a glance and focused.

#### Acceptance Criteria

1. THE page SHALL provide a Campaign filter with an "All campaigns" option (default) and one option
   per campaign belonging to the business.
2. WHEN "All campaigns" is selected, THE view SHALL show activities for prospects across all of the
   business's campaigns (the overall view).
3. WHEN a specific campaign is selected, THE view SHALL show only activities for that campaign's
   prospects.
4. WHEN the page is opened with `?campaignId={id}`, THE Campaign filter SHALL default to that campaign.
5. ALL queries SHALL be tenant-scoped to the current business; a user SHALL never see another
   business's activities or campaigns.

### Requirement 3: Date / Period Filtering

**User Story:** As an operator, I want to filter activity by date or a quick period, so that I can
answer "what did we do today / this week / in this range".

#### Acceptance Criteria

1. THE filter card SHALL provide Date-from and Date-to inputs.
2. THE filter card SHALL provide quick-period chips: Today, Yesterday, This week, Last 7 days,
   This month, All time.
3. THE default period on first load (no query parameters) SHALL be **Today**.
4. ALL date filtering SHALL be against `OccurredAtUtc` (the occurred date), inclusive of both bounds
   for the whole local day(s) selected.
5. WHEN "All time" is selected, THE view SHALL apply no date bound.
6. THE activity log SHALL be ordered by `OccurredAtUtc` descending (most recent first), tie-broken by
   `Id` descending.

### Requirement 4: Additional Filters

**User Story:** As an operator, I want to narrow activity by city, type, signal and free text, so that
I can focus on what matters.

#### Acceptance Criteria

1. THE filter card SHALL provide a City filter populated with the distinct non-empty `Location` values
   of the business's prospects (within the selected campaign scope), plus an "All cities" default.
2. THE filter card SHALL provide an Activity-type filter: All types, Call, Email, Meeting, Demo, Note,
   Research, Social, Other.
3. THE filter card SHALL provide an Interaction-signal filter: Any, Positive, Neutral, Negative.
4. THE filter card SHALL provide a free-text Search that matches (case-insensitive, contains) the
   prospect name, the activity Outcome, the Notes, or the NextAction text.
5. THE filter card SHALL provide "Filter" and "Clear" actions; Clear SHALL reset all filters to their
   defaults (All campaigns, All cities, All types, Any signal, empty search) and the Today period.
6. Filters SHALL be combinable (AND semantics) and SHALL all narrow the same result set and summary.

### Requirement 5: Activity Log Table

**User Story:** As an operator, I want each activity shown with enough context to understand it, so
that I can review work without opening each prospect.

#### Acceptance Criteria

1. EACH row SHALL show: When (occurred date, plus time when a time-of-day is meaningful), Prospect
   (name linking to `/Prospecting/ProspectDetail/{id}`, with city and segment as a sub-line), Campaign
   (shown in the overall view; may be omitted or de-emphasised in single-campaign view), Type (pill),
   Outcome (text, with a follow-up flag when `IsFollowUp`), Signal (Positive/Neutral/Negative pill when
   set), Next action, and By (who logged it).
2. THE Next action cell SHALL reuse the working-list presentation: the next-action text, the
   `NextActionDate` and `NextActionTime` when present, and muted person/phone icon badges when a
   contact name / phone is attached to the activity's follow-up.
3. THE actual next-action contact name and phone number SHALL NOT be sent to the browser for this list
   — only presence flags — consistent with the working-list privacy rule. The icon tooltips SHALL be
   generic ("Contact name on file" / "Phone number on file").
4. WHEN the result set is empty for the current filters, THE table SHALL show an empty state within the
   table card (e.g. "No prospect activity for these filters.").
5. THE "By" column SHALL display the name of the user who performed the activity
   (`PerformedByUserId`), or a neutral fallback (e.g. "—") when unresolved.

### Requirement 6: Period Summary

**User Story:** As an operator, I want a quick summary of the filtered period, so that I can answer
"how much did we do" at a glance.

#### Acceptance Criteria

1. ABOVE the table, THE page SHALL show a summary strip for the CURRENT filtered set (same filters as
   the table, ignoring pagination).
2. THE summary SHALL include: total activities, counts for Calls, Emails and Meetings, number of
   activities that set a follow-up (`IsFollowUp = 1`), and a positive / negative signal count.
3. THE summary figures SHALL update to reflect every applied filter (campaign, date, city, type,
   signal, search).

### Requirement 7: Pagination

**User Story:** As an operator, I want the log paginated, so that large periods stay fast and readable.

#### Acceptance Criteria

1. THE activity log SHALL be paginated at a fixed page size (15 rows), consistent with other list views.
2. THE table card SHALL show "Showing X–Y of N" and Previous / Next controls.
3. Changing any filter SHALL reset to page 1.

### Requirement 8: Export (optional, if included)

**User Story:** As an operator, I want to export the filtered activity, so that I can share or archive
a period's prospecting work.

#### Acceptance Criteria

1. IF export is provided, THE page SHALL offer an "Export CSV" action that exports the CURRENT filtered
   set (all matching rows, not just the current page), tenant-scoped.
2. THE CSV SHALL include non-sensitive columns only; it SHALL NOT include the next-action contact name
   or phone number (consistent with the on-screen privacy rule). (Export is a nice-to-have; it MAY be
   deferred without blocking the rest of the feature.)

### Requirement 9: Performance & Consistency

**User Story:** As a developer, I want the view to be efficient and consistent with existing patterns,
so that it scales and stays maintainable.

#### Acceptance Criteria

1. THE activity list SHALL be produced by a single paged, filtered query joined to the prospect for
   name/city/segment/campaign — no N+1 per-row queries.
2. THE AJAX endpoints SHALL follow the `AxGet...` naming and the project's BlockUI → fetch →
   (SweetAlert2 on error) convention; a successful list load MAY render without a SweetAlert.
3. THE feature SHALL NOT alter existing prospect, campaign, or activity write paths; it is read-only
   over existing data.
