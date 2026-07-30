# Design: Future-Dated Payment Handling

## Overview

Future-dated payments (where `PaymentDateUtc > GETUTCDATE()`) are recorded in the database but excluded from all "paid" calculations until their date arrives. The UI marks them with an "Upcoming" badge to communicate their deferred status.

## Affected Components

### 1. PaymentRepository — Query Filters

**File:** `Portal.Infrastructure/Repositories/PaymentRepository.cs`

| Method | Change |
|--------|--------|
| `GetTotalPaidAsync` | Add `AND [PaymentDateUtc] <= GETUTCDATE()` to WHERE clause |
| `GetValidPaymentsByInvoiceIdAsync` | Add `AND [PaymentDateUtc] <= GETUTCDATE()` to WHERE clause |
| `GetPaidInPeriodAsync` | Add `AND [PaymentDateUtc] <= GETUTCDATE()` — ensures future payments in the range are excluded |

**`GetMonthlyTotalsAsync`** — No change. Future payments appearing in their correct future month is expected (the chart shows forecasted revenue placement).

### 2. FinancialStatusEngine — No Code Change

The engine calls `GetValidPaymentsByInvoiceIdAsync` which will now exclude future-dated payments. The `ComputeOutstandingBalance` and `DetermineFinancialStatus` pure functions remain unchanged — they operate on the filtered set.

### 3. DashboardService — KPI Query Filters

**File:** `Portal.Infrastructure/Services/DashboardService.cs`

| Query | Change |
|-------|--------|
| Outstanding Receivables sub-query (`ValidPayments`) | Add `AND [PaymentDateUtc] <= GETUTCDATE()` |
| Overdue Amount sub-query (`ValidPayments`) | Add `AND [PaymentDateUtc] <= GETUTCDATE()` |
| Paid This Month query | Add `AND [PaymentDateUtc] <= GETUTCDATE()` |
| Partially Paid sub-query (`ValidPayments`) | Add `AND [PaymentDateUtc] <= GETUTCDATE()` |

### 4. PaymentHistoryDto — Add IsUpcoming Flag

**File:** `Portal.Infrastructure/Models/PaymentHistoryDto.cs`

```csharp
public bool IsUpcoming { get; set; }
```

**Mapping in `PaymentService.GetPaymentHistoryAsync`:**

```csharp
IsUpcoming = p.PaymentDateUtc > DateTime.UtcNow
```

### 5. UI Views — Upcoming Badge

**Invoice Detail payment history table** (`Views/Revenue/InvoiceDetail.cshtml`):
- When `IsUpcoming == true`, render an amber badge: `<span style="...background:#FEF3CD;color:#8A6D3B;...">Upcoming</span>`
- Exclude from displayed "Total Paid" (already handled by the query change)

**Revenue Dashboard recent payments** (`Views/Revenue/Dashboard.cshtml`):
- Same badge treatment next to the payment date

## Data Flow (After Fix)

```
User records payment (future date)
    → Payment saved in DB ✓
    → RecalculateStatusAsync called
        → GetValidPaymentsByInvoiceIdAsync (excludes future-dated) 
        → ComputeOutstandingBalance (correct — doesn't include this payment)
        → DetermineFinancialStatus (correct — status stays as-is)
    → Invoice status unchanged ✓

Date arrives → user views invoice / records another payment
    → RecalculateStatusAsync called
        → GetValidPaymentsByInvoiceIdAsync (now includes matured payment)
        → Status transitions correctly ✓
```

## Edge Cases

| Scenario | Behaviour |
|----------|-----------|
| Payment date = today (midnight UTC) | Included — `<= GETUTCDATE()` passes |
| Payment voided before date arrives | Stays voided — no effect on calculations |
| Payment date in past (normal case) | Included — no change from current behaviour |
| Multiple future payments on same invoice | All excluded until their respective dates arrive |
| Global payment with future date | Already blocked by `RecordGlobalPaymentAsync` validation — no change needed |

## No Schema Changes

The fix is purely at the query/service layer. `PaymentDateUtc` already captures the full semantic meaning. No new columns, flags, or tables required.
