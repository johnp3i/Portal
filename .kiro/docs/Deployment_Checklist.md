# Portal Deployment Checklist

## Overview

This document covers the complete deployment process for publishing the Portal (Bili) platform to a production server. Follow each section in order.

---

## 1. Databases

Three SQL Server databases are required:

| Database | Purpose |
|----------|---------|
| `Portal` | Main business data (quotations, invoices, purchases, billing, subscriptions) |
| `Portal.Membership` | ASP.NET Core Identity (users, roles, claims, pending registrations, permissions) |
| `Portal.Logging` | Serilog structured logs (application-level logging) |

### 1.1 Create Databases

```sql
CREATE DATABASE [Portal];
CREATE DATABASE [Portal.Membership];
CREATE DATABASE [Portal.Logging];
```

### 1.2 Run Portal Migrations

Execute all migration scripts in numerical order from `Portal.Database/Migrations/` (001 through 087) against the `Portal` database:

```
001_CreateSchemas.sql
002_CreateBusinessTable.sql
003_CreateBusinessProfileTable.sql
...
085_CreateInvoiceSequenceTable.sql
086_AddInvoiceNumberToBillingInvoice.sql
087_AddStripeProductIdToPlan.sql
```

### 1.3 Run Membership Migrations

Execute all migration scripts from `Portal.Database/Migrations/Membership/` against the `Portal.Membership` database:

```
001_CreateMembershipSchema.sql (if exists)
...
004_CreateUserBusinessTables.sql
005_AddPromoCodeIdToPendingRegistration.sql
...
007_CreatePendingRegistrationTable.sql
```

### 1.4 Run Logging Migrations

Execute all migration scripts from `Portal.Database/Migrations/Logging/` against the `Portal.Logging` database.

### 1.5 Run Seed Data

Against the `Portal` database:

1. `076_SeedBusinessPlan.sql` — Creates the Business plan with 9 module features
2. `084_SeedPlatformConfig.sql` — Platform configuration defaults
3. `Portal.Database/Seeds/Seed_SamplePromoCodes.sql` — Sample promo codes (optional for production)

### 1.6 Update Plan with Stripe IDs

After creating the Stripe product/price in live mode:

```sql
UPDATE [dbo].[Plan]
SET [StripePriceId] = 'price_LIVE_xxxxx',
    [StripeProductId] = 'prod_LIVE_xxxxx'
WHERE [Slug] = 'business';
```

### 1.7 Fix UserBusinessPermission Constraint

Ensure the CHECK constraint includes all module names:

```sql
ALTER TABLE [membership].[UserBusinessPermission]
    DROP CONSTRAINT [CK_UserBusinessPermission_Module];

ALTER TABLE [membership].[UserBusinessPermission]
    ADD CONSTRAINT [CK_UserBusinessPermission_Module] CHECK (
        [Module] IN ('customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'audit', 'credit', 'products')
    );
```

---

## 2. Application Deployment

### 2.1 Publish the Application

```bash
dotnet publish Portal.Web/Portal.Web.csproj -c Release -o ./publish
```

### 2.2 Deploy to Server

Copy the `./publish` folder contents to the server's web application directory (e.g., `C:\inetpub\Portal\` or equivalent).

### 2.3 Server Prerequisites

| Requirement | Notes |
|-------------|-------|
| .NET 8 Hosting Bundle | Required for IIS hosting |
| SQL Server (2019+) | For all three databases |
| Chromium / Google Chrome | Required for PuppeteerSharp PDF generation |
| IIS or reverse proxy (Nginx) | To serve the application |

### 2.4 IIS Configuration (if using IIS)

- Create Application Pool: "No Managed Code", 64-bit enabled
- Create Site pointing to the publish folder
- Ensure the app pool identity has read/write access to the folder
- Install URL Rewrite module for HTTPS redirect

---

## 3. Configuration (Production)

### 3.1 Connection Strings

Set via environment variables or `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "PortalConnection": "Server=YOUR_SERVER;Database=Portal;User Id=portal_user;Password=STRONG_PASSWORD;TrustServerCertificate=True;",
    "MembershipConnection": "Server=YOUR_SERVER;Database=Portal.Membership;User Id=portal_user;Password=STRONG_PASSWORD;TrustServerCertificate=True;",
    "LoggingConnection": "Server=YOUR_SERVER;Database=Portal.Logging;User Id=portal_user;Password=STRONG_PASSWORD;TrustServerCertificate=True;"
  }
}
```

### 3.2 Stripe Configuration

Set via environment variables (NEVER in config files on server):

| Variable | Value |
|----------|-------|
| `Stripe_BILI__SecretKey` | `sk_live_...` |
| `Stripe_BILI__PublishableKey` | `pk_live_...` |
| `Stripe_BILI__WebhookSigningSecret` | `whsec_...` (from Stripe Dashboard) |
| `Stripe_BILI__DefaultTaxRateId` | `txr_...` (live 19% VAT rate) |
| `Stripe_BILI__BaseUrl` | `https://yourdomain.com` |

Note: Use double underscore `__` for nested config in environment variables.

### 3.3 Invoice Settings

In `appsettings.Production.json`:

```json
{
  "Invoice": {
    "CompanyName": "3 Inventors Ltd",
    "CompanyAddress": "Nicosia, Cyprus",
    "CompanyCountryCode": "CY",
    "CompanyVatNumber": "CY10439718W",
    "CompanyEmail": "invoices@3inventors.com",
    "PlatformCode": "BILI"
  }
}
```

### 3.4 Email / SMTP Settings

Configure your production SMTP credentials for:
- Registration confirmation emails
- Password reset emails
- Invoice notification emails

### 3.5 Environment Variable

Ensure this is set on the server:

```
ASPNETCORE_ENVIRONMENT=Production
```

---

## 4. Stripe (Live Mode)

### 4.1 Activate Stripe Account

Complete Stripe's onboarding (bank account, identity verification) to exit test mode.

### 4.2 Create Live Product & Price

| Setting | Value |
|---------|-------|
| Product Name | Bili Business |
| Price | €348.00 / year |
| Currency | EUR |
| Billing Period | Yearly |
| Tax Behavior | Exclusive |

### 4.3 Create Live Tax Rate

| Setting | Value |
|---------|-------|
| Display Name | Cyprus VAT |
| Percentage | 19% |
| Type | Exclusive |
| Country | CY |

### 4.4 Create Webhook Endpoint

In Stripe Dashboard → Developers → Webhooks:

- **URL:** `https://yourdomain.com/api/webhooks/stripe`
- **Events to listen for:**
  - `checkout.session.completed`
  - `invoice.paid`
  - `invoice.payment_failed`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`

Copy the signing secret to your server configuration.

### 4.5 Update Database

```sql
UPDATE [dbo].[Plan]
SET [StripePriceId] = 'price_LIVE_xxxxx',
    [StripeProductId] = 'prod_LIVE_xxxxx'
WHERE [Slug] = 'business';
```

---

## 5. DNS & SSL

### 5.1 DNS Configuration

Create an A record pointing your domain to the server's public IP:

```
bili.3inventors.com → YOUR_SERVER_IP
```

### 5.2 SSL Certificate

Options:
- **Let's Encrypt** (free, auto-renewal via Certbot or win-acme)
- **Commercial certificate** (DigiCert, Sectigo, etc.)

HTTPS is **required** — Stripe won't send webhooks to HTTP endpoints.

### 5.3 Configure Reverse Proxy

If using IIS:
- Bind HTTPS (443) with your SSL certificate
- Add HTTP → HTTPS redirect rule in URL Rewrite

If using Nginx:
```nginx
server {
    listen 443 ssl;
    server_name bili.3inventors.com;
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### 5.4 Force HTTPS

Ensure all HTTP requests redirect to HTTPS (301).

---

## 6. Post-Deployment Verification

| # | Test | Expected Result |
|---|------|-----------------|
| 6.1 | Navigate to `https://yourdomain.com` | Landing page loads |
| 6.2 | Register a new account (no promo) | "Check your email" page shown |
| 6.3 | Click confirmation link | Auto-signed-in, redirected to Stripe Checkout |
| 6.4 | Complete Stripe payment (use real card or test mode) | "Payment Successful" → Dashboard |
| 6.5 | Check Stripe Dashboard → Webhooks | Events show 200 responses |
| 6.6 | Navigate to Dashboard | KPIs load (all zeros for new business) |
| 6.7 | Navigate to My Business | Profile form loads |
| 6.8 | Navigate to Billing | Billing overview loads with subscription info |
| 6.9 | Register with promo code | Trial provisioned without Stripe |
| 6.10 | Download an invoice PDF | PDF generates and downloads |
| 6.11 | Check Portal.Logging DB | Log entries present |
| 6.12 | Create SuperAdmin account | Manual DB insert or first-user seed |

---

## 7. Security Hardening

| # | Task | Notes |
|---|------|-------|
| 7.1 | Remove `appsettings.Development.json` from deployment | Contains test keys |
| 7.2 | Store secrets in environment variables only | Never in files on server |
| 7.3 | Create dedicated SQL login (not `sa`) | Minimum required permissions per DB |
| 7.4 | Firewall: allow only 443 (HTTPS) and 80 (HTTP redirect) | Block all other inbound |
| 7.5 | Disable directory browsing | IIS or Nginx config |
| 7.6 | Set secure cookie policy | `SameSite=Strict`, `Secure=true` |
| 7.7 | Enable HSTS header | `Strict-Transport-Security: max-age=31536000` |
| 7.8 | Set `X-Content-Type-Options: nosniff` header | Prevent MIME sniffing |
| 7.9 | Set `X-Frame-Options: DENY` header | Prevent clickjacking |
| 7.10 | Review and restrict CORS if applicable | |

---

## 8. SuperAdmin Account Setup

After deployment, create the initial SuperAdmin account:

```sql
-- Run on Portal.Membership database
-- First, register a user via the app (or insert manually)
-- Then assign the SuperAdmin role:

INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId])
VALUES ('YOUR_USER_ID', (SELECT [Id] FROM [dbo].[AspNetRoles] WHERE [Name] = 'SuperAdmin'));
```

Or seed the SuperAdmin role if it doesn't exist:

```sql
IF NOT EXISTS (SELECT 1 FROM [dbo].[AspNetRoles] WHERE [Name] = 'SuperAdmin')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (NEWID(), 'SuperAdmin', 'SUPERADMIN', NEWID());
END
```

---

## Summary: Deployment Order

1. Create databases and run migrations
2. Seed data (Plans, PlatformConfig)
3. Configure DNS and SSL
4. Deploy application files
5. Set environment variables (connection strings, Stripe keys, email)
6. Create Stripe live product/price/webhook
7. Update Plan record with live Stripe IDs
8. Start the application
9. Run verification tests
10. Create SuperAdmin account
11. Security hardening review

---

*Last updated: 2026-06-08*
