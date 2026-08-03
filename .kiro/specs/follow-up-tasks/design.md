# Design Document: Follow-Up Tasks

## Overview

Follow-Up Tasks add a lightweight "action item" system to the Sales Pipeline that answers one question: **"Who should I contact today, and about what?"** Unlike meetings (which represent scheduled calendar events with duration and location), tasks are quick reminders with a due date, type, and assignee.

Tasks live in the `[sales]` schema alongside existing sales entities (LeadRequest, Meeting, SalesContact, TeamMember) and are integrated into the Pipeline page via a "Today's Actions" panel, the Lead Detail page as a section, and a standalone Tasks list view accessible from navigation.

The feature operates within the existing `sales` module access — no new module key is required.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| `[sales]` schema (not new schema) | Tasks are tightly coupled to leads/contacts — same bounded context as meetings |
| TaskType as NVARCHAR (not lookup table) | Only 5 fixed values (Call, Email, Follow-up, Meeting Prep, Other) — lookup table would be over-engineering |
| Soft-complete (not delete) | Completed tasks remain visible on Lead Detail for activity history |
| SnoozedCount tracking | Surfaces "this keeps slipping" visual warnings after 3 snoozes |
| LeadRequestId nullable | Tasks can exist independently (e.g., "Call John about renewal" without a lead) |
| ContactId nullable | Not every task has a specific contact (e.g., "Review Q4 proposals") |
| No email notifications in v1 | Keeps scope tight — in-app surfacing via Pipeline panel only |
| Batch count queries for KPI | Same pattern as attachment counts — avoids N+1 on list views |

---

## Architecture

```mermaid
graph TD
    subgraph "Portal.Web"
        A[SalesController — Task endpoints] --> B[IFollowUpTaskService]
        C[Pipeline.cshtml — Today's Actions panel] -->|AJAX| A
        D[LeadDetail.cshtml — Tasks section] -->|AJAX| A
        E[Tasks.cshtml — Full list view] -->|AJAX| A
    end

    subgraph "Portal.Infrastructure"
        B --> F[FollowUpTaskRepository]
        B --> G[ICurrentTenantService]
        F --> H[(SQL Server<br/>[sales].[FollowUpTask])]
    end

    subgraph "Existing Infrastructure"
        A --> I[ModuleAccess — Sales]
        G --> J[ClaimsIdentity → BusinessId]
    end
```

### Request Flow

1. User interacts with UI (creates task from Lead Detail, completes/snoozes from Today's Actions panel)
2. AJAX request hits `SalesController` (already protected by `[ModuleAccess(PortalModules.Sales)]`)
3. Controller resolves `BusinessId` from `ICurrentTenantService` and `UserId` from claims
4. `FollowUpTaskService` orchestrates validation + CRUD via `FollowUpTaskRepository`
5. JSON response returned; UI updates in-place without page reload

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| `SalesController` (existing) | New AJAX endpoints: AxPostCreateTask, AxPostCompleteTask, AxPostSnoozeTask, AxGetTodaysActions, AxGetTasksByLead |
| `IFollowUpTaskService` / `FollowUpTaskService` | Business logic: validation, snooze limit warnings, due date computation from presets |
| `FollowUpTaskRepository` | SQL CRUD on `[sales].[FollowUpTask]` — today's actions query, lead-scoped query, paged list |
| Pipeline.cshtml (Today's Actions) | Collapsible panel above Kanban, loaded via AJAX on page load |
| LeadDetail.cshtml (Tasks section) | New section between Meetings and Linked Documents |
| Tasks.cshtml | Standalone filterable list view |

---

## Components and Interfaces

### 1. FollowUpTask Entity

**Location:** `Portal.Infrastructure/Entities/Sales/FollowUpTask.cs`

```csharp
public class FollowUpTask
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? LeadRequestId { get; set; }
    public int? ContactId { get; set; }
    public int? TeamMemberId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!; // Call, Email, Follow-up, Meeting Prep, Other
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int SnoozedCount { get; set; }
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
    public LeadRequest? LeadRequest { get; set; }
    public SalesContact? Contact { get; set; }
    public TeamMember? TeamMember { get; set; }
}
```

### 2. IFollowUpTaskService

**Location:** `Portal.Infrastructure/Services/Sales/IFollowUpTaskService.cs`

```csharp
public interface IFollowUpTaskService
{
    Task<ServiceResult> CreateTaskAsync(CreateFollowUpTaskRequest request);
    Task<ServiceResult> CompleteTaskAsync(int taskId);
    Task<ServiceResult> SnoozeTaskAsync(int taskId, DateTime newDueDate);
    Task<List<FollowUpTaskDto>> GetTodaysActionsAsync(int? teamMemberId = null);
    Task<List<FollowUpTaskDto>> GetByLeadIdAsync(int leadRequestId);
    Task<PagedResult<FollowUpTaskDto>> GetTasksPagedAsync(FollowUpTaskFilter filter, int page, int pageSize);
    Task<int> GetOverdueCountAsync(int? teamMemberId = null);
}
```

### 3. FollowUpTaskRepository

**Location:** `Portal.Infrastructure/Repositories/Sales/FollowUpTaskRepository.cs`

Extends `GenericStoredProcedureRepository<FollowUpTask>`. Key methods:

| Method | Description |
|--------|-------------|
| `InsertAsync(FollowUpTask)` | INSERT, returns new Id |
| `CompleteAsync(int id, int businessId)` | UPDATE SET IsCompleted=1, CompletedAtUtc=GETUTCDATE() |
| `SnoozeAsync(int id, int businessId, DateTime newDue)` | UPDATE SET DueAtUtc, SnoozedCount += 1 |
| `GetTodaysActionsAsync(int businessId, int? teamMemberId)` | WHERE IsCompleted=0 AND DueAtUtc <= tomorrow, ordered by DueAtUtc ASC |
| `GetByLeadIdAsync(int businessId, int leadRequestId)` | All tasks for a lead, pending first then completed |
| `GetOverdueCountAsync(int businessId, int? teamMemberId)` | COUNT WHERE IsCompleted=0 AND DueAtUtc < today |

### 4. DTOs and Request Models

**Location:** `Portal.Infrastructure/Models/Sales/FollowUpTaskDto.cs`

```csharp
public class FollowUpTaskDto
{
    public int Id { get; set; }
    public int? LeadRequestId { get; set; }
    public string? ContactName { get; set; }
    public string? LeadProductName { get; set; }
    public string? AssignedToName { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int SnoozedCount { get; set; }
    public string Urgency { get; set; } = null!; // "overdue", "today", "tomorrow", "upcoming"
}

public class CreateFollowUpTaskRequest
{
    public int? LeadRequestId { get; set; }
    public int? ContactId { get; set; }
    public int? TeamMemberId { get; set; }
    public string Title { get; set; } = null!;
    public string TaskType { get; set; } = null!;
    public DateTime DueAtUtc { get; set; }
    public string? Notes { get; set; }
}

public class FollowUpTaskFilter
{
    public string? Status { get; set; } // pending, completed, overdue
    public string? TaskType { get; set; }
    public int? TeamMemberId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
```

### 5. Controller Endpoints (added to SalesController)

```csharp
// AxPostCreateTask(CreateFollowUpTaskRequest request)
// AxPostCompleteTask(int id)
// AxPostSnoozeTask(int id, DateTime newDueDate)
// AxGetTodaysActions(int? teamMemberId)
// AxGetTasksByLead(int leadRequestId)
// AxGetTasksPaged(string? status, string? taskType, int? teamMemberId, DateTime? dateFrom, DateTime? dateTo, int page = 1)
```

All follow existing SalesController patterns: `[ValidateAntiForgeryToken]` on POSTs, `try/catch (Exception ex)`, `Json(new { success, message, data })`.

---

## Data Models

### FollowUpTask Table

```sql
USE [Portal]
GO

CREATE TABLE [sales].[FollowUpTask]
(
    [Id]                INT             IDENTITY(1,1)   NOT NULL,
    [BusinessId]        INT                             NOT NULL,
    [LeadRequestId]     INT                             NULL,
    [ContactId]         INT                             NULL,
    [TeamMemberId]      INT                             NULL,
    [Title]             NVARCHAR(200)                   NOT NULL,
    [TaskType]          NVARCHAR(50)                    NOT NULL,
    [DueAtUtc]          DATETIME                        NOT NULL,
    [Notes]             NVARCHAR(500)                   NULL,
    [IsCompleted]       BIT                             NOT NULL  CONSTRAINT [DF_FollowUpTask_IsCompleted] DEFAULT (0),
    [CompletedAtUtc]    DATETIME                        NULL,
    [SnoozedCount]      INT                             NOT NULL  CONSTRAINT [DF_FollowUpTask_SnoozedCount] DEFAULT (0),
    [CreatedByUserId]   NVARCHAR(450)                   NOT NULL,
    [CreatedAtUtc]      DATETIME                        NOT NULL  CONSTRAINT [DF_FollowUpTask_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_FollowUpTask] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_FollowUpTask_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
    CONSTRAINT [FK_FollowUpTask_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest] ([Id]),
    CONSTRAINT [FK_FollowUpTask_Contact] FOREIGN KEY ([ContactId]) REFERENCES [sales].[SalesContact] ([Id]),
    CONSTRAINT [FK_FollowUpTask_TeamMember] FOREIGN KEY ([TeamMemberId]) REFERENCES [sales].[TeamMember] ([Id])
);
GO

-- Primary query: "What's due today?" — sorted by urgency
CREATE NONCLUSTERED INDEX [IX_FollowUpTask_BusinessId_DueAtUtc]
    ON [sales].[FollowUpTask] ([BusinessId], [DueAtUtc])
    INCLUDE ([TeamMemberId], [IsCompleted])
    WHERE [IsCompleted] = 0;
GO

-- Lead-scoped lookups
CREATE NONCLUSTERED INDEX [IX_FollowUpTask_LeadRequestId]
    ON [sales].[FollowUpTask] ([LeadRequestId])
    WHERE [IsCompleted] = 0;
GO
```

---

## UI Design

### Today's Actions Panel (Pipeline Page)

Positioned **above** the filter panel on the Pipeline page. Collapsible via a toggle. Loaded via AJAX on page load.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ⚡ Today's Actions                                    3 due today, 1 overdue │
│  ─────────────────────────────────────────────────────────────────────────── │
│  🔴  Call Maria Petrou (Acme Corp) — Proposal follow-up       Due: Yesterday │
│       [✓ Complete]  [⏩ Snooze ▾]                                            │
│  🟡  Email follow-up to Nexus Digital — Pricing query           Due: Today   │
│       [✓ Complete]  [⏩ Snooze ▾]                                            │
│  🟡  Send revised quotation to Costa Enterprises                Due: Today   │
│       [✓ Complete]  [⏩ Snooze ▾]                                            │
│  ⚪  Call John (Marina Bay) re: onboarding pack               Due: Tomorrow  │
│       [✓ Complete]  [⏩ Snooze ▾]                                            │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Visual indicators:**
- Red dot + "Overdue" badge: `DueAtUtc < today AND IsCompleted = 0`
- Amber dot + "Today" badge: `DueAtUtc = today AND IsCompleted = 0`
- Grey dot + "Tomorrow": `DueAtUtc = tomorrow AND IsCompleted = 0`
- 3+ snoozes: dashed amber border around the task card ("keeps slipping" indicator)

### Quick Creation Modal (Lead Detail)

Compact modal triggered by "Schedule Follow-up" button on Lead Detail:

```
┌──────────────────────────────────────────────┐
│  Schedule Follow-up                      [X] │
│  ─────────────────────────────────────────── │
│  Title: [Follow up — Maria Petrou       ]    │
│  Type:  [Email ▾]                            │
│  Due:   [Tomorrow] [In 3 days] [Next week]   │
│         [📅 Custom date picker]              │
│  Assign to: [Me ▾]                           │
│  Notes: [                              ]     │
│                                              │
│         [Cancel]  [Create Task]              │
└──────────────────────────────────────────────┘
```

Quick preset buttons immediately set the date and highlight. No extra clicks needed.

### Lead Detail — Tasks Section

Placed between the "Meetings" section and "Linked Documents" section:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Follow-Up Tasks (3)                          [+ Schedule Follow-up]        │
│  ─────────────────────────────────────────── ──────────────────────────────  │
│  🟡  Email follow-up — pricing query              Due: 15 Jul 2026          │
│       Assigned to: Constantinos  [✓ Complete]  [⏩ Snooze ▾]               │
│  🔴  Call back — contract questions               Due: 10 Jul 2026 (overdue)│
│       Assigned to: Me  [✓ Complete]  [⏩ Snooze ▾]                         │
│  ─────────────────────────────────────────── ──────────────────────────────  │
│  ✓  Send introductory email                       Completed: 08 Jul 2026    │
│  ✓  Call to confirm receipt                       Completed: 06 Jul 2026    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Tasks List Page (Sales/Tasks navigation item)

Standard table view with filters:

| Column | Description |
|--------|-------------|
| Title | Task title (linked to lead if associated) |
| Contact | Contact name |
| Type | Call / Email / Follow-up / Meeting Prep / Other |
| Due Date | Formatted, with urgency colour |
| Status | Overdue (red) / Today (amber) / Upcoming / Completed |
| Assigned To | Team member name |
| Actions | Complete, Snooze, View Lead |

Filter panel: Status dropdown, Type dropdown, Team Member dropdown, Date range.

---

## Navigation Integration

Add "Tasks" as a sub-item under "Opportunities" in the side navigation, positioned after "Meetings":

```
Opportunities
├── Lead Board
├── Contacts
├── Products & Services
├── Meetings
├── Tasks          ← NEW (with overdue badge count)
├── Templates
└── Team
```

The "Tasks" nav item displays a red badge with the overdue count when > 0.

---

## AJAX Interaction Patterns

All interactions follow existing `BlockUI.show() → fetch → BlockUI.hide() → Swal.fire()` pattern:

**Create Task:**
```javascript
BlockUI.show('Creating task...');
fetch('/Sales/AxPostCreateTask', { method: 'POST', headers: { 'RequestVerificationToken': token }, body: JSON.stringify(payload) })
→ BlockUI.hide() → Swal.fire(success) → refresh task list
```

**Complete Task:**
```javascript
BlockUI.show('Completing...');
fetch('/Sales/AxPostCompleteTask?id=' + taskId, { method: 'POST', headers: { 'RequestVerificationToken': token } })
→ BlockUI.hide() → remove task card from DOM (no Swal needed — quick operation)
```

**Snooze Task:**
```javascript
// Snooze is quick, no SweetAlert result needed
BlockUI.show('Snoozing...');
fetch('/Sales/AxPostSnoozeTask?id=' + taskId + '&newDueDate=' + date, { method: 'POST', ... })
→ BlockUI.hide() → update card due date in DOM
```

---

## Error Handling

| Scenario | Response | HTTP Status |
|----------|----------|-------------|
| Title is empty or > 200 chars | `{ success: false, message: "Title is required (max 200 characters)." }` | 200 (JSON) |
| Invalid TaskType | `{ success: false, message: "Invalid task type." }` | 200 (JSON) |
| DueAtUtc is in the past (create) | Allowed — user might be recording a missed task |
| Task not found / wrong business | `{ success: false, message: "Task not found." }` | 200 (JSON) |
| Task already completed | `{ success: false, message: "Task is already completed." }` | 200 (JSON) |
| Lead not found (if LeadRequestId provided) | `{ success: false, message: "The associated lead was not found." }` | 200 (JSON) |

---

## Dashboard Integration

The main Dashboard briefing card (if it exists) will include a signal:

> "You have **3 overdue** follow-up tasks"

This uses `IFollowUpTaskService.GetOverdueCountAsync()` — a single COUNT query, no performance concern.

---

## Testing Strategy

### Unit Tests
- Service validation: empty title, invalid type, duplicate completion
- Snooze count increment logic
- Due date preset calculation (tomorrow, +3 days, next Monday)
- Urgency classification: overdue vs today vs tomorrow vs upcoming

### Integration Tests
- Create → Complete cycle
- Create → Snooze → verify new DueAtUtc and SnoozedCount
- Tenant isolation: Business A can't see Business B's tasks
- Today's Actions query returns correct urgency classification

### Manual Verification
- Quick preset buttons set correct dates
- Collapsible panel persists state
- Snooze dropdown positioning on mobile
- Badge count updates in real-time after complete/snooze
