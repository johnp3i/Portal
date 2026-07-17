# Payment Receipts & Signature Management — Testing Scenarios

## Prerequisites

1. Run migrations `121_CreateSignatureTable.sql`, `122_CreatePaymentReceiptTables.sql`, and `123_AddIsAutoReceiptEnabledToBusiness.sql` against your Portal database
2. Ensure you have a customer with at least 2 issued (unpaid) invoices
3. Log in as a business owner with Revenue module access
4. Prepare a PNG or SVG signature image file (transparent background, under 2 MB)

---

## Scenario 1: Upload a Digital Signature

1. Navigate to **Signature → Index** (or `/Signature`)
2. **Expected:** Page shows "Signature Library" with upload form and empty gallery
3. Enter Label = "John Smith — Director"
4. Select a PNG signature file (< 2 MB)
5. Click **"Upload"**
6. **Expected:** BlockUI → SweetAlert2 success → signature appears in gallery with label and preview image
7. Click **"Set Default"** on the uploaded signature
8. **Expected:** Signature now shows "Default" green badge

### Edge Cases:
- Try uploading a `.jpg` file → **Expected:** Error "Only PNG and SVG files are allowed for signatures."
- Try uploading a file > 2 MB → **Expected:** Error "File size must not exceed 2 MB."
- Try uploading without a label → **Expected:** Error "Signature label is required."

---

## Scenario 2: Generate Receipt from Per-Invoice Payment

1. Navigate to **Finance → Revenue** dashboard
2. Ensure a customer has an outstanding invoice (e.g., INV-1-00015 for €500)
3. Record a per-invoice payment of €500 (full payment) via Invoice Detail
4. After payment is recorded, navigate to **Finance → Receipts** (`/Receipt`)
5. **Expected:** If auto-receipt is OFF, no receipt appears yet
6. Go back to Invoice Detail → Payment History → find the payment row
7. Click **"Generate Receipt"** (if button exists) or use `/Receipt/AxPostGenerate` with the payment ID
8. **Expected:** BlockUI → success → receipt created with number REC-1-00001
9. Navigate to **Finance → Receipts**
10. **Expected:** Table shows the new receipt:
    - Receipt #: REC-1-00001
    - Customer: (customer name)
    - Amount: €500.00
    - Status: Active (green pill)
11. Click **"View"** → Receipt Detail page opens
12. **Expected:** 
    - Business header with logo, name, address
    - Customer name and address
    - Payment line: INV-1-00015, Invoice Total €500, Amount €500, Outstanding After €0
    - Status: "Payment in Full"
    - Signature image (if default was set)
    - Payment method and reference shown

---

## Scenario 3: Generate Receipt from Global Payment (Multi-Invoice)

1. Navigate to **Finance → Statement**
2. Select a customer with 2 outstanding invoices (e.g., INV-001 = €300, INV-002 = €200)
3. Record a global payment of €500 (FIFO mode — covers both invoices fully)
4. Navigate to **Finance → Receipts**
5. If auto-receipt is OFF, generate manually via `/Receipt/AxPostGenerate` with the parent payment ID
6. **Expected:** Receipt created with:
    - Receipt #: REC-1-00002
    - Total: €500.00
    - 2 line items:
      - INV-001: Invoice Total €300, Amount €300, Outstanding After €0 ("Paid in Full")
      - INV-002: Invoice Total €200, Amount €200, Outstanding After €0 ("Paid in Full")
    - Payment type: "Multi-Invoice Payment"

---

## Scenario 4: Generate Receipt from Overpayment (Credit)

1. Select a customer with 1 outstanding invoice (€150)
2. Record a global payment of €250 → €150 allocated, €100 credit
3. Generate receipt for the parent payment
4. **Expected:** Receipt shows:
    - Line: INV-XXX, Amount €150, Outstanding After €0
    - Total Received: €250.00
    - Credit note: "Credit held on account: €100.00"

---

## Scenario 5: Auto-Receipt Generation

1. Navigate to **My Business → Settings** (or directly update the database: `UPDATE [portal].[Business] SET IsAutoReceiptEnabled = 1 WHERE Id = {your-business-id}`)
2. Record a per-invoice payment of €200 against any outstanding invoice
3. **Expected:** Receipt auto-generated immediately — visible in Finance → Receipts without manual trigger
4. The receipt uses the default signature (if one was set in Scenario 1)
5. Verify receipt number incremented correctly (e.g., REC-1-00003)

---

## Scenario 6: Void a Receipt

1. Navigate to **Finance → Receipts**
2. Find an active receipt in the list
3. Click **"Void"** button
4. **Expected:** SweetAlert2 confirmation dialog: "This receipt will be marked as voided. Share links will be deactivated."
5. Confirm
6. **Expected:** BlockUI → success → receipt row now shows:
    - Strikethrough/muted styling
    - Status: "Voided" (red pill)
    - Actions: dash (no void button)
7. Click into the voided receipt detail
8. **Expected:** Red banner at top: "This receipt has been voided."

---

## Scenario 7: Void Payment Cascades to Receipt

1. Ensure a payment has a receipt generated (from Scenario 2 or 5)
2. Navigate to the payment location (Revenue Dashboard, Invoice Detail, or Statement)
3. Void the payment using the "Void" button
4. Navigate to **Finance → Receipts**
5. **Expected:** The receipt associated with that payment is now voided (status changed to Voided)
6. If the receipt had a share link, it should now show "This receipt has been voided" to the public viewer

---

## Scenario 8: Duplicate Receipt Prevention

1. Take a payment that already has a receipt generated
2. Attempt to generate another receipt for the same payment (via API: POST `/Receipt/AxPostGenerate` with the same paymentId)
3. **Expected:** Error: "A receipt has already been generated for this payment."

---

## Scenario 9: Receipt for Voided Payment

1. Void a payment first (without receipt)
2. Attempt to generate a receipt for the voided payment
3. **Expected:** Error: "Cannot generate a receipt for a voided payment."

---

## Scenario 10: Signature Deactivation

1. Navigate to **Signature → Index**
2. Upload a second signature (e.g., "Jane Doe — Finance")
3. Set the new signature as default
4. **Expected:** Old signature loses "Default" badge, new one gains it
5. Deactivate the old signature
6. **Expected:** SweetAlert2 confirmation → old signature disappears from gallery (or shows as deactivated)
7. Generate a new receipt
8. **Expected:** The receipt uses the new default signature (Jane Doe), not the deactivated one

---

## Scenario 11: Receipt List Filtering

1. Navigate to **Finance → Receipts** with several receipts (mix of active and voided)
2. Filter by Status = "Active"
3. **Expected:** Only active receipts shown, count updates
4. Filter by Status = "Voided"
5. **Expected:** Only voided receipts shown
6. Filter by Date Range (e.g., today)
7. **Expected:** Only receipts from today shown
8. Clear filters
9. **Expected:** All receipts shown again

---

## Scenario 12: Receipt Number Sequencing

1. Generate multiple receipts in sequence
2. **Expected:** Numbers increment: REC-1-00001, REC-1-00002, REC-1-00003, etc.
3. Void a receipt (e.g., REC-1-00002)
4. Generate another receipt
5. **Expected:** Next number is REC-1-00004 (no gaps reused — voided numbers are not recycled)

---

## Verification Checklist

| # | Test | Expected Result | Pass? |
|---|------|----------------|-------|
| 1 | Upload PNG signature | Success, appears in gallery | ☐ |
| 2 | Upload invalid file type | Error message, no upload | ☐ |
| 3 | Set default signature | Green badge appears | ☐ |
| 4 | Per-invoice receipt | Single line, correct amounts | ☐ |
| 5 | Global payment receipt | Multiple lines, all invoices listed | ☐ |
| 6 | Overpayment receipt | Credit note displayed | ☐ |
| 7 | Auto-receipt (enabled) | Receipt auto-created on payment | ☐ |
| 8 | Auto-receipt (disabled) | No receipt created automatically | ☐ |
| 9 | Void receipt manually | Status changes, red banner | ☐ |
| 10 | Void payment → receipt cascade | Receipt auto-voided | ☐ |
| 11 | Duplicate prevention | Error on second generate | ☐ |
| 12 | Voided payment → no receipt | Error on generate attempt | ☐ |
| 13 | Receipt number sequencing | Correct increment, no gaps reused | ☐ |
| 14 | Receipt list filtering | Filters work correctly | ☐ |
| 15 | Signature deactivation | Disappears from selection | ☐ |
| 16 | Receipts nav item visible | Appears under Finance section | ☐ |

---

## Database Verification Queries

```sql
-- Check all receipts for a business
SELECT * FROM [revenue].[PaymentReceipt] WHERE BusinessId = 1 ORDER BY Id DESC;

-- Check receipt lines
SELECT PaymentReceiptLine.* 
FROM [revenue].[PaymentReceiptLine]
INNER JOIN [revenue].[PaymentReceipt] ON PaymentReceiptLine.PaymentReceiptId = PaymentReceipt.Id
WHERE PaymentReceipt.BusinessId = 1;

-- Check signatures
SELECT * FROM [portal].[Signature] WHERE BusinessId = 1;

-- Check auto-receipt setting
SELECT Id, Name, IsAutoReceiptEnabled FROM [portal].[Business] WHERE Id = 1;

-- Verify void cascade: payment voided → receipt voided
SELECT PaymentReceipt.Id, PaymentReceipt.ReceiptNumber, PaymentReceipt.IsVoided, Payment.IsVoided AS PaymentIsVoided
FROM [revenue].[PaymentReceipt]
INNER JOIN [revenue].[Payment] ON PaymentReceipt.PaymentId = Payment.Id
WHERE PaymentReceipt.BusinessId = 1;
```
