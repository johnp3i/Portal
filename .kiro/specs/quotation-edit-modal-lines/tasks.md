# Implementation Plan: Quotation Edit Modal Lines

## Overview

Transform the Quotation Edit view's line item management from inline forms to a modal-based approach. This involves rewriting `_SectionCards.cshtml` to render compact tables, creating a new `_LineItemModal.cshtml` partial, adding `quotation-line-modal.js` for modal logic and AJAX submission, adding `quotation-line-modal.css` for styling, and updating `Edit.cshtml` to include the new assets.

## Tasks

- [x] 1. Create modal CSS and partial view
  - [x] 1.1 Create `wwwroot/css/quotation-line-modal.css` with modal and table styles
    - Define `.modal-overlay` (fixed position, full-screen backdrop, z-index, flex centering)
    - Define `.modal-card` (centered card with border-radius, shadow, max-width ~720px, padding)
    - Define `.modal-header`, `.modal-footer` button layouts
    - Define `.line-item-table` styles (compact rows, right-aligned numeric columns, bold totals)
    - Define `.section-header` and `.section-summary` styles
    - Define `.info-banner` for General section (muted background, soft border, info icon)
    - Define General section card dashed border and muted background
    - Define `.discount-green` for green discount display
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 4.1, 5.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Create `Views/Quotation/_LineItemModal.cshtml` partial view
    - Add modal overlay container with `id="lineItemModal"` and `style="display:none"`
    - Add modal card with header (`#lineModalTitle`, `#lineModalSubtitle`)
    - Add form `#lineItemForm` with hidden fields: `__RequestVerificationToken`, `lineId`, `ProposalSectionId`, `ProductCode`
    - Add Row 1: Description field (full width, required, with autocomplete container)
    - Add Row 2: Subtitle + Reference URL (2-column layout)
    - Add Row 3: Quantity, Unit Price, VAT%, Cost Price (4-column layout)
    - Add Row 4: Discount, Discount Type dropdown, Move to Section dropdown (3-column layout)
    - Add Advanced section with Reverse Charge checkbox labelled "Reverse Charge (VAT accounted by buyer)"
    - Add modal footer with `#lineModalSubmitBtn` and Cancel button
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2, 3.4, 7.1, 7.2_

- [x] 2. Rewrite section cards to compact table layout
  - [x] 2.1 Rewrite `Views/Quotation/_SectionCards.cshtml` — section header and table structure
    - Replace inline line-item forms with section card layout
    - Render section header row: section name (bold, large), section summary (`{count} item(s) · Subtotal {currency}{amount}`), action buttons (reorder ↑↓, Edit Section, Remove)
    - For General section: add `.info-banner` with info icon and text "The General section always appears at the bottom. Create a named section and move items to reorder."
    - Apply dashed border and muted background to General section card
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4_

  - [x] 2.2 Rewrite `Views/Quotation/_SectionCards.cshtml` — line item table rows
    - Render `<table>` with columns: #, Description, Qty, Unit Price, Discount, Total, Actions
    - Each row: row number (muted), description (bold) + subtitle (smaller muted below), quantity, unit price with currency, discount display (dash if 0, green minus-prefixed amount if > 0), line total (bold)
    - Actions column: Edit button (calls `showEditLineModal`) and Remove × button (calls `confirmRemoveLine`)
    - Add `data-*` attributes on each `<tr>` for all line item fields (line-id, description, subtitle, reference-url, quantity, unit-price, vat-rate, discount, discount-type, cost-price, is-reverse-charge, product-code, section-id)
    - Add "+ Add Line Item" button below each section's table (calls `showAddLineModal(sectionId)`)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 3.1, 6.1_

- [x] 3. Implement modal JavaScript module
  - [x] 3.1 Create `wwwroot/js/quotation-line-modal.js` — modal open/close and population logic
    - Implement `showEditLineModal(lineId, sectionId)`: find table row by `data-line-id`, read all `data-*` attributes, populate form fields, set title to "Edit Line Item" / subtitle "Update the details for this line item.", show submit button as "Save Changes" (primary), display modal
    - Implement `showAddLineModal(sectionId)`: clear form fields, pre-fill quantity=1, VAT%=business default, discount=0, set hidden ProposalSectionId, set title "Add Line Item" / subtitle "Add a new item to this section.", show submit button as "Add Line" (green), display modal
    - Implement `hideLineItemModal()`: hide modal overlay, reset form
    - Add overlay click handler to close modal (click on background outside card)
    - Implement Reverse Charge toggle: when checked set VAT% to 0 and readonly, when unchecked restore previous value and editable
    - _Requirements: 2.1, 2.2, 2.5, 2.9, 3.1, 3.2, 3.3, 3.7_

  - [x] 3.2 Create `wwwroot/js/quotation-line-modal.js` — form submission and AJAX logic
    - Implement submit handler: gather FormData from `#lineItemForm`, include antiforgery token
    - Determine endpoint: AddLine URL for add mode, UpdateLine URL for edit mode
    - If edit mode and "Move to Section" changed, call move-line endpoint first
    - Follow pattern: `BlockUI.show('Saving...')` → `fetch(url, { method: 'POST', body: formData })` with AbortController (30s timeout) → parse JSON → `BlockUI.hide()`
    - On success: `location.reload()`
    - On failure: `Swal.fire({ icon: 'error', title: 'Error', text: data.message || 'Unable to reach the server...', confirmButtonColor: '#0D5EA6' })` — modal stays open
    - _Requirements: 2.6, 2.7, 2.8, 3.4, 3.5, 3.6, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4_

  - [x] 3.3 Implement remove line confirmation flow in `quotation-line-modal.js`
    - Implement `confirmRemoveLine(quotationId, lineId)`: show `Swal.fire({ title: 'Remove this line item?', icon: 'warning', showCancelButton: true, confirmButtonColor: '#C24A4A', confirmButtonText: 'Yes, remove it' })`
    - On confirm: `BlockUI.show()` → `fetch('/Quotation/RemoveLine/{quotationId}/{lineId}', { method: 'POST', body with antiforgery })` → `BlockUI.hide()` → on success `location.reload()`, on error `Swal.fire({ icon: 'error', ... })`
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 4. Checkpoint - Verify partial view and JS integration
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update Edit view and wire everything together
  - [x] 5.1 Update `Views/Quotation/Edit.cshtml` to include new assets and partial
    - Add `<link>` reference to `quotation-line-modal.css` in the styles section
    - Add `<script src="~/js/quotation-line-modal.js"></script>` reference
    - Add `@await Html.PartialAsync("_LineItemModal", Model)` to render the modal partial (once, outside the sections loop)
    - Ensure the modal partial receives the list of sections for the "Move to Section" dropdown and business default VAT rate
    - _Requirements: 2.1, 3.1, 9.1, 9.2_

  - [x] 5.2 Bind catalog autocomplete to modal description field
    - In the modal open (add mode), re-initialize the existing Catalog_Autocomplete targeting `#lineItemForm` description field
    - Ensure autocomplete populates ProductCode hidden field and other fields when an item is selected
    - _Requirements: 3.8_

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Property-based tests with fast-check
  - [x] 7.1 Write property test for line total computation
    - **Property 1: Line total equals qty × unitPrice − discountAmount**
    - Generate arbitrary quantity (>0), unitPrice (≥0), discount (≥0), discountType ∈ {Percentage, Fixed}
    - Assert computed total matches formula
    - **Validates: Requirements 1.5**

  - [x] 7.2 Write property test for discount display formatting
    - **Property 2: Zero discount shows dash, positive shows minus-prefixed amount**
    - Generate arbitrary discount values (0 and >0) with any other field values
    - Assert formatting rules hold
    - **Validates: Requirements 1.3, 1.4**

  - [x] 7.3 Write property test for modal pre-population round-trip
    - **Property 3: Edit button populates modal fields matching data attributes**
    - Generate arbitrary field value objects, simulate data-* attributes and modal population
    - Assert all form field values match source attributes
    - **Validates: Requirements 2.1**

  - [x] 7.4 Write property test for section summary computation
    - **Property 4: Summary shows correct count and sum of line totals**
    - Generate arbitrary arrays of line items with positive totals
    - Assert count and formatted sum match
    - **Validates: Requirements 4.2, 4.3**

  - [x] 7.5 Write property test for reverse charge toggle
    - **Property 5: Reverse charge sets VAT to 0 and readonly, unchecking restores**
    - Generate arbitrary initial VAT rates, simulate toggle on/off
    - Assert VAT=0 and readonly when checked, restored when unchecked
    - **Validates: Requirements 2.5**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using fast-check
- No backend changes are required — all work is frontend/view-layer
- The existing `quotation-line-save.js` AJAX interception module is reused for the BlockUI + fetch pattern
- The Quotation Create view is explicitly out of scope (Requirement 9.2)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3"] },
    { "id": 4, "tasks": ["5.1", "5.2"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5"] }
  ]
}
```
