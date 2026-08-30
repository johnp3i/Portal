# Requirements Document

## Introduction

This spec covers two targeted enhancements to the Sales Meetings module:

1. **Meeting Task Time Picker** — The meeting task creation form currently uses `type="date"` for the due date, which only captures the date without a time component. The backend already supports `ScheduledTimeUtc` (TimeOnly?) on `FollowUpTask`, but the meeting task form doesn't expose it. This enhancement changes the due date input to `type="datetime-local"` so users can specify both date and time when creating tasks from a meeting.

2. **Meeting Outcome Classification** — Meeting outcomes are currently free-text only (`Outcome` field on Meeting). This enhancement adds a structured `MeetingOutcomeClassification` dropdown alongside the existing free-text Outcome textarea, enabling filtering and reporting on meeting results. The free-text Outcome field is preserved for descriptive notes.

No changes to existing data — both features are additive.

## Glossary

- **Meeting_Task**: A `FollowUpTask` record linked to a meeting via `MeetingId` FK, created from the meeting edit modal's inline task form
- **Meeting_Outcome_Classification**: A new lookup value classifying the overall result of a meeting (Positive, Neutral, Negative, Rescheduled, No Show)
- **ScheduledTimeUtc**: An existing `TIME(0)` column on `[sales].[FollowUpTask]` that stores the optional time-of-day component. Currently populated by the standalone Tasks page but not by the meeting task form

## Requirements

### Requirement 1: Meeting Task Due Date with Time

**User Story:** As a sales team member, I want to set a specific time when creating a task from a meeting, so that I can schedule follow-up calls and emails at precise times rather than just assigning them to a day.

#### Acceptance Criteria

1. THE meeting task creation form (inline form inside the meeting edit modal) SHALL replace the current `type="date"` input for Due Date with a `type="datetime-local"` input
2. WHEN the user creates a meeting task with a datetime value, THE system SHALL parse the datetime and store the date portion in `DueAtUtc` and the time portion in `ScheduledTimeUtc` on the `FollowUpTask` entity
3. WHEN the user creates a meeting task without specifying a time (leaves the time at 00:00 or the browser default), THE system SHALL store `ScheduledTimeUtc` as NULL (all-day task behaviour preserved)
4. THE meeting task list rendered below the creation form SHALL display the time alongside the due date when `ScheduledTimeUtc` is not null (e.g., "27 Aug 2026, 10:00")
5. THE meeting task list SHALL display only the date when `ScheduledTimeUtc` is null (e.g., "27 Aug 2026")
6. THE label for the input SHALL change from "Due Date *" to "Due Date & Time *" to reflect the combined input
7. THE existing standalone Tasks page (`/Sales/Tasks`) task creation flow is NOT affected by this change — it already handles `ScheduledTimeUtc` separately

### Requirement 2: Meeting Outcome Classification Lookup

**User Story:** As a business operator, I want to classify meeting outcomes with a structured type (Positive, Neutral, Negative, Rescheduled, No Show), so that I can filter meetings by result and track conversion patterns across my sales pipeline.

#### Acceptance Criteria

1. THE Portal database SHALL contain a `[sales].[MeetingOutcomeClassification]` lookup table with columns: Id (INT, PK, identity), Name (NVARCHAR(50), NOT NULL), CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE())
2. THE Portal database SHALL seed the `[sales].[MeetingOutcomeClassification]` table with values: (1, 'Positive'), (2, 'Neutral'), (3, 'Negative'), (4, 'Rescheduled'), (5, 'No Show')
3. THE Portal database SHALL add a nullable `MeetingOutcomeClassificationId` (INT, NULL, FK to `[sales].[MeetingOutcomeClassification]`) column to the existing `[sales].[Meeting]` table
4. THE meeting edit modal SHALL display a "Classification" dropdown above the existing Outcome textarea, with options: (empty/unselected), Positive, Neutral, Negative, Rescheduled, No Show
5. THE Classification dropdown SHALL be optional — the user can save a meeting without selecting a classification
6. THE existing free-text Outcome textarea SHALL remain unchanged — classification and free-text work together (classification for filtering, free-text for description)
7. WHEN the user saves a meeting with a classification selected, THE system SHALL store the `MeetingOutcomeClassificationId` on the Meeting record
8. WHEN a meeting is loaded for editing, THE Classification dropdown SHALL be pre-selected with the current value (or empty if null)
9. THE Meetings list page (`/Sales/Meetings`) SHALL display the classification as a coloured pill next to the Outcome column when set
10. THE Meetings list page filter panel SHALL include an "Outcome" dropdown filter with options: All, Positive, Neutral, Negative, Rescheduled, No Show — filtering meetings by their `MeetingOutcomeClassificationId`
11. THE classification pill colours SHALL follow: Positive → green (#129867), Neutral → blue (#0D5EA6), Negative → red (#C24A4A), Rescheduled → amber (#C8912E), No Show → red (#C24A4A)
12. THE Meeting entity and DTOs SHALL include the new `MeetingOutcomeClassificationId` property and the resolved `OutcomeClassificationName` string for display
