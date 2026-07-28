# Stripe Connect — Design

## Architecture

```
Business Settings
    → "Connect with Stripe" button
    → Stripe OAuth flow (Standard Connect)
    → Callback → Store StripeConnectedAccountId
    → Status shown in Settings

Shared Invoice Page (Public)
    → "Pay by Card" button (if business has Stripe connected)
    → POST /InvoiceView/CreateCheckoutSession
        → Stripe API: Create Checkout Session (destination charge)
        → Redirect customer to Stripe Checkout
    → Customer pays
    → Stripe redirects to success URL

Webhook Endpoint (POST /stripe/connect-webhook)
    → Verify signature
    → checkout.session.completed
        → Extract metadata (invoiceId, businessId)
        → Create Payment record
        → Recalculate financial status
        → Auto-generate receipt if enabled
```

---

## Database Changes

### New Table: `[stripe].[ConnectedAccount]`

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

### New Table: `[stripe].[CheckoutSession]`

```sql
CREATE TABLE [stripe].[CheckoutSession]
(
    [Id]                        INT IDENTITY(1,1) NOT NULL,
    [BusinessId]                INT NOT NULL,
    [InvoiceId]                 INT NOT NULL,
    [StripeSessionId]           NVARCHAR(255) NOT NULL,
    [Amount]                    DECIMAL(18,2) NOT NULL,
    [StripeFeeAmount]           DECIMAL(18,2) NULL,
    [NetAmount]                 DECIMAL(18,2) NULL,
    [Currency]                  NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    [Status]                    NVARCHAR(50) NOT NULL DEFAULT 'pending',
    [StripePaymentIntentId]     NVARCHAR(255) NULL,
    [StripeChargeId]            NVARCHAR(255) NULL,
    [PaymentId]                 INT NULL,
    [CustomerName]              NVARCHAR(255) NULL,
    [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [CompletedAtUtc]            DATETIME NULL,

    CONSTRAINT [PK_CheckoutSession] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_CheckoutSession_StripeSessionId] UNIQUE ([StripeSessionId])
);
```

**Fee capture:** On `checkout.session.completed`, the webhook handler retrieves the charge's `BalanceTransaction` to get the exact Stripe fee. Stored as `StripeFeeAmount`. `NetAmount` is computed as `Amount - StripeFeeAmount`.

### Modified Table: `[revenue].[Payment]`

No schema changes needed. Stripe payments use existing columns:
- `Reference` = Stripe charge ID (e.g., `ch_3xyz...`)
- `Notes` = "Stripe card payment"
- `PaymentMethodTypeId` = new seed: "Card" (Id = TBD, add to PaymentMethodType)

### Seed Data

```sql
-- Add "Card" payment method if not exists
IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Name] = 'Card')
    INSERT INTO [revenue].[PaymentMethodType] ([Name], [IsActive]) VALUES ('Card', 1);
```

---

## Service Layer

### StripeConnectService

```csharp
public interface IStripeConnectService
{
    // Onboarding
    Task<string> GetOAuthConnectUrlAsync(int businessId, string returnUrl);
    Task<ServiceResult> CompleteOAuthAsync(int businessId, string authorizationCode);
    Task<ServiceResult> DisconnectAsync(int businessId);
    Task<bool> IsConnectedAsync(int businessId);
    Task<string?> GetConnectedAccountIdAsync(int businessId);

    // Checkout
    Task<ServiceResult<string>> CreateCheckoutSessionAsync(int invoiceId, int businessId, string successUrl, string cancelUrl);

    // Webhook
    Task<ServiceResult> HandleCheckoutCompletedAsync(string stripeSessionId, string paymentIntentId);
}
```

### Key Implementation Details

**OAuth Flow:**
1. Generate OAuth URL: `https://connect.stripe.com/oauth/authorize?client_id={PLATFORM_CLIENT_ID}&state={CSRF_TOKEN}&scope=read_write&response_type=code&redirect_uri={CALLBACK_URL}`
2. User authorizes on Stripe
3. Stripe redirects to callback with `code` parameter
4. Exchange code for connected account ID via `POST /oauth/token`
5. Store `StripeAccountId` in `[stripe].[ConnectedAccount]`

**Checkout Session Creation:**
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
                UnitAmount = (long)(outstandingBalance * 100), // cents
                Currency = "eur",
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = $"Invoice {invoiceNumber}",
                }
            },
            Quantity = 1
        }
    },
    Mode = "payment",
    SuccessUrl = successUrl,
    CancelUrl = cancelUrl,
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
        { "platform", "portal" }
    }
};
```

**Webhook Processing:**
1. Verify signature using webhook signing secret
2. Parse event → `checkout.session.completed`
3. Read metadata: invoiceId, businessId
4. Check idempotency: does a CheckoutSession record with this StripeSessionId already have status "completed"?
5. If not: create Payment record, recalculate financial status, update CheckoutSession status
6. Return 200 OK

---

## Controller Endpoints

### Business Settings (Authenticated)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/MyBusiness/StripeConnect` | Show connect status + button |
| GET | `/MyBusiness/StripeConnectCallback` | OAuth callback handler |
| POST | `/MyBusiness/AxPostDisconnectStripe` | Disconnect account |

### Shared Invoice (Public)

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/InvoiceView/CreateCheckoutSession` | Create Stripe Checkout Session |

### Webhook (Public, no auth)

| Method | Route | Purpose |
|--------|-------|---------|
| POST | `/stripe/connect-webhook` | Receive Stripe events |

---

## UI Changes

### Business Settings Page

Add a "Payments" section:
- If not connected: "Connect with Stripe" button + explanation text
- If connected: Green "Connected" badge + account label + "Disconnect" button

### Shared Invoice Page

When business has Stripe connected and invoice has outstanding balance:
- Show "Pay by Card" primary button
- Existing "Pay by Bank Transfer" becomes secondary
- Both options remain available (customer chooses)

### Invoice Detail (Business-Facing)

- Payment history shows Stripe payments with a card icon badge
- Reference shows clickable Stripe charge ID (links to Stripe Dashboard)

---

## Configuration

Stored in User Secrets (never in appsettings.json):

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

---

## Error Handling

| Scenario | Handling |
|----------|----------|
| Checkout session creation fails | Show error to payer: "Card payments temporarily unavailable" |
| Webhook signature invalid | Return 400, log warning |
| Duplicate webhook (same session ID) | Skip silently, return 200 |
| Connected account restricted | Checkout creation fails → fallback message |
| Customer abandons checkout | Session expires after 24h, no action needed |
| Invoice already paid (race condition) | Check outstanding balance before creating session |

---

## Files to Create

| File | Layer | Purpose |
|------|-------|---------|
| `Portal.Database/Migrations/XXX_CreateStripeConnectedAccountTable.sql` | DB | Connected account storage |
| `Portal.Database/Migrations/XXX_CreateStripeCheckoutSessionTable.sql` | DB | Checkout session tracking |
| `Portal.Database/Migrations/XXX_SeedCardPaymentMethodType.sql` | DB | Add "Card" payment method |
| `Portal.Infrastructure/Entities/StripeConnectedAccount.cs` | Entity | EF Core entity |
| `Portal.Infrastructure/Entities/StripeCheckoutSession.cs` | Entity | EF Core entity |
| `Portal.Infrastructure/Repositories/StripeConnectRepository.cs` | Repository | Data access |
| `Portal.Infrastructure/Services/IStripeConnectService.cs` | Service | Interface |
| `Portal.Web/Services/StripeConnectService.cs` | Service | Implementation |
| `Portal.Web/Controllers/StripeConnectWebhookController.cs` | Controller | Webhook endpoint |
| `Portal.Web/Views/MyBusiness/_StripeConnectSection.cshtml` | View | Settings UI |

### Files to Modify

| File | Change |
|------|--------|
| `Portal.Web/Controllers/MyBusinessController.cs` | Add connect/disconnect/callback actions |
| `Portal.Web/Controllers/InvoiceViewController.cs` | Add CreateCheckoutSession action |
| `Portal.Web/Views/InvoiceView/SharedInvoice.cshtml` | Add "Pay by Card" button |
| `Portal.Web/Views/MyBusiness/Index.cshtml` | Add Stripe Connect section |
| `Portal.Infrastructure/Data/PortalDbContext.cs` | Add new DbSets + configuration |
| `Portal.Web/Program.cs` | DI registration |

---

## Card Payments View (Fee Transparency)

### Route: `/Revenue/CardPayments`

### Summary Cards

| Card | Source | Description |
|------|--------|-------------|
| Total Received (Gross) | `SUM(CheckoutSession.Amount) WHERE Status='completed'` | What customers paid |
| Total Stripe Fees | `SUM(CheckoutSession.StripeFeeAmount) WHERE Status='completed'` | What Stripe took |
| Net Received | `SUM(CheckoutSession.NetAmount) WHERE Status='completed'` | What reached the bank |
| Transactions | `COUNT(*) WHERE Status='completed'` | Number of card payments |

### Table Columns

| Column | Source |
|--------|--------|
| Date | `CheckoutSession.CompletedAtUtc` |
| Invoice | `Invoice.InvoiceNumber` (linked) |
| Customer | `CheckoutSession.CustomerName` or from Invoice→Customer |
| Gross | `CheckoutSession.Amount` |
| Stripe Fee | `CheckoutSession.StripeFeeAmount` |
| Net | `CheckoutSession.NetAmount` |
| Status | `CheckoutSession.Status` (completed/refunded) |

### Filters

- Date range (This Month / Last Month / Last 3 Months / Custom)
- Pagination (20 per page)

### CSV Export

Export all rows for the selected period with columns: Date, Invoice Number, Customer, Gross, Fee, Net, Stripe Charge ID.

### Access

- Only visible when business has an active Stripe Connected Account.
- Navigation: Revenue section → "Card Payments" item in sidebar.
- Permission gate: `stripe_connect` module key (Professional+).

---

## Webhook Fee Retrieval Logic

When `checkout.session.completed` is received:

```csharp
// 1. Get the session with expanded payment intent
var session = await _stripeClient.Checkout.Sessions.GetAsync(stripeSessionId, new SessionGetOptions
{
    Expand = new List<string> { "payment_intent" }
});

// 2. Get the charge from the payment intent
var paymentIntent = session.PaymentIntent;
var chargeId = paymentIntent.LatestChargeId;

// 3. Retrieve the charge with expanded balance transaction
var charge = await _stripeClient.Charges.GetAsync(chargeId, new ChargeGetOptions
{
    Expand = new List<string> { "balance_transaction" }
});

// 4. Extract the fee
var balanceTransaction = charge.BalanceTransaction;
var stripeFeeAmount = balanceTransaction.Fee / 100m; // Stripe fees are in cents
var netAmount = balanceTransaction.Net / 100m;

// 5. Store on CheckoutSession
checkoutSession.StripeFeeAmount = stripeFeeAmount;
checkoutSession.NetAmount = netAmount;
checkoutSession.StripeChargeId = chargeId;
```

**Note:** Balance transaction amounts are in the smallest currency unit (cents for EUR). Divide by 100 for display.

---

## Phase 2: Payment Schedule Integration

When an invoice has an active payment schedule:

### Shared Invoice Page Logic

```
IF invoice has payment schedule with pending/due instalments:
    → Show: "Pay next instalment: €X.XX (due DD/MM/YYYY)"
    → Checkout Session amount = next due instalment amount
    → Metadata includes: instalmentId
ELSE:
    → Show: "Pay €X.XX" (full outstanding balance)
    → Checkout Session amount = outstanding balance
```

### Webhook Handling (Phase 2)

```
checkout.session.completed:
    → Create Payment record (amount = instalment amount)
    → Call PaymentScheduleService.MatchPaymentToScheduleAsync(paymentId, amount, invoiceId, businessId, userId)
    → Instalment status updates automatically
    → Financial status recalculated
```

The existing matching logic handles this without modification — it already matches payments to the next eligible instalment by priority (Due → Overdue → Pending).
