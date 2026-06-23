# Stripe Connect Integration — Technical & Business Guide

## Overview

This document covers the Stripe Connect integration strategy for the Portal platform. The goal is to allow business owners to accept online payments from their customers via payment links attached to invoices — with funds going directly to the business owner's bank account, not through 3 Inventors.

---

## Architecture: Connected Accounts (Standard)

The Portal uses **Stripe Connect with Standard accounts**. This means:

- Each business owner has their own Stripe account (or creates one during onboarding)
- 3 Inventors is the **platform** — it facilitates the connection but never holds customer funds
- Payments go directly to the business owner's Stripe balance → their bank account
- 3 Inventors can optionally collect a small application fee per transaction

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Customer   │────▶│    Stripe    │────▶│   Business   │
│ (pays invoice)│     │  (processes) │     │   Owner's    │
│              │     │              │     │ Bank Account │
└──────────────┘     └──────┬───────┘     └──────────────┘
                            │
                            │ webhook
                            ▼
                     ┌──────────────┐
                     │    Portal    │
                     │ (auto-records│
                     │   payment)   │
                     └──────────────┘
```

---

## Why Standard Connect (Not Express or Custom)

| Type | Control | Onboarding | Best for |
|------|---------|------------|----------|
| **Standard** ✅ | Business owns their Stripe account fully | Redirects to Stripe's hosted onboarding | Platforms where merchants are real businesses |
| Express | Platform controls dashboard | Stripe-hosted, fewer fields | Marketplaces with many small sellers |
| Custom | Platform builds everything | Fully embedded, complex | Large platforms with custom UX needs |

**Standard** is correct because:
- Business owners retain full control of their Stripe account
- They can log into Stripe directly to see their balance, payouts, disputes
- Minimal compliance burden on 3 Inventors
- Simplest integration with least ongoing maintenance

---

## Cost Structure

### Costs to the Business Owner (Portal User)

| Cost | Amount (EU) | Notes |
|------|-------------|-------|
| Stripe account | Free | No monthly fee |
| European card transaction | 1.5% + €0.25 | Deducted from payment |
| UK card transaction | 2.5% + €0.25 | Deducted from payment |
| Non-EU/International card | 3.25% + €0.25 | Deducted from payment |
| Chargeback (dispute) | €15 per dispute | If customer disputes payment |
| Payout to bank | Free | Standard schedule (2 business days) |

### Example: €172.55 Invoice Paid by European Card

```
Customer pays:           €172.55
Stripe fee (1.5%+€0.25): -€2.84
Business receives:       €169.71
```

### Costs to 3 Inventors (Platform)

| Cost | Amount | Notes |
|------|--------|-------|
| Platform account | Free | No monthly fee for Stripe Connect |
| Per-transaction (on application fee only) | 0.5% of application fee | Only if you charge a fee |

### Optional Platform Application Fee

3 Inventors can charge a per-transaction fee on top of Stripe's processing fee:

| Strategy | Amount | Revenue Example (€10,000 monthly volume) |
|----------|--------|------------------------------------------|
| Launch (no fee) | €0 | €0/month — drives adoption |
| Growth (flat) | €0.15 per transaction | ~€15/month per 100 transactions |
| Mature (percentage) | 0.5% | €50/month per €10,000 volume |

**Recommendation:** Launch without application fee. Introduce 0.5% after proving value.

---

## Integration Flow

### 1. Business Owner Connects Stripe (One-time Setup)

```
Portal Settings → Payment Gateway → "Connect Stripe"
  → Redirects to Stripe OAuth (Standard account link)
  → Business creates Stripe account or connects existing one
  → Stripe redirects back to Portal with authorization code
  → Portal exchanges code for stripe_account_id
  → Stores: BusinessPaymentGateway record (provider: "stripe", accountId: "acct_xxx")
```

### 2. Invoice Payment Link Generation

```
Invoice Detail → "Generate Payment Link" button
  → Portal calls Stripe API: create Checkout Session
    - on_behalf_of: connected account ID
    - line_items: invoice total (or itemized)
    - success_url: /Invoice/PaymentSuccess?invoiceId=X
    - cancel_url: /Invoice/Detail/X
    - metadata: { invoiceId, businessId }
  → Returns Checkout Session URL
  → Portal stores URL on invoice record
  → "Pay Now" button appears on shared invoice / customer portal
```

### 3. Customer Pays

```
Customer clicks "Pay Now" → Stripe Checkout (hosted by Stripe)
  → Enters card details on Stripe's secure page
  → Payment processed → funds to business owner's Stripe balance
  → Stripe fires webhook: checkout.session.completed
```

### 4. Webhook Processing (Auto-Reconciliation)

```
POST /api/stripe/webhook (Portal endpoint)
  → Verify webhook signature (stripe-signature header)
  → Extract: invoiceId from metadata, amount paid, payment method
  → Look up invoice in Portal DB
  → Create Payment record (amount, date, method: "Stripe", reference: session ID)
  → Update invoice financial status (Paid / Partially Paid)
  → Log activity
  → Return 200 OK
```

---

## Security Considerations

| Concern | Mitigation |
|---------|------------|
| Webhook authenticity | Verify `stripe-signature` header using webhook signing secret |
| Credential storage | Store `stripe_account_id` only (not API keys) — Standard Connect uses OAuth |
| PCI compliance | Stripe Checkout is fully hosted — Portal never sees card numbers |
| Idempotency | Store Stripe session ID, reject duplicate webhook deliveries |
| Access control | Only business owner (or SuperAdmin) can connect/disconnect gateway |
| Demo users | Payment gateway configuration blocked for demo sessions |

---

## Data Model

### `[portal].[BusinessPaymentGateway]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT FK → Business | One gateway per business (initially) |
| Provider | NVARCHAR(50) | 'stripe', 'jcc' (future) |
| ProviderAccountId | NVARCHAR(256) | e.g., 'acct_1234567890' |
| IsActive | BIT DEFAULT 1 | Can be disabled without deleting |
| ConnectedAtUtc | DATETIME2 | When the account was linked |
| ConnectedByUserId | NVARCHAR(450) FK → AspNetUsers | Who connected it |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

### `[portal].[InvoicePaymentLink]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| InvoiceId | INT FK → Invoice | |
| Provider | NVARCHAR(50) | 'stripe' |
| ProviderSessionId | NVARCHAR(256) | Stripe Checkout Session ID |
| PaymentUrl | NVARCHAR(500) | The checkout URL for the customer |
| Status | NVARCHAR(20) | 'pending', 'paid', 'expired', 'cancelled' |
| AmountRequested | DECIMAL(18,2) | Amount in the payment link |
| AmountPaid | DECIMAL(18,2) NULL | Actual amount received |
| PaidAtUtc | DATETIME2 NULL | When payment was confirmed |
| ExpiresAtUtc | DATETIME2 NULL | Stripe sessions expire after 24h by default |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

---

## Provider-Agnostic Design

The architecture supports multiple payment providers via a simple adapter pattern:

```csharp
public interface IPaymentGatewayProvider
{
    string ProviderName { get; }
    Task<string> CreatePaymentLinkAsync(Invoice invoice, string connectedAccountId);
    Task<PaymentConfirmation> ProcessWebhookAsync(HttpRequest request);
    Task<string> GetOAuthConnectUrlAsync(int businessId, string returnUrl);
    Task<string> CompleteOAuthAsync(string authorizationCode);
}
```

Implementations:
- `StripePaymentProvider` (Phase 1)
- `JccPaymentProvider` (Phase 2 — local Cyprus provider)
- Future: PayPal, Square, etc.

---

## JCC (Local Provider) — Future Addition

JCC doesn't support a Connect-equivalent, so the integration is simpler but less automated:

1. Business owner enters JCC merchant credentials in Portal settings
2. Portal generates a payment page URL using JCC's hosted payment page API
3. Customer pays on JCC's page
4. JCC sends a callback/redirect to Portal
5. Portal verifies and records the payment

**Key difference:** JCC requires the business to already have a merchant agreement with JCC (obtained through their bank). The Portal just uses their existing credentials.

---

## User Experience

### Business Owner Setup (One-time)

1. Navigate to Settings → Payment Gateway
2. Click "Connect with Stripe"
3. Redirected to Stripe — create account or log in
4. Grant permission to Portal
5. Redirected back — confirmation shown
6. Payment links now available on all invoices

### Generating a Payment Link

1. Open Invoice Detail
2. Click "Generate Payment Link" (or auto-generate on invoice issue)
3. Payment link appears — can be:
   - Included in the invoice email automatically
   - Shared via the existing "Share" feature
   - Displayed on the shared invoice page
   - Shown in the Client Portal (if implemented)

### Customer Paying

1. Receives invoice email with "Pay Now" button
2. Clicks → Stripe Checkout opens (secure, mobile-friendly, branded)
3. Enters card → pays
4. Redirected to success page
5. Invoice automatically marked as paid in Portal (within seconds via webhook)

---

## Relationship to Other Features

| Feature | Relationship |
|---------|-------------|
| Automated Payment Reminders | Reminder emails include the payment link — one click to pay |
| Client Portal | Customer portal shows invoices with "Pay" buttons |
| Cash Flow Forecasting | Payment links reduce uncertainty — higher confidence in projected inflows |
| Revenue Dashboard | Real-time payment recording updates KPIs immediately |
| Invoice Acceptance | "Accept & Pay" flow for quotation-to-invoice conversion |

---

## Implementation Phases

### Phase A: Core Integration
- Stripe OAuth connect/disconnect
- Generate payment links for issued invoices
- Webhook processing + auto-reconciliation
- Payment status display on invoice detail

### Phase B: Automation & UX
- Auto-include payment link in invoice emails
- Payment link on shared invoice page
- Partial payment support
- Payment gateway status on admin dashboard

### Phase C: Advanced
- JCC provider adapter
- Application fee configuration (per-business or global)
- Payment analytics (conversion rate, average time to pay)
- Refund processing via Portal
