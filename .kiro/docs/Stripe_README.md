# Stripe Integration — Configuration Guide

## Overview

The Portal platform integrates with Stripe for subscription billing, checkout, and webhook processing. All Stripe credentials are stored in **User Secrets** (never in appsettings.json) following the project's security conventions.

## Required Configuration Keys

| Key | Description | Example |
|-----|-------------|---------|
| `Stripe_BILI:SecretKey` | Stripe API secret key (server-side) | `sk_test_51...` |
| `Stripe_BILI:PublishableKey` | Stripe publishable key (client-side) | `pk_test_51...` |
| `Stripe_BILI:WebhookSigningSecret` | Webhook endpoint signing secret | `whsec_...` |

## Setup Instructions

### 1. Initialize User Secrets (if not already done)

From the `Portal.Web` project directory:

```bash
dotnet user-secrets init
```

### 2. Set Stripe Keys

```bash
dotnet user-secrets set "Stripe_BILI:SecretKey" "sk_test_your_key_here"
dotnet user-secrets set "Stripe_BILI:PublishableKey" "pk_test_your_key_here"
dotnet user-secrets set "Stripe_BILI:WebhookSigningSecret" "whsec_your_secret_here"
```

### User Secrets File Location

The secrets are stored locally at:

```
%APPDATA%\Microsoft\UserSecrets\52cc8d49-d6da-4b79-8809-f3dc3f6aef40\secrets.json
```

Full path example:

```
C:\Users\<YourUsername>\AppData\Roaming\Microsoft\UserSecrets\52cc8d49-d6da-4b79-8809-f3dc3f6aef40\secrets.json
```

The file content looks like:

```json
{
  "Stripe_BILI:SecretKey": "sk_test_...",
  "Stripe_BILI:PublishableKey": "pk_test_...",
  "Stripe_BILI:WebhookSigningSecret": "whsec_..."
}
```

This file is **never committed to source control** — it exists only on the developer's machine.

### 3. Verify Configuration

On startup, the application validates all three keys are present and non-empty. If any key is missing, a descriptive `InvalidOperationException` is thrown with the specific missing key names.

## Skipping Validation (Development Only)

For local development without a Stripe account, set the environment variable:

```
SKIP_STRIPE_VALIDATION=true
```

This bypasses the startup validation but Stripe-dependent features will not function.

## Where to Get Your Keys

1. Log in to [Stripe Dashboard](https://dashboard.stripe.com)
2. **Secret Key / Publishable Key**: Developers → API Keys
3. **Webhook Signing Secret**: Developers → Webhooks → Select your endpoint → Signing secret

For local development, use **test mode** keys (prefixed with `sk_test_` and `pk_test_`).

## Webhook Endpoint

The webhook controller is at:

```
POST /api/webhooks/stripe
```

When configuring the webhook in Stripe Dashboard, set the endpoint URL to:

```
https://your-domain.com/api/webhooks/stripe
```

### Events to Subscribe To

- `checkout.session.completed`
- `invoice.paid`
- `invoice.payment_failed`
- `customer.subscription.updated`
- `customer.subscription.deleted`

## Local Webhook Testing

Use the [Stripe CLI](https://stripe.com/docs/stripe-cli) to forward events to your local server:

```bash
stripe listen --forward-to https://localhost:5001/api/webhooks/stripe
```

The CLI will output a webhook signing secret (starts with `whsec_`). Use that value for your local `Stripe_BILI:WebhookSigningSecret`.

## Production Deployment

In production, keys are loaded from **environment variables** (not User Secrets). The variable names use double underscore `__` as the section separator:

```
Stripe_BILI__SecretKey=sk_live_...
Stripe_BILI__PublishableKey=pk_live_...
Stripe_BILI__WebhookSigningSecret=whsec_...
```

A batch file is provided to set these on the live server:

```
Portal.Database/Scripts/SetStripeEnvironmentVariables_BILI.cmd
```

Run it as Administrator. The `_BILI` suffix distinguishes this platform's keys from other platforms (e.g., JDS) on the same server.

## Architecture

```
Portal.Web/Configuration/StripeSettings.cs     — Settings POCO (3 properties)
Portal.Web/Extensions/ServicesExtensions.cs     — ConfigureStripe() registration + validation
Portal.Web/Services/Stripe/                     — Service implementations
Portal.Web/Controllers/CheckoutController.cs    — Checkout flow
Portal.Web/Controllers/Api/StripeWebhookController.cs — Webhook receiver
```

## Database Schemas

The integration uses two dedicated schemas:

- `[billing]` — Subscription, Invoice, Payment tables
- `[stripe]` — Customer mapping, WebhookEvent (idempotency) tables

Migrations: `Portal.Database/Migrations/076_` through `079_`

## Plan Configuration

Each plan in `[dbo].[Plan]` needs a `StripePriceId` column populated with the corresponding Stripe Price ID (e.g., `price_1ABC...`). This links the Portal plan to the Stripe subscription price.

```sql
UPDATE [dbo].[Plan] SET StripePriceId = 'price_your_stripe_price_id' WHERE Id = 1;
```
