# Implementation Plan: Universal Void Payment

## Overview

Adds void payment capability to 3 UI locations (Revenue Dashboard, Invoice Detail, Statement) and introduces individual child allocation voiding with credit return. Backend already has most logic — this is primarily UI + one new service method.

## Tasks

- [x] 1. Backend: Individual child void with credit return
  - [x] 1.1 Add `VoidChildAllocationAsync` method to PaymentService
    - Validate: payment exists, belongs to business, is a child (ParentPaymentId != null), not already voided
    - Set IsVoided = 1 on the child
    - Add child.Amount back to parent's CreditAmount
    - Recalculate invoice financial status
    - Revert instalment schedule match
    - Return ServiceResult with void details
  - [x] 1.2 Add `AxPostVoidChildAllocation` endpoint to RevenueController
    - [HttpPost][ValidateAntiForgeryToken]
    - Accepts paymentId
    - Calls VoidChildAllocationAsync
    - Returns Json with success, message, returnedCredit amount
  - [x] 1.3 Update `AxPostVoidPayment` to detect payment type and route correctly
    - If payment is a parent (InvoiceId = NULL, has children) → call VoidGlobalPaymentAsync
    - If payment is a child (ParentPaymentId != NULL) → call VoidChildAllocationAsync
    - If payment is standalone (InvoiceId set, ParentPaymentId = NULL) → call existing VoidPaymentAsync
    - Single endpoint that handles all 3 cases based on payment type

- [x] 2. Checkpoint — Verify backend compiles

- [x] 3. UI: Revenue Dashboard — Recent Payments void button
  - [x] 3.1 Add "Void" button to each non-voided payment row in the Recent Payments table
  - [x] 3.2 Show "Voided" badge (red pill, strikethrough row) for voided payments
  - [x] 3.3 Wire SweetAlert2 confirmation + AJAX call to void endpoint
  - [x] 3.4 Refresh table after successful void

- [x] 4. UI: Invoice Detail — Payment History void button
  - [x] 4.1 Update payment history rendering to show Void button per non-voided row
  - [x] 4.2 For child allocations, confirmation says "Reverse allocation — returns to credit"
  - [x] 4.3 For standalone payments, confirmation says "Void payment — invoice recalculated"
  - [x] 4.4 Wire AJAX call + refresh payment history section

- [x] 5. UI: Statement — Transaction History void button
  - [x] 5.1 Add "Void" link/button to each payment line in the transaction table
  - [x] 5.2 Style voided lines with opacity + strikethrough + "Voided" badge
  - [x] 5.3 Wire SweetAlert2 confirmation (varies by type: parent/child/standalone)
  - [x] 5.4 Refresh statement after successful void

- [x] 6. Checkpoint — Full build and test

## Notes

- The existing `VoidPayment` endpoint on Revenue/InvoiceDetail already works for standalone payments
- The existing `VoidGlobalPayment` endpoint handles parent cascade
- New: `VoidChildAllocation` returns amount to parent credit — this is the only new backend logic
- A single smart endpoint (Task 1.3) that detects payment type simplifies the frontend — one URL to call regardless of payment type
- Statement void needs the payment ID — currently not exposed in the transaction table. Need to include payment.Id in the statement line data.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "4.1", "4.2", "4.3", "4.4", "5.1", "5.2", "5.3", "5.4"] },
    { "id": 3, "tasks": ["6"] }
  ]
}
```
