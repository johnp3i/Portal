# Requirements Document

## Introduction

This document defines the requirements for the subscription-permission-gating feature — a plan-based and user-based permission gating system for the Portal. The system controls access to Portal modules based on the business's subscription tier (Starter, Professional, Enterprise) and the individual user's granted permissions within that business.

The enforcement architecture follows four layers: Authentication → Plan Check → User Permission → Access Level. The system reuses the same module key vocabulary and access level concepts ('full', 'readonly', 'none') already established by the DemoPermissionFilter, ensuring consistency across all permission mechanisms.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **Subscription_Plan**: A tier definition (Starter, Professional, Enterprise) that determines which modules a business may access
- **Business_Subscription**: The active association between a Business and a Subscription_Plan, including status and dates
- **Plan_Module_Permission**: A mapping record indicating whether a specific module is included in a given plan and at what access level
- **User_Module_Permission**: A record granting a specific user access to a specific module within their business, constrained to a subset of the plan's permissions
- **Module_Key**: A string identifier representing a discrete feature area (e.g., 'quotation', 'cashflow', 'pnl')
- **Access_Level**: One of 'full', 'readonly', or 'none' — defines the degree of access to a module
- **PlanPermissionFilter**: A global IAsyncAuthorizationFilter that checks plan-level module access
- **UserPermissionFilter**: A global IAsyncAuthorizationFilter that checks user-level module access within plan bounds
- **Plan_Check_Service**: An injectable service that controllers and views use to query plan/user permissions for conditional UI rendering
- **Owner**: The business owner user who always retains full access to all plan-permitted modules
- **Soft_Gate_View**: A friendly "upgrade to access this feature" page shown when a plan does not include a module
- **Non_Module_Controller**: Controllers exempt from plan checks (Home, Account, Demo, Admin, MyBusiness)
- **SuperAdmin**: A system administrator with elevated privileges to manage business subscriptions

## Requirements

### Requirement 1: Subscription Plan Data Model

**User Story:** As a system architect, I want subscription plan definitions stored in the database, so that the platform can reference plan tiers and their module permissions for authorization decisions.

#### Acceptance Criteria

1. THE Portal database SHALL contain a SubscriptionPlan table with columns for Id, Name, DisplayName, MaxUsers, MonthlyPrice, AnnualPrice, IsActive, and CreatedAtUtc
2. THE Portal database SHALL contain a PlanModulePermission table that maps each Subscription_Plan to its included modules with an AccessLevel value
3. THE Portal database SHALL contain a BusinessSubscription table that associates each Business with exactly one active Subscription_Plan including Status, StartedAtUtc, ExpiresAtUtc, and TrialEndsAtUtc
4. THE Portal database SHALL contain a UserModulePermission table that grants individual users access to specific modules within their business at a specified AccessLevel
5. WHEN a UserModulePermission record is created, THE Portal SHALL record the GrantedByUserId indicating who granted the permission
6. THE Portal database SHALL enforce referential integrity between SubscriptionPlan, PlanModulePermission, BusinessSubscription, UserModulePermission, Business, and AspNetUsers tables

### Requirement 2: Subscription Plan Seed Data

**User Story:** As a system administrator, I want three predefined subscription plans with correct module allocations, so that businesses can be assigned to the appropriate tier.

#### Acceptance Criteria

1. THE Portal database SHALL contain a Starter plan (€390/year) with modules: quotation, invoice, revenue, customer, purchase, vat, credit, products, payment_link_manual, payment_reminder_manual — all at 'full' access
2. THE Portal database SHALL contain a Professional plan (€790/year) that includes all Starter modules plus: payment_link_auto, payment_reminder_auto, cashflow, pnl, expense_insights, attachments — all at 'full' access
3. THE Portal database SHALL contain an Enterprise plan (€1490/year) that includes all Professional modules plus: client_portal, activity_timeline, audit_log, api, webhooks, multi_currency — all at 'full' access
4. WHEN the migration executes, THE Portal SHALL assign all existing businesses to the Professional plan with status 'active'

### Requirement 3: Plan Permission Filter

**User Story:** As a platform operator, I want the system to block access to modules not included in the business's subscription plan, so that tier boundaries are enforced consistently.

#### Acceptance Criteria

1. WHEN an authenticated non-demo user requests a module controller, THE PlanPermissionFilter SHALL resolve the user's business subscription and verify the requested module is included in the plan
2. IF the business's subscription plan does not include the requested module, THEN THE PlanPermissionFilter SHALL return a Soft_Gate_View explaining the feature and which plan includes it
3. WHILE a user session has a DemoInvitationId claim, THE PlanPermissionFilter SHALL skip plan checks entirely and defer to the existing DemoPermissionFilter
4. WHEN a request targets a Non_Module_Controller (Home, Account, Demo, Admin, MyBusiness), THE PlanPermissionFilter SHALL allow the request without plan checks
5. IF the business has no active Business_Subscription record, THEN THE PlanPermissionFilter SHALL return a Soft_Gate_View indicating the subscription is inactive

### Requirement 4: User Permission Filter

**User Story:** As a business owner, I want to control which team members can access which modules, so that I can manage internal access within the bounds of my subscription plan.

#### Acceptance Criteria

1. WHEN a non-owner user requests a module controller, THE UserPermissionFilter SHALL verify the user has a UserModulePermission record for that module with AccessLevel other than 'none'
2. IF the user has no UserModulePermission record for the requested module or their AccessLevel is 'none', THEN THE UserPermissionFilter SHALL return a view indicating access has not been granted
3. WHILE a user's AccessLevel for a module is 'readonly', THE UserPermissionFilter SHALL block non-GET requests and allow only data retrieval operations
4. WHEN the user is the business Owner, THE UserPermissionFilter SHALL grant full access to all plan-permitted modules without checking UserModulePermission records
5. WHILE a user session has a DemoInvitationId claim, THE UserPermissionFilter SHALL skip user permission checks entirely

### Requirement 5: Plan Check Service

**User Story:** As a developer, I want an injectable service that checks plan and user permissions programmatically, so that controllers and views can conditionally render UI elements based on access.

#### Acceptance Criteria

1. THE Plan_Check_Service SHALL expose a method to check if a given module is included in the current business's subscription plan
2. THE Plan_Check_Service SHALL expose a method to check if the current user has access to a given module and return the effective AccessLevel
3. THE Plan_Check_Service SHALL expose a method to retrieve all modules available to the current business's plan
4. WHEN the Plan_Check_Service is called, THE service SHALL combine plan permissions and user permissions to determine the effective access level (the more restrictive of the two)
5. THE Plan_Check_Service SHALL be injectable via dependency injection and usable in both controllers and Razor views

### Requirement 6: Filter Execution Order

**User Story:** As a system architect, I want the authorization filters to execute in a defined order, so that enforcement layers do not conflict and each check builds on the previous.

#### Acceptance Criteria

1. THE Portal SHALL execute authorization filters in this order: Authentication → DemoPermissionFilter → PlanPermissionFilter → UserPermissionFilter
2. WHEN the DemoPermissionFilter blocks a request, THE PlanPermissionFilter and UserPermissionFilter SHALL not execute
3. WHEN the PlanPermissionFilter blocks a request, THE UserPermissionFilter SHALL not execute

### Requirement 7: Soft-Gate Views

**User Story:** As a user on a lower-tier plan, I want to see a friendly explanation of gated features with an upgrade path, so that I understand what is available and feel encouraged rather than frustrated.

#### Acceptance Criteria

1. WHEN a plan check blocks access, THE Portal SHALL display a view showing the feature name, a brief description of what it does, and which plan includes it
2. WHEN a user permission check blocks access, THE Portal SHALL display a view indicating the user does not have access and should contact their business owner
3. WHILE a user has 'readonly' access and attempts a write operation, THE Portal SHALL display a message indicating read-only restrictions apply
4. THE soft-gate views SHALL not use error styling or error language — they SHALL use informational, encouraging copy with an upgrade call-to-action where appropriate

### Requirement 8: Module Key Registry

**User Story:** As a developer, I want all module keys defined in a single constants file, so that the permission system and all consumers reference consistent identifiers.

#### Acceptance Criteria

1. THE PortalModules constants class SHALL define all 22 module keys: quotation, invoice, revenue, customer, purchase, vat, credit, products, payment_link_manual, payment_reminder_manual, payment_link_auto, payment_reminder_auto, cashflow, pnl, expense_insights, attachments, client_portal, activity_timeline, audit_log, api, webhooks, multi_currency
2. THE PortalModules class SHALL provide a validation method that confirms whether a given string is a recognized module key
3. THE PlanPermissionFilter SHALL use the PortalModules constants to resolve which module a controller belongs to

### Requirement 9: SuperAdmin Subscription Management

**User Story:** As a SuperAdmin, I want to view and change business subscriptions, so that I can manage tier assignments and handle customer support scenarios.

#### Acceptance Criteria

1. WHEN a SuperAdmin navigates to the subscription management page, THE Portal SHALL display a list of all businesses with their current plan, status, and expiry information
2. WHEN a SuperAdmin changes a business's subscription plan, THE Portal SHALL update the BusinessSubscription record and log the change
3. WHEN a SuperAdmin changes a subscription status (active, trial, cancelled, expired), THE Portal SHALL update the record and the change takes effect immediately
4. THE subscription management UI SHALL be accessible only to users with SuperAdmin role

### Requirement 10: User Permission Management

**User Story:** As a business owner, I want to grant and revoke module access for my team members, so that I can control who sees what within the bounds of my plan.

#### Acceptance Criteria

1. WHEN a business owner navigates to user permission settings, THE Portal SHALL display all team members with their current module access levels
2. WHEN a business owner grants a module permission to a user, THE Portal SHALL create a UserModulePermission record only if the module is included in the business's plan
3. IF a business owner attempts to grant a module permission that exceeds the plan's access level, THEN THE Portal SHALL reject the operation and display a message explaining the plan limitation
4. WHEN a business owner revokes a user's module access, THE Portal SHALL set the AccessLevel to 'none' or remove the UserModulePermission record
5. THE user permission management UI SHALL prevent any modification to the Owner's permissions

### Requirement 11: Migration of Existing Businesses

**User Story:** As a platform operator, I want all existing businesses automatically assigned to the Professional plan when the feature launches, so that no existing functionality is lost during the transition.

#### Acceptance Criteria

1. WHEN the migration script runs, THE Portal SHALL create a BusinessSubscription record for every existing Business with SubscriptionPlanId pointing to the Professional plan
2. WHEN the migration creates BusinessSubscription records, THE Portal SHALL set Status to 'active' and StartedAtUtc to the current UTC time
3. WHEN the migration creates BusinessSubscription records, THE Portal SHALL set ExpiresAtUtc to NULL indicating no expiry
