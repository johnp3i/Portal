# Implementation Plan: Follow-Up Task Type Lookup

## Overview

Replace the free-text `[sales].[FollowUpTask].[TaskType]` column with a `[sales].[FollowUpTaskTypes]` reference table (TINYINT Id), mirroring `[sales].[MeetingType]`. Tasks reference the type via `FollowUpTaskTypeId` FK. Phase 1 (this plan) adds the table, backfills, wires all code to the id, and keeps `TaskType` synced. Phase 2 (later, after confirmation) drops `TaskType`.

## Tasks

- [ ] 1. Migrations
  - [ ] 1.1 Migration 186 — create + seed `[sales].[FollowUpTaskTypes]`
    - `USE [Portal]`; idempotent `IF NOT EXISTS`
    - Columns: `Id TINYINT NOT NULL` (PK, non-identity), `Name NVARCHAR(50) NOT NULL`
    - Seed (idempotent, in order): 1 Call, 2 Email, 3 Follow-up, 4 Meeting Prep, 5 Other
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ] 1.2 Migration 187 — add + backfill `FollowUpTaskTypeId` on `[sales].[FollowUpTask]`
    - Add nullable `FollowUpTaskTypeId TINYINT` (idempotent)
    - Backfill by matching `TaskType` name to lookup; unmatched → `Other`
    - Add FK `FK_FollowUpTask_FollowUpTaskType` (idempotent)
    - Set column `NOT NULL` after backfill
    - Retain `TaskType` column
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [ ] 2. Entity + EF
  - [ ] 2.1 Create `FollowUpTaskType` entity (`byte Id`, `string Name`)
    - _Requirements: 3.1_

  - [ ] 2.2 Add `FollowUpTaskTypeId` (byte) + `FollowUpTaskType` nav to `FollowUpTask` entity; keep `TaskType`
    - _Requirements: 3.3, 3.5_

  - [ ] 2.3 Add `ConfigureFollowUpTaskTypes` (ToTable/HasKey/ValueGeneratedNever/Name) and wire the call; add FK mapping in `FollowUpTask` config
    - _Requirements: 3.2, 3.4_

- [ ] 3. Repositories
  - [ ] 3.1 Create `FollowUpTaskTypeRepository.GetAllAsync()` (mirror `MeetingTypeRepository`, order by Id)
    - _Requirements: 4.1_

  - [ ] 3.2 Update `FollowUpTaskRepository` INSERT — write `FollowUpTaskTypeId` (+ keep `TaskType` name in sync)
    - _Requirements: 4.2, 4.3_

  - [ ] 3.3 Update `FollowUpTaskRepository` UPDATE — set `FollowUpTaskTypeId` (+ `TaskType` name)
    - _Requirements: 4.3, 4.4_

  - [ ] 3.4 Update `FollowUpTaskRepository` SELECTs to include `FollowUpTaskTypeId`; keep reading `TaskType` for display name (Phase 1)
    - _Requirements: 4.5_

  - [ ] 3.5 Update paged/list filter from `TaskType` string to `FollowUpTaskTypeId`
    - _Requirements: 4.6_

- [ ] 4. Service + DTOs
  - [ ] 4.1 DTOs — add `FollowUpTaskTypeId` to create/update requests; add `FollowUpTaskTypeId` + `TaskTypeName` to `FollowUpTaskDto`
    - _Requirements: 5.3, 5.4_

  - [ ] 4.2 `FollowUpTaskService` — remove `ValidTaskTypes` array; inject `FollowUpTaskTypeRepository`; validate id against lookup; set entity `FollowUpTaskTypeId` (+ `TaskType` name in sync); map DTO fields
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ] 4.3 `MeetingService` task mapping — carry `FollowUpTaskTypeId` alongside the name
    - _Requirements: 5.4_

- [ ] 5. Controller + DI
  - [ ] 5.1 `AxGetLookups` — add `taskTypes` (Id, Name) from `FollowUpTaskTypeRepository`
    - _Requirements: 6.1_

  - [ ] 5.2 Create/update endpoints accept `FollowUpTaskTypeId`; `AxGetTasksPaged` filter param → `followUpTaskTypeId`
    - _Requirements: 6.2, 6.3, 6.4_

  - [ ] 5.3 Register `FollowUpTaskTypeRepository` in `Program.cs`
    - _Requirements: 6.5_

- [ ] 6. UI
  - [ ] 6.1 `follow-up-tasks.js` — build Type dropdown from lookup, default `Follow-up`, submit `followUpTaskTypeId`; badge colours keyed by name
    - _Requirements: 7.1, 7.4, 7.5_

  - [ ] 6.2 `Tasks.cshtml` — filter dropdown from lookup; send `followUpTaskTypeId`
    - _Requirements: 7.2, 7.5_

  - [ ] 6.3 `Meetings.cshtml` — meeting-task Type dropdown from lookup; submit `followUpTaskTypeId`
    - _Requirements: 7.3, 7.4_

- [ ] 7. Verification
  - [ ] 7.1 Build solution — 0 errors
    - _Requirements: 8.4_

  - [ ] 7.2 Manual/data checks: existing tasks retain type (backfill correct, bogus→Other, NOT NULL, FK present); create/update persists id + syncs name; filter by id works; three dropdowns list five types with correct default and badge colours; completion/scheduling/meeting-linkage unaffected
    - _Requirements: 2.2, 2.3, 8.1, 8.2, 8.3, 7.1, 7.2, 7.3, 7.5_

## Notes

- Mirror `[sales].[MeetingType]` at every layer; the only divergence is `Id TINYINT` (entity `byte`) and plural table name `FollowUpTaskTypes`.
- Seed order = UI order (Call, Email, Follow-up, Meeting Prep, Other); `Follow-up` (id 3) stays the create default.
- Phase 1 keeps `TaskType` populated in sync so read queries need no join yet and rollback is trivial.
- All migrations idempotent, `USE [Portal]`, `[sales]` schema.
- Repositories: `try/catch (Exception ex) { throw; }`. AJAX: `Json(new { success, message })`.

## Phase 2 (separate, after operator confirmation — NOT in this plan)

- Migration to drop `[sales].[FollowUpTask].[TaskType]`.
- Remove the `TaskType` entity property; switch repository SELECTs to `JOIN [sales].[FollowUpTaskTypes]` for the display name.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 6, "tasks": ["7.1", "7.2"] }
  ]
}
```
