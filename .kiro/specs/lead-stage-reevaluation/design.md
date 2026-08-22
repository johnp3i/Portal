# Lead Stage Reevaluation Bugfix Design

## Overview

When a meeting linked to a lead is cancelled and no other valid meetings remain, the lead stays stuck in the "Meetings" stage. Conversely, reactivating a meeting doesn't advance a lead that has regressed below "Meetings." Rather than computing regression targets by querying live state across multiple tables (meetings, responses), this fix introduces a `[sales].[LeadTrackingHistory]` table that records every stage transition with the action type that caused it. Regression then becomes: "scan backwards through history, find the highest justified stage whose related entity is still valid." This provides a complete audit trail, extensible action types, and a single-query regression algorithm.

## Glossary

- **Bug_Condition (C)**: A meeting linked to a lead is cancelled/reactivated AND the system does not re-evaluate the lead's pipeline stage accordingly
- **Property (P)**: After cancellation with no remaining valid forward actions, the lead regresses from "Meetings" to the highest justified stage from history; after reactivation, the lead advances to "Meetings" if currently at a lower stage
- **Preservation**: Leads in stages beyond "Meetings" (Proposal, Negotiation, Won) are never regressed; standalone meetings (no LeadRequestId) are unaffected; meeting updates without cancellation trigger no stage logic
- **LeadTrackingHistory**: New audit table recording every stage transition with the action type, from/to stages, and the related entity (meeting ID, response ID, etc.)
- **LeadTrackingActionType**: Lookup table defining action types (MeetingScheduled, MeetingCancelled, ResponseSent, ProposalLinked, ManualStageChange, etc.)
- **LeadStatusTypeId**: The `[sales].[LeadStatusType]` ID representing the pipeline stage (1=New, 2=Contacted, 3=Qualified, 4=Meetings, 5=Proposal, 6=Negotiation, 7=Won)
- **SuggestStageTransitionAsync**: Existing method in `ILeadRequestService` that advances leads forward — will be extended to also write a tracking history record
- **ReevaluateStageOnMeetingChangeAsync**: New method to be introduced in `ILeadRequestService` that scans tracking history to determine the correct regression target
- **Related Entity Validation**: For each history record, checking whether the associated entity (meeting, response, proposal) is still valid/active to determine if the stage it justified is still supported

## Bug Details

### Bug Condition

The bug manifests when a meeting linked to a lead is cancelled (or reactivated) and the system performs no stage re-evaluation. `MeetingService.CancelMeetingAsync` simply calls `_meetingRepository.CancelAsync` and returns. `MeetingService.ReactivateMeetingAsync` simply calls `_meetingRepository.ReactivateAsync` and returns. Neither method queries remaining meetings or triggers any stage transition logic.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { action: "cancel" | "reactivate", meetingId: int, leadRequestId: int? }
  OUTPUT: boolean

  IF input.leadRequestId IS NULL THEN
    RETURN false  -- standalone meetings are not affected
  END IF

  lead := GetLeadById(input.leadRequestId)
  IF lead.IsTerminal THEN
    RETURN false  -- terminal stages are never re-evaluated
  END IF

  IF input.action == "cancel" THEN
    RETURN lead.LeadStatusTypeId == 4  -- Meetings stage
  END IF

  IF input.action == "reactivate" THEN
    RETURN lead.LeadStatusTypeId < 4  -- Below Meetings stage
  END IF

  RETURN false
END FUNCTION
```

### Examples

- **Cancel, no remaining valid history, lead at "Meetings"**: Meeting #5 for Lead #10 is cancelled. Lead #10 is at stage 4 (Meetings). History scan finds no other valid forward records. Expected: Lead regresses to stage 1 (New). Actual: Lead stays at stage 4.
- **Cancel, history shows ResponseSent still valid, lead at "Meetings"**: Meeting #5 for Lead #10 is cancelled. History contains a ResponseSent record (always valid). Expected: Lead regresses to stage 2 (Contacted). Actual: Lead stays at stage 4.
- **Cancel, other MeetingScheduled still valid in history**: Meeting #5 for Lead #10 is cancelled. History contains MeetingScheduled for Meeting #6, which is still active. Expected: Lead stays at stage 4. Actual: Lead stays at stage 4 (correct by coincidence).
- **Reactivate, lead below "Meetings"**: Meeting #5 for Lead #10 is reactivated. Lead #10 is at stage 2 (Contacted). Expected: Lead advances to stage 4 (Meetings). Actual: Lead stays at stage 2.
- **Cancel, lead already at "Proposal"**: Meeting #5 for Lead #10 is cancelled. Lead #10 is at stage 5 (Proposal). Expected: Lead stays at stage 5 — no regression. Actual: Lead stays at stage 5 (correct, but only because no logic runs at all).

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Creating a new meeting for a lead continues to advance the lead to "Meetings" via `SuggestStageTransitionAsync` (which now additionally writes a history record)
- Cancelling a standalone meeting (no `LeadRequestId`) performs no stage evaluation
- Leads in terminal stages (Won/Lost) are never modified regardless of meeting operations
- Meeting updates (subject, time, location, outcome) do not trigger any stage re-evaluation
- Leads already at "Proposal", "Negotiation", or "Won" are never regressed by meeting cancellation
- All non-meeting-stage controller endpoints remain unaffected

**Scope:**
All inputs that do NOT involve a meeting cancellation/reactivation on a lead-linked meeting where the lead is at the "Meetings" stage (for cancel) or below it (for reactivate) should be completely unaffected by this fix. This includes:
- Standalone meeting operations (no LeadRequestId)
- Meeting creation (already handled by existing `SuggestStageTransitionAsync`)
- Meeting field updates (UpdateMeetingAsync)
- Any lead operation not triggered by meeting cancellation/reactivation

## Hypothesized Root Cause

Based on the bug description, the root cause is clear:

1. **Missing Stage Logic in CancelMeetingAsync**: The `MeetingService.CancelMeetingAsync` method performs only `_meetingRepository.CancelAsync(id, businessId, description)` — it never queries remaining meetings or evaluates the lead's stage. There is no call to any stage transition method after cancellation.

2. **Missing Stage Logic in ReactivateMeetingAsync**: The `MeetingService.ReactivateMeetingAsync` method performs only `_meetingRepository.ReactivateAsync(id, businessId)` — it never checks the lead's current stage or suggests advancement.

3. **SuggestStageTransitionAsync Is Forward-Only**: The existing `SuggestStageTransitionAsync` method only advances leads (using `when lead.LeadStatusTypeId < X` guards). It has no concept of regression and no audit trail of what caused each transition.

4. **No Stage History Exists**: There is no mechanism to answer "why is this lead at this stage?" or "what was the highest justified stage based on still-valid actions?" Without a history table, regression logic would require ad-hoc queries across meetings, responses, and quotations tables with complex if-else branching.

## Correctness Properties

Property 1: Bug Condition - Stage Regression on Meeting Cancellation

_For any_ meeting cancellation where the meeting has a `LeadRequestId`, the lead is currently at stage 4 (Meetings), the lead is not in a terminal stage, and scanning the lead's tracking history reveals no forward action records whose related entities are still valid at or above stage 4, the fixed `CancelMeetingAsync` SHALL trigger stage re-evaluation that regresses the lead to the highest `ToLeadStatusTypeId` among valid history records (or stage 1 "New" if no valid forward records exist).

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition - Stage Advancement on Meeting Reactivation

_For any_ meeting reactivation where the meeting has a `LeadRequestId` and the lead is currently at a stage earlier than 4 (Meetings) and the lead is not in a terminal stage, the fixed `ReactivateMeetingAsync` SHALL advance the lead to stage 4 (Meetings) and record a "MeetingReactivated" entry in tracking history.

**Validates: Requirements 2.3**

Property 3: Preservation - No Regression Beyond Meetings Stage

_For any_ meeting cancellation where the lead is at stage 5 (Proposal), 6 (Negotiation), or 7 (Won), the fixed code SHALL NOT modify the lead's stage, preserving the current advanced stage regardless of remaining meeting count.

**Validates: Requirements 2.4, 3.5**

Property 4: Preservation - Standalone Meetings Unaffected

_For any_ meeting cancellation or reactivation where the meeting has no `LeadRequestId` (standalone meeting), the fixed code SHALL produce exactly the same behavior as the original code — no stage evaluation occurs and no history record is written for stage changes.

**Validates: Requirements 3.2**

Property 5: Preservation - Terminal Stage Immunity

_For any_ meeting cancellation or reactivation where the lead is in a terminal stage (Won/Lost with `IsTerminal = true`), the fixed code SHALL NOT modify the lead's stage.

**Validates: Requirements 3.3**

## Fix Implementation

### Schema Changes

#### New Table: `[sales].[LeadTrackingActionType]`

```sql
CREATE TABLE [sales].[LeadTrackingActionType] (
    [Id]   INT NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_LeadTrackingActionType] PRIMARY KEY ([Id])
);

INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES
(1, 'MeetingScheduled'),
(2, 'MeetingCancelled'),
(3, 'MeetingReactivated'),
(4, 'ResponseSent'),
(5, 'ProposalLinked'),
(6, 'ManualStageChange'),
(7, 'MarkedAsWon'),
(8, 'LeadCancelled'),
(9, 'LeadReactivated');
```

#### New Table: `[sales].[LeadTrackingHistory]`

```sql
CREATE TABLE [sales].[LeadTrackingHistory] (
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [LeadRequestId]             INT NOT NULL,
    [BusinessId]                INT NOT NULL,
    [LeadTrackingActionTypeId]  INT NOT NULL,
    [FromLeadStatusTypeId]      INT NULL,
    [ToLeadStatusTypeId]        INT NOT NULL,
    [RelatedEntityId]           INT NULL,
    [CreatedByUserId]           NVARCHAR(450) NULL,
    [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_LeadTrackingHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeadTrackingHistory_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]),
    CONSTRAINT [FK_LeadTrackingHistory_ActionType] FOREIGN KEY ([LeadTrackingActionTypeId]) REFERENCES [sales].[LeadTrackingActionType]([Id])
);

CREATE NONCLUSTERED INDEX [IX_LeadTrackingHistory_LeadRequestId_BusinessId]
ON [sales].[LeadTrackingHistory] ([LeadRequestId], [BusinessId])
INCLUDE ([LeadTrackingActionTypeId], [ToLeadStatusTypeId], [RelatedEntityId]);
```

### Regression Algorithm

When a meeting is cancelled, the `ReevaluateStageOnMeetingChangeAsync` method:

1. Checks if lead is at stage 4 and is not terminal — if not, exits early
2. Queries all tracking history for this lead
3. Filters to "forward" action types: MeetingScheduled (1), ResponseSent (4), ProposalLinked (5), ManualStageChange (6)
4. Batch-loads all related meeting IDs from MeetingScheduled records in a single query (avoids N+1 — collect distinct RelatedEntityIds, query `[sales].[Meeting]` WHERE `Id IN (...)` to get their active status)
5. For each forward record, validates the related entity is still active:
   - **MeetingScheduled** → check batch-loaded meeting is `IsCancelled = 0 AND IsActive = 1`
   - **ResponseSent** → always valid (responses cannot be cancelled)
   - **ProposalLinked** → always valid (proposals are not unlinked)
   - **ManualStageChange** → always valid (user intent preserved)
6. The highest `ToLeadStatusTypeId` among ALL valid records is the regression target (no artificial ceiling — if another active meeting exists at stage 4, the lead naturally stays at 4)
7. If no valid forward records exist → regress to stage 1 (New)
8. Records a "MeetingCancelled" entry in tracking history (From=4, To=computed target)
9. Updates the lead's stage via `UpdateStageAsync` (only if target differs from current stage)

### Changes Required

**New File**: `Portal.Database/Migrations/XXX_CreateLeadTrackingHistory.sql`
- Migration script creating both `[sales].[LeadTrackingActionType]` and `[sales].[LeadTrackingHistory]` tables with seed data

**New File**: `Portal.Infrastructure/Entities/Sales/LeadTrackingHistory.cs`
- EF Core entity mapping to `[sales].[LeadTrackingHistory]`

**New File**: `Portal.Infrastructure/Entities/Sales/LeadTrackingActionType.cs`
- EF Core entity mapping to `[sales].[LeadTrackingActionType]`

**New File**: `Portal.Infrastructure/Repositories/Sales/LeadTrackingHistoryRepository.cs`
- Repository with methods: `InsertAsync`, `GetByLeadRequestIdAsync`

---

**File**: `Portal.Infrastructure/Services/Sales/ILeadRequestService.cs`

**Change**: Add new method signature
```csharp
Task ReevaluateStageOnMeetingChangeAsync(int leadRequestId, string changeType);
```

---

**File**: `Portal.Infrastructure/Services/Sales/LeadRequestService.cs`

**Function**: New method `ReevaluateStageOnMeetingChangeAsync`

**Specific Changes**:
1. **Implement Regression Logic** (when `changeType == "meeting_cancelled"`):
   - Load lead, check if at stage 4 and not terminal — exit early otherwise
   - Write "MeetingCancelled" history record (ActionTypeId=2, FromLeadStatusTypeId=current, ToLeadStatusTypeId=determined)
   - Query tracking history for this lead
   - Filter to forward action types (1, 4, 5, 6)
   - For MeetingScheduled records: validate related meeting is still active via `_meetingRepository.GetByIdAsync`
   - Find highest `ToLeadStatusTypeId` among valid records below stage 4 as regression target
   - If no valid records → target is stage 1 (New)
   - Call `_leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, targetStageId)`

2. **Implement Advancement Logic** (when `changeType == "meeting_reactivated"`):
   - Load lead, check if terminal → return early
   - Check if `lead.LeadStatusTypeId < 4` → advance to stage 4
   - Write "MeetingReactivated" history record (ActionTypeId=3, From=current, To=4)
   - Call `_leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, 4)`

---

**File**: `Portal.Infrastructure/Services/Sales/LeadRequestService.cs`

**Function**: Modify existing `SuggestStageTransitionAsync`

**Specific Changes**:
- Change method signature to accept optional `relatedEntityId`: `Task SuggestStageTransitionAsync(int leadRequestId, string eventType, int? relatedEntityId = null)`
- After successfully updating the stage, write a tracking history record:
  - `"response_sent"` → ActionTypeId=4 (ResponseSent), RelatedEntityId=null
  - `"meeting_scheduled"` → ActionTypeId=1 (MeetingScheduled), RelatedEntityId=relatedEntityId (the meeting ID)
  - `"proposal_linked"` → ActionTypeId=5 (ProposalLinked), RelatedEntityId=relatedEntityId (the quotation ID)

**Function**: Modify existing `LinkProposalAsync`

**Specific Changes**:
- Update the internal `SuggestStageTransitionAsync` call to pass `quotationId` as `relatedEntityId`:
  - `await SuggestStageTransitionAsync(leadRequestId, "proposal_linked", quotationId);`

---

**File**: `Portal.Infrastructure/Services/Sales/MeetingService.cs`

**Function**: `CancelMeetingAsync`

**Specific Changes**:
1. **Retrieve Meeting Before Cancel**: Call `_meetingRepository.GetByIdAsync(id, businessId)` to get the meeting entity including its `LeadRequestId`
2. **Proceed with cancel**: Call `_meetingRepository.CancelAsync(id, businessId, description)`
3. **Add Post-Cancel Stage Evaluation**: If `meeting.LeadRequestId.HasValue`, call `await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_cancelled")`

---

**File**: `Portal.Infrastructure/Services/Sales/MeetingService.cs`

**Function**: `ReactivateMeetingAsync`

**Specific Changes**:
1. **Retrieve Meeting Before Reactivate**: Call `_meetingRepository.GetByIdAsync(id, businessId)` to get the meeting entity including its `LeadRequestId`
2. **Proceed with reactivate**: Call `_meetingRepository.ReactivateAsync(id, businessId)`
3. **Add Post-Reactivate Stage Evaluation**: If `meeting.LeadRequestId.HasValue`, call `await _leadRequestService.ReevaluateStageOnMeetingChangeAsync(meeting.LeadRequestId.Value, "meeting_reactivated")`

---

**File**: `Portal.Web/Program.cs`

**Change**: Register `LeadTrackingHistoryRepository` in the DI container

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests that call `CancelMeetingAsync` and `ReactivateMeetingAsync` with mocked repositories, then assert that the lead's stage is (or isn't) updated. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Cancel Last Meeting Test**: Cancel the only active meeting for a lead at stage 4. Assert stage changes (will fail on unfixed code — stage stays at 4).
2. **Cancel Non-Last Meeting Test**: Cancel one of two active meetings for a lead at stage 4. Assert stage stays at 4 (passes on unfixed code by coincidence).
3. **Reactivate Below Meetings Test**: Reactivate a meeting for a lead at stage 2. Assert stage advances to 4 (will fail on unfixed code — stage stays at 2).
4. **Cancel At Higher Stage Test**: Cancel a meeting for a lead at stage 5. Assert stage stays at 5 (passes on unfixed code).

**Expected Counterexamples**:
- `CancelMeetingAsync` does not call any stage transition method — lead stage is unchanged
- `ReactivateMeetingAsync` does not call any stage transition method — lead stage is unchanged

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  IF input.action == "cancel" THEN
    result := CancelMeetingAsync_fixed(input.meetingId, description)
    lead := GetLeadById(input.leadRequestId)
    history := GetTrackingHistory(input.leadRequestId)
    validForwardRecords := history.Where(h => h.ActionTypeId IN [1,4,5,6] AND isRelatedEntityValid(h))
    expectedStage := MAX(validForwardRecords.ToLeadStatusTypeId) OR 1 if empty
    ASSERT lead.LeadStatusTypeId == expectedStage
    ASSERT history.Contains(MeetingCancelled record)
  ELSE IF input.action == "reactivate" THEN
    result := ReactivateMeetingAsync_fixed(input.meetingId)
    lead := GetLeadById(input.leadRequestId)
    ASSERT lead.LeadStatusTypeId == 4  -- Meetings
    ASSERT history.Contains(MeetingReactivated record)
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT CancelMeetingAsync_original(input) == CancelMeetingAsync_fixed(input)
  ASSERT ReactivateMeetingAsync_original(input) == ReactivateMeetingAsync_fixed(input)
  -- Lead stage must remain unchanged
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many random combinations of lead stages, meeting counts, and terminal flags
- It catches edge cases such as leads with zero meetings already at "New" stage
- It provides strong guarantees that non-buggy inputs are completely unaffected

**Test Plan**: Observe behavior on UNFIXED code first for non-bug-condition scenarios (standalone meetings, advanced-stage leads, meeting updates), then write property-based tests capturing that behavior.

**Test Cases**:
1. **Standalone Meeting Preservation**: Cancel/reactivate meetings with no LeadRequestId — verify no stage logic is invoked and no history records are written
2. **Advanced Stage Preservation**: Cancel meetings for leads at stages 5, 6, 7 — verify stage is unchanged
3. **Terminal Stage Preservation**: Cancel/reactivate meetings for leads with IsTerminal=true — verify no modification
4. **Meeting Update Preservation**: Update meeting fields — verify no stage re-evaluation occurs

### Unit Tests

- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_cancelled" for lead at stage 4, history scan returns no valid forward records → expect stage 1
- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_cancelled" for lead at stage 4, history scan returns valid ResponseSent at stage 2 → expect stage 2
- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_cancelled" for lead at stage 4, history scan returns valid MeetingScheduled for another active meeting → expect stage 4 (no regression)
- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_cancelled" for lead at stage 5 → expect stage 5 (exit early, no regression)
- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_reactivated" for lead at stage 2 → expect stage 4
- Test `ReevaluateStageOnMeetingChangeAsync` with "meeting_reactivated" for lead at stage 4 → expect stage 4 (no change)
- Test `SuggestStageTransitionAsync` writes correct history record when advancing (MeetingScheduled, ResponseSent, ProposalLinked)
- Test `CancelMeetingAsync` integration — verify it retrieves meeting, cancels, and calls reevaluation
- Test `ReactivateMeetingAsync` integration — verify it retrieves meeting, reactivates, and calls reevaluation
- Test history record correctness — FromLeadStatusTypeId, ToLeadStatusTypeId, RelatedEntityId all populated correctly

### Property-Based Tests

- Generate random lead states (stage 1–7, terminal flag) and random tracking history configurations, apply cancellation, and verify regression target matches the algorithm (highest valid ToLeadStatusTypeId from forward records, or 1)
- Generate random non-bug-condition inputs (standalone meetings, advanced stages, terminal leads) and verify zero side effects on lead stage
- Generate random reactivation scenarios across all stage combinations and verify only leads below stage 4 advance
- Generate random history sequences and verify the validation rules are correctly applied per action type (MeetingScheduled checks active, ResponseSent always valid, etc.)

### Integration Tests

- Full flow: Create lead → Schedule meeting (stage advances to 4, history records MeetingScheduled) → Cancel meeting (history scans, finds no other valid records, stage regresses to 1) → Reactivate meeting (stage advances to 4, history records MeetingReactivated)
- Full flow: Create lead → Send response (stage to 2, history records ResponseSent) → Schedule meeting (stage to 4, history records MeetingScheduled) → Cancel meeting → Verify regression to 2 (ResponseSent still valid in history)
- Full flow: Create lead → Schedule 2 meetings → Cancel one (history finds other MeetingScheduled still valid, stage stays at 4) → Cancel second (no valid forward records at stage 4, regresses)
- Full flow: Create lead → Schedule meeting → Link proposal (stage to 5, history records ProposalLinked) → Cancel meeting (stage stays at 5, no regression beyond Meetings)
- Full flow: Verify tracking history table contains complete audit trail of all transitions with correct action types, from/to stages, and related entity IDs
