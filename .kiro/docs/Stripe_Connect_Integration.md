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

### Platform Fee Decision

**Decision: No application fee.** Stripe Connect card payments are a platform value add-on that drives subscription retention. The feature justifies the subscription cost — businesses stay because they can accept card payments from their invoices. Revenue comes from subscriptions, not from transaction fees.

This can be revisited later if transaction volume justifies it.

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
| OAuth CSRF | Use `state` parameter with random token, verify on callback |

---

## Configuration (User Secrets)

Stored in User Secrets — never committed to source control:

```json
{
    "Stripe": {
        "SecretKey": "sk_live_...",
        "PublishableKey": "pk_live_...",
        "ConnectClientId": "ca_...",
        "ConnectWebhookSecret": "whsec_...",
        "ConnectOAuthRedirectUri": "https://portal.3inventors.com/MyBusiness/StripeConnectCallback"
    }
}
```

**Note:** `SecretKey` and `PublishableKey` are already configured for subscription billing. `ConnectClientId` and `ConnectWebhookSecret` are new additions for Connect.

The `ConnectClientId` is found in Stripe Dashboard → Settings → Connect → Platform settings.

---

## NuGet Package

The `Stripe.net` NuGet package is already referenced in the project (used by subscription billing). No additional packages needed for Connect — the same SDK covers all Stripe APIs.

---

## Data Model

### Option A: Provider-Agnostic (Supports future JCC/PayPal)

#### `[portal].[BusinessPaymentGateway]`

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

#### `[portal].[InvoicePaymentLink]`

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

### Option B: Stripe-Specific (Chosen for Phase 1)

For Phase 1, we use Stripe-specific tables in the `[stripe]` schema. This is simpler and avoids premature abstraction. If JCC is added later, we either migrate to Option A or add a `[jcc]` schema.

#### `[stripe].[ConnectedAccount]`

```sql
CREATE TABLE [stripe].[ConnectedAccount]
(
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [BusinessId]                INT NOT NULL,
    [StripeAccountId]           NVARCHAR(255) NOT NULL,
    [IsActive]                  BIT NOT NULL DEFAULT 1,
    [ConnectedAtUtc]            DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [DisconnectedAtUtc]         DATETIME NULL,
    [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_ConnectedAccount] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ConnectedAccount_Business] FOREIGN KEY ([BusinessId])
        REFERENCES [portal].[Business]([Id]),
    CONSTRAINT [UQ_ConnectedAccount_Business] UNIQUE ([BusinessId])
);
```

#### `[stripe].[CheckoutSession]`

```sql
CREATE TABLE [stripe].[CheckoutSession]
(
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [BusinessId]                INT NOT NULL,
    [InvoiceId]                 INT NOT NULL,
    [StripeSessionId]           NVARCHAR(255) NOT NULL,
    [Amount]                    DECIMAL(18,2) NOT NULL,
    [Currency]                  NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    [Status]                    NVARCHAR(50) NOT NULL DEFAULT 'pending',
    [StripePaymentIntentId]     NVARCHAR(255) NULL,
    [PaymentId]                 INT NULL,
    [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [CompletedAtUtc]            DATETIME NULL,

    CONSTRAINT [PK_CheckoutSession] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_CheckoutSession_StripeSessionId] UNIQUE ([StripeSessionId])
);
```

**Note:** The `[stripe]` schema already exists (for `StripeCustomer` and `WebhookEvent` used by subscription billing). These new tables coexist in the same schema.

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

---

## Charge Type: Destination Charges

Portal uses **destination charges** (not direct charges). This means:

- The platform (Portal) creates the Checkout Session on its own Stripe account
- The `PaymentIntentData.TransferData.Destination` specifies the connected account
- Funds are transferred to the connected account after successful payment
- The platform can optionally deduct an application fee (not used in Phase 1)

```csharp
PaymentIntentData = new SessionPaymentIntentDataOptions
{
    TransferData = new SessionPaymentIntentDataTransferDataOptions
    {
        Destination = connectedAccountId  // "acct_xxx"
    }
    // No ApplicationFeeAmount — zero platform fee
}
```

**Why destination charges over direct charges:**
- Portal controls the checkout experience and branding
- Simpler error handling (one API key for all sessions)
- Application fee can be added later without changing the integration pattern
- Webhook events come to the platform account (not each connected account)

---

## Checkout Session — Full API Parameters

```csharp
var options = new SessionCreateOptions
{
    PaymentMethodTypes = new List<string> { "card" },
    LineItems = new List<SessionLineItemOptions>
    {
        new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                UnitAmount = (long)(outstandingBalance * 100), // convert to cents
                Currency = "eur",
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = $"Invoice {invoiceNumber}",
                    Description = $"Payment for invoice {invoiceNumber} — {customerName}"
                }
            },
            Quantity = 1
        }
    },
    Mode = "payment",
    SuccessUrl = $"{baseUrl}/Invoice/Shared/{shareToken}?payment=success",
    CancelUrl = $"{baseUrl}/Invoice/Shared/{shareToken}",
    PaymentIntentData = new SessionPaymentIntentDataOptions
    {
        TransferData = new SessionPaymentIntentDataTransferDataOptions
        {
            Destination = connectedAccountId
        }
    },
    Metadata = new Dictionary<string, string>
    {
        { "invoiceId", invoiceId.ToString() },
        { "businessId", businessId.ToString() },
        { "shareToken", shareToken },
        { "platform", "portal" }
    },
    ExpiresAt = DateTime.UtcNow.AddMinutes(30) // shorter expiry for invoice payments
};
```

---

## Webhook Events to Handle

| Event | Action |
|-------|--------|
| `checkout.session.completed` | Create Payment record, recalculate invoice status, trigger receipt |
| `checkout.session.expired` | Update CheckoutSession status to "expired" — no other action |
| `charge.refunded` | (Phase 2) Void the corresponding Payment record |
| `account.updated` | (Future) Update connected account health status |

### Webhook Signature Verification

```csharp
var payload = await new StreamReader(Request.Body).ReadToEndAsync();
var signature = Request.Headers["Stripe-Signature"];
var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
```

If verification fails → return 400. Never process unverified events.

---

## Idempotency Strategy

Stripe may deliver the same webhook event multiple times. The handler must be idempotent:

1. **CheckoutSession table** has a `UNIQUE` constraint on `StripeSessionId`
2. Before processing `checkout.session.completed`:
   - Check: does a `CheckoutSession` record exist with `Status = 'completed'` for this `StripeSessionId`?
   - If yes → return 200 OK immediately (already processed)
   - If no → process and update status to 'completed'
3. The Payment record creation is wrapped in a transaction with the CheckoutSession status update — atomic operation

---

## Tier Gating

| Plan | Card Payment Access |
|------|-------------------|
| Foundation | Bank transfer only (no Stripe Connect) |
| Professional | Bank transfer + Stripe Connect card payments |
| Enterprise | Bank transfer + Stripe Connect card payments |

Module key: `stripe_connect`

- The "Connect with Stripe" button in Business Settings is hidden for Foundation tier
- The "Pay by Card" button on shared invoices is hidden if the business doesn't have Stripe connected
- A soft-gate teaser is shown to Foundation users: "Accept card payments instantly — upgrade to Professional"

---

## Existing Infrastructure Reused

| Component | How It's Reused |
|-----------|----------------|
| `StripeCustomer` table | Different — that's for Portal subscription billing, not Connect |
| Payment recording service | Webhook creates payments using the same `PaymentService.RecordPaymentAsync` |
| Financial status engine | `RecalculateStatusAsync` is called after webhook payment creation |
| Receipt auto-generation | Triggered same as manual payments |
| Shared invoice page | "Pay by Card" button added alongside existing "Pay by Bank Transfer" |
| Payment method types | New seed: "Card" added to `PaymentMethodType` table |
| User Secrets | Stripe keys already stored there for subscriptions — Connect keys added alongside |

---

## Edge Cases & Race Conditions

| Scenario | Handling |
|----------|----------|
| Invoice paid manually while Checkout Session is open | Check outstanding balance before creating session. If 0 → reject with message. |
| Two customers open checkout for same invoice simultaneously | Only first webhook creates payment. Second hits idempotency check (session already completed) or balance check (outstanding = 0). |
| Business disconnects Stripe while checkout is in progress | Payment still completes (Stripe processes independently). Webhook still fires. Portal records it normally. |
| Invoice voided after checkout created but before payment | Webhook fires → Portal checks invoice status → if cancelled/voided, still record payment but flag for review. |
| Webhook arrives before success redirect | Normal — webhook is faster than redirect. Customer sees updated status on success page. |
| Customer refreshes success page | Idempotent — page just shows current invoice status. |
