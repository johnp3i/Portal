# Requirements — Prospects & Campaigns

## Introduction

3 Inventors researches potential customers ("prospects") in an Excel workbook, then works
through them with cold calls on a weekly rhythm. Today this lives entirely in a spreadsheet,
disconnected from the Portal.

This feature adds a **Prospects & Campaigns** module — a complementary sub-area **under
Sales / Opportunities** — that replaces the prospecting spreadsheet. It introduces a stage
**before** the existing Lead: a prospect is someone we intend to approach; it becomes a Sales
Contact and Lead only **after it shows interest**, via an explicit conversion.

The guiding lifecycle:

```
Campaign → Prospect → (activities) → Convert → Lead → Opportunity → Customer
```

### Why this is a separate module, not part of Leads

A researched-but-uncontacted company, or a "called, no answer," is prospecting evidence — it
must **not** enter the Sales pipeline or the Contacts registry. Keeping prospects separate
keeps the sales data clean while preserving the full acquisition history. Only a deliberate
**Convert to Lead** brings a prospect into the pipeline.

### Scope discipline (V1)

Build the smallest coherent module that lets 3 Inventors run the real Chaplin Pro Cyprus
campaign inside the Portal instead of a spreadsheet. Explicitly **out of V1**:

- The **Prospecting Assistant** (weekly-objective automation) — **backlogged**. V1 shows a
  static weekly-objective panel; the assistant is designed later, once the module is used and
  tested, so we know exactly what it should say.
- No AI scoring, automated email sequences, scraping, or marketing automation.
- No separate campaign-membership join table (a prospect belongs to one campaign in V1 — see
  design rationale). No pricing/tier change (rides the existing `sales` module gate).

## Glossary

- **Campaign** — a named prospecting push for one product, with a weekly cold-call target and
  a date range (e.g. "Chaplin Pro — Cyprus ICP Validation — Sep 2026").
- **Prospect** — a researched organisation/person we intend to approach, belonging to a
  campaign. Distinct from a Sales Contact; it is not in the pipeline.
- **Prospect Activity** — a timeline entry for a prospect (Research, Call, Email, Meeting,
  Note, etc.), recorded without creating any Lead.
- **Convert to Lead** — the explicit action that creates a Sales Contact + Lead from a prospect
  and records a back-reference for measurement.
- **Score** — the campaign qualification score: four dimensions (ICP Fit, Pain Probability,
  Accessibility, Learning Value), each 0–5, totalling 0–20, mapped to a Priority (A/B/Hold).
- **Sales Contact / Lead** — the existing `[sales].[Contact]` and `[sales].[LeadRequest]`
  entities; prospects are separate from both until conversion.

## Requirements

### Requirement 1: Campaign

**User Story:** As a sales operator, I want to create prospecting campaigns, so that each
outreach push has its own product focus, target, and prospect list.

#### Acceptance Criteria
1. THE system SHALL allow creating a Campaign with: Name (required), Product (references an
   existing `[sales].[Product]` / SalesProduct), Description/Objective, Market/Geography,
   Start Date, End Date (optional), Weekly Call Target (integer), Owner, Status, Notes.
2. THE Campaign Status SHALL be one of: `Draft`, `Active`, `Closed`.
3. THE system SHALL list campaigns for the current business with prospect count, weekly target,
   period, and status.
4. THE system SHALL scope every campaign to the current business (tenant isolation).
5. THE system SHALL record `CreatedAtUtc` on every campaign.

### Requirement 2: Prospect

**User Story:** As a sales operator, I want to hold researched prospects inside a campaign,
separate from Sales Contacts, so that uncontacted or unresponsive targets never pollute the
pipeline.

#### Acceptance Criteria
1. THE system SHALL allow a Prospect with: Name (required), Segment, Location, Business Type,
   Public Contact/Role, Phone, Email, Website, Public Evidence (research notes), Why-Fit
   rationale, Research Source URL, Recommended First Contact, Assigned user, Status, Next Action,
   Next Action Date.
2. THE Prospect Status SHALL be one of: `Research`, `Ready`, `Contacting`, `Engaged`,
   `Converted`, `Disqualified`.
3. A Prospect SHALL belong to exactly one Campaign (V1).
4. A Prospect SHALL be a distinct entity from `[sales].[Contact]` — creating/editing a Prospect
   SHALL NOT create or modify any Sales Contact or Lead.
5. THE Prospect's Phone/Email SHALL be stored as plain research fields, not as a Contact record.
6. THE system SHALL scope every prospect to the current business and record `CreatedAtUtc`.

### Requirement 3: Qualification Score

**User Story:** As a sales operator, I want a consistent qualification score per prospect, so
that I can prioritise who to call.

#### Acceptance Criteria
1. THE system SHALL store four score dimensions on the Prospect, each 0–5: ICP Fit, Pain
   Probability, Accessibility, Learning Value.
2. THE Total (0–20) SHALL be computed from the four dimensions, not entered manually.
3. THE Priority SHALL be derived from the Total: `A` = 16–20, `B` = 12–15, `Hold` = below 12.
4. WHEN any dimension is outside 0–5, THE system SHALL reject the value.

### Requirement 4: Prospect Activities

**User Story:** As a sales operator, I want to log calls/emails/notes against a prospect, so
that I keep the full outreach history without creating leads.

#### Acceptance Criteria
1. THE system SHALL record Prospect Activities with: Prospect, Activity Type, Date/Time, User,
   Outcome, Notes, Next Action, Next Action Date.
2. THE Activity Type SHALL be one of: `Research`, `Call`, `Email`, `Meeting`, `Demo`, `Note`,
   `Social`, `Other`.
3. Recording an activity SHALL NOT create any Lead or Sales Contact.
4. THE system SHALL display a prospect's activities as a timeline, most recent first.
5. THE activity history SHALL remain accessible after the prospect is converted.
6. Individual call attempts SHALL be modelled as activities, NOT as prospect statuses or
   dated columns (`Call1Date`, etc.).

### Requirement 5: Convert Prospect to Lead

**User Story:** As a sales operator, when a prospect shows real interest, I want to convert it
into a Lead with one action, so that it enters the existing pipeline and I can measure whether
the score predicted the conversion.

#### Acceptance Criteria
1. THE system SHALL provide an explicit **Convert to Lead** action on a prospect.
2. Conversion SHALL create a Sales Contact via the existing `IContactService.CreateContactAsync`
   flow (honouring its email/phone dedup rules), or reuse an existing matching contact.
3. Conversion SHALL create a Lead via the existing `ILeadRequestService.CreateLeadRequestAsync`
   flow, with `LeadSourceTypeId = Cold Call (4)` and the campaign's Product as `ProductId`.
4. THE created Lead SHALL carry relevant prospect context (e.g. request/notes) without
   duplicating unnecessary data.
5. THE Prospect SHALL be marked `Converted` and SHALL store a reference to the created Lead
   (`ConvertedLeadRequestId`) so predicted-score-vs-actual-conversion can be measured.
6. THE system SHALL prevent converting the same prospect twice (guard on already-converted).
7. WHEN the existing contact dedup blocks creation (duplicate email/phone), THE system SHALL
   surface that to the user and let them proceed by reusing the matched contact.

### Requirement 6: Excel Import

**User Story:** As a sales operator, I want to import the research workbook into a campaign, so
that I don't retype 30 prospects, and re-importing is safe.

#### Acceptance Criteria
1. THE system SHALL import the "30 Prospects" sheet of the Chaplin workbook, mapping its columns
   (Prospect, Segment, Location, Business Type, Public Contact/Role, Phone, Email, Website,
   Public Evidence, Why-Fit, the four scores, Total, Priority, Recommended First Contact,
   Research Source) to Prospect fields.
2. THE import SHALL ignore the "Campaign Summary" and "Weekly Call Tracker" sheets.
3. THE import SHALL validate each score is 0–5 and RECOMPUTE the Total, flagging (not silently
   trusting) any row whose file Total disagrees with the computed Total.
4. THE import SHALL derive/validate Priority from the computed Total.
5. THE import SHALL detect likely duplicate prospects within the campaign (by name + website/
   email) and skip them rather than create duplicates.
6. THE import SHALL produce a result summary: created / matched-skipped / needs-review / invalid.
7. THE import SHALL be safe to re-run — re-importing the same workbook SHALL NOT create
   duplicate prospects.
8. THE import SHALL enforce file-type (.xlsx) and size limits consistent with existing imports.

### Requirement 7: Working List, Detail & Campaign Dashboard

**User Story:** As a sales operator, I want an operational list and a campaign dashboard, so
that I can work prospects on the weekly rhythm and see the funnel.

#### Acceptance Criteria
1. THE prospect list SHALL show: Prospect, Segment, Priority, Score, Status, Last Activity,
   Next Action Date, Assigned; with filters (Priority, Status, Segment, Next Action/overdue),
   and row actions (Open, Add Activity, Convert to Lead, Disqualify).
2. THE prospect detail SHALL show identity/research, the four-dimension score with computed
   total and priority, status, and the activity timeline; with a prominent Convert to Lead.
3. THE campaign dashboard SHALL show the funnel counts: Prospects → Ready → Contacting →
   Engaged → Converted.
4. THE campaign dashboard SHALL include a **static** "This week's objective" panel (weekly call
   target, Priority-A ready count, follow-ups due) marked as a placeholder for the future
   Prospecting Assistant. No automation logic is built in V1.
5. THE list SHALL make explicit (label/notice) that prospects are not Sales Contacts.

### Requirement 8: Module Placement, Gating & Conventions

**User Story:** As the platform owner, I want prospecting to live inside Sales under the same
access rules and conventions, so that it fits the platform.

#### Acceptance Criteria
1. THE module SHALL be gated by the existing `[ModuleAccess(PortalModules.Sales)]` (Professional+);
   no new module key or pricing change.
2. THE module SHALL appear as a **"Prospecting"** sub-item under the existing "Opportunities"
   sidebar section.
3. ALL new tables SHALL be in the `[sales]` schema, tenant-scoped by `BusinessId`, with
   `CreatedAtUtc DATETIME NOT NULL DEFAULT (GETUTCDATE())`, following the repository/service/
   AJAX (`AxGet`/`AxPost` + `Json(new{success,message})`) conventions already used in Sales.
4. New migrations SHALL start at **210** (current highest is 209) and follow the SQL header/
   idempotency conventions.

### Requirement 9: Data Integrity & Measurement

**User Story:** As the platform owner, I want the prospect→lead link preserved, so that I can
later evaluate whether the qualification scoring actually predicts conversions.

#### Acceptance Criteria
1. A converted Prospect SHALL retain `ConvertedLeadRequestId` (and campaign attribution) so a
   report can compare prospect Priority/Score against real conversion outcomes.
2. Conversion SHALL not alter existing Lead/Opportunity/Contact semantics.
3. THE module SHALL maintain tenant isolation and the existing authorisation conventions
   throughout.
