# Requirements Document

## Introduction

The Promo Code System enables the platform super admin to create promotional codes that grant potential customers a free trial period, bypassing the Stripe checkout flow entirely. This provides a premium onboarding experience for prospects at conferences, targeted outreach, or partnership scenarios. Promo codes support two modes: email-bound (locked to a specific recipient for one-time use) and generic (redeemable by up to X people regardless of email). When a user redeems a valid promo code during registration, the system provisions their account with a full "Business" plan subscription in a "trialing" status for the configured duration, using the existing provisioning infrastructure. After the trial period expires, the existing subscription expiry detection and lockout mechanisms handle the transition to paid billing.

## Glossary

- **Promo_Code**: A record in `[dbo].[PromoCode]` representing an auto-generated promotional code with a duration, expiry date, and optional email binding.
- **Promo_Code_Redemption**: A record in `[dbo].[PromoCodeRedemption]` tracking which user and business redeemed a specific promo code and when.
- **Promo_Code_Admin_Page**: The administration page under the Administration section where super admins create, view, and manage promo codes.
- **Platform_Config**: A record in `[dbo].[PlatformConfig]` storing a key-value platform-wide configuration setting.
- **Provisioning_Service**: The existing service that creates a Business, User association, Subscription, and module permissions during onboarding.
- **Subscription_Plan_Service**: The existing service that resolves subscription access, plan name, and status for a given business, including expiry detection and grace period handling.
- **Registration_Page**: The public-facing page at `/Account/Register` where new users register for the platform.
- **SuperAdmin**: A platform user role with administrative privileges who can manage promo codes and platform configuration.
- **Billing_Subscription**: The `[billing].[Subscription]` record representing a business's subscription lifecycle including status and period dates.
- **Expiry_Guard**: The server-side logic within SubscriptionPlanService that detects expired subscriptions and enforces grace period and lockout flows.
- **Promo_Email_Service**: The service responsible for sending branded promotional emails containing a promo code and registration link to specified recipients.

## Requirements

### Requirement 1: Promo Code Database Schema

**User Story:** As a developer, I want a well-defined database schema for promo codes and platform configuration, so that promo code data is stored reliably and configuration settings are accessible platform-wide.

#### Acceptance Criteria

1. THE `[dbo].[PlatformConfig]` table SHALL contain: Key (NVARCHAR(256), NOT NULL, PRIMARY KEY), Value (NVARCHAR(MAX), NOT NULL), Description (NVARCHAR(500), NULL), and LastModifiedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
2. THE `[dbo].[PromoCode]` table SHALL contain: Id (INT IDENTITY, PRIMARY KEY), Code (NVARCHAR(50), NOT NULL, UNIQUE), DurationMonths (INT, NOT NULL, CHECK > 0), MaxRedemptions (INT, NOT NULL, CHECK > 0), CurrentRedemptions (INT, NOT NULL, DEFAULT 0, CHECK >= 0), ExpiresAtUtc (DATETIME, NOT NULL), BoundEmail (NVARCHAR(256), NULL), IsRevoked (BIT, NOT NULL, DEFAULT 0), CreatedByUserId (NVARCHAR(450), NOT NULL), and CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE()).
3. THE `[dbo].[PromoCodeRedemption]` table SHALL contain: Id (INT IDENTITY, PRIMARY KEY), PromoCodeId (INT, NOT NULL, FK to dbo.PromoCode.Id), UserId (NVARCHAR(450), NOT NULL), BusinessId (INT, NOT NULL, FK to dbo.Business.Id), RedeemedAtUtc (DATETIME, NOT NULL), and CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE()).
4. THE `[dbo].[PromoCode]` table SHALL enforce a CHECK constraint ensuring DurationMonths is between 1 and 24 inclusive.
5. THE `[dbo].[PromoCode]` table SHALL enforce a CHECK constraint ensuring CurrentRedemptions is less than or equal to MaxRedemptions.
6. THE `[dbo].[PromoCodeRedemption]` table SHALL have a foreign key from PromoCodeId to `[dbo].[PromoCode].Id` and from BusinessId to `[dbo].[Business].Id`.
7. THE `[dbo].[PlatformConfig]` table SHALL be seeded with two initial records: Key = "ShowPromoCodeField" with Value = "false" and Description = "Controls visibility of the promo code field on the registration page", and Key = "TrialBadgeText" with Value = "Trial" and Description = "Badge text displayed for promo trial subscriptions in the subscription indicator".

### Requirement 2: Promo Code Generation

**User Story:** As a super admin, I want to create promo codes with configurable parameters, so that I can offer tailored trial experiences to different prospects and events.

#### Acceptance Criteria

1. WHEN the super admin submits the create promo code form with a valid duration, max redemptions, and expiry date, THE Promo_Code_Admin_Page SHALL generate a unique alphanumeric code of 8 characters (uppercase letters and digits, excluding ambiguous characters O, 0, I, l, 1) and persist a new PromoCode record.
2. WHEN the super admin provides an email address in the BoundEmail field, THE Promo_Code_Admin_Page SHALL create an email-bound promo code with MaxRedemptions set to 1 and the BoundEmail field populated with the provided email address.
3. WHEN the super admin leaves the BoundEmail field empty, THE Promo_Code_Admin_Page SHALL create a generic promo code with MaxRedemptions set to the value specified by the super admin.
4. IF a generated code collides with an existing code in the PromoCode table, THEN THE system SHALL regenerate a new code and retry up to 5 times before returning an error indicating code generation failed.
5. THE Promo_Code_Admin_Page SHALL validate that the expiry date is at least 1 day in the future relative to the current UTC time before creating the promo code.
6. THE Promo_Code_Admin_Page SHALL validate that the duration is between 1 and 24 months inclusive before creating the promo code.
7. THE Promo_Code_Admin_Page SHALL validate that max redemptions is between 1 and 500 inclusive for generic codes before creating the promo code.
8. WHEN a promo code is created successfully, THE Promo_Code_Admin_Page SHALL display a success notification showing the generated code value.
9. IF the provided BoundEmail is not a well-formed email address, THEN THE Promo_Code_Admin_Page SHALL display a validation error indicating a valid email is required.

### Requirement 3: Promo Code Administration Interface

**User Story:** As a super admin, I want to view, filter, and manage all promo codes from a dedicated administration page, so that I can monitor usage and revoke codes when necessary.

#### Acceptance Criteria

1. THE Promo_Code_Admin_Page SHALL be accessible only to authenticated users with the SuperAdmin role at a route under the Administration section.
2. THE Promo_Code_Admin_Page SHALL display a paginated table of all promo codes showing: Code, Type (Email-Bound or Generic), DurationMonths, Redemptions (CurrentRedemptions / MaxRedemptions), ExpiresAtUtc, BoundEmail (if applicable), Status, and CreatedAtUtc.
3. THE Promo_Code_Admin_Page SHALL derive the Status column from the promo code state: "Active" when IsRevoked is false AND ExpiresAtUtc is in the future AND CurrentRedemptions is less than MaxRedemptions; "Redeemed" when CurrentRedemptions equals MaxRedemptions; "Expired" when ExpiresAtUtc is in the past AND IsRevoked is false; "Revoked" when IsRevoked is true.
4. THE Promo_Code_Admin_Page SHALL provide a filter panel allowing the super admin to filter by Status (Active, Redeemed, Expired, Revoked, All).
5. WHEN the super admin clicks the "Revoke" action on an active promo code, THE Promo_Code_Admin_Page SHALL set IsRevoked to true on that PromoCode record and display a success notification.
6. THE Promo_Code_Admin_Page SHALL not allow revoking a promo code that is already revoked, fully redeemed, or expired.
7. THE Promo_Code_Admin_Page SHALL display the creation form at the top of the page within a collapsible section, and the promo codes table below.
8. WHEN the super admin clicks the "Send Code" button on an email-bound promo code that is active, THE Promo_Code_Admin_Page SHALL send a branded email to the BoundEmail address containing the promo code and a registration link.
9. IF the super admin clicks "Send Code" on a generic promo code, THEN THE Promo_Code_Admin_Page SHALL display a modal prompting for a recipient email address before sending the branded email.

### Requirement 4: Promo Code Email Delivery

**User Story:** As a super admin, I want to send a branded email with the promo code and registration link to a prospect, so that the recipient receives a premium invitation experience.

#### Acceptance Criteria

1. WHEN the "Send Code" action is triggered, THE Promo_Email_Service SHALL send an email to the specified recipient containing the promo code value and a registration link in the format `/Account/Register?promoCode={code}`.
2. THE promotional email SHALL use the existing branded email template styling consistent with the invitation email templates already in the platform.
3. THE promotional email SHALL include: a header with the Portal branding, a message explaining the free trial offer, the promo code displayed prominently, the trial duration in months, the expiry date of the code, and a call-to-action button linking to the registration page with the promoCode query parameter.
4. IF the email delivery fails, THEN THE Promo_Email_Service SHALL log the error at Warning severity and display an error notification to the super admin indicating the email could not be sent.
5. THE Promo_Email_Service SHALL not modify the promo code record when sending the email (sending does not count as a redemption).

### Requirement 5: Registration Page Promo Code Integration

**User Story:** As a potential customer with a promo code, I want to enter my promo code during registration, so that I can activate my free trial without going through payment.

#### Acceptance Criteria

1. WHEN the Platform_Config key "ShowPromoCodeField" has a value of "true", THE Registration_Page SHALL display a promo code input field on the registration form.
2. WHEN the Platform_Config key "ShowPromoCodeField" has a value of "false" AND no `promoCode` query parameter is present in the URL, THE Registration_Page SHALL not display the promo code input field.
3. WHEN a `promoCode` query parameter is present in the URL, THE Registration_Page SHALL display the promo code input field pre-populated with the parameter value regardless of the ShowPromoCodeField configuration setting.
4. WHEN the user submits the registration form with a promo code value, THE Registration_Page SHALL validate the promo code before proceeding with registration.
5. WHEN validating a promo code, THE system SHALL check that: the code exists in the PromoCode table, IsRevoked is false, ExpiresAtUtc is in the future, and CurrentRedemptions is less than MaxRedemptions.
6. WHEN validating an email-bound promo code (BoundEmail is not null), THE system SHALL additionally verify that the email address entered in the registration form matches the BoundEmail value (case-insensitive comparison).
7. IF the promo code is invalid (does not exist, is revoked, is expired, has reached max redemptions, or email does not match for email-bound codes), THEN THE Registration_Page SHALL display a validation error describing the specific reason the code is not valid.
8. WHEN the promo code is valid, THE Registration_Page SHALL proceed with user registration without plan selection and without redirecting to Stripe checkout after email confirmation.
9. WHEN a valid promo code is submitted, THE Registration_Page SHALL store the promo code Id in the PendingRegistration record alongside the selected plan (Business plan) for use during provisioning.
10. THE promo code input field SHALL accept alphanumeric input with a maximum length of 8 characters and render the input in uppercase.

### Requirement 6: Promo Code Redemption and Provisioning

**User Story:** As a user who registered with a valid promo code, I want my account to be automatically provisioned with a full trial subscription after confirming my email, so that I can start using the platform immediately without payment.

#### Acceptance Criteria

1. WHEN a user with a valid promo code in their PendingRegistration confirms their email, THE Provisioning_Service SHALL execute the provisioning flow (create Business, UserBusiness association, module permissions) using the "Business" plan without requiring Stripe payment.
2. WHEN the Provisioning_Service provisions a promo code user, THE Provisioning_Service SHALL create a Billing_Subscription record with: Status = "trialing", PlanId referencing the "Business" plan, CurrentPeriodStart = current UTC time, CurrentPeriodEnd = current UTC time plus the PromoCode.DurationMonths, and StripeSubscriptionId = NULL.
3. WHEN the Provisioning_Service provisions a promo code user, THE Provisioning_Service SHALL increment the PromoCode.CurrentRedemptions by 1.
4. WHEN the Provisioning_Service provisions a promo code user, THE Provisioning_Service SHALL create a PromoCodeRedemption record with the PromoCodeId, the new UserId, the new BusinessId, and RedeemedAtUtc set to the current UTC time.
5. THE Provisioning_Service SHALL execute the subscription creation, redemption count increment, and redemption record creation within a single database transaction.
6. IF two concurrent registrations attempt to redeem the same generic promo code and only one redemption remains (CurrentRedemptions = MaxRedemptions - 1), THEN THE Provisioning_Service SHALL ensure only one redemption succeeds by re-verifying CurrentRedemptions < MaxRedemptions within the transaction.
7. IF the promo code has become invalid between registration submission and email confirmation (expired, revoked, or fully redeemed), THEN THE Provisioning_Service SHALL reject the provisioning, log the rejection reason, and redirect the user to a page indicating their promo code is no longer valid with a link to subscribe via the normal Stripe checkout flow.
8. WHEN provisioning completes for a promo code user, THE Confirm_Account_Page SHALL redirect the user directly to the Setup Wizard (or dashboard if setup is complete) instead of the Stripe checkout page.

### Requirement 7: Promo Trial Subscription Lifecycle

**User Story:** As a platform operator, I want promo trial subscriptions to integrate with the existing subscription lifecycle enforcement, so that expired trials are handled consistently without custom expiry logic.

#### Acceptance Criteria

1. WHILE a promo trial Billing_Subscription has Status "trialing" AND CurrentPeriodEnd is in the future, THE Subscription_Plan_Service SHALL treat the subscription as active and grant full access to all modules included in the "Business" plan.
2. WHEN the Expiry_Guard detects that a Billing_Subscription with Status "trialing" has a CurrentPeriodEnd earlier than the current UTC time, THE Expiry_Guard SHALL apply the same grace access and lockout flow as it does for expired "active" subscriptions.
3. THE subscription indicator in the sidebar SHALL display the badge text configured in the Platform_Config key "TrialBadgeText" (default "Trial") for subscriptions with Status "trialing" that have a NULL StripeSubscriptionId.
4. WHEN a promo trial user is locked out after the grace period, THE lockout screen SHALL display a "Subscribe Now" button that navigates the user to the normal Stripe checkout flow for the "Business" plan.
5. THE Expiry_Guard SHALL treat "trialing" status identically to "active" status for the purposes of expiry detection (checking CurrentPeriodEnd against the current UTC time).
6. IF a promo trial user subscribes via Stripe before the trial expires, THEN THE Billing_Subscription record SHALL be updated with the Stripe subscription data (StripeSubscriptionId, Status = "active", new CurrentPeriodStart and CurrentPeriodEnd from Stripe) and the subscription indicator badge SHALL change to "Active".

### Requirement 8: Platform Configuration Service

**User Story:** As a developer, I want a reusable service for reading platform configuration values, so that feature flags and settings can be managed centrally without hardcoding values.

#### Acceptance Criteria

1. THE platform SHALL provide a PlatformConfigService that reads configuration values from the `[dbo].[PlatformConfig]` table by key.
2. WHEN a configuration value is requested, THE PlatformConfigService SHALL return the Value for the matching Key using a case-insensitive key lookup.
3. IF a requested configuration key does not exist in the PlatformConfig table, THEN THE PlatformConfigService SHALL return a null value without throwing an exception.
4. THE PlatformConfigService SHALL cache configuration values for the duration of the HTTP request to avoid repeated database queries within a single request.
5. THE PlatformConfigService SHALL be registered as a scoped service in the dependency injection container.
6. WHEN a Platform_Config record is updated, THE system SHALL set the LastModifiedAtUtc column to the current UTC time.

### Requirement 9: Security and Access Control

**User Story:** As a platform operator, I want promo code management restricted to super admins and promo code validation protected against abuse, so that the system remains secure and promotional offers are not exploited.

#### Acceptance Criteria

1. THE Promo_Code_Admin_Page SHALL be accessible only to authenticated users with the SuperAdmin role; non-SuperAdmin users SHALL receive an HTTP 403 response.
2. WHEN validating a promo code during registration, THE system SHALL perform all validation checks server-side regardless of any client-side validation.
3. THE system SHALL not expose internal promo code details (Id, CreatedByUserId, CurrentRedemptions, MaxRedemptions) to the registration page; only a valid/invalid result and error message SHALL be returned to the client.
4. THE promo code input field SHALL sanitize input by trimming whitespace and converting to uppercase before validation.
5. IF a user attempts to redeem a promo code that is bound to a different email address, THEN THE system SHALL return a generic "invalid code" message without revealing that the code exists or is email-bound.
6. THE system SHALL log all promo code creation, revocation, and redemption events at informational level using Serilog structured logging with the acting UserId, PromoCodeId, and action type.
