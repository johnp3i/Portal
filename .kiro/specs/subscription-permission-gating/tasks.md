# Implementation Plan: Subscription Permission Gating

## Overview

Implement a two-dimensional permission gating system for the Portal: plan-level (what the subscription allows) and user-level (what the business owner grants to each team member). The system uses two global `IAsyncAuthorizationFilter` instances plus an injectable `PlanCheckService` to enforce access control. The design extends existing entities (`Plan`, `PlanFeature`, `BusinessPlan`, `UserBusinessPermission`) rather than creating new tables.

## Tasks

- [x] 1. Database migrations and entity extensions
  - [x] 1.1 Create migration to add AccessLevel column to PlanFeature table
    - Add `AccessLevel NVARCHAR(20) NOT NULL DEFAULT 'full'` to `[dbo].[PlanFeature]`
    - Follow SQL script header rule: `USE [Portal]` at top
    - File: `Portal.Database/Migrations/0XX_AddAccessLevelToPlanFeature.sql` (next sequential number)
    - _Requirements: 1.2_

  - [x] 1.2 Create migration to add Status and TrialEndsAtUtc columns to BusinessPlan table
    - Add `Status NVARCHAR(20) NOT NULL DEFAULT 'active'` to `[dbo].[BusinessPlan]`
    - Add `TrialEndsAtUtc DATETIME2 NULL` to `[dbo].[BusinessPlan]`
    - _Requirements: 1.3_

  - [x] 1.3 Create migration to add GrantedByUserId column to UserBusinessPermission table
    - Add `GrantedByUserId NVARCHAR(450) NULL` to the `UserBusinessPermission` table
    - _Requirements: 1.4, 1.5_

  - [x] 1.4 Update EF Core entity models with new properties
    - Add `public string AccessLevel { get; set; } = "full";` to `PlanFeature` entity
    - Add `public string Status { get; set; } = "active";` to `BusinessPlan` entity
    - Add `public DateTime? TrialEndsAtUtc { get; set; }` to `BusinessPlan` entity
    - Add `public string? GrantedByUserId { get; set; }` to `UserBusinessPermission` entity
    - Update EF Core configurations with default value SQL expressions
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

  - [x] 1.5 Create seed migration for PlanFeature records (Starter, Professional, Enterprise modules)
    - Insert PlanFeature records for Starter plan (10 modules, all 'full')
    - Insert PlanFeature records for Professional plan (16 modules, all 'full')
    - Insert PlanFeature records for Enterprise plan (22 modules, all 'full')
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 1.6 Create data migration to assign all existing businesses to Professional plan
    - Insert a `BusinessPlan` record for every existing Business with PlanId pointing to Professional
    - Set Status = 'active', StartedAtUtc = GETUTCDATE(), ExpiresAtUtc = NULL
    - Use conditional logic to skip businesses that already have a BusinessPlan record
    - _Requirements: 2.4, 11.1, 11.2, 11.3_

- [x] 2. Extend PortalModules constants and create ModuleControllerMap
  - [x] 2.1 Add 13 new module keys to PortalModules.cs
    - Add constants: `PaymentLinkManual`, `PaymentReminderManual`, `PaymentLinkAuto`, `PaymentReminderAuto`, `Cashflow`, `Pnl`, `ExpenseInsights`, `Attachments`, `ClientPortal`, `ActivityTimeline`, `AuditLog`, `Api`, `Webhooks`, `MultiCurrency`
    - Update the `All` array to include all 22 module keys
    - Update `IsValid()` method accordingly
    - _Requirements: 8.1, 8.2_

  - [x] 2.2 Create ModuleControllerMap static helper
    - Create `Portal.Web/Filters/ModuleControllerMap.cs`
    - Define `Map` dictionary mapping all 22 module keys to their controller names
    - Implement `ResolveModule(string controllerName)` method
    - Include all mappings from the design document
    - _Requirements: 8.3, 3.1_

  - [x] 2.3 Refactor DemoPermissionFilter to use ModuleControllerMap
    - Replace the private `ModuleControllers` dictionary in `DemoPermissionFilter.cs` with a call to `ModuleControllerMap.ResolveModule()`
    - Verify existing DemoPermissionFilter tests still pass
    - _Requirements: 8.3_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement PlanCheckService
  - [x] 4.1 Create IPlanCheckService interface
    - Create `Portal.Infrastructure/Services/IPlanCheckService.cs`
    - Define methods: `IsModuleInPlanAsync`, `GetEffectiveAccessLevelAsync`, `GetPlanModulesAsync`, `GetRequiredPlanForModuleAsync`, `HasActiveSubscriptionAsync`, `IsOwnerAsync`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 4.2 Implement PlanCheckService
    - Create `Portal.Infrastructure/Services/PlanCheckService.cs`
    - Inject `ICurrentTenantService` for business context resolution
    - Inject `PortalDbContext` for PlanFeature queries via BusinessPlan.Plan.PlanFeatures
    - Inject `MembershipDbContext` for UserBusinessPermission queries
    - Implement per-request caching via `IHttpContextAccessor` and `HttpContext.Items`
    - Implement effective access level logic: `min(planLevel, userLevel)` with ordering none < readonly < full
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 4.3 Register PlanCheckService in DI container
    - Register `IPlanCheckService` → `PlanCheckService` as scoped in `Program.cs`
    - _Requirements: 5.5_

  - [x] 4.4 Write property test for effective access level computation (Property 7)
    - **Property 7: Effective access level equals the more restrictive of plan and user levels**
    - Generate all combinations from {full, readonly, none} × {full, readonly, none}
    - Verify result follows the "more restrictive wins" rule
    - **Validates: Requirements 5.2, 5.4**

  - [x] 4.5 Write property test for module key validation (Property 8)
    - **Property 8: Module key validation matches known module list**
    - Generate random strings (mix of valid module keys and arbitrary strings)
    - Verify `PortalModules.IsValid(s)` returns true iff `s` is in `PortalModules.All`
    - **Validates: Requirements 8.2**

- [x] 5. Implement PlanPermissionFilter
  - [x] 5.1 Create PlanPermissionFilter
    - Create `Portal.Web/Filters/PlanPermissionFilter.cs`
    - Implement `IAsyncAuthorizationFilter` with `Order = 1`
    - Skip if DemoInvitationId claim is present
    - Skip if controller is non-module (Home, Account, Demo, Admin, MyBusiness, Billing, SetupWizard)
    - Resolve module from controller name using `ModuleControllerMap.ResolveModule()`
    - Query business subscription via `IPlanCheckService`
    - If no active subscription → return SubscriptionInactive view (or JSON for AJAX)
    - If module not in plan → return PlanSoftGate view (or JSON for AJAX)
    - Store resolved plan permissions in `HttpContext.Items["PlanPermissions"]`
    - Use AJAX detection pattern from DemoPermissionFilter
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 6.1_

  - [x] 5.2 Write property test for plan filter module inclusion (Property 1)
    - **Property 1: Plan filter grants access if and only if the plan includes the module**
    - Generate random module + random plan config (with/without module)
    - Verify filter allows iff module is in plan
    - **Validates: Requirements 3.1, 3.2**

  - [x] 5.3 Write property test for non-module controller bypass (Property 2)
    - **Property 2: Non-module controllers bypass plan checks**
    - Generate random controller names from exempt list
    - Verify filter always allows
    - **Validates: Requirements 3.4**

  - [x] 5.4 Write property test for demo session bypass (Property 3)
    - **Property 3: Demo sessions bypass both plan and user permission filters**
    - Generate random controller + random DemoInvitationId claim value
    - Verify both filters skip checks entirely
    - **Validates: Requirements 3.3, 4.5**

- [x] 6. Implement UserPermissionFilter
  - [x] 6.1 Create UserPermissionFilter
    - Create `Portal.Web/Filters/UserPermissionFilter.cs`
    - Implement `IAsyncAuthorizationFilter` with `Order = 2`
    - Skip if DemoInvitationId claim is present
    - Skip if controller is non-module
    - Skip if user is business Owner (resolved via `IPlanCheckService.IsOwnerAsync`)
    - Query user's module permission via `IPlanCheckService.GetEffectiveAccessLevelAsync`
    - If no permission or AccessLevel == 'none' → return UserAccessDenied view (or JSON for AJAX)
    - If AccessLevel == 'readonly' and request is non-GET (excluding actions starting with "Get") → return ReadOnlyBlocked view/JSON 403
    - Set `HttpContext.Items["UserReadOnly"] = true` for readonly access on GET
    - Use AJAX detection pattern from DemoPermissionFilter
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 6.1_

  - [x] 6.2 Write property test for user filter access (Property 4)
    - **Property 4: User filter grants non-owner access if and only if a permission record exists with level other than 'none'**
    - Generate random user + random permission state (exists/none/full/readonly)
    - Verify filter behavior matches property
    - **Validates: Requirements 4.1, 4.2**

  - [x] 6.3 Write property test for readonly blocks non-GET (Property 5)
    - **Property 5: Readonly access blocks non-GET requests**
    - Generate random HTTP methods × readonly access level
    - Verify non-GET (excluding "Get" prefixed actions) are blocked, GET is allowed
    - **Validates: Requirements 4.3**

  - [x] 6.4 Write property test for owner bypass (Property 6)
    - **Property 6: Business owner always has full access to plan-permitted modules**
    - Generate random modules for owner user
    - Verify owner always gets full access without needing UserBusinessPermission
    - **Validates: Requirements 4.4**

- [x] 7. Register filters in Program.cs and create SoftGateViewModel
  - [x] 7.1 Create SoftGateViewModel
    - Create `Portal.Web/Models/SoftGateViewModel.cs`
    - Properties: `ModuleName`, `ModuleDisplayName`, `ModuleDescription`, `RequiredPlanName`, `CurrentPlanName`
    - _Requirements: 7.1_

  - [x] 7.2 Register PlanPermissionFilter and UserPermissionFilter in Program.cs
    - Add `options.Filters.Add<PlanPermissionFilter>(1);`
    - Add `options.Filters.Add<UserPermissionFilter>(2);`
    - Ensure DemoPermissionFilter remains at order 0
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Create soft-gate views
  - [x] 9.1 Create PlanSoftGate.cshtml view
    - Create `Portal.Web/Views/Shared/PlanSoftGate.cshtml`
    - Display feature name, description, and which plan includes it
    - Use informational/encouraging copy (no error styling)
    - Include upgrade call-to-action
    - Use MyChair Design System (glass card-pad pattern)
    - Model: `SoftGateViewModel`
    - _Requirements: 7.1, 7.4_

  - [x] 9.2 Create UserAccessDenied.cshtml view
    - Create `Portal.Web/Views/Shared/UserAccessDenied.cshtml`
    - Display message indicating user does not have access and should contact business owner
    - Use informational tone, not error styling
    - Model: `SoftGateViewModel`
    - _Requirements: 7.2, 7.4_

  - [x] 9.3 Create ReadOnlyBlocked.cshtml view
    - Create `Portal.Web/Views/Shared/ReadOnlyBlocked.cshtml`
    - Display message indicating read-only restrictions apply
    - Use informational tone
    - Model: `SoftGateViewModel`
    - _Requirements: 7.3, 7.4_

  - [x] 9.4 Create SubscriptionInactive.cshtml view
    - Create `Portal.Web/Views/Shared/SubscriptionInactive.cshtml`
    - Display message indicating the subscription is inactive/expired
    - Include renewal call-to-action
    - Use MyChair Design System (glass card-pad pattern)
    - Model: `SoftGateViewModel`
    - _Requirements: 3.5, 7.4_

  - [x] 9.5 Write property test for soft-gate view content (Property 10)
    - **Property 10: Soft-gate view contains module identification and required plan**
    - Generate random blocked modules
    - Verify returned SoftGateViewModel contains module display name and lowest tier plan name
    - **Validates: Requirements 7.1**

- [x] 10. SuperAdmin subscription management
  - [x] 10.1 Add subscription management actions to AdminController
    - Add `SubscriptionManagement()` GET action — returns list of businesses with current plan, status, expiry
    - Add `AxPostChangeBusinessPlan(int businessId, int planId)` — changes business plan, logs change
    - Add `AxPostChangeSubscriptionStatus(int businessPlanId, string status)` — changes status (active/trial/cancelled/expired)
    - Restrict with SuperAdmin role authorization
    - Follow existing controller pattern (service injection, try/catch with `ex` variable)
    - AJAX endpoints return `Json(new { success, message })`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 10.2 Create SubscriptionManagement.cshtml view
    - Create the view showing all businesses with plan, status, and expiry info
    - Include dropdown/modal for changing plan and status per business
    - Use BlockUI + SweetAlert2 for AJAX calls (no native alerts)
    - Use MyChair Design System (glass card-pad, topbar pattern)
    - Follow filter + table layout standard
    - _Requirements: 9.1, 9.2, 9.3_

- [x] 11. User permission management
  - [x] 11.1 Add user permission actions to MyBusinessController
    - Add `UserPermissions()` GET action — shows team members with module access grid
    - Add `AxPostGrantPermission(string userId, string module, string accessLevel)` — creates/updates UserBusinessPermission
    - Add `AxPostRevokePermission(string userId, string module)` — sets AccessLevel to 'none' or removes record
    - Validate: module must be in business plan, access level must not exceed plan level
    - Record `GrantedByUserId` on grant operations
    - Prevent modification of Owner's permissions
    - Follow existing controller pattern (try/catch with `ex` variable)
    - AJAX endpoints return `Json(new { success, message })`
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 11.2 Create UserPermissions.cshtml view
    - Create the view displaying all team members with a module access grid
    - Show plan modules as columns, users as rows, access levels as cell values
    - Include grant/revoke controls per user-module cell
    - Use BlockUI + SweetAlert2 for AJAX calls (no native alerts)
    - Use MyChair Design System (glass card-pad, topbar pattern)
    - Owner row should be visually distinguished and non-editable
    - _Requirements: 10.1, 10.5_

  - [x] 11.3 Write property test for permission grant bounded by plan (Property 9)
    - **Property 9: Permission grant is bounded by the business plan**
    - Generate random module + random plan config + random requested level
    - Verify grant succeeds only if module is in plan AND level does not exceed plan level
    - **Validates: Requirements 10.2, 10.3**

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck.Xunit)
- Unit tests validate specific examples and edge cases (xUnit)
- All AJAX endpoints follow the `AxPost`/`AxGet` naming convention
- All views use MyChair Design System with informational tone (no error styling for soft gates)
- SQL migrations follow the `USE [Portal]` header rule and sequential numbering
- The design extends existing entities — no new tables, only new columns on existing tables
- Filter ordering: DemoPermissionFilter(0) → PlanPermissionFilter(1) → UserPermissionFilter(2)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1"] },
    { "id": 1, "tasks": ["1.4", "1.5", "2.2"] },
    { "id": 2, "tasks": ["1.6", "2.3", "4.1", "7.1"] },
    { "id": 3, "tasks": ["4.2"] },
    { "id": 4, "tasks": ["4.3", "4.4", "4.5"] },
    { "id": 5, "tasks": ["5.1", "6.1"] },
    { "id": 6, "tasks": ["5.2", "5.3", "5.4", "6.2", "6.3", "6.4", "7.2"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 8, "tasks": ["9.5", "10.1", "11.1"] },
    { "id": 9, "tasks": ["10.2", "11.2", "11.3"] }
  ]
}
```
