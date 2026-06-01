# Implementation Plan: Identity Pages

## Overview

This plan implements the public-facing identity pages (Registration, Confirm Account, Forgot Password, Reset Password) for the Portal platform. The implementation extends the existing `AccountController`, introduces a shared `_IdentityLayout.cshtml`, creates new services (`IRegistrationService`, `IPlanService`, `IIdentityEmailService`), adds the `PendingRegistration` entity to the Membership database, and builds all associated views with full accessibility and SEO compliance.

Tasks are ordered to establish infrastructure first (database migration, entity, layout), then build services, then controllers and views, and finally wire everything together with integration tests.

## Tasks

- [x] 1. Database migration and entity setup
  - [x] 1.1 Create SQL migration for PendingRegistration table
    - Create migration file `Portal.Database/Migrations/XXX_CreatePendingRegistrationTable.sql`
    - Define `[membership].[PendingRegistration]` table with columns: `Id` (INT IDENTITY PK), `UserId` (NVARCHAR(450) NOT NULL), `PlanId` (INT NOT NULL), `IsCompleted` (BIT NOT NULL DEFAULT 0), `CreatedAtUtc` (DATETIME NOT NULL DEFAULT GETUTCDATE()), `CompletedAtUtc` (DATETIME NULL)
    - Add FK constraint to `[dbo].[AspNetUsers]([Id])` with ON DELETE NO ACTION
    - Add UNIQUE constraint on `UserId`
    - _Requirements: 2.6, 2.8_

  - [x] 1.2 Create PendingRegistration entity class
    - Create `Portal.Infrastructure/Entities/Identity/PendingRegistration.cs`
    - Define properties: `Id`, `UserId`, `PlanId`, `IsCompleted`, `CreatedAtUtc`, `CompletedAtUtc`, navigation to `ApplicationUser`
    - _Requirements: 2.8_

  - [x] 1.3 Register PendingRegistration in MembershipDbContext
    - Add `DbSet<PendingRegistration>` to `MembershipDbContext`
    - Configure entity mapping in `OnModelCreating`: table name `PendingRegistration` in schema `membership`, unique index on `UserId`, FK to `ApplicationUser` with `DeleteBehavior.NoAction`, `CreatedAtUtc` default value `GETUTCDATE()`
    - _Requirements: 2.8_

- [x] 2. View models and service interfaces
  - [x] 2.1 Create identity page view models
    - Create `Portal.Web/Models/RegisterViewModel.cs` with fields: `FirstName`, `LastName`, `Email`, `Password`, `ConfirmPassword`, `SelectedPlanId`, `AvailablePlans`, `PreSelectedPlan`
    - Create `Portal.Web/Models/ForgotPasswordViewModel.cs` with field: `Email`
    - Create `Portal.Web/Models/ResetPasswordViewModel.cs` with fields: `UserId`, `Token`, `Password`, `ConfirmPassword`
    - Create `Portal.Web/Models/PlanDisplayModel.cs` with fields: `Id`, `Name`, `Slug`, `MonthlyPriceEur`, `Description`
    - Add data annotations for validation: `[Required]`, `[EmailAddress]`, `[MaxLength]`, `[MinLength]`, `[Compare]`
    - _Requirements: 2.2, 2.11, 2.12, 2.15, 2.17, 4.2, 5.2, 5.7, 5.8_

  - [x] 2.2 Create service interfaces
    - Create `Portal.Web/Services/IRegistrationService.cs` with methods: `RegisterAsync(RegisterViewModel)`, `GetPendingRegistrationByUserIdAsync(string)`, `MarkPendingRegistrationCompletedAsync(string)`
    - Create `Portal.Web/Services/IPlanService.cs` with methods: `GetActivePlansOrderedAsync()`, `GetPlanBySlugAsync(string)`
    - Create `Portal.Web/Services/IIdentityEmailService.cs` with methods: `SendEmailConfirmationAsync(string email, string confirmationLink)`, `SendPasswordResetAsync(string email, string resetLink)`
    - _Requirements: 2.5, 2.6, 2.7, 2.8, 4.3_

- [x] 3. Implement core services
  - [x] 3.1 Implement PlanService
    - Create `Portal.Web/Services/PlanService.cs` implementing `IPlanService`
    - `GetActivePlansOrderedAsync()`: query `Plan` table from `PortalDbContext` where `IsActive = true`, order by `DisplayOrder` ascending, project to `PlanDisplayModel`
    - `GetPlanBySlugAsync(string slug)`: query single plan by slug, return null if not found or inactive
    - _Requirements: 2.4, 2.5_

  - [ ]* 3.2 Write property test for plan ordering (Property 1)
    - **Property 1: Plan listing is ordered by DisplayOrder**
    - **Validates: Requirements 2.5**

  - [x] 3.3 Implement RegistrationService
    - Create `Portal.Web/Services/RegistrationService.cs` implementing `IRegistrationService`
    - `RegisterAsync`: create `ApplicationUser` with `EmailConfirmed = false`, `BusinessId = null`, no `UserBusiness` or `UserBusinessPermission` records; create `PendingRegistration` record with selected `PlanId`; generate email confirmation token; send confirmation email via `IIdentityEmailService`
    - `GetPendingRegistrationByUserIdAsync`: query `PendingRegistration` by `UserId`
    - `MarkPendingRegistrationCompletedAsync`: set `IsCompleted = true` and `CompletedAtUtc = DateTime.UtcNow`
    - Handle duplicate email by returning failure result with specific error
    - _Requirements: 2.6, 2.7, 2.8, 2.10, 2.14, 6.3_

  - [ ]* 3.4 Write property test for registration state (Property 2)
    - **Property 2: Valid registration creates unconfirmed user with correct pending state**
    - **Validates: Requirements 2.6, 2.7, 2.8**

  - [ ]* 3.5 Write property test for public registration isolation (Property 3)
    - **Property 3: Public registration creates user without business or permissions**
    - **Validates: Requirements 2.14, 6.3**

  - [ ]* 3.6 Write property test for duplicate email rejection (Property 4)
    - **Property 4: Duplicate email is rejected**
    - **Validates: Requirements 2.10**

  - [x] 3.7 Implement IdentityEmailService
    - Create `Portal.Web/Services/IdentityEmailService.cs` implementing `IIdentityEmailService`
    - `SendEmailConfirmationAsync`: compose and send email with confirmation link using existing email infrastructure
    - `SendPasswordResetAsync`: compose and send email with reset link
    - Log errors via Serilog; do not reveal email sending failures to the user
    - _Requirements: 2.7, 4.3_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Shared identity layout and static assets
  - [x] 5.1 Create _IdentityLayout.cshtml
    - Create `Portal.Web/Views/Shared/_IdentityLayout.cshtml`
    - Implement top bar with gradient background (`linear-gradient(180deg, #1A6BB8 0%, #0D5EA6 100%)`), "Portal" text left-aligned
    - Implement two-column grid (55% hero / 45% card) above 900px viewport
    - Implement single centered card (max-width 420px) at/below 900px viewport
    - Implement frosted glass card: `backdrop-filter: blur(16px)`, `border-radius: 24px`, `max-width: 420px`
    - Implement particle background canvas with `aria-hidden="true"`, `pointer-events: none`
    - Disable animations when `prefers-reduced-motion: reduce` is active (static gradient fallback)
    - Implement footer: "© {currentYear} Portal · 3 Inventors" centered, year rendered server-side
    - Include `<title>` in format "{PageTitle} - Portal", OG meta tags (`og:title`, `og:description`, `og:type`, `og:url`), `<meta name="description">`, favicon `<link rel="icon">`, conditional `<meta name="robots" content="noindex">`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 5.2 Create particle background JavaScript
    - Create `Portal.Web/wwwroot/js/identity-particles.js`
    - Implement canvas-based particle animation with floating particles and connection lines
    - Implement mouse interaction (particles react to cursor proximity)
    - Respect `prefers-reduced-motion: reduce` — do not animate when enabled
    - _Requirements: 1.4, 1.5_

- [x] 6. Implement AccountController identity actions
  - [x] 6.1 Add Registration actions to AccountController
    - Add `[HttpGet] Register(string? plan)` action: load available plans via `IPlanService`, pre-select plan if `plan` query param matches a slug, return `Register` view with `RegisterViewModel`
    - Add `[HttpPost] Register(RegisterViewModel model)` action: validate model state, call `IRegistrationService.RegisterAsync()`, on success redirect to `RegisterConfirmation` view, on failure return view with model state errors
    - Add `[HttpGet] RegisterConfirmation()` action: return the "check your email" view
    - Apply `[AllowAnonymous]` and `[ValidateAntiForgeryToken]` attributes appropriately
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.9, 2.10, 2.11, 2.12, 2.13, 2.15, 2.16, 2.17_

  - [x] 6.2 Add ConfirmEmail action to AccountController
    - Add `[HttpGet] ConfirmEmail(string? userId, string? token)` action
    - Validate `userId` and `token` are present; return generic error if missing
    - Call `UserManager.FindByIdAsync(userId)`; return generic error if user not found (do not reveal existence)
    - If user's email is already confirmed, display "already verified" message with Stripe CTA
    - Call `UserManager.ConfirmEmailAsync(user, token)`; on success set `EmailConfirmed = true`, retrieve `PendingRegistration` to build Stripe checkout URL, display success with CTA button
    - On invalid/expired token, display error with link to request new verification email
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [x] 6.3 Add ForgotPassword actions to AccountController
    - Add `[HttpGet] ForgotPassword()` action: return the forgot password form view
    - Add `[HttpPost] ForgotPassword(ForgotPasswordViewModel model)` action: validate model state; always redirect to `ForgotPasswordConfirmation` regardless of email existence; only generate token and send email if email matches a confirmed account; do not send for unconfirmed accounts
    - Add `[HttpGet] ForgotPasswordConfirmation()` action: return confirmation view with message "If an account exists with that email, a reset link has been sent."
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

  - [x] 6.4 Add ResetPassword actions to AccountController
    - Add `[HttpGet] ResetPassword(string? userId, string? token)` action: validate parameters present; validate token before showing form; if invalid/expired, display error without password form; if valid, return form view
    - Add `[HttpPost] ResetPassword(ResetPasswordViewModel model)` action: validate model state; call `UserManager.ResetPasswordAsync()`; on success display confirmation with login link; on failure return view with errors
    - Add `[HttpGet] ResetPasswordConfirmation()` action: return success view with login link
    - Display generic error for missing/invalid userId without revealing user existence
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11_

  - [ ]* 6.5 Write property test for password policy validation (Property 5)
    - **Property 5: Password policy validation returns specific errors per unmet criterion**
    - **Validates: Requirements 2.11, 5.7**

  - [ ]* 6.6 Write property test for name validation (Property 6)
    - **Property 6: Name validation rejects empty or oversized names**
    - **Validates: Requirements 2.12**

  - [ ]* 6.7 Write property test for password mismatch (Property 7)
    - **Property 7: Password confirmation mismatch is rejected**
    - **Validates: Requirements 2.15, 5.8**

  - [ ]* 6.8 Write property test for email format validation (Property 8)
    - **Property 8: Email format validation rejects malformed addresses**
    - **Validates: Requirements 2.17, 4.7**

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Create identity page views
  - [x] 8.1 Create Register.cshtml view
    - Create `Portal.Web/Views/Account/Register.cshtml` using `_IdentityLayout`
    - Render form with fields: first name, last name, email, password, confirm password
    - Render plan selection section (all active plans with names/prices) or pre-selected plan display
    - Include validation error display with `role="alert"`, `aria-invalid`, `aria-describedby` pattern
    - Mark all required fields with `aria-required="true"`
    - Include link to Login page
    - Set page title "Register - Portal", OG tags, meta description
    - Ensure full keyboard navigation and logical tab order
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.13, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.8, 7.9, 8.1, 8.2, 8.4_

  - [x] 8.2 Create RegisterConfirmation.cshtml view
    - Create `Portal.Web/Views/Account/RegisterConfirmation.cshtml` using `_IdentityLayout`
    - Display "check your email" message with instructions
    - Set page title "Check Your Email - Portal", OG tags, meta description
    - _Requirements: 2.9, 8.1, 8.2, 8.4_

  - [x] 8.3 Create ConfirmEmail.cshtml view
    - Create `Portal.Web/Views/Account/ConfirmEmail.cshtml` using `_IdentityLayout`
    - Display success state with CTA button to Stripe checkout
    - Display error state for invalid/expired token with link to request new email
    - Display "already verified" state with Stripe CTA
    - Display generic error for missing parameters
    - Set `<meta name="robots" content="noindex">` (token-bearing URL)
    - Set page title "Confirm Account - Portal", OG tags, meta description
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.7, 3.8, 8.1, 8.2, 8.4, 8.5_

  - [x] 8.4 Create ForgotPassword.cshtml view
    - Create `Portal.Web/Views/Account/ForgotPassword.cshtml` using `_IdentityLayout`
    - Render email input form with max length 256
    - Include validation error display with accessibility attributes
    - Include link back to Login page
    - Set page title "Forgot Password - Portal", OG tags, meta description
    - _Requirements: 4.1, 4.2, 4.6, 4.7, 7.1, 7.2, 7.5, 7.6, 7.8, 7.9, 8.1, 8.2, 8.4_

  - [x] 8.5 Create ForgotPasswordConfirmation.cshtml view
    - Create `Portal.Web/Views/Account/ForgotPasswordConfirmation.cshtml` using `_IdentityLayout`
    - Display uniform message: "If an account exists with that email, a reset link has been sent."
    - Include link back to Login page
    - Set page title "Password Reset Requested - Portal", OG tags, meta description
    - _Requirements: 4.4, 4.5, 8.1, 8.2, 8.4_

  - [x] 8.6 Create ResetPassword.cshtml view
    - Create `Portal.Web/Views/Account/ResetPassword.cshtml` using `_IdentityLayout`
    - Render password and confirm password form fields (hidden `UserId` and `Token` fields)
    - Include validation error display with accessibility attributes
    - Display error state for invalid/expired token (no form rendered)
    - Include link to Forgot Password page
    - Set `<meta name="robots" content="noindex">` (token-bearing URL)
    - Set page title "Reset Password - Portal", OG tags, meta description
    - _Requirements: 5.1, 5.2, 5.3, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 7.1, 7.2, 7.5, 7.6, 7.8, 7.9, 8.1, 8.2, 8.4, 8.5_

  - [x] 8.7 Create ResetPasswordConfirmation.cshtml view
    - Create `Portal.Web/Views/Account/ResetPasswordConfirmation.cshtml` using `_IdentityLayout`
    - Display success message with link to Login page
    - Set page title "Password Reset Complete - Portal", OG tags, meta description
    - _Requirements: 5.5, 8.1, 8.2, 8.4_

- [x] 9. Service registration and wiring
  - [x] 9.1 Register services in Program.cs
    - Register `IRegistrationService` → `RegistrationService` as scoped
    - Register `IPlanService` → `PlanService` as scoped
    - Register `IIdentityEmailService` → `IdentityEmailService` as scoped
    - Verify `UserManager<ApplicationUser>` and `SignInManager<ApplicationUser>` are already registered via Identity configuration
    - _Requirements: 2.6, 2.7, 3.2, 4.3, 5.4_

- [ ] 10. Security and token validation
  - [ ]* 10.1 Write property test for email confirmation token (Property 9)
    - **Property 9: Valid token confirms email and sets EmailConfirmed to true**
    - **Validates: Requirements 3.2, 3.6**

  - [ ]* 10.2 Write property test for generic error on invalid tokens (Property 10)
    - **Property 10: Invalid or missing token returns generic error without revealing user existence**
    - **Validates: Requirements 3.4, 3.5, 5.6, 5.9, 5.10**

  - [ ]* 10.3 Write property test for uniform forgot password response (Property 11)
    - **Property 11: Forgot password returns uniform response regardless of email existence**
    - **Validates: Requirements 4.4, 4.5, 4.8**

  - [ ]* 10.4 Write property test for invalid reset token gate (Property 12)
    - **Property 12: Invalid reset token prevents password form display**
    - **Validates: Requirements 5.3, 5.6**

  - [ ]* 10.5 Write property test for valid reset updates password (Property 13)
    - **Property 13: Valid reset token and valid password updates the user's password**
    - **Validates: Requirements 5.4**

- [x] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Coexistence verification and invitation merge
  - [x] 12.1 Verify invitation flow coexistence
    - Confirm `InvitationController` routes, actions, views, and service calls remain unmodified
    - Ensure `AccountController` new actions do not conflict with existing Login/Logout/AccessDenied actions
    - Verify both flows share the same `ApplicationUser` entity in the Membership database
    - _Requirements: 6.1, 6.2, 6.6_

  - [ ]* 12.2 Write property test for invitation merge (Property 14)
    - **Property 14: Existing public user can accept invitation without re-registration**
    - **Validates: Requirements 6.5**

- [x] 13. Accessibility and validation UX
  - [x] 13.1 Implement client-side validation feedback
    - Ensure `aria-invalid="true"` is set on fields that fail validation
    - Ensure `aria-describedby` links each field to its error message `<span>`
    - Ensure error messages use `role="alert"` for screen reader announcement
    - Ensure visible focus indicators (2px solid outline, primary blue, 3:1 contrast ratio)
    - Ensure all text meets WCAG AA contrast ratios (4.5:1 normal, 3:1 large) against frosted glass card
    - Implement removal of `aria-invalid` when field is corrected (server-side re-render on valid resubmission)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9_

- [x] 14. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Property test files should be placed in `Portal.Tests/PropertyBased/` following existing naming conventions (e.g., `PlanOrderingPropertyTests.cs`)
- The existing `InvitationController` and its flow must remain completely untouched
- All views use the new `_IdentityLayout.cshtml` (not the existing `_Layout.cshtml`)
- The `AccountController` is extended — existing Login/Logout/AccessDenied actions are preserved

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.2"] },
    { "id": 1, "tasks": ["1.2", "1.3", "5.1", "5.2"] },
    { "id": 2, "tasks": ["3.1", "3.7"] },
    { "id": 3, "tasks": ["3.2", "3.3"] },
    { "id": 4, "tasks": ["3.4", "3.5", "3.6"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["6.5", "6.6", "6.7", "6.8", "9.1"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5"] },
    { "id": 9, "tasks": ["12.1", "12.2"] },
    { "id": 10, "tasks": ["13.1"] }
  ]
}
```
