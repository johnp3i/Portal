# Implementation Plan: What's New Announcements

## Overview

This plan implements the What's New Announcements feature following the established Portal architecture (Controller → Service → Repository). Work is structured as: database layer first (SQL migration + EF Core entities), then DTOs/request models, then repository + service layer, then ViewComponents and admin controller with views, then AJAX dismiss endpoints, and finally UI integration into _Layout.cshtml and Home/Index.cshtml.

## Tasks

- [x] 1. Database migration and EF Core entity configuration
  - [x] 1.1 Create SQL migration: FeatureAnnouncements and UserAnnouncementDismissals tables
    - Create migration file `Portal.Database/Migrations/XXX_CreateFeatureAnnouncementsTables.sql`
    - `USE [Portal]` header
    - Create `[dbo].[FeatureAnnouncements]` table with columns: Id (INT IDENTITY PK), Title (NVARCHAR(200) NOT NULL), Summary (NVARCHAR(500) NOT NULL), DetailHtml (NVARCHAR(MAX) NOT NULL), ModuleKey (NVARCHAR(100) NULL), CtaLabel (NVARCHAR(100) NULL), CtaUrl (NVARCHAR(500) NULL), TargetPlanTier (NVARCHAR(50) NULL), IsActive (BIT NOT NULL DEFAULT 1), PublishedAtUtc (DATETIME NOT NULL), ExpiresAtUtc (DATETIME NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Create `[dbo].[UserAnnouncementDismissals]` table with columns: Id (INT IDENTITY PK), UserId (NVARCHAR(450) NOT NULL), FeatureAnnouncementId (INT NOT NULL FK to FeatureAnnouncements), DismissedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add UNIQUE constraint on (UserId, FeatureAnnouncementId)
    - Create nonclustered index IX_UserAnnouncementDismissals_UserId on UserId INCLUDE (FeatureAnnouncementId, DismissedAtUtc)
    - Create nonclustered index IX_FeatureAnnouncements_Visibility on (IsActive, PublishedAtUtc, ExpiresAtUtc) INCLUDE (TargetPlanTier)
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Create EF Core entity classes
    - Create `Portal.Infrastructure/Entities/FeatureAnnouncement.cs` with all properties per design
    - Create `Portal.Infrastructure/Entities/UserAnnouncementDismissal.cs` with all properties per design
    - _Requirements: 1.1, 1.2_

  - [x] 1.3 Add DbContext configuration for new entities
    - Add `DbSet<FeatureAnnouncement>` and `DbSet<UserAnnouncementDismissal>` to PortalDbContext
    - Configure entity mappings in `OnModelCreating`: table names, column types, precision, defaults (GETUTCDATE()), FK relationship, unique constraint
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. DTOs and request models
  - [x] 2.1 Create AnnouncementDto, AdminAnnouncementDto, and WhatsNewViewModel
    - Create `Portal.Infrastructure/Models/AnnouncementDto.cs` with properties: Id, Title, Summary, DetailHtml, ModuleKey, CtaLabel, CtaUrl, TargetPlanTier, PublishedAtUtc, IsDismissed, HasCta (computed)
    - Create `Portal.Infrastructure/Models/AdminAnnouncementDto.cs` with properties: Id, Title, Summary, DetailHtml, ModuleKey, CtaLabel, CtaUrl, TargetPlanTier, IsActive, PublishedAtUtc, ExpiresAtUtc, CreatedAtUtc, Status (computed: Active/Inactive/Expired/Scheduled)
    - Create `Portal.Web/Models/ViewComponents/WhatsNewViewModel.cs` with properties: Announcements (List<AnnouncementDto>), UnreadCount, BadgeText
    - _Requirements: 2.5, 3.2, 3.3, 6.1, 8.2_

  - [x] 2.2 Create CreateAnnouncementRequest and UpdateAnnouncementRequest
    - Create `Portal.Infrastructure/Models/CreateAnnouncementRequest.cs` with fields: Title, Summary, DetailHtml, ModuleKey, CtaLabel, CtaUrl, TargetPlanTier, IsActive, PublishedAtUtc, ExpiresAtUtc
    - Create `Portal.Infrastructure/Models/UpdateAnnouncementRequest.cs` with same fields plus Id
    - _Requirements: 6.2, 6.6_

- [x] 3. Repository layer
  - [x] 3.1 Create AnnouncementRepository
    - Create `Portal.Infrastructure/Repositories/AnnouncementRepository.cs`
    - Extend `GenericStoredProcedureRepository<FeatureAnnouncement>`
    - Methods: `GetVisibleAsync(DateTime utcNow, string? userPlanTier)` — returns active, published, non-expired announcements filtered by plan tier
    - `GetAllAsync()` — returns all announcements for admin (includes inactive/expired)
    - `GetByIdAsync(int id)` — returns single announcement
    - `InsertAsync(FeatureAnnouncement entity)` — inserts and returns generated Id using OUTPUT INSERTED.Id
    - `UpdateAsync(FeatureAnnouncement entity)` — updates all fields by Id
    - `GetDismissalsForUserAsync(string userId)` — returns all dismissal records for user
    - `DismissAsync(string userId, int announcementId)` — idempotent insert using IF NOT EXISTS pattern
    - `DismissAllAsync(string userId, List<int> announcementIds)` — bulk idempotent insert for multiple announcements
    - All queries use full table names (no aliases), SqlParameter with null-safety (`?? (object)DBNull.Value`)
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 5.1, 5.2, 5.3_

- [x] 4. Service layer
  - [x] 4.1 Create IAnnouncementService interface and AnnouncementService class
    - Create `Portal.Infrastructure/Services/IAnnouncementService.cs` with methods per design: GetVisibleForUserAsync, GetUnreadCountAsync, GetBannerAnnouncementAsync, DismissAsync, DismissAllAsync, GetAllForAdminAsync, GetByIdForAdminAsync, CreateAsync, UpdateAsync, ToggleActiveAsync
    - Create `Portal.Infrastructure/Services/AnnouncementService.cs`
    - Inject: AnnouncementRepository, IPlanCheckService
    - _Requirements: 2.1–2.5, 5.1–5.3, 6.1–6.6, 9.1–9.5_

  - [x] 4.2 Implement visibility filtering and plan tier logic
    - Implement `GetVisibleForUserAsync(string userId)` — resolve user plan tier via `IPlanCheckService.GetCurrentPlanNameAsync()`, call repository GetVisibleAsync, join with dismissals to set IsDismissed flag, order by PublishedAtUtc descending
    - Implement private `IsTierVisible(string? targetTier, string userTier)` — tier hierarchy: Starter/Foundation=1, Professional=2, Enterprise=3; NULL/All target = visible to all; user rank >= target rank = visible
    - Default to "Starter" if plan tier cannot be resolved
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 4.3 Implement unread count and banner logic
    - Implement `GetUnreadCountAsync(string userId)` — visible minus dismissed count
    - Implement `GetBannerAnnouncementAsync(string userId)` — returns most recent visible undismissed announcement or null
    - _Requirements: 3.2, 3.3, 3.4, 7.1, 7.4_

  - [x] 4.4 Implement dismiss methods
    - Implement `DismissAsync(string userId, int announcementId)` — calls repository DismissAsync, returns updated unread count
    - Implement `DismissAllAsync(string userId)` — gets all visible undismissed IDs, calls repository DismissAllAsync, returns 0
    - Both methods are idempotent (unique constraint handles duplicates gracefully)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 4.5 Implement admin CRUD methods
    - Implement `GetAllForAdminAsync()` — maps to AdminAnnouncementDto with computed Status
    - Implement `GetByIdForAdminAsync(int id)` — returns single item or null
    - Implement `CreateAsync(CreateAnnouncementRequest request)` — validates Title/Summary/PublishedAtUtc, maps to entity, inserts, returns ServiceResult<int>
    - Implement `UpdateAsync(UpdateAnnouncementRequest request)` — validates, fetches existing, updates, returns ServiceResult
    - Implement `ToggleActiveAsync(int id, bool isActive)` — updates IsActive flag
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 8.1, 8.2, 8.3_

- [x] 5. Checkpoint — Database + Service layer build verification
  - Ensure all migrations are syntactically correct, EF entities compile, service and repository methods build successfully.
  - Ask the user if questions arise.

- [x] 6. ViewComponents
  - [x] 6.1 Create WhatsNewViewComponent
    - Create `Portal.Web/ViewComponents/WhatsNewViewComponent.cs`
    - Inject IAnnouncementService, check authentication, get visible announcements, compute unread count, set BadgeText (">9" → "9+"), return View(model)
    - Return `Content(string.Empty)` if not authenticated or on exception (never break page layout)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 6.2 Create WhatsNew ViewComponent Razor view (Default.cshtml)
    - Create `Views/Shared/Components/WhatsNew/Default.cshtml`
    - Render sparkle icon with numeric badge (hidden when 0)
    - Render slide-out panel (hidden by default) with announcement list: Title, Summary, relative date, read/unread visual distinction
    - Expand on click to show DetailHtml, CTA button (if HasCta), ModuleKey info
    - "Mark all as read" action button
    - Inline JS: panel toggle, dismiss single (AJAX POST to /Home/AxPostDismissAnnouncement), dismiss all (AJAX POST to /Home/AxPostDismissAllAnnouncements), badge count update in DOM
    - AJAX pattern: BlockUI → fetch POST → BlockUI hide → update badge count (no SweetAlert2 for dismiss — quick operation)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.4, 5.5, 10.1, 10.2, 10.3, 10.4_

  - [x] 6.3 Create WhatsNewBannerViewComponent
    - Create `Portal.Web/ViewComponents/WhatsNewBannerViewComponent.cs`
    - Inject IAnnouncementService, check authentication, get banner announcement, return View(banner) or Content(string.Empty)
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 6.4 Create WhatsNewBanner ViewComponent Razor view (Default.cshtml)
    - Create `Views/Shared/Components/WhatsNewBanner/Default.cshtml`
    - Render dismissible banner card: Title, Summary, "Learn More" link (opens panel)
    - Dismiss button triggers AJAX POST to /Home/AxPostDismissAnnouncement with the banner announcement Id
    - AJAX pattern: BlockUI → fetch POST → BlockUI hide → hide banner element from DOM
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 7. Admin controller and views
  - [x] 7.1 Create AdminWhatsNewController
    - Create `Portal.Web/Controllers/AdminWhatsNewController.cs`
    - `[Authorize(Roles = "SuperAdmin")]` attribute
    - Page actions: `Index()` (list all announcements), `Create()` (show form), `Edit(int id)` (show form with data)
    - Form POST actions: `Create(CreateAnnouncementRequest model)` [HttpPost][ValidateAntiForgeryToken], `Edit(UpdateAnnouncementRequest model)` [HttpPost][ValidateAntiForgeryToken]
    - AJAX endpoints: `AxPostToggleActive(int id, bool isActive)` — BlockUI + reload pattern, `AxPostPreview(string html)` — returns rendered HTML preview
    - All form POST actions redirect back to Index on success, re-display form with validation errors on failure
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 8.1, 8.2, 8.3, 11.1, 11.2_

  - [x] 7.2 Create Admin Index view (list)
    - Create `Views/AdminWhatsNew/Index.cshtml`
    - Follow AdminCompliance pattern: table listing all announcements with columns: Title, Status badge (Active/Inactive/Expired/Scheduled), TargetPlanTier, PublishedAtUtc, ExpiresAtUtc
    - Toggle IsActive via AJAX (AxPostToggleActive) — BlockUI + reload (quick toggle operation)
    - Edit button per row linking to Edit action
    - "Create New Announcement" button linking to Create action
    - _Requirements: 6.1, 8.2_

  - [x] 7.3 Create Admin Create/Edit view (form)
    - Create `Views/AdminWhatsNew/Create.cshtml` and `Views/AdminWhatsNew/Edit.cshtml` (or shared partial)
    - Follow AdminCompliance table + form toggle pattern
    - Form fields: Title (text, required), Summary (textarea, required), DetailHtml (rich text editor / textarea), ModuleKey (dropdown of known modules), CtaLabel (text), CtaUrl (text), TargetPlanTier (dropdown: All/Starter/Professional/Enterprise), PublishedAtUtc (datetime picker, required), ExpiresAtUtc (datetime picker, optional), IsActive (checkbox/toggle)
    - Preview button calls AxPostPreview to render DetailHtml
    - Client-side validation: Title and Summary required, PublishedAtUtc required
    - Form submission is standard POST (not AJAX) since it has rich HTML content
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.6_

- [x] 8. Dismiss AJAX endpoints on HomeController
  - [x] 8.1 Add AxPostDismissAnnouncement endpoint to HomeController
    - Add `[HttpPost][Authorize]` method `AxPostDismissAnnouncement(int announcementId)`
    - Extract userId from `User.FindFirstValue(ClaimTypes.NameIdentifier)`
    - Call `IAnnouncementService.DismissAsync(userId, announcementId)`
    - Return `Json(new { success = true, unreadCount = updatedCount })` on success
    - Return `Json(new { success = false, message = "Failed to dismiss announcement." })` on error
    - try/catch with `(Exception ex)` pattern
    - _Requirements: 5.1, 5.3, 5.4, 11.4_

  - [x] 8.2 Add AxPostDismissAllAnnouncements endpoint to HomeController
    - Add `[HttpPost][Authorize]` method `AxPostDismissAllAnnouncements()`
    - Extract userId from claims principal
    - Call `IAnnouncementService.DismissAllAsync(userId)`
    - Return `Json(new { success = true, unreadCount = 0 })`
    - Return error JSON on failure
    - _Requirements: 5.2, 5.4, 11.4_

- [x] 9. UI integration
  - [x] 9.1 Integrate WhatsNewViewComponent into _Layout.cshtml
    - Add `@await Component.InvokeAsync("WhatsNew")` in the topbar right section near existing utility icons
    - Badge and panel render server-side (no layout shift)
    - _Requirements: 3.1, 3.5_

  - [x] 9.2 Integrate WhatsNewBannerViewComponent into Home/Index.cshtml
    - Add `@await Component.InvokeAsync("WhatsNewBanner")` above existing Briefing Card / KPI cards on the Dashboard
    - Banner renders server-side, hidden when no undismissed announcements exist
    - _Requirements: 7.1, 7.5_

  - [x] 9.3 Add admin navigation link for What's New management
    - Add "What's New" link to `/AdminWhatsNew` in admin sidebar/menu, visible only to SuperAdmin users
    - Position with other admin management links
    - _Requirements: 11.1_

- [x] 10. DI registration
  - [x] 10.1 Register AnnouncementRepository and IAnnouncementService in DI container
    - Add `services.AddScoped<AnnouncementRepository>()` in Program.cs or service registration extension
    - Add `services.AddScoped<IAnnouncementService, AnnouncementService>()` in Program.cs or service registration extension
    - _Requirements: 2.1, 3.1_

- [x] 11. Checkpoint — Full feature build verification
  - Ensure all components compile, views render without errors, DI container resolves correctly.
  - Verify: admin list loads, create form saves, dismiss endpoint returns JSON, badge renders on layout, banner renders on dashboard.
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 12. Property-based tests
  - [ ]* 12.1 Write property test for visibility filtering
    - **Property 1: Visibility Filtering Completeness**
    - Generate random FeatureAnnouncement instances with varying IsActive, PublishedAtUtc, ExpiresAtUtc values
    - Assert: only announcements where IsActive=true AND PublishedAtUtc<=now AND (ExpiresAtUtc is null OR ExpiresAtUtc>now) appear in results
    - **Validates: Requirements 2.1, 2.2, 1.5, 6.3, 6.4, 8.1, 8.3**

  - [ ]* 12.2 Write property test for plan tier hierarchy
    - **Property 2: Plan Tier Hierarchy Filtering**
    - Generate announcements with various TargetPlanTier values and users with various plan tiers
    - Assert: announcement visible iff user tier rank >= announcement target tier rank (Starter=1, Professional=2, Enterprise=3)
    - **Validates: Requirements 2.3, 9.1, 9.2**

  - [ ]* 12.3 Write property test for NULL tier universality
    - **Property 3: NULL Tier Universality**
    - Generate announcements with NULL or "All" TargetPlanTier and users on any plan
    - Assert: all such announcements are included in visible results (assuming visibility filter passes)
    - **Validates: Requirements 1.4, 2.4, 9.3**

  - [ ]* 12.4 Write property test for sort order
    - **Property 4: Sort Order Invariant**
    - Generate random sets of visible announcements
    - Assert: returned list is ordered by PublishedAtUtc descending
    - **Validates: Requirements 2.5**

  - [ ]* 12.5 Write property test for unread count accuracy
    - **Property 5: Unread Count Accuracy**
    - Generate N visible announcements and M dismissals (M<=N)
    - Assert: unread count = N - M; badge text = "9+" when count > 9; badge hidden when count = 0
    - **Validates: Requirements 3.2, 3.3, 3.4**

  - [ ]* 12.6 Write property test for dismissal idempotence
    - **Property 6: Dismissal Idempotence**
    - Generate user-announcement pairs, call dismiss multiple times
    - Assert: exactly one dismissal record exists regardless of call count
    - **Validates: Requirements 5.3**

  - [ ]* 12.7 Write property test for mark all coverage
    - **Property 7: Mark All Creates Complete Coverage**
    - Generate user with K visible undismissed announcements, call dismiss all
    - Assert: K new dismissal records created and resulting unread count = 0
    - **Validates: Requirements 5.2**

  - [ ]* 12.8 Write property test for panel open no side effects
    - **Property 8: Panel Open Has No Side Effects**
    - Call GetVisibleForUserAsync and verify no dismissal records created/modified/deleted
    - **Validates: Requirements 4.6**

  - [ ]* 12.9 Write property test for banner logic
    - **Property 9: Banner Shows Most Recent or Nothing**
    - Generate visible announcements with various dismissed states
    - Assert: GetBannerAnnouncementAsync returns null (all dismissed) or the announcement with max PublishedAtUtc among undismissed
    - **Validates: Requirements 7.1, 7.4**

  - [ ]* 12.10 Write property test for CTA rendering
    - **Property 10: CTA Rendering Biconditional**
    - Generate announcements with various CtaLabel/CtaUrl combinations
    - Assert: HasCta = true iff both CtaLabel non-empty AND CtaUrl non-empty
    - **Validates: Requirements 10.1, 10.3**

  - [ ]* 12.11 Write property test for admin validation
    - **Property 11: Admin Validation Rejects Invalid Input**
    - Generate CreateAnnouncementRequest with null/whitespace Title, Summary, or default PublishedAtUtc
    - Assert: service returns failure result without persisting data
    - **Validates: Requirements 6.6**

  - [ ]* 12.12 Write property test for status computation
    - **Property 12: Admin Status Label Computation**
    - Generate announcements with various IsActive, ExpiresAtUtc, PublishedAtUtc values
    - Assert: computed status is mutually exclusive and exhaustive (Inactive/Expired/Scheduled/Active)
    - **Validates: Requirements 8.2**

  - [ ]* 12.13 Write property test for plan tier fallback
    - **Property 13: Plan Tier Fallback**
    - Generate users with null/empty plan tier
    - Assert: system treats user as "Starter" — sees only NULL/All/Starter tier announcements
    - **Validates: Requirements 9.5**

  - [ ]* 12.14 Write property test for dismiss user isolation
    - **Property 14: Dismiss Endpoint User Isolation**
    - Generate dismiss requests
    - Assert: dismissal record uses authenticated userId from claims, never user-supplied input
    - **Validates: Requirements 11.4**

- [x] 13. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at logical boundaries
- Property tests use FsCheck (FsCheck.Xunit) with minimum 100 iterations per property
- The database is Portal (`USE [Portal]`), schema is `[dbo]`
- Admin controller uses form-based POST (not AJAX) for create/edit since it has rich HTML content
- Dismiss endpoints are AJAX (on HomeController) following BlockUI → fetch POST → BlockUI hide → update DOM pattern
- Admin toggle (AxPostToggleActive) uses BlockUI → AJAX → Unblock → Reload (quick toggle, no SweetAlert2)
- Admin views follow the same pattern as AdminCompliance (table + form toggle)
- All controller AJAX methods use `AxPost`/`AxGet` prefix convention
- Repository methods use full table names in SQL (no aliases)
- ViewComponents catch exceptions and return `Content(string.Empty)` — never break page layout
- UserId for dismissals comes from authenticated claims principal (never from user input)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2"] },
    { "id": 3, "tasks": ["3.1"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3", "4.4", "4.5"] },
    { "id": 6, "tasks": ["6.1", "6.3", "7.1"] },
    { "id": 7, "tasks": ["6.2", "6.4", "7.2", "7.3", "8.1", "8.2"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "10.1"] },
    { "id": 9, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5", "12.6", "12.7", "12.8", "12.9", "12.10", "12.11", "12.12", "12.13", "12.14"] }
  ]
}
```
