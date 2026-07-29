# Tasks

## Task 1: Create ICheckoutSessionExpireService Interface

- [ ] 1.1 Create `Portal.Infrastructure/Services/ICheckoutSessionExpireService.cs` with the `TryExpirePendingSessionsAsync(int invoiceId, int businessId, string? excludeSessionId = null)` method signature
- [ ] 1.2 Add XML documentation explaining the service's purpose and the `excludeSessionId` parameter

## Task 2: Create CheckoutSessionExpireService Implementation

- [ ] 2.1 Create `Portal.Infrastructure/Services/CheckoutSessionExpireService.cs` implementing `ICheckoutSessionExpireService`
- [ ] 2.2 Inject `PortalDbContext`, `IStripeKeyResolutionService`, and `ILogger<CheckoutSessionExpireService>` via constructor
- [ ] 2.3 Implement pending session query: filter by `InvoiceId`, `BusinessId`, `Status = 'pending'`, and exclude `excludeSessionId` if provided
- [ ] 2.4 Implement Stripe key resolution via `IStripeKeyResolutionService.ResolveKeysAsync(businessId)`
- [ ] 2.5 Implement the expire loop: for each session, call `Stripe.Checkout.SessionService.ExpireAsync` with resolved keys in `RequestOptions`
- [ ] 2.6 Handle success: set `Status = 'expired'` and `CompletedAtUtc = DateTime.UtcNow` on the entity
- [ ] 2.7 Handle StripeException with `already expired/completed` response: update local status to match, continue without error log
- [ ] 2.8 Handle other StripeException: log warning with `StripeSessionId`, `InvoiceId`, and error details, continue to next session
- [ ] 2.9 Handle key resolution failure: log error and return immediately without processing
- [ ] 2.10 Wrap entire method body in try/catch — catch all exceptions, log, and return (never throw)
- [ ] 2.11 Add structured logging: start message (invoiceId, count), per-session success, per-session failure, summary (processed, succeeded, failed)
- [ ] 2.12 Call `SaveChangesAsync()` once after the loop (batch update)

## Task 3: Modify FinancialStatusEngine to Call Expire Service

- [ ] 3.1 Add `ICheckoutSessionExpireService` as a constructor parameter in `FinancialStatusEngine`
- [ ] 3.2 Add optional `string? excludeStripeSessionId = null` parameter to `RecalculateStatusAsync` (both interface and implementation)
- [ ] 3.3 After determining `newStatusId == StatusPaid` (3), call `_checkoutSessionExpireService.TryExpirePendingSessionsAsync(invoiceId, businessId, excludeStripeSessionId)` wrapped in try/catch
- [ ] 3.4 Update DI registration in `Program.cs` to resolve `ICheckoutSessionExpireService` into the `FinancialStatusEngine` constructor

## Task 4: Register CheckoutSessionExpireService in DI

- [ ] 4.1 Add `builder.Services.AddScoped<ICheckoutSessionExpireService, CheckoutSessionExpireService>()` in `Program.cs` under the Revenue Control section
- [ ] 4.2 Ensure the DI registration order is correct: `CheckoutSessionExpireService` must be registered before `FinancialStatusEngine` since the engine depends on it

## Task 5: Update Webhook Handler for Session Exclusion

- [ ] 5.1 In `StripeConnectService.HandleCheckoutCompletedAsync`, pass the `stripeSessionId` parameter to `RecalculateStatusAsync` as the `excludeStripeSessionId` so the just-completed session is not expired
- [ ] 5.2 Verify that the existing `HandleCheckoutExpiredAsync` flow is unaffected (it only updates the local DB record)

## Task 6: Property-Based Tests

- [ ] 6.1 Create `Portal.Tests/PropertyBased/CheckoutSessionExpirePropertyTests.cs`
- [ ] 6.2 Implement FsCheck generator for random lists of `StripeCheckoutSession` entities (0–10 items, random session IDs)
- [ ] 6.3 Implement FsCheck generator for random failure patterns (per-session success/fail at Stripe API level)
- [ ] 6.4 Write Property 1 test: "All pending sessions are attempted exactly once" — verify mock Stripe API receives exactly N calls for N sessions regardless of failure pattern
- [ ] 6.5 Write Property 2 test: "Successful expiration updates the local record" — verify Status='expired' and CompletedAtUtc is set for all sessions where mock returns success
- [ ] 6.6 Write Property 3 test: "Failed expiration preserves the local record" — verify Status remains 'pending' and CompletedAtUtc remains null for sessions where mock returns error
- [ ] 6.7 Write Property 4 test: "Webhook exclusion removes exactly one session" — verify N-1 expire calls are made when excludeSessionId matches one pending session

## Task 7: Unit and Integration Tests

- [ ] 7.1 Write unit test: empty pending sessions → no Stripe API calls, no errors
- [ ] 7.2 Write unit test: "already expired" Stripe response → local record updated, no warning logged
- [ ] 7.3 Write unit test: total Stripe unreachability → method returns without exception
- [ ] 7.4 Write unit test: key resolution failure → early return, no session processing
- [ ] 7.5 Write integration test: verify expire service is triggered when `RecalculateStatusAsync` determines status = Paid
- [ ] 7.6 Write integration test: verify expire service is NOT triggered when status remains non-Paid (partial payment)

## Task 8: Build Verification

- [ ] 8.1 Run `dotnet build` to verify compilation
- [ ] 8.2 Run `dotnet test` to verify all tests pass (existing + new)
- [ ] 8.3 Verify no regressions in existing `FinancialStatusEnginePropertyTests`
