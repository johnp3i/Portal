# Promo Code Tier Selection — Design

## Data Flow

```
Admin creates promo code (with PlanId)
    → PromoCode table stores PlanId
    
Prospect enters promo code during registration
    → Validation succeeds → PromoCode.Id stored in session
    → Registration confirms email
    → RegistrationService loads PromoCode by Id → reads PlanId
    → PendingRegistration.PlanId = promoCode.PlanId ?? ProfessionalPlanId
    → ProvisionPromoTrialAsync provisions with that PlanId
    → User gets the correct tier's modules
```

## Database Change

```sql
ALTER TABLE [dbo].[PromoCode]
ADD [PlanId] INT NULL
CONSTRAINT [FK_PromoCode_Plan] FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plan]([Id]);
```

## UI Change — Create Form

Before:
```
[Duration] [Max Redemptions] [Expiry Date] [Email] [Create]
```

After:
```
[Plan/Tier ▼] [Duration] [Max Redemptions] [Expiry Date] [Email] [Create]
```

Dropdown options:
- Foundation (PlanId from `[dbo].[Plan]` where Slug='starter')
- Professional (PlanId from `[dbo].[Plan]` where Slug='professional') — default
- Enterprise (PlanId from `[dbo].[Plan]` where Slug='enterprise')

## UI Change — Table

Add "Plan" column after "Type" showing the tier name, or "Professional (default)" for NULL PlanId codes.

## Registration Logic Change

Current (`RegistrationService.cs` ~line 73):
```csharp
var businessPlan = await _planRepository.GetBySlugAsync("business");
planId = businessPlan.Id;
```

New:
```csharp
var promoCode = await _promoCodeRepository.GetByIdAsync(model.ValidatedPromoCodeId.Value);
if (promoCode?.PlanId != null)
{
    planId = promoCode.PlanId.Value;
}
else
{
    // Fallback for legacy codes without PlanId
    var professionalPlan = await _planRepository.GetBySlugAsync("professional");
    planId = professionalPlan!.Id;
}
```

## Controller Change

The `PromoCodeController` already receives `CreatePromoCodeRequest` from `[FromForm]`. Adding `PlanId` as a form field means it auto-binds. No controller code change needed beyond what's in the request model.

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Existing codes have NULL PlanId | Fallback to Professional in RegistrationService |
| Plan table IDs vary between environments | Always lookup by slug, never hardcode IDs |
| Admin selects wrong tier | Show tier description in dropdown ("Professional — automation + intelligence") |
