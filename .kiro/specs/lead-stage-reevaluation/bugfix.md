# Bugfix Requirements Document

## Introduction

When a meeting linked to a lead is cancelled, the system does not re-evaluate the lead's pipeline stage. This causes the lead to remain stuck in the "Meetings" stage even when no valid (non-cancelled, active) meetings exist for that lead. The same gap exists in reverse: reactivating a meeting does not re-evaluate whether the lead should advance back to "Meetings." The fix must introduce stage re-evaluation logic after both meeting cancellation and reactivation while respecting the pipeline order and not regressing leads that have already progressed beyond the "Meetings" stage.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a meeting linked to a lead is cancelled AND no other valid (non-cancelled, active) meetings exist for that lead THEN the system leaves the lead in the "Meetings" stage without re-evaluation

1.2 WHEN a meeting linked to a lead is cancelled AND the lead is in the "Meetings" stage AND other valid meetings still exist for that lead THEN the system does not verify this — it simply does nothing regarding stage (which happens to be correct by accident, not by design)

1.3 WHEN a cancelled meeting linked to a lead is reactivated AND the lead is in an earlier stage (e.g., "Contacted") THEN the system does not advance the lead back to the "Meetings" stage

### Expected Behavior (Correct)

2.1 WHEN a meeting linked to a lead is cancelled AND no other valid (non-cancelled, active) meetings exist for that lead AND the lead is currently in the "Meetings" stage THEN the system SHALL regress the lead to the appropriate earlier stage based on its activity history (e.g., "Contacted" if responses exist, otherwise "New")

2.2 WHEN a meeting linked to a lead is cancelled AND other valid (non-cancelled, active) meetings still exist for that lead THEN the system SHALL keep the lead in the "Meetings" stage

2.3 WHEN a cancelled meeting linked to a lead is reactivated AND the lead is currently in a stage earlier than "Meetings" THEN the system SHALL advance the lead to the "Meetings" stage

2.4 WHEN a meeting linked to a lead is cancelled AND the lead is currently in a stage beyond "Meetings" (e.g., "Proposal", "Negotiation", or "Won") THEN the system SHALL NOT regress the lead stage

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a new meeting is created for a lead THEN the system SHALL CONTINUE TO advance the lead to the "Meetings" stage via `SuggestStageTransitionAsync`

3.2 WHEN a meeting is cancelled for a lead that has no `LeadRequestId` (standalone meeting) THEN the system SHALL CONTINUE TO cancel the meeting without any stage evaluation

3.3 WHEN a lead is in a terminal stage (Won/Lost) THEN the system SHALL CONTINUE TO not modify the lead stage regardless of meeting cancellation or reactivation

3.4 WHEN a meeting is updated (subject, time, location changes) but not cancelled THEN the system SHALL CONTINUE TO not trigger any stage re-evaluation

3.5 WHEN a lead has progressed to "Proposal" or "Negotiation" stage and a meeting is cancelled THEN the system SHALL CONTINUE TO keep the lead at its current advanced stage without regression
