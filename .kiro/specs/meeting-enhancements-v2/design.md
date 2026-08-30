# Design Document

## Overview

Two additive enhancements to the Sales Meetings module:

1. **Meeting Task Time Picker** — Change the meeting task due date input from `type="date"` to `type="datetime-local"` so users can specify both date and time. The backend `ScheduledTimeUtc` column already exists on `[sales].[FollowUpTask]` — only the meeting task form needs updating.

2. **Meeting Outcome Classification** — Add a structured classification dropdown (Positive, Neutral, Negative, Rescheduled, No Show) alongside the existing free-text Outcome field. New lookup table + FK on Meeting. Classification pills on the Meetings list page + new filter.

Mockup locked at: `Portal.Web/wwwroot/mockups/meeting-enhancements-v2.html`

---

## Feature 1: Meeting Task Time Picker

### Current State

- Meeting task inline form in `Meetings.cshtml` uses `<input type="date" id="meetingTaskDueDate" />`
- `submitMeetingTask()` in `meetings.js` sends `dueAtUtc` as a date string (e.g., "2026-08-28")
- The `CreateFollowUpTaskRequest` model has both `DueAtUtc` (DateTime) and `ScheduledTimeUtc` (TimeOnly?) properties
- The meeting task form does NOT send `scheduledTimeUtc` — it's always null for meeting-created tasks
- The standalone Tasks page (`/Sales/Tasks`) already supports time via a separate time input

### Design

#### View Change (Meetings.cshtml)

Replace:
```html
<div class="field"><label style="font-size:12px;">Due Date *</label><input type="date" id="meetingTaskDueDate" /></div>
```

With:
```html
<div class="field"><label style="font-size:12px;">Due Date & Time *</label><input type="datetime-local" id="meetingTaskDueDate" /></div>
```

#### JS Change (meetings.js — `submitMeetingTask`)

Current payload:
```javascript
dueAtUtc: dueDate   // "2026-08-28"
```

Updated payload — parse the datetime-local value into date + time:
```javascript
var dtVal = document.getElementById('meetingTaskDueDate').value; // "2026-08-28T09:00"
var dtObj = new Date(dtVal);
var dateOnly = dtVal.substring(0, 10); // "2026-08-28"
var hours = dtObj.getHours();
var minutes = dtObj.getMinutes();
var hasTime = (hours !== 0 || minutes !== 0);

// payload fields:
dueAtUtc: dateOnly,
scheduledTimeUtc: hasTime ? String(hours).padStart(2, '0') + ':' + String(minutes).padStart(2, '0') : null
```

The `CreateFollowUpTaskRequest` already accepts `ScheduledTimeUtc` as `TimeOnly?`. The service and repository already handle it. No backend changes needed.

#### DTO Change (MeetingTaskBriefDto)

The `MeetingTaskBriefDto` (returned inside `MeetingDetailDto.Tasks` by `AxGetMeetingDetail`) currently does NOT include `ScheduledTimeUtc`. Add:
```csharp
public TimeOnly? ScheduledTimeUtc { get; set; }
```

Update the mapping in `MeetingService.GetByIdAsync` (or wherever `MeetingTaskBriefDto` is populated) to include `ScheduledTimeUtc` from the `FollowUpTask` entity.

#### JS Change (meetings.js — `renderMeetingTasks`)

Update the task list rendering to show time when present:
- If task has `scheduledTimeUtc`: display "27 Aug 2026, 10:00"
- If task has no `scheduledTimeUtc`: display "27 Aug 2026"

The `AxGetMeetingDetail` endpoint already returns `scheduledTimeUtc` on task DTOs (it's part of `FollowUpTaskDto`).

**Note:** The `MeetingTaskBriefDto` currently lacks `ScheduledTimeUtc` — task 1.4 adds this property and updates the service mapping.

#### Design Decision: Midnight Ambiguity

If the user selects `datetime-local` with time 00:00, we treat it as "no time specified" (all-day task) and send `scheduledTimeUtc: null`. A genuine midnight task is an edge case unlikely in business operations. This is documented and accepted.

#### JS Change (meetings.js — `hideMeetingTaskForm`)

Update the reset to clear the datetime-local input: `document.getElementById('meetingTaskDueDate').value = '';`

No change needed — the same line works for both `type="date"` and `type="datetime-local"`.

### Files Affected

| File | Change |
|------|--------|
| `Portal.Web/Views/Sales/Meetings.cshtml` | Change `type="date"` to `type="datetime-local"`, update label |
| `Portal.Web/wwwroot/js/sales/meetings.js` | Parse datetime in `submitMeetingTask`, update task display in `renderMeetingTasks` |

### No Database Changes

The `ScheduledTimeUtc TIME(0) NULL` column already exists on `[sales].[FollowUpTask]`. The repository `InsertAsync` already maps it. No migration needed.

---

## Feature 2: Meeting Outcome Classification

### Data Model

#### New Lookup Table: `[sales].[MeetingOutcomeClassification]`

```
[Id]            INT NOT NULL (PK, IDENTITY)
[Name]          NVARCHAR(50) NOT NULL
[CreatedAtUtc]  DATETIME NOT NULL DEFAULT GETUTCDATE()
```

Seed data:
| Id | Name |
|----|------|
| 1 | Positive |
| 2 | Neutral |
| 3 | Negative |
| 4 | Rescheduled |
| 5 | No Show |

This is a static reference table — exempt from `CreatedAtUtc` audit requirement per the SQL schema design steering (lookup tables with static seed data).

#### Altered Table: `[sales].[Meeting]`

Add column:
```
[MeetingOutcomeClassificationId] INT NULL
    CONSTRAINT [FK_Meeting_MeetingOutcomeClassification]
    FOREIGN KEY REFERENCES [sales].[MeetingOutcomeClassification]([Id])
```

Nullable — existing meetings remain unclassified. No backfill required.

### Entity Changes

#### New Entity: `MeetingOutcomeClassification`

```csharp
// Portal.Infrastructure/Entities/Sales/MeetingOutcomeClassification.cs
public class MeetingOutcomeClassification
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
```

#### Updated Entity: `Meeting`

Add property:
```csharp
public int? MeetingOutcomeClassificationId { get; set; }
```

### Repository Changes

#### MeetingRepository

- Update ALL `SELECT` queries to include `[MeetingOutcomeClassificationId]` in the column list
- The `UpdateAsync` method must include the new column in the `UPDATE SET` clause
- The `GetPagedAsync` method already accepts `MeetingFilter` — add the `OutcomeClassificationId` filter handling: when `MeetingFilter.OutcomeClassificationId` has a value, append `AND [MeetingOutcomeClassificationId] = @OutcomeClassificationId` to the WHERE clause

### Service Changes

#### MeetingService

- `GetMeetingsPagedAsync`: The classification filter comes through `MeetingFilter.OutcomeClassificationId` (already passed to repository). Resolve `OutcomeClassificationName` when mapping to `MeetingPagedListDto` using a static dictionary: `{ 1: "Positive", 2: "Neutral", 3: "Negative", 4: "Rescheduled", 5: "No Show" }`
- `UpdateMeetingAsync` / `AxPostUpdateMeeting`: Accept and persist `meetingOutcomeClassificationId` from the `UpdateMeetingRequest` model.
- `GetByIdAsync`: Map `MeetingOutcomeClassificationId` to the `MeetingDetailDto` response for the edit modal.

#### DTO Changes

- `MeetingPagedListDto`: Add `int? MeetingOutcomeClassificationId` and `string? OutcomeClassificationName`
- `MeetingDetailDto` (returned by `AxGetMeetingDetail`): Add `int? MeetingOutcomeClassificationId`
- `MeetingFilter`: Add `int? OutcomeClassificationId` to the filter model (consistent with existing Status/MeetingTypeId pattern)
- Update meeting request model: Add `int? MeetingOutcomeClassificationId` to `UpdateMeetingRequest`

### Controller Changes

#### SalesController

- `AxGetMeetingsPaged`: Accept `int? outcomeClassificationId` query parameter, populate `MeetingFilter.OutcomeClassificationId`, pass to service
- `AxPostUpdateMeeting`: Already accepts `[FromBody] UpdateMeetingRequest` — the new `MeetingOutcomeClassificationId` property flows through automatically
- `AxGetMeetingDetail`: Already returns `MeetingDetailDto` — the new property flows through after DTO + service mapping updates

### View Changes

#### Meetings.cshtml — Edit Modal

Add Classification dropdown between Notes and Outcome textarea:
```html
<div class="field">
    <label>Classification</label>
    <select id="editMeetingClassification">
        <option value="">— Select outcome —</option>
        <option value="1">Positive</option>
        <option value="2">Neutral</option>
        <option value="3">Negative</option>
        <option value="4">Rescheduled</option>
        <option value="5">No Show</option>
    </select>
</div>
```

Options are hardcoded (5 static values, no DB fetch needed).

#### Meetings.cshtml — Filter Panel

Add Classification dropdown to the filter bar:
```html
<div class="field" style="min-width:160px;">
    <label>Classification</label>
    <select id="filterOutcomeClassification">
        <option value="">All</option>
        <option value="1">Positive</option>
        <option value="2">Neutral</option>
        <option value="3">Negative</option>
        <option value="4">Rescheduled</option>
        <option value="5">No Show</option>
    </select>
</div>
```

### JS Changes (meetings.js)

#### `openEditMeetingModal`
Pre-select the classification dropdown from `m.meetingOutcomeClassificationId`:
```javascript
document.getElementById('editMeetingClassification').value = m.meetingOutcomeClassificationId || '';
```

#### `submitEditMeeting`
Add to payload:
```javascript
meetingOutcomeClassificationId: parseInt(document.getElementById('editMeetingClassification').value) || null
```

#### `renderMeetingsTable`
Add Classification column with coloured pill:
```javascript
function getClassificationPillHtml(classificationName) {
    if (!classificationName) return '<span style="color:#8a9bab;">—</span>';
    var colors = {
        'Positive': { bg: 'rgba(18,152,103,.08)', color: '#129867' },
        'Neutral': { bg: 'rgba(13,94,166,.08)', color: '#0D5EA6' },
        'Negative': { bg: 'rgba(194,74,74,.08)', color: '#C24A4A' },
        'Rescheduled': { bg: 'rgba(200,145,46,.08)', color: '#C8912E' },
        'No Show': { bg: 'rgba(194,74,74,.08)', color: '#C24A4A' }
    };
    var c = colors[classificationName] || { bg: 'rgba(94,115,133,.08)', color: '#5E7385' };
    return '<span style="display:inline-flex;align-items:center;padding:3px 10px;border-radius:8px;font-size:11px;font-weight:700;background:' + c.bg + ';color:' + c.color + ';">' + classificationName + '</span>';
}
```

#### `loadMeetingsPage`
Include `outcomeClassificationId` in the query params from the filter dropdown.

#### `clearMeetingFilters`
Reset the Classification filter dropdown to empty.

### Table Column Order (Meetings List)

Updated columns: Subject, Type, Contact, Scheduled, Duration, **Classification**, Outcome, Status, Actions

The thead `<th>Classification</th>` is inserted between Duration and Outcome.

### Pill Colour Mapping

| Classification | Background | Text Colour |
|---|---|---|
| Positive | `rgba(18,152,103,.08)` | `#129867` |
| Neutral | `rgba(13,94,166,.08)` | `#0D5EA6` |
| Negative | `rgba(194,74,74,.08)` | `#C24A4A` |
| Rescheduled | `rgba(200,145,46,.08)` | `#C8912E` |
| No Show | `rgba(194,74,74,.08)` | `#C24A4A` |

---

## Files Summary

| File | Feature | Change Type |
|------|---------|-------------|
| `Portal.Database/Migrations/XXX_CreateMeetingOutcomeClassification.sql` | Classification | New migration |
| `Portal.Infrastructure/Entities/Sales/MeetingOutcomeClassification.cs` | Classification | New entity |
| `Portal.Infrastructure/Entities/Sales/Meeting.cs` | Classification | Add property |
| `Portal.Infrastructure/Models/Sales/MeetingPagedListDto` (in relevant DTO file) | Classification | Add properties |
| `Portal.Infrastructure/Repositories/Sales/MeetingRepository.cs` | Classification | Update SELECTs, UPDATE, GetPagedAsync filter |
| `Portal.Infrastructure/Services/Sales/MeetingService.cs` | Classification | Map classification, accept filter |
| `Portal.Web/Controllers/SalesController.cs` | Classification | Accept filter + update payload |
| `Portal.Web/Views/Sales/Meetings.cshtml` | Both | Add dropdown to edit modal + filter bar, change task input type |
| `Portal.Web/wwwroot/js/sales/meetings.js` | Both | Parse task datetime, render classification pills, filter, pre-select |
