# Reminder System Master Toggle

**Date:** 27 August 2026, ~18:30 UTC
**Type:** Direct implementation (no spec)
**Migration:** `180_AddIsReminderSystemEnabledToBusiness.sql`

---

## Summary

Wired up the existing UI-only "Reminder System" toggle on the Payment Reminder Settings page (`/PaymentReminder/Settings`) to actually persist state and control the automated reminder pipeline. Previously the toggle only changed the label text — it didn't save anything or affect reminder delivery.

## What Changed

| Layer | File | Change |
|---|---|---|
| DB | `Portal.Database/Migrations/180_AddIsReminderSystemEnabledToBusiness.sql` | New `[IsReminderSystemEnabled] BIT NOT NULL DEFAULT 1` on `[portal].[Business]` |
| Entity | `Portal.Infrastructure/Entities/Business.cs` | Added `bool IsReminderSystemEnabled` property |
| EF Config | `Portal.Infrastructure/Data/PortalDbContext.cs` | Configured with `.IsRequired().HasDefaultValue(true)` |
| Repository | `Portal.Infrastructure/Repositories/BusinessRepository.cs` | Added `[IsReminderSystemEnabled]` to both `GetAllAsync` and `GetByIdAsync` SELECT queries |
| Service | `Portal.Infrastructure/Services/PaymentReminderService.cs` | `GetEligibleBusinessIdsAsync` now filters `.Where(bp => bp.Business.IsReminderSystemEnabled)` — businesses with the toggle off are excluded at SQL level |
| Controller | `Portal.Web/Controllers/PaymentReminderController.cs` | Injected `PortalDbContext`. `Settings()` passes `ViewBag.IsReminderSystemEnabled`. New `AxPostToggleReminderSystem(bool enabled)` endpoint |
| View | `Portal.Web/Views/PaymentReminder/Settings.cshtml` | Removed old cosmetic toggle from Suppression Rules card. Added new top-level "Reminder System" section with descriptive text, reading persisted state from ViewBag |
| JS | `Portal.Web/wwwroot/js/payment-reminder-settings.js` | Replaced cosmetic `onSystemToggle()` with `toggleReminderSystem(enabled)` — AJAX POST to `AxPostToggleReminderSystem`, BlockUI + SweetAlert2, reverts toggle on failure |

## Behaviour

- **Default:** `true` — existing businesses keep reminders active after migration
- **When enabled:** Background service includes the business in the daily evaluation cycle
- **When disabled:** Background service skips the business entirely — no automated reminders fire
- **Manual reminders** from the invoice detail page are **not affected** by this toggle
- Toggle persists immediately on click (no save button) — same pattern as auto-receipt and auto-signature toggles on the My Business page
- Page load reads the persisted state — no longer hardcoded as `checked`

## Deployment

Run migration `180_AddIsReminderSystemEnabledToBusiness.sql` before deploying.
