# Admin Flow: Recording Manual Payments

**Date:** 28 August 2026
**Module:** Subscription Management (`/Admin/Subscriptions`)
**Actor:** SuperAdmin
**Related spec:** `.kiro/specs/manual-payment-recording/`
**Related mockups:** `.kiro/docs/mockups/manual-payment-recording-mockup.html`, `manual-payment-instalment-flow-mockup.html`

---

## Scenario A: Full Payment — Bank Transfer

**Context:** WT Miami AGL Limited wants to subscribe. They'll pay via bank transfer.

### Prerequisites

1. The admin already created a promo code. The customer registered and is now on a Trial subscription with Professional plan.
2. The customer sends a bank transfer for €890.00 (Professional annual). The admin sees the money in the bank.

### Steps

3. Admin goes to `/Admin/Subscriptions`. They see WT Miami with status "Trial".

4. Admin clicks **"Record Payment"** on the WT Miami row.

5. The Record Payment modal opens. It shows:
   - "WT Miami AGL Limited — Professional plan (€890.00/year)"
   - Invoice Amount: pre-filled with €890.00
   - Payment Amount: pre-filled with €890.00
   - Method: admin selects "Bank Transfer"
   - Reference: admin types the bank transfer reference (e.g., "TRF-2026-0055")
   - Period Start: defaults to today (28 Aug 2026)
   - Period End: defaults to +1 year (28 Aug 2027)
   - Notes: admin types "Annual Professional subscription — paid via bank transfer"

6. Admin clicks **"Record Payment"**. A confirmation dialog appears showing the summary. Admin clicks **"Confirm & Record"**.

7. The system (in one transaction):
   - Generates invoice number BILI-INV-2026-0004
   - Creates the billing invoice (€890, paid, period Aug 2026–Aug 2027)
   - Creates the billing payment (€890, bank_transfer, reference, notes, recorded by admin)
   - Updates the subscription: status → "active", period → Aug 2026–Aug 2027
   - Updates the BusinessPlan: status → "active"

8. Success dialog shows: "Payment Recorded — Invoice BILI-INV-2026-0004. Subscription activated until 28 Aug 2027."

9. The subscriptions table reloads. WT Miami now shows status **"Active"** instead of "Trial", with expiry date 28 Aug 2027.

### Customer Experience

10. The customer logs into the portal, goes to `/Account/Billing`, and sees invoice BILI-INV-2026-0004. They click **"Download"** and get a PDF invoice showing 3 Inventors as issuer, WT Miami as subscriber, the Professional plan line item, and payment info (Bank Transfer, €890, 28 Aug 2026).

---

## Scenario B: Instalment Payment — 2 Bank Transfers

**Context:** Same customer, next year. They want to renew but pay in 2 × €445.

### First Instalment

11. Admin clicks **"Record Payment"** on WT Miami.

12. Admin sets Invoice Amount = €890, Payment Amount = €445. Fills in the new period (Aug 2027–Aug 2028), method (Bank Transfer), reference.

13. After confirm, the system creates invoice BILI-INV-2027-0012 with status **"Partially Paid"**. Subscription period updated to the new year. First payment of €445 recorded.

14. The subscriptions table still shows "Active" (subscription was activated with the first payment). Admin clicks **"History"** on WT Miami and sees the new invoice with "Partially Paid" badge, showing €445 paid, €445 outstanding, and an **"Add Payment"** button.

### Second Instalment (3 months later)

15. The second bank transfer arrives. Admin clicks **"History"** → **"Add Payment"** on the partially-paid invoice.

16. The Add Payment modal shows: "BILI-INV-2027-0012 — Due: €890 | Paid: €445 | Remaining: €445". Payment Amount defaults to €445.

17. Admin fills in the reference and clicks **"Add Payment"** → confirmation → the invoice flips to **"Paid"**. PaidAtUtc is set to now.

18. The customer's billing page now shows both the original and the new invoice. The new invoice PDF shows both payments in a "Payment History" section at the bottom.

---

## Key Behaviours

| Behaviour | Detail |
|-----------|--------|
| Invoice Amount vs Payment Amount | Invoice Amount = total due for the period. Payment Amount = what's being paid now. For full payment, they're equal. For instalments, Payment Amount < Invoice Amount. |
| Invoice status | `paid` when Payment Amount = Invoice Amount. `partially_paid` when Payment Amount < Invoice Amount. Transitions to `paid` when subsequent instalments complete the total. |
| Subscription activation | Happens on the first payment (whether full or partial). The subscription period covers the full year from day one — even if only the first instalment has been paid. |
| Instalment payments | Don't change the subscription period. They only add payment rows to the existing invoice and update the invoice status when fully settled. |
| Invoice numbering | Continues the same sequence as Stripe invoices. Format: `BILI-INV-{yyyy}-{NNNN}`. |
| PDF download | Business owner downloads from `/Account/Billing`. Admin downloads from the Payment History modal (admin-specific endpoint that bypasses tenant scoping). Same PDF template for both. |
| Confirmation | Every payment (first or instalment) requires a SweetAlert2 confirmation dialog before submission. Shows business name, amount, method, period, and the resulting invoice status. |
