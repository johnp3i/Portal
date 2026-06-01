# Final Modules Before MVP Launch

## Context

The subscription plan mechanism is complete (Plan, PlanFeature, BusinessPlan schema, repositories, seed data, user limit enforcement in InvitationService, property-based tests). The remaining work enables the full self-service flow: a new user registers, selects a plan, pays, and starts working immediately.

---

## What's Done

- ✅ Landing page with pricing cards
- ✅ Subscription schema (Plan, PlanFeature, BusinessPlan tables in `[dbo]`)
- ✅ Business plan seeded with 9 modules
- ✅ User limit enforcement in InvitationService (MaxUsers check)
- ✅ Routing (unauthenticated `/` → landing page; authenticated `/` → dashboard)

---

## What's Left (in logical order)

### 1. Identity Pages (Module 11) ✅ Completed 2025-07-29

- [x] Registration page (collects email, password, name, plan selection)
- [x] Confirm Account page (email verification success/failure states)
- [x] Forgot Password page (email input, reset link sent confirmation)
- [x] Reset Password page (new password form with token validation)
- [x] Apply Identity Page Design Guide styling (frosted glass card, particle background)

### 2. Stripe Integration (Module 10.4–10.5)

- [ ] Create Stripe Checkout Session when user selects a plan on the registration/pricing page
- [ ] Implement Stripe webhook handler for payment events:
  - `checkout.session.completed` — payment successful, trigger provisioning
  - `invoice.paid` — recurring payment confirmed
  - `invoice.payment_failed` — payment failed, notify user
  - `customer.subscription.updated` — plan change detected
  - `customer.subscription.deleted` — subscription cancelled

### 3. Tenant Auto-Provisioning (Module 10.6)

- [ ] On successful Stripe payment (`checkout.session.completed`):
  - Create Business record (tenant)
  - Create first User with owner role
  - Create BusinessPlan association (link tenant to selected plan)
  - Assign module permissions per plan (from PlanFeature records)
  - Send confirmation email

### 4. Post-Signup Setup Wizard (Module 10.7)

- [ ] First-login experience for the new business owner:
  - Business name
  - VAT registration number
  - Business address
  - Logo upload
  - Currency selection
  - Redirect to dashboard on completion

### 5. Module Access Middleware (Module 10.8)

- [ ] Gate each module by checking the business's active plan includes that module (via PlanFeature table)
- [ ] Check plan-level access BEFORE checking user-level permissions
- [ ] Show "upgrade required" message when accessing a module not included in the current plan

### 6. Subscription Lifecycle (Module 10.9–10.11)

- [ ] Sidebar/topbar indicator showing plan name and subscription status
- [ ] Grace period handling for lapsed subscriptions (e.g., 7-day grace before lockout)
- [ ] "Billing required" lockout screen when subscription lapses
- [ ] Stripe Customer Portal link for self-service billing management (upgrade/downgrade, payment methods, invoice history)

### 7. Admin Visibility (Module 10.12)

- [ ] Super admin "Subscriptions" view showing:
  - All tenants with their active plans
  - Payment status per tenant
  - Monthly Recurring Revenue (MRR) summary
  - Subscription lifecycle events

---

## Critical Path

The minimum path to "user registers, pays, starts working":

```
Identity Pages → Stripe Checkout → Webhook → Auto-Provision → Setup Wizard → Module Access Middleware
```

| Step | Module Tasks | Blocking? |
|------|-------------|-----------|
| Identity Pages | 11.1, 11.2, 11.5 | Yes — user needs a registration form |
| Stripe Checkout | 10.4 | Yes — payment is required |
| Webhook Handler | 10.5 | Yes — triggers provisioning |
| Auto-Provisioning | 10.6 | Yes — creates the tenant |
| Setup Wizard | 10.7 | Yes — user needs to configure their business |
| Module Access Middleware | 10.8 | Yes — gates feature access by plan |
| Subscription Indicator | 10.9 | No — nice-to-have for launch |
| Grace Period / Lockout | 10.10 | No — can launch without (manual handling) |
| Stripe Customer Portal | 10.11 | No — can add post-launch |
| Admin Subscriptions View | 10.12 | No — internal tool, can follow |

---

## Recommended Execution Order

1. **Identity Pages** — Registration + Confirm Account (the entry point)
2. **Stripe Integration** — Checkout Session + Webhooks (the payment gate)
3. **Auto-Provisioning** — Tenant creation on payment success (the activation)
4. **Setup Wizard** — First-login business configuration (the onboarding)
5. **Module Access Middleware** — Plan-based feature gating (the enforcement)
6. **Subscription Lifecycle** — Indicators, grace period, lockout (the polish)
7. **Admin View** — Internal monitoring (the visibility)

---

## Notes

- The subscription schema supports future Starter and Enterprise tiers via data inserts alone (no schema changes needed)
- DisplayOrder values 1 and 3 are reserved for Starter and Enterprise respectively
- User limit enforcement is already active — new invitations are blocked when MaxUsers is reached
- Property-based tests cover all core subscription logic (7 properties, 34 test cases)
