# Design Document: Dashboard Onboarding

## Architecture Overview

The onboarding feature is a **frontend-heavy enhancement** backed by a lightweight service method in the Dashboard pipeline. No new service class or repository is introduced — the existing `HomeController` and `PortalDbContext` are extended.

### Component Diagram

```
HomeController.Index()
       │
       ▼
 ┌─────────────────────────┐
 │ OnboardingService        │  (new service)
 │  GetOnboardingStateAsync │
 └─────────────────────────┘
       │
       ▼
 PortalDbContext queries:
  - BusinessProfiles
  - BusinessLogos
  - BusinessPaymentDetails
  - Customers
  - Quotations
  - Invoices
  - Business (IsOnboardingDismissed flag)
       │
       ▼
 OnboardingStateDto → View via ViewBag
       │
       ▼
 Index.cshtml renders Onboarding Panel (or hides it)
```

## Database Change

Add a single BIT column to the existing `[portal].[Business]` table:

```sql
-- ============================================================
-- Add IsOnboardingDismissed flag to Business table
-- ============================================================

USE [Guardian]
GO

ALTER TABLE [portal].[Business]
ADD [IsOnboardingDismissed] BIT NOT NULL CONSTRAINT [DF_Business_IsOnboardingDismissed] DEFAULT 0;
GO
```

**Entity Update** — Add property to `Business.cs`:

```csharp
public bool IsOnboardingDismissed { get; set; }
```

**EF Configuration** — In `ConfigureBusiness`:

```csharp
entity.Property(e => e.IsOnboardingDismissed)
    .IsRequired()
    .HasDefaultValue(false);
```

## Data Model

### OnboardingStateDto

```csharp
namespace Portal.Infrastructure.Models;

public class OnboardingStateDto
{
    public bool IsVisible { get; set; }
    public bool IsCelebration { get; set; }
    public int CompletedCount { get; set; }
    public int TotalSteps => 6;

    public bool HasBusinessProfile { get; set; }
    public bool HasLogo { get; set; }
    public bool HasPaymentDetails { get; set; }
    public bool HasCustomer { get; set; }
    public bool HasQuotationOrInvoice { get; set; }
    public bool HasIssuedInvoice { get; set; }
}
```

## Service Layer

### IOnboardingService

```csharp
namespace Portal.Infrastructure.Services;

public interface IOnboardingService
{
    Task<OnboardingStateDto> GetOnboardingStateAsync(int businessId);
    Task DismissOnboardingAsync(int businessId);
}
```

### OnboardingService Implementation

```csharp
public class OnboardingService : IOnboardingService
{
    private readonly PortalDbContext _dbContext;

    public OnboardingService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OnboardingStateDto> GetOnboardingStateAsync(int businessId)
    {
        try
        {
            var business = await _dbContext.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null || business.IsOnboardingDismissed)
            {
                return new OnboardingStateDto { IsVisible = false };
            }

            var hasProfile = await _dbContext.BusinessProfiles
                .AnyAsync(bp => bp.BusinessId == businessId
                    && bp.AddressLine1 != null && bp.AddressLine1 != ""
                    && bp.VatRegistrationNumber != null && bp.VatRegistrationNumber != "");

            var hasLogo = await _dbContext.BusinessLogos
                .AnyAsync(bl => bl.BusinessId == businessId);

            var hasPaymentDetails = await _dbContext.BusinessPaymentDetails
                .AnyAsync(pd => pd.BusinessId == businessId && pd.IsActive);

            var hasCustomer = await _dbContext.Customers
                .AnyAsync(c => c.BusinessId == businessId);

            var hasQuotationOrInvoice = await _dbContext.Quotations.AnyAsync(q => q.BusinessId == businessId)
                || await _dbContext.Invoices.AnyAsync(i => i.BusinessId == businessId);

            var hasIssuedInvoice = await _dbContext.Invoices
                .AnyAsync(i => i.BusinessId == businessId && i.InvoiceStatusTypeId == 2);

            var completedCount = new[] { hasProfile, hasLogo, hasPaymentDetails, hasCustomer, hasQuotationOrInvoice, hasIssuedInvoice }
                .Count(b => b);

            var isCelebration = completedCount == 6;

            return new OnboardingStateDto
            {
                IsVisible = true,
                IsCelebration = isCelebration,
                CompletedCount = completedCount,
                HasBusinessProfile = hasProfile,
                HasLogo = hasLogo,
                HasPaymentDetails = hasPaymentDetails,
                HasCustomer = hasCustomer,
                HasQuotationOrInvoice = hasQuotationOrInvoice,
                HasIssuedInvoice = hasIssuedInvoice
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DismissOnboardingAsync(int businessId)
    {
        try
        {
            var business = await _dbContext.Businesses
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business != null)
            {
                business.IsOnboardingDismissed = true;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
```

## Controller Changes

### HomeController — Index Action

Replace the existing Getting Started ViewBag logic with:

```csharp
// Onboarding state
var onboardingState = await _onboardingService.GetOnboardingStateAsync(businessId);
ViewBag.Onboarding = onboardingState;
```

### HomeController — Dismiss Endpoint

```csharp
[HttpPost]
public async Task<IActionResult> AxPostDismissOnboarding()
{
    try
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _onboardingService.DismissOnboardingAsync(businessId);
        return Json(new { success = true, message = "Onboarding dismissed." });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Something went wrong. Please try again." });
    }
}
```

## View Design

### Panel Placement

The onboarding panel renders **above** the KPI gauges (after Quick Actions, before the `gauge-row`).

### Panel States

1. **Checklist state** — Shows progress indicator + 6 steps + dismiss button
2. **Celebration state** — Shows success icon + congratulations text + "Got it" button
3. **Hidden** — When dismissed or when `IsVisible = false`

### HTML Structure (Checklist State)

```html
<section id="onboardingPanel" class="glass card-pad" style="margin-bottom:22px;border-left:4px solid var(--blue);">
    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;">
        <div style="display:flex;align-items:center;gap:10px;">
            <svg ...rocket icon.../>
            <h3 style="font-family:'Manrope',sans-serif;font-size:15px;font-weight:700;">
                Getting Started
            </h3>
            <span style="font-size:12px;font-weight:600;color:#5a6a7a;background:rgba(13,94,166,.06);padding:2px 10px;border-radius:50px;">
                @onboarding.CompletedCount of 6 completed
            </span>
        </div>
        <button type="button" onclick="dismissOnboarding()" style="...dismiss styles...">
            Dismiss
        </button>
    </div>
    <!-- Progress bar -->
    <div style="height:4px;background:rgba(13,94,166,.08);border-radius:2px;margin-bottom:16px;">
        <div style="height:100%;width:@((onboarding.CompletedCount * 100 / 6))%;background:var(--blue);border-radius:2px;transition:width .3s;"></div>
    </div>
    <!-- Checklist items -->
    <div style="display:flex;flex-direction:column;gap:8px;">
        @* Each step rendered with conditional check/circle icon *@
    </div>
</section>
```

### HTML Structure (Celebration State)

```html
<section id="onboardingPanel" class="glass card-pad" style="margin-bottom:22px;border-left:4px solid #129867;">
    <div style="display:flex;align-items:center;gap:14px;">
        <div style="width:44px;height:44px;border-radius:50%;background:rgba(18,152,103,.1);display:flex;align-items:center;justify-content:center;">
            <svg ...party icon.../>
        </div>
        <div>
            <h3 style="font-family:'Manrope',sans-serif;font-size:15px;font-weight:700;color:#129867;">
                Setup Complete!
            </h3>
            <p style="font-size:13px;color:#5a6a7a;margin:0;">
                You're all set. Your business is ready to create quotations, send invoices, and track revenue.
            </p>
        </div>
        <button type="button" onclick="dismissOnboarding()" style="...got-it styles...">
            Got it
        </button>
    </div>
</section>
```

### JavaScript — Dismiss Action

```javascript
async function dismissOnboarding() {
    BlockUI.show('Saving...');
    try {
        var response = await fetch('/Home/AxPostDismissOnboarding', {
            method: 'POST',
            headers: { 'RequestVerificationToken': getAntiForgeryToken() }
        });
        var data = await response.json();
        BlockUI.hide();

        if (data.success) {
            document.getElementById('onboardingPanel').style.display = 'none';
        } else {
            Swal.fire({ title: 'Error', text: data.message, icon: 'error', confirmButtonColor: '#0D5EA6' });
        }
    } catch (e) {
        BlockUI.hide();
        Swal.fire({ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' });
    }
}
```

## Step Definitions Table

| # | Label | Completion Condition | Link |
|---|-------|---------------------|------|
| 1 | Complete your business profile | BusinessProfile exists with non-empty Name, AddressLine1, VatRegistrationNumber | `/MyBusiness` |
| 2 | Upload your logo | At least 1 BusinessLogo row for businessId | `/MyBusiness` (Logos tab) |
| 3 | Add payment details | At least 1 active BusinessPaymentDetail for businessId | `/MyBusiness` (Payment tab) |
| 4 | Create your first customer | At least 1 Customer for businessId | `/Customer` |
| 5 | Create a quotation or invoice | At least 1 Quotation OR 1 Invoice for businessId | `/Quotation/Create` |
| 6 | Issue your first invoice | At least 1 Invoice with InvoiceStatusTypeId = 2 | `/Invoice` |

## Error Handling

- If the `DismissOnboarding` endpoint fails, the panel remains visible and a SweetAlert2 error is shown.
- If the onboarding service throws during `GetOnboardingStateAsync`, the controller catches and returns `OnboardingStateDto { IsVisible = false }` to avoid breaking the entire dashboard.

## Performance Considerations

- When `IsOnboardingDismissed = true`, the controller skips all 6 existence queries (short-circuit in service).
- All queries use `AnyAsync()` which stops at the first match — no full table scans.
- Queries are scoped to `businessId` which is an indexed foreign key on all relevant tables.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Panel Visibility Logic

*For any* business state (dismissed flag, set of step completion booleans), the onboarding panel is visible if and only if the dismissal flag is false AND at least one step is incomplete.

**Validates: Requirements 1.1, 3.3**

### Property 2: Step Completion Computation

*For any* business with arbitrary data across BusinessProfiles, BusinessLogos, BusinessPaymentDetails, Customers, Quotations, and Invoices, each onboarding step flag equals true if and only if the defined data condition for that step is satisfied.

**Validates: Requirements 1.3, 4.1**

### Property 3: Progress Count Consistency

*For any* set of 6 boolean step completion values, the CompletedCount equals the number of values that are true.

**Validates: Requirements 1.2**

### Property 4: Celebration State Activation

*For any* business state where all 6 step flags are true and the dismissal flag is false, the IsCelebration flag is true and IsVisible is true.

**Validates: Requirements 2.1**
