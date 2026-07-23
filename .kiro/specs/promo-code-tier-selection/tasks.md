# Promo Code Tier Selection — Tasks

## Task 1: Create database migration
- Create `Portal.Database/Migrations/153_AddPlanIdToPromoCode.sql`
- Add `[PlanId] INT NULL` column with FK to `[dbo].[Plan]([Id])`
- Idempotent (IF NOT EXISTS check)

## Task 2: Update PromoCode entity
- Add `public int? PlanId { get; set; }` to `Portal.Infrastructure/Entities/PromoCode.cs`
- Add `public Plan? Plan { get; set; }` navigation property

## Task 3: Update PromoCode repository
- `InsertAsync`: add `[PlanId]` column and `@PlanId` parameter
- `GetByIdAsync`: add `[PlanId]` to SELECT
- `GetByCodeAsync`: add `[PlanId]` to SELECT
- `GetFilteredAsync` (if it selects columns): add `[PlanId]`

## Task 4: Update CreatePromoCodeRequest model
- Add `public int? PlanId { get; set; }` to `Portal.Web/Models/PromoCode/CreatePromoCodeRequest.cs`

## Task 5: Update PromoCodeService
- In `CreateAsync`, set `promoCode.PlanId = request.PlanId` when building the entity

## Task 6: Update admin UI (Create form + table)
- Add Plan dropdown to the create form in `Portal.Web/Views/PromoCode/Index.cshtml`
- Pass available plans via ViewBag from the controller's Index action
- Add "Plan" column to the promo codes table
- Show tier name or "Professional (default)" for NULL

## Task 7: Update PromoCodeController
- In the `Index` action, load plans from DB and pass as ViewBag.Plans
- No change to Create action (auto-binds PlanId from form)

## Task 8: Update RegistrationService
- Replace the hardcoded `"business"` plan lookup with PromoCode.PlanId resolution
- Inject `PromoCodeRepository` if not already available
- Fallback to Professional plan for codes with NULL PlanId

## Task 9: Verify build and test
- Verify build compiles
- Test scenarios:
  - Create promo code with Professional tier → redeem → user gets Professional modules
  - Create promo code with Foundation tier → redeem → user gets Foundation modules only
  - Existing promo code (NULL PlanId) → redeem → user gets Professional (fallback)

## Task 10: Update PromoCodeListItem DTO and mapping
- Add `PlanName` to the list item DTO so the table can display it
- In the mapping function (`MapToListItem`), resolve PlanId to plan name
