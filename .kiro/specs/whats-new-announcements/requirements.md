# Requirements: What's New Announcements

## Introduction

The What's New Announcements feature is a lightweight, non-intrusive system for educating Portal users about newly released features. It enables SuperAdmins to create rich announcements (with descriptions, examples, screenshots, and CTAs) and surfaces them to users through a topbar icon with an unread count badge. Users can browse announcements in a dedicated panel and dismiss them once read.

Announcements are plan-aware — content can be targeted to specific subscription tiers so users only see announcements relevant to their plan. The system auto-expires announcements after a configurable date. An optional dashboard banner highlights the most important or recent announcement.

This is a platform-level utility available to all users (not plan-gated), designed to feel helpful and educational rather than promotional.

## Glossary

- **Announcement**: A database-backed record containing a title, summary, rich HTML detail, optional CTA, and lifecycle metadata (publish/expiry dates).
- **Dismissal**: A record indicating that a specific user has read/acknowledged a specific announcement.
- **Unread Count**: The number of active, non-expired announcements that a user has not yet dismissed.
- **CTA (Call to Action)**: An optional button within an announcement linking to a relevant page or module.
- **Plan Tier**: The user's subscription level (e.g., Starter, Professional, Enterprise) used to filter which announcements are visible.
- **ModuleKey**: An optional reference linking an announcement to a specific Portal module for contextual navigation.
- **Dashboard Banner**: A dismissible card on the Dashboard page highlighting the most recent or important announcement.
- **Announcement_Service**: The service layer responsible for retrieving, filtering, and managing announcement data.
- **Announcement_Panel**: The user-facing UI component that lists all visible announcements when the topbar icon is clicked.
- **Admin_UI**: The SuperAdmin interface for creating, editing, publishing, and managing announcements.

## Requirements

### Requirement 1: Announcement Data Model

**User Story:** As a developer, I want a structured data model for feature announcements, so that announcements can be stored, queried, and managed reliably.

#### Acceptance Criteria

1. THE database SHALL contain a FeatureAnnouncements table with columns: Id (INT, PK, IDENTITY), Title (NVARCHAR(200), NOT NULL), Summary (NVARCHAR(500), NOT NULL), DetailHtml (NVARCHAR(MAX), NOT NULL), ModuleKey (NVARCHAR(100), NULL), CtaLabel (NVARCHAR(100), NULL), CtaUrl (NVARCHAR(500), NULL), TargetPlanTier (NVARCHAR(50), NULL), IsActive (BIT, NOT NULL, DEFAULT 1), PublishedAtUtc (DATETIME, NOT NULL), ExpiresAtUtc (DATETIME, NULL), CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE()).
2. THE database SHALL contain a UserAnnouncementDismissals table with columns: Id (INT, PK, IDENTITY), UserId (INT, NOT NULL, FK to Users), FeatureAnnouncementId (INT, NOT NULL, FK to FeatureAnnouncements), DismissedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE()), CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE()).
3. THE UserAnnouncementDismissals table SHALL enforce a unique constraint on the combination of UserId and FeatureAnnouncementId.
4. WHEN TargetPlanTier is NULL, THE announcement SHALL be visible to all plan tiers.
5. WHEN ExpiresAtUtc is NULL, THE announcement SHALL remain visible indefinitely until manually deactivated.

### Requirement 2: Announcement Visibility and Filtering

**User Story:** As a Portal user, I want to see only announcements relevant to my plan and that are currently active, so that the content is always useful and not cluttered.

#### Acceptance Criteria

1. THE Announcement_Service SHALL return only announcements where IsActive is true AND PublishedAtUtc is in the past or present.
2. WHEN an announcement has an ExpiresAtUtc value in the past, THE Announcement_Service SHALL exclude that announcement from results.
3. WHEN an announcement has a TargetPlanTier value, THE Announcement_Service SHALL include it only for users whose subscription plan matches that tier or a higher tier.
4. WHEN an announcement has a NULL TargetPlanTier, THE Announcement_Service SHALL include it for all users regardless of plan.
5. THE Announcement_Service SHALL order announcements by PublishedAtUtc descending (newest first).

### Requirement 3: Unread Count and Topbar Badge

**User Story:** As a Portal user, I want to see a badge on the topbar indicating unread announcements, so that I know when new feature information is available without being interrupted.

#### Acceptance Criteria

1. THE topbar SHALL display a "What's New" icon (sparkle or lightbulb style) in the right section near existing utility icons.
2. WHEN the user has one or more visible announcements without a corresponding dismissal record, THE icon SHALL display a numeric badge showing the unread count.
3. WHEN the unread count exceeds 9, THE badge SHALL display "9+".
4. WHEN all visible announcements have been dismissed by the user, THE badge SHALL be hidden.
5. THE unread count SHALL be computed server-side during page render to avoid layout shift.

### Requirement 4: Announcement Panel

**User Story:** As a Portal user, I want to click the What's New icon and see a panel listing all current announcements, so that I can browse feature updates at my convenience.

#### Acceptance Criteria

1. WHEN the user clicks the What's New icon, THE system SHALL display the Announcement_Panel as a slide-out panel or dropdown overlay.
2. THE Announcement_Panel SHALL list all visible announcements showing Title, Summary, and PublishedAtUtc formatted as a relative date (e.g., "3 days ago").
3. EACH announcement in the panel SHALL visually distinguish between read (dismissed) and unread items using font weight or a dot indicator.
4. WHEN the user clicks on an announcement in the list, THE panel SHALL expand to show the full DetailHtml content, optional CTA button, and optional ModuleKey link.
5. THE Announcement_Panel SHALL include a "Mark all as read" action that dismisses all currently visible announcements for the user.
6. WHEN the panel is opened, THE system SHALL NOT automatically mark announcements as read — the user must explicitly dismiss them.

### Requirement 5: Announcement Dismissal

**User Story:** As a Portal user, I want to mark announcements as read so that the unread badge reflects only genuinely new information.

#### Acceptance Criteria

1. WHEN the user clicks a "Dismiss" or "Mark as read" control on a single announcement, THE system SHALL create a UserAnnouncementDismissals record for that user and announcement.
2. WHEN the user clicks "Mark all as read", THE system SHALL create dismissal records for all currently visible and undismissed announcements.
3. IF a dismissal record already exists for a user-announcement pair, THE system SHALL not create a duplicate (idempotent operation).
4. WHEN a dismissal is recorded, THE topbar badge count SHALL update without requiring a full page reload.
5. THE dismiss action SHALL use the standard AJAX pattern: BlockUI → fetch POST → BlockUI hide → update badge count in DOM.

### Requirement 6: Admin Announcement Management

**User Story:** As a SuperAdmin, I want to create, edit, and manage feature announcements, so that I can communicate new features to users effectively.

#### Acceptance Criteria

1. THE Admin_UI SHALL provide a list view of all announcements (active and inactive) with columns: Title, Status (Active/Inactive/Expired), TargetPlanTier, PublishedAtUtc, ExpiresAtUtc.
2. THE Admin_UI SHALL provide a create/edit form with fields: Title, Summary, DetailHtml (rich text editor), ModuleKey (dropdown of known modules), CtaLabel, CtaUrl, TargetPlanTier (dropdown: All, Starter, Professional, Enterprise), PublishedAtUtc (datetime picker), ExpiresAtUtc (datetime picker, optional), IsActive (toggle).
3. WHEN a SuperAdmin saves an announcement with PublishedAtUtc in the future, THE announcement SHALL not appear to users until that date arrives.
4. WHEN a SuperAdmin sets IsActive to false, THE announcement SHALL immediately stop appearing to users regardless of dates.
5. THE Admin_UI SHALL allow a SuperAdmin to preview the DetailHtml content before publishing.
6. THE Admin_UI SHALL validate that Title and Summary are provided, and that PublishedAtUtc is a valid date, before allowing save.

### Requirement 7: Dashboard Banner Integration

**User Story:** As a Portal user, I want to see the most important recent announcement highlighted on my Dashboard, so that critical feature updates are visible even if I don't check the What's New panel.

#### Acceptance Criteria

1. THE Dashboard SHALL display a dismissible banner card showing the most recent visible and undismissed announcement for the user.
2. THE banner SHALL display the announcement Title, Summary, and a "Learn More" link that opens the Announcement_Panel to that announcement's detail.
3. WHEN the user dismisses the banner, THE system SHALL record a dismissal for that announcement (same as panel dismissal) and hide the banner.
4. WHEN no undismissed announcements exist for the user, THE Dashboard SHALL not render the banner section.
5. THE banner SHALL appear below the topbar and above other Dashboard content (Briefing Card, KPI cards).

### Requirement 8: Announcement Expiry Handling

**User Story:** As a SuperAdmin, I want announcements to automatically disappear after their expiry date, so that outdated information doesn't clutter the user experience.

#### Acceptance Criteria

1. WHEN the current UTC time exceeds an announcement's ExpiresAtUtc, THE Announcement_Service SHALL exclude it from all user-facing queries.
2. THE Admin_UI SHALL still display expired announcements in the management list with an "Expired" status label.
3. WHEN a SuperAdmin edits an expired announcement and sets a new future ExpiresAtUtc, THE announcement SHALL become visible to users again (if IsActive is true and PublishedAtUtc is in the past).
4. THE expiry check SHALL be performed at query time — no background job is required for expiry.

### Requirement 9: Plan-Aware Targeting

**User Story:** As a SuperAdmin, I want to target announcements to specific plan tiers, so that users only see announcements about features available to them.

#### Acceptance Criteria

1. WHEN a SuperAdmin sets TargetPlanTier to "Professional", THE announcement SHALL be visible to users on Professional and Enterprise plans but not Starter plans.
2. WHEN a SuperAdmin sets TargetPlanTier to "Enterprise", THE announcement SHALL be visible only to users on the Enterprise plan.
3. WHEN a SuperAdmin sets TargetPlanTier to "All" or leaves it NULL, THE announcement SHALL be visible to users on all plans.
4. THE Announcement_Service SHALL resolve the current user's plan tier from their active subscription and filter announcements accordingly.
5. IF a user's plan tier cannot be determined, THE Announcement_Service SHALL treat the user as having the lowest tier (Starter) for filtering purposes.

### Requirement 10: CTA Button and Module Navigation

**User Story:** As a Portal user, I want announcements to include a direct link to try the announced feature, so that I can easily navigate to it.

#### Acceptance Criteria

1. WHEN an announcement has both CtaLabel and CtaUrl populated, THE announcement detail view SHALL render a styled button with the CtaLabel text linking to CtaUrl.
2. WHEN an announcement has a ModuleKey populated, THE announcement detail view SHALL display the module name as contextual information (e.g., "Related module: Compliance").
3. WHEN either CtaLabel or CtaUrl is empty, THE system SHALL not render a CTA button for that announcement.
4. THE CTA button SHALL open the target URL within the Portal (same window navigation), not in a new tab.

### Requirement 11: Access Control

**User Story:** As a platform operator, I want announcement management restricted to SuperAdmins, so that regular users cannot create or modify announcements.

#### Acceptance Criteria

1. THE Admin_UI for announcement management SHALL be accessible only to users with the SuperAdmin role.
2. IF a non-SuperAdmin user attempts to access the announcement management URL directly, THE system SHALL return a 403 Forbidden response.
3. THE user-facing announcement panel and badge SHALL be available to all authenticated users regardless of role.
4. THE dismiss endpoints SHALL verify that the authenticated user is dismissing announcements for their own account only.
