# Requirements Document

## Introduction

This feature adds two capabilities to the Opportunities module:

1. **Team** — A registry of people who can be assigned to leads. Team members can be standalone records (external agents, freelancers, partners) or linked to existing portal users. This replaces the current "AssignedToUserId" system with a proper team management layer.

2. **Activity Feed** — A chronological timeline of all actions performed on a lead, providing full audit trail and operational context without navigating to separate sections.

## Glossary

- **Team_Member**: A person registered in the team who can be assigned to leads. May or may not be a portal user.
- **Activity_Feed**: A chronological list of events/actions that occurred on a lead, displayed on the LeadDetail page.
- **Activity_Entry**: A single event in the feed (e.g., "Stage changed from New to Contacted").
- **Portal_User_Link**: An optional association between a Team Member and an existing portal user (via UserId). When linked, the member's name resolves from the portal user record.

## Requirements

### Requirement 1: Team Member Entity

**User Story:** As a business owner, I want to register team members who handle leads, so that I can assign leads to specific people and track accountability.

#### Acceptance Criteria

1. THE database SHALL contain a `[sales].[TeamMember]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), FirstName (NVARCHAR(100) NOT NULL), LastName (NVARCHAR(100) NULL), Email (NVARCHAR(200) NULL), PhoneNumber (NVARCHAR(50) NULL), Role (NVARCHAR(100) NULL), UserId (NVARCHAR(450) NULL — optional link to portal user), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. WHEN UserId is set, THE system SHALL resolve the member's display name from the portal user's profile when available.
3. WHEN UserId is NULL, THE system SHALL use FirstName + LastName as the display name.
4. THE system SHALL allow multiple team members per business.
5. THE system SHALL enforce uniqueness of Email within a business (partial unique index, excluding NULL).

### Requirement 2: Team Management UI

**User Story:** As a business owner, I want an interface to add, edit, and manage my team, so that I can maintain who is available for lead assignment.

#### Acceptance Criteria

1. THE Opportunities module navigation SHALL include a "Team" sub-item.
2. THE Team page SHALL display all team members with: Name, Email, Phone, Role, Status (Active/Inactive), Portal User (linked/not linked).
3. THE Team page SHALL allow creating a new member with: FirstName (required), LastName, Email, PhoneNumber, Role (free text, e.g., "Sales Agent", "Partner"), and optional portal user link (dropdown of existing business users).
4. THE Team page SHALL allow editing any field of an existing member.
5. THE Team page SHALL allow deactivating a member (soft — sets IsActive = 0).
6. THE Team page SHALL allow reactivating a previously deactivated member.
7. DEACTIVATED members SHALL NOT appear in the lead assignment dropdown but remain visible in the Team list and on already-assigned leads.

### Requirement 3: Lead Assignment to Team Member

**User Story:** As a business user, I want to assign a lead to a team member, so that responsibility is clear and trackable.

#### Acceptance Criteria

1. THE `[sales].[LeadRequest]` table SHALL reference team members via a new `TeamMemberId` (INT NULL FK to `[sales].[TeamMember]`) column, replacing the current `AssignedToUserId` approach.
2. THE Lead Detail page SHALL show the assigned team member's name (resolved from TeamMember record).
3. THE Lead Detail page SHALL provide an "Assign" dropdown listing all active team members for the business.
4. THE Pipeline filter "Assigned To" SHALL list team members instead of portal users.
5. THE Kanban card MAY show the assigned team member's initials.
6. WHEN a lead is unassigned, THE TeamMemberId SHALL be NULL and display "Unassigned".

### Requirement 4: Migration from AssignedToUserId

**User Story:** As a platform developer, I want to migrate existing lead assignments to the new team member system without losing data.

#### Acceptance Criteria

1. THE migration SHALL create TeamMember records for any existing distinct `AssignedToUserId` values in `[sales].[LeadRequest]`.
2. THE migration SHALL update `[sales].[LeadRequest]` rows to point to the newly created TeamMember records.
3. AFTER migration, THE old `AssignedToUserId` column MAY be retained as deprecated or removed.
4. THE migration SHALL be idempotent (safe to run multiple times).

### Requirement 5: Activity Feed Entity

**User Story:** As a platform developer, I want a dedicated table to store activity events per lead, so that the feed can be queried efficiently.

#### Acceptance Criteria

1. THE database SHALL contain a `[sales].[ActivityFeed]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), LeadRequestId (INT NOT NULL FK to LeadRequest), Action (NVARCHAR(50) NOT NULL), Description (NVARCHAR(500) NOT NULL), PerformedByUserId (NVARCHAR(450) NULL), PerformedByTeamMemberId (INT NULL FK), Metadata (NVARCHAR(MAX) NULL — JSON for structured data like old/new stage), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE table SHALL have a non-clustered index on (LeadRequestId, CreatedAtUtc DESC) for efficient timeline queries.
3. THE `Action` column SHALL use standardised values: "lead_created", "stage_changed", "lead_cancelled", "lead_reactivated", "response_logged", "meeting_scheduled", "meeting_cancelled", "proposal_linked", "invoice_linked", "marked_as_won", "assigned", "unassigned", "request_details_updated".

### Requirement 6: Activity Feed Recording

**User Story:** As a business user, I want every significant action on a lead to be automatically recorded, so that I have a complete history without manual effort.

#### Acceptance Criteria

1. THE system SHALL automatically write an ActivityFeed entry when any of the following occur:
   - Lead created
   - Stage changed (record old stage → new stage)
   - Lead cancelled (record reason)
   - Lead reactivated
   - Response logged (record channel and snippet)
   - Meeting scheduled (record subject and date)
   - Meeting cancelled
   - Proposal linked (record quotation reference)
   - Invoice linked (record invoice reference)
   - Marked as Won
   - Assigned to a team member (record member name)
   - Unassigned
   - Request details updated
2. EACH entry SHALL record who performed the action (PerformedByUserId from the authenticated user).
3. THE entries SHALL be immutable — no editing or deleting activity feed records.
4. THE system SHALL handle action recording failures gracefully (log error but don't block the primary action).

### Requirement 7: Activity Feed Display

**User Story:** As a business user, I want to see a chronological timeline of all actions on a lead, so that I understand its full history at a glance.

#### Acceptance Criteria

1. THE LeadDetail page SHALL include an "Activity Feed" section displaying all events for the lead.
2. THE feed SHALL be ordered by CreatedAtUtc descending (newest first).
3. EACH entry SHALL display: coloured dot/icon per action type, action label, timestamp (relative + absolute), description, and who performed it.
4. THE feed SHALL use the visual design from the approved mockup (`lead-activity-feed.html`): vertical timeline with coloured dots, card-style entries, user avatars.
5. THE feed SHALL be loaded via AJAX for performance (lazy load, paginated if > 20 entries).
6. THE feed section SHALL be collapsible (default: expanded).

### Requirement 8: Tenant Isolation

**User Story:** As a business user, I want team members and activity data scoped to my business.

#### Acceptance Criteria

1. ALL team member queries SHALL filter by BusinessId.
2. ALL activity feed queries SHALL filter by BusinessId.
3. THE assignment dropdown SHALL only show team members from the current business.
4. Activity entries SHALL only be visible on leads belonging to the current business.

### Requirement 9: Team Member on Pipeline Views

**User Story:** As a business user, I want to filter and view leads by team member across the pipeline views.

#### Acceptance Criteria

1. THE Lead Board filter "Assigned To" dropdown SHALL list active team members (replacing portal users).
2. THE Table view "Assigned" column SHALL show the team member name.
3. THE Kanban cards SHALL show the team member's initials in the avatar circle.
4. THE Lead Detail "Assigned To" field SHALL show the team member name with option to change.

### Requirement 10: Foundation Tier Availability

**User Story:** As a platform owner, I want this feature available to all plans since team management is basic operational functionality.

#### Acceptance Criteria

1. THE Team and Activity Feed features SHALL be available on all subscription tiers (Foundation, Professional, Enterprise).
2. NO PlanFeature gating SHALL be applied — these are part of the base Opportunities module.
