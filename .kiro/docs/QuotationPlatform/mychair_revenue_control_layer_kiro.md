# MyChair Revenue Control Layer
## KIRO Implementation Specification

---

## 1. Purpose

This document defines the **Revenue Control Layer** that should follow the quotation-to-invoice workflow.

The purpose of this layer is to give MyChair controlled visibility over:

- issued invoices
- payment status
- outstanding balances
- due dates
- invoice settlement state
- reconciliation visibility

This is not accounting replacement.
This is an **operational financial control layer**.

---

## 2. Product Positioning

The Revenue Control Layer sits after:

```
Quotation → Invoice → Payment Visibility → Revenue Control
```

It exists to answer:

- Which invoices are still unpaid?
- Which customers are overdue?
- What amount is outstanding right now?
- Which invoices are partially paid?
- What revenue has been collected vs still open?

---

## 3. Scope

### In scope
- Invoice status tracking
- Payment recording
- Outstanding amount calculation
- Due-date tracking
- Paid / unpaid / overdue visibility
- Partial payment support
- Invoice-level revenue summaries

### Out of scope
- Full accounting ledger
- VAT return engine
- payroll
- bank reconciliation automation
- general ledger posting
- full ERP accounting replacement

---

## 4. Core Concepts

### 4.1 Invoice Financial Status

An invoice must have a financial status independent from its document status.

Example:

- Invoice document status = Issued
- Financial status = Partially Paid

These are not the same thing.

---

### 4.2 Payment Is an Entity

Do not store only a single `PaidAmount` on the invoice.

A payment must be its own record because:
- one invoice may have multiple payments
- payment history must be auditable
- reversals / corrections may be needed later

---

### 4.3 Deterministic Balance Calculation

Outstanding balance must be derived from:

```
InvoiceTotal - Sum(ValidPayments)
```

Never rely on manually maintained balance fields as the primary source of truth.

---

## 5. Data Model

### 5.1 Invoice

```
Invoice
-------
Id (PK)
InvoiceNumber
QuotationId (FK, nullable)
CustomerId
DocumentStatus (Draft / Issued / Cancelled)
FinancialStatus (Unpaid / PartiallyPaid / Paid / Overdue / WrittenOff)
InvoiceDate
DueDate
Subtotal
TaxAmount
TotalAmount
CurrencyCode
CreatedAtUtc
UpdatedAtUtc
```

---

### 5.2 InvoiceLine

```
InvoiceLine
-----------
Id (PK)
InvoiceId (FK)
Description
Quantity
UnitPrice
LineTotal
SortOrder
```

---

### 5.3 Payment

```
Payment
-------
Id (PK)
InvoiceId (FK)
PaymentDateUtc
Amount
PaymentMethodTypeId
Reference
Notes
IsVoided
CreatedAtUtc
CreatedByUserId
```

---

### 5.4 PaymentMethodType

```
PaymentMethodType
-----------------
Id (PK)
Name
IsActive
```

Suggested seeded values:
- Cash
- Bank Transfer
- Card
- Cheque
- Other

---

### 5.5 Optional Future Table: PaymentAllocation
Only needed if one payment can be allocated across multiple invoices.

Not required for phase 1.

---

## 6. Financial Status Rules

### 6.1 Unpaid
```
ValidPayments == 0
AND DueDate >= Today
```

### 6.2 Overdue
```
OutstandingAmount > 0
AND DueDate < Today
```

### 6.3 Partially Paid
```
ValidPayments > 0
AND OutstandingAmount > 0
```

### 6.4 Paid
```
OutstandingAmount <= 0
AND ValidPayments > 0
```

### 6.5 Written Off
Manual business action only.
Should not be inferred automatically.

---

## 7. Core Calculations

### 7.1 Total Paid
```
TotalPaid = Sum(Payment.Amount WHERE IsVoided = 0)
```

### 7.2 Outstanding Amount
```
OutstandingAmount = Invoice.TotalAmount - TotalPaid
```

### 7.3 Overpayment
Phase 1 recommendation:
- do not support overpayment silently
- reject payment if it exceeds remaining amount unless explicitly allowed

---

## 8. Workflow

### 8.1 Invoice Issue
Once invoice is issued:
- document status = Issued
- financial status defaults to Unpaid
- due date must be set

### 8.2 Payment Entry
When payment is entered:
- create Payment row
- recompute financial status
- recompute outstanding amount
- update invoice summary view

### 8.3 Voiding a Payment
If payment is voided:
- do not delete the record
- set `IsVoided = 1`
- recompute invoice financial state

---

## 9. UI / Screen Requirements

### 9.1 Revenue Dashboard
Must show:
- total outstanding
- overdue amount
- paid this month
- unpaid invoice count
- overdue invoice count

### 9.2 Invoice Detail Screen
Must show:
- invoice total
- total paid
- outstanding amount
- due date
- financial status
- payment history table
- add payment action

### 9.3 Add Payment Screen / Modal
Fields:
- invoice
- payment date
- amount
- payment method
- reference
- notes

### 9.4 Receivables List
Columns:
- invoice number
- customer
- invoice date
- due date
- total
- paid
- outstanding
- financial status

Filters:
- unpaid
- partially paid
- overdue
- paid

---

## 10. Revenue Dashboard Metrics

Recommended cards:

- Outstanding Receivables
- Overdue Receivables
- Paid This Month
- Invoices Awaiting Payment

Recommended tables:

- overdue invoices
- recently paid invoices
- largest outstanding balances

---

## 11. Conversion from Invoice Layer

This layer starts after invoice creation.

### Rule
Only issued invoices should accept payments.

Do not allow payments on:
- Draft invoices
- Cancelled invoices

---

## 12. Failure Modes

### 12.1 Duplicate Payment Entry
Mitigation:
- require reference where applicable
- show recent payments clearly
- support manual void, not delete

### 12.2 Silent Status Drift
Mitigation:
- financial status must be recalculated deterministically
- do not allow manual direct editing of financial status except WrittenOff

### 12.3 Race Conditions
If multiple users can record payments:
- use transaction scope
- reload invoice totals after insert
- validate remaining balance before commit

---

## 13. Suggested Application Services

### 13.1 `IInvoiceFinancialStatusService`
Responsibilities:
- compute total paid
- compute outstanding
- compute financial status

### 13.2 `IPaymentApplicationService`
Responsibilities:
- validate payment input
- create payment
- void payment
- trigger recalculation

### 13.3 `IReceivablesQueryService`
Responsibilities:
- dashboard summaries
- overdue list
- outstanding invoice list

---

## 14. Suggested Domain Rules

### Payment validation
- amount must be > 0
- payment date required
- invoice must be issued
- invoice must not be cancelled
- amount must not exceed remaining balance unless explicit overpayment policy exists

### Due-date validation
- due date should be >= invoice date
- missing due date should be rejected for issued invoices

---

## 15. KIRO Execution Steps

### Step 1
Create tables:
- Invoice
- InvoiceLine
- Payment
- PaymentMethodType

### Step 2
Add EF Core entities and mappings

### Step 3
Implement financial calculation service

### Step 4
Implement payment entry service

### Step 5
Create receivables queries and dashboard DTOs

### Step 6
Build UI screens:
- revenue dashboard
- invoice detail with payment history
- add payment
- receivables list

### Step 7
Test scenarios:
- unpaid
- partial payment
- full payment
- overdue
- voided payment
- duplicate entry attempt

---

## 16. Suggested Phase 1 UI Pack

Create these screens:

1. Revenue Control Dashboard
2. Receivables List
3. Invoice Detail with Payment History
4. Add Payment Modal / Screen

---

## 17. Future Extensions

Not phase 1, but prepare for:
- credit notes
- partial allocations across invoices
- customer statement
- reminder workflow
- ERP export
- bank feed integration
- write-off approval flow

---

## 18. Final Engineering Note

This layer must remain:

- deterministic
- auditable
- operationally clear
- isolated from full accounting complexity

The purpose is not to become an accounting package.
The purpose is to give the business **revenue control visibility**.

---

## End