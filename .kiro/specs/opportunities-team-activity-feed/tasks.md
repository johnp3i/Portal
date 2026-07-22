# Implementation Plan: Opportunities Team & Activity Feed

## Overview

Adds Team Member management and Activity Feed to the Opportunities module. Team members are assignable to leads. The activity feed records all significant events per lead as an immutable timeline.

## Tasks

- [x] 1. Database migrations
  - [x] 1.1 Create `[sales].[TeamMember]` table with partial unique index on (BusinessId, Email)
  - [x] 1.2 Create `[sales].[ActivityFeed]` table with index on (LeadRequestId, CreatedAtUtc DESC)
  - [x] 1.3 ALTER `[sales].[LeadRequest]` ADD TeamMemberId (INT NULL FK to TeamMember)
  - [x] 1.4 Migration script: create TeamMember records from existing AssignedToUserId values, backfill TeamMemberId

- [x] 2. Entity and model layer
  - [x] 2.1 Create `TeamMember` entity class
  - [x] 2.2 Create `ActivityFeed` entity class
  - [x] 2.3 Update `LeadRequest` entity — add TeamMemberId (int?) and TeamMember navigation
  - [x] 2.4 Register entities in PortalDbContext
  - [x] 2.5 Create DTOs: TeamMemberDto, CreateTeamMemberRequest, UpdateTeamMemberRequest, ActivityFeedDto, ActivityEntry

- [x] 3. Checkpoint — Verify compile

- [x] 4. Repository layer
  - [x] 4.1 Create TeamMemberRepository (Insert, Update, Deactivate, Activate, GetAll, GetActive, GetById)
  - [x] 4.2 Create ActivityFeedRepository (Insert, GetByLeadRequestId paged)
  - [x] 4.3 Update LeadRequestRepository — add UpdateTeamMemberAsync(id, businessId, teamMemberId)

- [x] 5. Service layer
  - [x] 5.1 Create ITeamMemberService / TeamMemberService
  - [x] 5.2 Create IActivityFeedService / ActivityFeedService (RecordAsync with try/catch non-blocking)
  - [x] 5.3 Update LeadRequestService — replace AssignedToUserId logic with TeamMemberId, inject IActivityFeedService
  - [x] 5.4 Add activity recording calls to all lead mutation methods (ChangeStage, Cancel, Reactivate, MarkAsWon, LinkProposal, etc.)

- [x] 6. Checkpoint — Verify compile

- [x] 7. Controller layer
  - [x] 7.1 Add Team AJAX endpoints to SalesController (AxPostCreateTeamMember, AxPostUpdateTeamMember, AxPostDeactivateTeamMember, AxPostActivateTeamMember)
  - [x] 7.2 Add AxGetActivityFeed endpoint (returns paginated feed for a lead)
  - [x] 7.3 Add Team page action (GET /Sales/Team)
  - [x] 7.4 Update AxPostAssignLead to accept TeamMemberId instead of UserId
  - [x] 7.5 Update AxGetPipelineData to resolve team member names

- [x] 8. Checkpoint — Verify compile

- [x] 9. DI registration
  - [x] 9.1 Register TeamMemberRepository, TeamMemberService, ActivityFeedRepository, ActivityFeedService

- [x] 10. Views
  - [x] 10.1 Create Sales/Team.cshtml — Team management page (table + create/edit modal)
  - [x] 10.2 Update Sales/LeadDetail.cshtml — Add Activity Feed section (AJAX-loaded timeline)
  - [x] 10.3 Update Sales/LeadDetail.cshtml — Replace "Assigned To" text with team member dropdown
  - [x] 10.4 Update Sales/Pipeline.cshtml — Replace filter "Assigned To" with team member dropdown
  - [x] 10.5 Update pipeline.js — Resolve team member names on cards, use TeamMemberId for filtering
  - [x] 10.6 Add "Team" to navigation (ModuleNavigation ViewComponent)

- [x] 11. Checkpoint — Full integration test

- [ ] 12. Property-based tests
  - [ ]* 12.1 Activity feed immutability — entries cannot be modified after creation
  - [ ]* 12.2 Activity feed completeness — every mutation method records an entry
  - [ ]* 12.3 Team member tenant isolation
  - [ ]* 12.4 Deactivated members excluded from assignment dropdown

- [ ] 13. Final checkpoint

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"], "description": "Database migrations" },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5"], "description": "Entities and models" },
    { "id": 2, "tasks": ["3"], "description": "Checkpoint: compile" },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"], "description": "Repositories" },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4"], "description": "Services" },
    { "id": 5, "tasks": ["6"], "description": "Checkpoint: compile" },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5", "9.1"], "description": "Controller + DI" },
    { "id": 7, "tasks": ["8"], "description": "Checkpoint: compile" },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6"], "description": "Views" },
    { "id": 9, "tasks": ["11"], "description": "Integration test" },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3", "12.4"], "description": "Property tests" },
    { "id": 11, "tasks": ["13"], "description": "Final checkpoint" }
  ]
}
```

## Notes

- Activity Feed recording is non-blocking — if it fails, the primary action still succeeds
- The mockup at `.kiro/docs/mockups/lead-activity-feed.html` is the approved visual reference
- Team is part of the base Opportunities module — no additional PlanFeature gating
- The migration from AssignedToUserId → TeamMemberId preserves existing assignments
- Navigation: Opportunities → Team (new sub-item after Templates)
