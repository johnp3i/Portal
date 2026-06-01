================================================================================
 SUBSCRIPTION VALIDATION ON LOGIN — How It Works
================================================================================

When a user logs in and navigates to any module-protected page, the platform
validates their subscription status in real time. This is NOT a one-time check
at login — it runs on EVERY request to a gated endpoint.

================================================================================
 VALIDATION FLOW (ModuleAccessAttribute)
================================================================================

The ModuleAccessAttribute is an authorization filter applied to controllers/actions
that require module access. It executes the following checks in order:

  Step 1: SuperAdmin Bypass
  ─────────────────────────
  If the user has the "SuperAdmin" role → skip all checks, grant access.

  Step 2: Business Association
  ────────────────────────────
  Read the "BusinessId" claim from the user's identity.
  If missing or invalid → show "No Business Association" error page (403).

  Step 3: Module Identifier Validation
  ─────────────────────────────────────
  Check the requested module name against PortalModules.All.
  If invalid → deny access (ForbidResult), log warning.

  Step 4: Subscription Status Check
  ──────────────────────────────────
  Call SubscriptionPlanService.GetAccessAsync(businessId).
  This queries [billing].[Subscription] for the business.

  Results are cached per-request (HttpContext.Items) to avoid repeated DB hits
  within the same HTTP request.

  Status handling:
    ┌─────────────────┬──────────────────────────────────────────────────┐
    │ Status          │ Action                                           │
    ├─────────────────┼──────────────────────────────────────────────────┤
    │ active          │ Allow access                                     │
    │ trialing        │ Allow access                                     │
    │ past_due        │ Allow access + show warning banner               │
    │ cancelled       │ DENY → show "Subscription Required" page (403)   │
    │ incomplete      │ DENY → show "Subscription Required" page (403)   │
    │ unpaid          │ DENY → show "Subscription Required" page (403)   │
    │ (no record)     │ DENY → show "Subscription Required" page (403)   │
    └─────────────────┴──────────────────────────────────────────────────┘

  Step 5: Module Inclusion Check
  ──────────────────────────────
  Check if the requested module exists in the plan's PlanFeature records
  where IsIncluded = true.
  If module NOT in plan → show "Upgrade Required" page (403).

  Step 6: User-Level Permission Check
  ────────────────────────────────────
  Call IPermissionService.GetAccessLevelAsync(userId, module, businessId).
  Check if the user's access level meets the required level (e.g., ReadOnly, Full).
  If insufficient → deny access (ForbidResult).

================================================================================
 WHERE THE STATUS COMES FROM
================================================================================

The [billing].[Subscription] table holds the current status for each business.
This status is updated by Stripe webhooks:

  • checkout.session.completed  → status set to "active"
  • invoice.paid               → status remains "active"
  • invoice.payment_failed     → status set to "past_due" or "unpaid"
  • customer.subscription.updated → status synced from Stripe
  • customer.subscription.deleted → status set to "cancelled"

The webhook controller at POST /api/webhooks/stripe receives these events
from Stripe in real time and updates the database accordingly.

================================================================================
 WHAT THE USER SEES
================================================================================

  • Active subscription → normal platform access
  • Past due → platform access WITH a yellow warning banner:
    "Payment Overdue — Your subscription payment is past due.
     Please update your payment method to avoid service interruption."
  • Cancelled/Unpaid → full lockout page:
    "Your Subscription Is Inactive — Your subscription has been cancelled
     or is no longer active. Visit the billing page to manage your subscription."
  • Module not in plan → upgrade page:
    "Module Not Available on Your Plan — The [module] module is not included
     in your current subscription plan. View Plans & Upgrade."

================================================================================
 SETUP WIZARD REDIRECT (Separate Filter)
================================================================================

In addition to subscription validation, the SetupWizardRedirectFilter runs
globally on every MVC action. It checks:

  1. User is authenticated
  2. User has IsOwner claim
  3. User has a BusinessId claim
  4. No BusinessProfile exists for that BusinessId
  5. Request is NOT targeting SetupWizard/Account/Checkout controllers

If all conditions are met → redirect to /Setup/Wizard.
This ensures new owners complete their business setup before accessing anything.

================================================================================
 KEY FILES
================================================================================

  Portal.Web/Security/ModuleAccessAttribute.cs
    — The authorization filter (Steps 1–6 above)

  Portal.Web/Services/Stripe/SubscriptionPlanService.cs
    — Queries subscription + plan features, caches per-request

  Portal.Web/Filters/SetupWizardRedirectFilter.cs
    — Global filter for setup wizard redirect

  Portal.Web/Filters/SubscriptionWarningResultFilter.cs
    — Transfers warning flag from HttpContext.Items to ViewData

  Portal.Web/Views/Shared/SubscriptionRequired.cshtml
    — Lockout page for inactive subscriptions

  Portal.Web/Views/Shared/UpgradeRequired.cshtml
    — Page shown when module not in plan

  Portal.Web/Views/Shared/_SubscriptionWarningBanner.cshtml
    — Warning banner partial for past_due status

================================================================================
 NOTES
================================================================================

  • There is NO grace period currently. If Stripe marks the subscription as
    cancelled or unpaid, the user is locked out on their next page load.
  • The validation is per-request, not per-session. If the subscription status
    changes mid-session (e.g., webhook updates it), the user sees the effect
    on their very next navigation.
  • SuperAdmins are never affected by subscription checks.
  • The 3 Inventors account (Business ID = 1) has a 4-year subscription seeded
    directly in the database — no Stripe dependency.

================================================================================
