# Implementation Plan: Invoice Edit Modal Lines

## Overview

Transform the Invoice Edit view's line item management from inline forms to a modal-based approach. This involves rewriting the line items section of `Edit.cshtml` to render a compact flat table (no section grouping), creating a new `_InvoiceLineItemModal.cshtml` partial, adding `invoice-line-modal.js` for modal logic and AJAX submission, adding `invoice-line-modal.css` for styling, and updating `Edit.cshtml` to include the new assets.

## Tasks

- [x] 1. Create modal CSS and partial view
  - [x] 1.1 Create `wwwroot/css/invoice-line-modal.css` with modal and table styles
    - Define `.modal-overlay` (fixed position, full-screen backdrop, z-index, flex centering)
    - Define `.modal-card` (centered card with border-radius, shadow, max-width ~720px, padding)
    - Define `.modal-header`, `.modal-footer` button layouts
    - Define `.line-item-table` styles (compact rows, right-aligned numeric columns, bold totals)
    - Define `.discount-green` for green discount display
    - Define `.modal-advanced-section` with collapsed toggle styling
    - Define empty state muted paragraph styling
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.3, 6.2_

  - [x] 1.2 Create `Views/Invoice/_InvoiceLineItemModal.cshtml` partial view
    - Add modal overlay container with `id="invoiceLineItemModal"` and `style="display:none"`
    - Add modal card with header (`#invoiceLineModalTitle`, `#invoiceLineModalSubtitle`)
    - Add form `#invoiceLineItemForm` with hidden fields: `lineId`, `invoiceId`, `productCode`
    - Add Row 1: Description field (full width, required, with autocomplete container)
    - Add Row 2: Subtitle + Reference URL (2-column layout)
    - Add Row 3: Quantity, Unit Price, VAT%, Cost Price (4-column layout)
    - Add Row 4: Discount + Discount Type dropdown (2-column layout)
    - Add Advanced collapsible section with Reverse Charge checkbox labelled "Reverse Charge (VAT accounted by buyer)"
    - Add modal footer with `#invoiceLineModalSubmitBtn` and Cancel button
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.7, 3.1, 3.2, 3.4, 6.1, 6.2, 6.3_

- [x] 2. Rewrite Invoice Edit view line items section to compact table layout
  - [x] 2.1 Rewrite `Views/Invoice/Edit.cshtml` — line items table structure
    - Replace inline line-item add/edit forms with a single compact `<table>` layout
    - Render table with columns: #, Description, Qty, Unit Price, Discount, Total, Actions
    - Each row: row number (muted), description (bold) + subtitle (smaller muted below), quantity, unit price with currency, discount display (dash if 0, green minus-prefixed amount if > 0), line total (bold)
    - Actions column: Edit button (calls `showEditInvoiceLineModal`) and Remove × button (calls `confirmRemoveInvoiceLine`)
    - Add `data-*` attributes on each `<tr>` for all line item fields (line-id, description, subtitle, reference-url, quantity, unit-price, vat-rate, discount, discount-type, cost-price, is-reverse-charge, product-code)
    - Order rows by SortOrder ascending
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 2.1_

  - [x] 2.2 Rewrite `Views/Invoice/Edit.cshtml` — add button and empty state
    - Add "+ Add Line" button below the table (calls `showAddInvoiceLineModal()`)
    - Add empty state: when no lines exist, display a muted paragraph with prompt to add a line item
    - Remove all existing inline add/edit form markup for line items
    - _Requirements: 1.8, 3.1_

- [x] 3. Implement modal JavaScript module
  - [x] 3.1 Create `wwwroot/js/invoice-line-modal.js` — modal open/close and population logic
    - Implement `showEditInvoiceLineModal(lineId)`: find table row by `data-line-id`, read all `data-*` attributes, populate form fields, set title to "Edit Line Item" / subtitle "Update the details for this line item.", show submit button as "Save Changes" (primary), display modal
    - Implement `showAddInvoiceLineModal()`: clear form fields, pre-fill quantity=1, discount=0, discountType=Percentage, set hidden invoiceId, set title "Add Line Item" / subtitle "Add a new line item to this invoice.", show submit button as "Add Line" (green), display modal
    - Implement `hideInvoiceLineItemModal()`: hide modal overlay, reset form
    - Add overlay click handler to close modal (click on background outside card)
    - Add Escape key handler to close modal
    - Implement Reverse Charge toggle: when checked set VAT% to 0 and readonly, when unchecked restore previous value and editable
    - Implement Advanced section collapse/expand toggle
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6, 2.12, 3.1, 3.2, 3.3, 3.9_

  - [x] 3.2 Create `wwwroot/js/invoice-line-modal.js` — form submission and AJAX logic
    - Implement submit handler: gather form data from `#invoiceLineItemForm`, include antiforgery token
    - Implement Description validation: prevent submission if empty or whitespace-only, show inline validation message
    - Implement Quantity validation: prevent submission if ≤ 0
    - Determine endpoint: `/Invoice/AddLine` for add mode, `/Invoice/UpdateLine` for edit mode
    - Follow pattern: `BlockUI.show('Saving...')` → `fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body: urlEncodedFormData })` with AbortController (30s timeout) → parse JSON → `BlockUI.hide()`
    - On success: `location.reload()`
    - On failure: `Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'An unexpected error occurred.', confirmButtonColor: '#0D5EA6' })` — modal stays open
    - On timeout: `Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.', confirmButtonColor: '#0D5EA6' })` — modal stays open
    - Send data as `application/x-www-form-urlencoded` with correct field names
    - _Requirements: 2.8, 2.9, 2.10, 2.11, 3.5, 3.6, 3.7, 3.8, 3.10, 5.1, 5.2, 5.3, 5.4, 5.5, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 3.3 Implement remove line confirmation flow in `invoice-line-modal.js`
    - Implement `confirmRemoveInvoiceLine(lineId)`: show `Swal.fire({ title: 'Remove this line item?', text: 'This action cannot be undone.', icon: 'warning', showCancelButton: true, confirmButtonColor: '#C24A4A', cancelButtonColor: '#6b7c8d', confirmButtonText: 'Yes, remove it', cancelButtonText: 'Cancel' })`
    - On confirm: `BlockUI.show('Removing...')` → `fetch('/Invoice/RemoveLine', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token }, body: 'lineId=' + lineId })` → `BlockUI.hide()` → on success `location.reload()`, on error `Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Unable to reach the server.', confirmButtonColor: '#0D5EA6' })`
    - On cancel/dismiss: take no action
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 4. Checkpoint - Verify partial view and JS integration
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update Edit view and wire everything together
  - [x] 5.1 Update `Views/Invoice/Edit.cshtml` to include new assets and partial
    - Add `<link>` reference to `invoice-line-modal.css` in the styles section
    - Add `<script src="~/js/invoice-line-modal.js"></script>` reference
    - Add `@await Html.PartialAsync("_InvoiceLineItemModal", Model)` to render the modal partial (once, outside line items loop)
    - Remove any native `alert()` or `confirm()` calls from existing inline line item JavaScript
    - Ensure modal partial receives the current InvoiceId for the hidden field
    - _Requirements: 2.1, 3.1, 5.3, 8.1, 8.2, 8.3, 8.4_

  - [x] 5.2 Bind catalog autocomplete to modal description field
    - In the modal open (add mode), re-initialize the existing Catalog_Autocomplete targeting `#invoiceLineItemForm` description field
    - Ensure autocomplete populates ProductCode hidden field, Description, Unit Price, Cost Price, and VAT% fields when an item is selected
    - Autocomplete fetches from existing `/LineItemCatalog/Search` endpoint
    - _Requirements: 3.11, 3.12_

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Property-based tests with fast-check
  - [x] 7.1 Write property test for line total computation
    - **Property 1: Line total equals qty × unitPrice − discountAmount**
    - Generate arbitrary quantity (>0), unitPrice (≥0), discount (≥0), discountType ∈ {Percentage, Fixed}
    - Assert computed total matches formula: for Percentage type discountAmount = qty × unitPrice × (discount / 100), for Fixed type discountAmount = discount
    - **Validates: Requirements 1.6**

  - [x] 7.2 Write property test for discount display formatting
    - **Property 2: Zero discount shows dash, positive shows minus-prefixed currency amount**
    - Generate arbitrary discount values (0 and >0), quantity, unitPrice, and discountType
    - Assert: discount === 0 → dash character displayed; discount > 0 + Percentage → formatted computed amount with minus prefix; discount > 0 + Fixed → formatted discount value with minus prefix
    - **Validates: Requirements 1.3, 1.4, 1.5**

  - [x] 7.3 Write property test for modal pre-population round-trip
    - **Property 3: Edit button populates modal fields matching data attributes**
    - Generate arbitrary field value objects (strings, numbers, booleans), simulate data-* attributes on a table row and modal population
    - Assert all form field values match source data attributes for: description, subtitle, reference URL, quantity, unit price, VAT%, discount, discount type, cost price, reverse charge state, product code
    - **Validates: Requirements 2.1**

  - [x] 7.4 Write property test for reverse charge toggle round-trip
    - **Property 4: Reverse charge sets VAT to 0 and readonly, unchecking restores previous value**
    - Generate arbitrary initial VAT rates (0–99.99), simulate toggle on then off
    - Assert: when checked VAT=0 and readonly; when unchecked VAT=original value and editable
    - **Validates: Requirements 2.5, 2.6**

  - [x] 7.5 Write property test for whitespace description rejection
    - **Property 5: Whitespace-only descriptions are rejected**
    - Generate arbitrary strings composed of whitespace characters (spaces, tabs, newlines)
    - Assert: form submission is prevented and inline validation message is displayed
    - **Validates: Requirements 2.8, 6.4**

  - [x] 7.6 Write property test for sort order invariant
    - **Property 6: Table rows appear in ascending SortOrder**
    - Generate arbitrary arrays of line items with distinct SortOrder values
    - Assert rendered table row order matches strictly ascending SortOrder sequence
    - **Validates: Requirements 1.7**

  - [x] 7.7 Write property test for catalog autocomplete field population
    - **Property 7: Selected catalog item populates all target fields**
    - Generate arbitrary catalog items with non-null values for description, unit price, cost price, VAT rate, and product code
    - Assert selecting that item populates the modal's Description, Unit Price, Cost Price, VAT%, and Product Code fields with exact corresponding values
    - **Validates: Requirements 3.12**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using fast-check
- No backend changes are required — all work is frontend/view-layer
- Invoice lines are flat (no section grouping) unlike quotations — no "Move to Section" functionality
- The existing `BlockUI` utility from `/js/block-ui.js` is reused for the BlockUI + fetch pattern
- The Invoice Create view is explicitly out of scope (Requirement 8.2)
- Endpoints: `/Invoice/AddLine`, `/Invoice/UpdateLine`, `/Invoice/RemoveLine`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3"] },
    { "id": 4, "tasks": ["5.1", "5.2"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5", "7.6", "7.7"] }
  ]
}
```
