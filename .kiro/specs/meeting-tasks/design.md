# Design Document: Meeting Tasks

## Overview

Meeting Tasks add a direct link between `FollowUpTask` and `Meeting`, enabling users to create, view, and complete tasks from within the meeting context. The implementation adds a nullable `MeetingId` FK to the existing `[sales].[FollowUpTask]` table, extends the meeting detail endpoint to include linked tasks, and enhances the Meetings page UI with a tasks section inside the Edit Meeting modal and a task count badge on meeting rows.

This follows the existing nullable FK pattern already used by `LeadRequestId` and `ContactId` on FollowUpTask — no new tables, no new entities, no new service classes. The change is additive and fully backward-compatible.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Nullable FK on FollowUpTask (not a join table) | Follows the existing `LeadRequestId`/`ContactId` pattern. One-to-many is sufficient — a task originates from one meeting. Avoids join table complexity. |
| Tasks section inside Edit Meeting modal (not a new detail page) | Keeps the UX consistent — users already use the edit modal for meeting interaction. The tasks section is lightweight enough to fit. |
| Batch task counts via a single query (not per-meeting lookups) | The meetings paged list needs task counts for badges. A single `GROUP BY MeetingId` query avoids N+1. Same pattern as attachment counts elsewhere. |
| Auto-populate ContactId and LeadRequestId from meeting | Reduces friction — when creating a task from meeting context, the user only needs to enter title, type, and due date. |
| No separate MeetingTask entity | Tasks are structurally identical to FollowUpTask. A separate entity would duplicate the entire task workflow (completion, snooze, overdue tracking). |
| MeetingSubject on FollowUpTaskDto (not a navigation lookup) | The meeting subject is fetched via a batch query at the service level and flattened into the DTO. No EF navigation needed at runtime. |

## Architecture

```mermaid
flowchart TD
    subgraph Browser
        A[Meetings.cshtml] --> B[meetings.js]
        B -->|fetch| C[/Sales/AxGetMeetingDetail]
        B -->|fetch| D[/Sales/AxPostCreateTask]
        B -->|fetch| E[/Sales/AxPostCompleteTask]
        B -->|fetch| F[/Sales/AxGetMeetingsPaged]
    end

    subgraph Controller
        C --> G[SalesController.AxGetMeetingDetail]
        D --> H[SalesController.AxPostCreateTask]
        E --> I[SalesController.AxPostCompleteTask]
        F --> J[SalesController.AxGetMeetingsPaged]
    end

    subgraph Service
        G --> K[MeetingService.GetByIdAsync]
        H --> L[FollowUpTaskService.CreateTaskAsync]
        I --> M[FollowUpTaskService.CompleteTaskAsync]
        J --> N[MeetingService.GetMeetingsPagedAsync]
    end

    subgraph Repository
        K --> O[FollowUpTaskRepository.GetByMeetingIdAsync]
        L --> P[FollowUpTaskRepository.InsertAsync]
        N --> Q[FollowUpTaskRepository.GetTaskCountsByMeetingIdsAsync]
    end

    subgraph Database
        O --> R[(sales.FollowUpTask)]
        P --> R
        Q --> R
    end
```

### Request Flows

**Create task from meeting context:**
1. User opens Edit Meeting modal → modal loads meeting detail including linked tasks
2. User clicks "Add Task" → inline form appears with pre-filled context
3. User enters title, type, due date → submits
4. `AxPostCreateTask` receives request with `MeetingId`, `ContactId`, `LeadRequestId` set
5. Task is created; activity feed entry recorded if lead is linked
6. JS re-fetches meeting tasks and updates the tasks section inline

**View task count badges on meeting list:**
1. `AxGetMeetingsPaged` returns meeting list DTOs
2. Service batch-fetches task counts for all meeting IDs on the current page via `GetTaskCountsByMeetingIdsAsync`
3. Each `MeetingPagedListDto` includes `TaskCount` and `PendingTaskCount`
4. JS renders badge in meeting row

## Components and Interfaces

### Database Migration

**Migration 178: Add MeetingId to FollowUpTask**

```sql
USE [Portal]
GO

ALTER TABLE [sales].[FollowUpTask]
    ADD [MeetingId] INT NULL
    CONSTRAINT [FK_FollowUpTask_Meeting] FOREIGN KEY ([MeetingId])
        REFERENCES [sales].[Meeting] ([Id]);
GO

CREATE NONCLUSTERED INDEX [IX_FollowUpTask_MeetingId]
    ON [sales].[FollowUpTask] ([MeetingId])
    WHERE [MeetingId] IS NOT NULL;
GO
```

### Entity Changes

**FollowUpTask.cs — Add MeetingId property and navigation:**

```csharp
public int? MeetingId { get; set; }
public Meeting? Meeting { get; set; }
```

**Meeting.cs — Add Tasks navigation collection:**

```csharp
public ICollection<FollowUpTask> Tasks { get; set; } = new List<FollowUpTask>();
```

### EF Core Configuration

Add to `ConfigureFollowUpTask` in `PortalDbContext.cs`:

```csharp
entity.HasOne(e => e.Meeting)
    .WithMany(m => m.Tasks)
    .HasForeignKey(e => e.MeetingId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.NoAction);
```

### Repository Layer

**New method: `FollowUpTaskRepository.GetByMeetingIdAsync`**

```csharp
public async Task<List<FollowUpTask>> GetByMeetingIdAsync(int meetingId, int businessId)
```

- Queries `[sales].[FollowUpTask]` WHERE `[MeetingId] = @MeetingId AND [BusinessId] = @BusinessId`
- Orders: pending first (`[IsCompleted] ASC`), then by `[DueAtUtc] ASC` for pending, `[CompletedAtUtc] DESC` for completed
- Uses full table names, `catch (Exception ex) { throw; }`

**New method: `FollowUpTaskRepository.GetTaskCountsByMeetingIdsAsync`**

```csharp
public async Task<Dictionary<int, (int Total, int Pending)>> GetTaskCountsByMeetingIdsAsync(
    IEnumerable<int> meetingIds, int businessId)
```

- Single query: `SELECT [MeetingId], COUNT(*) AS Total, SUM(CASE WHEN [IsCompleted] = 0 THEN 1 ELSE 0 END) AS Pending FROM [sales].[FollowUpTask] WHERE [MeetingId] IN (...) AND [BusinessId] = @BusinessId GROUP BY [MeetingId]`
- Returns a dictionary keyed by MeetingId
- Uses parameterised IN clause built from the meeting IDs list
- Returns empty dictionary if input is empty

**Modified method: `FollowUpTaskRepository.InsertAsync`**

- Add `[MeetingId]` to the INSERT column list and VALUES list
- Add `new SqlParameter("@MeetingId", entity.MeetingId ?? (object)DBNull.Value)` to the parameter set

**Modified: All existing SELECT queries in FollowUpTaskRepository**

The repository has 4 methods (`GetByIdAsync`, `GetByLeadRequestIdAsync`, `GetDashboardBriefAsync`, `GetPagedAsync`) that use explicit column lists. All must add `[MeetingId]` to the SELECT list — otherwise `entity.MeetingId` will always be null for tasks fetched through these methods, even if the database column has a value.

### Service Layer

**Modified: `MeetingService.GetByIdAsync`**

After fetching product requests and opportunities, also fetch tasks:

```csharp
var tasks = await _followUpTaskRepository.GetByMeetingIdAsync(id, businessId);
```

Map to `MeetingTaskBriefDto` list and include in the returned `MeetingDetailDto`.

**Modified: `MeetingService.GetMeetingsPagedAsync`**

After fetching the paged meeting list, batch-fetch task counts:

```csharp
var meetingIds = items.Select(m => m.Id);
var taskCounts = await _followUpTaskRepository.GetTaskCountsByMeetingIdsAsync(meetingIds, businessId);
```

Set `TaskCount` and `PendingTaskCount` on each `MeetingPagedListDto`.

**Modified: `FollowUpTaskService.MapToDto`**

Accept an additional `Dictionary<int, string> meetingSubjectsLookup` parameter. When the entity has a `MeetingId` and the lookup contains a match, set `MeetingSubject` on the DTO.

**Modified: `FollowUpTaskService.GetTasksPagedAsync`, `GetTodaysActionsAsync`, and `GetByLeadIdAsync`**

After fetching tasks, collect distinct non-null `MeetingId` values, batch-fetch meeting subjects via `_meetingRepository.GetSubjectsByIdsAsync(meetingIds, businessId)`, and pass the lookup to `MapToDto`.

### Dependency Injection Changes

Two cross-service dependencies are introduced:

1. **`MeetingRepository` → `FollowUpTaskService`**: Required for batch-fetching meeting subjects when enriching task DTOs. The service currently only depends on `FollowUpTaskRepository`, `SalesContactRepository`, and `ICurrentTenantService`.

2. **`FollowUpTaskRepository` → `MeetingService`**: Required for fetching tasks by meeting ID (meeting detail) and task counts (meetings paged list). The service currently depends on `MeetingRepository`, `MeetingProductRequestRepository`, `MeetingOpportunityRepository`, `SalesContactRepository`, `SalesProductRepository`, `MeetingTypeRepository`, `ILeadRequestService`, and `ICurrentTenantService`.

Both are repository-level dependencies (not service-to-service), which avoids circular dependency issues.

### New DTO

**`MeetingTaskBriefDto`** — Lightweight task representation for the meeting detail response:

```csharp
public class MeetingTaskBriefDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? TaskOutcome { get; set; }
}
```

### Modified DTOs

**`MeetingDetailDto`** — Add Tasks collection:

```csharp
public List<MeetingTaskBriefDto> Tasks { get; set; } = new();
```

**`MeetingPagedListDto`** — Add task count fields:

```csharp
public int TaskCount { get; set; }
public int PendingTaskCount { get; set; }
```

**`CreateFollowUpTaskRequest`** — Add MeetingId:

```csharp
public int? MeetingId { get; set; }
```

**`FollowUpTaskDto`** — Add meeting reference:

```csharp
public int? MeetingId { get; set; }
public string? MeetingSubject { get; set; }
```

### New Repository Method

**`MeetingRepository.GetSubjectsByIdsAsync`**

```csharp
public async Task<Dictionary<int, string>> GetSubjectsByIdsAsync(IEnumerable<int> ids, int businessId)
```

- Fetches `Id` and `Subject` from `[sales].[Meeting]` WHERE `[Id] IN (...) AND [BusinessId] = @BusinessId`
- Returns dictionary keyed by Meeting.Id, value is Subject
- Used for enriching task DTOs with meeting subjects without N+1

### Controller Layer

**No new endpoints required.** The existing endpoints are reused:

| Endpoint | Role in Meeting Tasks |
|----------|----------------------|
| `AxGetMeetingDetail` | Returns meeting detail including `Tasks` collection (new field) — satisfies Requirement 3.5 (tasks by MeetingId) as part of the detail payload |
| `AxPostCreateTask` | Creates task with optional `MeetingId` (new field in request) |
| `AxPostCompleteTask` | Completes task (unchanged) |
| `AxGetMeetingsPaged` | Returns meeting list with `TaskCount` and `PendingTaskCount` (new fields) |

The `AxPostCreateTask` controller method already calls `RecordActivityAsync` when `LeadRequestId` is present. The activity description will be updated to include the meeting subject when `MeetingId` is provided:

```csharp
if (result.Success && request.LeadRequestId.HasValue)
{
    var description = request.MeetingId.HasValue
        ? $"Follow-up task created from meeting: {meetingSubject}"
        : $"Follow-up task created: {request.Title}";
    await RecordActivityAsync(request.LeadRequestId.Value, "task_created", description);
}
```

To resolve `meetingSubject`, fetch it from `_meetingService.GetByIdAsync` only when `MeetingId` is provided.

## Data Models

### Modified Table: `[sales].[FollowUpTask]`

| Column | Type | Change |
|--------|------|--------|
| MeetingId | INT NULL | **NEW** — FK to `[sales].[Meeting]([Id])` |

All existing columns remain unchanged.

### Index

```sql
CREATE NONCLUSTERED INDEX [IX_FollowUpTask_MeetingId]
    ON [sales].[FollowUpTask] ([MeetingId])
    WHERE [MeetingId] IS NOT NULL;
```

Filtered index — only indexes rows with a non-null MeetingId, keeping the index small and focused.

## UI Design

### Meeting Tasks Section (Edit Meeting Modal)

Positioned below the Outcome textarea, above the modal action buttons:

```
┌─────────────────────────────────────────────────────────────┐
│  Edit Meeting                                          [X]  │
│  ───────────────────────────────────────────────────────── │
│  Contact: Maria Petrou (read-only)                          │
│  Subject: [Guardian Platform Demo                     ]     │
│  Meeting Type: [On-Site ▾]                                  │
│  Date & Time: [2026-08-25T10:00]  Duration: [60] min        │
│  Location: [3 Inventors HQ                            ]     │
│  Notes: [Initial demo of Guardian platform            ]     │
│  Outcome: [Interested in portal — requested demo access]    │
│                                                             │
│  ─── Meeting Tasks (2) ──────────────────── [+ Add Task] ──│
│  ☐  Send demo account credentials    Email     Due: 28 Aug  │
│  ☐  Schedule technical deep-dive     Follow-up Due: 02 Sep  │
│  ─────────────────────────────────────────────────────────  │
│  ✓  Share product brochure           Email     27 Aug       │
│                                                             │
│              [Cancel]  [Save Changes]                       │
└─────────────────────────────────────────────────────────────┘
```

**Inline task creation form** (appears when "Add Task" is clicked):

```
│  ─── New Task ──────────────────────────────────────────── │
│  Title: [                                             ]     │
│  Type: [Follow-up ▾]     Due: [2026-09-01]                  │
│  Notes: [                                             ]     │
│                              [Cancel]  [Create Task]        │
│  ──────────────────────────────────────────────────────── │
```

### Task Count Badge on Meeting Row

In the meetings table, a small badge appears in the Subject column next to the meeting title:

```
Guardian Platform Demo  [2 tasks]     On-Site    Maria Petrou    25 Aug 2026 ...
Pricing Discussion      [1 task ✓]    Video      John Costa      22 Aug 2026 ...
Introduction Call                     Phone      Anna Pavlou     20 Aug 2026 ...
```

- Blue badge (`[2 tasks]`): has pending tasks
- Green/muted badge (`[1 task ✓]`): all tasks completed
- No badge: no linked tasks

### Meeting Reference on Task Views

In the Tasks list page and Today's Actions panel, tasks with a meeting reference show a subtle label:

```
☐  Send demo account credentials         Email     Due: 28 Aug
   from: Guardian Platform Demo
```

Styled as `font-size:11px; color:#8a9bab; margin-top:2px;` — same as the relative time label pattern.

## AJAX Interaction Patterns

**Create task from meeting context:**
```javascript
BlockUI.show('Creating task...');
fetch('/Sales/AxPostCreateTask', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
    body: JSON.stringify({
        meetingId: meetingId,
        contactId: meetingContactId,
        leadRequestId: meetingLeadRequestId,
        title: title,
        taskType: taskType,
        dueAtUtc: dueDate,
        notes: notes
    })
})
→ BlockUI.hide() → Swal.fire(success) → refresh task list in modal
```

**Complete task from meeting modal:**
```javascript
BlockUI.show('Completing...');
fetch('/Sales/AxPostCompleteTask?id=' + taskId, {
    method: 'POST',
    headers: { 'RequestVerificationToken': getAntiForgeryToken() }
})
→ BlockUI.hide() → update task item in DOM (move to completed, mute style) — no Swal needed
```

## Correctness Properties

### Property 1: MeetingId FK integrity

*For any* FollowUpTask with a non-null MeetingId, the referenced Meeting must exist in `[sales].[Meeting]` and belong to the same BusinessId. The FK constraint at the database level guarantees referential integrity.

**Validates: Requirements 1.1, 1.2**

### Property 2: Backward compatibility

*For any* FollowUpTask where MeetingId is null, all existing task operations (create, complete, snooze, reopen, update, paged list, today's actions) produce identical results to the pre-migration behaviour. The nullable column has no impact on existing workflows.

**Validates: Requirement 1.6**

### Property 3: Auto-population correctness

*For any* task created via the meeting tasks inline form, the task's ContactId equals the meeting's ContactId and the task's LeadRequestId equals the meeting's LeadRequestId. This is enforced by the JS form which reads these values from the meeting detail response.

**Validates: Requirements 2.5, 2.6**

### Property 4: Task count consistency

*For any* meeting displayed in the paged list, the TaskCount equals the actual count of FollowUpTask rows where MeetingId = that meeting's Id, and PendingTaskCount equals the count where additionally IsCompleted = 0. This is guaranteed by the batch COUNT/SUM query.

**Validates: Requirements 5.2, 5.3, 5.4, 5.5**

### Property 5: Task ordering in meeting detail

*For any* meeting detail response, the Tasks list orders pending tasks (IsCompleted = false) before completed tasks (IsCompleted = true), with pending tasks sorted by DueAtUtc ascending and completed tasks sorted by CompletedAtUtc descending.

**Validates: Requirements 3.3, 7.4**

## Error Handling

| Layer | Error Scenario | Handling |
|-------|---------------|----------|
| Repository | SQL exception in GetByMeetingIdAsync | `catch (Exception ex) { throw; }` — propagates to service/controller |
| Repository | SQL exception in GetTaskCountsByMeetingIdsAsync | `catch (Exception ex) { throw; }` — returns empty task counts, meetings still render |
| Service | Meeting not found when resolving subject for activity log | Graceful fallback — uses task title instead of meeting subject |
| Controller (AxPostCreateTask) | MeetingId provided but meeting doesn't exist | FK violation caught, return `{ success: false, message: "The referenced meeting was not found." }` |
| JavaScript (inline task form) | Empty title or missing due date | `Swal.fire` with warning icon, prevent submission |
| JavaScript (complete task) | Network error | `BlockUI.hide()`, `Swal.fire` error |
| JavaScript (task list fetch) | Empty results | Show "No tasks yet." placeholder |

## Testing Strategy

### Unit Tests

- Service: CreateTaskAsync with MeetingId set — verify entity MeetingId is passed to repository
- Service: MapToDto with meeting subject lookup — verify MeetingSubject is populated when MeetingId is present
- Service: MapToDto without meeting — verify MeetingSubject is null
- Service: GetMeetingsPagedAsync with task counts — verify TaskCount and PendingTaskCount are set correctly

### Integration Tests

- Create task with MeetingId → GetByMeetingIdAsync returns the task
- Create task without MeetingId → GetByMeetingIdAsync returns empty for that meeting
- GetTaskCountsByMeetingIdsAsync with mix of meetings (some with tasks, some without) → correct counts
- Tenant isolation: Business A's tasks don't appear in Business B's meeting detail

### Manual Verification

- Edit Meeting modal shows tasks section with correct task list
- Inline task creation form creates task and updates list without closing modal
- Complete button moves task to completed section
- Task count badge appears on meeting rows with correct count
- Meeting reference label shows on Tasks list page for meeting-originated tasks
- Backward compatibility: existing tasks without MeetingId display and function unchanged
