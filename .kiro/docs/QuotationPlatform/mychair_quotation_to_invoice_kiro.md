
# MyChair Quotation → Invoice Conversion
## KIRO Implementation Specification

---

## 1. Purpose

This document defines the **Quotation-to-Invoice workflow** for the MyChair platform.

This is a **core commercial flow**, not a secondary feature.

Goal:
- Preserve commercial traceability
- Prevent data inconsistencies
- Ensure controlled financial execution

---

## 2. Core Principles

### 2.1 Single Source of Truth
A quotation is the **origin document**.
An invoice must:
- Reference exactly one quotation
- Never duplicate logic
- Never diverge silently

---

### 2.2 State-Driven Workflow

Quotation lifecycle:

```
Draft → Sent → Accepted → Converted → Archived
```

Rules:
- Only **Accepted** quotations can be converted
- Conversion is **idempotent**
- A quotation can produce **only one invoice**

---

### 2.3 Traceability

Invoice MUST contain:
- QuotationId
- QuotationReference
- Snapshot of line items at time of conversion

---

## 3. Data Model

### 3.1 Quotation

```
Quotation
---------
Id (PK)
Reference
CustomerId
Status (Draft/Sent/Accepted/Converted)
ValidUntil
TotalAmount
CreatedAt
UpdatedAt
```

### 3.2 QuotationLine

```
QuotationLine
-------------
Id (PK)
QuotationId (FK)
Description
Quantity
UnitPrice
LineTotal
```

---

### 3.3 Invoice

```
Invoice
-------
Id (PK)
InvoiceNumber
QuotationId (FK)
CustomerId
Status (Draft/Issued/Cancelled)
TotalAmount
CreatedAt
```

---

### 3.4 InvoiceLine

```
InvoiceLine
-----------
Id (PK)
InvoiceId (FK)
Description
Quantity
UnitPrice
LineTotal
```

---

## 4. Conversion Rules

### 4.1 Preconditions

Before conversion:

- Quotation.Status == Accepted
- Customer exists and is valid
- At least 1 line item exists

---

### 4.2 Conversion Algorithm

```
BEGIN TRANSACTION

1. Validate quotation status
2. Check if invoice already exists
3. Create Invoice record
4. Copy all QuotationLines → InvoiceLines
5. Set Invoice.TotalAmount
6. Update Quotation.Status = Converted

COMMIT
```

---

### 4.3 Idempotency Protection

```
IF Invoice exists WHERE QuotationId = X
    RETURN existing invoice
```

---

## 5. UI Behavior

### 5.1 Quotation Detail Screen

Show button:

```
IF Status == Accepted:
    Show "Convert to Invoice"
ELSE:
    Disabled with explanation
```

---

### 5.2 Conversion Screen

Sections:
- Source quotation summary
- Billing details
- Preview totals
- Confirm button

---

### 5.3 Invoice List

Must display:
- Source quotation reference
- Status
- Total

---

## 6. Failure Modes

### 6.1 Double Conversion

Prevent using:
- Unique constraint on Invoice.QuotationId

---

### 6.2 Data Drift

Prevent by:
- Copying line items (not referencing live data)

---

### 6.3 Partial Conversion

Prevent by:
- Wrapping conversion in transaction

---

## 7. Extension Points

Future upgrades:

- Partial invoicing
- Payment tracking
- Credit notes
- Integration with ERP

---

## 8. KIRO Execution Steps

### Step 1
Create database tables

### Step 2
Implement domain models

### Step 3
Build conversion service

### Step 4
Add validation layer

### Step 5
Connect UI buttons

### Step 6
Test edge cases

---

## 9. Final Notes

This flow is:

- Financially critical
- Must be deterministic
- Must be auditable

Do NOT:
- Allow manual inconsistencies
- Allow silent edits after conversion

---

End of document
