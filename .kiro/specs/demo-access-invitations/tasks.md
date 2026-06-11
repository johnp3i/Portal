# Implementation Plan: Demo Access Invitations

## Overview

This plan implements the Demo Access Invitations feature, enabling SuperAdmins to create and manage magic-link demo invitations that auto-authenticate prospects into designated demo businesses with configurable module permissions and expiry. The implementation follows the existing MVC + Service + Repository pattern with EF Core Database-First, ASP.NET Core Identity claims-based session management, and a global authorization filter for demo permission enforcement.

## Tasks

- [x] 1. Database migrations
  - [x] 1.1 Create migration `089_AddIsDemoAccountToBusiness.sql` adding `IsDemoAccount BIT NOT NULL DEFAULT 0` to `[portal].[Business]`, filtered non-clustered index on `IsDemoAccount = 1`, and UPDATE to set `IsDemoAccount = 1` for existing demo business (Id = 1000)
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Create migration `090_CreateDemoInvitationTable.sql` with `[portal].[DemoInvitation]` table (Id INT IDENTITY PK, BusinessId INT FK, Token NVARCHAR(100) UNIQUE, RecipientEmail NVARCHAR(256), RecipientName NVARCHAR(200) NULL, ExpiresAtUtc DATETIME2, Status NVARCHAR(20) CHECK IN ('sent','accessed','expired','revoked'), CreatedByUserId NVARCHAR(450) FK, FirstAccessedAtUtc DATETIME2 NULL, LastAccessedAtUtc DATETIME2 NULL, AccessCount INT DEFAULT 0, RevokedAtUtc DATETIME2 NULL, CreatedAtUtc DATETIME2 DEFAULT GETUTCDATE()), unique index on Token, non-clustered index on Status including ExpiresAtUtc and RecipientEmail
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.3 Create migration `091_CreateDemoInvitationPermissionTable.sql` with `[portal].[DemoInvitationPermission]` table (Id INT IDENTITY PK, DemoInvitationId INT FK, Module NVARCHAR(50) CHECK IN module list, AccessLevel NVARCHAR(20) CHECK IN ('full','readonly','none'), CreatedAtUtc DATETIME2 DEFAULT GETUTCDATE()), unique constraint on (DemoInvitationId, Module)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 2. Entity models and DbContext registration
  - [x] 2.1 Create `DemoInvitation.cs` entity in `Portal.Infrastructure/Entities` with all properties, navigation to Business and Permissions collection
    - _Requirements: 2.1_

  - [x] 2.2 Create `DemoInvitationPermission.cs` entity in `Portal.Infrastructure/Entities` with all properties, navigation to DemoInvitation
    - _Requirements: 3.1_

  - [x] 2.3 Update `Business.cs` entity to add `IsDemoAccount` property (bool)
    - _Requirements: 1.1_

  - [x] 2.4 Register `DemoInvitation` and `DemoInvitationPermission` DbSets in `PortalDbContext`, configure entity relationships, indexes, check constraints, and default values in `OnModelCreating`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4_

- [x] 3. Request/Response models
  - [x] 3.1 Create models in `Portal.Infrastructure/Models`: `CreateDemoInvitationRequest`, `ModulePermissionEntry`, `DemoInvitationValidationResult`, `DemoInvitationListItem`, `DemoBusinessItem`, `PagedResult<T>` (if not existing), `RevokeRequest`, `ResendRequest`
    - _Requirements: 5.1, 5.2, 7.2, 10.1_

- [x] 4. Repository layer
  - [x] 4.1 Create `DemoInvitationRepository.cs` in `Portal.Infrastructure/Repositories` extending `GenericStoredProcedureRepository<DemoInvitation>` with methods: `GetByTokenAsync`, `GetAllAsync`, `GetPagedAsync`, `GetTotalCountAsync`, `InsertAsync` (invitation + permissions in transaction), `UpdateStatusAsync`, `UpdateAccessTrackingAsync`, `GetPermissionsByInvitationIdAsync`, `GetDemoBusinessesAsync`
    - Follow repository standards: try/catch with rethrow, SqlParameter with null-safe values, full table names in queries
    - _Requirements: 1.2, 2.2, 2.5, 4.3, 9.1, 9.2, 9.3, 10.2, 10.4, 11.3_

- [x] 5. Checkpoint — Ensure migrations and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Service layer
  - [x] 6.1 Create `IDemoInvitationService.cs` interface in `Portal.Infrastructure/Services` with all methods: `CreateAsync`, `ValidateAndTrackAccessAsync`, `RevokeAsync`, `ResendEmailAsync`, `GetAllPagedAsync`, `GetDemoBusinessesAsync`, `GetPermissionsForInvitationAsync`, `GenerateToken`, `EnsureDemoUserBusinessAsync`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.2, 5.3, 7.2, 8.1, 9.1, 9.2, 10.2, 11.3, 12.2_

  - [x] 6.2 Create `DemoInvitationService.cs` implementation in `Portal.Web/Services` with: token generation (32-byte crypto random → Base64URL, collision retry up to 3 attempts), invitation creation with validation (email format, IsDemoAccount business, future expiry, at least one non-'none' permission), access tracking (FirstAccessedAtUtc, LastAccessedAtUtc, AccessCount, status transitions), revocation logic, pagination with descending CreatedAtUtc sort
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.2, 5.3, 7.2, 7.4, 9.1, 9.2, 9.3, 10.2, 10.4, 11.3, 12.2_

  - [x] 6.3 Write property test: Token Format Validity (Property 1)
    - **Property 1: Token Format Validity**
    - For any generated token, decoding from Base64URL produces exactly 32 bytes, and the token contains only `[A-Za-z0-9_-]` with no `=` padding
    - **Validates: Requirements 4.1, 4.2**

  - [x] 6.4 Write property test: Token Validation — Valid vs Expired (Property 3)
    - **Property 3: Token Validation — Valid vs Expired**
    - Valid tokens (status sent/accessed, ExpiresAtUtc > UtcNow) return IsValid=true; expired tokens return IsValid=false with ErrorReason="expired" and status updated
    - **Validates: Requirements 7.2, 7.4**

  - [x] 6.5 Write property test: Access Tracking Invariants (Property 4)
    - **Property 4: Access Tracking Invariants**
    - Valid access increments AccessCount by 1, sets LastAccessedAtUtc, and sets FirstAccessedAtUtc on first access with status → 'accessed'
    - **Validates: Requirements 9.1, 9.2**

  - [x] 6.6 Write property test: Invitation Creation Validation (Property 6)
    - **Property 6: Invitation Creation Validation**
    - Invalid email, non-demo business, past expiry, or no granted permissions SHALL reject and not persist
    - **Validates: Requirements 5.2**

  - [x] 6.7 Write property test: Invitation List Ordering (Property 8)
    - **Property 8: Invitation List Ordering**
    - GetAllPagedAsync returns results sorted by CreatedAtUtc descending — each adjacent pair satisfies result[i].CreatedAtUtc >= result[i+1].CreatedAtUtc
    - **Validates: Requirements 10.2**

  - [x] 6.8 Write property test: Pagination Correctness (Property 9)
    - **Property 9: Pagination Correctness**
    - For total N invitations and page P with size 10, returns at most 10 items at correct offset, total count equals N
    - **Validates: Requirements 10.4**

  - [x] 6.9 Write property test: Revocation State Transition (Property 10)
    - **Property 10: Revocation State Transition**
    - Revoking an invitation with status 'sent' or 'accessed' sets status to 'revoked' and RevokedAtUtc within tolerance of UtcNow
    - **Validates: Requirements 11.3**

- [x] 7. Email service extension
  - [x] 7.1 Add `SendDemoInvitationEmailAsync(string toEmail, string magicLink, string businessName, DateTime expiresAtUtc)` to `IEmailService` interface and implement in `PortalEmailService` using existing email template pattern with EmailDepartmentEnum.Sales, CTA "Explore Demo", human-readable expiry, business name in subject
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 7.2 Write property test: Email Content Completeness (Property 7)
    - **Property 7: Email Content Completeness**
    - For any business name and expiry date, the generated HTML contains the business name, expiry in readable format, and anchor href with magic link URL
    - **Validates: Requirements 6.2**

- [x] 8. Checkpoint — Ensure service layer compiles and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. DemoController — Public entry endpoint
  - [x] 9.1 Create `DemoController.cs` in `Portal.Web/Controllers` with `[AllowAnonymous]` attribute, `GET /Demo/Enter?token=` action: validate token via service, handle invalid/expired/revoked with appropriate views, create or retrieve demo ApplicationUser for RecipientEmail+BusinessId, sign in with DemoScheme cookie including DemoInvitationId/BusinessId/IsDemoSession claims, redirect to dashboard
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 8.1, 8.2, 8.3, 8.4_

  - [x] 9.2 Create error/status views: `Views/Demo/DemoInvalid.cshtml`, `Views/Demo/DemoExpired.cshtml`, `Views/Demo/DemoRevoked.cshtml`, `Views/Demo/DemoSessionExpired.cshtml` with appropriate messaging and MyChair styling
    - _Requirements: 7.3, 7.4, 7.5, 13.2_

- [x] 10. DemoInvitationController — Admin CRUD
  - [x] 10.1 Create `DemoInvitationController.cs` in `Portal.Web/Controllers` with `[Authorize(Roles = "SuperAdmin")]`, route `Admin/DemoInvitations`: `GET Index` (paginated list), `GET Create` (form page), `POST Create` (AJAX with antiforgery, JSON response), `POST Revoke` (AJAX), `POST Resend` (AJAX) — all returning `Json(new { success, message })`
    - _Requirements: 5.1, 5.3, 5.4, 5.5, 10.1, 10.2, 10.4, 11.1, 11.2, 11.3, 12.1, 12.2, 12.3, 12.4, 15.1, 15.2_

  - [x] 10.2 Create `Views/DemoInvitation/Index.cshtml` — Admin list view with table (recipient email, name, business, status badges: sent=blue/accessed=green/expired=amber/revoked=red, expiry date, access count, first accessed, created date), pagination, Revoke/Resend action buttons with SweetAlert2 confirmations, BlockUI on AJAX calls
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 11.1, 11.2, 11.5, 12.1, 12.5_

  - [x] 10.3 Create `Views/DemoInvitation/Create.cshtml` — Create form with demo business dropdown, recipient email, optional name, expiry datepicker, module permissions grid (each module with full/readonly/none radio buttons), AJAX submission with validation, SweetAlert2 success/error, BlockUI
    - _Requirements: 5.1, 5.2, 5.4, 5.5_

- [x] 11. DemoPermissionFilter — Global authorization filter
  - [x] 11.1 Create `DemoPermissionFilter.cs` in `Portal.Web/Filters` implementing `IAsyncAuthorizationFilter`: check for DemoInvitationId claim, resolve module from controller route using module-to-controller mapping, deny access for 'none'/missing permissions (show DemoAccessRestricted view), block non-GET for 'readonly' (return 403 JSON), allow all for 'full', skip non-module controllers
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

  - [x] 11.2 Create `Views/Shared/DemoAccessRestricted.cshtml` — 403 page for demo users accessing restricted modules with MyChair styling
    - _Requirements: 14.2_

  - [x] 11.3 Write property test: Demo Permission Enforcement (Property 5)
    - **Property 5: Demo Permission Enforcement**
    - For any DemoInvitationId claim and controller mapped to a module: 'none' → denied, 'readonly' → GET allowed/non-GET blocked, 'full' → all allowed
    - **Validates: Requirements 8.5, 14.1, 14.2, 14.3, 14.4**

  - [x] 11.4 Write property test: Demo Business Filtering (Property 2)
    - **Property 2: Demo Business Filtering**
    - GetDemoBusinessesAsync returns exactly businesses where IsDemoAccount=1, excludes IsDemoAccount=0
    - **Validates: Requirements 1.2**

- [x] 12. Authentication scheme configuration
  - [x] 12.1 Configure `DemoScheme` cookie authentication in `Program.cs` with 2-hour sliding expiry, `OnRedirectToLogin` event that detects expired demo sessions (IsDemoSession claim) and redirects to `/Demo/SessionExpired` instead of `/Account/Login`
    - _Requirements: 13.1, 13.2, 13.3_

- [x] 13. DI registration and wiring
  - [x] 13.1 Register `DemoInvitationRepository`, `IDemoInvitationService`/`DemoInvitationService` in DI container, register `DemoPermissionFilter` as a global filter in MVC pipeline
    - _Requirements: 14.1, 15.1_

  - [x] 13.2 Add "Demo Invitations" menu item to Admin panel navigation, visible only for users with "SuperAdmin" role, linking to `/Admin/DemoInvitations`
    - _Requirements: 15.3_

- [-] 14. Checkpoint — Full integration verification
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 15. Unit and integration tests
  - [-] 15.1 Write unit tests for `DemoInvitationService`: token generation non-null/non-empty, collision retry, validation for missing/revoked/expired tokens, revoke already-revoked no-op, email failure still persists invitation
    - _Requirements: 4.1, 4.4, 6.4, 7.3, 7.4, 7.5, 11.3_

  - [-] 15.2 Write unit tests for `DemoPermissionFilter`: non-demo users pass through, demo users denied for restricted modules, readonly blocks writes, full allows all
    - _Requirements: 14.1, 14.2, 14.3, 14.4_

  - [-] 15.3 Write unit tests for `DemoController`: valid token redirects to dashboard, invalid/expired/revoked tokens render correct views, missing token renders DemoInvalid
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 8.4_

  - [-] 15.4 Write integration tests for `DemoInvitationRepository`: insert + retrieve by token, unique constraint violation, FK enforcement, pagination
    - _Requirements: 2.2, 2.4, 2.5, 10.4_

- [x] 16. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck via FsCheck.Xunit with minimum 100 iterations per property
- Unit tests validate specific examples and edge cases
- The DemoScheme cookie is isolated from the primary authentication cookie so regular users are unaffected
- Repository follows GenericStoredProcedureRepository pattern with try/catch rethrow, SqlParameter null-safe, full table names
- All AJAX endpoints follow the standard pattern: BlockUI.show → fetch with antiforgery → BlockUI.hide → Swal.fire

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "3.1"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["6.1"] },
    { "id": 5, "tasks": ["6.2", "7.1"] },
    { "id": 6, "tasks": ["6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9", "7.2"] },
    { "id": 7, "tasks": ["9.1", "10.1", "11.1", "12.1"] },
    { "id": 8, "tasks": ["9.2", "10.2", "10.3", "11.2", "13.1"] },
    { "id": 9, "tasks": ["11.3", "11.4", "13.2"] },
    { "id": 10, "tasks": ["15.1", "15.2", "15.3", "15.4"] }
  ]
}
```
