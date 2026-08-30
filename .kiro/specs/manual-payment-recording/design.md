# Design Document: Manual Payment Recording

## Overview

Manual Payment Recording extends the Admin Subscriptions page with three capabilities: recording offline payments (bank transfer, cheque, cash), recording instalment payments against existing invoices, and viewing payment history with invoice/payment drill-down. The design reuses the existing `[billing].[Invoice]` and `[billing].[Payment]` tables — the same pipeline Stripe payments flow through.

The billing invoice PDF download already works (`BillingService.GenerateInvoicePdfAsync` + `BillingController.DownloadInvoice` + `_InvoicePdf.cshtml` Razor template + PuppeteerSharp). Manual payment invoices flow through this existing pipeline automatically because they use the same `[billing].[Invoice]` table. Only minor updates are needed to the PDF model and template to handle the `partially_paid` status and manual payment method display.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Reuse `[billing].[Payment]` instead of a new table | One table for all subscription payments (Stripe + manual). Single pipeline for revenue reports, invoice numbering, payment history. |
| One invoice, multiple payments for instalments | The invoice represents the total due for a period (e.g., €1,290/year). Each instalment is a separate payment row linked to that invoice. Invoice status transitions: `open` → `partially_paid` → `paid`. |
| Invoice Amount vs. Payment Amount | The modal separates these: Invoice Amount = total due for the period, Payment Amount = amount being paid now. For single full payments, they're equal. For instalments, Payment Amount < Invoice Amount. |
| Existing PDF pipeline — no new code | `BillingController.DownloadInvoice` + `BillingService.GenerateInvoicePdfAsync` already generates PDF invoices. Manual invoices go through the same path. Only the template needs minor updates for `partially_paid` status. |
| Status CHECK constraint expansion | Adding `'partially_paid'` to the existing CHECK. The migration drops and recreates the constraint. |
| No separate receipt entity | The billing invoice PDF serves as both invoice and receipt — it includes payment information at the bottom. For partially paid invoices, it shows each payment and the remaining balance. |

## Architecture

```mermaid
flowchart TD
    subgraph Admin Browser
        A[SubscriptionManagement.cshtml] --> B[Record Payment Modal]
        A --> C[Payment History Modal]
        C --> D[Add Payment Modal]
        B -->|POST| E[/Admin/Subscriptions/RecordPayment]
        D -->|POST| F[/Admin/Subscriptions/AddPayment]
        C -->|GET| G[/Admin/Subscriptions/PaymentHistory]
    end

    subgraph User Browser
        H[/Account/Billing] --> I[Download Invoice PDF]
        I -->|GET| J[/Account/Billing/DownloadInvoice/id]
    end

    subgraph Controller
        E --> K[AdminSubscriptionController.AxPostRecordPayment]
        F --> L[AdminSubscriptionController.AxPostAddPayment]
        G --> M[AdminSubscriptionController.AxGetPaymentHistory]
        J --> N[BillingController.DownloadInvoice — existing]
    end

    subgraph Service
        N --> O[BillingService.GenerateInvoicePdfAsync — existing]
    end

    subgraph Database
        K -->|Transaction| P[(billing.Invoice)]
        K -->|Transaction| Q[(billing.Payment)]
        K -->|Transaction| R[(billing.Subscription)]
        K -->|Transaction| S[(dbo.BusinessPlan)]
        L -->|Transaction| P
        L -->|Transaction| Q
        M --> P
        M --> Q
        O --> P
        O --> Q
    end
```

## Components and Interfaces

### Database Migrations

**Migration A: Add manual payment columns to [billing].[Payment]**

```sql
ALTER TABLE [billing].[Payment] ADD [Reference] NVARCHAR(200) NULL;
ALTER TABLE [billing].[Payment] ADD [Notes] NVARCHAR(500) NULL;
ALTER TABLE [billing].[Payment] ADD [RecordedByUserId] NVARCHAR(450) NULL;
```

**Migration B: Expand [billing].[Invoice] Status CHECK constraint**

```sql
ALTER TABLE [billing].[Invoice] DROP CONSTRAINT [CK_BillingInvoice_Status];
ALTER TABLE [billing].[Invoice] ADD CONSTRAINT [CK_BillingInvoice_Status]
    CHECK ([Status] IN ('draft','open','paid','void','uncollectible','partially_paid'));
```

### Controller Endpoints

**New: `AxPostRecordPayment`** — First payment (creates invoice + payment + activates subscription)

```csharp
[HttpPost("RecordPayment")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AxPostRecordPayment([FromBody] RecordManualPaymentRequest request)
```

Within a single database transaction (started BEFORE invoice number generation — `GenerateNextAsync` requires an active transaction):

1. Validates input (amounts > 0, periodEnd > periodStart, paymentAmount <= invoiceAmount, method is valid)
2. Verifies subscription exists via `SubscriptionRepository.GetByBusinessIdAsync`
3. Starts `BeginTransactionAsync()`
4. Generates invoice number via `InvoiceNumberGenerator.GenerateNextAsync(DateTime.UtcNow)`
5. Determines invoice status: `paymentAmount == invoiceAmount` → `'paid'` with `PaidAtUtc = now`; `paymentAmount < invoiceAmount` → `'partially_paid'` with `PaidAtUtc = NULL`
6. Inserts `[billing].[Invoice]` — AmountEur = invoiceAmount (total due), StripeInvoiceId = NULL
7. Inserts `[billing].[Payment]` — AmountEur = paymentAmount (amount paid now), Method, Reference, Notes, RecordedByUserId
8. Updates `[billing].[Subscription]` via `SubscriptionRepository.UpdatePeriodAsync(subscription.Id, periodStart, periodEnd, "active", subscription.PlanId)` — uses the subscription's own Id and existing PlanId from the lookup result
9. Updates `[dbo].[BusinessPlan]` if it exists (null-safe) — Status = 'active', dates from form
10. Commits transaction
11. Returns `{ success, message, invoiceNumber }`

**New: `AxPostAddPayment`** — Instalment payment (adds payment to existing invoice)

```csharp
[HttpPost("AddPayment")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AxPostAddPayment([FromBody] AddInstalmentPaymentRequest request)
```

Within a single transaction:

1. Validates input (amount > 0, invoiceId exists, invoice status is 'partially_paid')
2. Calculates total already paid (sum of existing payments for this invoice)
3. Validates paymentAmount <= (invoiceAmount - totalPaid)
4. Inserts `[billing].[Payment]`
5. If totalPaid + paymentAmount == invoiceAmount → update invoice status to `'paid'`, set PaidAtUtc = now
6. Commits transaction
7. Returns `{ success, message }`

**New: `AxGetPaymentHistory`** — Returns invoices with nested payments

```csharp
[HttpGet("PaymentHistory/{businessId}")]
public async Task<IActionResult> AxGetPaymentHistory(int businessId)
```

Returns JSON:
```json
{
  "success": true,
  "summary": { "totalRevenue": 1068.00, "invoiceCount": 2, "outstanding": 860.00 },
  "data": [
    {
      "invoiceId": 5,
      "invoiceNumber": "3I-INV-2026-0057",
      "amountDue": 1290.00,
      "amountPaid": 430.00,
      "outstanding": 860.00,
      "status": "partially_paid",
      "periodStart": "2026-08-28",
      "periodEnd": "2027-08-28",
      "createdAtUtc": "2026-08-28",
      "payments": [
        {
          "id": 8,
          "amount": 430.00,
          "method": "bank_transfer",
          "paidAtUtc": "2026-08-28",
          "reference": "TRF-2026-0042",
          "notes": "First instalment",
          "isStripe": false
        }
      ]
    }
  ]
}
```

**New: `AxGetDownloadInvoice`** — Admin-specific invoice PDF download (bypasses tenant scoping)

```csharp
[HttpGet("DownloadInvoice/{invoiceId}")]
public async Task<IActionResult> AxGetDownloadInvoice(int invoiceId, int businessId)
```

The existing `BillingController.DownloadInvoice` uses `_tenantService.CurrentBusinessId` which scopes to the logged-in user's business. SuperAdmin needs to download invoices for any business. This endpoint accepts `businessId` as a parameter and calls the same `BillingService.GenerateInvoicePdfAsync(invoiceId, businessId)`. Authorization is implicit via the controller's `[Authorize(Roles = "SuperAdmin")]` attribute.

### Service Dependencies / DI Injections

The `AdminSubscriptionController` currently only has `PortalDbContext`. New dependencies:

| Dependency | Purpose |
|-----------|---------|
| `IInvoiceNumberGenerator` | Generate sequential invoice numbers |
| `BillingInvoiceRepository` | Insert invoices, query by business |
| `BillingPaymentRepository` | Insert payments, query by invoice |
| `SubscriptionRepository` | Look up and update subscription |
| `IBillingService` | Generate invoice PDFs for admin download |

### Repository Updates Required

**`BillingPaymentRepository.InsertAsync`** — Add `[Reference]`, `[Notes]`, `[RecordedByUserId]` to the INSERT column list.

**`BillingPaymentRepository.GetByInvoiceIdAsync`** — Add the 3 new columns to the SELECT list.

**`BillingInvoiceRepository.GetByBusinessIdPagedAsync`** — Add `[InvoiceNumber]`, `[IsEmailSent]` to the SELECT and reader mapping.

**`BillingInvoiceRepository.GetByIdAsync`** — Add `[InvoiceNumber]`, `[IsEmailSent]` to the SELECT. Without this fix, the PDF generator's fallback `invoice.InvoiceNumber ?? $"INV-{invoice.Id:D6}"` would show the wrong format for manual invoices that always have a real InvoiceNumber.

**New: `BillingInvoiceRepository.GetByInvoiceIdAsync(int id)`** — Admin-only lookup by invoice Id without business scoping. Used by the AddPayment flow to load the invoice for status/amount validation.

### Request Models

**`RecordManualPaymentRequest`** (first payment — new invoice):

```csharp
public class RecordManualPaymentRequest
{
    public int BusinessId { get; set; }
    public decimal InvoiceAmount { get; set; }      // Total due for the period
    public decimal PaymentAmount { get; set; }       // Amount being paid now
    public string Method { get; set; } = null!;
    public string? Reference { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string? Notes { get; set; }
}
```

**`AddInstalmentPaymentRequest`** (additional payment on existing invoice):

```csharp
public class AddInstalmentPaymentRequest
{
    public int InvoiceId { get; set; }
    public int BusinessId { get; set; }            // For server-side verification
    public decimal PaymentAmount { get; set; }
    public string Method { get; set; } = null!;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
```

### Entity Changes

**`BillingPayment`** — Add:

```csharp
public string? Reference { get; set; }
public string? Notes { get; set; }
public string? RecordedByUserId { get; set; }
```

### PDF Invoice Updates (Existing Pipeline)

The `BillingService.GenerateInvoicePdfAsync` method and `_InvoicePdf.cshtml` template already render invoices with a single payment. Minor updates needed:

1. **`BillingInvoicePdfModel`** — Add `List<PaymentLineItem> Payments` (amount, method, date, reference) and `AmountPaid`, `Outstanding`, `IsPartiallyPaid` fields
2. **`_InvoicePdf.cshtml`** — When `IsPartiallyPaid`, show a "Payment History" section listing each instalment with amount, method, and date, followed by "Outstanding: €X"
3. **`GenerateInvoicePdfAsync`** — Load ALL payments for the invoice (not just first), compute AmountPaid/Outstanding, set the new model fields

## Data Models

### Modified Table: `[billing].[Payment]`

| Column | Type | Change |
|--------|------|--------|
| Reference | NVARCHAR(200) NULL | **NEW** |
| Notes | NVARCHAR(500) NULL | **NEW** |
| RecordedByUserId | NVARCHAR(450) NULL | **NEW** |

### Modified Constraint: `[billing].[Invoice]`

| Constraint | Old Values | New Values |
|-----------|------------|------------|
| `CK_BillingInvoice_Status` | draft, open, paid, void, uncollectible | draft, open, paid, void, uncollectible, **partially_paid** |

### Method Values

| Value | Source |
|-------|--------|
| `stripe` | Stripe webhook (existing) |
| `bank_transfer` | Manual — bank transfer |
| `cheque` | Manual — cheque |
| `cash` | Manual — cash payment |
| `other` | Manual — other offline method |

## UI Design

### Record Payment Modal (First Payment)

```
+-----------------------------------------------------------------+
|  Record Payment -- 3 Inventors Limited                    [X]   |
|  Current Plan: Professional (EUR 890.00/year)                    |
|  ---------------------------------------------------------------+
|  Invoice Amount (EUR) *  [890.00   ]   (total due for period)   |
|  Payment Amount (EUR) *  [890.00   ]   (amount being paid now)  |
|  Payment Method *        [Bank Transfer v]                       |
|  Reference               [TRF-2026-0042            ]            |
|  ---------------------------------------------------------------+
|  Period Start *  [2026-08-28]    Period End *  [2027-08-28]     |
|  ---------------------------------------------------------------+
|  Notes  [Annual subscription -- Professional plan              ] |
|                                                                  |
|                               [Cancel]  [Record Payment]        |
+-----------------------------------------------------------------+
```

For instalment scenario (Enterprise €1,290/year, first of 3 payments):
- Invoice Amount: 1290.00
- Payment Amount: 430.00
- → Creates invoice with status `partially_paid`

### Add Payment Modal (Instalment)

```
+-----------------------------------------------------------------+
|  Add Payment -- INV 3I-INV-2026-0057                     [X]   |
|  Total Due: EUR 1,290.00  |  Paid: EUR 430.00  |  Remaining: EUR 860.00|
|  ---------------------------------------------------------------+
|  Payment Amount (EUR) *  [860.00   ]   (defaults to remaining) |
|  Payment Method *        [Bank Transfer v]                       |
|  Reference               [TRF-2026-0098            ]            |
|  Notes  [Second instalment                                     ] |
|                                                                  |
|                               [Cancel]  [Add Payment]           |
+-----------------------------------------------------------------+
```

### Payment History Modal (with nested payments)

```
+---------------------------------------------------------------------------+
|  Payment History -- 3 Inventors Limited                            [X]   |
|  Total Revenue: EUR 1,068.00 across 3 invoices -- EUR 860.00 outstanding |
|  -------------------------------------------------------------------------+
|                                                                           |
|  3I-INV-2026-0057   EUR 1,290.00   Partially Paid   Aug 2026-Aug 2027   |
|  Outstanding: EUR 860.00                          [Add Payment] [Download]|
|  +-- EUR 430.00  Bank Transfer  28 Aug 2026  Ref: TRF-2026-0042         |
|      First instalment                                                     |
|                                                                           |
|  3I-INV-2026-0056   EUR 890.00     Paid            Jul 2026-Aug 2026    |
|                                                               [Download] |
|  +-- EUR 890.00  Bank Transfer  04 Jul 2026  Ref: TRF-2026-0030         |
|      Annual subscription                                                  |
|                                                                           |
|  3I-INV-2026-0055   EUR 89.00      Paid            Jun 2026-Jul 2026    |
|                                                               [Download] |
|  +-- EUR 89.00   Stripe         04 Jun 2026                             |
|                                                                           |
|                                                              [Close]     |
+---------------------------------------------------------------------------+
```

- `Paid` = green badge, `Partially Paid` = amber badge
- "Add Payment" button only appears on `partially_paid` invoices
- "Download" link calls `/Admin/Subscriptions/DownloadInvoice/{id}?businessId={businessId}` (new admin endpoint — the existing `BillingController.DownloadInvoice` uses tenant scoping which doesn't work for admin cross-business access)
- Stripe payments show blue "Stripe" badge, manual payments show green/amber/grey badges

### Confirmation Dialog (before submit)

```
+------------------------------------------------+
|  Confirm Payment Recording                      |
|                                                  |
|  Business:  3 Inventors Limited                  |
|  Invoice:   EUR 1,290.00 (Enterprise annual)     |
|  Payment:   EUR 430.00                           |
|  Method:    Bank Transfer                        |
|  Period:    28 Aug 2026 -> 28 Aug 2027           |
|  Reference: TRF-2026-0042                        |
|                                                  |
|  Invoice will be marked as Partially Paid.       |
|                                                  |
|          [Cancel]    [Confirm & Record]           |
+------------------------------------------------+
```

## Correctness Properties

### Property 1: Invoice-Payment consistency

*For any* manual payment recording, invoice and payment records are created within a single transaction. If any INSERT or UPDATE fails, the entire transaction is rolled back.

### Property 2: Payment amount invariant

*For any* invoice, the sum of all linked Payment.AmountEur values never exceeds Invoice.AmountEur. Validation enforces this at both the first payment and each instalment.

### Property 3: Status transition correctness

*For any* invoice: when total payments == invoice amount → status is `'paid'` and PaidAtUtc is set; when total payments < invoice amount → status is `'partially_paid'` and PaidAtUtc is NULL. The transition from `partially_paid` to `paid` is atomic within the instalment transaction.

### Property 4: Invoice number continuity

*For any* manual payment invoice, the generated invoice number follows the same sequential series as Stripe-generated numbers. No gaps, no duplicates.

### Property 5: Subscription period — set once per invoice

*For any* invoice, the subscription period is set during the first payment only. Instalment payments do not modify the subscription period or status.

### Property 6: Backward compatibility

*For any* existing Stripe payment/invoice record, all new columns are NULL and no behaviour changes.

### Property 7: PDF pipeline compatibility

*For any* manual payment invoice, `BillingController.DownloadInvoice` and `BillingService.GenerateInvoicePdfAsync` produce a valid PDF using the same template as Stripe invoices. The admin download endpoint (`AdminSubscriptionController.AxGetDownloadInvoice`) uses the same service method with an explicit businessId parameter.

### Property 8: Decimal precision for financial comparisons

*For any* payment amount comparison (totalPaid + paymentAmount vs. invoiceAmount), C# `decimal` type is used throughout. Decimal provides exact arithmetic for financial values — no floating-point rounding errors. All amount columns in the database use `DECIMAL(10,2)` which aligns with the C# decimal type.

## Error Handling

| Scenario | Response |
|----------|----------|
| Payment Amount <= 0 | `{ success: false, message: "Payment amount must be greater than zero." }` |
| Invoice Amount <= 0 | `{ success: false, message: "Invoice amount must be greater than zero." }` |
| Payment > Invoice Amount | `{ success: false, message: "Payment amount cannot exceed the invoice total." }` |
| Instalment > remaining | `{ success: false, message: "Payment amount exceeds the remaining balance of €X." }` |
| Period End <= Period Start | `{ success: false, message: "Period end must be after period start." }` |
| No subscription | `{ success: false, message: "No subscription found for this business." }` |
| Invoice not partially_paid (for instalment) | `{ success: false, message: "This invoice is already fully paid." }` |
| Transaction failure | Rollback, return generic error |
