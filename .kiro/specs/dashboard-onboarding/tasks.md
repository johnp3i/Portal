# Implementation Plan: Dashboard Onboarding

## Overview

Upgrade the existing Getting Started card to a full onboarding wizard with 6 steps, progress indicator, celebration state, and database-persisted dismissal. Replace localStorage-based dismissal with a per-business `IsOnboardingDismissed` flag on the Business entity.

## Tasks

- [ ] 1. Database schema and entity update
  - [ ] 1.1 Create SQL migration to add `IsOnboardingDismissed` BIT column to `[portal].[Business]`
    - Add column with NOT NULL constraint and DEFAULT 0
    - Place migration in `Portal.Database/Migrations/` with next sequence number
    - _Requirements: 3.2, 3.3_
  - [ ] 1.2 Update `Business.cs` entity with `IsOnboardingDismissed` property and EF configuration
    - Add `public bool IsOnboardingDismissed { get; set; }` to `Business.cs`
    - Add `.HasDefaultValue(false)` in `ConfigureBusiness` method of `PortalDbContext.cs`
    - _Requirements: 3.2, 3.3_

- [ ] 2. Onboarding service
  - [ ] 2.1 Create `OnboardingStateDto` model in `Portal.Infrastructure/Models/`
    - Properties: IsVisible, IsCelebration, CompletedCount, TotalSteps, HasBusinessProfile, HasLogo, HasPaymentDetails, HasCustomer, HasQuotationOrInvoice, HasIssuedInvoice
    - _Requirements: 1.2, 1.3, 4.2_
  - [ ] 2.2 Create `IOnboardingService` interface and `OnboardingService` implementation
    - `GetOnboardingStateAsync(int businessId)` — queries existing entities to compute step completion
    - `DismissOnboardingAsync(int businessId)` — sets IsOnboardingDismissed = true
    - Short-circuit: if business.IsOnboardingDismissed is true, return IsVisible=false without further queries
    - _Requirements: 4.1, 4.3, 3.2_
  - [ ]* 2.3 Write property tests for OnboardingService computation logic
    - **Property 1: Panel Visibility Logic** — panel visible iff !dismissed AND completedCount < 6
    - **Property 3: Progress Count Consistency** — CompletedCount equals count of true step flags
    - **Property 4: Celebration State Activation** — IsCelebration true iff all 6 steps true and not dismissed
    - **Validates: Requirements 1.1, 1.2, 2.1, 3.3**

- [ ] 3. Checkpoint - Ensure service builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Controller integration
  - [ ] 4.1 Register `IOnboardingService` in DI (`Program.cs`)
    - Add `builder.Services.AddScoped<IOnboardingService, OnboardingService>();`
    - _Requirements: 4.1_
  - [ ] 4.2 Update `HomeController.Index()` to use `IOnboardingService`
    - Inject `IOnboardingService` into constructor
    - Replace existing ViewBag.HasBusinessProfile/HasCustomers/HasQuotations/HasInvoices/HasPayments with single `ViewBag.Onboarding = await _onboardingService.GetOnboardingStateAsync(businessId);`
    - Remove old Getting Started query code
    - _Requirements: 4.1, 4.2, 4.3_
  - [ ] 4.3 Add `AxPostDismissOnboarding` endpoint to HomeController
    - [HttpPost], calls `_onboardingService.DismissOnboardingAsync(businessId)`
    - Returns `Json(new { success, message })`
    - _Requirements: 3.2, 3.4_

- [ ] 5. View update
  - [ ] 5.1 Replace existing Getting Started section in `Index.cshtml` with new onboarding panel
    - Remove old `<section id="gettingStartedCard">` and its `<script>` block
    - Add new panel section reading from `ViewBag.Onboarding` (OnboardingStateDto)
    - Render checklist state with progress bar, 6 steps, dismiss button
    - Render celebration state when IsCelebration is true
    - Hide panel entirely when IsVisible is false
    - Position above KPI gauges (after Quick Actions, before gauge-row)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 3.1_
  - [ ] 5.2 Add dismiss JavaScript function using fetch + BlockUI + SweetAlert2
    - `dismissOnboarding()` — BlockUI.show, POST to `/Home/AxPostDismissOnboarding`, BlockUI.hide, hide panel on success or show Swal error on failure
    - Remove old `dismissGettingStarted()` function and localStorage references
    - _Requirements: 3.2, 3.4_

- [ ] 6. Final checkpoint - Verify end-to-end
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The implementation language is C# (ASP.NET Core MVC 8)
- This feature replaces the existing localStorage-based Getting Started card with a DB-persisted version
- No new database table needed — only a column addition to `[portal].[Business]`
- Property tests validate the pure computation logic of step completion and visibility

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1"] },
    { "id": 1, "tasks": ["2.2"] },
    { "id": 2, "tasks": ["2.3", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["5.2"] }
  ]
}
```
