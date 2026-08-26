# Requirements Document

## Introduction

This feature adds a document-level bulk discount capability to both the invoicing and quotation modules. Currently, discounts can only be applied per individual line item. When a document has many line items and the user needs to apply a uniform discount (percentage or fixed amount) across the entire document, they must edit each line individually. The bulk discount feature introduces a special "adjustment line" that represents the document-level discount while preserving the existing per-line discount model and maintaining VAT compliance. The same mechanism applies identically to both quotations and invoices, ensuring a consistent discount carries through when a quotation is converted to an invoice.

## Glossary

- **Invoice**: A financial document representing an obligation to pay, stored in `[invoice].[Invoice]`.
- **Quotation**: A financial proposal document stored in `[quotation].[Quotation]`.
- **Invoice_Line**: An individual priced item within an invoice, stored in `[invoice].[InvoiceLine]`.
- **Quotation_Line**: An individual priced item within a quotation, stored in `[quotation].[QuotationLine]`.
- **Adjustment_Line**: A special Invoice_Line or Quotation_Line flagged with `IsAdjustmentLine = true` that represents a document-level bulk discount. It has a negative LineTotal and VatRate of 0.
- **Subtotal**: The sum of `Quantity × UnitPrice` for all normal (non-adjustment) lines before any discounts.
- **Line_Discounts**: The aggregate of per-line discounts already applied to individual lines.
- **Document_Discount**: The discount amount represented by the Adjustment_Line.
- **Net_Amount**: The result of Subtotal minus Line_Discounts minus Document_Discount.
- **Draft_Status**: Invoice status where `InvoiceStatusTypeId = 1`, or Quotation status where `QuotationStatusTypeId = 1`, the only statuses in which documents are editable.
- **Bulk_Discount_Modal**: The UI dialog opened via the "Bulk Discount" button where users configure the document-level discount.
- **InvoiceService**: The service responsible for all invoice line CRUD and total recomputation.
- **QuotationService**: The service responsible for all quotation line CRUD and total recomputation.
- **Audit_Log**: A record capturing the creation, modification, or removal of the Adjustment_Line for traceability.
- **Quotation_Conversion**: The process of converting a quotation into an invoice, copying all lines including adjustment lines.

## Requirements

### Requirement 1: Adjustment Line Creation via Percentage Discount

**User Story:** As an invoice editor, I want to apply a percentage-based bulk discount to an entire invoice, so that I can quickly discount all line items without editing each one individually.

#### Acceptance Criteria

1. WHEN the user submits a percentage discount value through the Bulk_Discount_Modal, THE InvoiceService SHALL create an Adjustment_Line with `LineTotal = -(Subtotal × percentage / 100)` rounded to 2 decimal places using standard half-up rounding.
2. THE Adjustment_Line SHALL have `IsAdjustmentLine = true`, `VatRate = 0`, `Quantity = 1`, and `DiscountType = "Percentage"`.
3. THE Adjustment_Line SHALL store the user-entered percentage value in the `Discount` field.
4. WHEN an Adjustment_Line already exists on the invoice, THE InvoiceService SHALL replace the existing Adjustment_Line with the new one rather than creating a second Adjustment_Line.
5. IF the user-entered percentage value is less than 0.01 or greater than 100, THEN THE InvoiceService SHALL reject the operation and return a validation error message indicating the percentage must be between 0.01 and 100 inclusive, accepting up to 2 decimal places of precision.
6. IF the invoice Subtotal is zero at the time of submission, THEN THE InvoiceService SHALL reject the operation and return a validation error message indicating a percentage discount cannot be applied to an invoice with a zero subtotal.

### Requirement 2: Adjustment Line Creation via Fixed Amount Discount

**User Story:** As an invoice editor, I want to apply a fixed amount discount to an entire invoice, so that I can subtract a specific monetary value (e.g. to zero-out decimals).

#### Acceptance Criteria

1. WHEN the user submits a fixed amount discount through the Bulk_Discount_Modal, THE InvoiceService SHALL create an Adjustment_Line with `LineTotal = -(fixed amount)` where the fixed amount is a positive value entered by the user, rounded to 2 decimal places.
2. THE Adjustment_Line SHALL have `IsAdjustmentLine = true`, `VatRate = 0`, `Quantity = 1`, and `DiscountType = "Fixed"`.
3. THE Adjustment_Line SHALL store the user-entered fixed amount in the `Discount` field.
4. IF the fixed amount exceeds the pre-adjustment Net_Amount (Subtotal minus Line_Discounts, excluding any existing Adjustment_Line), THEN THE InvoiceService SHALL reject the operation, preserve the existing invoice state unchanged, and return a validation error message indicating that the discount cannot exceed the available net amount.
5. IF the user enters a fixed amount that is less than 0.01, has more than 2 decimal places, or exceeds 999,999,999.99, THEN THE InvoiceService SHALL reject the operation and return a validation error message indicating the accepted value range.

### Requirement 3: Draft Status Constraint

**User Story:** As a system administrator, I want bulk discounts to only be applicable on draft invoices, so that finalized or sent invoices cannot be altered.

#### Acceptance Criteria

1. WHILE the invoice is in Draft_Status, THE Bulk_Discount_Modal SHALL be accessible via the "Bulk Discount" button in the invoice edit UI.
2. WHILE the invoice is not in Draft_Status, THE invoice edit UI SHALL hide the "Bulk Discount" button.
3. IF a bulk discount creation, replacement, or removal is attempted on a non-draft invoice via the API, THEN THE InvoiceService SHALL reject the operation and return an error indicating the invoice must be in Draft status.
4. IF the invoice status changes from Draft_Status to another status while an unsaved bulk discount operation is in progress, THEN THE InvoiceService SHALL reject the operation and return an error indicating the invoice must be in Draft status.

### Requirement 4: Single Adjustment Line Constraint

**User Story:** As an invoice editor, I want only one bulk discount active at a time per invoice, so that the discount calculation remains clear and predictable.

#### Acceptance Criteria

1. THE InvoiceService SHALL ensure a maximum of one Adjustment_Line exists per invoice at any time.
2. WHEN a new bulk discount is applied to an invoice that already has an Adjustment_Line, THE InvoiceService SHALL delete the existing Adjustment_Line and create the new one within a single atomic operation.
3. WHEN the user removes the bulk discount, THE InvoiceService SHALL delete the Adjustment_Line and recompute invoice totals by calling `RecomputeAndUpdateTotalsAsync`.

### Requirement 5: Invoice Totals Recomputation

**User Story:** As an invoice editor, I want the invoice totals to accurately reflect both per-line discounts and the invoice-level discount, so that the financial summary is correct.

#### Acceptance Criteria

1. WHEN an Adjustment_Line is created, modified, or deleted, THE InvoiceService SHALL recompute the invoice totals by calling `RecomputeAndUpdateTotalsAsync`.
2. THE InvoiceService SHALL compute Subtotal as the sum of `LineTotal` for all normal Invoice_Lines (excluding the Adjustment_Line).
3. THE InvoiceService SHALL compute TaxAmount as the sum of each normal Invoice_Line's tax contribution (each line's `LineTotal × VatRate / 100`), rounded to 2 decimal places at the aggregate level (matching existing production rounding behavior).
4. THE InvoiceService SHALL compute TotalAmount as the sum of `LineTotal` for all Invoice_Lines (including the Adjustment_Line) plus TaxAmount.

### Requirement 6: Invoice Totals Breakdown Display

**User Story:** As an invoice editor, I want to see a detailed totals breakdown showing subtotal, line discounts, invoice discount, net amount, VAT, and total, so that I understand exactly how the final amount is derived.

#### Acceptance Criteria

1. THE invoice edit UI SHALL display the following totals breakdown: Subtotal, Line_Discounts, Invoice_Discount, Net_Amount, VAT, and Total.
2. WHEN no Adjustment_Line exists, THE invoice edit UI SHALL hide the Invoice_Discount row from the totals breakdown.
3. WHEN no per-line discounts exist on any Invoice_Line, THE invoice edit UI SHALL hide the Line_Discounts row from the totals breakdown.
4. WHEN both per-line discounts and an Adjustment_Line exist, THE invoice edit UI SHALL display both Line_Discounts and Invoice_Discount as separate rows in the totals summary.

### Requirement 7: Bulk Discount Modal UI

**User Story:** As an invoice editor, I want a modal dialog to configure the bulk discount, so that I can choose between percentage and fixed amount and confirm the discount before it is applied.

#### Acceptance Criteria

1. WHEN the user clicks the "Bulk Discount" button, THE invoice edit UI SHALL open the Bulk_Discount_Modal.
2. THE Bulk_Discount_Modal SHALL provide a toggle or selector for choosing between "Percentage" and "Fixed Amount" discount types, with "Percentage" selected by default.
3. IF "Percentage" discount type is selected, THEN THE Bulk_Discount_Modal SHALL display a numeric input field that accepts values between 0.01 and 100 inclusive, with up to 2 decimal places. IF "Fixed Amount" discount type is selected, THEN THE Bulk_Discount_Modal SHALL display a numeric input field that accepts values between 0.01 and the current Net_Amount (Subtotal minus Line_Discounts), with up to 2 decimal places.
4. WHEN the user changes the discount value or discount type in the Bulk_Discount_Modal, THE Bulk_Discount_Modal SHALL display a preview showing the calculated discount amount in currency (i.e., the resulting negative LineTotal that would be applied) within 300 milliseconds of the input change.
5. WHEN the user confirms the discount in the Bulk_Discount_Modal, THE invoice edit UI SHALL block the UI using BlockUI, submit the discount to the InvoiceService, unblock the UI, close the Bulk_Discount_Modal, and display a SweetAlert2 success or error notification.
6. IF the discount value is zero, empty, negative, exceeds 100 for percentage type, or exceeds the Net_Amount for fixed amount type, THEN THE Bulk_Discount_Modal SHALL display a validation message below the input field and disable the confirm button.

### Requirement 8: Adjustment Line Removal

**User Story:** As an invoice editor, I want to remove a previously applied bulk discount, so that I can revert the invoice to its original line-item totals.

#### Acceptance Criteria

1. WHEN an Adjustment_Line exists on the invoice, THE invoice edit UI SHALL display a "Remove Discount" action (button or icon) near the invoice discount summary row.
2. WHEN the user clicks the "Remove Discount" action, THE invoice edit UI SHALL display a SweetAlert2 confirmation dialog with a warning icon, a confirm button, and a cancel button.
3. WHEN the user confirms removal in the SweetAlert2 dialog, THE invoice edit UI SHALL block the UI using BlockUI, send the delete request to the InvoiceService, unblock the UI, and display a SweetAlert2 success or error notification. THE InvoiceService SHALL delete the Adjustment_Line and recompute invoice totals.
4. WHEN the Adjustment_Line is successfully removed, THE invoice edit UI SHALL update the totals breakdown in the DOM to reflect the removal without requiring a full page reload.

### Requirement 9: Printed Invoice PDF Representation

**User Story:** As a business owner, I want the bulk discount to appear on the printed invoice PDF, so that the customer can see the discount that was applied.

#### Acceptance Criteria

1. WHEN an invoice with an Adjustment_Line is rendered as a PDF, THE PDF generator SHALL include the Adjustment_Line in the line items section with its description and negative amount.
2. THE PDF totals section SHALL display the same breakdown as the edit UI: Subtotal, Line_Discounts, Invoice_Discount, Net_Amount, VAT, and Total.
3. THE Adjustment_Line in the PDF SHALL display a description that includes the discount type and value — formatted as "Invoice Discount (X%)" for percentage discounts or "Invoice Discount (-€X.XX)" for fixed amount discounts — to distinguish it from normal Invoice_Lines.

### Requirement 10: Audit Logging

**User Story:** As a system administrator, I want all bulk discount operations to be audit-logged, so that there is a traceable record of who applied, modified, or removed invoice-level discounts.

#### Acceptance Criteria

1. WHEN an Adjustment_Line is created, THE InvoiceService SHALL write an Audit_Log entry recording the invoice ID, discount type, discount value, resulting line total, the user who performed the action, and a UTC timestamp of the operation.
2. WHEN an Adjustment_Line is replaced by a new bulk discount, THE InvoiceService SHALL write an Audit_Log entry recording the invoice ID, the previous discount type and value, the new discount type and value, the previous and new line totals, the user who performed the action, and a UTC timestamp.
3. WHEN an Adjustment_Line is removed, THE InvoiceService SHALL write an Audit_Log entry recording the invoice ID, the removed discount type, discount value, line total, the user who performed the action, and a UTC timestamp.
4. IF the Audit_Log entry fails to persist, THEN THE InvoiceService SHALL roll back the associated bulk discount operation and return an error indicating the operation could not be completed.

### Requirement 11: Database Schema Extension

**User Story:** As a developer, I want the InvoiceLine table to support the adjustment line flag, so that bulk discount lines can be distinguished from normal line items.

#### Acceptance Criteria

1. THE database schema SHALL include an `IsAdjustmentLine` BIT column on `[invoice].[InvoiceLine]` with a default value of `0`.
2. THE `IsAdjustmentLine` column SHALL be `NOT NULL`.
3. THE existing Invoice_Lines SHALL remain unaffected by the schema change (all existing rows default to `IsAdjustmentLine = 0`).
4. THE same migration SHALL add `IsAdjustmentLine` BIT NOT NULL DEFAULT 0 to `[quotation].[QuotationLine]` in the same script.

### Requirement 12: Adjustment Line Exclusion from Line Item Editing

**User Story:** As an invoice or quotation editor, I want the adjustment line to be excluded from the normal line item editing flow, so that it cannot be accidentally modified or duplicated through the standard line item modal.

#### Acceptance Criteria

1. THE invoice and quotation edit UIs SHALL exclude the Adjustment_Line from the editable line items list.
2. THE line item modal SHALL NOT be openable for an Adjustment_Line.
3. WHEN displaying lines in the edit UI, THE edit UI SHALL render the Adjustment_Line as a non-editable row displaying its description and negative amount, with a label indicating it is a system-managed discount line.
4. IF an update or delete request targeting an Adjustment_Line is received through the standard line item API endpoint, THEN THE service SHALL reject the request and return an error indicating that adjustment lines cannot be modified through the line item editing flow.

### Requirement 13: Quotation Bulk Discount

**User Story:** As a quotation editor, I want to apply a bulk discount to an entire quotation (percentage or fixed amount), so that I can present a discounted proposal to the client without editing each line individually.

#### Acceptance Criteria

1. THE quotation edit UI SHALL provide a "Bulk Discount" button that opens the Bulk_Discount_Modal, identical in functionality to the invoice Bulk_Discount_Modal.
2. WHEN the user submits a percentage or fixed amount discount through the Bulk_Discount_Modal, THE QuotationService SHALL create an Adjustment_Line on `[quotation].[QuotationLine]` with `IsAdjustmentLine = true`, `VatRate = 0`, `Quantity = 1`, and the computed negative `LineTotal`.
3. THE QuotationService SHALL enforce the same validation rules as the InvoiceService: percentage between 0.01–100, fixed amount not exceeding net amount, zero subtotal rejection, and single adjustment line per quotation.
4. THE QuotationService SHALL auto-recalculate percentage adjustment lines when normal quotation lines are added, removed, or updated.
5. WHILE the quotation is not in Draft_Status, THE quotation edit UI SHALL hide the "Bulk Discount" button.
6. THE quotation PDF (Proposal/Snapshot) SHALL display the Adjustment_Line and totals breakdown identically to the invoice PDF.

### Requirement 14: Quotation-to-Invoice Conversion Carry-Over

**User Story:** As a user converting a quotation to an invoice, I want the bulk discount from the quotation to carry over to the generated invoice, so that the invoice total matches what the client approved on the quotation.

#### Acceptance Criteria

1. WHEN a quotation with an Adjustment_Line is converted to an invoice, THE conversion process SHALL create a corresponding Adjustment_Line on the new invoice with `IsAdjustmentLine = true` and the same `DiscountType`, `Discount` value, and computed `LineTotal`.
2. FOR percentage-type adjustments carried over during conversion, THE conversion process SHALL recalculate the `LineTotal` based on the new invoice's subtotal (to account for any rounding differences).
3. FOR fixed-amount adjustments carried over during conversion, THE conversion process SHALL copy the `LineTotal` as-is.
4. AFTER conversion, THE InvoiceService SHALL call `RecomputeAndUpdateTotalsAsync` on the new invoice to ensure totals are consistent.

### Requirement 15: Database Schema Extension for QuotationLine

**User Story:** As a developer, I want the QuotationLine table to support the adjustment line flag, so that bulk discount lines can be distinguished from normal quotation line items.

#### Acceptance Criteria

1. THE database schema SHALL include an `IsAdjustmentLine` BIT column on `[quotation].[QuotationLine]` with a default value of `0`.
2. THE `IsAdjustmentLine` column SHALL be `NOT NULL`.
3. THE existing Quotation_Lines SHALL remain unaffected by the schema change (all existing rows default to `IsAdjustmentLine = 0`).
