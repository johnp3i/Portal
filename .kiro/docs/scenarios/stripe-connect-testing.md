# Stripe Connect — Testing Scenarios

## Prerequisites

1. Run migrations 153–155 against the Portal database (creates `[stripe]` schema, `ConnectedAccount`, `CheckoutSession` tables, seeds "Card" payment method)
2. Configure Stripe test mode API keys in User Secrets:
   - `Stripe:SecretKey` = `sk_test_...`
   - `Stripe:ConnectClientId` = `ca_...`
   - `Stripe:ConnectWebhookSecret` = `whsec_...`
   - `Stripe:ConnectOAuthRedirectUri` = `https://localhost:xxxx/MyBusiness/StripeConnectCallback`
3. Business has Professional or Enterprise plan (has `stripe_connect` module permission)
4. At least one invoice with outstanding balance > 0
5. Stripe CLI installed for webhook testing (`stripe listen --forward-to localhost:xxxx/stripe/connect-webhook`)

---

## Scenario 1: Business Onboarding (Connect with Stripe)

1. Log in as business owner (IsOwner = true)
2. Navigate to **Business Settings**
3. Locate the "Payments" / "Stripe Connect" section
4. Verify status shows "Not Connected" with a "Connect with Stripe" button
5. Click "Connect with Stripe"
6. **Expected:** Redirect to Stripe OAuth page (`connect.stripe.com/oauth/authorize`)
7. Complete the Stripe OAuth flow (use test account credentials)
8. **Expected:** Redirect back to `/MyBusiness/StripeConnectCallback?code=...&state=...`
9. **Expected:** SweetAlert2 success message or status update showing "Connected"
10. Verify `[stripe].[ConnectedAccount]` table has a new record:
    - `BusinessId` matches current business
    - `StripeAccountId` starts with `acct_`
    - `IsActive = 1`
    - `ConnectedAtUtc` is recent timestamp

---

## Scenario 2: Connection Status Display

1. After connecting (Scenario 1 complete)
2. Navigate to Business Settings
3. **Expected:** "Payments" section shows:
   - Green "Connected" badge
   - Connected account identifier
   - "Disconnect" button visible
4. **Expected:** "Connect with Stripe" button is no longer shown

---

## Scenario 3: Disconnect Flow

1. On Business Settings page (business is connected)
2. Click "Disconnect" button
3. **Expected:** SweetAlert2 confirmation dialog: "Are you sure you want to disconnect Stripe?"
4. Confirm the action
5. **Expected:** BlockUI during request → SweetAlert2 success → page reloads
6. **Expected:** Status reverts to "Not Connected" with "Connect with Stripe" button
7. Verify database: `[stripe].[ConnectedAccount].IsActive = 0`, `DisconnectedAtUtc` is set
8. Navigate to a shared invoice page for this business
9. **Expected:** "Pay by Card" button is NOT visible (only "Pay by Bank Transfer" shown)

---

## Scenario 4: Customer Pays by Card — Happy Path

1. Ensure business is connected to Stripe (Scenario 1)
2. Create or use an existing invoice with outstanding balance > 0
3. Share the invoice (generate share link)
4. Open the shared invoice page in an incognito browser
5. **Expected:** "Pay by Card" button is visible alongside "Pay by Bank Transfer"
6. Click "Pay by Card"
7. **Expected:** BlockUI → Redirect to Stripe Checkout page (`checkout.stripe.com`)
8. On Stripe Checkout, use test card: `4242 4242 4242 4242`, any future expiry, any CVC
9. Complete payment
10. **Expected:** Redirect back to shared invoice page with success banner (green message)
11. Verify `[stripe].[CheckoutSession]` record created:
    - `Status = 'pending'` (until webhook fires) or `'completed'` (after webhook)
    - `Amount` matches outstanding balance
    - `StripeSessionId` starts with `cs_`

---

## Scenario 5: Webhook Reconciliation (checkout.session.completed)

1. After payment completes in Scenario 4
2. Stripe sends `checkout.session.completed` webhook (or trigger via Stripe CLI: `stripe trigger checkout.session.completed`)
3. **Expected:** Webhook endpoint returns 200 OK
4. Verify `[revenue].[Payment]` record created:
   - `Amount` matches the checkout session amount
   - `Reference` contains Stripe charge ID (`ch_...`)
   - `Notes` = "Stripe card payment"
   - `PaymentMethodTypeId` = Card method type ID
   - `IsVoided = 0`
5. Verify invoice financial status updated (Unpaid → Paid if full balance was paid)
6. Verify `[stripe].[CheckoutSession]` record updated:
   - `Status = 'completed'`
   - `StripeFeeAmount` populated (e.g., 2.9% + 0.25)
   - `NetAmount = Amount - StripeFeeAmount`
   - `StripeChargeId` populated
   - `StripePaymentIntentId` populated
   - `PaymentId` links to the new Payment record
   - `CompletedAtUtc` is set
7. If business has auto-receipt enabled:
   - Verify receipt was auto-generated for this payment

---

## Scenario 6: Idempotency — Duplicate Webhook

1. After Scenario 5 completes (payment already recorded)
2. Re-send the same `checkout.session.completed` event (Stripe CLI: `stripe events resend evt_xxx`)
3. **Expected:** Webhook returns 200 OK (no error)
4. **Expected:** No duplicate Payment record created
5. Verify `[revenue].[Payment]` table: still only ONE record for this Stripe session ID

---

## Scenario 7: Card Payments View — Summary and Table

1. Log in as business owner (business connected, at least one completed card payment)
2. Navigate to **Revenue → Card Payments**
3. **Expected:** Page loads with four summary cards:
   - Total Received (Gross) — sum of completed checkout amounts
   - Total Stripe Fees — sum of fee amounts
   - Net Received — gross minus fees
   - Transactions — count of completed sessions
4. **Expected:** Table shows completed card payment transactions with columns:
   - Date, Invoice Number, Customer, Gross Amount, Stripe Fee, Net Amount, Status
5. Click on an invoice number link
6. **Expected:** Navigates to the invoice detail page

---

## Scenario 8: Card Payments View — Date Range Filter

1. On Card Payments page
2. Select "This Month" filter
3. **Expected:** Table refreshes to show only transactions from current month
4. Select "Last Month"
5. **Expected:** Table shows previous month's transactions only
6. Select "Last 3 Months"
7. **Expected:** Table shows 3-month range
8. Click "Clear" or select "All"
9. **Expected:** All transactions shown again

---

## Scenario 9: Card Payments View — CSV Export

1. On Card Payments page with data visible
2. Click "Export CSV" button
3. **Expected:** CSV file downloads with columns: Date, Invoice Number, Customer, Gross, Fee, Net, Stripe Charge ID
4. Open CSV in spreadsheet application
5. **Expected:** Data matches what was shown in the table, amounts are correctly formatted

---

## Scenario 10: Plan Gating — Foundation User

1. Log in as a Foundation tier user (no `stripe_connect` module)
2. Navigate directly to `/Revenue/CardPayments`
3. **Expected:** Soft-gate / upgrade-required page shown (feature not available on your plan)
4. Check sidebar
5. **Expected:** "Card Payments" link is NOT visible in the Revenue section

---

## Scenario 11: Plan Gating — Professional User

1. Log in as a Professional tier user (has `stripe_connect` module)
2. Navigate to **Revenue → Card Payments**
3. **Expected:** Full page loads with summary cards and table
4. Check sidebar
5. **Expected:** "Card Payments" link is visible in the Revenue section

---

## Scenario 12: Error State — Checkout Creation Failure

1. Connect a Stripe account that has restrictions (or simulate failure)
2. Open shared invoice and click "Pay by Card"
3. **Expected:** Error banner shown: "Card payments are temporarily unavailable for this business. Please use bank transfer."
4. **Expected:** "Pay by Bank Transfer" button remains functional

---

## Scenario 13: Error State — Disconnected Business

1. Disconnect the business from Stripe (Scenario 3)
2. Open a shared invoice for this business
3. **Expected:** "Pay by Card" button is NOT visible
4. **Expected:** Only "Pay by Bank Transfer" is shown (existing behaviour)

---

## Scenario 14: Error State — Fully Paid Invoice

1. Ensure an invoice has financial status = "Paid" (outstanding balance = 0)
2. Open the shared invoice link
3. **Expected:** "Pay by Card" button is NOT shown (no outstanding balance)
4. **Expected:** Invoice shows "Paid" status

---

## Scenario 15: Webhook Signature Verification

1. Send a POST request to `/stripe/connect-webhook` without a valid `Stripe-Signature` header
2. **Expected:** Response is 400 Bad Request
3. **Expected:** No Payment records created
4. Send with a tampered payload (valid header for different body)
5. **Expected:** Response is 400 Bad Request

---

## Scenario 16: Stripe Payment Badge in Payment History

1. After a successful card payment (Scenario 5)
2. Log in as business owner
3. Navigate to the Invoice Detail page for the paid invoice
4. Check the Payment History section
5. **Expected:** Stripe payment shows a card icon/badge
6. **Expected:** Reference shows the Stripe charge ID (e.g., `ch_3xyz...`)

---

## Scenario 17: Owner-Only Access for Connect/Disconnect

1. Log in as a team member (IsOwner = false) for the same business
2. Navigate to Business Settings
3. **Expected:** Stripe Connect section is either hidden or shows "Connected" status without connect/disconnect buttons
4. Attempt to POST to disconnect endpoint directly
5. **Expected:** Action is denied (only owner can connect/disconnect)

---

## Scenario 18: Checkout Session Expiry

1. Click "Pay by Card" on a shared invoice
2. On the Stripe Checkout page, do NOT complete payment
3. Wait for session to expire (or use Stripe Dashboard to expire it)
4. **Expected:** No Payment record created
5. **Expected:** `[stripe].[CheckoutSession].Status` remains 'pending' or updates to 'expired'
6. **Expected:** Customer can click "Pay by Card" again to create a new session

---

## Verification Checklist

| # | Requirement | Verification Method | Status |
|---|-------------|-------------------|--------|
| FR-2 | Business onboarding via OAuth | Scenario 1 | [ ] |
| FR-2 | Connection status display | Scenario 2 | [ ] |
| FR-4 | Pay by Card button visibility (connected) | Scenario 4, Step 5 | [ ] |
| FR-4 | Pay by Card hidden (disconnected) | Scenario 13 | [ ] |
| FR-4 | Pay by Card hidden (fully paid) | Scenario 14 | [ ] |
| FR-5 | Checkout Session creation | Scenario 4, Steps 6-7 | [ ] |
| FR-5 | Stripe Checkout redirect + payment | Scenario 4, Steps 8-10 | [ ] |
| FR-6 | Webhook reconciliation | Scenario 5 | [ ] |
| FR-6 | Stripe fee capture | Scenario 5, Step 6 | [ ] |
| FR-6 | Idempotency | Scenario 6 | [ ] |
| FR-7 | Payment record integration | Scenario 5, Step 4 | [ ] |
| FR-7 | Receipt auto-generation | Scenario 5, Step 7 | [ ] |
| FR-8 | Card Payments view (summary + table) | Scenario 7 | [ ] |
| FR-8 | Date range filtering | Scenario 8 | [ ] |
| FR-8 | CSV export | Scenario 9 | [ ] |
| FR-8 | Disconnect flow | Scenario 3 | [ ] |
| FR-9 | Error handling (checkout failure) | Scenario 12 | [ ] |
| NFR-1 | Webhook signature verification | Scenario 15 | [ ] |
| NFR-3 | Idempotency (duplicate events) | Scenario 6 | [ ] |
| Tier | Foundation → soft-gate | Scenario 10 | [ ] |
| Tier | Professional → full access | Scenario 11 | [ ] |

---

## Database Queries for Manual Inspection

### Check Connected Accounts

```sql
SELECT
    ConnectedAccount.Id,
    ConnectedAccount.BusinessId,
    ConnectedAccount.StripeAccountId,
    ConnectedAccount.IsActive,
    ConnectedAccount.ConnectedAtUtc,
    ConnectedAccount.DisconnectedAtUtc
FROM [stripe].[ConnectedAccount]
ORDER BY ConnectedAccount.CreatedAtUtc DESC;
```

### Check Checkout Sessions

```sql
SELECT
    CheckoutSession.Id,
    CheckoutSession.BusinessId,
    CheckoutSession.InvoiceId,
    CheckoutSession.StripeSessionId,
    CheckoutSession.Amount,
    CheckoutSession.StripeFeeAmount,
    CheckoutSession.NetAmount,
    CheckoutSession.Status,
    CheckoutSession.StripePaymentIntentId,
    CheckoutSession.StripeChargeId,
    CheckoutSession.PaymentId,
    CheckoutSession.CustomerName,
    CheckoutSession.CreatedAtUtc,
    CheckoutSession.CompletedAtUtc
FROM [stripe].[CheckoutSession]
ORDER BY CheckoutSession.CreatedAtUtc DESC;
```

### Check Stripe Payments in Revenue

```sql
SELECT
    Payment.Id,
    Payment.BusinessId,
    Payment.InvoiceId,
    Payment.Amount,
    Payment.Reference,
    Payment.Notes,
    Payment.PaymentMethodTypeId,
    Payment.PaymentDateUtc,
    Payment.IsVoided,
    Payment.CreatedAtUtc
FROM [revenue].[Payment]
WHERE Payment.Notes = 'Stripe card payment'
ORDER BY Payment.CreatedAtUtc DESC;
```

### Verify Card Payment Method Type Exists

```sql
SELECT
    PaymentMethodType.Id,
    PaymentMethodType.Name,
    PaymentMethodType.IsActive
FROM [revenue].[PaymentMethodType]
WHERE PaymentMethodType.Name = 'Card';
```

### Card Payments Summary (Matches View)

```sql
SELECT
    COUNT(*) AS TransactionCount,
    SUM(CheckoutSession.Amount) AS TotalGross,
    SUM(CheckoutSession.StripeFeeAmount) AS TotalFees,
    SUM(CheckoutSession.NetAmount) AS TotalNet
FROM [stripe].[CheckoutSession]
WHERE CheckoutSession.BusinessId = @BusinessId
  AND CheckoutSession.Status = 'completed';
```

### Check Invoice Financial Status After Payment

```sql
SELECT
    Invoice.Id,
    Invoice.InvoiceNumber,
    Invoice.TotalAmount,
    Invoice.InvoiceFinancialStatusTypeId,
    InvoiceFinancialStatusType.Name AS FinancialStatus
FROM [invoice].[Invoice]
INNER JOIN [invoice].[InvoiceFinancialStatusType]
    ON Invoice.InvoiceFinancialStatusTypeId = InvoiceFinancialStatusType.Id
WHERE Invoice.Id = @InvoiceId;
```

---

## Stripe CLI Commands (Local Testing)

```bash
# Listen for Connect webhooks and forward to local
stripe listen --forward-to localhost:5001/stripe/connect-webhook

# Trigger a test checkout.session.completed event
stripe trigger checkout.session.completed

# Resend a specific event (for idempotency testing)
stripe events resend evt_xxxxxxxxxxxxx
```

---

## Test Cards (Stripe Test Mode)

| Card Number | Scenario |
|-------------|----------|
| 4242 4242 4242 4242 | Succeeds |
| 4000 0000 0000 0002 | Declined (generic) |
| 4000 0000 0000 9995 | Insufficient funds |
| 4000 0025 0000 3155 | Requires 3D Secure |
| 4000 0000 0000 3220 | 3D Secure (always authenticate) |

Use any future expiry date and any 3-digit CVC.
