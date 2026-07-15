# Global Payment Allocation — Testing Scenarios

## Prerequisites

1. Run migration `120_AddGlobalPaymentColumns.sql` against your Portal database
2. Ensure you have a customer with at least 3 issued (unpaid) invoices
3. Log in as a Professional plan user with Revenue module access

---

## Scenario 1: FIFO Allocation (Happy Path)

1. Navigate to **Finance → Statement**
2. Select a customer who has 3 outstanding invoices (e.g., INV-001 = €500, INV-002 = €300, INV-003 = €200)
3. Generate a statement (All Time or appropriate period)
4. Click **"Record Payment"** button
5. **Expected:** Modal opens with customer name and "Total outstanding: €1,000.00 across 3 invoice(s)"
6. Enter Amount = **€700**, Date = today, Method = Bank Transfer
7. Leave Allocation Mode as "Auto (FIFO)"
8. Click **"Record Payment"**
9. **Expected:** BlockUI → success message: "2 invoice(s) allocated. Total: €700.00"
10. Statement refreshes — verify:
    - INV-001 (oldest, €500): now fully paid
    - INV-002 (€300): now has €200 partial payment (€700 - €500 = €200 remaining allocated here)
    - INV-003 (€200): unchanged — payment exhausted before reaching it

---

## Scenario 2: Full Payment (All Invoices Settled)

1. Same customer, now with INV-002 outstanding €100 and INV-003 outstanding €200
2. Record Payment = **€300** with FIFO
3. **Expected:** "2 invoice(s) allocated. Total: €300.00"
4. Both invoices transition to Paid status
5. No credit balance

---

## Scenario 3: Overpayment (Credit Balance)

1. Select a customer with 1 outstanding invoice (€150)
2. Record Payment = **€200**
3. **Expected:** SweetAlert2 warning: "This payment exceeds the total outstanding by €50.00. The excess will be recorded as a credit."
4. Click "Proceed"
5. **Expected:** "1 invoice(s) allocated. Total: €150.00. Credit: €50.00"
6. The invoice is now Paid
7. The parent payment record has CreditAmount = 50.00

---

## Scenario 4: Manual Allocation

1. Select a customer with 3 outstanding invoices (€500, €300, €200)
2. Click "Record Payment", enter Amount = **€400**
3. Switch Allocation Mode to **"Manual (select invoices)"**
4. **Expected:** Invoice list appears showing all 3 invoices with outstanding balances
5. Enter €100 for INV-001, €300 for INV-002, leave INV-003 empty
6. Click "Record Payment"
7. **Expected:** "2 invoice(s) allocated. Total: €400.00"
8. INV-001: €100 partial payment applied
9. INV-002: fully paid (€300)
10. INV-003: unchanged

---

## Scenario 5: Manual Allocation with Remainder as Credit

1. Customer has 1 invoice outstanding €200
2. Record Payment = **€500**, switch to Manual
3. Allocate only €200 to the invoice
4. Click "Record Payment"
5. **Expected:** "1 invoice(s) allocated. Total: €200.00. Credit: €300.00"
6. Invoice is Paid, parent payment has CreditAmount = 300.00

---

## Scenario 6: Void Global Payment (Cascade)

1. After Scenario 1, go to Revenue → InvoiceDetail for INV-001
2. The payment history should show: "€500.00 | Auto-allocated from payment [reference]"
3. Navigate to Statement → the parent payment line should be visible
4. Void the parent payment (via the Revenue Dashboard or void button)
5. **Expected:** SweetAlert2 confirmation: "Voiding this payment will also reverse 2 allocation(s)..."
6. Confirm → all child allocations voided
7. INV-001 reverts to Unpaid/Overdue
8. INV-002 reverts (partial payment reversed)
9. CreditAmount on parent = 0

---

## Scenario 7: Validation — Amount Zero or Negative

1. Open the Record Payment modal
2. Enter Amount = **0** or **-50**
3. Click "Record Payment"
4. **Expected:** Error: "Amount must be greater than zero."

---

## Scenario 8: Validation — Future Date

1. Enter a payment date in the future (e.g., tomorrow)
2. Click "Record Payment"
3. **Expected:** Error: "Payment date cannot be in the future."

---

## Scenario 9: Customer With No Outstanding Invoices

1. Select a customer whose all invoices are Paid
2. Click "Record Payment"
3. **Expected:** Modal shows "Total outstanding: €0.00 across 0 invoice(s)"
4. Enter an amount and try to record
5. **Expected:** Warning asking to confirm recording as credit-only

---

## Scenario 10: Manual Allocation Exceeds Invoice Balance

1. Customer has an invoice with €200 outstanding
2. Switch to Manual mode, enter **€250** for that invoice
3. Click "Record Payment"
4. **Expected:** Error: "Amount exceeds outstanding balance for [InvoiceNumber]."

---

## Scenario 11: Per-Invoice Payment Still Works

1. Navigate to **Invoice Detail** for any issued invoice
2. Use the existing "Record Payment" button (from the Revenue/InvoiceDetail page)
3. Record a normal per-invoice payment
4. **Expected:** Works exactly as before — no ParentPaymentId, no global allocation, just a direct payment against the single invoice
5. Financial status updates correctly

---

## Scenario 12: Tenant Isolation

1. Log in as Business A, create a global payment for Customer X
2. Log in as Business B
3. Attempt to void the payment from Business A (via direct URL manipulation: `/Revenue/AxPostVoidGlobalPayment?paymentId=...`)
4. **Expected:** Error: "Payment not found." (business scoping prevents access)

---

## Quick Smoke Test (5 minutes)

1. Statement page → select a customer with 2+ outstanding invoices
2. Click "Record Payment" → modal opens with correct outstanding total
3. Enter an amount less than total outstanding → FIFO → success → statement refreshes
4. Verify the oldest invoice shows the payment in its history
5. Void the global payment → both allocations reversed → invoices revert
