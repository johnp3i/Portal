# Design Document: Opportunities Team & Activity Feed

## Overview

This feature adds Team Member management and an Activity Feed timeline to the Opportunities module. Team Members are people assignable to leads — they can be standalone records or optionally linked to portal users. The Activity Feed records every significant action on a lead as an immutable timeline.

### Key Design Decisions

1. **Team Members are standalone with optional portal user link** — A team member exists as its own record. If `UserId` is set, the system can resolve additional info from the portal user profile, but it's not required.
2. **LeadRequest gets TeamMemberId FK** — Replaces the current `AssignedToUserId` string column with a proper FK to `[sales].[TeamMember]`.
3. **Activity Feed is write-only** — Entries are never edited or deleted. Failures to write are logged but don't block the primary operation.
4. **Feed is per-lead** — Each entry links to a specific LeadRequestId. The feed is displayed on LeadDetail.
5. **Action types are standardised strings** — Enables filtering and icon/colour mapping without a lookup table.

## Data Model

### New Table: `[sales].[TeamMember]`

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | INT IDENTITY | NOT NULL | — | PK |
| BusinessId | INT | NOT NULL | — | FK → Business |
| FirstName | NVARCHAR(100) | NOT NULL | — | First name |
| LastName | NVARCHAR(100) | NULL | — | Last name |
| Email | NVARCHAR(200) | NULL | — | Email (unique per business) |
| PhoneNumber | NVARCHAR(50) | NULL | — | Phone |
| Role | NVARCHAR(100) | NULL | — | Free-text role label |
| UserId | NVARCHAR(450) | NULL | — | Optional link to portal user |
| IsActive | BIT | NOT NULL | 1 | Active/inactive |
| CreatedAtUtc | DATETIME | NOT NULL | GETUTCDATE() | Creation timestamp |

### New Table: `[sales].[ActivityFeed]`

| Column | Type | Nullable | Default | Description |
|--------|------|----------|---------|-------------|
| Id | INT IDENTITY | NOT NULL | — | PK |
| BusinessId | INT | NOT NULL | — | FK → Business |
| LeadRequestId | INT | NOT NULL | — | FK → LeadRequest |
| Action | NVARCHAR(50) | NOT NULL | — | Standardised action key |
| Description | NVARCHAR(500) | NOT NULL | — | Human-readable description |
| PerformedByUserId | NVARCHAR(450) | NULL | — | Portal user who performed it |
| PerformedByTeamMemberId | INT | NULL | — | Team member (if action by assigned member) |
| Metadata | NVARCHAR(MAX) | NULL | — | JSON for structured data |
| CreatedAtUtc | DATETIME | NOT NULL | GETUTCDATE() | Event timestamp |

### Modified Table: `[sales].[LeadRequest]`

| Column | Change | Description |
|--------|--------|-------------|
| TeamMemberId | NEW (INT NULL FK) | Replaces AssignedToUserId |
| AssignedToUserId | DEPRECATED | Retained for migration but no longer used by new code |

## Architecture

### Activity Feed Recording Pattern

Every service method that modifies a lead calls `_activityFeedService.RecordAsync()` in a fire-and-forget pattern:

```csharp
// In LeadRequestService.ChangeStageAsync:
await _leadRequestRepository.UpdateStageAsync(id, businessId, newStatusId);

// Record activity (non-blocking)
try
{
    await _activityFeedService.RecordAsync(new ActivityEntry
    {
        BusinessId = businessId,
        LeadRequestId = id,
        Action = "stage_changed",
        Description = $"Stage changed from {oldStageName} to {newStageName}",
        PerformedByUserId = userId,
        Metadata = JsonSerializer.Serialize(new { fromStageId = oldId, toStageId = newId })
    });
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to record activity for lead {LeadId}", id);
}
```

### Action Type → Visual Mapping

| Action | Dot Colour | Icon | Label |
|--------|-----------|------|-------|
| lead_created | Grey | + | Lead Created |
| stage_changed | Blue | → | Stage Changed |
| lead_cancelled | Red | ✕ | Lead Cancelled |
| lead_reactivated | Green | ↺ | Lead Reactivated |
| response_logged | Green | 💬 | Response Logged |
| meeting_scheduled | Gold | 📅 | Meeting Scheduled |
| meeting_cancelled | Red | 📅 | Meeting Cancelled |
| proposal_linked | Cyan | 📄 | Proposal Linked |
| invoice_linked | Cyan | 📄 | Invoice Linked |
| marked_as_won | Green | ✓ | Marked as Won |
| assigned | Blue | 👤 | Assigned |
| unassigned | Grey | 👤 | Unassigned |
| request_details_updated | Grey | ✏ | Details Updated |

### Team Member Resolution

```csharp
public string GetDisplayName(TeamMember member)
{
    if (!string.IsNullOrWhiteSpace(member.UserId))
    {
        // Resolve from portal user if linked
        var portalUser = await _userNameResolver.ResolveNameAsync(member.UserId);
        if (portalUser != null) return portalUser;
    }
    
    return string.IsNullOrWhiteSpace(member.LastName)
        ? member.FirstName
        : $"{member.FirstName} {member.LastName}";
}
```

## Service Interfaces

### ITeamMemberService

```csharp
public interface ITeamMemberService
{
    Task<ServiceResult> CreateAsync(CreateTeamMemberRequest request);
    Task<ServiceResult> UpdateAsync(UpdateTeamMemberRequest request);
    Task<ServiceResult> DeactivateAsync(int id);
    Task<ServiceResult> ActivateAsync(int id);
    Task<List<TeamMemberDto>> GetActiveAsync();
    Task<List<TeamMemberDto>> GetAllAsync();
    Task<TeamMemberDto?> GetByIdAsync(int id);
}
```

### IActivityFeedService

```csharp
public interface IActivityFeedService
{
    Task RecordAsync(ActivityEntry entry);
    Task<List<ActivityFeedDto>> GetByLeadAsync(int leadRequestId, int page = 1, int pageSize = 20);
}
```

## UI Components

### Team Page (`/Sales/Team`)

- Navigation: Opportunities → Team
- Table: Name, Email, Phone, Role, Portal User (badge), Status, Actions
- Create/Edit modal with optional portal user dropdown
- Standard tbl-action buttons (Edit, Deactivate/Activate)

### Activity Feed on LeadDetail

- Section below Lead Information
- Vertical timeline matching the approved mockup
- AJAX-loaded for performance
- Paginated (20 per page, "Load more" button)
- Collapsible (default expanded)

### Assignment Dropdown

- On LeadDetail: dropdown of active team members
- On Pipeline filter: same dropdown
- Shows "Unassigned" as default option

## Migration Strategy

1. Create `[sales].[TeamMember]` table
2. For each distinct `AssignedToUserId` in `[sales].[LeadRequest]`:
   - Create a TeamMember record with UserId = that value, FirstName resolved from membership DB
3. Add `TeamMemberId` column to `[sales].[LeadRequest]`
4. UPDATE LeadRequest SET TeamMemberId = (matched TeamMember.Id) WHERE AssignedToUserId IS NOT NULL
5. Future: deprecate/remove AssignedToUserId column
