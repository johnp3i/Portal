# Implementation Plan: Subscription Expiry Guard

## Overview

This plan implements a server-side subscription expiration enforcement layer that detects expired subscriptions at the point of module access, grants one grace access with a warning modal, atomically transitions the subscription to "cancelled", and denies all subsequent access. The implementation integrates into the existing `SubscriptionPlanService` and `ModuleAccessAttribute` pipeline without breaking the current Stripe webhook flow.

## Tasks

- [x] 1. Database migration and entity updates
  - [x] 1.1 Create SQL migration to add IsGraceAccessUsed column
    - Create file `Portal.Database/Migrations/080_AddIsGraceAccessUsedToSubscription.sql`
    - Add `[IsGraceAccessUsed] BIT NOT NULL CONSTRAINT [DF_Subscription_IsGraceAccessUsed] DEFAULT (0)` to `[billing].[Subscription]`
    - Use idempotent `IF NOT EXISTS` pattern consistent with existing migrations
    - _Requirements: 5.1_

  - [x] 1.2 Update Subscription entity with IsGraceAccessUsed property
    - Add `public bool IsGraceAccessUsed { get; set; }` to `Portal.Infrastructure/Entities/Billing/Subscription.cs`
    - _Requirements: 5.1_

  - [x] 1.3 Update SubscriptionRepository.GetByBusinessIdAsync to include IsGraceAccessUsed
    - Add `[billing].[Subscription].[IsGraceAccessUsed]` to the SELECT column list in `GetByBusinessIdAsync`
    - File: `Portal.Infrastructure/Repositories/SubscriptionRepository.cs`
    - _Requirements: 5.1, 1.1_

- [x] 2. Repository layer — atomic grace access consumption
  - [x] 2.1 Add ConsumeGraceAccessAsync method to SubscriptionRepository
    - Implement `public virtual async Task<bool> ConsumeGraceAccessAsync(int subscriptionId)` in `Portal.Infrastructure/Repositories/SubscriptionRepository.cs`
    - Use `UPDATE [billing].[Subscription] WITH (ROWLOCK) SET [Status] = 'cancelled', [CancelledAtUtc] = @CancelledAtUtc, [IsGraceAccessUsed] = 1 WHERE [billing].[Subscription].[Id] = @Id AND [billing].[Subscription].[IsGraceAccessUsed] = 0 AND [billing].[Subscription].[Status] = 'active'`
    - Return `true` if rows affected == 1, `false` otherwise
    - Use `ExecuteSqlRawAsync` with `SqlParameter` for `@Id` and `@CancelledAtUtc` (DateTime.UtcNow)
    - Follow existing repository pattern: try/catch with rethrow, full table names in SQL
    - _Requirements: 3.1, 3.3, 4.4, 5.2_

  - [ ]* 2.2 Write property test for atomic grace access state transition
    - **Property 5: Grace access triggers correct state transition**
    - **Validates: Requirements 3.1, 5.2**

- [x] 3. Service layer — expiry detection logic
  - [x] 3.1 Add IsGraceAccess property to SubscriptionAccessResult
    - Add `public bool IsGraceAccess { get; set; }` to `Portal.Web/Models/Stripe/SubscriptionAccessResult.cs`
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Implement expiry detection in SubscriptionPlanService.GetAccessAsync
    - After fetching the subscription, if `Status == "active"` and `CurrentPeriodEnd < DateTime.UtcNow`:
      - If `BusinessId == 1`: skip expiry detection, treat as valid (bypass)
      - If `IsGraceAccessUsed == false`: call `ConsumeGraceAccessAsync`; on success set `IsGraceAccess = true`, `HasActiveSubscription = true`, and set `HttpContext.Items["GraceAccessGranted"] = true`; on failure (race lost) set `HasActiveSubscription = false`
      - If `IsGraceAccessUsed == true`: set `HasActiveSubscription = false`
    - If `ConsumeGraceAccessAsync` throws an exception: log at Warning severity, allow current request (fail-open), set `IsGraceAccess = true` and `HasActiveSubscription = true`
    - Strict less-than comparison: `CurrentPeriodEnd < DateTime.UtcNow` (equal means still valid)
    - File: `Portal.Web/Services/Stripe/SubscriptionPlanService.cs`
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 2.1, 2.2, 3.4, 4.2, 4.3_

  - [ ]* 3.3 Write property test for non-expired active subscriptions
    - **Property 1: Non-expired active subscriptions are treated as valid**
    - **Validates: Requirements 1.2**

  - [ ]* 3.4 Write property test for expired subscription with unused grace
    - **Property 2: Expired active subscription with unused grace grants access with flag**
    - **Validates: Requirements 1.1, 2.1, 2.2**

  - [ ]* 3.5 Write property test for expired subscription with consumed grace
    - **Property 3: Expired active subscription with consumed grace denies access**
    - **Validates: Requirements 4.2, 4.3, 5.4**

  - [ ]* 3.6 Write property test for non-active statuses bypass
    - **Property 4: Non-active statuses bypass expiry detection**
    - **Validates: Requirements 1.5, 6.3**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Webhook handler updates
  - [x] 5.1 Update HandleSubscriptionUpdated to reset IsGraceAccessUsed on renewal
    - Modify `UpdatePeriodAsync` call in `HandleSubscriptionUpdated` to also reset `IsGraceAccessUsed = 0` when the webhook sets Status to "active" with a `CurrentPeriodEnd` later than the currently stored value
    - Add a new repository method `UpdatePeriodWithGraceResetAsync` or extend `UpdatePeriodAsync` with an additional parameter
    - File: `Portal.Web/Services/Stripe/WebhookProcessingService.cs` and `Portal.Infrastructure/Repositories/SubscriptionRepository.cs`
    - _Requirements: 5.3, 6.3_

  - [x] 5.2 Update HandleSubscriptionDeleted to skip already-cancelled subscriptions
    - Before calling `UpdateStatusAsync`, check if `subscription.Status` is already "cancelled"
    - If already cancelled: skip the status update, log at Info level, still record the webhook event and return HTTP 200
    - File: `Portal.Web/Services/Stripe/WebhookProcessingService.cs`
    - _Requirements: 6.1, 6.2, 6.4_

  - [ ]* 5.3 Write property test for webhook renewal grace reset
    - **Property 6: Webhook renewal resets grace access flag**
    - **Validates: Requirements 5.3**

  - [ ]* 5.4 Write unit tests for webhook coexistence scenarios
    - Test: webhook skips update for already-cancelled subscription
    - Test: webhook does not revert "cancelled" to "active" on race condition
    - _Requirements: 6.2, 6.4_

- [x] 6. View layer — grace access warning modal
  - [x] 6.1 Create _GraceAccessModal.cshtml partial view
    - Create file `Portal.Web/Views/Shared/_GraceAccessModal.cshtml`
    - Check `HttpContext.Items["GraceAccessGranted"]` — if true, render a `<script>` block with SweetAlert2 `Swal.fire` on `DOMContentLoaded`
    - Configuration: `icon: 'warning'`, `title: 'Subscription Expired'`, `text: 'Your subscription has expired. This is your last access. Please renew to continue using the platform.'`, `allowOutsideClick: false`, `allowEscapeKey: false`, `confirmButtonText: 'I Understand'`, `confirmButtonColor: '#0D5EA6'`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 6.2 Include _GraceAccessModal partial in _Layout.cshtml
    - Add `@await Html.PartialAsync("_GraceAccessModal")` before the closing `</body>` tag in the main layout
    - Ensure it renders after SweetAlert2 library script reference
    - _Requirements: 7.1, 2.3_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck + xUnit)
- Unit tests validate specific examples and edge cases
- The `ConsumeGraceAccessAsync` method uses `ROWLOCK` hint and `WHERE IsGraceAccessUsed = 0 AND Status = 'active'` to guarantee at most one concurrent request receives grace access
- The fail-open behaviour on `ConsumeGraceAccessAsync` exception is intentional per design — the user hasn't seen the warning yet
- Migration 080 must be applied to the database before deployment of the code changes

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "3.1"] },
    { "id": 1, "tasks": ["1.3", "2.1"] },
    { "id": 2, "tasks": ["2.2", "3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4", "3.5", "3.6", "5.1", "5.2"] },
    { "id": 4, "tasks": ["5.3", "5.4", "6.1"] },
    { "id": 5, "tasks": ["6.2"] }
  ]
}
```
