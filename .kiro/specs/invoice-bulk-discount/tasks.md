# Implementation Plan: Invoice Bulk Discount

## Overview

This feature adds a document-level bulk discount capability to both invoicing and quotation modules via a special "adjustment line" mechanism. An adjustment line is a regular InvoiceLine/QuotationLine flagged with `IsAdjustmentLine = true` that carries a negative LineTotal. Implementation proceeds bottom-up: DB migration → Entity changes → Repository updates → Service methods → Controller endpoints → UI (modal + totals breakdown + line rendering) → PDF updates → Quotation parity → Conversion carry-over → Audit logging.

## Tasks

- [x] 1. Database migration and entity layer
  - [x] 1.1 Create database migration script for IsAdjustmentLine column
    - Create SQL migration file `Portal.Database/Migrations/XXX_AddIsAdjustmentLineToInvoiceAndQuotationLine.sql`
    - Add `USE [PortalDb]` header
    - ALTER TABLE `[invoice].[InvoiceLine]` ADD `[IsAdjustmentLine] BIT NOT NULL CONSTRAINT [DF_InvoiceLine_IsAdjustmentLine] DEFAULT (0)`
    - ALTER TABLE `[quotation].[QuotationLine]` ADD `[IsAdjustmentLine] BIT NOT NULL CONSTRAINT [DF_QuotationLine_IsAdjustmentLine] DEFAULT (0)`
    - Existing rows remain unaffected (default to 0)
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 15.1, 15.2, 15.3_

  - [x] 1.2 Add IsAdjustmentLine property to InvoiceLine and QuotationLine entities
    - Add `public bool IsAdjustmentLine { get; set; }` to `Portal.Infrastructure.Entities.InvoiceLine`
    - Add `public bool IsAdjustmentLine { get; set; }` to `Portal.Infrastructure.Entities.QuotationLine`
    - _Requirements: 11.1, 15.1_

- [x] 2. Repository layer updates
  - [x] 2.1 Update InvoiceLineRepository SELECT queries to include IsAdjustmentLine
    - Add `[IsAdjustmentLine]` to ALL existing SELECT column lists in InvoiceLineRepository
    - Add new method `GetAdjustmentLineByInvoiceIdAsync(int invoiceId)` — SELECT WHERE InvoiceId = @InvoiceId AND IsAdjustmentLine = 1
    - Use full table names in queries, `catch (Exception ex)`, rethrow
    - _Requirements: 11.1, 4.1_

  - [x] 2.2 Update QuotationLineRepository SELECT queries to include IsAdjustmentLine
    - Add `[IsAdjustmentLine]` to ALL existing SELECT column lists in QuotationLineRepository
    - Add new method `GetAdjustmentLineByQuotationIdAsync(int quotationId)` — SELECT WHERE QuotationId = @QuotationId AND IsAdjustmentLine = 1
    - Use full table names in queries, `catch (Exception ex)`, rethrow
    - _Requirements: 15.1, 13.2_

- [x] 3. Checkpoint — Ensure migration, entities, and repository changes compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Service layer — DTOs and InvoiceService bulk discount methods
  - [x] 4.1 Create BulkDiscountResult and InvoiceTotalsBreakdown DTOs
    - Create `BulkDiscountResult` class with: Success (bool), Message (string?), Totals (InvoiceTotalsBreakdown?)
    - Create `InvoiceTotalsBreakdown` class with: GrossSubtotal, NetSubtotal, LineDiscounts, InvoiceDiscount, NetAmount, Vat, Total, DiscountType, DiscountValue, HasInvoiceDiscount, HasLineDiscounts, CurrencyCode
    - Create `QuotationTotalsBreakdown` class with same fields (or use a shared `DocumentTotalsBreakdown` base class)
    - Add new method signatures to `IInvoiceService` interface: `ApplyBulkDiscountAsync`, `RemoveBulkDiscountAsync`, `GetTotalsBreakdownAsync`
    - Add new method signatures to `IQuotationService` interface: `ApplyBulkDiscountAsync`, `RemoveBulkDiscountAsync`, `GetTotalsBreakdownAsync`
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 4.2 Implement ApplyBulkDiscountAsync in InvoiceService
    - Validate invoice exists and belongs to current business
    - Validate draft status (InvoiceStatusTypeId == 1), reject otherwise
    - Validate discountType is "Percentage" or "Fixed"
    - Get all normal lines (IsAdjustmentLine = false), compute subtotal
    - For Percentage: validate 0.01 ≤ value ≤ 100, reject if subtotal == 0, compute LineTotal = -Round(subtotal × value / 100, 2, MidpointRounding.AwayFromZero)
    - For Fixed: validate 0.01 ≤ value ≤ 999,999,999.99, max 2 decimal places, reject if value > net amount, set LineTotal = -value
    - Wrap in explicit transaction: delete existing adjustment line if present, insert new adjustment line with correct field values (IsAdjustmentLine=true, VatRate=0, Quantity=1, etc.), call RecomputeAndUpdateTotalsAsync, write audit log
    - Resolve currency symbol from invoice CurrencyCode for description formatting
    - Return BulkDiscountResult with totals breakdown
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4, 2.5, 3.3, 4.1, 4.2, 5.1_

  - [x] 4.3 Implement RemoveBulkDiscountAsync in InvoiceService
    - Validate invoice exists, belongs to current business, and is in draft status
    - Find existing adjustment line (reject if none found)
    - Capture values for audit
    - Delete adjustment line
    - Call RecomputeAndUpdateTotalsAsync
    - Write audit log (removal)
    - Return BulkDiscountResult with updated totals
    - _Requirements: 4.3, 8.3, 5.1_

  - [x] 4.4 Implement GetTotalsBreakdownAsync in InvoiceService
    - Get all lines for invoice
    - Compute GrossSubtotal, NetSubtotal, LineDiscounts, InvoiceDiscount, NetAmount, Vat, Total
    - Populate HasInvoiceDiscount, HasLineDiscounts booleans
    - Include CurrencyCode from invoice
    - _Requirements: 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_

  - [x] 4.5 Update RecomputeAndUpdateTotalsAsync with adjustment line logic
    - Exclude adjustment lines from subtotal and tax computation
    - Auto-recalculate percentage adjustment lines: if DiscountType == "Percentage", recompute LineTotal = -Round(newSubtotal × Discount / 100, 2, MidpointRounding.AwayFromZero) and update if changed
    - Leave fixed-amount adjustment lines unchanged
    - Implement transaction nesting safety: check `_context.Database.CurrentTransaction != null` before creating new transaction
    - Compute TotalAmount = subtotal + adjustmentLineTotal + taxAmount
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 4.6 Write property test for adjustment line field invariants
    - **Property 1: Adjustment Line Field Invariants**
    - Test: For any valid discount operation, the resulting adjustment line has IsAdjustmentLine=true, VatRate=0, Quantity=1, DiscountType matching requested type, Discount equal to user value
    - **Validates: Requirements 1.2, 1.3, 2.2, 2.3**

  - [ ]* 4.7 Write property test for LineTotal computation correctness
    - **Property 2: LineTotal Computation Correctness**
    - Test: For any valid percentage p (0.01–100) with subtotal S > 0, LineTotal == -Round(S×p/100, 2, AwayFromZero). For any valid fixed f, LineTotal == -f
    - **Validates: Requirements 1.1, 2.1**

  - [ ]* 4.8 Write property test for invalid discount value rejection
    - **Property 3: Invalid Discount Values Are Rejected**
    - Test: For any percentage outside [0.01, 100] or fixed amount < 0.01 / > 999,999,999.99 / > 2dp, service rejects and invoice state is unchanged
    - **Validates: Requirements 1.5, 2.5**

  - [ ]* 4.9 Write property test for fixed amount cannot exceed net amount
    - **Property 4: Fixed Amount Cannot Exceed Net Amount**
    - Test: For any invoice with net amount N and fixed discount f > N, service rejects and state is unchanged
    - **Validates: Requirements 2.4**

  - [ ]* 4.10 Write property test for non-draft invoice rejection
    - **Property 5: Non-Draft Invoice Rejection**
    - Test: For any invoice with InvoiceStatusTypeId ≠ 1, any apply/remove operation is rejected
    - **Validates: Requirements 3.3, 3.4**

  - [ ]* 4.11 Write property test for single adjustment line invariant
    - **Property 6: Single Adjustment Line Invariant**
    - Test: For any sequence of bulk discount operations, at most 1 adjustment line exists per invoice at any point
    - **Validates: Requirements 4.1, 4.2, 4.3**

  - [ ]* 4.12 Write property test for invoice totals computation
    - **Property 7: Invoice Totals Computation**
    - Test: Subtotal = sum of normal LineTotal; TaxAmount = Round(sum of normal LineTotal×VatRate/100, 2); TotalAmount = sum of ALL LineTotals + TaxAmount
    - **Validates: Requirements 5.2, 5.3, 5.4**

- [x] 5. Checkpoint — Ensure InvoiceService bulk discount methods compile and pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Service layer — Guards and audit logging
  - [x] 6.1 Add adjustment line guards to UpdateLineAsync and RemoveLineAsync
    - In InvoiceService.UpdateLineAsync: add guard at top — if line.IsAdjustmentLine, throw InvalidOperationException
    - In InvoiceService.RemoveLineAsync: add guard at top — if line.IsAdjustmentLine, throw InvalidOperationException
    - Same guards in QuotationService.UpdateLineAsync and RemoveLineAsync
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [x] 6.2 Implement audit logging for bulk discount operations
    - Write audit log on create (invoice ID, discount type, value, line total, user, UTC timestamp)
    - Write audit log on replace (previous + new discount type/value/line total, user, UTC timestamp)
    - Write audit log on remove (removed discount type/value/line total, user, UTC timestamp)
    - If audit log persistence fails, roll back the entire bulk discount operation
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [ ]* 6.3 Write property test for standard endpoint guard
    - **Property 9: Standard Endpoints Reject Adjustment Line Operations**
    - Test: For any line where IsAdjustmentLine=true, UpdateLineAsync and RemoveLineAsync reject with error
    - **Validates: Requirements 12.4**

  - [ ]* 6.4 Write property test for description formatting
    - **Property 8: Adjustment Line Description Formatting**
    - Test: For percentage discount p, description matches "Invoice Discount ({p}%)". For fixed f with currency C, description matches "Invoice Discount (-{symbol}{f:F2})"
    - **Validates: Requirements 9.3**

- [x] 7. Controller layer — Invoice bulk discount endpoints
  - [x] 7.1 Add AxPostApplyBulkDiscount to InvoiceController
    - [HttpPost], [ValidateAntiForgeryToken]
    - Accept invoiceId (int), discountType (string), discountValue (decimal)
    - Call ApplyBulkDiscountAsync, return Json { success, data }
    - Catch ArgumentException → Json { success: false, message }
    - Catch InvalidOperationException → Json { success: false, message }
    - Catch Exception ex → Json { success: false, message: "An unexpected error occurred." }
    - _Requirements: 1.1, 2.1, 7.5_

  - [x] 7.2 Add AxPostRemoveBulkDiscount to InvoiceController
    - [HttpPost], [ValidateAntiForgeryToken]
    - Accept invoiceId (int)
    - Call RemoveBulkDiscountAsync, return Json { success, data }
    - Catch InvalidOperationException → Json { success: false, message }
    - Catch Exception ex → Json { success: false, message: "An unexpected error occurred." }
    - _Requirements: 8.3_

- [x] 8. Checkpoint — Ensure controller endpoints compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. UI — Bulk Discount Modal and totals breakdown (Invoice)
  - [x] 9.1 Create shared Bulk Discount Modal partial view
    - Create `_BulkDiscountModal.cshtml` partial (shared between invoice and quotation)
    - Toggle: Percentage / Fixed Amount (default: Percentage)
    - Numeric input with validation (0.01–100 for percentage, 0.01–NetAmount for fixed)
    - Live preview showing calculated discount amount (client-side computation, < 300ms)
    - Confirm and Cancel buttons
    - Validation message below input when value invalid, confirm button disabled
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.6_

  - [x] 9.2 Implement Invoice Bulk Discount JavaScript
    - "Bulk Discount" button click → open modal (only visible when draft status)
    - On type toggle → update input validation range and preview
    - On value change → debounced preview update (< 300ms)
    - On confirm → BlockUI.show() → POST AxPostApplyBulkDiscount with antiforgery token → BlockUI.hide() → close modal → Swal.fire success/error → update DOM totals
    - Hide "Bulk Discount" button when not in draft status
    - _Requirements: 3.1, 3.2, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 9.3 Implement totals breakdown display on Invoice Edit
    - Replace simple subtotal/tax/total with full breakdown: Gross Subtotal, Line Discounts (conditional), Net Subtotal, Invoice Discount (conditional), Net Amount, VAT, Total
    - Hide Invoice Discount row when no adjustment line exists
    - Hide Line Discounts row when no per-line discounts exist
    - Show both when both exist
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 9.4 Implement adjustment line rendering and removal UI
    - Render adjustment line as non-editable row with muted style and "System-managed discount" label
    - No edit/delete icons on adjustment line row
    - "Remove Discount" button near invoice discount summary row (only when adjustment line exists)
    - On "Remove Discount" click → SweetAlert2 confirmation (warning icon) → BlockUI → POST AxPostRemoveBulkDiscount → BlockUI.hide() → Swal.fire success/error → update DOM totals
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 12.1, 12.2, 12.3_

- [x] 10. Checkpoint — Ensure invoice UI compiles and renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. QuotationService — Bulk discount methods (mirror InvoiceService)
  - [x] 11.1 Implement ApplyBulkDiscountAsync in QuotationService
    - Same logic as InvoiceService.ApplyBulkDiscountAsync but operating on QuotationLine and Quotation entities
    - Validate draft status (QuotationStatusTypeId == 1)
    - Same validations: type, value range, subtotal > 0 for percentage, fixed ≤ net amount
    - Insert adjustment line with IsAdjustmentLine=true, VatRate=0, Quantity=1
    - Call RecomputeQuotationTotalsAsync
    - Write audit log
    - Use "Quotation Discount" in description (not "Invoice Discount")
    - _Requirements: 13.1, 13.2, 13.3_

  - [x] 11.2 Implement RemoveBulkDiscountAsync in QuotationService
    - Same logic as InvoiceService.RemoveBulkDiscountAsync but for quotations
    - _Requirements: 13.1_

  - [x] 11.3 Implement GetTotalsBreakdownAsync in QuotationService
    - Same computation as InvoiceService.GetTotalsBreakdownAsync but for quotation lines
    - _Requirements: 13.1_

  - [x] 11.4 Update RecomputeQuotationTotalsAsync with adjustment line logic
    - Same pattern as invoice: exclude adjustment lines from subtotal/tax, auto-recalculate percentage adjustments, transaction nesting safety
    - _Requirements: 13.4_

  - [ ]* 11.5 Write property test for percentage adjustment auto-recalculation
    - **Property 10: Percentage Adjustment Auto-Recalculation**
    - Test: For any invoice/quotation with percentage adjustment (Discount=p), after normal line changes, adjustment LineTotal == -Round(newSubtotal × p / 100, 2, AwayFromZero)
    - **Validates: Requirements 5.1, 13.4**

  - [ ]* 11.6 Write property test for fixed adjustment immutability on line changes
    - **Property 11: Fixed Adjustment Immutability on Line Changes**
    - Test: For any invoice/quotation with fixed adjustment (LineTotal=-f), after normal line changes, adjustment LineTotal remains -f
    - **Validates: Requirements 5.1, 13.4**

- [x] 12. QuotationController — Bulk discount endpoints
  - [x] 12.1 Add AxPostApplyBulkDiscount to QuotationController
    - Same pattern as InvoiceController: [HttpPost], [ValidateAntiForgeryToken], accept quotationId/discountType/discountValue
    - _Requirements: 13.1, 13.2_

  - [x] 12.2 Add AxPostRemoveBulkDiscount to QuotationController
    - Same pattern as InvoiceController
    - _Requirements: 13.1_

- [x] 13. UI — Quotation Bulk Discount Modal, totals breakdown, and line rendering
  - [x] 13.1 Integrate Bulk Discount Modal on Quotation Edit view
    - Include shared `_BulkDiscountModal.cshtml` partial
    - "Bulk Discount" button (hidden when not in draft status)
    - JavaScript wiring identical to invoice (different endpoint URLs)
    - _Requirements: 13.1, 13.5_

  - [x] 13.2 Implement totals breakdown and adjustment line rendering on Quotation Edit
    - Same pattern as invoice: full breakdown, conditional rows, non-editable adjustment line row, "Remove Discount" button
    - Add guards to QuotationService.UpdateLineAsync and RemoveLineAsync for adjustment lines
    - _Requirements: 13.1, 13.3_

- [x] 14. Quotation-to-Invoice conversion carry-over
  - [x] 14.1 Update ConvertFromQuotationAsync to copy IsAdjustmentLine and handle adjustment lines
    - In the InvoiceLine creation loop: add `IsAdjustmentLine = line.IsAdjustmentLine` to the field mapping
    - Copy `Discount` and `DiscountType` fields for adjustment lines
    - Replace inline totals computation (subtotal/taxAmount/totalAmount) with `RecomputeAndUpdateTotalsAsync(invoiceId)` call
    - For percentage adjustments: LineTotal is auto-recalculated by RecomputeAndUpdateTotalsAsync
    - For fixed adjustments: LineTotal is copied as-is
    - _Requirements: 14.1, 14.2, 14.3, 14.4_

  - [x] 14.2 Update DuplicateInvoiceAsync to handle adjustment lines
    - Verify the existing duplication code copies IsAdjustmentLine field (it should if it copies all fields)
    - If the duplication uses explicit field mapping (not `SELECT *`), add `IsAdjustmentLine = sourceLine.IsAdjustmentLine`
    - Ensure RecomputeAndUpdateTotalsAsync is called on the duplicated invoice (auto-recalculates percentage adjustments)
    - _Requirements: 4.1_

- [x] 15. PDF updates — Invoice and Quotation
  - [x] 15.1 Update Invoice PDF Snapshot with totals breakdown
    - Filter adjustment lines out of section line item tables in Snapshot.cshtml
    - Replace simple totals card with full breakdown: Gross Subtotal, Line Discounts (conditional), Invoice Discount (conditional), Net Amount, VAT, Total
    - Format adjustment line description: "Invoice Discount (X%)" or "Invoice Discount (−{symbol}X.XX)"
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 15.2 Update Quotation PDF (Proposal/Snapshot) with totals breakdown
    - Same pattern as invoice PDF but using "Quotation Discount" in labels
    - Filter adjustment lines from section tables
    - Conditional Line Discounts and Document Discount rows
    - _Requirements: 13.6_

- [x] 16. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit, minimum 100 iterations)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex)` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Transaction nesting safety is critical: RecomputeAndUpdateTotalsAsync is called from both transactional and non-transactional contexts
- Currency symbol resolution uses the invoice/quotation CurrencyCode field (EUR→€, GBP→£, USD→$)
- Auto-recalculation of percentage adjustments happens in RecomputeAndUpdateTotalsAsync — existing AddLineAsync/UpdateLineAsync/RemoveLineAsync already call this method
- Fixed-amount adjustments are never auto-recalculated
- Quotation-to-invoice conversion replaces inline totals computation with RecomputeAndUpdateTotalsAsync
- Bottom-up ordering: DB → Entities → Repos → Services → Controller → UI → PDF → Conversion

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "4.5"] },
    { "id": 5, "tasks": ["4.6", "4.7", "4.8", "4.9", "4.10", "4.11", "4.12", "5"] },
    { "id": 6, "tasks": ["6.1", "6.2"] },
    { "id": 7, "tasks": ["6.3", "6.4", "7.1", "7.2"] },
    { "id": 8, "tasks": ["8"] },
    { "id": 9, "tasks": ["9.1"] },
    { "id": 10, "tasks": ["9.2", "9.3", "9.4"] },
    { "id": 11, "tasks": ["10"] },
    { "id": 12, "tasks": ["11.1", "11.2", "11.3", "11.4"] },
    { "id": 13, "tasks": ["11.5", "11.6", "12.1", "12.2"] },
    { "id": 14, "tasks": ["13.1", "13.2"] },
    { "id": 15, "tasks": ["14.1", "14.2"] },
    { "id": 16, "tasks": ["15.1", "15.2"] },
    { "id": 17, "tasks": ["16"] }
  ]
}
```
