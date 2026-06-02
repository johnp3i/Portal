# Implementation Plan: Promo Code System

## Overview

This plan implements the Promo Code System for the Portal platform, enabling super admins to create promotional codes that grant trial subscriptions without Stripe checkout. The implementation follows the existing MVC + Service + Repository pattern and integrates with the current provisioning and subscription lifecycle infrastructure.

## Tasks

- [x] 1. Database migrations — Schema tables
  - [x] 1.1 Create migration `081_CreatePlatformConfigTable.sql` with PlatformConfig table (Key NVARCHAR(256) PK, Value NVARCHAR(MAX) NOT NULL, Description NVARCHAR(500) NULL, LastModifiedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE())
    - _Requirements: 1.1, 8.1_

  - [x] 1.2 Create migration `082_CreatePromoCodeTable.sql` with PromoCode table (Id INT IDENTITY PK, Code NVARCHAR(50) UNIQUE, DurationMonths INT CHECK 1-24, MaxRedemptions INT CHECK > 0, CurrentRedemptions INT DEFAULT 0 CHECK >= 0 and <= MaxRedemptions, ExpiresAtUtc DATETIME, BoundEmail NVARCHAR(256) NULL, IsRevoked BIT DEFAULT 0, CreatedByUserId NVARCHAR(450), CreatedAtUtc DATETIME DEFAULT GETUTCDATE())
    - _Requirements: 1.2, 1.4, 1.5_

  - [x] 1.3 Create migration `083_CreatePromoCodeRedemptionTable.sql` with PromoCodeRedemption table (Id INT IDENTITY PK, PromoCodeId INT FK, UserId NVARCHAR(450), BusinessId INT FK, RedeemedAtUtc DATETIME, CreatedAtUtc DATETIME DEFAULT GETUTCDATE())
    - _Requirements: 1.3, 1.6_

  - [x] 1.4 Create migration `Membership/005_AddPromoCodeIdToPendingRegistration.sql` adding nullable PromoCodeId INT column
    - _Requirements: 5.9_

- [x] 2. Database migrations — Seed data
  - [x] 2.1 Create migration `084_SeedPlatformConfig.sql` seeding ShowPromoCodeField=false and TrialBadgeText=Trial with descriptions
    - _Requirements: 1.7, 8.1_

- [x] 3. Entity classes and DbContext registration
  - [x] 3.1 Create `PlatformConfig.cs` entity in Portal.Infrastructure/Entities and register in PortalDbContext
    - _Requirements: 8.1_

  - [x] 3.2 Create `PromoCode.cs` entity in Portal.Infrastructure/Entities with navigation to PromoCodeRedemption collection and register in PortalDbContext
    - _Requirements: 1.2_

  - [x] 3.3 Create `PromoCodeRedemption.cs` entity in Portal.Infrastructure/Entities with navigation to PromoCode and Business, register in PortalDbContext
    - _Requirements: 1.3_

  - [x] 3.4 Update `PendingRegistration.cs` to include nullable PromoCodeId property and update MembershipDbContext mapping
    - _Requirements: 5.9_

- [x] 4. Repository layer
  - [x] 4.1 Create `PlatformConfigRepository.cs` in Portal.Infrastructure/Repositories with GetByKeyAsync (case-insensitive) and UpsertAsync methods following GenericStoredProcedureRepository pattern
    - _Requirements: 8.1, 8.2, 8.3, 8.6_

  - [x] 4.2 Create `PromoCodeRepository.cs` in Portal.Infrastructure/Repositories with InsertAsync, GetByCodeAsync, CodeExistsAsync, RevokeAsync, IncrementRedemptionsAsync (WHERE CurrentRedemptions < MaxRedemptions), GetFilteredAsync (paginated)
    - _Requirements: 2.1, 2.4, 3.2, 3.5, 6.3, 6.6_

  - [x] 4.3 Create `PromoCodeRedemptionRepository.cs` in Portal.Infrastructure/Repositories with InsertAsync
    - _Requirements: 6.4_

- [x] 5. Models and DTOs
  - [x] 5.1 Create request/result models: CreatePromoCodeRequest, PromoCodeCreateResult, PromoCodeFilter, PromoCodeListItem, PromoCodeValidationResult in Portal.Web/Models
    - _Requirements: 2.1, 3.2, 5.5, 9.3_

- [x] 6. PlatformConfig service
  - [x] 6.1 Create `IPlatformConfigService.cs` and `PlatformConfigService.cs` in Portal.Web/Services with GetValueAsync and SetValueAsync, request-scoped caching via HttpContext.Items, register as scoped
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 7. PromoCode service
  - [x] 7.1 Create `IPromoCodeService.cs` and `PromoCodeService.cs` in Portal.Web/Services implementing code generation (8-char from ABCDEFGHJKLMNPQRSTUVWXYZ23456789 using RandomNumberGenerator), collision retry up to 5 attempts, input validation, email-bound MaxRedemptions=1 override
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

- [x] 8. PromoCode validation service
  - [x] 8.1 Create `IPromoCodeValidationService.cs` and `PromoCodeValidationService.cs` implementing 5-step validation (exists, not revoked, not expired, not fully redeemed, email match case-insensitive), input sanitization (trim+uppercase), generic error for email mismatch
    - _Requirements: 5.5, 5.6, 5.7, 9.2, 9.4, 9.5_

- [x] 9. PromoEmail service
  - [x] 9.1 Create `IPromoEmailService.cs` and `PromoEmailService.cs` implementing branded email with promo code, duration, expiry, CTA button to /Account/Register?promoCode={code}. Add PromoCode to EmailDepartmentEnum. Does not modify promo code record.
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 10. PromoCode admin controller
  - [x] 10.1 Create `PromoCodeController.cs` with [Authorize(Roles="SuperAdmin")] [Route("Admin/PromoCodes")] — Index GET, Create POST AJAX, Revoke POST AJAX, SendCode POST AJAX with [ValidateAntiForgeryToken]
    - _Requirements: 3.1, 3.5, 3.8, 3.9, 9.1_

- [x] 11. Registration integration — ViewModel and GET
  - [x] 11.1 Update `RegisterViewModel.cs` to include optional PromoCode string property
    - _Requirements: 5.10_

  - [x] 11.2 Update AccountController Register GET to check PlatformConfig ShowPromoCodeField and promoCode query parameter, pass visibility flag and pre-populated value to view
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 12. Admin view and UI
  - [x] 12.1 Create `Views/PromoCode/Index.cshtml` with collapsible create form (duration, max redemptions, expiry, optional email), status filter panel, paginated table (Code, Type, Duration, Redemptions, Expiry, BoundEmail, Status, Created, Actions)
    - _Requirements: 3.2, 3.3, 3.4, 3.7_

  - [x] 12.2 Implement status derivation display (Active/Redeemed/Expired/Revoked badges), Revoke button disabled for non-active, Send Code button with modal for generic codes
    - _Requirements: 3.3, 3.6, 3.8, 3.9_

  - [x] 12.3 Update Register view to conditionally show promo code input (maxlength=8, uppercase, visible when config=true or query param present)
    - _Requirements: 5.1, 5.2, 5.3, 5.10_

- [x] 13. Admin page AJAX and navigation
  - [x] 13.1 Wire AJAX calls in admin page using BlockUI.show/hide + fetch + SweetAlert2 pattern, add navigation link in Administration sidebar
    - _Requirements: 2.8, 3.1, 3.5_

- [x] 14. Registration POST validation
  - [x] 14.1 Update AccountController Register POST to validate promo code via IPromoCodeValidationService before user creation, display specific error on invalid, store PromoCodeId in PendingRegistration on valid
    - _Requirements: 5.4, 5.5, 5.7, 5.8, 5.9, 9.2_

- [x] 15. Provisioning models
  - [x] 15.1 Create `PromoProvisioningRequest.cs` model with UserId, PendingRegistrationId, PlanId, PromoCodeId, DurationMonths
    - _Requirements: 6.1_

- [x] 16. Provisioning service extension
  - [x] 16.1 Add `ProvisionPromoTrialAsync` to IProvisioningService interface and implement in ProvisioningService — single transaction: create Business, UserBusiness, Subscription (trialing, null StripeId, period now+DurationMonths), Permissions, IncrementRedemptions (WHERE guard), PromoCodeRedemption record, mark PendingRegistration completed
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 17. Concurrent redemption guard
  - [x] 17.1 Handle concurrent redemption in ProvisionPromoTrialAsync: if IncrementRedemptions returns 0 rows, rollback and return failure
    - _Requirements: 6.6_

  - [x] 17.2 Handle stale promo code in ProvisionPromoTrialAsync: re-validate within transaction, reject if expired/revoked/fully redeemed since registration
    - _Requirements: 6.7_

- [x] 18. Email confirmation flow
  - [x] 18.1 Update AccountController ConfirmEmail to detect PendingRegistration with PromoCodeId, call ProvisionPromoTrialAsync instead of Stripe redirect
    - _Requirements: 6.8_

  - [x] 18.2 On successful promo provisioning redirect to SetupWizard; on failure redirect to PromoCodeExpired page
    - _Requirements: 6.7, 6.8_

- [x] 19. Registration service update
  - [x] 19.1 Update RegistrationService.RegisterAsync to accept optional PromoCodeId, store in PendingRegistration, force Business plan PlanId when promo code present
    - _Requirements: 5.8, 5.9, 6.1_

- [x] 20. Subscription lifecycle integration
  - [x] 20.1 Update SubscriptionPlanService expiry detection to include Status="trialing" in the expired period check alongside "active"
    - _Requirements: 7.2, 7.5_

  - [x] 20.2 Update subscription indicator ViewComponent to display PlatformConfig TrialBadgeText for trialing subscriptions with null StripeSubscriptionId
    - _Requirements: 7.3_

  - [x] 20.3 Update lockout view to show Subscribe Now button linking to /Checkout for trialing users
    - _Requirements: 7.4_

- [x] 21. Stripe webhook integration
  - [x] 21.1 Verify/update Stripe webhook handling to correctly update existing trialing subscription to active when user subscribes via Stripe
    - _Requirements: 7.6_

- [x] 22. Error page and logging
  - [x] 22.1 Create `Views/Account/PromoCodeExpired.cshtml` with explanation message and Subscribe Now button linking to /Checkout
    - _Requirements: 6.7_

  - [x] 22.2 Add Serilog structured logging for promo code creation, revocation, redemption, email send, provisioning events with UserId, PromoCodeId, action type
    - _Requirements: 9.6_

- [x] 23. Property-based tests
  - [x] 23.1 Set up FsCheck.Xunit package in test project and write property tests for: code format invariant, email-bound MaxRedemptions=1, expiry validation, duration validation, MaxRedemptions validation, status derivation, status filter, revoke guard, email content, email read-only, composite validation, email match, trial period calculation, concurrent atomicity, trialing/active equivalence, config lookup, no internal details, input sanitization
    - _Requirements: 2.1, 2.2, 2.5, 2.6, 2.7, 3.3, 3.4, 3.6, 4.1, 4.3, 4.5, 5.5, 5.6, 6.2, 6.6, 7.2, 7.5, 8.2, 9.3, 9.4_

- [x] 24. Unit tests
  - [x] 24.1 Write unit tests for PromoCodeService, PromoCodeValidationService, PlatformConfigService, ProvisionPromoTrialAsync covering happy paths, error cases, and edge conditions
    - _Requirements: 2.1, 2.4, 5.5, 5.7, 6.1, 6.6, 6.7, 8.2, 8.3_

- [x] 25. Seed data
  - [x] 25.1 Create seed script `Portal.Database/Seeds/Seed_SamplePromoCodes.sql` that inserts sample promo codes for development and testing purposes
    - Insert a mix of email-bound and generic promo codes with various statuses: active, expired, fully redeemed, and revoked
    - Include codes with different DurationMonths values (1, 3, 6, 12) and varying MaxRedemptions
    - Use realistic sample email addresses for BoundEmail on email-bound codes
    - Set ExpiresAtUtc values to produce both active (future) and expired (past) records
    - Set CurrentRedemptions = MaxRedemptions for fully redeemed codes and IsRevoked = 1 for revoked codes
    - _Requirements: 1.2, 3.3_

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "3.1", "3.2", "3.3", "3.4", "25.1"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "5.1"] },
    { "id": 3, "tasks": ["6.1", "7.1"] },
    { "id": 4, "tasks": ["8.1", "9.1", "10.1", "11.1", "15.1"] },
    { "id": 5, "tasks": ["12.1", "11.2", "16.1"] },
    { "id": 6, "tasks": ["12.2", "12.3", "17.1", "17.2"] },
    { "id": 7, "tasks": ["13.1", "14.1", "18.1", "20.1", "20.2", "20.3"] },
    { "id": 8, "tasks": ["19.1", "18.2", "21.1", "22.2"] },
    { "id": 9, "tasks": ["22.1", "23.1", "24.1"] }
  ]
}
```

## Notes

- Migrations are numbered starting at 081 since 080 is the latest existing migration
- The PendingRegistration PromoCodeId is a logical cross-database reference (no physical FK since PendingRegistration is in Membership DB and PromoCode is in Portal DB)
- The SubscriptionPlanService already handles "trialing" as active (confirmed in code), but the expiry detection block only checks `status == "active"` — this needs to be updated in Task 20.1
- Property-based tests use FsCheck.Xunit to integrate with the existing xUnit test runner
- All AJAX endpoints follow the established BlockUI + fetch + SweetAlert2 pattern documented in the project steering
