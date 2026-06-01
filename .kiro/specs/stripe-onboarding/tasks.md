# Implementation Plan: Stripe Onboarding

## Overview

This plan implements the Stripe integration and tenant onboarding pipeline for the Portal platform. The implementation follows a dependency-driven order: database schemas and migrations first, then EF Core entities and repositories, then services, then controllers and views, then middleware/filters, and finally property-based tests. The architecture introduces two new database schemas (`[billing]` and `[stripe]`), extends the existing `[dbo].[Plan]` table, and builds the full checkout → webhook → provisioning → setup wizard → module gating → billing history pipeline.

## Tasks

- [x] 1. Database schemas and migrations
  - [x] 1.1 Create billing and stripe schemas and subscription table
    - Create migration file `Portal.Database/Migrations/076_CreateBillingStripeSchemas.sql`
    - Create `[billing]` and `[stripe]` schemas with IF NOT EXISTS guards
    - Create `[billing].[Subscription]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → dbo.Business.Id), PlanId (INT NOT NULL FK → dbo.Plan.Id), Status (NVARCHAR(20) NOT NULL), StripeSubscriptionId (NVARCHAR(100) NULL), CurrentPeriodStart (DATETIME NOT NULL), CurrentPeriodEnd (DATETIME NOT NULL), CancelledAtUtc (DATETIME NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add CHECK constraint on Status: IN ('active','past_due','cancelled','trialing','incomplete','unpaid')
    - _Requirements: 6.1, 6.7, 6.11, 6.13_

  - [x] 1.2 Create billing invoice and payment tables
    - Create migration file `Portal.Database/Migrations/077_CreateBillingInvoicePaymentTables.sql`
    - Create `[billing].[Invoice]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → dbo.Business.Id), StripeInvoiceId (NVARCHAR(100) NULL), AmountEur (DECIMAL(10,2) NOT NULL CHECK >= 0.00), PeriodStart (DATETIME NOT NULL), PeriodEnd (DATETIME NOT NULL), Status (NVARCHAR(20) NOT NULL), PaidAtUtc (DATETIME NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add CHECK constraint on Status: IN ('draft','open','paid','void','uncollectible')
    - Create `[billing].[Payment]` table with columns: Id (INT IDENTITY PK), InvoiceId (INT NOT NULL FK → billing.Invoice.Id), AmountEur (DECIMAL(10,2) NOT NULL CHECK >= 0.00), Method (NVARCHAR(50) NOT NULL), PaidAtUtc (DATETIME NOT NULL), StripePaymentIntentId (NVARCHAR(100) NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - _Requirements: 6.2, 6.3, 6.8, 6.9, 6.12_

  - [x] 1.3 Create stripe customer and webhook event tables
    - Create migration file `Portal.Database/Migrations/078_CreateStripeCustomerWebhookEventTables.sql`
    - Create `[stripe].[Customer]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → dbo.Business.Id), StripeCustomerId (NVARCHAR(100) NOT NULL UNIQUE), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Create `[stripe].[WebhookEvent]` table with columns: Id (INT IDENTITY PK), EventId (NVARCHAR(100) NOT NULL UNIQUE), Type (NVARCHAR(100) NOT NULL), ProcessedAtUtc (DATETIME NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - _Requirements: 6.4, 6.5, 6.10, 6.13_

  - [x] 1.4 Add StripePriceId column to Plan table
    - Create migration file `Portal.Database/Migrations/079_AddStripePriceIdToPlan.sql`
    - ALTER TABLE `[dbo].[Plan]` ADD `StripePriceId` NVARCHAR(100) NULL
    - _Requirements: 6.6_

- [x] 2. Entity classes and DbContext configuration
  - [x] 2.1 Create billing entity classes
    - Create `Portal.Infrastructure/Entities/Billing/Subscription.cs` with properties: Id, BusinessId, PlanId, Status, StripeSubscriptionId, CurrentPeriodStart, CurrentPeriodEnd, CancelledAtUtc, CreatedAtUtc
    - Create `Portal.Infrastructure/Entities/Billing/BillingInvoice.cs` with properties: Id, BusinessId, StripeInvoiceId, AmountEur, PeriodStart, PeriodEnd, Status, PaidAtUtc, CreatedAtUtc
    - Create `Portal.Infrastructure/Entities/Billing/BillingPayment.cs` with properties: Id, InvoiceId, AmountEur, Method, PaidAtUtc, StripePaymentIntentId, CreatedAtUtc
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 2.2 Create stripe entity classes
    - Create `Portal.Infrastructure/Entities/Stripe/StripeCustomer.cs` with properties: Id, BusinessId, StripeCustomerId, CreatedAtUtc
    - Create `Portal.Infrastructure/Entities/Stripe/WebhookEvent.cs` with properties: Id, EventId, Type, ProcessedAtUtc, CreatedAtUtc
    - _Requirements: 6.4, 6.5_

  - [x] 2.3 Extend Plan entity and register entities in PortalDbContext
    - Add `StripePriceId` property to existing `Portal.Infrastructure/Entities/Plan.cs`
    - Add `DbSet<Subscription>`, `DbSet<BillingInvoice>`, `DbSet<BillingPayment>`, `DbSet<StripeCustomer>`, `DbSet<WebhookEvent>` to `PortalDbContext`
    - Configure entity mappings in `OnModelCreating`: schema names (`billing`, `stripe`), table names, FK relationships, unique indexes (StripeCustomerId, EventId), CHECK constraints, CreatedAtUtc defaults
    - _Requirements: 6.1, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10, 6.11, 6.12_

- [x] 3. Repositories
  - [x] 3.1 Create billing repositories
    - Create `Portal.Infrastructure/Repositories/SubscriptionRepository.cs` extending `GenericStoredProcedureRepository<Subscription>` with methods: `GetByBusinessIdAsync(int)`, `InsertAsync(Subscription)`, `UpdateStatusAsync(int id, string status, DateTime? cancelledAtUtc)`, `UpdatePeriodAsync(int id, DateTime periodStart, DateTime periodEnd, string status, int planId)`
    - Create `Portal.Infrastructure/Repositories/BillingInvoiceRepository.cs` with methods: `GetByBusinessIdPagedAsync(int businessId, int page, int pageSize)`, `GetByIdAsync(int id, int businessId)`, `InsertAsync(BillingInvoice)`, `GetCountByBusinessIdAsync(int businessId)`, `GetSummaryByBusinessIdAsync(int businessId)`
    - Create `Portal.Infrastructure/Repositories/BillingPaymentRepository.cs` with methods: `InsertAsync(BillingPayment)`, `GetByInvoiceIdAsync(int invoiceId)`
    - _Requirements: 6.1, 6.2, 6.3, 9.3, 9.7, 9.12_

  - [x] 3.2 Create stripe repositories
    - Create `Portal.Infrastructure/Repositories/StripeCustomerRepository.cs` extending `GenericStoredProcedureRepository<StripeCustomer>` with methods: `GetByBusinessIdAsync(int)`, `InsertAsync(StripeCustomer)`, `GetByStripeCustomerIdAsync(string)`
    - Create `Portal.Infrastructure/Repositories/WebhookEventRepository.cs` extending `GenericStoredProcedureRepository<WebhookEvent>` with methods: `ExistsByEventIdAsync(string eventId)`, `InsertAsync(WebhookEvent)`
    - _Requirements: 6.4, 6.5, 2.4, 2.5_

- [x] 4. Stripe configuration and service interfaces
  - [x] 4.1 Create StripeSettings and startup validation
    - Create `Portal.Web/Configuration/StripeSettings.cs` with properties: SecretKey, PublishableKey, WebhookSigningSecret
    - Register `IOptions<StripeSettings>` from User Secrets in `Program.cs`
    - Add startup validation: throw descriptive exception if any required value is missing or empty
    - Configure `StripeConfiguration.ApiKey` with the SecretKey during service registration
    - Install `Stripe.net` NuGet package
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 4.2 Create service interfaces
    - Create `Portal.Web/Services/Stripe/ICheckoutService.cs` with method: `CreateCheckoutSessionAsync(string userId)`
    - Create `Portal.Web/Services/Stripe/IWebhookProcessingService.cs` with method: `ProcessEventAsync(string json, string signatureHeader)`
    - Create `Portal.Web/Services/Stripe/IProvisioningService.cs` with method: `ProvisionTenantAsync(ProvisioningRequest request)`
    - Create `Portal.Web/Services/Stripe/ISetupWizardService.cs` with methods: `IsSetupCompleteAsync(int businessId)`, `CompleteSetupAsync(int businessId, SetupWizardModel model)`, `IsBusinessNameTakenAsync(string name, int excludeBusinessId)`
    - Create `Portal.Web/Services/Stripe/ISubscriptionPlanService.cs` with method: `GetAccessAsync(int businessId)`
    - Create `Portal.Web/Services/Stripe/IBillingService.cs` with methods: `GetBillingOverviewAsync(int businessId)`, `GetInvoicesAsync(int businessId, int page, int pageSize)`, `GenerateInvoicePdfAsync(int invoiceId, int businessId)`
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 9.1_

  - [x] 4.3 Create result models and view models
    - Create `Portal.Web/Models/Stripe/CheckoutResult.cs` with properties: Success, CheckoutUrl, ErrorMessage, FailureReason (enum: NoPendingRegistration, AlreadyCompleted, PlanNotAvailable, StripeApiError)
    - Create `Portal.Web/Models/Stripe/ProvisioningRequest.cs` with properties: UserId, PendingRegistrationId, PlanId, StripeCustomerId, StripeSessionId, StripeSubscriptionId, StripePaymentIntentId, SubscriptionStart, SubscriptionEnd, AmountPaid, Currency
    - Create `Portal.Web/Models/Stripe/ProvisioningResult.cs` with properties: Success, BusinessId, ErrorMessage
    - Create `Portal.Web/Models/Stripe/SetupWizardModel.cs` with properties: BusinessName (required, max 200), VatNumber (optional, max 50), AddressLine1, AddressLine2, City, PostalCode, Country, CurrencySymbol (required), Logo (IFormFile optional)
    - Create `Portal.Web/Models/Stripe/SubscriptionAccessResult.cs` with properties: HasActiveSubscription, SubscriptionStatus, PlanName, IncludedModules (HashSet<string>)
    - Create `Portal.Web/Models/Stripe/BillingOverviewModel.cs`, `BillingInvoiceModel.cs`, `BillingSummaryModel.cs`
    - _Requirements: 1.1, 3.1, 4.2, 4.3, 5.1, 9.2, 9.3, 9.7_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement core services
  - [x] 6.1 Implement CheckoutService
    - Create `Portal.Web/Services/Stripe/CheckoutService.cs` implementing `ICheckoutService`
    - Load PendingRegistration by UserId from MembershipDbContext
    - Validate preconditions: not completed, Plan has non-null StripePriceId
    - Create Stripe Checkout Session in `subscription` mode with Plan's StripePriceId
    - Set metadata: PendingRegistrationId, UserId
    - Set success URL to `/Checkout/Success` and cancel URL to `/Checkout/Cancelled`
    - Set `allow_promotion_codes = true`
    - Log session creation (SessionId, UserId, PlanId) via Serilog structured logging
    - Return CheckoutResult with appropriate FailureReason on error
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 1.11, 8.1_

  - [x] 6.2 Write property test for checkout precondition enforcement
    - **Property 1: Checkout precondition enforcement**
    - **Validates: Requirements 1.1, 1.6, 1.8**

  - [x] 6.3 Implement WebhookProcessingService
    - Create `Portal.Web/Services/Stripe/WebhookProcessingService.cs` implementing `IWebhookProcessingService`
    - Verify Stripe signature using WebhookSigningSecret
    - Check idempotency via WebhookEventRepository.ExistsByEventIdAsync
    - Route to handler based on event type: checkout.session.completed, invoice.paid, invoice.payment_failed, customer.subscription.updated, customer.subscription.deleted
    - Wrap state changes + WebhookEvent insert in single database transaction
    - Return HTTP-appropriate result codes
    - Log all events with structured properties (EventId, Type, result)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10, 2.11, 2.12, 2.13, 2.14, 2.15, 2.16, 2.17, 2.18, 8.2, 8.3, 8.8_

  - [x] 6.4 Write property test for webhook idempotency
    - **Property 2: Webhook idempotency**
    - **Validates: Requirements 2.4, 2.5**

  - [x] 6.5 Write property test for webhook state transitions
    - **Property 3: Webhook state transitions**
    - **Validates: Requirements 2.8, 2.10**

  - [x] 6.6 Write property test for webhook subscription synchronization
    - **Property 4: Webhook subscription synchronization**
    - **Validates: Requirements 2.7, 2.9**

  - [x] 6.7 Implement ProvisioningService
    - Create `Portal.Web/Services/Stripe/ProvisioningService.cs` implementing `IProvisioningService`
    - Execute all operations in a single database transaction (BeginTransaction/Commit/Rollback)
    - Create Business (Name = "{FirstName} {LastName}'s Business", IsActive = true)
    - Create UserBusiness (IsOwner = true, IsDefault = true, IsActive = true)
    - Create Subscription (Status = "active", correct period dates)
    - Create StripeCustomer mapping (BusinessId → StripeCustomerId)
    - Create BillingInvoice (Status = "paid", AmountEur from session)
    - Create BillingPayment (linked to invoice, StripePaymentIntentId)
    - Create UserBusinessPermission for each PlanFeature where IsIncluded = true (AccessLevel = "full")
    - Mark PendingRegistration as completed (IsCompleted = true, CompletedAtUtc)
    - Handle idempotency: skip if PendingRegistration already completed or StripeSessionId already provisioned
    - Log provisioning result (BusinessId, UserId, PlanId, SubscriptionId) via Serilog
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 3.11, 3.12, 3.13, 8.4, 8.5_

  - [x] 6.8 Write property test for provisioning completeness
    - **Property 5: Provisioning completeness**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.9, 3.10**

  - [x] 6.9 Write property test for provisioning idempotency
    - **Property 6: Provisioning idempotency**
    - **Validates: Requirements 3.11, 3.12**

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement setup wizard and billing services
  - [x] 8.1 Implement SetupWizardService
    - Create `Portal.Web/Services/Stripe/SetupWizardService.cs` implementing `ISetupWizardService`
    - `IsSetupCompleteAsync`: check if BusinessProfile exists for the given BusinessId
    - `CompleteSetupAsync`: validate model, check business name uniqueness, create BusinessProfile record, update Business.Name, handle logo upload via existing BusinessLogo entity
    - `IsBusinessNameTakenAsync`: query Business.Name excluding the current business
    - Return typed result with validation errors
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.13, 4.14_

  - [x] 8.2 Write property test for setup wizard redirect enforcement
    - **Property 7: Setup wizard redirect enforcement**
    - **Validates: Requirements 4.1, 4.11**

  - [x] 8.3 Write property test for setup wizard form persistence
    - **Property 8: Setup wizard form persistence**
    - **Validates: Requirements 4.3, 4.5, 4.9**

  - [x] 8.4 Write property test for setup wizard input validation
    - **Property 9: Setup wizard input validation**
    - **Validates: Requirements 4.7, 4.8, 4.4, 4.12**

  - [x] 8.5 Write property test for business name uniqueness enforcement
    - **Property 10: Business name uniqueness enforcement**
    - **Validates: Requirements 4.14**

  - [x] 8.6 Implement SubscriptionPlanService
    - Create `Portal.Web/Services/Stripe/SubscriptionPlanService.cs` implementing `ISubscriptionPlanService`
    - `GetAccessAsync`: query Subscription by BusinessId, resolve PlanFeatures, build SubscriptionAccessResult
    - Implement request-scoped caching (IHttpContextAccessor Items dictionary) to avoid repeated DB queries
    - Handle missing subscription (return HasActiveSubscription = false)
    - Resolve module list from PlanFeature where IsIncluded = true
    - _Requirements: 5.1, 5.2, 5.6, 5.7, 5.12_

  - [x] 8.7 Write property test for module access decision correctness
    - **Property 11: Module access decision correctness**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.8, 5.11**

  - [x] 8.8 Implement BillingService
    - Create `Portal.Web/Services/Stripe/BillingService.cs` implementing `IBillingService`
    - `GetBillingOverviewAsync`: query current subscription status, plan name, period dates
    - `GetInvoicesAsync`: query billing.Invoice paginated, ordered by PaidAtUtc DESC, include payment info
    - `GenerateInvoicePdfAsync`: use existing IViewRenderService + DinkToPdf pattern to generate PDF with 3 Inventors header, business details, invoice number, date, period, line items, subtotal, VAT, total, payment method and date
    - Handle empty state (no invoices)
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9, 9.12_

  - [x] 8.9 Write property test for billing invoice ordering
    - **Property 12: Billing invoice ordering**
    - **Validates: Requirements 9.3**

  - [x] 8.10 Write property test for billing summary aggregation
    - **Property 13: Billing summary aggregation**
    - **Validates: Requirements 9.7, 9.8**

  - [x] 8.11 Write property test for invoice PDF content completeness
    - **Property 14: Invoice PDF content completeness**
    - **Validates: Requirements 9.4, 9.5, 9.6**

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Controllers
  - [x] 10.1 Implement CheckoutController
    - Create `Portal.Web/Controllers/CheckoutController.cs`
    - `[Authorize] [HttpGet] Index()`: call CheckoutService.CreateCheckoutSessionAsync, redirect to Stripe URL on success, handle failure cases (redirect to registration, dashboard, or display error)
    - `[Authorize] [HttpGet] Success()`: display "payment successful, setting up your account" message
    - `[Authorize] [HttpGet] Cancelled()`: display "payment cancelled" message with retry button linking back to /Checkout
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7, 1.9, 1.12_

  - [x] 10.2 Implement StripeWebhookController
    - Create `Portal.Web/Controllers/Api/StripeWebhookController.cs` as `[ApiController] [Route("api/webhooks/stripe")]`
    - `[HttpPost] HandleWebhook()`: read raw request body, call WebhookProcessingService.ProcessEventAsync, return appropriate HTTP status codes (200, 400, 500)
    - Do NOT apply `[Authorize]` — Stripe calls this endpoint directly
    - _Requirements: 2.1, 2.2, 2.3, 2.12, 2.13, 2.16, 2.18_

  - [x] 10.3 Implement SetupWizardController
    - Create `Portal.Web/Controllers/SetupWizardController.cs`
    - `[Authorize] [HttpGet] Wizard()`: return setup wizard view with empty SetupWizardModel
    - `[Authorize] [HttpPost] Wizard(SetupWizardModel model)`: validate model, call SetupWizardService.CompleteSetupAsync, redirect to dashboard on success, return view with errors on failure
    - Handle logo file upload validation (type: PNG/JPG/SVG, size: max 2MB)
    - Preserve form data on server error
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.12, 4.13, 4.14_

  - [x] 10.4 Implement BillingController
    - Create `Portal.Web/Controllers/BillingController.cs`
    - `[Authorize] [HttpGet] Index(int page = 1)`: call BillingService.GetBillingOverviewAsync and GetInvoicesAsync, return billing history view with subscription status, invoice table, and summary
    - `[Authorize] [HttpGet] DownloadInvoice(int id)`: call BillingService.GenerateInvoicePdfAsync, return FileResult with PDF content type
    - Restrict access to users with `owner` role
    - Route: `/Account/Billing`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.10, 9.11, 9.12_

- [ ] 11. Views
  - [x] 11.1 Create Checkout views
    - Create `Portal.Web/Views/Checkout/Success.cshtml` — payment successful message with "setting up your account" text
    - Create `Portal.Web/Views/Checkout/Cancelled.cshtml` — payment cancelled message with retry button navigating to /Checkout
    - Use existing `_Layout.cshtml` or `_IdentityLayout.cshtml` as appropriate
    - _Requirements: 1.4, 1.5_

  - [x] 11.2 Create SetupWizard view
    - Create `Portal.Web/Views/SetupWizard/Wizard.cshtml`
    - Single-page form with fields: business name (required, max 200), VAT number (optional, max 50), address fields (optional), logo upload (optional, PNG/JPG/SVG, max 2MB), currency dropdown (required, default EUR "€")
    - Client-side validation for required fields and file constraints
    - Server-side validation error display with `role="alert"` and `aria-invalid` pattern
    - Preserve form data on error (model binding)
    - Follow MyChair Design System (glass card, proper spacing, SweetAlert2 for errors)
    - _Requirements: 4.2, 4.3, 4.4, 4.7, 4.8, 4.9, 4.12, 4.13_

  - [x] 11.3 Create Billing views
    - Create `Portal.Web/Views/Billing/Index.cshtml` following the billing-history.html mock
    - Display current subscription status section (plan name, status badge, period dates, next renewal)
    - Display summary section (total paid, invoice count, last payment date)
    - Display paginated invoice table (date, period, amount EUR, status, Download PDF link)
    - Display empty state when no invoices exist
    - Include link to Stripe Customer Portal (when available)
    - Follow layout standards: filter card → data table card pattern, pagination with `margin-top:18px`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.7, 9.8, 9.11_

  - [x] 11.4 Create invoice PDF Razor template
    - Create `Portal.Web/Views/Billing/_InvoicePdf.cshtml` (partial view for PDF rendering)
    - Include 3 Inventors company header (logo, name, address)
    - Include subscribing business details (name, VAT number if available, address if available)
    - Include invoice number (derived from Id), invoice date, period covered
    - Include line items table (plan name, qty 1, unit price)
    - Include subtotal, VAT amount (if applicable), total amount
    - Include payment method and payment date
    - _Requirements: 9.4, 9.5, 9.6, 9.9_

- [ ] 12. Middleware, filters, and module access
  - [x] 12.1 Implement SetupWizardRedirectFilter
    - Create `Portal.Web/Filters/SetupWizardRedirectFilter.cs` implementing `IAsyncActionFilter`
    - Check: user is authenticated, has IsOwner claim, has BusinessId claim, no BusinessProfile exists for that BusinessId, current request is NOT targeting SetupWizard controller
    - If all conditions met → redirect to `/Setup/Wizard`
    - Register globally in `Program.cs` via `MvcOptions.Filters`
    - _Requirements: 4.1, 4.10, 4.11_

  - [x] 12.2 Extend ModuleAccessAttribute for subscription plan checking
    - Modify existing `ModuleAccessAttribute` or create `SubscriptionPlanFilter` that integrates with the existing authorization pattern
    - Check order: (1) SuperAdmin bypass, (2) resolve BusinessId from claims, (3) check Subscription status via SubscriptionPlanService.GetAccessAsync, (4) check PlanFeature includes requested module, (5) check user-level permission (existing IPermissionService)
    - Handle statuses: active/trialing → allow, past_due → allow with `ViewData["SubscriptionWarning"]` for banner, cancelled/incomplete/unpaid → redirect to "subscription required" page
    - Handle no Business association → redirect to error page
    - Handle module not in plan → display "upgrade required" page
    - Handle invalid module identifier → deny access, log warning
    - Use PortalModules.All for valid module validation
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 5.12, 8.6_

  - [x] 12.3 Create subscription status views
    - Create `Portal.Web/Views/Shared/SubscriptionRequired.cshtml` — explains subscription is inactive, link to billing page
    - Create `Portal.Web/Views/Shared/UpgradeRequired.cshtml` — shows which plans include the requested module
    - Add subscription warning banner partial `Portal.Web/Views/Shared/_SubscriptionWarningBanner.cshtml` for past_due status display
    - _Requirements: 5.3, 5.4, 5.8_

- [ ] 13. Service registration and wiring
  - [x] 13.1 Register all services in Program.cs
    - Register `ICheckoutService` → `CheckoutService` as scoped
    - Register `IWebhookProcessingService` → `WebhookProcessingService` as scoped
    - Register `IProvisioningService` → `ProvisioningService` as scoped
    - Register `ISetupWizardService` → `SetupWizardService` as scoped
    - Register `ISubscriptionPlanService` → `SubscriptionPlanService` as scoped
    - Register `IBillingService` → `BillingService` as scoped
    - Register all repositories (SubscriptionRepository, BillingInvoiceRepository, BillingPaymentRepository, StripeCustomerRepository, WebhookEventRepository) as scoped
    - Register SetupWizardRedirectFilter globally
    - Update PortalModules.All array if new modules are added
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 9.1_

- [x] 14. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Property test files should be placed in `Portal.Tests/PropertyBased/StripeOnboarding/` following existing naming conventions
- The existing `ModuleAccessAttribute` pattern is extended — not replaced
- All repositories follow the `GenericStoredProcedureRepository<T>` base class pattern
- SQL migrations use IF NOT EXISTS guards consistent with existing migration patterns
- Stripe.net NuGet package is used for all Stripe API interactions
- DinkToPdf is used for PDF generation following the existing StatementRenderer pattern
- All logging uses Serilog structured logging with named properties

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 4, "tasks": ["6.1", "6.3"] },
    { "id": 5, "tasks": ["6.2", "6.4", "6.5", "6.6", "6.7"] },
    { "id": 6, "tasks": ["6.8", "6.9", "8.1", "8.6", "8.8"] },
    { "id": 7, "tasks": ["8.2", "8.3", "8.4", "8.5", "8.7", "8.9", "8.10", "8.11"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 9, "tasks": ["11.1", "11.2", "11.3", "11.4"] },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 11, "tasks": ["13.1"] }
  ]
}
```
