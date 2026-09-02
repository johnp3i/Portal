# Stripe Connect — Onboarding Guide

**Audience:** Business owners using the Portal (primary), platform administrators / developers (Appendix)
**Feature:** Accept card payments on shared invoices, paid directly into your own Stripe account
**Plan requirement:** Professional or higher (`stripe_connect` module)

---

## What is Stripe Connect on the Portal?

Stripe Connect lets your customers pay their invoices **by card**, with the money going
**directly into your own Stripe account** — the Portal never holds your funds and takes no
platform fee. Card payments sit alongside the existing **bank transfer** option; on a shared
invoice the customer can choose whichever they prefer.

When a card payment completes, the Portal automatically:

- records the payment against the invoice,
- recalculates the invoice's financial status,
- generates a receipt (if receipts are enabled),
- captures the exact Stripe fee so you can see gross, fee, and net amounts.

---

## Before you start

You will need:

- A **Stripe account** (create one free at stripe.com if you don't have one). You can use an
  existing business Stripe account.
- **Owner access** to your business in the Portal. Only the business owner can connect,
  disconnect, or manage Stripe keys — team members see a read-only status.
- Your Portal plan must include the **Card Payments** feature (Professional or higher). If it
  doesn't, you'll see an upgrade prompt instead of the connect option.

---

## Part 1 — Connect your Stripe account (recommended path)

This is the simplest path. It uses a secure "Connect with Stripe" flow — you authorise the
Portal on Stripe's own website and never paste secret keys.

1. Sign in to the Portal as the **business owner**.
2. Go to **My Business** (Business Settings) → the **Payments** section.
3. Click **Connect with Stripe**.
4. You'll be taken to **Stripe's website**. Sign in to your Stripe account (or create one) and
   authorise the connection.
5. Stripe returns you to the Portal. You'll see a green **Connected** badge with your account
   label.

That's it — card payments are now enabled on your shared invoices.

> **What's happening behind the scenes:** the Portal uses Stripe's OAuth "Standard Connect"
> flow. You approve the connection on Stripe, Stripe hands the Portal a reference to your
> account, and the Portal stores only that account reference — never your Stripe login or
> full secret keys.

### To disconnect

Return to **My Business → Payments** and click **Disconnect**. Card payment buttons stop
appearing on your shared invoices immediately. Existing recorded payments and receipts are
unaffected.

---

## Part 2 — Using your own Stripe API keys (advanced / optional)

Most businesses should use **Part 1**. Use this path only if your organisation manages its own
Stripe Connect application and wants to supply its own keys.

Keys are entered in **My Business → Automation tab** and are **encrypted at rest**. They
override the platform defaults. You'll provide up to three values:

| Field | Stripe format | Where to find it on Stripe |
|-------|---------------|----------------------------|
| **Connect Client ID** | `ca_...` | Stripe Dashboard → Settings → Connect → *Platform settings* |
| **Secret Key** | `sk_live_...` (or `sk_test_...`) | Stripe Dashboard → Developers → API keys |
| **Webhook Signing Secret** | `whsec_...` | Stripe Dashboard → Developers → Webhooks → your endpoint → *Signing secret* |

Steps:

1. Go to **My Business → Automation**.
2. In the **Stripe Keys** panel, enter the three values above.
3. Click **Save**. The Portal validates the keys against Stripe before saving — if a key is
   wrong or Stripe can't be reached, nothing is saved and you'll see which key failed.
4. Once saved, values are shown **masked** (e.g. `sk_live_****…a1b2`). Use **Reveal** to view a
   full value (owner only; rate-limited and audit-logged).
5. To remove them, use **Delete keys** — the Portal warns you first if this would leave you
   with no working configuration while a connection is active.

> **Key resolution order:** the Portal uses your **per-business keys** if present, otherwise it
> falls back to the **platform keys** configured by the administrator. So if you don't enter
> keys here, the standard "Connect with Stripe" flow in Part 1 still works.

---

## Part 3 — How your customers pay

1. You share an invoice as usual (shared link / invoice email).
2. On the shared invoice page, when you're connected to Stripe and the invoice has an
   outstanding balance, the customer sees a **Pay by Card** button (primary) alongside
   **Pay by Bank Transfer** (secondary).
3. **Pay by Card** takes the customer to **Stripe Checkout** to enter card details securely.
4. On success, Stripe notifies the Portal, which records the payment and updates the invoice.
5. The customer is returned to a success page.

Nothing needs to be done manually — the payment, status update, and receipt happen
automatically.

---

## Part 4 — Seeing your card payments and fees

Once connected, a **Card Payments** item appears under the **Revenue** section in the sidebar.
It shows fee transparency:

- **Total Received (Gross)** — what customers paid
- **Total Stripe Fees** — what Stripe deducted
- **Net Received** — what actually reached your bank
- **Transactions** — number of card payments

You can filter by date range (This Month / Last Month / Last 3 Months / Custom), page through
the list, and **export to CSV** (Date, Invoice, Customer, Gross, Fee, Net, Stripe Charge ID).

On an invoice's own detail page, card payments appear in the payment history with a card icon
and a clickable Stripe charge reference (`ch_...`) that opens the transaction in your Stripe
Dashboard.

---

## Troubleshooting

| Symptom | Likely cause | What to do |
|---------|-------------|------------|
| No "Connect with Stripe" button | Plan doesn't include Card Payments, or you're not the owner | Upgrade to Professional+, or ask the business owner to connect |
| "Card payments temporarily unavailable" on the invoice | Stripe couldn't create the checkout session (e.g. restricted account) | Check your Stripe account status in the Stripe Dashboard |
| Saved keys rejected | A key is wrong or Stripe was unreachable during validation | Re-check the exact values in Stripe; the Portal names which key failed |
| Customer paid but invoice still shows unpaid | Webhook not received yet or misconfigured | Payments usually reflect within seconds; if persistent, contact the administrator to verify the webhook endpoint |
| "Keys are corrupted. Please re-enter." | Encryption key rotation on the platform | Re-enter your keys in the Automation tab |

---

## Security notes

- The Portal never stores your Stripe login. With the Connect flow (Part 1), it stores only a
  reference to your connected account.
- Per-business API keys are **encrypted at rest** and only the **owner** can view (reveal) or
  change them. Reveal actions are rate-limited and written to the audit log.
- Card details are entered on **Stripe Checkout**, not on the Portal — the Portal never sees or
  stores card numbers.

---

## Appendix — Platform / Administrator setup (developers)

This section is for whoever operates the Portal platform, not individual business owners.

### 1. Register a Stripe Connect platform

In the Stripe Dashboard (the **platform** account, not a connected business):

1. **Settings → Connect** → enable Connect and configure the **Standard** integration.
2. Note the **Connect Client ID** (`ca_...`).
3. Set the **OAuth redirect URI** to the Portal callback:
   `https://<your-domain>/MyBusiness/StripeConnectCallback`
4. **Developers → API keys** → note the **Secret Key** (`sk_live_...` / `sk_test_...`).
5. **Developers → Webhooks** → add an endpoint pointing at the Portal webhook:
   `https://<your-domain>/stripe/connect-webhook`, subscribe to `checkout.session.completed`,
   and note the **Signing Secret** (`whsec_...`).

### 2. Configure platform secrets (User Secrets — never appsettings.json)

```json
{
  "Stripe": {
    "SecretKey": "sk_live_...",
    "ConnectClientId": "ca_...",
    "ConnectWebhookSecret": "whsec_...",
    "ConnectOAuthRedirectUri": "https://portal.3inventors.com/MyBusiness/StripeConnectCallback"
  }
}
```

These are the fallback values used when a business has not supplied its own keys via the
Automation tab (see Part 2). Resolution order at runtime is **per-business DB keys → platform
User Secrets**.

### 3. Key endpoints (reference)

| Route | Purpose |
|-------|---------|
| `GET /MyBusiness/StripeConnect` | Start the OAuth connect flow |
| `GET /MyBusiness/StripeConnectCallback` | OAuth callback (must match the redirect URI) |
| `POST /MyBusiness/AxPostDisconnectStripe` | Disconnect a business's account |
| `POST /InvoiceView/CreateCheckoutSession` | Create a Checkout Session from a shared invoice |
| `POST /stripe/connect-webhook` | Receive Stripe events (signature-verified) |

### 4. Related specifications

- `.kiro/specs/stripe-connect/design.md` — architecture, OAuth flow, checkout, webhook, Card Payments view
- `.kiro/specs/stripe-api-keys-config/design.md` — per-business encrypted key configuration
- `.kiro/specs/stripe-onboarding/design.md` + `requirements.md` — platform `StripeSettings` and startup validation
- `.kiro/specs/stripe-session-auto-expire/design.md` — checkout session lifecycle / expiry
- `.kiro/docs/scenarios/stripe-connect-testing.md` — manual test scenarios
- `.kiro/docs/mockups/stripe-shared-invoice-pay.html`, `stripe-card-payments-view.html` — UI mockups

---

*This guide reflects the implemented Stripe Connect feature. Content was compiled from the
Stripe Connect, Stripe API Keys, and Stripe Onboarding specifications.*
