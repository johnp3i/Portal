# Stripe Connect — Requirements

## Overview

Enable businesses on the Portal platform to accept card payments from their customers on shared invoice links. Payments are processed via Stripe Connect (destination charges) with automatic reconciliation — when a customer pays, the invoice status updates without manual intervention.

No platform fee is taken. Stripe's standard processing fees (2.9% + €0.25) are charged to the connected business's Stripe account. This feature is a platform value add-on that drives subscription retention.

---

## Functional Requirements

### FR-1: Platform Registration

- Portal must be registered as a Stripe Connect platform.
- Platform API keys (already existing from subscription billing) are reused.
- Connect settings configured in Stripe Dashboard (not in-app).

### FR-2: Business Onboarding (Connect with Stripe)

- Business owner can connect their Stripe account from Business Settings.
- The onboarding uses Stripe's **OAuth Standard Connect** flow (hosted by Stripe).
- Portal stores the resulting `StripeConnectedAccountId` against the business.
- The business can **disconnect** at any time from Business Settings.
- Connection status is visible: Connected (with account label) or Not Connected.
- Only the business owner (IsOwner = true) can connect/disconnect.

### FR-3: Stripe Account Types

- Use **Standard Connect accounts** — the business has full control of their own Stripe Dashboard.
- Portal does not manage the business's Stripe payouts, disputes, or refunds — those are handled by the business in their own Stripe Dashboard.

### FR-4: Payment Button on Shared Invoice

- When a business has Stripe Connect enabled:
  - The shared invoice page shows a "Pay by Card" button alongside the existing "Pay by Bank Transfer" button.
- When Stripe Connect is NOT enabled:
  - Only "Pay by Bank Transfer" is shown (existing behaviour).
- The "Pay by Card" button is only shown when the invoice has an outstanding balance > 0.

### FR-5: Checkout Flow

- Clicking "Pay by Card" creates a Stripe Checkout Session:
  - Amount: invoice outstanding balance (full payment only in Phase 1).
  - Currency: EUR (from business profile).
  - Destination: business's connected account ID.
  - Application fee: €0 (no platform fee).
  - Metadata: invoiceId, businessId, shareToken.
  - Success URL: shared invoice page with success message.
  - Cancel URL: shared invoice page (no changes).
- Customer is redirected to Stripe's hosted Checkout page.
- After payment, customer is redirected back to the success URL.

### FR-6: Webhook Auto-Reconciliation

- Portal exposes a webhook endpoint for Stripe Connect events.
- On `checkout.session.completed`:
  - Validate webhook signature (Stripe signing secret).
  - Extract invoiceId and amount from session metadata.
  - Retrieve the PaymentIntent and its associated charge to capture the Stripe fee amount.
  - Create a Payment record in the Portal database (same as manual payment recording).
  - Store the Stripe fee amount on the CheckoutSession record.
  - Recalculate invoice financial status (Unpaid → Partially Paid → Paid).
  - Store Stripe charge ID as the payment reference.
- On `checkout.session.expired`:
  - No action needed (customer abandoned checkout).
- Idempotency: if a payment already exists for this Stripe session ID, skip (prevent double-processing).

### FR-7: Payment Record Integration

- Payments created from Stripe webhooks are treated identically to manual payments:
  - Visible in payment history on Invoice Detail.
  - Participate in financial status calculations.
  - Can be voided by the business owner (void in Portal only — Stripe refund is separate).
  - Trigger receipt auto-generation if enabled.
- Payment source is identifiable: `Notes = "Stripe card payment"`, `Reference = Stripe charge ID`.

### FR-8: Card Payments View (Fee Transparency)

- Business owner can view all card payment transactions and associated Stripe fees.
- Accessible from: Revenue section → "Card Payments" page.
- Summary cards at the top:
  - Total card payments received (gross amount).
  - Total Stripe fees deducted.
  - Net received after fees.
  - Number of card transactions.
- Detailed table showing each transaction:
  - Date, Invoice Number, Customer Name, Gross Amount, Stripe Fee, Net Amount, Status.
- Filterable by date range (month/quarter/custom).
- Export to CSV for accounting reconciliation.
- Only visible when the business has Stripe connected.

### FR-8: Disconnect Flow

- Business owner clicks "Disconnect Stripe" in Business Settings.
- Portal removes the stored connected account ID.
- All future shared invoices stop showing "Pay by Card".
- Existing payments are unaffected (historical records preserved).
- No Stripe API call needed for disconnect (just local state change).

### FR-9: Stripe Account Health

- If a connected account has issues (restricted, pending verification), Stripe will reject checkout sessions.
- Portal should handle this gracefully: if checkout session creation fails, show a user-friendly error to the payer ("Card payments are temporarily unavailable for this business. Please use bank transfer.").

---

## Non-Functional Requirements

### NFR-1: Security

- Webhook signature verification is mandatory (reject unsigned/invalid requests).
- Connected account IDs stored encrypted at rest or in secure configuration.
- OAuth state parameter used to prevent CSRF during connect flow.
- No sensitive Stripe data (card numbers, CVC) ever touches Portal servers.

### NFR-2: Performance

- Checkout session creation must complete in under 2 seconds.
- Webhook processing must complete in under 5 seconds (Stripe retries on timeout).

### NFR-3: Idempotency

- Webhook events may be delivered multiple times. The handler must be idempotent.
- Use `StripeSessionId` as a unique key to prevent duplicate payment creation.

### NFR-4: Availability

- If Stripe is down, "Pay by Card" button can show a loading error. "Pay by Bank Transfer" remains available as fallback.
- Webhook failures are retried by Stripe automatically (up to 72 hours).

---

## Tier Gating

- **Foundation:** Bank transfer only (existing).
- **Professional:** Bank transfer + Stripe Connect card payments.
- **Enterprise:** Bank transfer + Stripe Connect card payments.

The module key for permission gating: `stripe_connect`.

---

## Out of Scope (Phase 1)

- Partial card payments (customer pays a portion by card) — full outstanding balance only.
- Pay-per-instalment by card (pay next due instalment only) — Phase 2.
- Stripe refunds from within Portal (business handles refunds in their Stripe Dashboard).
- Multi-currency support (EUR only for now).
- Platform application fees.
- Subscription/recurring card payments via Connect.
- Express or Custom Connect account types (Standard only).

---

## Phase 2: Instalment Integration

When Phase 1 is stable and adopted:

### FR-P2-1: Pay Next Instalment by Card

- If an invoice has an active payment schedule, the shared invoice page shows:
  - The next due instalment amount (not the full outstanding balance).
  - "Pay €X.XX instalment" button instead of "Pay full balance".
- Clicking the button creates a Checkout Session for the instalment amount only.
- On webhook completion:
  - Payment is recorded for the instalment amount.
  - The existing payment-to-instalment matching logic (PaymentScheduleService.MatchPaymentToScheduleAsync) handles the auto-match.
  - Instalment status updates: Pending/Due → Paid.
  - If multiple instalments are overdue, the payment matches to the oldest due instalment first (existing FIFO logic).
- The customer can return to pay subsequent instalments as they become due.

### FR-P2-2: Partial Card Payments

- Customer can choose to pay a custom amount less than the full outstanding balance.
- Amount field shown on shared invoice page (pre-filled with outstanding balance, editable).
- Minimum payment: €1.00.
- Partial payment creates a standard Payment record → invoice remains Partially Paid.

### FR-P2-3: Payment Link in Reminder Emails (Instalment-Aware)

- When automated reminders are sent for invoices with payment schedules:
  - Reminder text references the overdue instalment amount (not the full invoice).
  - "Pay by Card" link in the email opens the shared invoice page with the instalment amount pre-selected.

