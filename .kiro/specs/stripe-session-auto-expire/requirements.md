# Requirements Document

## Introduction

When a business records a payment that fully settles an invoice, any still-open Stripe Checkout Sessions for that invoice become a liability — a customer who already has the checkout page open can complete the payment, resulting in an overpayment. This feature proactively expires pending Stripe Checkout Sessions the moment an invoice transitions to Paid, closing the race condition window.

## Glossary

- **Portal**: The ASP.NET Core MVC web application (this system)
- **Auto_Expire_Service**: The service component responsible for identifying and expiring pending Stripe Checkout Sessions when an invoice is fully paid
- **Financial_Status_Engine**: The existing engine (`IFinancialStatusEngine`) that computes outstanding balance and determines the invoice's financial status (Unpaid, Partially Paid, Paid, Overdue, Written Off)
- **Checkout_Session**: A `[stripe].[CheckoutSession]` record representing a Stripe Checkout Session created for a customer to pay an invoice by card
- **Stripe_API**: The external Stripe REST API, specifically `Stripe.Checkout.SessionService.ExpireAsync()`
- **Pending_Session**: A `CheckoutSession` record with `Status = 'pending'` — the customer has not yet completed or abandoned payment
- **Fully_Paid**: An invoice state where outstanding balance equals zero (`TotalAmount - SUM(non-voided payments) - applied credit = 0`), corresponding to `InvoiceFinancialStatusTypeId = 3`
- **Payment_Service**: The existing service (`PaymentService`) that records manual payments and triggers financial status recalculation
- **Webhook_Handler**: The existing controller (`StripeConnectWebhookController`) that processes Stripe webhook events

## Requirements

### Requirement 1: Expire Pending Sessions on Full Payment

**User Story:** As a business owner, I want pending Stripe Checkout Sessions to be automatically expired when an invoice is fully paid, so that customers cannot accidentally overpay through an already-open checkout page.

#### Acceptance Criteria

1. WHEN the Financial_Status_Engine determines that an invoice's financial status transitions to Fully_Paid (status 3), THE Auto_Expire_Service SHALL retrieve all Pending_Sessions for that invoice from the database.
2. WHEN Pending_Sessions exist for a Fully_Paid invoice, THE Auto_Expire_Service SHALL call Stripe_API `ExpireAsync` for each Pending_Session using its `StripeSessionId`.
3. WHEN the Stripe_API confirms expiration of a session, THE Auto_Expire_Service SHALL update the corresponding Checkout_Session record to `Status = 'expired'` and set `CompletedAtUtc` to the current UTC timestamp.
4. WHEN no Pending_Sessions exist for the invoice, THE Auto_Expire_Service SHALL complete without error and without making any Stripe_API calls.

### Requirement 2: Trigger Points for Auto-Expire

**User Story:** As a business owner, I want the auto-expire mechanism to fire regardless of how the invoice becomes fully paid, so that all payment recording paths are covered.

#### Acceptance Criteria

1. WHEN a manual payment is recorded via Payment_Service that causes the invoice to become Fully_Paid, THE Auto_Expire_Service SHALL execute for that invoice.
2. WHEN a FIFO allocation (PaymentAllocationEngine) settles the remaining balance of an invoice causing it to become Fully_Paid, THE Auto_Expire_Service SHALL execute for that invoice.
3. WHEN a credit note application zeroes the outstanding balance causing the invoice to become Fully_Paid, THE Auto_Expire_Service SHALL execute for that invoice.
4. WHEN a Stripe webhook payment (`checkout.session.completed`) causes the invoice to become Fully_Paid, THE Auto_Expire_Service SHALL expire all other Pending_Sessions for that invoice (excluding the session that just completed).

### Requirement 3: Graceful Failure Handling

**User Story:** As a business owner, I want the payment recording to succeed even if Stripe is unreachable, so that the auto-expire feature never blocks critical business operations.

#### Acceptance Criteria

1. IF the Stripe_API returns an error (network timeout, 5xx, rate limit), THEN THE Auto_Expire_Service SHALL log a warning with the StripeSessionId and error details and continue processing remaining sessions.
2. IF the Stripe_API indicates the session is already expired or completed, THEN THE Auto_Expire_Service SHALL update the local Checkout_Session status to match and continue without logging an error.
3. IF the Stripe_API is completely unreachable for all sessions, THEN THE Auto_Expire_Service SHALL log a warning and return control to the caller without throwing an exception.
4. THE Auto_Expire_Service SHALL execute asynchronously without blocking the payment recording operation's response to the user.

### Requirement 4: Database Cleanup After Expiration

**User Story:** As a developer, I want the local checkout session records to accurately reflect their Stripe-side status, so that reporting and debugging are reliable.

#### Acceptance Criteria

1. WHEN the Stripe_API confirms a session expiration, THE Auto_Expire_Service SHALL set the Checkout_Session `Status` column to `'expired'`.
2. WHEN the Stripe_API confirms a session expiration, THE Auto_Expire_Service SHALL set the Checkout_Session `CompletedAtUtc` column to the current UTC date and time.
3. IF the Stripe_API call fails for a session, THEN THE Auto_Expire_Service SHALL leave that Checkout_Session record unchanged (Status remains `'pending'`).

### Requirement 5: No Expire on Partial Payment

**User Story:** As a customer, I want to be able to pay the remaining balance by card after a partial payment has been recorded, so that I am not locked out of the payment link prematurely.

#### Acceptance Criteria

1. WHEN a payment is recorded that does not bring the outstanding balance to zero, THE Auto_Expire_Service SHALL NOT execute for that invoice.
2. WHILE the invoice outstanding balance remains greater than zero, THE Portal SHALL continue to display the "Pay by Card" button on the shared invoice page.
3. WHEN a partial payment is recorded, THE Financial_Status_Engine SHALL update the invoice status to Partially Paid (status 2) and THE Auto_Expire_Service SHALL NOT expire any Pending_Sessions.

### Requirement 6: Logging and Observability

**User Story:** As a developer, I want structured log entries for all auto-expire operations, so that I can monitor the feature and diagnose issues in production.

#### Acceptance Criteria

1. WHEN the Auto_Expire_Service begins processing for an invoice, THE Portal SHALL log an informational message containing the invoice ID and the count of Pending_Sessions found.
2. WHEN a session is successfully expired via Stripe_API, THE Portal SHALL log an informational message containing the StripeSessionId and invoice ID.
3. IF a session expiration fails, THEN THE Portal SHALL log a warning message containing the StripeSessionId, invoice ID, and error details.
4. WHEN all sessions for an invoice have been processed, THE Portal SHALL log a summary containing the invoice ID, total sessions processed, sessions successfully expired, and sessions that failed.
