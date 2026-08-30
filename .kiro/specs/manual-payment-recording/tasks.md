# Implementation Plan: Manual Payment Recording

## Overview

This plan adds manual payment recording with instalment support to the Admin Subscriptions page. A SuperAdmin can record offline payments, record additional instalments against partially-paid invoices, view payment history with nested payment breakdowns, and download invoice PDFs for any business. The existing billing invoice PDF pipeline handles manual invoices automatically.

## Tasks

- [x] 1. Database migrations
  - [x] 1.1 Add Reference, Notes, RecordedByUserId columns to [billing].[Payment]
    - `ALTER TABLE [billing].[Payment] ADD [Reference] NVARCHAR(200) NULL`
    - `ALTER TABLE [billing].[Payment] ADD [Notes] NVARCHAR(500) NULL`
    - `ALTER TABLE [billing].[Payment] ADD [RecordedByUserId] NVARCHAR(450) NULL`
    - Idempotent with IF NOT EXISTS column checks
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Expand [billing].[Invoice] Status CHECK constraint to include 'partially_paid'
    - Drop existing `[CK_BillingInvoice_Status]` constraint
    - Recreate with values: draft, open, paid, void, uncollectible, partially_paid
    - _Requirements: 9.1_

- [x] 2. Entity and EF Core changes
  - [x] 2.1 Add new properties to BillingPayment entity
    - Add `public string? Reference { get; set; }`
    - Add `public string? Notes { get; set; }`
    - Add `public string? RecordedByUserId { get; set; }`
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 2.2 Add EF Core property configuration for new BillingPayment columns
    - In `PortalDbContext.ConfigureBillingPayment`, add HasMaxLength for Reference (200), Notes (500), RecordedByUserId (450)
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 2.3 Update EF Core BillingInvoice CHECK constraint string to include 'partially_paid'
    - In `PortalDbContext.ConfigureBillingInvoice`, update the `CK_BillingInvoice_Status` HasCheckConstraint string from `"[Status] IN ('draft','open','paid','void','uncollectible')"` to include `'partially_paid'`
    - Keeps EF model in sync with the database migration
    - _Requirements: 9.1_

- [x] 3. Repository updates
  - [x] 3.1 Update BillingPaymentRepository.InsertAsync to include new columns
    - Add `[Reference]`, `[Notes]`, `[RecordedByUserId]` to INSERT column list and VALUES
    - Add SqlParameter entries with `?? (object)DBNull.Value` for nullable values
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 3.2 Update BillingPaymentRepository.GetByInvoiceIdAsync SELECT
    - Add `[Reference]`, `[Notes]`, `[RecordedByUserId]` to SELECT column list
    - Without this fix, entity properties will always be null when reading payment records
    - _Requirements: 6.4_

  - [x] 3.3 Update BillingInvoiceRepository.GetByBusinessIdPagedAsync SELECT
    - Add `[InvoiceNumber]`, `[IsEmailSent]` to SELECT column list
    - Update reader mapping to include the new columns
    - _Requirements: 6.3, 7.4_

  - [x] 3.4 Update BillingInvoiceRepository.GetByIdAsync SELECT
    - Add `[InvoiceNumber]`, `[IsEmailSent]` to the SELECT column list
    - This method is used by BillingService.GenerateInvoicePdfAsync — without this fix, invoice.InvoiceNumber will be null and the PDF will show the fallback format `INV-{Id:D6}` instead of the real number
    - _Requirements: 7.2_

  - [x] 3.5 Add BillingPaymentRepository.GetTotalPaidByInvoiceIdAsync
    - New method: `SELECT ISNULL(SUM([AmountEur]), 0) FROM [billing].[Payment] WHERE [InvoiceId] = @InvoiceId`
    - Returns decimal — the total amount already paid for an invoice
    - Used by the AddPayment endpoint to calculate remaining balance
    - _Requirements: 2.4_

  - [x] 3.6 Add BillingInvoiceRepository.UpdateStatusAsync
    - New method: updates `[Status]` and optionally `[PaidAtUtc]` on an invoice by Id
    - Used when an instalment completes the invoice (partially_paid → paid)
    - _Requirements: 2.5, 9.3_

  - [x] 3.7 Add BillingInvoiceRepository.GetByInvoiceIdAsync (admin — no business scoping)
    - New method: `SELECT ... FROM [billing].[Invoice] WHERE [Id] = @Id` — without BusinessId filter
    - Used by the AddPayment flow to load the invoice for status/amount validation
    - Admin-only — the existing `GetByIdAsync(int id, int businessId)` requires tenant scoping
    - _Requirements: 2.2_

- [x] 4. Request models
  - [x] 4.1 Create RecordManualPaymentRequest model
    - BusinessId (int), InvoiceAmount (decimal), PaymentAmount (decimal), Method (string), Reference (string?), PeriodStart (DateTime), PeriodEnd (DateTime), Notes (string?)
    - _Requirements: 1.3_

  - [x] 4.2 Create AddInstalmentPaymentRequest model
    - InvoiceId (int), BusinessId (int), PaymentAmount (decimal), Method (string), Reference (string?), Notes (string?)
    - BusinessId included for server-side verification that the invoice belongs to the specified business
    - _Requirements: 2.3, 2.7_

- [x] 5. Controller — DI and endpoints
  - [x] 5.1 Inject new dependencies into AdminSubscriptionController
    - Add: `IInvoiceNumberGenerator`, `BillingInvoiceRepository`, `BillingPaymentRepository`, `SubscriptionRepository`, `IBillingService`
    - All already registered in Program.cs
    - `IBillingService` needed for the admin invoice PDF download endpoint
    - _Requirements: 3.1, 3.2, 3.4, 7.4, 7.5_

  - [x] 5.2 Implement AxPostRecordPayment (first payment — new invoice)
    - Validate: invoiceAmount > 0, paymentAmount > 0, paymentAmount <= invoiceAmount, periodEnd > periodStart, valid method
    - Look up subscription via `_subscriptionRepository.GetByBusinessIdAsync(request.BusinessId)` — fail if null
    - **Start transaction BEFORE `GenerateNextAsync`** (requires active transaction)
    - Determine status: paymentAmount == invoiceAmount → 'paid' (PaidAtUtc=now), else → 'partially_paid' (PaidAtUtc=NULL)
    - INSERT invoice (AmountEur=invoiceAmount), INSERT payment (AmountEur=paymentAmount)
    - UPDATE subscription via `_subscriptionRepository.UpdatePeriodAsync(subscription.Id, periodStart, periodEnd, "active", subscription.PlanId)` — uses the subscription's own Id and existing PlanId from the lookup result
    - UPDATE BusinessPlan if exists (null-safe — log warning if missing)
    - Commit, return `{ success, message, invoiceNumber }`
    - All amount comparisons use C# `decimal` type (exact arithmetic, no floating-point risk)
    - _Requirements: 1.4, 1.5, 1.6, 1.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4_

  - [x] 5.3 Implement AxPostAddPayment (instalment — existing invoice)
    - Validate: invoiceId exists (via `GetByInvoiceIdAsync`), invoice status is 'partially_paid', paymentAmount > 0
    - Verify invoice.BusinessId matches request.BusinessId (sanity check)
    - Calculate remaining: invoice.AmountEur - `GetTotalPaidByInvoiceIdAsync(invoiceId)`
    - Validate: paymentAmount <= remaining
    - Within transaction: INSERT payment, if totalPaid + paymentAmount == invoiceAmount → update invoice to 'paid' via `UpdateStatusAsync` + set PaidAtUtc
    - Commit, return `{ success, message }`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [x] 5.4 Implement AxGetPaymentHistory
    - Query invoices by businessId with nested payments
    - Compute amountPaid (sum of payments), outstanding (amountDue - amountPaid) per invoice
    - Compute revenue summary: totalRevenue (sum of all payments across all invoices), invoiceCount, totalOutstanding
    - Return JSON with summary + invoices → payments hierarchy
    - Include isStripe flag (StripePaymentIntentId != null)
    - Order by CreatedAtUtc DESC
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 5.5 Implement AxGetDownloadInvoice (admin PDF download)
    - Add `[HttpGet("DownloadInvoice/{invoiceId}")]` endpoint
    - Accept `businessId` as query parameter
    - Call `_billingService.GenerateInvoicePdfAsync(invoiceId, businessId)` — same service as user-facing endpoint
    - Return `File(pdfBytes, "application/pdf", $"Invoice-{invoiceId}.pdf")`
    - Authorization: implicit via controller's `[Authorize(Roles = "SuperAdmin")]`
    - This is needed because `BillingController.DownloadInvoice` uses `_tenantService.CurrentBusinessId` which scopes to the admin's own business, not the customer's
    - _Requirements: 7.4, 7.5, 7.6_

- [x] 6. Checkpoint — Build verification
  - Build and verify all backend changes compile cleanly.

- [x] 7. PDF invoice updates (existing pipeline)
  - [x] 7.1 Update BillingInvoicePdfModel for partial payment display
    - Add: `List<PaymentLineItem> Payments`, `decimal AmountPaid`, `decimal Outstanding`, `bool IsPartiallyPaid`
    - `PaymentLineItem`: Amount, Method, PaidAtUtc, Reference
    - _Requirements: 7.2, 7.3_

  - [x] 7.2 Update BillingService.GenerateInvoicePdfAsync for multiple payments
    - Load ALL payments for the invoice (not just first) via `GetByInvoiceIdAsync`
    - Compute AmountPaid = sum, Outstanding = AmountEur - AmountPaid
    - Map each payment to a `PaymentLineItem` for the model
    - Set PaymentMethod display: single payment → that method; multiple payments → "Multiple"
    - _Requirements: 7.2, 7.3_

  - [x] 7.3 Update _InvoicePdf.cshtml for partial payment display
    - When IsPartiallyPaid or multiple payments: show "Payment History" section listing each payment (amount, method, date, reference)
    - Show "Outstanding Balance: €X" below the payment list when IsPartiallyPaid
    - When fully paid with multiple payments: show the payment list as receipt detail (no outstanding)
    - Status badge: "Paid" (green) or "Partially Paid" (amber)
    - _Requirements: 7.2, 7.3_

- [x] 8. View changes — SubscriptionManagement.cshtml
  - [x] 8.1 Add Record Payment button to Actions column
    - Green button, pass businessId, businessName, planName, annualPrice
    - Only enabled when business has a subscription
    - _Requirements: 1.1_

  - [x] 8.2 Add Payment History button to Actions column
    - Small "History" button for each business
    - _Requirements: 6.1_

  - [x] 8.3 Add Record Payment Modal
    - Business name + plan name header, Invoice Amount (pre-filled with plan annual price), Payment Amount (defaults to same), Method dropdown, Reference, Period dates, Notes
    - _Requirements: 1.2, 1.3_

  - [x] 8.4 Add Payment History Modal
    - Revenue summary line at top ("Total Revenue: €X across N invoices — €Y outstanding")
    - Scrollable list of invoices with nested payments
    - Invoice rows: number, amount due, amount paid, outstanding, status badge, period, Download link (using admin endpoint), Add Payment button (if partially_paid)
    - Payment rows (nested): amount, method badge, date, reference, notes
    - Download link uses `/Admin/Subscriptions/DownloadInvoice/{id}?businessId={bid}` (NOT the BillingController endpoint)
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 8.5 Add Add Payment Modal (for instalments)
    - Invoice number + balance summary header, Payment Amount (defaults to remaining), Method, Reference, Notes
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 8.6 Add plan pricing to the view model
    - Add `AnnualPriceEur` to `SubscriptionManagementItem` or pass via `AvailablePlans`
    - For amount pre-population in the Record Payment modal
    - _Requirements: 1.3_

- [x] 9. JavaScript
  - [x] 9.1 Implement openRecordPaymentModal(businessId, businessName, planName, annualPrice)
    - Populate modal, pre-fill Invoice Amount and Payment Amount with annualPrice, set default dates
    - _Requirements: 1.2, 1.3_

  - [x] 9.2 Implement submitRecordPayment()
    - JS validation (amounts, dates, method, paymentAmount <= invoiceAmount)
    - SweetAlert2 confirmation dialog with summary + "invoice will be marked as Partially Paid" when paymentAmount < invoiceAmount
    - BlockUI → POST → success Swal (show invoice number) → reload page
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.6, 8.7, 8.8_

  - [x] 9.3 Implement openPaymentHistory(businessId, businessName)
    - BlockUI → GET /PaymentHistory/{id} → render revenue summary + invoices with nested payments
    - Method badges, Download links (admin endpoint), Add Payment buttons
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [x] 9.4 Implement openAddPaymentModal(invoiceId, businessId, invoiceNumber, amountDue, amountPaid)
    - Show balance summary, pre-fill remaining amount, store businessId for the request
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 9.5 Implement submitAddPayment()
    - JS validation (amount > 0, amount <= remaining)
    - SweetAlert2 confirmation
    - BlockUI → POST /AddPayment → refresh payment history modal
    - _Requirements: 2.4, 2.5, 2.6, 8.6, 8.7_

- [x] 10. Final checkpoint
  - Build, verify no errors
  - Test: record a full payment → verify invoice (paid) + payment + subscription updated
  - Test: record partial payment → verify invoice (partially_paid) → add second instalment → verify invoice flips to paid
  - Test: download PDF from admin Payment History → verify admin endpoint serves correct PDF
  - Test: download PDF for partially_paid invoice → verify it shows payment breakdown
  - Test: download PDF for fully paid manual invoice → verify correct method displayed
  - Test: verify user can download their manual invoice from /Account/Billing (existing endpoint)

## Notes

- No new tables — reuses existing [billing].[Invoice] and [billing].[Payment]
- Invoice Amount = total due for the period. Payment Amount = amount paid now. For full payments, they're equal.
- Invoice status lifecycle for instalments: partially_paid → paid (when all payments sum to invoice amount)
- The subscription period is set with the first payment only — instalments don't change it
- Invoice numbers follow the same sequence as Stripe (InvoiceNumberGenerator)
- PDF pipeline is existing (BillingService + _InvoicePdf.cshtml + PuppeteerSharp) — only template updates needed for partial payment display
- Admin PDF download uses a new endpoint on AdminSubscriptionController (NOT BillingController) because the existing endpoint uses tenant scoping
- BusinessPlan may not exist for Stripe-provisioned businesses — handle with null check
- Transaction MUST start BEFORE InvoiceNumberGenerator.GenerateNextAsync
- All amount comparisons use C# `decimal` type — exact arithmetic, no floating-point risk
- AddInstalmentPaymentRequest includes BusinessId for server-side invoice ownership verification
- All AJAX follows BlockUI + SweetAlert2 pattern
- All catch blocks use `catch (Exception ex) { throw; }`
- Future enhancement: "Void Invoice" action for invoices with no payments — the CHECK constraint already allows 'void' status

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "3.6", "3.7", "4.1", "4.2"] },
    { "id": 3, "tasks": ["5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4", "5.5"] },
    { "id": 5, "tasks": ["6"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5"] },
    { "id": 9, "tasks": ["10"] }
  ]
}
```
