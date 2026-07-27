# Concerns: Demo User → Real Registration Conversion

**Date:** 23 July 2026

---

## Status: Implemented — Pending Edge Case Review

The demo-to-real conversion flow is functional for the happy path. The following concerns remain for review before considering this production-hardened.

---

## Concern 1: Stripe Registration Path (No Promo Code)

**Scenario:** A demo user registers without a promo code (selects a paid plan → Stripe checkout).

**Risk:** Medium. The demo conversion fires (password reset, EmailConfirmed = false), then the flow redirects to Stripe checkout. If Stripe isn't configured or the user abandons checkout, they're left with:
- A converted user (new password, EmailConfirmed = false)
- No new business created
- Demo UserBusiness still active (IsDefault cleared by provisioning — but provisioning didn't run)

**Impact:** User can't log in (email not confirmed) and has no business context.

**Suggested fix:** Only convert the demo user AFTER successful provisioning, not during registration. Or: gate the conversion on the promo code path only.

---

## Concern 2: Demo Link Access After Registration (Race Condition)

**Scenario:** User registers (conversion sets EmailConfirmed = false) → before confirming email, clicks an old demo magic link.

**Risk:** Medium. The demo entry flow (`DemoController.Enter`) sets `EmailConfirmed = true` unconditionally on the user. This would bypass the email confirmation requirement for the real registration.

**Impact:** User's email is marked as confirmed without actually confirming. Provisioning could fire prematurely.

**Suggested fix:** In `DemoController.Enter`, check if the user has a `PendingRegistration` with `IsCompleted = false`. If yes, don't override `EmailConfirmed`.

---

## Concern 3: Multiple Demo Businesses

**Scenario:** A user has demo invitations across multiple demo businesses (e.g., two different demo environments).

**Risk:** Low. `IsDemoOnlyUserAsync` checks ALL linked businesses via `AllAsync(b => b.IsDemoAccount)`. If any is not a demo, conversion is blocked.

**Status:** Covered by current implementation.

---

## Concern 4: Email Confirmation Token Validity

**Scenario:** User registers, receives confirmation email. Before clicking the link, the demo session cookie is still valid.

**Risk:** Low. Demo uses `DemoScheme` (separate cookie), real auth uses primary scheme. No conflict.

**Status:** Covered by separate auth schemes.

---

## Concern 5: Demo UserBusiness Cleanup

**Scenario:** After conversion, the demo `UserBusiness` record remains (IsDefault = false, IsActive = true). The user can technically still access the demo business if they switch contexts.

**Risk:** Low. The demo business is read-only by permission filter. No data corruption risk.

**Decision:** Intentional — keeps demo access available for re-invitations.

---

## Concern 6: PendingRegistration Duplicate

**Scenario:** A converted demo user already has a stale `PendingRegistration` from a previous failed attempt.

**Risk:** Low. The current code creates a new `PendingRegistration` regardless. The old one remains orphaned (IsCompleted = false, never fulfilled).

**Suggested fix:** Before creating a new `PendingRegistration`, delete or mark as superseded any existing incomplete ones for the same user.

---

## Priority for Next Session

1. **Concern 2** (demo link race condition) — narrow window but security-adjacent
2. **Concern 1** (Stripe path without promo) — functional gap for non-promo registrations
3. **Concern 6** (stale PendingRegistration) — data hygiene
