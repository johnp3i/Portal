# Requirements Document

## Introduction

The Portal platform currently restricts each user to a single business via `ApplicationUser.BusinessId`. This feature introduces a multi-business membership model with granular, per-module permissions. Users will be mapped to one or more businesses through a `UserBusiness` junction table, and each mapping will carry module-level access grants via `UserBusinessPermission`. The system will enforce permissions at runtime through an authorization filter and hide inaccessible modules from the sidebar navigation.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application serving as the multi-tenant back-office platform
- **Membership_Database**: The SQL Server database (`Portal.Membership`) storing identity, invitation, and permission data
- **UserBusiness**: The junction entity mapping a user to a business, stored in `[membership].[UserBusiness]`
- **UserBusinessPermission**: The entity granting a specific module access level within a UserBusiness mapping, stored in `[membership].[UserBusinessPermission]`
- **Module**: A logical grouping of platform functionality corresponding to a database schema: `customer`, `quotation`, `invoice`, `revenue`, `purchase`, `vat`, `audit`
- **AccessLevel**: The degree of access granted to a module: `full` (read + write), `readonly` (read only), `none` (no access)
- **IPermissionService**: The service interface responsible for querying and caching a user's module permissions for the current business
- **ModuleAccess_Filter**: The ASP.NET Core authorization filter attribute that enforces module-level access on controller actions
- **ICurrentTenantService**: The existing scoped service that resolves the active BusinessId for the current request
- **Default_Business**: The business marked with `IsDefault = true` in a user's UserBusiness records, loaded on login
- **Soft_Delete**: The deactivation pattern using `IsActive = 0` and `DeactivatedAtUtc` timestamp instead of physical row deletion
- **SuperAdmin**: The administrative role that manages invitations and user permissions

## Requirements

### Requirement 1: User-to-Business Mapping Table

**User Story:** As a SuperAdmin, I want to assign users to multiple businesses, so that a single user account can operate across different business tenants.

#### Acceptance Criteria

1. THE Membership_Database SHALL contain a `[membership].[UserBusiness]` table with columns: `Id` (PK, INT IDENTITY), `UserId` (FK → AspNetUsers.Id, NVARCHAR(450)), `BusinessId` (FK → portal.Business.Id, INT), `IsDefault` (BIT, default 0), `IsActive` (BIT, default 1), `DeactivatedAtUtc` (DATETIME2, nullable), `CreatedAtUtc` (DATETIME2, default GETUTCDATE())
2. THE Membership_Database SHALL enforce a unique constraint on (`UserId`, `BusinessId`) in the UserBusiness table to prevent duplicate mappings
3. WHEN a user has multiple active UserBusiness records, THE Portal SHALL ensure exactly one record has `IsDefault = 1` for that user
4. THE Membership_Database SHALL enforce referential integrity from `UserBusiness.UserId` to `AspNetUsers.Id`

### Requirement 2: Module Permissions Table

**User Story:** As a SuperAdmin, I want to assign per-module access levels to each user-business mapping, so that users have granular control over what they can do within each business.

#### Acceptance Criteria

1. THE Membership_Database SHALL contain a `[membership].[UserBusinessPermission]` table with columns: `Id` (PK, INT IDENTITY), `UserBusinessId` (FK → UserBusiness.Id, INT), `Module` (NVARCHAR(50)), `AccessLevel` (NVARCHAR(20)), `IsActive` (BIT, default 1), `DeactivatedAtUtc` (DATETIME2, nullable), `CreatedAtUtc` (DATETIME2, default GETUTCDATE())
2. THE Membership_Database SHALL enforce a unique constraint on (`UserBusinessId`, `Module`) in the UserBusinessPermission table to prevent duplicate module grants
3. THE Membership_Database SHALL restrict `Module` values to: `customer`, `quotation`, `invoice`, `revenue`, `purchase`, `vat`, `audit`
4. THE Membership_Database SHALL restrict `AccessLevel` values to: `full`, `readonly`, `none`
5. THE Membership_Database SHALL enforce referential integrity from `UserBusinessPermission.UserBusinessId` to `UserBusiness.Id`

### Requirement 3: Soft-Delete Pattern

**User Story:** As a SuperAdmin, I want deactivated records to be preserved for audit purposes, so that historical data is never lost.

#### Acceptance Criteria

1. WHEN a UserBusiness record is deactivated, THE Portal SHALL set `IsActive` to 0 and `DeactivatedAtUtc` to the current UTC timestamp
2. WHEN a UserBusinessPermission record is deactivated, THE Portal SHALL set `IsActive` to 0 and `DeactivatedAtUtc` to the current UTC timestamp
3. THE Portal SHALL exclude records where `IsActive = 0` from all standard queries unless explicitly requested
4. THE Portal SHALL retain deactivated records in the database indefinitely for audit trail purposes

### Requirement 4: Invitation Flow with Module Permissions

**User Story:** As a SuperAdmin, I want to specify module access levels when inviting a user, so that the invited user receives appropriate permissions upon registration.

#### Acceptance Criteria

1. WHEN a SuperAdmin creates an invitation, THE Portal SHALL accept a list of module-access-level pairs alongside the email and businessId
2. WHEN an invited user completes registration, THE Portal SHALL create a UserBusiness record linking the user to the invitation's business with `IsDefault = 1`
3. WHEN an invited user completes registration, THE Portal SHALL create UserBusinessPermission records for each module-access-level pair specified in the invitation
4. IF no module permissions are specified in the invitation, THEN THE Portal SHALL assign `AccessLevel = none` for all modules by default
5. WHEN a SuperAdmin creates an invitation, THE Portal SHALL validate that each module name is one of: `customer`, `quotation`, `invoice`, `revenue`, `purchase`, `vat`, `audit`
6. WHEN a SuperAdmin creates an invitation, THE Portal SHALL validate that each access level is one of: `full`, `readonly`, `none`

### Requirement 5: Permission Service

**User Story:** As a developer, I want a centralized permission service, so that any part of the application can query a user's module access level for the current business.

#### Acceptance Criteria

1. THE IPermissionService SHALL expose a method to retrieve the AccessLevel for a given userId, businessId, and module name
2. THE IPermissionService SHALL return `none` when no active UserBusinessPermission record exists for the requested combination
3. THE IPermissionService SHALL only consider records where both UserBusiness.IsActive and UserBusinessPermission.IsActive are true
4. WHEN the IPermissionService is queried, THE Portal SHALL use the scoped ICurrentTenantService.CurrentBusinessId as the default businessId if none is explicitly provided

### Requirement 6: ModuleAccess Authorization Filter

**User Story:** As a developer, I want a declarative authorization attribute, so that controller actions are automatically protected by module-level permission checks.

#### Acceptance Criteria

1. THE ModuleAccess_Filter SHALL accept parameters for module name and minimum required AccessLevel (`readonly` or `full`)
2. WHEN a request reaches a controller action decorated with ModuleAccess_Filter, THE Portal SHALL query IPermissionService for the current user's access level on the specified module
3. IF the user's AccessLevel is `none`, THEN THE ModuleAccess_Filter SHALL return HTTP 403 Forbidden
4. IF the user's AccessLevel is `readonly` and the required level is `full`, THEN THE ModuleAccess_Filter SHALL return HTTP 403 Forbidden
5. IF the user's AccessLevel meets or exceeds the required level, THEN THE ModuleAccess_Filter SHALL allow the request to proceed
6. THE ModuleAccess_Filter SHALL bypass permission checks for users with the SuperAdmin role

### Requirement 7: Sidebar Navigation Filtering

**User Story:** As a user, I want the sidebar to only show modules I have access to, so that I am not confused by links to areas I cannot use.

#### Acceptance Criteria

1. WHEN the sidebar navigation is rendered, THE Portal SHALL query IPermissionService for all module access levels for the current user and business
2. THE Portal SHALL hide sidebar menu items for modules where the user's AccessLevel is `none`
3. THE Portal SHALL display sidebar menu items for modules where the user's AccessLevel is `readonly` or `full`
4. WHEN the user's permissions change, THE Portal SHALL reflect the updated sidebar on the next page load

### Requirement 8: CurrentTenantService Update

**User Story:** As a developer, I want ICurrentTenantService to resolve the BusinessId from the UserBusiness table, so that multi-business users have their active business correctly identified.

#### Acceptance Criteria

1. WHEN a user logs in, THE Portal SHALL resolve the BusinessId from the UserBusiness record where `IsDefault = 1` and `IsActive = 1` for that user
2. THE Portal SHALL store the resolved BusinessId as a claim in the user's authentication cookie
3. IF no active default UserBusiness record exists for the user, THEN THE ICurrentTenantService SHALL return 0 (maintaining existing fallback behavior)
4. THE ICurrentTenantService SHALL continue to read BusinessId from the `BusinessId` claim type for backward compatibility

### Requirement 9: Data Migration from Single-Business Model

**User Story:** As a platform operator, I want existing users to be migrated to the new multi-business model, so that the transition is seamless and no access is lost.

#### Acceptance Criteria

1. WHEN the migration runs, THE Portal SHALL create a UserBusiness record for each existing user that has a non-null `ApplicationUser.BusinessId`, with `IsDefault = 1` and `IsActive = 1`
2. WHEN the migration runs, THE Portal SHALL create UserBusinessPermission records granting `AccessLevel = full` for all seven modules for each migrated UserBusiness record
3. THE Portal SHALL not create UserBusiness records for users where `ApplicationUser.BusinessId` is null (SuperAdmin accounts)
4. AFTER the migration completes, THE Portal SHALL no longer rely on `ApplicationUser.BusinessId` for tenant resolution at runtime
