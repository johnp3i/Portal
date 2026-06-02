# Implementation Plan: Subscription Status Indicator

## Overview

Implement a ViewComponent that displays the current business's subscription plan name and status badge in the sidebar of the authenticated layout. The component reuses the existing `SubscriptionPlanService` per-request cache, renders color-coded badges per the MyChair Design System, and conditionally links to `/Account/Billing` for business owners.

## Tasks

- [x] 1. Create ViewModel and ViewComponent class
  - [x] 1.1 Create `SubscriptionStatusIndicatorViewModel` in `Portal.Web/Models/ViewComponents/SubscriptionStatusIndicatorViewModel.cs`
    - Define properties: `PlanName` (default "No Plan"), `BadgeText` (default "No Subscription"), `BadgeBackgroundColor` (default "#C24A4A"), `BadgeTextColor` (default "#FFFFFF"), `IsOwner`, `HasActiveSubscription`, `IsGraceAccess`
    - Add XML documentation comments for each property
    - _Requirements: 6.6, 5.1, 5.2_

  - [x] 1.2 Create `SubscriptionStatusIndicatorViewComponent` in `Portal.Web/ViewComponents/SubscriptionStatusIndicatorViewComponent.cs`
    - Inject `ISubscriptionPlanService` and `ILogger<SubscriptionStatusIndicatorViewComponent>` via constructor
    - Implement `InvokeAsync()` method that:
      - Extracts `BusinessId` claim from `UserClaimsPrincipal` and parses as integer
      - Returns `Content(string.Empty)` if user is not authenticated, BusinessId is missing/null/non-numeric/zero/negative
      - Returns `Content(string.Empty)` if user is SuperAdmin with no valid BusinessId
      - Calls `ISubscriptionPlanService.GetAccessAsync(businessId)` (hits per-request cache)
      - Returns `Content(string.Empty)` if SuperAdmin and service returns null/empty subscription status and plan
      - Maps `SubscriptionAccessResult.SubscriptionStatus` to badge text and colors (case-insensitive): active→("Active","#129867"), trialing→("Trial","#0D5EA6"), past_due→("Past Due","#C8912E"), cancelled→("Cancelled","#C24A4A"), null/empty→("No Subscription","#C24A4A"), other→("Unknown","#C24A4A")
      - Applies "No Plan" fallback when PlanName is null or empty
      - Determines `IsOwner` from `IsOwner` claim (value "true")
      - Wraps all logic in try/catch, logging exceptions at Warning level and returning `Content(string.Empty)` on failure
    - _Requirements: 6.1, 6.2, 6.4, 6.5, 4.1, 4.2, 4.3, 5.1, 5.2, 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 2. Create Razor view and integrate into layout
  - [x] 2.1 Create `Default.cshtml` in `Portal.Web/Views/Shared/Components/SubscriptionStatusIndicator/Default.cshtml`
    - Set `@model SubscriptionStatusIndicatorViewModel`
    - For owners: render as `<a href="/Account/Billing" aria-label="View billing and subscription">` wrapping the indicator content
    - For non-owners: render as `<div role="status">` wrapping the indicator content
    - Display plan name inside a `<span class="nav-text">` element, truncated to 20 characters with "…" suffix if exceeded
    - Render badge pill with inline `background-color` and `color` from ViewModel, include `aria-label="Subscription status: {BadgeText}"`
    - Use CSS classes consistent with sidebar nav items for spacing and alignment
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.2, 3.3, 3.4, 7.1, 7.2, 7.3, 7.4, 7.5, 5.3, 5.4_

  - [x] 2.2 Add ViewComponent invocation to `Portal.Web/Views/Shared/_Layout.cshtml`
    - Insert `@await Component.InvokeAsync("SubscriptionStatusIndicator")` at the bottom of the `<aside class="sidebar">` element, after the Invite User nav item block and before the closing `</aside>` tag
    - _Requirements: 6.3, 1.1_

- [x] 3. Checkpoint - Verify component renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Write unit tests for ViewComponent
  - [x] 4.1 Create unit tests in `Portal.Tests/Unit/ViewComponents/SubscriptionStatusIndicatorViewComponentTests.cs`
    - Mock `ISubscriptionPlanService` using Moq
    - Test: SuperAdmin with null BusinessId returns empty content (Req 4.1)
    - Test: SuperAdmin with valid BusinessId renders indicator view (Req 4.2)
    - Test: SuperAdmin with BusinessId but no subscription record returns empty (Req 4.3)
    - Test: Owner user produces ViewModel with `IsOwner = true` (Req 3.1, 3.2)
    - Test: Non-owner user produces ViewModel with `IsOwner = false` (Req 3.3)
    - Test: Missing BusinessId claim returns empty content (Req 6.5)
    - Test: Non-numeric BusinessId claim returns empty content (Req 6.5)
    - Test: Zero BusinessId claim returns empty content (Req 6.5)
    - Test: Service called with correct parsed BusinessId integer (Req 6.2)
    - Test: Null PlanName in result maps to "No Plan" in ViewModel (Req 5.1)
    - Test: Null SubscriptionStatus in result maps to "No Subscription" badge (Req 5.2)
    - Test: Service exception caught and empty content returned (Error Handling)
    - _Requirements: 4.1, 4.2, 4.3, 3.1, 3.2, 3.3, 5.1, 5.2, 6.2, 6.5_

- [x] 5. Write property-based tests for correctness properties
  - [x]* 5.1 Write property test for status-to-badge mapping totality and determinism
    - **Property 1: Status-to-badge mapping is total and deterministic**
    - Generate random strings (including null, empty, arbitrary casing, special characters) for SubscriptionStatus
    - Assert: mapping always produces a non-empty BadgeText, valid hex BadgeBackgroundColor, and "#FFFFFF" BadgeTextColor
    - Assert: known statuses map correctly (case-insensitive), null/empty → "No Subscription", unknown → "Unknown"
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 5.2**

  - [x]* 5.2 Write property test for plan name display truncation
    - **Property 2: Plan name display truncation**
    - Generate random strings (including null, empty, various lengths) for PlanName
    - Assert: null/empty → "No Plan"; length ≤ 20 → unchanged; length > 20 → first 20 chars + "…"
    - **Validates: Requirements 1.2, 5.1**

  - [x]* 5.3 Write property test for link rendering conditioned on ownership
    - **Property 3: Link rendering is conditioned on ownership**
    - Generate random combinations of IsOwner flag and subscription data
    - Assert: IsOwner=true → anchor with href="/Account/Billing" and correct aria-label; IsOwner=false → no anchor, element with role="status"
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 7.2, 7.3**

  - [x]* 5.4 Write property test for invalid BusinessId producing empty content
    - **Property 4: Invalid BusinessId produces empty content**
    - Generate random invalid BusinessId values (null, empty, non-numeric strings, "0", negative numbers)
    - Assert: ViewComponent returns empty content result for all invalid values
    - **Validates: Requirements 6.5**

  - [x]* 5.5 Write property test for aria-label format consistency
    - **Property 5: Aria-label format consistency**
    - Generate random SubscriptionStatus values, map to badge configuration
    - Assert: aria-label equals "Subscription status: {BadgeText}" for every badge produced
    - **Validates: Requirements 7.1**

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck.Xunit (already available in Portal.Tests)
- Unit tests validate specific examples and edge cases using xUnit + Moq
- The ViewComponent reuses the per-request cached `SubscriptionAccessResult` from `ISubscriptionPlanService` — no additional database queries are introduced
- The sidebar collapse behavior is CSS-driven via the `.nav-text` class pattern already used by other sidebar elements

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["2.1", "2.2"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5"] }
  ]
}
```
