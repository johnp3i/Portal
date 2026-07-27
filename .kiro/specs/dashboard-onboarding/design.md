# Dashboard Onboarding & Quick Actions — Design

## Dashboard Layout (top to bottom)

```
┌─────────────────────────────────────────────────┐
│ DASHBOARD                                        │
│ Welcome back, {FirstName}                        │
├─────────────────────────────────────────────────┤
│ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐       │
│ │ KPI │ │ KPI │ │ KPI │ │ KPI │ │ KPI │       │
│ └─────┘ └─────┘ └─────┘ └─────┘ └─────┘       │
├─────────────────────────────────────────────────┤
│ ⚡ Quick Actions                                 │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐         │
│ │ 📄       │ │ 🧾       │ │ 👤       │         │
│ │ New      │ │ Create   │ │ New      │         │
│ │ Quotation│ │ Invoice  │ │ Customer │         │
│ └──────────┘ └──────────┘ └──────────┘         │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐         │
│ │ 💰       │ │ 🛒       │ │ 📊       │         │
│ │ Record   │ │ Record   │ │ Customer │         │
│ │ Payment  │ │ Purchase │ │ Statement│         │
│ └──────────┘ └──────────┘ └──────────┘         │
├─────────────────────────────────────────────────┤
│ 🚀 Getting Started (collapsible)                │
│ ✅ Complete your business profile                │
│ ✅ Create your first customer                    │
│ ○  Create your first quotation                  │
│ ○  Issue your first invoice                     │
│ ○  Record a payment                             │
│                              [Dismiss]          │
├─────────────────────────────────────────────────┤
│ [Existing dashboard content: charts, etc.]      │
└─────────────────────────────────────────────────┘
```

## Quick Actions Card Design

```html
<section class="glass card-pad" style="margin-bottom:22px;">
    <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;">
        <svg ...lightning icon.../>
        <h3 style="font-size:16px;font-weight:700;">Quick Actions</h3>
    </div>
    <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));gap:12px;">
        <!-- Each action card -->
        <a href="/Quotation/Create" class="quick-action-card">
            <div class="quick-action-card__icon">📄</div>
            <div class="quick-action-card__label">New Quotation</div>
        </a>
        ...
    </div>
</section>
```

## Quick Action Card CSS

```css
.quick-action-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
    padding: 18px 12px;
    border-radius: 14px;
    background: rgba(13,94,166,.03);
    border: 1.5px solid rgba(13,94,166,.08);
    text-decoration: none;
    transition: all .15s;
    cursor: pointer;
}
.quick-action-card:hover {
    background: rgba(13,94,166,.07);
    border-color: rgba(13,94,166,.18);
    box-shadow: 0 4px 12px rgba(13,94,166,.06);
    transform: translateY(-1px);
}
.quick-action-card__icon {
    font-size: 24px;
}
.quick-action-card__label {
    font-size: 12px;
    font-weight: 700;
    color: #0B1B28;
    text-align: center;
}
```

## Getting Started Card

- Uses `<details>` for collapsibility
- Checks localStorage for dismissal
- Each item's completion is checked server-side via ViewBag booleans:
  - `HasBusinessProfile` — BusinessProfile exists with Name filled
  - `HasCustomers` — at least 1 customer
  - `HasQuotations` — at least 1 quotation
  - `HasInvoices` — at least 1 invoice
  - `HasPayments` — at least 1 payment

## Sidebar Help Link

Position: after the last nav section, before SubscriptionStatusIndicator.

```html
<a class="nav-item" href="/Help">
    <span class="nav-icon"><svg ...question mark circle.../></span>
    <span class="nav-text">Help</span>
</a>
```

## Data Requirements

The Dashboard controller needs to pass:
- `ViewBag.HasBusinessProfile` (bool)
- `ViewBag.HasCustomers` (bool)
- `ViewBag.HasQuotations` (bool)
- `ViewBag.HasInvoices` (bool)
- `ViewBag.HasPayments` (bool)
- `ViewBag.UserFirstName` (string)
