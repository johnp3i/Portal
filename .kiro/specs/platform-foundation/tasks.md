# Implementation Plan: Platform Foundation

## Overview

Establish the Portal.Web MVC 8 project with dual-database configuration, ASP.NET Core Identity, invitation-only registration, shared layout, structured logging, generic repository base class, and business administration screens. Each task builds incrementally on the previous, ensuring no orphaned code.

## Tasks

- [x] 1. Create Portal.Web project and configure infrastructure
  - [x] 1.1 Create the Portal.Web ASP.NET Core MVC 8 project with project reference to Portal.Infrastructure
    - Create `src/Portal.Web/Portal.Web.csproj` targeting net8.0 with `<ProjectReference>` to `../Portal.Infrastructure/Portal.Infrastructure.csproj`
    - Add NuGet packages: `Serilog.AspNetCore`, `Serilog.Enrichers.CorrelationId`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
    - Create folder structure: Controllers/, Services/, Views/, Models/, Security/
    - Create a minimal `Program.cs` that builds and runs (empty pipeline for now)
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Create MembershipDbContext, ApplicationUser, and Invitation entity
    - Create `src/Portal.Infrastructure/Entities/Identity/ApplicationUser.cs` extending `IdentityUser` with `BusinessId`, `FirstName`, `LastName`, `IsActive`, `CreatedAtUtc`
    - Create `src/Portal.Infrastructure/Entities/Identity/Invitation.cs` with `Id`, `Email`, `BusinessId`, `Token`, `CreatedAtUtc`, `ExpiresAtUtc`, `IsUsed`, `CreatedByUserId`
    - Create `src/Portal.Infrastructure/Data/MembershipDbContext.cs` inheriting `IdentityDbContext<ApplicationUser, IdentityRole, string>` with `DbSet<Invitation>` and fluent configuration (unique index on Token, max lengths)
    - Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package reference to `Portal.Infrastructure.csproj`
    - _Requirements: 4.1, 5.1, 5.2_

  - [x] 1.3 Configure Program.cs with DI registration and middleware pipeline
    - Register `PortalDbContext` with SQL Server provider, connection string from User Secrets key `ConnectionStrings:PortalDb`
    - Register `MembershipDbContext` with SQL Server provider, connection string from User Secrets key `ConnectionStrings:MembershipDb`
    - Configure ASP.NET Core Identity with `ApplicationUser`, `IdentityRole`, password policy (8+ chars, digit, uppercase, non-alphanumeric), lockout (5 attempts, 15 min), cookie auth (login path `/Account/Login`, access denied `/Account/AccessDenied`, 8h expiry, sliding)
    - Register `IHttpContextAccessor` as singleton
    - Register `ICurrentTenantService` / `CurrentTenantService` as scoped
    - Register `IBusinessService` / `BusinessService` as scoped
    - Register `IInvitationService` / `InvitationService` as scoped
    - Register `IEmailService` / `StubEmailService` as scoped
    - Configure middleware pipeline in order: StaticFiles → SerilogRequestLogging → Routing → Authentication → Authorization → MapControllerRoute
    - Add global exception handler: `app.UseExceptionHandler("/Home/Error")`
    - _Requirements: 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2, 4.3_

- [x] 2. Checkpoint - Verify project compiles
  - Ensure the solution compiles with `dotnet build`. Ask the user if questions arise.

- [x] 3. Implement GenericStoredProcedureRepository and BusinessRepository
  - [x] 3.1 Create GenericStoredProcedureRepository base class
    - Create `src/Portal.Infrastructure/Repositories/GenericStoredProcedureRepository.cs`
    - Generic class `GenericStoredProcedureRepository<T> where T : class`
    - Constructor accepts `DbContext`, stores as `protected readonly DbContext _context`
    - Method `ExecuteStoredProcedure(string sqlQuery, params object[] parameters)` returns `Task<List<T>>` using `FromSqlRaw`
    - Method `ExecuteSingleRecordStoredProcedure(string sqlQuery, params object[] parameters)` returns `Task<T?>` using `FromSqlRaw` + `FirstOrDefault()`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [ ]* 3.2 Write property test for GenericStoredProcedureRepository
    - **Property 1: Tenant resolution from claims**
    - **Validates: Requirements 3.3**

  - [x] 3.3 Create BusinessRepository
    - Create `src/Portal.Infrastructure/Repositories/BusinessRepository.cs` extending `GenericStoredProcedureRepository<Business>`
    - Implement methods: `GetAllAsync()`, `GetByIdAsync(int id)`, `InsertAsync(Business)`, `UpdateAsync(Business)`, `IsNameUniqueAsync(string name, int? excludeId)`
    - Use `ExecuteSqlRawAsync` for INSERT/UPDATE with `SqlParameter` and null-safe values
    - Use full table names in SQL queries (no aliases)
    - Wrap all data access in try/catch with `throw;`
    - _Requirements: 10.1, 10.2, 10.4, 10.5_

- [x] 4. Implement IBusinessService and BusinessService
  - [x] 4.1 Create IBusinessService interface and BusinessService implementation
    - Create `src/Portal.Infrastructure/Services/IBusinessService.cs` with methods: `GetAllBusinessesAsync()`, `GetBusinessByIdAsync(int)`, `CreateBusinessAsync(string)`, `UpdateBusinessAsync(Business)`, `DeactivateBusinessAsync(int)`, `IsBusinessNameUniqueAsync(string, int?)`, `GetBusinessProfileAsync(int)`, `SaveBusinessProfileAsync(BusinessProfile)`
    - Create `src/Portal.Infrastructure/Services/BusinessService.cs` implementing `IBusinessService`
    - Inject `BusinessRepository` and `PortalDbContext`
    - `CreateBusinessAsync` sets `IsActive = true` and `CreatedAtUtc = DateTime.UtcNow`
    - `DeactivateBusinessAsync` sets `IsActive = false` and `UpdatedAtUtc = DateTime.UtcNow`
    - `SaveBusinessProfileAsync` validates `VatPeriodLengthInMonths` is in {1, 2, 3, 4, 6, 12}
    - `IsBusinessNameUniqueAsync` performs case-insensitive comparison
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

  - [ ]* 4.2 Write property tests for BusinessService
    - **Property 12: Business creation sets IsActive to true**
    - **Property 14: Business deactivation sets IsActive to false**
    - **Property 15: Business name uniqueness enforcement**
    - **Property 16: VatPeriodLengthInMonths validation**
    - **Validates: Requirements 10.2, 10.4, 10.5, 10.6**

- [x] 5. Implement invitation and email services
  - [x] 5.1 Create IInvitationService interface and InvitationService implementation
    - Create `src/Portal.Infrastructure/Services/IInvitationService.cs` with methods: `CreateInvitationAsync(string email, int businessId, string createdByUserId)`, `ValidateTokenAsync(string token)`, `MarkAsUsedAsync(int invitationId)`
    - Create `src/Portal.Infrastructure/Services/InvitationService.cs` implementing `IInvitationService`
    - Inject `MembershipDbContext`
    - `CreateInvitationAsync` generates a unique token (GUID-based), sets `ExpiresAtUtc = CreatedAtUtc + 72 hours`
    - `ValidateTokenAsync` returns null if token not found, expired (`DateTime.UtcNow > ExpiresAtUtc`), or already used (`IsUsed = true`)
    - `MarkAsUsedAsync` sets `IsUsed = true`
    - _Requirements: 5.1, 5.2, 5.5_

  - [ ]* 5.2 Write property tests for InvitationService
    - **Property 3: Invitation creation produces unique token with correct expiry**
    - **Property 5: Expired or invalid tokens are rejected**
    - **Validates: Requirements 5.1, 5.2, 5.5**

  - [x] 5.3 Create IEmailService interface and StubEmailService
    - Create `src/Portal.Infrastructure/Services/IEmailService.cs` with method: `SendInvitationEmailAsync(string toEmail, string invitationLink, string businessName)`
    - Create `src/Portal.Infrastructure/Services/StubEmailService.cs` implementing `IEmailService` — logs the invitation link to `ILogger` instead of sending a real email
    - _Requirements: 5.1_

- [x] 6. Implement BusinessClaimsPrincipalFactory
  - [x] 6.1 Create BusinessClaimsPrincipalFactory
    - Create `src/Portal.Web/Security/BusinessClaimsPrincipalFactory.cs`
    - Extend `UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>`
    - Override `GenerateClaimsAsync` to add "BusinessId" claim from `user.BusinessId` when non-null
    - Register in DI via `.AddClaimsPrincipalFactory<BusinessClaimsPrincipalFactory>()` (already in Program.cs from task 1.3)
    - _Requirements: 6.1, 6.2_

  - [ ]* 6.2 Write property test for BusinessClaimsPrincipalFactory
    - **Property 7: BusinessId claim injection on authentication**
    - **Validates: Requirements 6.1**

- [x] 7. Checkpoint - Verify compilation and service resolution
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement AccountController
  - [x] 8.1 Create AccountController with login, logout, and access denied
    - Create `src/Portal.Web/Controllers/AccountController.cs`
    - `[HttpGet] Login` — returns login view
    - `[HttpPost] Login` — validates credentials via `SignInManager`, checks user has `BusinessId` (or is SuperAdmin), denies login if no BusinessId with error message "Account not linked to a business"
    - `[HttpPost] Logout` — signs out and redirects to login
    - `[HttpGet] AccessDenied` — returns access denied view
    - Create corresponding views: `Views/Account/Login.cshtml`, `Views/Account/AccessDenied.cshtml`
    - _Requirements: 6.3, 4.3_

  - [ ]* 8.2 Write property test for login denial without BusinessId
    - **Property 8: Login denied for users without BusinessId**
    - **Validates: Requirements 6.3**

- [x] 9. Implement InvitationController
  - [x] 9.1 Create InvitationController with invitation creation and registration flow
    - Create `src/Portal.Web/Controllers/InvitationController.cs`
    - `[Authorize(Roles = "SuperAdmin")]` on invitation creation actions
    - `[HttpGet] Create` — returns form to enter email and select BusinessId
    - `[HttpPost] Create` — calls `IInvitationService.CreateInvitationAsync`, then `IEmailService.SendInvitationEmailAsync` with the registration link
    - `[HttpGet] Register(string token)` — calls `IInvitationService.ValidateTokenAsync`, returns error view if invalid/expired, otherwise returns registration form pre-populated with email
    - `[HttpPost] Register` — validates token again, creates user via `UserManager<ApplicationUser>` with `BusinessId` from invitation, calls `MarkAsUsedAsync`, signs in user
    - Handle edge cases: expired token (error message), already-used token (error message), existing email (error message "Account already exists")
    - Create views: `Views/Invitation/Create.cshtml`, `Views/Invitation/Register.cshtml`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [ ]* 9.2 Write property tests for invitation registration flow
    - **Property 4: Registration with valid token creates correctly associated user**
    - **Property 6: SuperAdmin role required for admin endpoints**
    - **Validates: Requirements 5.4, 5.7**

- [x] 10. Implement BusinessController
  - [x] 10.1 Create BusinessController with CRUD for Business and BusinessProfile
    - Create `src/Portal.Web/Controllers/BusinessController.cs`
    - `[Authorize(Roles = "SuperAdmin")]` on the controller
    - `[HttpGet] Index` — lists all businesses (Name, IsActive, CreatedAtUtc) via `IBusinessService.GetAllBusinessesAsync()`
    - `[HttpGet] Create` / `[HttpPost] Create` — create new Business, validate name uniqueness
    - `[HttpGet] Edit(int id)` / `[HttpPost] Edit` — edit Business name, validate uniqueness
    - `[HttpPost] Deactivate(int id)` — calls `IBusinessService.DeactivateBusinessAsync`
    - `[HttpGet] Profile(int businessId)` / `[HttpPost] Profile` — create/update BusinessProfile, validate VatPeriodLengthInMonths
    - Create views: `Views/Business/Index.cshtml`, `Views/Business/Create.cshtml`, `Views/Business/Edit.cshtml`, `Views/Business/Profile.cshtml`
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [ ]* 10.2 Write property tests for BusinessController authorization
    - **Property 6: SuperAdmin role required for admin endpoints**
    - **Validates: Requirements 10.7**

- [x] 11. Checkpoint - Verify controllers and services compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Create shared layout and views
  - [x] 12.1 Create _Layout.cshtml with sidebar and topbar
    - Create `src/Portal.Web/Views/Shared/_Layout.cshtml`
    - Fixed sidebar (260px): logo/brand area, navigation groups by module (Platform: Dashboard/Businesses/Users, Quotations, Invoicing, Revenue), active state highlighting
    - Sticky topbar: page title area, user context (authenticated user's name + current Business name from claims + logout link)
    - Content area with `@RenderBody()`
    - Apply MyChair Design System: Primary Blue #0D5EA6, background #F7FAFC, secondary #EEF4F8
    - Typography: Manrope for headings, Inter for body (via Google Fonts or local)
    - Cards with 20-30px border radius, soft shadows
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 12.2 Create _ViewImports.cshtml and _ViewStart.cshtml
    - Create `src/Portal.Web/Views/_ViewImports.cshtml` with `@using Portal.Web`, `@using Portal.Web.Models`, `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`
    - Create `src/Portal.Web/Views/_ViewStart.cshtml` setting layout to `_Layout`
    - _Requirements: 7.1_

  - [ ]* 12.3 Write property test for topbar rendering
    - **Property 9: Topbar displays user and business context**
    - **Validates: Requirements 7.5**

- [x] 13. Configure Serilog structured logging
  - [x] 13.1 Configure Serilog in Program.cs with enrichers and sinks
    - Configure `builder.Host.UseSerilog(...)` with: `Enrich.FromLogContext()`, `Enrich.WithProperty("Application", "Portal.Web")`, `Enrich.WithCorrelationId()`
    - Console sink with template: `[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}`
    - File sink with daily rolling: `logs/portal-.log` with template including CorrelationId, UserId, BusinessId
    - Add Serilog middleware to enrich logs with UserId and BusinessId from claims on each request
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [ ]* 13.2 Write property tests for log enrichment
    - **Property 10: Log enrichment with request context**
    - **Property 11: Exception logging with full context**
    - **Validates: Requirements 8.2, 8.4**

- [x] 14. Create Membership database migration script
  - [x] 14.1 Create SQL migration script for Membership database
    - Create `src/Portal.Database/Migrations/Membership/001_CreateMembershipSchema.sql`
    - Include ASP.NET Core Identity tables (AspNetUsers with extended columns: BusinessId, FirstName, LastName, IsActive, CreatedAtUtc; AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens)
    - Include Invitation table with unique index on Token and index on Email
    - Seed "SuperAdmin" role into AspNetRoles
    - _Requirements: 4.1, 4.4, 5.1_

- [x] 15. Final checkpoint - Ensure full solution compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design uses C# / ASP.NET Core MVC 8 — all code examples use this stack
- Credentials are stored in User Secrets, never in appsettings.json
- Repository pattern follows the established GenericStoredProcedureRepository convention with try/catch rethrow
