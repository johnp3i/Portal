# Implementation Plan: Multi-Business Permissions

## Overview

Implement a multi-business membership model with granular per-module permissions. The plan follows a bottom-up approach: schema first, then entities, services, authorization filter, UI integration, and finally data migration.

## Tasks

- [x] 1. Database migrations
  - [x] 1.1 Create migration script for UserBusiness and UserBusinessPermission tables
    - Create file `Portal.Database/Migrations/Membership/003_CreateUserBusinessTables.sql`
    - Create `[membership].[UserBusiness]` table with PK, FK to AspNetUsers, unique constraint on (UserId, BusinessId), and index on (UserId, IsActive)
    - Create `[membership].[UserBusinessPermission]` table with PK, FK to UserBusiness, unique constraint on (UserBusinessId, Module), CHECK constraints on Module and AccessLevel, and index on (UserBusinessId, IsActive)
    - _Requirements: 1.1, 1.2, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.2 Create migration script to add ModulePermissionsJson to Invitations
    - Create file `Portal.Database/Migrations/Membership/004_AddModulePermissionsJsonToInvitations.sql`
    - ALTER TABLE `[dbo].[Invitations]` ADD `[ModulePermissionsJson]` NVARCHAR(MAX) NULL
    - _Requirements: 4.1_

- [x] 2. Entity classes and constants
  - [x] 2.1 Create UserBusiness entity
    - Create file `Portal.Infrastructure/Entities/Identity/UserBusiness.cs`
    - Properties: Id, UserId, BusinessId, IsDefault, IsActive, DeactivatedAtUtc, CreatedAtUtc, navigation to ApplicationUser
    - _Requirements: 1.1, 3.1_

  - [x] 2.2 Create UserBusinessPermission entity
    - Create file `Portal.Infrastructure/Entities/Identity/UserBusinessPermission.cs`
    - Properties: Id, UserBusinessId, Module, AccessLevel, IsActive, DeactivatedAtUtc, CreatedAtUtc, navigation to UserBusiness
    - _Requirements: 2.1, 3.2_

  - [x] 2.3 Create PortalModules and AccessLevels constants
    - Create file `Portal.Infrastructure/Constants/PortalModules.cs`
    - Create file `Portal.Infrastructure/Constants/AccessLevels.cs`
    - PortalModules: static string constants for all 7 modules, All array, IsValid method
    - AccessLevels: Full/ReadOnly/None constants, All array, IsValid method, MeetsRequirement method
    - _Requirements: 2.3, 2.4, 6.1_

  - [ ]* 2.4 Write property tests for AccessLevels.MeetsRequirement and validation
    - **Property 8: Access level hierarchy (MeetsRequirement)**
    - **Property 4: Module and AccessLevel validation rejects invalid values**
    - **Validates: Requirements 2.3, 2.4, 6.3, 6.4, 6.5**

  - [x] 2.5 Add ModulePermissionsJson property to existing Invitation entity
    - Modify `Portal.Infrastructure/Entities/Identity/Invitation.cs`
    - Add `public string? ModulePermissionsJson { get; set; }`
    - _Requirements: 4.1_

  - [x] 2.6 Create InvitationModulePermission DTO
    - Create file `Portal.Infrastructure/Models/InvitationModulePermission.cs`
    - Properties: Module, AccessLevel
    - _Requirements: 4.1, 4.3_

- [x] 3. MembershipDbContext updates
  - [x] 3.1 Add DbSets and entity configuration for UserBusiness and UserBusinessPermission
    - Modify `Portal.Infrastructure/Data/MembershipDbContext.cs`
    - Add `DbSet<UserBusiness> UserBusinesses` and `DbSet<UserBusinessPermission> UserBusinessPermissions`
    - Configure table mappings, keys, indexes, unique constraints, relationships, and max lengths in OnModelCreating
    - _Requirements: 1.1, 1.2, 1.4, 2.1, 2.2, 2.5_

- [x] 4. Permission service
  - [x] 4.1 Create IPermissionService interface
    - Create file `Portal.Infrastructure/Services/IPermissionService.cs`
    - Methods: GetAccessLevelAsync(userId, module, businessId?), GetAllAccessLevelsAsync(userId, businessId?)
    - _Requirements: 5.1, 5.4_

  - [x] 4.2 Create PermissionService implementation
    - Create file `Portal.Infrastructure/Services/PermissionService.cs`
    - Inject MembershipDbContext and ICurrentTenantService
    - Query UserBusinessPermissions filtering by IsActive on both UserBusiness and UserBusinessPermission
    - Return "none" when no matching record found
    - Use ICurrentTenantService.CurrentBusinessId as fallback when businessId is null
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 4.3 Write property tests for PermissionService
    - **Property 6: PermissionService returns "none" for inactive or missing records**
    - **Property 7: PermissionService tenant fallback equivalence**
    - **Validates: Requirements 3.3, 5.2, 5.3, 5.4**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. ModuleAccessAttribute authorization filter
  - [x] 6.1 Create ModuleAccessAttribute
    - Create file `Portal.Web/Security/ModuleAccessAttribute.cs`
    - Implement IAsyncAuthorizationFilter
    - Accept module name and required access level parameters
    - Bypass checks for SuperAdmin role
    - Return ForbidResult when access is insufficient or userId is missing
    - Resolve IPermissionService from HttpContext.RequestServices
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [ ]* 6.2 Write property test for SuperAdmin bypass
    - **Property 9: SuperAdmin bypasses all permission checks**
    - **Validates: Requirements 6.6**

- [x] 7. Updated BusinessClaimsPrincipalFactory
  - [x] 7.1 Update BusinessClaimsPrincipalFactory to resolve BusinessId from UserBusiness
    - Modify `Portal.Web/Security/BusinessClaimsPrincipalFactory.cs`
    - Inject MembershipDbContext
    - In GenerateClaimsAsync: query UserBusinesses for default active record first
    - Fall back to ApplicationUser.BusinessId if no UserBusiness record found
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 7.2 Write property test for claims factory
    - **Property 11: Claims factory resolves BusinessId from default UserBusiness**
    - **Validates: Requirements 8.1**

- [x] 8. Updated InvitationService
  - [x] 8.1 Update InvitationService to store ModulePermissionsJson on invitation creation
    - Modify `Portal.Infrastructure/Services/InvitationService.cs`
    - Serialize module permission list to JSON and store in ModulePermissionsJson column
    - Validate module names and access levels before saving
    - _Requirements: 4.1, 4.5, 4.6_

  - [x] 8.2 Update InvitationService registration flow to create UserBusiness and permissions
    - On registration completion: create UserBusiness record with IsDefault = true
    - Deserialize ModulePermissionsJson and create UserBusinessPermission records
    - If ModulePermissionsJson is null/empty/malformed, assign AccessLevel = "none" for all modules
    - _Requirements: 4.2, 4.3, 4.4_

  - [ ]* 8.3 Write property test for registration round-trip
    - **Property 5: Registration round-trip preserves invitation permissions**
    - **Validates: Requirements 4.2, 4.3**

- [x] 9. Updated InvitationController and view
  - [x] 9.1 Update InvitationController to accept module permission selections
    - Modify the invitation creation action to accept a list of module-access-level pairs
    - Pass module permission data to InvitationService
    - Validate inputs using PortalModules.IsValid and AccessLevels.IsValid
    - _Requirements: 4.1, 4.5, 4.6_

  - [x] 9.2 Update invitation creation view with module permission checkboxes
    - Add a section to the invitation form with checkboxes/dropdowns for each module
    - Each module row shows module name and access level selector (full/readonly/none)
    - Default selection: none for all modules
    - _Requirements: 4.1_

- [x] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. ModuleNavigationViewComponent and sidebar
  - [x] 11.1 Create ModuleNavigationViewComponent
    - Create file `Portal.Web/ViewComponents/ModuleNavigationViewComponent.cs`
    - Inject IPermissionService
    - Query all access levels for current user; SuperAdmin gets full access to all modules
    - Return view with permissions dictionary
    - _Requirements: 7.1, 7.3_

  - [x] 11.2 Create sidebar partial view for module navigation
    - Create view file `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml`
    - Render menu items only for modules where access level is not "none"
    - _Requirements: 7.2, 7.3_

  - [x] 11.3 Integrate ModuleNavigationViewComponent into layout sidebar
    - Replace static sidebar module links with `@await Component.InvokeAsync("ModuleNavigation")`
    - _Requirements: 7.1, 7.4_

  - [ ]* 11.4 Write property test for sidebar visibility
    - **Property 10: Sidebar visibility matches access level**
    - **Validates: Requirements 7.2, 7.3**

- [x] 12. DI registration
  - [x] 12.1 Register IPermissionService in Program.cs
    - Add `builder.Services.AddScoped<IPermissionService, PermissionService>();` to Program.cs
    - _Requirements: 5.1_

- [x] 13. Data migration script
  - [x] 13.1 Create data migration script for existing users
    - Create file `Portal.Database/Migrations/Membership/005_MigrateExistingUsersToUserBusiness.sql`
    - INSERT into UserBusiness from AspNetUsers where BusinessId IS NOT NULL and IsActive = 1, with IsDefault = 1
    - INSERT into UserBusinessPermission for all 7 modules with AccessLevel = 'full' for each migrated UserBusiness record
    - Do NOT create records for users with NULL BusinessId (SuperAdmin accounts)
    - _Requirements: 9.1, 9.2, 9.3_

  - [ ]* 13.2 Write property test for migration correctness
    - **Property 12: Migration creates correct records based on legacy BusinessId**
    - **Validates: Requirements 9.1, 9.2, 9.3**

- [x] 14. Apply ModuleAccessAttribute to existing controllers
  - [x] 14.1 Decorate existing controllers with ModuleAccessAttribute
    - Add `[ModuleAccess(PortalModules.Customer)]` to CustomerController
    - Add `[ModuleAccess(PortalModules.Quotation)]` to QuotationController
    - Add `[ModuleAccess(PortalModules.Invoice)]` to InvoiceController (if exists)
    - Add appropriate attributes to any other module controllers
    - Use `AccessLevels.Full` for write actions (Create, Edit, Delete) and `AccessLevels.ReadOnly` for read actions (Index, Detail)
    - _Requirements: 6.1, 6.2_

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The implementation language is C# as specified in the design
