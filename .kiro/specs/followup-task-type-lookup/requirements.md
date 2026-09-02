# Requirements Document

## Introduction

The `[sales].[FollowUpTask].[TaskType]` column is currently free-text (`NVARCHAR(50)`). Its allowed values — `Call`, `Email`, `Follow-up`, `Meeting Prep`, `Other` — are defined only in code: a private C# string array (`FollowUpTaskService.ValidTaskTypes`) plus hardcoded `<option>` literals in three UI locations (the follow-up task modal JS, the Tasks page filter, and the meeting-task form). Nothing constrains the value at the database level, and the source of truth is duplicated across layers.

This feature replaces the free-text column with a proper key–value **reference table**, modelled exactly on the existing `[sales].[MeetingType]` lookup. The task will reference the lookup by id (`FollowUpTaskTypeId`), the allowed values become database rows, and every layer loads them from a single source.

The conversion is performed in two phases to keep it safe and reversible:
- **Phase 1 (this spec):** add the lookup table, add `FollowUpTaskTypeId`, backfill existing rows, wire all code to the id, and keep the old `TaskType` string column populated in sync.
- **Phase 2 (later, after the operator confirms):** drop the `TaskType` column.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 multi-tenant application.
- **Follow_Up_Task**: A lightweight sales reminder (`[sales].[FollowUpTask]`) attached to a lead, contact, or meeting.
- **Follow_Up_Task_Type**: The category of a follow-up task (Call, Email, Follow-up, Meeting Prep, Other).
- **Follow_Up_Task_Types_Table**: The new reference table `[sales].[FollowUpTaskTypes]`.
- **Meeting_Type_Table**: The existing reference table `[sales].[MeetingType]` this feature mirrors.
- **Lookup**: A small, code-defined reference table of `Id` + `Name` rows loaded into dropdowns.

## Requirements

### Requirement 1: Follow-Up Task Types Reference Table

**User Story:** As a system architect, I want follow-up task types stored in a dedicated reference table, so that valid values are defined once in the database rather than duplicated across code.

#### Acceptance Criteria

1. THE Portal database SHALL contain a `FollowUpTaskTypes` table in the `[sales]` schema with columns: `Id` (TINYINT NOT NULL), `Name` (NVARCHAR(50) NOT NULL).
2. THE `Id` column SHALL be the primary key and SHALL NOT be an identity column (values are assigned explicitly, mirroring `[sales].[MeetingType]`).
3. THE table SHALL be seeded with exactly these rows, in this order: `Call`, `Email`, `Follow-up`, `Meeting Prep`, `Other`.
4. THE seed SHALL be idempotent — running the migration more than once SHALL NOT create duplicate rows.
5. THE creation script SHALL follow the project SQL conventions: `USE [Portal]` header, `IF NOT EXISTS` guards, and `[sales]` schema.

### Requirement 2: FollowUpTaskTypeId on FollowUpTask

**User Story:** As a system architect, I want each follow-up task to reference its type by id, so that the type is enforced by a foreign key.

#### Acceptance Criteria

1. THE `[sales].[FollowUpTask]` table SHALL add a `FollowUpTaskTypeId` (TINYINT) column with a foreign key to `[sales].[FollowUpTaskTypes]([Id])`.
2. THE migration SHALL backfill `FollowUpTaskTypeId` for every existing row by matching the current `TaskType` text to the corresponding lookup `Name`.
3. IF an existing row has a `TaskType` value that does not match any lookup name, THEN THE migration SHALL map it to the `Other` type so no row is left without a valid type.
4. AFTER backfill, `FollowUpTaskTypeId` SHALL be set `NOT NULL`.
5. THE existing `TaskType` (NVARCHAR) column SHALL be retained in this phase (not dropped).
6. THE migration SHALL be idempotent (column-existence and constraint-existence guards).

### Requirement 3: Entity and EF Core Mapping

**User Story:** As a developer, I want the lookup and the id relationship represented in the EF model, so that queries and inserts use the id.

#### Acceptance Criteria

1. THE codebase SHALL contain a `FollowUpTaskType` entity (`Id`, `Name`) mirroring the `MeetingType` entity.
2. THE `PortalDbContext` SHALL configure `FollowUpTaskTypes` with `ToTable("FollowUpTaskTypes", "sales")`, key on `Id`, `Id` value-generated-never, and `Name` required max length 50.
3. THE `FollowUpTask` entity SHALL add a `FollowUpTaskTypeId` property and a `FollowUpTaskType` navigation property.
4. THE `FollowUpTask` EF configuration SHALL map the `FollowUpTaskTypeId` foreign key relationship to `FollowUpTaskType`.
5. THE existing `TaskType` string property on the entity SHALL be retained in this phase.

### Requirement 4: Repository Reads and Writes by Id

**User Story:** As a developer, I want the repository to persist and read the task type by id and return the type name, so that the UI can display it without a second lookup.

#### Acceptance Criteria

1. THE codebase SHALL contain a `FollowUpTaskTypeRepository` with a `GetAllAsync()` method returning all types, mirroring `MeetingTypeRepository`.
2. THE `FollowUpTaskRepository` INSERT SHALL write `FollowUpTaskTypeId`.
3. DURING this transition phase, THE INSERT and UPDATE SHALL also keep the legacy `TaskType` string column populated with the corresponding type name, so the retained column stays consistent.
4. THE `FollowUpTaskRepository` UPDATE SHALL update `FollowUpTaskTypeId`.
5. THE `FollowUpTaskRepository` SELECT queries SHALL include `FollowUpTaskTypeId` and SHALL resolve the type `Name` (via join) for display.
6. THE paged/list query filter that currently filters by `TaskType` text SHALL filter by `FollowUpTaskTypeId`.

### Requirement 5: Service Validation by Lookup

**User Story:** As a developer, I want task-type validation to be based on the reference table, so that the hardcoded string array is removed.

#### Acceptance Criteria

1. THE `FollowUpTaskService` SHALL validate the submitted `FollowUpTaskTypeId` against the values in `[sales].[FollowUpTaskTypes]` (existence check), replacing the hardcoded `ValidTaskTypes` string array.
2. WHEN an invalid or missing `FollowUpTaskTypeId` is submitted, THE service SHALL return a failure result with a clear message.
3. THE create and update service methods SHALL accept `FollowUpTaskTypeId`.
4. THE task DTOs returned to the UI SHALL include both `FollowUpTaskTypeId` and the resolved `TaskTypeName`.

### Requirement 6: Controller Endpoints and Lookup Delivery

**User Story:** As a front-end developer, I want the task types available from the lookups endpoint and the create/update/filter endpoints to use the id, so that the UI has one source for options.

#### Acceptance Criteria

1. THE `AxGetLookups` endpoint SHALL include a `taskTypes` collection (`Id`, `Name`) sourced from `FollowUpTaskTypeRepository`.
2. THE create-task endpoint SHALL accept `FollowUpTaskTypeId` on its request model.
3. THE update-task endpoint SHALL accept `FollowUpTaskTypeId`.
4. THE tasks list/paged endpoint SHALL accept a `followUpTaskTypeId` filter parameter (replacing the `taskType` string filter).
5. THE `FollowUpTaskTypeRepository` SHALL be registered in the DI container.

### Requirement 7: UI Loads Types from the Lookup

**User Story:** As a user, I want the task type dropdowns to show the configured types and behave exactly as before, so that creating and filtering tasks is unchanged from my perspective.

#### Acceptance Criteria

1. THE follow-up task create/edit modal SHALL populate its Type dropdown from the lookup (via `AxGetLookups`) and submit the selected `FollowUpTaskTypeId`.
2. THE Tasks page type filter SHALL populate from the lookup and filter by `followUpTaskTypeId`.
3. THE meeting-task form's Type dropdown SHALL populate from the lookup and submit the selected `FollowUpTaskTypeId`.
4. THE default selected type in create forms SHALL remain `Follow-up`, matching current behaviour.
5. THE task type badge/label rendering SHALL continue to display the type name, and existing name-keyed badge colours (Call, Email, Follow-up, Meeting Prep, Other) SHALL be preserved.

### Requirement 8: Backward Compatibility and Data Integrity

**User Story:** As a platform operator, I want the change to be safe and reversible, so that no data is lost and existing tasks continue to work.

#### Acceptance Criteria

1. ALL existing follow-up tasks SHALL retain their type after migration (backfilled `FollowUpTaskTypeId` matching their prior `TaskType`).
2. WHILE the `TaskType` column is retained, reads that still reference it SHALL continue to return the correct value.
3. THE change SHALL not alter tenant isolation, task completion/outcome, scheduling, or meeting-linkage behaviour.
4. THE solution SHALL build with 0 errors.

## Out of Scope (Phase 2 — later)

- Dropping the `TaskType` NVARCHAR column and removing its entity property and repository references. This is deferred to a follow-up migration performed only after the operator confirms Phase 1 is verified in the running environment.
- Making task types user/admin-managed (CRUD UI for the lookup). The set remains code/seed-defined, consistent with `[sales].[MeetingType]`.
- Adding a `DisplayOrder` or `IsActive` column — mirrors `MeetingType`, which has neither (order is fixed by seed/name).
