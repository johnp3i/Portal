# Requirements Document: Manual Payment Recording

## Introduction

Businesses can currently subscribe to the platform via two paths: paying through Stripe (card payment), or using a promo code (creates a trial subscription with no payment record). As a SuperAdmin, you can change plan and status, but there is no way to record an offline payment — bank transfer, cheque, or cash — against a subscription.

This is an immediate operational need. When a business pays via bank transfer and the SuperAdmin activates their subscription, there is no financial record of the payment. The platform has no visibility into how much was paid, when, by what method, or for what period.

Manual Payment Recording adds a "Record Payment" action to the Admin Subscriptions page. It supports both single payments and instalments — a customer can pay their annual subscription in multiple bank transfers, with each payment recorded against one invoice. All records reuse the existing `[billing].[Invoice]` and `[billing].[Payment]` tables so Stripe and manual revenue flows through a single pipeline.

The business owner can view their billing invoices and download PDF invoices from the existing `/Account/Billing` page. The PDF generation pipeline (BillingService, Razor template, PuppeteerSharp) is already built and fully functional — manual payment invoices flow through the same path automatically.

## Glossary

- **Manual_Payment**: A payment recorded by the SuperAdmin for an offline transaction (bank transfer, cheque, cash), as opposed to a Stripe-processed payment
- **Record_Payment_Modal**: The modal dialog on the Admin Subscriptions page where the SuperAdmin enters payment details
- **Add_Payment_Modal**: The modal dialog for recording an additional instalment against an existing invoice
- **Payment_Method**: The channel through which the payment was made: bank_transfer, cheque, cash, or other
- **Payment_Reference**: A free-text identifier for the payment (bank transfer reference, cheque number, receipt ID)
- **Billing_Invoice**: A record in `[billing].[Invoice]` representing a subscription charge for a period — can originate from Stripe or manual recording. For instalments, one invoice may have multiple payments.
- **Billing_Payment**: A record in `[billing].[Payment]` representing a single monetary transaction linked to an invoice
- **Instalment**: One of multiple partial payments against a single invoice. The invoice total represents the full amount due; each payment reduces the outstanding balance.

## Requirements

### Requirement 1: Record Payment Action (First Payment / New Invoice)

**User Story:** As a SuperAdmin, I want to record an offline payment for a business subscription, so that the financial transaction is captured in the platform.

#### Acceptance Criteria

1. THE Admin Subscriptions page SHALL display a "Record Payment" button in the Actions column for each business that has a subscription record
2. WHEN the "Record Payment" button is clicked, THE Record_Payment_Modal SHALL open with the business name and current plan name displayed as context
3. THE Record_Payment_Modal SHALL include the following fields: Invoice Amount (decimal, required — the total amount due for the period, pre-populated with the plan's annual price), Payment Amount (decimal, required, min 0.01 — the amount being paid now, defaults to same as Invoice Amount), Payment Method (dropdown: Bank Transfer, Cheque, Cash, Other — required), Payment Reference (text input, optional, max 200 chars), Period Start (date, required, defaults to today), Period End (date, required, defaults to +1 year), Notes (textarea, optional, max 500 chars)
4. IF the Payment Amount equals the Invoice Amount, THE invoice status SHALL be set to 'paid'
5. IF the Payment Amount is less than the Invoice Amount, THE invoice status SHALL be set to 'partially_paid'
6. THE Payment Amount SHALL NOT exceed the Invoice Amount
7. WHEN the modal is submitted with valid data, THE system SHALL create a billing invoice and payment record, and activate the subscription for the specified period

### Requirement 2: Record Additional Payment (Instalment)

**User Story:** As a SuperAdmin, I want to record additional payments against an existing invoice, so that I can track instalment payments for annual subscriptions.

#### Acceptance Criteria

1. THE Payment History modal SHALL display an "Add Payment" button next to each invoice that has status 'partially_paid'
2. WHEN "Add Payment" is clicked, THE Add_Payment_Modal SHALL open showing: the invoice number, total amount due, amount already paid, and the remaining balance
3. THE Add_Payment_Modal SHALL include: Payment Amount (decimal, required, defaults to remaining balance), Payment Method (dropdown), Payment Reference (text input, optional), Notes (textarea, optional)
4. THE Payment Amount SHALL NOT exceed the remaining balance
5. WHEN the additional payment brings the total paid to the invoice amount, THE invoice status SHALL update to 'paid'
6. WHEN the additional payment does not cover the full remaining balance, THE invoice status SHALL remain 'partially_paid'
7. THE instalment request SHALL include the BusinessId for server-side verification that the invoice belongs to the specified business

### Requirement 3: Invoice and Payment Creation

**User Story:** As a SuperAdmin, I want manual payments to create the same invoice and payment records as Stripe payments, so that all revenue data flows through one pipeline.

#### Acceptance Criteria

1. WHEN a new manual payment is recorded (first payment), THE system SHALL create a `[billing].[Invoice]` record with: BusinessId, StripeInvoiceId = NULL, AmountEur = Invoice Amount from the form, PeriodStart and PeriodEnd, Status = 'paid' or 'partially_paid', PaidAtUtc = now (if fully paid) or NULL (if partial), InvoiceNumber = auto-generated (next in sequence), IsEmailSent = false
2. WHEN a payment is recorded (first or instalment), THE system SHALL create a `[billing].[Payment]` record with: InvoiceId, AmountEur = Payment Amount, Method, PaidAtUtc = now, StripePaymentIntentId = NULL, Reference, Notes, RecordedByUserId
3. THE invoice and payment records SHALL be created within a single database transaction
4. THE system SHALL generate a sequential invoice number using the existing `InvoiceNumberGenerator` service — continuing the same sequence as Stripe invoices

### Requirement 4: Subscription Period Update

**User Story:** As a SuperAdmin, I want the subscription period to update when I record the first payment for a new invoice, so that the business's access is activated for the correct period.

#### Acceptance Criteria

1. WHEN a new invoice is created (first payment), THE system SHALL update the subscription period: CurrentPeriodStart = Period Start, CurrentPeriodEnd = Period End, Status = 'active'
2. WHEN a new invoice is created (first payment), THE system SHALL update BusinessPlan if it exists: Status = 'active', StartDateUtc = Period Start, EndDateUtc = Period End
3. WHEN an instalment payment is added to an existing invoice, THE subscription period SHALL NOT change (it was already set with the first payment)
4. IF the subscription status was 'trialing' or 'cancelled', THE system SHALL update it to 'active' after the first payment

### Requirement 5: Extended Payment Metadata

**User Story:** As a SuperAdmin, I want to store additional details about manual payments (reference number, notes, who recorded it), so that I have an audit trail for offline transactions.

#### Acceptance Criteria

1. THE `[billing].[Payment]` table SHALL include a nullable `Reference` column (NVARCHAR(200)) for payment reference numbers
2. THE `[billing].[Payment]` table SHALL include a nullable `Notes` column (NVARCHAR(500)) for free-text notes
3. THE `[billing].[Payment]` table SHALL include a nullable `RecordedByUserId` column (NVARCHAR(450)) identifying the SuperAdmin who recorded the payment
4. EXISTING Stripe payment records SHALL NOT be affected (new columns are nullable)

### Requirement 6: Payment History Visibility

**User Story:** As a SuperAdmin, I want to see the payment history for each business including instalment breakdowns, so that I can verify what has been paid and what's outstanding.

#### Acceptance Criteria

1. THE Admin Subscriptions page SHALL display a "Payment History" button in the Actions column for each business
2. WHEN the "Payment History" button is clicked, THE system SHALL open a modal showing a revenue summary line at the top ("Total Revenue: €X across N invoices — €Y outstanding") followed by all billing invoices for that business, with nested payments under each invoice
3. EACH invoice row SHALL display: Invoice Number, Total Amount Due, Amount Paid (sum of payments), Outstanding Balance, Status badge (paid/partially_paid), Period Covered
4. EACH payment row (nested under its invoice) SHALL display: Payment Amount, Method badge, Payment Date, Reference, Notes
5. INVOICES SHALL be ordered by CreatedAtUtc descending (newest first)
6. Invoices with status 'partially_paid' SHALL display an "Add Payment" button
7. THE modal SHALL distinguish Stripe payments from manual payments visually (method badge colour)

### Requirement 7: Invoice and Receipt Download

**User Story:** As a business owner, I want to download my subscription invoices as PDF from the billing page, so that I have financial records for my accounting.

#### Acceptance Criteria

1. THE existing `/Account/Billing` page already supports invoice PDF download via `BillingController.DownloadInvoice` — manual payment invoices SHALL flow through the same pipeline automatically (they use the same `[billing].[Invoice]` table)
2. THE billing invoice PDF SHALL display the correct payment method: "Bank Transfer", "Cheque", "Cash", or "Other" instead of "Stripe" for manual payments
3. FOR partially paid invoices, THE PDF SHALL show the payment status as "Partially Paid" with the amount received and the remaining balance
4. THE admin Payment History modal SHALL include a "Download" link for each invoice, calling a dedicated admin download endpoint (`/Admin/Subscriptions/DownloadInvoice/{invoiceId}?businessId={businessId}`) that bypasses tenant scoping
5. THE admin download endpoint SHALL be restricted to SuperAdmin role and SHALL call the same `BillingService.GenerateInvoicePdfAsync` as the user-facing endpoint
6. THE existing `BillingController.DownloadInvoice` endpoint uses `_tenantService.CurrentBusinessId` for tenant isolation — this SHALL NOT be modified. The admin endpoint is a separate route that accepts businessId as a parameter.

### Requirement 8: Validation and Safety

**User Story:** As a SuperAdmin, I want the system to validate my input and confirm before recording, so that I don't accidentally create incorrect financial records.

#### Acceptance Criteria

1. THE system SHALL validate that Payment Amount is greater than zero
2. THE system SHALL validate that Invoice Amount is greater than zero (for new invoices)
3. THE system SHALL validate that Period End is after Period Start (for new invoices)
4. THE system SHALL validate that Payment Amount does not exceed Invoice Amount (new) or remaining balance (instalment)
5. THE system SHALL validate that the business has a subscription record before allowing payment recording
6. THE system SHALL use BlockUI during the AJAX request and show SweetAlert2 for success/error feedback
7. BEFORE submitting, THE system SHALL display a SweetAlert2 confirmation dialog summarising: Business Name, Amount, Method, Period — requiring the SuperAdmin to confirm before proceeding
8. AFTER a successful payment recording, THE subscriptions table SHALL reload to reflect the updated status and period

### Requirement 9: Invoice Status Expansion

**User Story:** As a developer, I want the billing invoice status to support partial payments, so that instalments are tracked correctly.

#### Acceptance Criteria

1. THE `[billing].[Invoice]` Status CHECK constraint SHALL be expanded to include 'partially_paid' alongside the existing values (draft, open, paid, void, uncollectible)
2. THE `partially_paid` status SHALL indicate that some payments have been recorded but the total paid is less than the invoice amount
3. WHEN all payments for an invoice sum to the invoice amount, THE status SHALL transition from 'partially_paid' to 'paid' and PaidAtUtc SHALL be set to the timestamp of the final payment
