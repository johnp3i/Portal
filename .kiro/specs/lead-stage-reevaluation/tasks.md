# Implementation Plan: Lead Stage Reevaluation Bugfix

## Overview

When a meeting linked to a lead is cancelled and no other valid meetings remain, the lead stays stuck in the "Meetings" stage. Conversely, reactivating a meeting doesn't advance a lead that has regressed below "Meetings." This bugfix introduces `[sales].[LeadTrackingHistory]` and `[sales].[LeadTrackingActionType]` tables to record every stage transition, a `LeadTrackingHistoryRepository`, and a new `ReevaluateStageOnMeetingChangeAsync` method that uses a history-scanning regression algorithm to determine the correct target stage. The existing `SuggestStageTransitionAsync` is extended to write history records, and `MeetingService.CancelMeetingAsync` / `ReactivateMeetingAsync` are wired to trigger re-evaluation.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Lead Stage Not Reevaluated on Meeting Cancel/Reactivate
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to two concrete failing scenarios:
    1. Cancel the last active meeting for a lead at stage 4 (Meetings) — assert that `ReevaluateStageOnMeetingChangeAsync` is called and stage changes (regresses based on tracking history)
    2. Reactivate a meeting for a lead below stage 4 — assert that `ReevaluateStageOnMeetingChangeAsync` is called and stage advances to 4
  - Test that `CancelMeetingAsync(id, description)` triggers stage regression when: meeting has a `LeadRequestId`, lead is at stage 4, and the tracking history regression algorithm determines a lower target (from Bug Condition `isBugCondition` in design)
  - Test that `ReactivateMeetingAsync(id)` triggers stage advancement when: meeting has a `LeadRequestId`, lead is below stage 4 (from Bug Condition `isBugCondition` in design)
  - Use FsCheck with mocked `MeetingRepository`, `ILeadRequestService`, `LeadTrackingHistoryRepository`
  - Generate random `leadRequestId`, `businessId`, and history configurations for cancel scenarios
  - Generate random stages 1–3 for reactivation scenarios
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists because neither `CancelMeetingAsync` nor `ReactivateMeetingAsync` call any stage transition or re-evaluation logic)
  - Document counterexamples found: e.g., "CancelMeetingAsync does not call ReevaluateStageOnMeetingChangeAsync — lead remains at stage 4" and "ReactivateMeetingAsync does not advance lead from stage 2 to stage 4"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.3, 2.1, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Bug-Condition Inputs Unaffected
  - **IMPORTANT**: Follow observation-first methodology
  - Observe on UNFIXED code:
    - Cancel a standalone meeting (no `LeadRequestId`) — no stage logic invoked, no history written
    - Cancel a meeting for lead at stage 5, 6, or 7 — stage unchanged, no regression
    - Cancel a meeting for lead with `IsTerminal = true` — stage unchanged
    - Reactivate a meeting for lead already at stage 4 or above — stage unchanged
    - Update a meeting (subject, time, location) — no stage re-evaluation triggered
  - Write property-based tests (FsCheck) capturing observed behavior:
    - **Standalone meetings**: For all meetings where `LeadRequestId` is null, cancellation and reactivation produce no stage evaluation calls and no tracking history records
    - **Advanced stage leads**: For all leads at stages 5, 6, 7, meeting cancellation does not modify the stage (Preservation Property 3 from design)
    - **Terminal stage leads**: For all leads with `IsTerminal = true`, meeting cancellation/reactivation does not modify the stage (Preservation Property 5 from design)
    - **Already-at-or-above meetings reactivation**: For all leads at stage >= 4, reactivation does not change stage
  - Generate random combinations of stage (1–7), terminal flag, meeting counts, and LeadRequestId presence
  - Verify tests PASS on UNFIXED code (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Schema migration — Create LeadTrackingActionType and LeadTrackingHistory tables

  - [x] 3.1 Create SQL migration script
    - Create `Portal.Database/Migrations/XXX_CreateLeadTrackingHistory.sql`
    - Add `USE [Portal]` at top of script
    - Create `[sales].[LeadTrackingActionType]` table:
      - `[Id] INT NOT NULL` (PK)
      - `[Name] NVARCHAR(50) NOT NULL`
    - Seed action types: 1=MeetingScheduled, 2=MeetingCancelled, 3=MeetingReactivated, 4=ResponseSent, 5=ProposalLinked, 6=ManualStageChange, 7=MarkedAsWon, 8=LeadCancelled, 9=LeadReactivated
    - Create `[sales].[LeadTrackingHistory]` table:
      - `[Id] INT IDENTITY(1,1) NOT NULL` (PK)
      - `[LeadRequestId] INT NOT NULL` (FK → `[sales].[LeadRequest]`)
      - `[BusinessId] INT NOT NULL`
      - `[LeadTrackingActionTypeId] INT NOT NULL` (FK → `[sales].[LeadTrackingActionType]`)
      - `[FromLeadStatusTypeId] INT NULL`
      - `[ToLeadStatusTypeId] INT NOT NULL`
      - `[RelatedEntityId] INT NULL`
      - `[CreatedByUserId] NVARCHAR(450) NULL`
      - `[CreatedAtUtc] DATETIME NOT NULL DEFAULT GETUTCDATE()`
    - Add FK constraints: `FK_LeadTrackingHistory_LeadRequest`, `FK_LeadTrackingHistory_ActionType`
    - Add nonclustered index: `IX_LeadTrackingHistory_LeadRequestId_BusinessId` on `([LeadRequestId], [BusinessId]) INCLUDE ([LeadTrackingActionTypeId], [ToLeadStatusTypeId], [RelatedEntityId])` for efficient history scans
    - Use full table names in SQL (no aliases)
    - _Requirements: 2.1, 2.3_

  - [x] 3.2 Create EF Core entity for LeadTrackingActionType
    - Create `Portal.Infrastructure/Entities/Sales/LeadTrackingActionType.cs`
    - Properties: `int Id`, `string Name`
    - _Requirements: 2.1_

  - [x] 3.3 Create EF Core entity for LeadTrackingHistory
    - Create `Portal.Infrastructure/Entities/Sales/LeadTrackingHistory.cs`
    - Properties: `int Id`, `int LeadRequestId`, `int BusinessId`, `int LeadTrackingActionTypeId`, `int? FromLeadStatusTypeId`, `int ToLeadStatusTypeId`, `int? RelatedEntityId`, `string? CreatedByUserId`, `DateTime CreatedAtUtc`
    - _Requirements: 2.1, 2.3_

  - [x] 3.4 Create LeadTrackingHistoryRepository
    - Create `Portal.Infrastructure/Repositories/Sales/LeadTrackingHistoryRepository.cs`
    - Extend `GenericStoredProcedureRepository<LeadTrackingHistory>`
    - Method `InsertAsync(LeadTrackingHistory entity)`: INSERT into `[sales].[LeadTrackingHistory]` using `ExecuteSqlRawAsync` with SqlParameters, null-safe via `?? (object)DBNull.Value`
    - Method `GetByLeadRequestIdAsync(int leadRequestId, int businessId)`: SELECT from `[sales].[LeadTrackingHistory]` WHERE `LeadTrackingHistory.LeadRequestId = @LeadRequestId AND LeadTrackingHistory.BusinessId = @BusinessId` ORDER BY `LeadTrackingHistory.CreatedAtUtc DESC`
    - Use full table names in SQL (no aliases)
    - `catch (Exception ex) { throw; }` pattern
    - _Requirements: 2.1, 2.3_

  - [x] 3.5 Register LeadTrackingHistoryRepository in DI
    - Add `builder.Services.AddScoped<LeadTrackingHistoryRepository>()` in `Portal.Web/Program.cs`
    - _Requirements: 2.1_

- [x] 4. Implement ReevaluateStageOnMeetingChangeAsync and wire up

  - [x] 4.1 Add interface method to ILeadRequestService
    - Add `Task ReevaluateStageOnMeetingChangeAsync(int leadRequestId, string changeType);` to `Portal.Infrastructure/Services/Sales/ILeadRequestService.cs`
    - _Requirements: 2.1, 2.3_

  - [x] 4.2 Modify SuggestStageTransitionAsync signature and implementation
    - Change signature to: `Task SuggestStageTransitionAsync(int leadRequestId, string eventType, int? relatedEntityId = null)`
    - Update interface in `ILeadRequestService.cs` to match
    - After successfully updating the stage, write a tracking history record via `_leadTrackingHistoryRepository.InsertAsync`:
      - `"response_sent"` → ActionTypeId=4, RelatedEntityId=null
      - `"meeting_scheduled"` → ActionTypeId=1, RelatedEntityId=relatedEntityId (the meeting ID)
      - `"proposal_linked"` → ActionTypeId=5, RelatedEntityId=relatedEntityId (the quotation ID)
    - Record: LeadRequestId, BusinessId, LeadTrackingActionTypeId, FromLeadStatusTypeId=current stage, ToLeadStatusTypeId=new stage, RelatedEntityId, CreatedByUserId from tenant
    - Inject `LeadTrackingHistoryRepository` into `LeadRequestService` constructor
    - _Bug_Condition: SuggestStageTransitionAsync now records history enabling regression algorithm_
    - _Requirements: 2.1, 3.1_

  - [x] 4.3 Implement ReevaluateStageOnMeetingChangeAsync in LeadRequestService
    - When `changeType == "meeting_cancelled"`:
      1. Load lead, check if terminal → return early
      2. Check if `lead.LeadStatusTypeId != 4` → return early (no regression for non-Meetings stage)
      3. Query all tracking history via `_leadTrackingHistoryRepository.GetByLeadRequestIdAsync`
      4. Filter to forward action types: MeetingScheduled (1), ResponseSent (4), ProposalLinked (5), ManualStageChange (6)
      5. Batch-load related meeting statuses: collect distinct `RelatedEntityId` values from MeetingScheduled records, query meetings in a single call (avoid N+1), check each is `IsCancelled == false && IsActive == true`
      6. For each forward record, validate related entity:
         - MeetingScheduled → check batch-loaded meeting status (must be active and non-cancelled)
         - ResponseSent → always valid
         - ProposalLinked → always valid
         - ManualStageChange → always valid
      7. Find highest `ToLeadStatusTypeId` among ALL valid records (no artificial ceiling — if another meeting at stage 4 is still active, the lead naturally stays at 4)
      8. If no valid forward records → regress to stage 1 (New)
      9. Write "MeetingCancelled" history record (ActionTypeId=2, From=4, To=computed target stage)
      10. Call `_leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, targetStageId)` only if target differs from current stage
    - When `changeType == "meeting_reactivated"`:
      1. Load lead, check if terminal → return early
      2. If `lead.LeadStatusTypeId >= 4` → no change needed, return early
      3. Write "MeetingReactivated" history record (ActionTypeId=3, From=current, To=4)
      4. Call `_leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, 4)`
    - `catch (Exception ex) { throw; }` pattern
    - _Bug_Condition: isBugCondition(input) where action is "cancel" with lead at stage 4; OR action is "reactivate" with lead below stage 4_
    - _Expected_Behavior: Cancel → regress to highest valid ToLeadStatusTypeId from forward history records, or stage 1 if none; Reactivate → advance to stage 4_
    - _Preservation: Leads at stages 5/6/7 never regressed (early return); terminal leads never modified; standalone meetings never reach this method_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.3, 3.5_

  - [x] 4.4 Modify MeetingService.CancelMeetingAsync
    - Retrieve meeting entity first: `var meeting = await _meetingRepository.GetByIdAsync(id, _tenantService.CurrentBusinessId)`
    - If meeting is null, return `ServiceResult.Fail("Meeting not found.")`
    - Call `await _meetingRepository.CancelAsync(id, _tenantService.CurrentBusinessId, description)`
    - After cancel succeeds: if `meeting.LeadRequestId.HasValue`, call `await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_cancelled")`
    - _Requirements: 2.1, 2.2, 2.4, 3.2_

  - [x] 4.5 Modify MeetingService.ReactivateMeetingAsync
    - Retrieve meeting entity first: `var meeting = await _meetingRepository.GetByIdAsync(id, _tenantService.CurrentBusinessId)`
    - If meeting is null, return `ServiceResult.Fail("Meeting not found.")`
    - Call `await _meetingRepository.ReactivateAsync(id, _tenantService.CurrentBusinessId)`
    - After reactivate succeeds: if `meeting.LeadRequestId.HasValue`, call `await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_reactivated")`
    - _Requirements: 2.3_

  - [x] 4.6 Update CreateMeetingAsync to pass relatedEntityId
    - In `MeetingService.CreateMeetingAsync`, change the `SuggestStageTransitionAsync` call to pass the new meeting ID:
      - `await _leadRequestService.SuggestStageTransitionAsync(request.LeadRequestId.Value, "meeting_scheduled", id)`
    - This ensures the tracking history records the meeting ID as the related entity
    - _Requirements: 3.1_

  - [x] 4.7 Update LinkProposalAsync to pass relatedEntityId
    - In `LeadRequestService.LinkProposalAsync`, change the `SuggestStageTransitionAsync` call to pass the quotation ID:
      - `await SuggestStageTransitionAsync(leadRequestId, "proposal_linked", quotationId)`
    - This ensures the tracking history records the quotation ID as the related entity for audit completeness
    - _Requirements: 3.1_

  - [x] 4.8 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Lead Stage Correctly Reevaluated After Fix
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (stage regression on cancel via history scan, stage advancement on reactivate)
    - When this test passes, it confirms:
      - `CancelMeetingAsync` now retrieves meeting, cancels, and calls `ReevaluateStageOnMeetingChangeAsync` which scans tracking history and regresses the lead
      - `ReactivateMeetingAsync` now retrieves meeting, reactivates, and calls `ReevaluateStageOnMeetingChangeAsync` which advances the lead to stage 4
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.3_

  - [x] 4.9 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Bug-Condition Inputs Still Unaffected After Fix
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm: standalone meetings still unaffected, advanced-stage leads still untouched, terminal leads still immune
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 5. Checkpoint - Ensure all tests pass
  - Run full test suite to confirm no regressions across the project
  - Verify bug condition test (Property 1) passes — confirms fix correctness
  - Verify preservation tests (Property 2) pass — confirms no side effects
  - Ensure all existing unit tests in `Portal.Tests` still pass
  - Verify SQL migration script syntax is valid
  - Ask the user if questions arise

## Notes

- Property-based tests use FsCheck.Xunit (already available in the project)
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- Pipeline stages: 1=New, 2=Contacted, 3=Qualified, 4=Meetings, 5=Proposal, 6=Negotiation, 7=Won
- Action types: 1=MeetingScheduled, 2=MeetingCancelled, 3=MeetingReactivated, 4=ResponseSent, 5=ProposalLinked, 6=ManualStageChange, 7=MarkedAsWon, 8=LeadCancelled, 9=LeadReactivated
- Regression algorithm: scan history for forward action types (1,4,5,6), validate related entities still active via batch-loading, find highest ToLeadStatusTypeId among ALL valid records (no artificial ceiling — the data naturally determines the correct target)
- Validation rules: MeetingScheduled → batch-check meetings still active (IsCancelled=0, IsActive=1); ResponseSent → always valid; ProposalLinked → always valid; ManualStageChange → always valid
- Batch-loading: collect distinct RelatedEntityIds from MeetingScheduled records, query all in one call to avoid N+1
- `SuggestStageTransitionAsync` uses optional parameter: `int? relatedEntityId = null` — backward compatible
- `MeetingService` already has `_leadRequestService` injected
- `LeadRequestService` already has `_meetingRepository` injected
- SQL migration uses `USE [Portal]` at top, full table names (no aliases), `CreatedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE()`
- The `LeadTrackingHistoryRepository` follows repository-standards: extends `GenericStoredProcedureRepository<T>`, uses `ExecuteSqlRawAsync`, null-safe SqlParameters

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3"] },
    { "id": 6, "tasks": ["4.4", "4.5", "4.6", "4.7"] },
    { "id": 7, "tasks": ["4.8", "4.9"] },
    { "id": 8, "tasks": ["5"] }
  ]
}
```
