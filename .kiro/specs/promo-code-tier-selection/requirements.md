# Promo Code Tier Selection

## Overview

Enhance the promo code system so that SuperAdmin can select which subscription tier (Foundation, Professional, or Enterprise) a promo code grants access to. Currently, all promo codes hardcode the "business" plan. After this change, each promo code carries its own PlanId, and the registration flow uses that PlanId to provision the correct tier.

## Background

- Promo codes grant prospects a free trial subscription without Stripe checkout
- The admin creates promo codes at `/Admin/PromoCodes`
- During registration, if a user enters a valid promo code, the system provisions them on that plan for the specified duration
- Currently: all promo codes force the "business" plan (legacy, before the 3-tier model existed)
- Target: admin selects Foundation/Professional/Enterprise when creating the promo code

## Requirements

### R1: Database — Add PlanId to PromoCode table
- Add `[PlanId] INT NULL` column to `[dbo].[PromoCode]`
- Add FK constraint referencing `[dbo].[Plan]([Id])`
- Nullable for backward compatibility (existing promo codes will have NULL, which should default to Professional at runtime)
- Migration script: `153_AddPlanIdToPromoCode.sql`

### R2: Entity — Update PromoCode entity
- Add `public int? PlanId { get; set; }` to `Portal.Infrastructure/Entities/PromoCode.cs`
- Add navigation property `public Plan? Plan { get; set; }`

### R3: Repository — Update queries
- `InsertAsync`: add `[PlanId]` to the INSERT statement and `@PlanId` parameter
- `GetByIdAsync` and `GetByCodeAsync`: add `[PlanId]` to the SELECT column list

### R4: Request model — Add PlanId
- Add `public int? PlanId { get; set; }` to `Portal.Web/Models/PromoCode/CreatePromoCodeRequest.cs`

### R5: Service — Pass PlanId through
- In `PromoCodeService.CreateAsync`, map `request.PlanId` to the entity's `PlanId`

### R6: Admin UI — Add Plan dropdown to Create form
- Add a `<select>` field for Plan selection in `Portal.Web/Views/PromoCode/Index.cshtml`
- Options: Foundation, Professional, Enterprise (loaded from database or hardcoded with known IDs)
- Default selection: Professional
- Field label: "Plan / Tier"
- Also display the plan name in the promo codes table (new column)

### R7: Registration — Use PromoCode's PlanId
- In `Portal.Web/Services/RegistrationService.cs`, replace the hardcoded `"business"` plan lookup:
  - Load the promo code by ID
  - Use its `PlanId` if set; fall back to Professional plan if `PlanId` is NULL
- The `PendingRegistration.PlanId` will then carry the correct tier
- `ProvisioningService.ProvisionPromoTrialAsync` already uses `request.PlanId` from PendingRegistration — no change needed there

### R8: Backward compatibility
- Existing promo codes with `PlanId = NULL` should default to Professional tier during redemption
- The admin UI should show "—" or "Professional (default)" for codes with no PlanId in the table

## Files to modify

| File | Change |
|------|--------|
| `Portal.Database/Migrations/153_AddPlanIdToPromoCode.sql` | New migration |
| `Portal.Infrastructure/Entities/PromoCode.cs` | Add PlanId property |
| `Portal.Infrastructure/Repositories/PromoCodeRepository.cs` | Add PlanId to INSERT/SELECT queries |
| `Portal.Web/Models/PromoCode/CreatePromoCodeRequest.cs` | Add PlanId property |
| `Portal.Web/Services/PromoCodeService.cs` | Pass PlanId through on create |
| `Portal.Web/Views/PromoCode/Index.cshtml` | Add Plan dropdown + table column |
| `Portal.Web/Services/RegistrationService.cs` | Read PlanId from promo code instead of hardcoding |

## Out of scope

- Editing PlanId on existing promo codes (can be added later)
- Stripe integration changes (promo codes bypass Stripe entirely)
- Changing the PromoCode validation endpoint (it already returns the code ID which is used to load PlanId at registration time)
