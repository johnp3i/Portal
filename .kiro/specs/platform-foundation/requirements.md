# Requirements Document

## Introduction

Platform Foundation (Module 0) is the foundational infrastructure layer for the Portal multi-tenant back-office platform. It establishes the ASP.NET Core MVC 8 web project, dependency injection configuration, authentication with invitation-only registration, shared UI layout, structured logging, base repository pattern, and tenant administration screens. All subsequent modules depend on this foundation being operational.

## Glossary

- **Portal_Web**: The ASP.NET Core MVC 8 web application project (`Portal.Web`) serving as the platform entry point.
- **Portal_Database**: The SQL Server database containing all business data across 8 schemas and 18 tables.
- **Membership_Database**: A separate SQL Server database used exclusively for ASP.NET Core Identity (users, roles, claims).
- **PortalDbContext**: The Entity Framework Core DbContext configured for the Portal database with global query filters for tenant isolation.
- **CurrentTenantService**: A scoped service that resolves the current tenant's BusinessId from the authenticated user's claims.
- **Super_Admin**: A platform-level administrator who can invite users, manage tenants, and grant module access.
- **Business**: The tenant entity representing a subscribing company within the platform.
- **BusinessProfile**: Configuration record holding company registration, VAT details, and contact information for a Business.
- **BusinessId_Claim**: A custom claim (`BusinessId`) added to the user's authentication token that identifies which tenant the user belongs to.
- **GenericStoredProcedureRepository**: A base class providing unified async SQL execution methods for all repositories.
- **Invitation_Token**: A unique, time-limited token generated when a Super Admin invites a user, used to validate the registration link.
- **MyChair_Design_System**: The UI design system specifying colours, typography, layout patterns, and component styles for the platform.
- **Serilog**: The structured logging framework used for application-wide logging with configurable sinks.

## Requirements

### Requirement 1: Web Project Structure

**User Story:** As a developer, I want a properly structured ASP.NET Core MVC 8 web project, so that all modules have a consistent foundation to build upon.

#### Acceptance Criteria

1. THE Portal_Web SHALL be an ASP.NET Core MVC 8 project targeting net8.0 with a reference to the Portal.Infrastructure class library.
2. THE Portal_Web SHALL organise code into Controllers, Services, Views, and Models folders following the MVC + Service Layer pattern.
3. THE Portal_Web SHALL configure the application pipeline with authentication, authorisation, static files, and MVC routing middleware in the correct order.

### Requirement 2: Database Context Registration

**User Story:** As a developer, I want PortalDbContext registered in the DI container with the correct connection string, so that all services can access the Portal database.

#### Acceptance Criteria

1. THE Portal_Web SHALL register PortalDbContext in the dependency injection container using SQL Server as the database provider.
2. THE Portal_Web SHALL read the Portal database connection string from User Secrets (not appsettings.json) in development environments.
3. WHEN PortalDbContext is resolved from the DI container, THE Portal_Web SHALL provide a scoped instance per HTTP request.

### Requirement 3: Tenant Service Registration

**User Story:** As a developer, I want ICurrentTenantService registered as a scoped service, so that EF Core global query filters can resolve the current tenant per request.

#### Acceptance Criteria

1. THE Portal_Web SHALL register ICurrentTenantService with CurrentTenantService as a scoped service in the DI container.
2. THE Portal_Web SHALL register IHttpContextAccessor as a singleton service to support tenant resolution from claims.
3. WHEN a request is processed, THE CurrentTenantService SHALL resolve the BusinessId from the authenticated user's BusinessId_Claim.

### Requirement 4: Identity Configuration

**User Story:** As a developer, I want ASP.NET Core Identity configured with a separate Membership database, so that authentication and authorisation are isolated from business data.

#### Acceptance Criteria

1. THE Portal_Web SHALL configure ASP.NET Core Identity using a dedicated MembershipDbContext connected to the Membership_Database.
2. THE Portal_Web SHALL read the Membership database connection string from User Secrets (not appsettings.json) in development environments.
3. THE Portal_Web SHALL configure Identity with password requirements, lockout settings, and cookie authentication appropriate for a business platform.
4. THE Portal_Web SHALL define a "SuperAdmin" role for platform-level administration.

### Requirement 5: Invitation-Only Registration

**User Story:** As a Super Admin, I want to invite users via email, so that only authorised personnel can access the platform.

#### Acceptance Criteria

1. WHEN a Super_Admin submits an invitation request with an email address and target BusinessId, THE Portal_Web SHALL generate a unique Invitation_Token and send an invitation email to the specified address.
2. THE Invitation_Token SHALL expire after 72 hours from generation.
3. WHEN a user navigates to the registration link containing a valid Invitation_Token, THE Portal_Web SHALL display a registration form pre-populated with the invited email address.
4. WHEN a user submits the registration form with a valid Invitation_Token, THE Portal_Web SHALL create the user account in the Membership_Database and associate the user with the specified BusinessId.
5. IF an Invitation_Token is expired or invalid, THEN THE Portal_Web SHALL display an error message and prevent registration.
6. IF a user attempts to register with an email address that already exists, THEN THE Portal_Web SHALL display an error message indicating the account already exists.
7. THE Portal_Web SHALL restrict the invitation functionality to authenticated users with the SuperAdmin role.

### Requirement 6: BusinessId Claim Injection

**User Story:** As a developer, I want the BusinessId claim added to the user's authentication token on login, so that ICurrentTenantService can resolve the correct tenant for every request.

#### Acceptance Criteria

1. WHEN a user successfully authenticates, THE Portal_Web SHALL add a BusinessId_Claim to the user's claims principal containing the user's associated BusinessId value.
2. THE Portal_Web SHALL implement a custom IUserClaimsPrincipalFactory or claims transformation to inject the BusinessId_Claim during sign-in.
3. IF a user has no associated BusinessId, THEN THE Portal_Web SHALL deny login and display an error indicating the account is not linked to a business.

### Requirement 7: Shared Layout

**User Story:** As a user, I want a consistent sidebar and topbar layout across all pages, so that navigation is predictable and follows the MyChair Design System.

#### Acceptance Criteria

1. THE Portal_Web SHALL provide a shared Razor layout (`_Layout.cshtml`) with a fixed sidebar for navigation and a topbar displaying the current user and business context.
2. THE Portal_Web SHALL apply MyChair_Design_System colours: Primary Blue #0D5EA6, background base #F7FAFC, secondary background #EEF4F8.
3. THE Portal_Web SHALL use Manrope font for headings and Inter font for body text as specified by the MyChair_Design_System.
4. THE Portal_Web SHALL render navigation items in the sidebar grouped by module, with active state highlighting.
5. THE Portal_Web SHALL display the authenticated user's name and current Business name in the topbar.

### Requirement 8: Structured Logging

**User Story:** As a developer, I want Serilog configured for structured logging, so that application events are captured in a queryable format for diagnostics and monitoring.

#### Acceptance Criteria

1. THE Portal_Web SHALL configure Serilog as the logging provider with structured JSON output.
2. THE Portal_Web SHALL enrich log entries with request context including CorrelationId, UserId, and BusinessId.
3. THE Portal_Web SHALL configure a Console sink for development and a File sink with daily rolling for all environments.
4. WHEN an unhandled exception occurs, THE Portal_Web SHALL log the exception with full stack trace and request context before returning an error response.

### Requirement 9: Generic Repository Base Class

**User Story:** As a developer, I want a GenericStoredProcedureRepository base class, so that all repositories share unified async SQL execution methods and consistent patterns.

#### Acceptance Criteria

1. THE GenericStoredProcedureRepository SHALL be a generic class accepting a type parameter constrained to reference types (`where T : class`).
2. THE GenericStoredProcedureRepository SHALL provide an `ExecuteStoredProcedure` method that executes a SQL query and returns a `List<T>` asynchronously.
3. THE GenericStoredProcedureRepository SHALL provide an `ExecuteSingleRecordStoredProcedure` method that executes a SQL query and returns a single `T` or null asynchronously.
4. THE GenericStoredProcedureRepository SHALL accept a DbContext via constructor injection and expose it as a protected field for derived repositories.

### Requirement 10: Business Administration

**User Story:** As a Super Admin, I want to create, view, edit, and deactivate Business tenants and their profiles, so that I can manage the platform's subscribing companies.

#### Acceptance Criteria

1. THE Portal_Web SHALL provide a Business list screen displaying all businesses with their Name, IsActive status, and CreatedAtUtc date.
2. WHEN a Super_Admin submits a new Business form, THE Portal_Web SHALL create a Business record with the provided Name and IsActive set to true.
3. WHEN a Super_Admin submits a BusinessProfile form for an existing Business, THE Portal_Web SHALL create or update the BusinessProfile record with company registration, VAT details, and contact information.
4. WHEN a Super_Admin deactivates a Business, THE Portal_Web SHALL set IsActive to false on the Business record.
5. THE Portal_Web SHALL validate that Business Name is unique across all tenants before creating or updating.
6. THE Portal_Web SHALL validate that VatPeriodLengthInMonths is one of the allowed values (1, 2, 3, 4, 6, 12) when saving a BusinessProfile.
7. THE Portal_Web SHALL restrict all Business administration screens to authenticated users with the SuperAdmin role.
