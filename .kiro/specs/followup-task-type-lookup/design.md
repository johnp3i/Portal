# Design Document: Follow-Up Task Type Lookup

## Overview

Convert `[sales].[FollowUpTask].[TaskType]` from a free-text column (with values defined only in code) into a proper reference-table relationship, modelled exactly on the existing `[sales].[MeetingType]` lookup. Tasks reference the type via `FollowUpTaskTypeId` (TINYINT FK); the allowed values become seed rows in `[sales].[FollowUpTaskTypes]`; and every layer loads the options from that single source.

Two-phase, reversible rollout:
- **Phase 1 (this design):** add the table + `FollowUpTaskTypeId`, backfill, wire all code to the id, and keep the legacy `TaskType` string column written in sync.
- **Phase 2 (later):** drop `TaskType` once Phase 1 is verified.

### Reference implementation

`[sales].[MeetingType]` is the template to copy at every layer:
- **Table** (`137_CreateMeetingTypeTable.sql`): `Id INT NOT NULL` (non-identity) + `Name NVARCHAR(50)`, seeded with explicit ids via idempotent `IF NOT EXISTS` inserts.
- **Entity** (`Entities/Sales/MeetingType.cs`): `Id`, `Name`.
- **EF config** (`ConfigureMeetingType`): `ToTable("MeetingType","sales")`, `HasKey(Id)`, `Property(Id).ValueGeneratedNever()`, `Name` required max 50.
- **Repository** (`MeetingTypeRepository`): `GetAllAsync()` → `SELECT [Id],[Name]` via `ExecuteStoredProcedureUnfiltered`, ordered by `Name`.
- **FK usage** (`Meeting.MeetingTypeId` → `[sales].[MeetingType]([Id])`).
- **Lookup delivery** (`AxGetLookups` returns `meetingTypes = [{Id,Name}]`; JS builds dropdowns from it).

The one deliberate divergence: **`Id` is `TINYINT`** (per decision), since there are only 5 values. `MeetingType` uses `INT`; `FollowUpTaskTypes` uses `TINYINT` and the FK column matches.

## Architecture / Data Flow

```
[sales].[FollowUpTaskTypes]  (Id TINYINT PK, Name)   ← seed: Call, Email, Follow-up, Meeting Prep, Other
        ▲ FK
[sales].[FollowUpTask].FollowUpTaskTypeId (TINYINT)   (+ retained TaskType NVARCHAR during Phase 1)

FollowUpTaskTypeRepository.GetAllAsync ──► AxGetLookups (taskTypes) ──► JS dropdowns (create modal, Tasks filter, meeting-task form)
                                                                              │ submit FollowUpTaskTypeId
                                                                              ▼
CreateFollowUpTaskRequest.FollowUpTaskTypeId ─► FollowUpTaskService (validate id against lookup)
                                                     │
                                                     ▼
FollowUpTaskRepository.InsertAsync/UpdateAsync ─► writes FollowUpTaskTypeId (+ TaskType name in sync)
FollowUpTaskRepository SELECTs ─► join FollowUpTaskTypes ─► FollowUpTaskDto { FollowUpTaskTypeId, TaskTypeName }
```

## Components and Interfaces

### 1. Database

#### 1a. Migration 186 — create + seed `[sales].[FollowUpTaskTypes]`

```sql
USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTaskTypes')
BEGIN
    CREATE TABLE [sales].[FollowUpTaskTypes]
    (
        [Id]   TINYINT      NOT NULL,
        [Name] NVARCHAR(50) NOT NULL,
        CONSTRAINT [PK_FollowUpTaskTypes] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Idempotent seed, mirroring existing UI order
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 1)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id],[Name]) VALUES (1, 'Call');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 2)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id],[Name]) VALUES (2, 'Email');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 3)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id],[Name]) VALUES (3, 'Follow-up');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 4)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id],[Name]) VALUES (4, 'Meeting Prep');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 5)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id],[Name]) VALUES (5, 'Other');
GO
```

Seed ids follow the create-form option order (Call, Email, Follow-up, Meeting Prep, Other) per the "mirror existing order" decision. `Follow-up` = id 3, which the UI keeps as its default selection.

#### 1b. Migration 187 — add + backfill `FollowUpTaskTypeId`

```sql
USE [Portal]
GO

-- 1. Add nullable column (idempotent)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA='sales' AND TABLE_NAME='FollowUpTask' AND COLUMN_NAME='FollowUpTaskTypeId')
BEGIN
    ALTER TABLE [sales].[FollowUpTask] ADD [FollowUpTaskTypeId] TINYINT NULL;
END
GO

-- 2. Backfill by matching TaskType text to lookup Name; unmatched -> 'Other'
UPDATE [sales].[FollowUpTask]
   SET [FollowUpTaskTypeId] = COALESCE(
        (SELECT [Id] FROM [sales].[FollowUpTaskTypes]
          WHERE [Name] = [sales].[FollowUpTask].[TaskType]),
        (SELECT [Id] FROM [sales].[FollowUpTaskTypes] WHERE [Name] = 'Other'))
 WHERE [FollowUpTaskTypeId] IS NULL;
GO

-- 3. FK (idempotent)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_FollowUpTask_FollowUpTaskType'
                 AND parent_object_id = OBJECT_ID('[sales].[FollowUpTask]'))
BEGIN
    ALTER TABLE [sales].[FollowUpTask]
        ADD CONSTRAINT [FK_FollowUpTask_FollowUpTaskType]
            FOREIGN KEY ([FollowUpTaskTypeId]) REFERENCES [sales].[FollowUpTaskTypes]([Id]);
END
GO

-- 4. Enforce NOT NULL after backfill
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA='sales' AND TABLE_NAME='FollowUpTask'
             AND COLUMN_NAME='FollowUpTaskTypeId' AND IS_NULLABLE='YES')
BEGIN
    ALTER TABLE [sales].[FollowUpTask] ALTER COLUMN [FollowUpTaskTypeId] TINYINT NOT NULL;
END
GO
```

`TaskType` column is **retained** in Phase 1. Optional filtered index on `FollowUpTaskTypeId` is unnecessary (only 5 distinct values; existing `BusinessId+DueAtUtc` index covers list queries).

### 2. Entity + EF

#### `FollowUpTaskType` entity (new — mirrors `MeetingType`)

```csharp
namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: category of a follow-up task (Call, Email, Follow-up, Meeting Prep, Other).
/// Schema: [sales].[FollowUpTaskTypes]
/// </summary>
public class FollowUpTaskType
{
    public byte Id { get; set; }
    public string Name { get; set; } = null!;
}
```

`Id` is `byte` (maps to `TINYINT`).

#### `FollowUpTask` entity (extend)

Add:
```csharp
public byte FollowUpTaskTypeId { get; set; }
public FollowUpTaskType FollowUpTaskType { get; set; } = null!;
```
Keep the existing `public string TaskType { get; set; }` (Phase 1). Update its XML comment to note it is legacy/kept-in-sync.

#### EF config

`ConfigureFollowUpTaskTypes` (new, mirrors `ConfigureMeetingType`):
```csharp
modelBuilder.Entity<FollowUpTaskType>(entity =>
{
    entity.ToTable("FollowUpTaskTypes", "sales");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedNever();
    entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
});
```
Register the call alongside `ConfigureMeetingType`. In the existing `FollowUpTask` config, add:
```csharp
entity.HasOne(e => e.FollowUpTaskType)
      .WithMany()
      .HasForeignKey(e => e.FollowUpTaskTypeId)
      .OnDelete(DeleteBehavior.ClientSetNull);
```

### 3. Repositories

#### `FollowUpTaskTypeRepository` (new — mirrors `MeetingTypeRepository`)

```csharp
public class FollowUpTaskTypeRepository : GenericStoredProcedureRepository<FollowUpTaskType>
{
    public FollowUpTaskTypeRepository(DbContext context) : base(context) { }

    public async Task<List<FollowUpTaskType>> GetAllAsync()
    {
        try
        {
            const string query = @"SELECT [Id],[Name] FROM [sales].[FollowUpTaskTypes]";
            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.Id).ToList();
        }
        catch (Exception ex) { throw; }
    }
}
```
Ordered by `Id` to preserve the seeded/UI order (Call, Email, Follow-up, Meeting Prep, Other) — a deliberate refinement over MeetingType's order-by-Name, because task-type order is meaningful for the dropdown default.

#### `FollowUpTaskRepository` (extend)

- **INSERT**: add `[FollowUpTaskTypeId]` to the column list, VALUES, and params. During Phase 1 also continue writing `[TaskType]` — set it to the type's Name resolved from the entity (the service supplies both). Concretely, the entity carries `FollowUpTaskTypeId` (authoritative) and `TaskType` (name, kept in sync); the INSERT writes both.
- **UPDATE**: set `[FollowUpTaskTypeId]` (and `[TaskType]` = name during Phase 1).
- **SELECT** (all read queries — `GetByIdAsync`, today's-actions, lead-scoped, meeting-scoped, paged): add `[FollowUpTaskTypeId]` and LEFT JOIN `[sales].[FollowUpTaskTypes]` to project the type `Name` into the read model. Because these use `FromSqlRaw`/`ExecuteStoredProcedure` mapping to the `FollowUpTask` entity, the join name is surfaced by mapping it onto the retained `TaskType` field (still present) OR via a projected read model. Simplest during Phase 1: keep selecting `[TaskType]` (still populated) for the display name, and additionally select `[FollowUpTaskTypeId]`. This avoids restructuring the raw-SQL entity mapping. Post Phase 2 (when TaskType is dropped), the SELECTs switch to `JOIN ... FollowUpTaskTypes` and map the name.
- **Paged filter**: replace `AND [TaskType] = @TaskType` with `AND [FollowUpTaskTypeId] = @FollowUpTaskTypeId`.

> Phase-1 pragmatic note: since `TaskType` stays populated and in sync, read queries can keep reading `TaskType` for the display name with zero join churn; only writes and the filter switch to the id now. The joins are introduced in Phase 2 when the column is dropped. This keeps Phase 1 minimal and low-risk while still making the id authoritative and FK-enforced.

### 4. Service + DTOs

#### `FollowUpTaskService`

- Remove `private static readonly string[] ValidTaskTypes = {...}`.
- Inject `FollowUpTaskTypeRepository`. Validate the submitted `FollowUpTaskTypeId` by checking it exists in `GetAllAsync()` (cache the list per call). On invalid → `ServiceResult.Fail("Invalid task type.")`.
- On create/update: set entity `FollowUpTaskTypeId`, and set entity `TaskType` = the resolved type Name (keeps the retained column in sync).
- Map `FollowUpTaskTypeId` and `TaskTypeName` into the DTO. During Phase 1 `TaskTypeName` can be sourced from the retained `TaskType` column value.

#### DTOs (`FollowUpTaskDtos.cs`)

- `CreateFollowUpTaskRequest`: add `public byte FollowUpTaskTypeId { get; set; }`. Keep `TaskType` string optional/ignored during transition, or remove from the request now (JS will send the id). Recommendation: **replace** `TaskType` with `FollowUpTaskTypeId` on the request, since all callers are updated in this spec.
- `UpdateFollowUpTaskRequest` (or the update endpoint params): add `FollowUpTaskTypeId`.
- `FollowUpTaskDto`: add `FollowUpTaskTypeId` and keep/rename `TaskType` → expose `TaskTypeName` (retain `TaskType` property name if the views read `t.taskType`, to avoid churn — see UI section).

#### `MeetingService` task mapping

The meeting task list projection maps `TaskType`. Update it to also carry `FollowUpTaskTypeId`; the display name continues from the resolved name.

### 5. Controller + DI

- `AxGetLookups`: inject `FollowUpTaskTypeRepository`, add `taskTypes = types.Select(t => new { t.Id, t.Name })` to the returned `data`.
- `AxPostCreateTask`: request model now carries `FollowUpTaskTypeId`.
- Update-task endpoint: accept `FollowUpTaskTypeId`.
- `AxGetTasksPaged`: replace `string? taskType` parameter with `byte? followUpTaskTypeId`; pass through to the filter.
- `Program.cs`: register `FollowUpTaskTypeRepository` (scoped, mirroring `MeetingTypeRepository` registration).

### 6. UI

Three dropdowns currently hardcode the five options. All three load from `AxGetLookups().data.taskTypes` and submit the id.

- **`wwwroot/js/sales/follow-up-tasks.js`** (`openCreateTaskModal`): build the Type `<select>` from `taskTypes` (fetch lookups on modal open or reuse an already-loaded lookups cache); default-select `Follow-up` (id 3). `submitCreateTask` sends `followUpTaskTypeId` (parsed int). The task list badge (`getTypeBadgeHtml`) keeps keying colours by **name**, so the row render uses `t.taskType`/`t.taskTypeName`.
- **`Views/Sales/Tasks.cshtml`** (`#filterType`): populate options from lookups; the list-load JS sends `followUpTaskTypeId` instead of `taskType`.
- **`Views/Sales/Meetings.cshtml`** (`#meetingTaskType`): populate from lookups; `submitMeetingTask` sends `followUpTaskTypeId`.

Badge colours remain keyed by the type **name** (`Call`, `Email`, `Follow-up`, `Meeting Prep`, `Other`) so visuals are unchanged.

## Error Handling

- SQL migrations: idempotent guards; backfill COALESCE to `Other` guarantees no NULL before the NOT NULL step.
- Service: invalid `FollowUpTaskTypeId` → `ServiceResult.Fail`; repositories use `try/catch (Exception ex) { throw; }` per golden rules.
- Controller AJAX endpoints return `Json(new { success, message })`.

## Testing Strategy

- **Migration**: on a DB with existing tasks of each type + one bogus `TaskType`, verify every row gets the correct `FollowUpTaskTypeId` and the bogus one maps to `Other`; column becomes NOT NULL; FK present.
- **Create/Update**: creating a task with each type persists the correct `FollowUpTaskTypeId` and keeps `TaskType` name in sync; invalid id rejected.
- **Filter**: Tasks list filtered by each type id returns the right rows.
- **UI**: all three dropdowns list the five types, default `Follow-up`, badges render with correct colours.
- **Regression**: completion/outcome, scheduling, meeting linkage unaffected; build 0 errors.

## Migration & Rollout

1. Run migration 186 (table + seed).
2. Run migration 187 (column + backfill + FK + NOT NULL).
3. Deploy code (entity/EF, repositories, service, controller, DI, views/JS).
4. Verify in the running environment: creating, editing, filtering tasks; existing tasks show correct types.
5. **Phase 2 (separate spec/migration, after confirmation):** drop `[sales].[FollowUpTask].[TaskType]`, remove the entity property, and switch repository SELECTs to join `FollowUpTaskTypes` for the display name.

## Design Decisions & Rationale

- **Mirror `MeetingType`** (user decision): consistency with the established lookup pattern; low cognitive load.
- **`TINYINT` Id** (user decision): only 5 values; smallest sensible key. Entity uses `byte`.
- **Plural table name `FollowUpTaskTypes`** (user decision): note this differs from singular `MeetingType`; accepted intentionally.
- **Order by Id, seed in UI order** (user decision "mirror existing order"): dropdown shows Call, Email, Follow-up, Meeting Prep, Other; `Follow-up` (id 3) stays the create default.
- **Two-phase, keep `TaskType` synced** (user decision "add, confirm, then drop"): zero-downtime, reversible; read queries keep using the still-populated `TaskType` for display in Phase 1, minimising join churn until the column is dropped in Phase 2.
