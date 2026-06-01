# Requirements Document

## Introduction

This specification defines the Stripe integration and tenant onboarding flow for the Portal platform, covering modules 10.4 through 10.8. The flow begins after a user confirms their email (from the identity-pages spec) and proceeds through: Stripe Checkout session creation, webhook-driven payment event processing, automatic tenant provisioning on successful payment, a post-signup setup wizard for business configuration, and module access middleware that gates features by the business's active subscription plan.

The implementation introduces new database schemas (`[billing]` and `[stripe]`) for subscription lifecycle tracking and Stripe integration data, adds a `StripePriceId` column to the existing `[dbo].[Plan]` table, and replaces the `BusinessPlan` table with `billing.Subscription` as the single source of truth for subscription status.

## Glossary

- **Checkout_Service**: The ASP.NET Core service responsible for creating Stripe Checkout Sessions and redirecting users to the Stripe-hosted payment page.
- **Webhook_Handler**: The ASP.NET Core API controller at `/api/webhooks/stripe` that receives and processes Stripe webhook events.
- **Provisioning_Service**: The service that creates a new tenant (Business, User association, Subscription, module permissions) upon successful payment confirmation.
- **Setup_Wizard**: The post-signup page flow where a new business owner configures their business details (name, VAT number, address, logo, currency) before accessing the dashboard.
- **Module_Access_Middleware**: The ASP.NET Core authorization policy that gates access to platform modules by verifying the business's active subscription includes the requested module.
- **Stripe_Checkout_Session**: A Stripe-hosted payment page session created via the Stripe API that collects payment details and processes the initial subscription charge.
- **Webhook_Event**: A JSON payload sent by Stripe to the platform's webhook endpoint notifying of payment lifecycle events.
- **Billing_Subscription**: The `billing.Subscription` record representing a business's active subscription, including status, plan reference, and billing period dates.
- **Stripe_Customer**: The `stripe.Customer` record mapping a Portal BusinessId to a Stripe Customer ID.
- **Webhook_Event_Log**: The `stripe.WebhookEvent` record used for idempotency to prevent duplicate processing of the same Stripe event.
- **Plan**: The `[dbo].[Plan]` record defining a subscription tier with pricing, user limits, and a `StripePriceId` linking to the corresponding Stripe Price object.
- **PendingRegistration**: The existing `[membership].[PendingRegistration]` record tracking a user's selected plan between registration and checkout completion.
- **Business**: The tenant entity in `[dbo].[Business]` representing a subscribing company.
- **BusinessProfile**: The `[dbo].[BusinessProfile]` record holding business configuration (address, VAT number, currency, logo reference).
- **PlanFeature**: The `[dbo].[PlanFeature]` record mapping which modules are included in each Plan.

## Requirements

### Requirement 1: Stripe Checkout Session Creation

**User Story:** As a newly registered user who has confirmed my email, I want to be redirected to a Stripe payment page for my selected plan, so that I can complete my subscription payment securely.

#### Acceptance Criteria

1. WHEN an authenticated, email-confirmed user navigates to the checkout endpoint with a PendingRegistration that exists, is not marked as completed, and references an active Plan, THE Checkout_Service SHALL create a Stripe Checkout Session in `subscription` mode using the Plan's `StripePriceId`.
2. WHEN the Stripe Checkout Session is created successfully, THE Checkout_Service SHALL redirect the user's browser to the Stripe-hosted Checkout URL.
3. WHEN creating the Stripe Checkout Session, THE Checkout_Service SHALL include the PendingRegistration Id and UserId as metadata on the session to enable correlation during webhook processing.
4. WHEN creating the Stripe Checkout Session, THE Checkout_Service SHALL set the success URL to a platform route that displays a "payment successful, setting up your account" message.
5. WHEN creating the Stripe Checkout Session, THE Checkout_Service SHALL set the cancel URL to a platform route that displays a "payment cancelled" message with a button that navigates the user back to the checkout endpoint to retry.
6. IF the PendingRegistration record is already marked as completed, THEN THE Checkout_Service SHALL redirect the user to the dashboard instead of creating a new Checkout Session.
7. IF no PendingRegistration record exists for the authenticated user, THEN THE Checkout_Service SHALL redirect the user to the registration page.
8. IF the Plan referenced by the PendingRegistration has a null or empty StripePriceId, THEN THE Checkout_Service SHALL log the error including the PlanId and UserId, and display an error message indicating the plan is not available for purchase.
9. IF the Stripe API returns an error when creating the Checkout Session, THEN THE Checkout_Service SHALL log the full Stripe error details including the UserId and PlanId, and display an error message indicating payment setup failed with a button that navigates the user back to the checkout endpoint to retry.
10. THE Checkout_Service SHALL store Stripe API keys in User Secrets and retrieve them via ASP.NET Core configuration, never hardcoding keys in source code.
11. WHEN creating the Checkout Session, THE Checkout_Service SHALL set `allow_promotion_codes` to true to support future discount codes.
12. IF the user is not authenticated or their email is not confirmed, THEN THE Checkout_Service SHALL redirect the user to the login page without creating a Checkout Session.

---

### Requirement 2: Stripe Webhook Handler

**User Story:** As the platform, I want to receive and process Stripe payment events reliably, so that subscription lifecycle changes are reflected in the system in real time.

#### Acceptance Criteria

1. THE Webhook_Handler SHALL expose a POST endpoint at `/api/webhooks/stripe` that accepts Stripe webhook event payloads.
2. WHEN a webhook request is received, THE Webhook_Handler SHALL verify the request signature using the Stripe webhook signing secret before processing the event.
3. IF the webhook signature verification fails, THEN THE Webhook_Handler SHALL return HTTP 400 and log the failed verification attempt without processing the event.
4. WHEN a valid webhook event is received, THE Webhook_Handler SHALL check the Webhook_Event_Log for the event's Stripe Event Id before processing.
5. IF the event's Stripe Event Id already exists in the Webhook_Event_Log, THEN THE Webhook_Handler SHALL return HTTP 200 without reprocessing the event.
6. WHEN the event type is `checkout.session.completed`, THE Webhook_Handler SHALL invoke the Provisioning_Service to initiate the tenant provisioning flow for the associated user and plan.
7. WHEN the event type is `invoice.paid`, THE Webhook_Handler SHALL update the corresponding Billing_Subscription's CurrentPeriodEnd and record the payment amount, currency, and Stripe Invoice Id against the subscription.
8. WHEN the event type is `invoice.payment_failed`, THE Webhook_Handler SHALL update the Billing_Subscription status to `past_due` and insert a record into the system audit log with the Stripe Event Id, subscription identifier, and failure reason.
9. WHEN the event type is `customer.subscription.updated`, THE Webhook_Handler SHALL update the Billing_Subscription record with the new status, plan, and period dates from the Stripe event data.
10. WHEN the event type is `customer.subscription.deleted`, THE Webhook_Handler SHALL set the Billing_Subscription status to `cancelled` and record the CancelledAtUtc timestamp.
11. WHEN a webhook event has been successfully processed, THE Webhook_Handler SHALL insert a record into the Webhook_Event_Log with the Event Id, event type, and ProcessedAtUtc timestamp within the same database transaction as the state change.
12. THE Webhook_Handler SHALL return HTTP 200 for all successfully processed events.
13. IF an unhandled processing error occurs during event handling, THEN THE Webhook_Handler SHALL roll back the database transaction, log the error with the Stripe Event Id and event type, and return HTTP 500 to trigger Stripe's retry mechanism.
14. THE Webhook_Handler SHALL process each event's state changes and Webhook_Event_Log insertion within a single database transaction to ensure atomicity.
15. THE Webhook_Handler SHALL store the webhook signing secret in User Secrets and retrieve it via ASP.NET Core configuration.
16. IF the webhook event type is not one of the five handled types, THEN THE Webhook_Handler SHALL log the event type at informational level and return HTTP 200 without further processing.
17. IF a webhook event references a Billing_Subscription that does not exist in the system, THEN THE Webhook_Handler SHALL log the event type and Stripe subscription identifier as a warning and return HTTP 200 without processing the event.
18. THE Webhook_Handler SHALL complete all processing and return a response within 30 seconds of receiving the request to satisfy Stripe's delivery timeout.

---

### Requirement 3: Tenant Auto-Provisioning

**User Story:** As a user who has completed payment, I want my business account to be automatically created with the correct plan and permissions, so that I can start using the platform immediately.

#### Acceptance Criteria

1. WHEN the Provisioning_Service receives a `checkout.session.completed` event, THE Provisioning_Service SHALL create a new Business record with `IsActive` set to true and the Name set to the user's full name appended with "'s Business" as a placeholder.
2. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a UserBusiness record associating the user (from PendingRegistration.UserId) with the new Business, with `IsOwner` set to true, `IsDefault` set to true, and `IsActive` set to true.
3. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a Billing_Subscription record with Status `active`, the selected PlanId, CurrentPeriodStart from the Stripe subscription start date, and CurrentPeriodEnd from the Stripe subscription current period end.
4. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a Stripe_Customer record mapping the new BusinessId to the Stripe Customer Id from the checkout session.
5. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a UserBusinessPermission record for each PlanFeature record (where `IsIncluded` is true) associated with the selected Plan, setting the `Module` to the PlanFeature's ModuleName and `AccessLevel` to "full".
6. WHEN provisioning completes successfully, THE Provisioning_Service SHALL mark the PendingRegistration record as completed by setting `IsCompleted` to true and `CompletedAtUtc` to the current UTC timestamp.
7. THE Provisioning_Service SHALL execute all provisioning operations within a single database transaction to ensure atomicity.
8. IF any step of the provisioning process fails, THEN THE Provisioning_Service SHALL roll back the entire transaction, log the error with full context (UserId, PlanId, Stripe Session Id), and leave the PendingRegistration as incomplete for manual resolution.
9. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a billing Invoice record for the initial payment with status `paid` and the amount from the Stripe checkout session.
10. WHEN the Provisioning_Service creates the Business, THE Provisioning_Service SHALL create a billing Payment record linked to the initial invoice with the Stripe PaymentIntent Id from the checkout session.
11. IF the PendingRegistration referenced in the checkout session metadata does not exist or is already completed, THEN THE Provisioning_Service SHALL log a warning and skip provisioning without returning an error to Stripe.
12. IF the Provisioning_Service receives a `checkout.session.completed` event with a Stripe Session Id that has already been successfully provisioned, THEN THE Provisioning_Service SHALL skip provisioning, log an informational message, and return a success response to Stripe without creating duplicate records.
13. THE Provisioning_Service SHALL respond to the Stripe webhook request within 30 seconds to prevent Stripe from retrying the event delivery.

---

### Requirement 4: Post-Signup Setup Wizard

**User Story:** As a new business owner who has just completed payment, I want to configure my business details through a guided setup, so that my account is properly set up before I start using the platform.

#### Acceptance Criteria

1. WHEN an authenticated user with the `owner` role accesses the platform and their Business has no BusinessProfile record, THE Setup_Wizard SHALL redirect the user to the setup wizard page instead of the dashboard.
2. THE Setup_Wizard SHALL display a single-page form collecting: business name (required, text input, maximum 200 characters), VAT registration number (optional, text input, maximum 50 characters), business address fields (optional: address line 1 maximum 200 characters, address line 2 maximum 200 characters, city maximum 100 characters, postal code maximum 20 characters, country maximum 100 characters), logo upload (optional), and currency selection (required, dropdown defaulting to EUR with the symbol "€").
3. WHEN the user submits the setup wizard form with valid data, THE Setup_Wizard SHALL create the BusinessProfile record with the provided values and store null for any optional fields left empty.
4. WHEN the user uploads a logo, THE Setup_Wizard SHALL validate that the file is an image (PNG, JPG, or SVG), does not exceed 2MB in file size, and store it using the existing BusinessLogo entity.
5. WHEN the setup wizard form is submitted successfully, THE Setup_Wizard SHALL update the Business.Name field with the provided business name, replacing the placeholder value assigned during registration.
6. WHEN the setup wizard is completed successfully, THE Setup_Wizard SHALL redirect the user to the main dashboard within 2 seconds of successful submission.
7. IF the business name field is empty or exceeds 200 characters, THEN THE Setup_Wizard SHALL display a validation error for the business name field and SHALL NOT submit the form.
8. IF the VAT number exceeds 50 characters, THEN THE Setup_Wizard SHALL display a validation error for the VAT number field and SHALL NOT submit the form.
9. THE Setup_Wizard SHALL allow the user to skip optional fields and complete the wizard with only the required fields (business name and currency).
10. THE Setup_Wizard SHALL determine setup completion by checking whether a BusinessProfile record exists for the user's Business, without requiring a separate progress tracking table.
11. WHILE the user has not completed the setup wizard, THE Setup_Wizard SHALL display the wizard on every authenticated page access for that user, preventing navigation to other platform pages.
12. IF the uploaded logo file exceeds 2MB or is not a supported image format (PNG, JPG, or SVG), THEN THE Setup_Wizard SHALL display a validation error indicating the file constraint that was violated, without submitting the form.
13. IF the setup wizard form submission fails due to a server error, THEN THE Setup_Wizard SHALL display an error message indicating the submission could not be completed and SHALL preserve all user-entered form data so the user can retry without re-entering information.
14. IF the provided business name matches an existing Business.Name for a different tenant, THEN THE Setup_Wizard SHALL display a validation error indicating the business name is already in use.

---

### Requirement 5: Module Access Middleware

**User Story:** As a platform operator, I want each module to be gated by the business's active subscription plan, so that businesses can only access features included in their paid tier.

#### Acceptance Criteria

1. WHEN an authenticated user requests access to a platform module, THE Module_Access_Middleware SHALL verify that the user's Business has an active Billing_Subscription (Status = `active` or `trialing`) by querying the subscription record associated with the user's BusinessId.
2. WHEN the Billing_Subscription is active, THE Module_Access_Middleware SHALL verify that the requested module is included in the Business's Plan by checking the PlanFeature records where `IsIncluded = 1` and `ModuleName` matches the requested module identifier.
3. IF the Business does not have an active Billing_Subscription (Status is not `active`, `trialing`, or `past_due`), THEN THE Module_Access_Middleware SHALL deny access and redirect the user to a "subscription required" page explaining that their subscription is inactive.
4. IF the requested module is not included in the Business's active Plan (no matching PlanFeature record with `IsIncluded = 1`), THEN THE Module_Access_Middleware SHALL deny access and display an "upgrade required" page showing which plans include the requested module.
5. THE Module_Access_Middleware SHALL be implemented as an ASP.NET Core authorization policy that can be applied to controllers or actions via the `[Authorize]` attribute with a policy name.
6. THE Module_Access_Middleware SHALL check plan-level module access before checking user-level permissions within the business.
7. THE Module_Access_Middleware SHALL cache the Business's active plan and included modules for the duration of the HTTP request to avoid repeated database queries within a single request.
8. WHEN the Billing_Subscription status is `past_due`, THE Module_Access_Middleware SHALL allow access but display a warning banner indicating that payment is overdue.
9. IF the user is not associated with any Business, THEN THE Module_Access_Middleware SHALL deny access and redirect to an error page indicating no business association exists.
10. THE Module_Access_Middleware SHALL use the module identifiers defined in the `PortalModules` constants class for consistent module name resolution.
11. IF the requested module identifier is not a valid value in the `PortalModules.All` array, THEN THE Module_Access_Middleware SHALL deny access and log the invalid module request at warning level.
12. WHEN the Module_Access_Middleware completes plan verification within a single HTTP request, THE Module_Access_Middleware SHALL resolve the subscription status and plan features in no more than 2 database queries (one for subscription, one for plan features) before caching for the request duration.

---

### Requirement 6: Billing Database Schema

**User Story:** As the platform, I want a dedicated billing schema to track subscriptions, invoices, and payments, so that financial records are separated from operational data and support audit requirements.

#### Acceptance Criteria

1. THE Billing_Subscription table SHALL reside in the `[billing]` schema and contain: Id (INT IDENTITY PK), BusinessId (INT, NOT NULL), PlanId (INT, NOT NULL), Status (NVARCHAR(20), NOT NULL), CurrentPeriodStart (DATETIME, NOT NULL), CurrentPeriodEnd (DATETIME, NOT NULL), CancelledAtUtc (DATETIME, nullable), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
2. THE `billing.Invoice` table SHALL contain: Id (INT IDENTITY PK), BusinessId (INT, NOT NULL), AmountEur (DECIMAL(10,2), NOT NULL, CHECK >= 0.00), PeriodStart (DATETIME, NOT NULL), PeriodEnd (DATETIME, NOT NULL), Status (NVARCHAR(20), NOT NULL), PaidAtUtc (DATETIME, nullable), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
3. THE `billing.Payment` table SHALL contain: Id (INT IDENTITY PK), InvoiceId (INT, NOT NULL, FK to billing.Invoice), AmountEur (DECIMAL(10,2), NOT NULL, CHECK >= 0.00), Method (NVARCHAR(50), NOT NULL), PaidAtUtc (DATETIME, NOT NULL), StripePaymentIntentId (NVARCHAR(100), nullable), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
4. THE `stripe.Customer` table SHALL contain: Id (INT IDENTITY PK), BusinessId (INT, NOT NULL, FK to dbo.Business), StripeCustomerId (NVARCHAR(100), NOT NULL, unique), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
5. THE `stripe.WebhookEvent` table SHALL contain: Id (INT IDENTITY PK), EventId (NVARCHAR(100), NOT NULL, unique), Type (NVARCHAR(100), NOT NULL), ProcessedAtUtc (DATETIME, nullable), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
6. THE `[dbo].[Plan]` table SHALL be extended with a `StripePriceId` column (NVARCHAR(100), nullable) to link each plan to its corresponding Stripe Price object.
7. THE Billing_Subscription table SHALL have a foreign key from BusinessId to `[dbo].[Business].Id` and from PlanId to `[dbo].[Plan].Id`.
8. THE `billing.Invoice` table SHALL have a foreign key from BusinessId to `[dbo].[Business].Id`.
9. THE `billing.Payment` table SHALL have a foreign key from InvoiceId to `billing.Invoice.Id`.
10. THE `stripe.Customer` table SHALL have a foreign key from BusinessId to `[dbo].[Business].Id`.
11. THE Billing_Subscription Status column SHALL only accept the values: `active`, `past_due`, `cancelled`, `trialing`, `incomplete`, or `unpaid` enforced via a CHECK constraint.
12. THE `billing.Invoice` Status column SHALL only accept the values: `draft`, `open`, `paid`, `void`, or `uncollectible` enforced via a CHECK constraint.
13. THE `[billing]` and `[stripe]` schemas SHALL be created as new database schemas prior to table creation, using idempotent IF NOT EXISTS guards consistent with existing migration patterns.

---

### Requirement 7: Stripe Configuration and Security

**User Story:** As a developer, I want Stripe credentials and configuration to be securely managed, so that API keys are never exposed in source code or configuration files committed to version control.

#### Acceptance Criteria

1. THE platform SHALL store the Stripe Secret Key, Publishable Key, and Webhook Signing Secret in ASP.NET Core User Secrets during development.
2. THE platform SHALL access Stripe configuration values through a strongly-typed options class registered via `IOptions<StripeSettings>` in the dependency injection container.
3. WHEN the application starts, THE platform SHALL validate that all required Stripe configuration values (Secret Key, Publishable Key, and Webhook Signing Secret) are present and non-empty, and log a clear error message identifying each missing value by configuration key name if any are absent.
4. THE platform SHALL use the Stripe.net NuGet package for all Stripe API interactions.
5. THE platform SHALL configure the Stripe client with the secret key during service registration in `Program.cs` or a dedicated extension method.
6. IF the Stripe Secret Key is missing or empty at startup, THEN THE platform SHALL throw a descriptive exception preventing the application from starting in an unconfigured state.
7. THE Webhook_Handler SHALL use the Stripe webhook signing secret exclusively for signature verification and SHALL NOT fall back to unverified processing if the secret is unavailable.
8. IF the Webhook_Handler receives a request with an invalid or missing Stripe signature header, THEN THE Webhook_Handler SHALL reject the request with an HTTP 400 response and log the rejection at warning level including the request timestamp and source IP.

---

### Requirement 8: Logging and Observability

**User Story:** As a platform operator, I want comprehensive logging of all Stripe interactions and provisioning events, so that I can diagnose issues and audit payment flows.

#### Acceptance Criteria

1. WHEN a Stripe Checkout Session is created, THE Checkout_Service SHALL log the session Id, UserId, PlanId, and creation timestamp at informational level using Serilog structured logging.
2. WHEN a webhook event is received, THE Webhook_Handler SHALL log the event Id, event type, and processing result (success or failure) at informational level.
3. IF a webhook event fails processing, THEN THE Webhook_Handler SHALL log the full exception details including stack trace, event Id, and event type at error level.
4. WHEN tenant provisioning completes, THE Provisioning_Service SHALL log the new BusinessId, UserId, PlanId, and SubscriptionId at informational level.
5. IF tenant provisioning fails, THEN THE Provisioning_Service SHALL log the failure reason, UserId, PlanId, and Stripe Session Id at error level.
6. WHEN the Module_Access_Middleware denies access, THE Module_Access_Middleware SHALL log the denied UserId, requested module, BusinessId, and denial reason (one of: no_business_association, subscription_inactive, module_not_in_plan, invalid_module) at warning level.
7. THE platform SHALL use Serilog structured logging with named properties (not string interpolation) for all Stripe-related log entries to enable filtering and correlation in log aggregation tools.
8. IF a webhook event is received with a duplicate EventId (already recorded in `stripe.WebhookEvent`), THEN THE Webhook_Handler SHALL log the duplicate event Id at informational level and skip reprocessing.

---

### Requirement 9: Billing History View and Invoice Export

**User Story:** As a business owner, I want to view my payment history and download PDF invoices, so that I can keep financial records and share them with my accountant.

**UI Reference:** #[[file:.kiro/mocks/billing-history.html]]

#### Acceptance Criteria

1. THE platform SHALL provide a Billing page accessible to authenticated users with the `owner` role at the route `/Account/Billing`.
2. THE Billing page SHALL display the current subscription status (plan name, status, current period start and end dates, and next renewal date).
3. THE Billing page SHALL display a paginated table of all billing invoices for the user's Business, ordered by PaidAtUtc descending (most recent first).
4. EACH invoice row in the billing history table SHALL display: invoice date, period covered (start–end), amount in EUR, payment status, and a "Download PDF" action link.
5. WHEN the user clicks "Download PDF" for an invoice, THE platform SHALL generate a PDF document containing: business name, invoice number (derived from Id), invoice date, period covered, line items (plan name, quantity 1, unit price), subtotal, VAT amount (if applicable), total amount, payment method, and payment date.
6. THE PDF invoice SHALL include the 3 Inventors company header (logo, company name, address) and the subscribing business details (name, VAT number if available, address if available) from the BusinessProfile record.
7. THE Billing page SHALL display a summary section showing: total amount paid to date, number of invoices, and the date of the most recent payment.
8. IF the Business has no billing invoices, THEN THE Billing page SHALL display an empty state message indicating no payment history is available yet.
9. THE PDF generation SHALL use the existing PDF rendering pattern (DinkToPdf or equivalent) already established in the platform for Customer Statements.
10. THE Billing page SHALL be accessible only to users with the `owner` role for their Business; non-owner users SHALL see an access denied message.
11. THE Billing page SHALL include a link to the Stripe Customer Portal (when available) for managing payment methods and viewing Stripe-hosted invoices.
12. WHEN the Billing page loads, THE platform SHALL query `billing.Invoice` and `billing.Payment` records for the authenticated user's BusinessId.
