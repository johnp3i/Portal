# Promo code tier selection and demo user conversion

This change adds plan/tier binding to promo codes, a send-count limiter for admin email dispatch, and a demo-user conversion path during registration. When a promo code carries a `PlanId`, the registration flow locks the user's plan selection to that tier. Admin-facing changes add a "Sent" counter (max 3) with a reset capability, and the promo code list now shows associated plan names.

Watch for: **NullReferenceException** on `SelectedPlanId.Value` when disabled radios fail to submit (confirmed). **Cross-database consistency gap** in `IsDemoOnlyUserAsync` — `AllAsync` on an empty filtered set returns `true`, misclassifying users as demo-only (confirmed). **No rate limiting** on the anonymous `AxPostValidatePromoCode` endpoint (confirmed).

## High-level view

The registration service now handles two paths: new users and demo-user conversion. Demo detection queries across `MembershipDbContext` and `PortalDbContext` in separate round-trips without transactional protection. The password reset and profile update that follow are not atomic, so a partial failure leaves the account in an ambiguous state.

The promo code validation endpoint is anonymous. The antiforgery token prevents cross-site abuse but not first-party scripted enumeration. The endpoint also returns internal `planId` values to unauthenticated users.

Plan selection disables radio inputs via JavaScript after promo validation, injecting a hidden input to carry the value. `SelectedPlanId` is `[Required]` on the view model, but `RegisterAsync` unconditionally dereferences `.Value` before checking whether a promo code overrides the plan — if the hidden input injection fails, this throws.

The `MaxSendCount` constant is hardcoded in both the controller (`const int MaxSendCount = 3`) and the Razor view (`@item.SentCount >= 3`), creating a maintenance coupling that will silently drift if only one is updated.

<details>
<summary>Issues (7)</summary>

1. **NullReferenceException on SelectedPlanId.Value** — `RegisterAsync` unconditionally dereferences `model.SelectedPlanId!.Value` at line 108 before checking `ValidatedPromoCodeId`. If the radio group submits null (disabled radios, no hidden input injected), this throws. Move the promo-code plan resolution before the unconditional dereference, or guard with a null check.
2. **Cross-database consistency gap in IsDemoOnlyUserAsync** — Two queries hit two different DbContexts without a distributed transaction. More critically: if `businessIds` is non-empty but none of those IDs exist in `PortalDbContext.Businesses` (referential integrity gap), `AllAsync` on an empty filtered result returns `true`, incorrectly allowing the demo conversion path.
3. **No rate limiting on AxPostValidatePromoCode** — The endpoint is anonymous. An attacker with a valid antiforgery token (obtainable from the registration page) can probe codes. Add rate limiting or a per-session attempt cap.
4. **Hardcoded send limit in two places** — `MaxSendCount = 3` in the controller and literal `3` in the Razor view. Extract to a shared constant or pass via ViewBag to prevent drift.
5. **Hidden input collision risk** — The JS creates a hidden input named `SelectedPlanId`, but the disabled radios retain `name="SelectedPlanId"`. If a user re-enables radios via DevTools, duplicate form keys submit and ASP.NET model binding takes the last value. The server re-validates the promo code on POST, so this isn't exploitable — but removing the `name` attribute from radios when disabling would be cleaner.
6. **PromoCodeValidationService catch block loses exception variable** — The final catch uses `catch (Exception)` without `ex`, violating the coding standard. Should be `catch (Exception ex)`.
7. **Dead hidden input ValidatedPromoCodeId** — The hidden input is set client-side but the server-side Register POST re-validates the promo code and overwrites `model.ValidatedPromoCodeId` from the server result. The hidden input is never consumed, making it dead form data that could confuse future developers. Remove it or document its purpose as purely visual state.

</details>

<details>
<summary>Details</summary>

## NullReferenceException in RegisterAsync plan resolution

The registration service dereferences `model.SelectedPlanId!.Value` unconditionally at the top of the plan-resolution block:

```csharp
var planId = model.SelectedPlanId!.Value;
if (model.ValidatedPromoCodeId.HasValue)
{
    var promoCode = await _promoCodeRepository.GetByIdAsync(model.ValidatedPromoCodeId.Value);
    if (promoCode?.PlanId != null)
    {
        planId = promoCode.PlanId.Value;
    }
    ...
}
```

When the JavaScript disables the plan radio buttons, it injects a hidden `SelectedPlanId` input. If that injection fails (JS error, DOM timing issue, or user submits before the async fetch completes), `SelectedPlanId` arrives as null. The `[Required]` annotation on the view model would normally catch this at ModelState validation — but the service method shouldn't rely on that assumption for a property typed as `int?`. A null-forgiving operator (`!`) on a nullable type is a code smell that masks the real issue.

The fix: check `ValidatedPromoCodeId` first and resolve planId from the promo code, falling back to `SelectedPlanId` only when no promo code is present. Add a guard returning a failure result if both are null.

## Cross-database demo user detection

`IsDemoOnlyUserAsync` fetches `businessIds` from `MembershipDbContext.UserBusinesses`, then checks `PortalDbContext.Businesses.IsDemoAccount` for those IDs:

```csharp
var businessIds = await _membershipDbContext.UserBusinesses
    .Where(ub => ub.UserId == userId && ub.IsActive)
    .Select(ub => ub.BusinessId)
    .ToListAsync();

if (!businessIds.Any())
    return true;

var allAreDemos = await _portalDbContext.Businesses
    .Where(b => businessIds.Contains(b.Id))
    .AllAsync(b => b.IsDemoAccount);
```

Two failure modes:

1. **Temporal inconsistency** — If a demo business is converted to real between the two queries, the method returns `true` and allows overwriting a user attached to a now-real business.

2. **Referential integrity gap** — If `businessIds` contains IDs that don't exist in `PortalDbContext.Businesses` (stale FK, data migration issue), the `Where` clause filters them all out. `AllAsync` on an empty sequence returns `true` in LINQ, so the user is classified as demo-only and their password gets reset. This is the more dangerous scenario because it can happen without concurrent writes — just missing data.

Mitigation: after the `Where` filter, check that the result count matches `businessIds.Count`. If not, return `false` (fail-closed).

## Anonymous validation endpoint

`AxPostValidatePromoCode` returns `planId` and `planName` on success. While the code space (32^8) makes brute-force impractical, targeted probing against common patterns or codes shared via email links is feasible. The response leaks plan tier structure to unauthenticated sessions.

Consider returning only a boolean success plus `durationMonths` to the client. The `planId` needed for the hidden input could be encoded as a server-signed token rather than a raw integer.

## SendCode TOCTOU gap

```csharp
if (promoCode.SentCount >= MaxSendCount)
    return Json(new { success = false, ... });

// ... send email ...

await _promoCodeService.IncrementSentCountAsync(promoCodeId);
```

The check and the increment are not atomic. `IncrementSentCountAsync` does `SET [SentCount] = [SentCount] + 1` unconditionally. A safer pattern: `UPDATE ... SET SentCount = SentCount + 1 WHERE SentCount < @Max`, returning rows-affected to gate the email send. Given single-admin usage, this is low priority but trivial to fix.

</details>

<details>
<summary>File map</summary>

| File | Change |
|------|--------|
| `Portal.Infrastructure/Data/PortalDbContext.cs` | Expanded check constraint for DemoInvitationPermission Module values |
| `Portal.Infrastructure/Entities/PromoCode.cs` | Added `PlanId`, `SentCount`, and `Plan` navigation property |
| `Portal.Infrastructure/Repositories/PromoCodeRepository.cs` | Added PlanId/SentCount to all SELECT/INSERT queries, new IncrementSentCountAsync and ResetSentCountAsync methods |
| `Portal.Web/Controllers/AccountController.cs` | New AxPostValidatePromoCode endpoint |
| `Portal.Web/Controllers/PromoCodeController.cs` | Added IPlanRepository injection, MaxSendCount limit, ResetSentCount action, ViewBag.Plans |
| `Portal.Web/Filters/DemoPermissionFilter.cs` | Allow "Generate" prefixed actions for readonly access |
| `Portal.Web/Models/PromoCode/CreatePromoCodeRequest.cs` | Added PlanId property |
| `Portal.Web/Models/PromoCode/PromoCodeListItem.cs` | Added PlanId, PlanName, SentCount |
| `Portal.Web/Models/PromoCode/PromoCodeValidationResult.cs` | Added PlanId, PlanName |
| `Portal.Web/Services/IPromoCodeService.cs` | Added IncrementSentCountAsync and ResetSentCountAsync signatures |
| `Portal.Web/Services/PromoCodeService.cs` | Plan lookup resolution, increment/reset passthrough, PlanId in mapping |
| `Portal.Web/Services/PromoCodeValidationService.cs` | Plan resolution with professional fallback on validation success |
| `Portal.Web/Services/RegistrationService.cs` | Demo user conversion logic, cross-DB query, promo code plan resolution |
| `Portal.Web/Views/Account/Register.cshtml` | Validate button, hidden input injection, radio disable logic |
| `Portal.Web/Views/DemoInvitation/Create.cshtml` | Changes not analyzed (outside review scope) |
| `Portal.Web/Views/DemoInvitation/Index.cshtml` | Changes not analyzed (outside review scope) |
| `Portal.Web/Views/PromoCode/Index.cshtml` | Plan column, Sent count column, Reset button, reload after send |

Full diff: `git diff main`

</details>
