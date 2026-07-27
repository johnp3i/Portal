# Admin: Business Insights Page

**Implemented:** 2026-07-11  
**Route:** `/Admin/BusinessInsights`  
**Access:** SuperAdmin only  
**Mockup:** `.kiro/docs/mockups/admin-business-insights.html`

---

## Purpose

Platform-wide activity overview for the SuperAdmin. Shows all registered businesses (excluding demo accounts) with aggregated usage metrics, subscription status, and engagement signals.

Helps the platform operator:
- Identify which businesses are getting value (retention signal)
- Spot inactive businesses (churn risk)
- Track trial-to-paid conversion readiness
- See email confirmation status

---

## Architecture

```
AdminBusinessInsightsController (SuperAdmin-only)
    → IBusinessInsightsService / BusinessInsightsService
        → BusinessInsightsRepository (Portal DB: aggregation query)
        → MembershipDbContext (owner info + email confirmed)
```

### Cross-Database Pattern

Business activity data lives in Portal DB. Owner identity data lives in Membership DB. The service:
1. Queries Portal DB for business metrics (counts, revenue, last activity)
2. Queries Membership DB for owner name, email, confirmation status
3. Merges the two in-memory
4. Applies filters and pagination

---

## Summary Cards

| Card | Source | Description |
|------|--------|-------------|
| Total Businesses | Count of non-demo businesses | All registered |
| Confirmed | Owner's `EmailConfirmed = true` | Email verified |
| Active (30 days) | Last activity within 30 days | At least 1 quotation/invoice/purchase |
| On Trial | `BusinessPlan.Status = "trial"` | Trial period active |

Summary cards reflect **unfiltered** totals (always show full platform state regardless of active filters).

---

## Table Columns

| Column | Source | Notes |
|--------|--------|-------|
| Business | `Business.Name` | Bold, primary colour |
| Owner | `UserBusiness` where `IsOwner = true` → `ApplicationUser` | Full name + email |
| Plan | `BusinessPlan` → `Plan.Name` | Badge |
| Status | `BusinessPlan.Status` | Active (green) / Trial (amber) / Expired (red) |
| Confirmed | `ApplicationUser.EmailConfirmed` | Confirmed (green) / Pending (amber) |
| Quotations | `COUNT(Quotation WHERE BusinessId = X)` | Muted if zero |
| Invoices | `COUNT(Invoice WHERE BusinessId = X)` | Muted if zero |
| Purchases | `COUNT(Purchase WHERE BusinessId = X)` | Muted if zero |
| Revenue | `SUM(Invoice.TotalAmount WHERE BusinessId = X)` | EUR formatted |
| Last Activity | `MAX(CreatedAtUtc)` across quotations/invoices/purchases | Color-coded freshness |

### Last Activity Colour Coding

- **Green** (< 7 days): recent engagement
- **Amber** (7–30 days): getting stale
- **Red** (30+ days or never): inactive / churn risk

---

## Filters

| Filter | Values | Behaviour |
|--------|--------|-----------|
| Search | Free text | Matches business name, owner name, or owner email |
| Plan | All / Foundation / Professional / Enterprise | Exact match on plan name |
| Status | All / Active / Trial / Expired | Matches `BusinessPlan.Status` |
| Activity | All / Active last 30 days / Inactive 30+ days / Never used | Based on last activity date |

All filtering is server-side. Pagination: 20 items per page.

---

## Demo Account Exclusion

Demo accounts are excluded at the **repository level** using:
```csharp
where !business.IsDemoAccount
```

This ensures demo businesses and their dummy `AspNetUser` accounts never appear in the insights view.

---

## Files

### New Files

| File | Layer | Purpose |
|------|-------|---------|
| `Portal.Infrastructure/Models/BusinessInsightDto.cs` | Model | Per-business row DTO |
| `Portal.Infrastructure/Models/BusinessInsightSummaryDto.cs` | Model | Summary cards DTO |
| `Portal.Infrastructure/Models/BusinessInsightFilter.cs` | Model | Filter parameters |
| `Portal.Infrastructure/Repositories/BusinessInsightsRepository.cs` | Repository | Aggregation query |
| `Portal.Infrastructure/Services/IBusinessInsightsService.cs` | Service | Interface |
| `Portal.Infrastructure/Services/BusinessInsightsService.cs` | Service | Cross-DB enrichment + filtering |
| `Portal.Web/Controllers/AdminBusinessInsightsController.cs` | Controller | HTTP handler |
| `Portal.Web/Views/AdminBusinessInsights/Index.cshtml` | View | Razor page |

### Modified Files

| File | Change |
|------|--------|
| `Portal.Web/Program.cs` | DI registration for repository + service |
| `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml` | Nav link in Administration section |

---

## Related Changes (Same Session)

### Admin/Users — Show All Platform Users

The `/Admin/Users` page was also updated to:
- Show **all users across all businesses** (not just the SuperAdmin's business)
- Add a **"Business" column** showing which business each user belongs to
- Add a **"User Type" filter** (All / Real Users / Demo Users)
- Display a **"Demo" badge** on users belonging to demo businesses
- Resolve business names + demo status from Portal DB (cross-database)

### Sidebar Consolidation

- Removed duplicate "Administration" section from `_Layout.cshtml`
- Consolidated all admin nav items into the `ModuleNavigation` component
- Added "Subscriptions" and "Business Insights" to the single Administration section
- Moved "Invite User" into the Account section (properly grouped with Billing + User Permissions)
