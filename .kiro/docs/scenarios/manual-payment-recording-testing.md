# Testing Scenarios: Manual Payment Recording

## Prerequisites

- Run migration `181_AddManualPaymentColumnsToPayment.sql` against the Portal database
- Run migration `182_ExpandBillingInvoiceStatusConstraint.sql` against the Portal database
- Have at least two businesses with active subscriptions (one on Trial, one on Active)
- Log in as a SuperAdmin user
- Note the last invoice number in `[billing].[Invoice]` for sequence verification

---

## Scenario 1: Full Payment — Bank Transfer (Trial → Active)

1. Navigate to `/Admin/Subscriptions`
2. Find a business with status "Trial" (e.g., WT Miami AGL Limited)
3. Click **"Record Payment"** on that row
4. **Expected:** Modal opens showing business name + plan name + annual price
5. **Expected:** Invoice Amount pre-filled with the plan's annual price (e.g., €890.00)
6. **Expected:** Payment Amount defaults to the same value as Invoice Amount
7. **Expected:** Period Start defaults to today, Period End defaults to +1 year
8. Set Method to "Bank Transfer", type a reference (e.g., "TRF-2026-0055"), add a note
9. Click **"Record Payment"**
10. **Expected:** Confirmation dialog appears showing: business name, invoice total, payment amount, method, period, reference, and "Invoice will be marked as Paid."
11. Click **"Confirm & Record"**
12. **Expected:** Success dialog showing invoice number (e.g., BILI-INV-2026-0004) and activation date
13. **Expected:** Page reloads, business status changes from "Trial" to "Active", expiry date shows

---

## Scenario 2: Full Payment — Cheque

1. Find a different business with status "Trial"
2. Click **"Record Payment"**, change Method to "Cheque"
3. Enter a cheque number as reference (e.g., "CHQ-4521")
4. Submit and confirm
5. **Expected:** Payment recorded with Method = "cheque"
6. **Expected:** Invoice number continues the sequence (e.g., BILI-INV-2026-0005)

---

## Scenario 3: Partial Payment — First Instalment

1. Find a business (or use the same one from Scenario 1)
2. Click **"Record Payment"**
3. Set Invoice Amount = 1290.00 (Enterprise annual)
4. Set Payment Amount = 430.00 (first instalment)
5. **Expected:** Payment Amount < Invoice Amount is allowed
6. Fill in method (Bank Transfer), reference, period, notes
7. Click **"Record Payment"**
8. **Expected:** Confirmation dialog shows "Invoice will be marked as Partially Paid." in amber
9. Confirm
10. **Expected:** Success dialog with invoice number
11. **Expected:** Page reloads, business status shows "Active" (subscription activated with first payment)

---

## Scenario 4: Payment History — View Invoices and Payments

1. Click **"History"** on the business from Scenario 3
2. **Expected:** Payment History modal opens with:
   - Revenue summary at top: "Total Revenue: €430.00 | Invoices: 1 | Outstanding: €860.00"
   - One invoice row showing: invoice number, "Due: €1,290.00 — Paid: €430.00", "Outstanding: €860.00"
   - Amber "Partially Paid" badge
   - An **"+ Add Payment"** button
   - A **"Download"** link
3. Below the invoice, one nested payment row showing: €430.00, Bank Transfer badge, date, reference
4. Click **"Download"**
5. **Expected:** PDF opens in a new tab showing the invoice with "Partially Paid" status badge and the payment breakdown

---

## Scenario 5: Add Payment — Second Instalment

1. From the Payment History modal (Scenario 4), click **"+ Add Payment"** on the partially-paid invoice
2. **Expected:** Add Payment modal opens showing:
   - Invoice number in the header
   - Balance summary: "Total Due: €1,290.00 | Paid: €430.00 | Remaining: €860.00"
   - Payment Amount defaults to €860.00 (remaining balance)
   - Hint text: "Max: €860.00 (remaining)"
3. Change Payment Amount to €430.00 (second instalment of 3)
4. Set Method, Reference ("TRF-2026-0098"), Notes ("Second instalment")
5. Click **"Add Payment"**
6. **Expected:** Confirmation dialog shows "Invoice will remain Partially Paid." in amber
7. Confirm
8. **Expected:** Success message, Payment History modal re-opens automatically
9. **Expected:** Invoice now shows: "Paid: €860.00", "Outstanding: €430.00", still "Partially Paid"
10. **Expected:** Two nested payment rows visible

---

## Scenario 6: Add Payment — Final Instalment (Completes Invoice)

1. From the Payment History modal, click **"+ Add Payment"** on the same invoice
2. **Expected:** Payment Amount defaults to €430.00 (remaining balance)
3. Leave amount as €430.00, fill in reference and notes
4. Click **"Add Payment"**
5. **Expected:** Confirmation dialog shows "This will complete the invoice. Status → Paid." in green
6. Confirm
7. **Expected:** Success message: "Invoice is now fully paid"
8. **Expected:** Payment History re-opens, invoice now shows:
   - Green "Paid" badge (no longer amber)
   - "€1,290.00" with no outstanding balance
   - No "Add Payment" button (fully paid)
   - Three nested payment rows visible
   - "Download" link still present
9. Click **"Download"** on the now-paid invoice
10. **Expected:** PDF shows all 3 payments in a "Payment History (3 payments)" section with a green "PAID" badge

---

## Scenario 7: Payment History — Mixed Stripe and Manual

1. Click **"History"** on the business that had Stripe payments before (e.g., 3 Inventors Limited)
2. **Expected:** Both Stripe and manual invoices appear in the list
3. **Expected:** Stripe payments show blue "Stripe" badge
4. **Expected:** Manual payments show green "Bank Transfer" / amber "Cheque" / grey "Cash" badges
5. **Expected:** Revenue summary totals include both Stripe and manual payments

---

## Scenario 8: Admin PDF Download

1. From the Payment History modal, click **"Download"** on any invoice
2. **Expected:** PDF opens in a new tab via `/Admin/Subscriptions/DownloadInvoice/{id}?businessId={bid}`
3. **Expected:** PDF shows correct 3 Inventors Ltd as issuer, customer as subscriber
4. **Expected:** Invoice number, period, line items, VAT, totals are correct
5. **Expected:** Payment info section shows the correct method (not "Stripe" for manual payments)

---

## Scenario 9: Customer Downloads Their Invoice

1. Log out as SuperAdmin
2. Log in as the business owner whose payment was recorded
3. Navigate to `/Account/Billing`
4. **Expected:** The manually-created invoice appears in the invoice list
5. Click **"Download"** on the invoice
6. **Expected:** PDF downloads via the existing `BillingController.DownloadInvoice` endpoint
7. **Expected:** PDF content matches what the admin saw (same template, same data)

---

## Scenario 10: Validation — Amount Exceeds Invoice Total

1. Open the Record Payment modal
2. Set Invoice Amount = 890.00, Payment Amount = 1000.00
3. Click **"Record Payment"**
4. **Expected:** SweetAlert2 warning: "Payment amount cannot exceed the invoice total."
5. **Expected:** No request is sent to the server

---

## Scenario 11: Validation — Instalment Exceeds Remaining Balance

1. Open the Add Payment modal on a partially-paid invoice with €430 remaining
2. Set Payment Amount = 500.00
3. Click **"Add Payment"**
4. **Expected:** SweetAlert2 warning: "Payment amount cannot exceed the remaining balance of €430.00."

---

## Scenario 12: Validation — Missing Required Fields

1. Open the Record Payment modal
2. Clear the Invoice Amount field, click "Record Payment"
3. **Expected:** SweetAlert2 warning about amount
4. Clear the Period Start date, try again
5. **Expected:** SweetAlert2 warning about dates
6. Set Period End before Period Start
7. **Expected:** SweetAlert2 warning: "Period end must be after period start."

---

## Scenario 13: Validation — No Subscription

1. If a business has no subscription record (BusinessPlanId = null)
2. **Expected:** "Record Payment" button is disabled for that row
3. If you force a request via devtools with a non-existent business
4. **Expected:** Server returns: "No subscription found for this business."

---

## Scenario 14: Backward Compatibility — Stripe Payments Unaffected

1. Verify existing Stripe payment records in the database:
   ```sql
   SELECT [Id], [Method], [Reference], [Notes], [RecordedByUserId]
   FROM [billing].[Payment]
   WHERE [Method] = 'stripe'
   ```
2. **Expected:** All Stripe records have Reference = NULL, Notes = NULL, RecordedByUserId = NULL
3. **Expected:** Existing invoice PDFs still generate correctly (no errors from new model fields)

---

## Scenario 15: Invoice Number Continuity

1. Before testing, note the last invoice number (e.g., BILI-INV-2026-0003)
2. Record a manual payment
3. **Expected:** New invoice number is BILI-INV-2026-0004 (next in sequence)
4. Trigger a Stripe payment (or check the webhook creates the next one)
5. **Expected:** Stripe invoice number is BILI-INV-2026-0005 (continues the same sequence — no gaps)

---

## Scenario 16: Subscription Period — Set Once, Not on Instalments

1. Record a partial payment with Period Start = 01 Sep 2026, Period End = 01 Sep 2027
2. Verify the subscription period in the database:
   ```sql
   SELECT [CurrentPeriodStart], [CurrentPeriodEnd], [Status]
   FROM [billing].[Subscription]
   WHERE [BusinessId] = @BusinessId
   ```
3. **Expected:** Period = Sep 2026 – Sep 2027, Status = "active"
4. Add an instalment payment
5. Re-check the subscription period
6. **Expected:** Period unchanged (still Sep 2026 – Sep 2027) — instalments don't modify it

---

## Database Verification Queries

After running through the scenarios, use these queries to verify data integrity:

```sql
-- Verify manual payment records have metadata
SELECT [billing].[Payment].[Id], [billing].[Payment].[InvoiceId],
       [billing].[Payment].[AmountEur], [billing].[Payment].[Method],
       [billing].[Payment].[Reference], [billing].[Payment].[Notes],
       [billing].[Payment].[RecordedByUserId], [billing].[Payment].[PaidAtUtc]
FROM [billing].[Payment]
WHERE [billing].[Payment].[Method] != 'stripe'
ORDER BY [billing].[Payment].[PaidAtUtc] DESC

-- Verify invoice status matches payment totals
SELECT [billing].[Invoice].[Id], [billing].[Invoice].[InvoiceNumber],
       [billing].[Invoice].[AmountEur] AS InvoiceTotal,
       [billing].[Invoice].[Status],
       ISNULL(PaymentSums.TotalPaid, 0) AS TotalPaid,
       [billing].[Invoice].[AmountEur] - ISNULL(PaymentSums.TotalPaid, 0) AS Outstanding
FROM [billing].[Invoice]
LEFT JOIN (
    SELECT [billing].[Payment].[InvoiceId], SUM([billing].[Payment].[AmountEur]) AS TotalPaid
    FROM [billing].[Payment]
    GROUP BY [billing].[Payment].[InvoiceId]
) PaymentSums ON [billing].[Invoice].[Id] = PaymentSums.InvoiceId
ORDER BY [billing].[Invoice].[CreatedAtUtc] DESC

-- Verify subscription period alignment
SELECT [billing].[Subscription].[BusinessId],
       [billing].[Subscription].[Status],
       [billing].[Subscription].[CurrentPeriodStart],
       [billing].[Subscription].[CurrentPeriodEnd]
FROM [billing].[Subscription]
ORDER BY [billing].[Subscription].[BusinessId]

-- Verify invoice number sequence has no gaps
SELECT [billing].[Invoice].[InvoiceNumber],
       [billing].[Invoice].[Status],
       [billing].[Invoice].[AmountEur],
       CASE WHEN [billing].[Invoice].[StripeInvoiceId] IS NOT NULL THEN 'Stripe' ELSE 'Manual' END AS Source
FROM [billing].[Invoice]
WHERE [billing].[Invoice].[InvoiceNumber] IS NOT NULL
ORDER BY [billing].[Invoice].[InvoiceNumber]
```
