# Bulk Discount — Manual Testing Scenarios

## Prerequisites

- Run migration `171_AddIsAdjustmentLineToInvoiceAndQuotationLine.sql` against the database
- Have at least one customer with a draft invoice containing 3+ line items (with varying per-line discounts)
- Have at least one draft quotation with 3+ line items (including at least one in a Subscription section)

---

## Scenario 1: Apply Percentage Discount to Invoice

**Steps:**
1. Navigate to `/Invoice/Edit/{id}` for a draft invoice with line items
2. Confirm the "Bulk Discount" button is visible in the topbar
3. Click "Bulk Discount"
4. Verify the modal opens with "Percentage" selected by default
5. Enter `15` in the discount value field
6. Verify the preview shows the calculated amount (e.g. "-€150.00" for a €1,000 subtotal)
7. Click "Apply Discount"
8. Verify SweetAlert2 success notification appears
9. Verify the totals breakdown now shows:
   - Gross Subtotal (sum of Qty × UnitPrice)
   - Line Discounts (if per-line discounts exist) in green
   - Net Subtotal
   - Invoice Discount (15%) in green with "Remove" button
   - Net Amount
   - VAT
   - Total
10. Verify a non-editable green row appears below line items: "Invoice Discount (15%) — System-managed discount"

**Expected:** Discount applied, totals updated in DOM without page reload, adjustment line visible.

---

## Scenario 2: Apply Fixed Amount Discount to Invoice

**Steps:**
1. On the same draft invoice, click "Bulk Discount" again
2. Switch to "Fixed Amount" tab
3. Enter `3.47` (to zero out decimals)
4. Verify the preview shows "-€3.47"
5. Click "Apply Discount"
6. Verify the previous 15% discount is replaced (not stacked)
7. Verify the Invoice Discount row now shows "-3.47" with description "Invoice Discount (-€3.47)"

**Expected:** Previous discount replaced atomically. Only one adjustment line exists.

---

## Scenario 3: Validation — Percentage Out of Range

**Steps:**
1. Click "Bulk Discount", stay on Percentage mode
2. Enter `0` → verify validation message, confirm button disabled
3. Enter `101` → verify validation message, confirm button disabled
4. Enter `-5` → verify validation message, confirm button disabled
5. Enter `50.123` → verify server rejects (if client allows, server returns error)

**Expected:** Invalid values are rejected client-side with validation messages. Confirm button stays disabled.

---

## Scenario 4: Validation — Fixed Amount Exceeds Net

**Steps:**
1. Note the current Net Subtotal (e.g. €1,000.00)
2. Click "Bulk Discount", switch to "Fixed Amount"
3. Enter an amount larger than the Net Subtotal (e.g. `1500.00`)
4. Verify validation message: "Amount cannot exceed the net amount (€1,000.00)"
5. Verify confirm button is disabled

**Expected:** Cannot apply a fixed discount larger than the available net amount.

---

## Scenario 5: Remove Bulk Discount

**Steps:**
1. With an active bulk discount on the invoice, locate the "Remove" button in the totals breakdown
2. Click "Remove"
3. Verify SweetAlert2 confirmation dialog appears ("Remove Discount? This will remove the invoice-level discount.")
4. Click "Yes, remove"
5. Verify SweetAlert2 success notification
6. Verify the Invoice Discount row disappears from the totals
7. Verify the adjustment line row below line items disappears
8. Verify the Total recalculates correctly

**Expected:** Discount removed, totals updated, no page reload needed.

---

## Scenario 6: Auto-Recalculation on Line Change (Percentage)

**Steps:**
1. Apply a 10% bulk discount to a draft invoice with subtotal €1,000 → adjustment = -€100.00
2. Add a new line item worth €500
3. Verify the adjustment line automatically recalculates to -€150.00 (10% of new €1,500 subtotal)
4. Edit an existing line (change quantity) → verify discount recalculates
5. Remove a line → verify discount recalculates

**Expected:** Percentage discount stays at the configured percentage regardless of line changes. The absolute amount adjusts automatically.

---

## Scenario 7: Fixed Discount Immutability on Line Change

**Steps:**
1. Apply a fixed €50.00 discount
2. Add a new line item
3. Verify the discount remains exactly -€50.00 (not recalculated)
4. Remove a line item
5. Verify the discount remains -€50.00

**Expected:** Fixed discounts never change when lines are modified.

---

## Scenario 8: Draft Status Guard

**Steps:**
1. Apply a bulk discount to a draft invoice
2. Issue the invoice (change status from Draft to Issued)
3. Navigate back to the invoice edit page (should redirect to Detail)
4. Verify the "Bulk Discount" button is NOT visible
5. Try calling the API directly: `POST /Invoice/AxPostApplyBulkDiscount?invoiceId=X&discountType=Percentage&discountValue=10`
6. Verify the API returns `{ success: false, message: "Invoice can only be edited in Draft status" }`

**Expected:** Non-draft invoices cannot have discounts applied or removed via UI or API.

---

## Scenario 9: Adjustment Line Cannot Be Edited via Standard Line Modal

**Steps:**
1. With an active bulk discount, try to click the adjustment line row
2. Verify no edit modal opens
3. Try calling the API: `POST /Invoice/AxPostUpdateLine?lineId={adjustmentLineId}&description=Hack&quantity=1&unitPrice=100&vatRate=0&discount=0&discountType=Percentage`
4. Verify the API returns error: "Adjustment lines cannot be modified through the line item editing flow."

**Expected:** Standard CRUD endpoints reject operations on adjustment lines.

---

## Scenario 10: Quotation Bulk Discount

**Steps:**
1. Navigate to `/Quotation/Edit/{id}` for a draft quotation
2. Click "Bulk Discount" → apply 20% discount
3. Verify totals breakdown appears correctly
4. Verify adjustment line shows "Quotation Discount (20%)"
5. If quotation has subscription lines, verify the discount is computed on the annualized subtotal (×12)

**Expected:** Same behavior as invoices, with "Quotation Discount" wording and subscription annualization.

---

## Scenario 11: Quotation-to-Invoice Conversion Carry-Over

**Steps:**
1. Create a draft quotation with line items and apply a 15% bulk discount
2. Note the quotation's total amount
3. Convert the quotation to an invoice (Accept → Convert)
4. Navigate to the new invoice's Edit page
5. Verify the adjustment line exists on the invoice: "Invoice Discount (15%)"
6. Verify the invoice total matches the quotation total (or very close, accounting for any reverse charge VAT adjustments)

**Expected:** Bulk discount carries over from quotation to invoice automatically.

---

## Scenario 12: Invoice Duplication

**Steps:**
1. Create a draft invoice with a 10% bulk discount
2. Duplicate the invoice
3. Navigate to the duplicated invoice's Edit page
4. Verify the adjustment line exists with the same percentage
5. Verify the totals are correct

**Expected:** Duplicated invoice retains the bulk discount.

---

## Scenario 13: PDF Rendering

**Steps:**
1. Create an invoice with both per-line discounts and a bulk 15% discount
2. View the invoice PDF (Snapshot view)
3. Verify:
   - The adjustment line does NOT appear in the line items table
   - The grand totals section shows: Subtotal, Line Discounts (green), Invoice Discount (green with description), Tax, Total
   - The negative amounts render as `-€X.XX` (not `€-X.XX`)
4. Repeat for a quotation PDF

**Expected:** PDF shows a clean totals breakdown with the discount clearly labelled.

---

## Scenario 14: Reopening Modal After Apply (Stale Context Check)

**Steps:**
1. Apply a 10% discount (subtotal drops)
2. Without refreshing the page, click "Bulk Discount" again
3. Switch to "Fixed Amount"
4. Verify the max allowed value in the validation reflects the NEW net subtotal (not the original)
5. Enter a value close to the new max → verify preview is correct

**Expected:** Modal uses fresh values after each apply/remove, not stale page-load values.

---

## Scenario 15: Zero Subtotal Guard

**Steps:**
1. Create a draft invoice with zero line items (or all lines with €0 unit price)
2. Click "Bulk Discount" → try to apply a percentage
3. Verify server returns: "Cannot apply percentage discount to an invoice with zero subtotal"

**Expected:** Percentage discount rejected when subtotal is zero.

---

## Edge Cases

| Scenario | Expected Behavior |
|----------|-------------------|
| Apply 100% discount | Allowed — net amount becomes €0 |
| Apply fixed = exact net amount | Allowed — net amount becomes €0, total = just VAT |
| Only one line item with 100% per-line discount → apply bulk | Rejected (zero subtotal) |
| Rapid double-click on "Apply Discount" | BlockUI prevents double submission |
| Network failure during apply | BlockUI hides, SweetAlert2 error shown, no partial state |
| All lines have same VAT rate + bulk discount | VAT computed correctly on pre-discount amounts |

---

## Audit Log Verification

After testing, check the audit log (Admin panel or database query):

```sql
SELECT * FROM [dbo].[AuditLog]
WHERE TableName IN ('Invoice', 'Quotation')
  AND Action IN ('BulkDiscountApplied', 'BulkDiscountReplaced', 'BulkDiscountRemoved')
ORDER BY Timestamp DESC;
```

Verify each operation logged the correct:
- InvoiceId/QuotationId
- Discount type and value
- Old values (for replace/remove)
- User who performed the action
- UTC timestamp
