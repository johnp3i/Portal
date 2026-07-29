# Stripe Payment Link Lifecycle

## Overview

This document describes the complete lifecycle of a Stripe payment link (Checkout Session) from creation to completion, expiry, or auto-expire. It serves as a reference for developers working on any payment-related feature.

---

## 1. When the "Pay by Card" Button Appears

The button renders on the shared invoice page only when **both** conditions are met:

| Condition | Value |
|-----------|-------|
| Invoice `InvoiceFinancialStatusTypeId` | `1` (Unpaid), `2` (Partially Paid), or `4` (Overdue) |
| Business has active Stripe Connect | `StripeConnectedAccount.IsActive = true` |

Once the invoice reaches status `3` (Paid), the button disappears from the rendered HTML. This is the **first line of defence** against double payment.

---

## 2. Customer Clicks "Pay by Card"

When the customer submits the form (`POST /invoice-view/{token}/pay-by-card`):

1. **Token validation** — the share token is resolved to an invoice
2. **Balance check** — `RecalculateStatusAsync` is not called; instead, outstanding balance is computed in real-time:
   - `outstandingBalance = invoice.TotalAmount - SUM(non-voided payments) - applied credit`
   - If `outstandingBalance <= 0`, returns an error: *"This invoice has no outstanding balance."*
3. **Stripe Checkout Session creation** — calls `StripeConnectService.CreateCheckoutSessionAsync`:
   - Amount = outstanding balance at that moment
   - Destination charge → funds flow to connected account
   - ExpiresAt = UTC now + 30 minutes
   - Metadata: `invoiceId`, `businessId`, `platform`
4. **Database record** — `[stripe].[CheckoutSession]` row inserted with `Status = 'pending'`
5. **Redirect** — customer is redirected to the Stripe-hosted checkout page

This is the **second line of defence** — even if the button somehow renders, the balance check prevents session creation for paid invoices.

---

## 3. Successful Payment (Webhook Flow)

After the customer completes payment on Stripe's hosted page:

1. **Stripe fires** `checkout.session.completed` webhook to `POST /stripe/connect-webhook`
2. **Signature verification** — using `ConnectWebhookSecret`
3. **Idempotency check** — if `CheckoutSession.Status == 'completed'` for this `StripeSessionId`, return 200 immediately
4. **Fee retrieval** — calls Stripe API to get charge and balance transaction details (fee, net)
5. **Payment creation** — inserts a `Payment` record (`PaymentMethodTypeId = Card`)
6. **Status recalculation** — calls `RecalculateStatusAsync(invoiceId, businessId)` which updates the invoice's `InvoiceFinancialStatusTypeId`
7. **Auto-receipt** — if the business has `IsAutoReceiptEnabled`, generates a receipt
8. **Session update** — marks `CheckoutSession` as `completed` with fee, net, chargeId, paymentId

The customer is also redirected to the success URL (`?payment=success`), where a green banner confirms receipt.

---

## 4. Customer Cancels or Abandons

If the customer closes the Stripe Checkout page without paying:

- **No webhook fires immediately** — Stripe considers the session still active
- **After 30 minutes** (the configured `ExpiresAt`), the session expires on Stripe's side
- **Stripe fires** `checkout.session.expired` webhook
- **Portal updates** `CheckoutSession.Status` to `'expired'` in the database
- **No other action** — the invoice remains in its current status; the customer can click "Pay by Card" again to create a new session

---

## 5. Manual Payment Recorded Before Stripe Payment Completes

**The race condition scenario:**

1. Customer clicks "Pay by Card" → Stripe Checkout page opens (session is `pending`)
2. Customer delays payment (leaves the tab open)
3. Business records a manual payment (bank transfer, cash, cheque) that fully pays the invoice
4. `RecalculateStatusAsync` transitions the invoice to `Paid` (status 3)
5. The "Pay by Card" button disappears from the page (if refreshed)
6. **BUT** the customer still has the Stripe Checkout tab open

**Without auto-expire (current gap):**
- The customer can complete the Stripe payment on the already-open page
- Stripe processes it → webhook fires → Payment record created → **overpayment**

**With auto-expire (new feature):**
- When the invoice transitions to Paid, the system calls `Stripe.Checkout.SessionService.ExpireAsync()` for all pending sessions
- The customer's open checkout page shows an "expired session" error from Stripe
- No overpayment is possible

---

## 6. Multiple Sessions for the Same Invoice

A customer (or multiple people with the shared link) can create multiple Checkout Sessions for the same invoice:

- Each "Pay by Card" click creates a new session (previous one may still be pending)
- When a Stripe webhook payment completes for one session, the new auto-expire feature should expire all other pending sessions for that invoice
- The idempotency constraint (`UNIQUE` on `StripeSessionId`) prevents duplicate processing of the same session

---

## 7. Session Status Transitions

```
┌─────────┐     Customer completes payment     ┌───────────┐
│ pending │ ──────────────────────────────────▶ │ completed │
└─────────┘                                     └───────────┘
     │
     │  30min expiry (natural)
     │  OR auto-expire (invoice fully paid)
     ▼
┌─────────┐
│ expired │
└─────────┘
```

| Status | Meaning |
|--------|---------|
| `pending` | Session created, customer has not paid yet |
| `completed` | Customer paid successfully, payment record created |
| `expired` | Session expired (naturally after 30min, or force-expired by auto-expire) |

---

## 8. Database Schema

```
[stripe].[CheckoutSession]
├── Id (PK)
├── BusinessId (FK → Business)
├── InvoiceId (FK → Invoice)
├── StripeSessionId (UNIQUE)
├── Amount
├── Currency
├── Status ('pending' | 'completed' | 'expired')
├── StripePaymentIntentId
├── StripeChargeId
├── PaymentId (FK → Payment, set on completion)
├── CustomerName
├── StripeFeeAmount
├── NetAmount
├── CreatedAtUtc
└── CompletedAtUtc
```

---

## 9. Safety Layers Summary

| Layer | Protection | Handles |
|-------|-----------|---------|
| 1. Button rendering | Only shows for Unpaid/PartiallyPaid/Overdue | Page refresh after payment |
| 2. Balance check on click | Returns error if outstanding ≤ 0 | Direct link access after payment |
| 3. Auto-expire (new) | Expires pending sessions when invoice is fully paid | Already-open Stripe Checkout tabs |
| 4. Webhook idempotency | Rejects duplicate session completions | Stripe retry delivery |

---

## 10. Configuration

| Setting | Value | Location |
|---------|-------|----------|
| Session expiry | 30 minutes | `ExpiresAt` in `CreateCheckoutSessionAsync` |
| Webhook secret | `ConnectWebhookSecret` | User Secrets |
| Payment method type | "Card" | `PaymentMethodType` seed data |
| Charge type | Destination charge | No application fee |
