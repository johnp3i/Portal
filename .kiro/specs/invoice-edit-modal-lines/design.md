# Design Document: Invoice Edit Modal Lines

## Overview

This design transforms the Invoice Edit view's line item management from inline add/edit forms into a clean two-layer interface: a compact, scannable table for viewing and a centered modal for creating/editing. The approach eliminates visual overload when invoices have many line items, standardises all AJAX operations to the BlockUI → fetch → SweetAlert2 pattern (removing native `alert()` calls), and preserves all existing backend endpoints unchanged.

The redesign is scoped exclusively to the Edit view (`/Invoice/Edit/{id}`). The Create view remains unchanged.

### Design Decisions

1. **Single shared modal for Add and Edit** — One `<div>` serves both modes, populated dynamically via JS. This reduces DOM size and ensures consistent styling.
2. **Page reload after save** — Matches the existing remove-line pattern. Both Add and Edit will reload on success for simplicity and data consistency.
3. **No backend changes** — The modal form submits to the same `/Invoice/AddLine` and `/Invoice/UpdateLine` endpoints using the same form data shape. The antiforgery token is included as a header.
4. **Reuse existing BlockUI + Swal patterns** — `BlockUI.show()` before fetch, `BlockUI.hide()` after response, `Swal.fire()` on error. Success triggers `location.reload()`.
5. **Catalog autocomplete in modal** — The existing autocomplete logic is re-bound when the modal opens in Add mode, targeting the modal's description field.
6. **No sections/grouping** — Unlike the Quotation Edit view which groups lines by ProposalSection, invoice lines are flat. There is no "Move to Section" functionality. Lines are rendered in a single table ordered by SortOrder.

## Architecture

The feature is entirely frontend/view-layer. No new controllers, services, or database changes are required.

```mermaid
graph TD
    A[Invoice Edit View] --> B[Line Items Section]
    B --> C[Line Item Table - flat]
    C --> D[Edit Button → Opens Modal]
    C --> E[Remove Button → Swal Confirm → AJAX]
    B --> F["+ Add Line" Button → Opens Modal]
    
    G[Line Item Modal] --> H[Form Fields]
    H --> I[Save → BlockUI → fetch POST → Reload]
    H --> J[Cancel → Close Modal]
    
    I --> K[/Invoice/AddLine]
    I --> L[/Invoice/UpdateLine]
    E --> M[/Invoice/RemoveLine]
```

### File Changes

| File | Change Type | Description |
|------|-------------|-------------|
| `Views/Invoice/Edit.cshtml` | Major rewrite | Replace inline forms with compact table + modal partial reference |
| `Views/Invoice/_InvoiceLineItemModal.cshtml` | New partial | Modal HTML structure with form fields |
| `wwwroot/js/invoice-line-modal.js` | New file | Modal open/close, populate fields, form submission via AJAX |
| `wwwroot/css/invoice-line-modal.css` | New file | Modal styling, table styling for line items |

## Components and Interfaces

### 1. Line Item Table (Razor — within `Edit.cshtml`)

The line items section renders a single `<table>` (no section grouping) replacing the inline forms.

**Table columns:**
| # | Column | Content | Style |
|---|--------|---------|-------|
| 1 | # | Row number (1-based) | Muted, narrow |
| 2 | Description | Description (bold) + Subtitle (muted, smaller, below) | Wide |
| 3 | Qty | `line.Quantity` formatted | Right-aligned |
| 4 | Unit Price | `line.UnitPrice` formatted with currency | Right-aligned |
| 5 | Discount | Dash if 0, green minus-prefixed amount if > 0 | Right-aligned |
| 6 | Total | `line.LineTotal` bold | Right-aligned |
| 7 | Actions | Edit button + Remove (×) button | Narrow |

**"+ Add Line" button** sits below the table within the line items card.

**Empty state:** When no lines exist, a muted paragraph with an action prompt replaces the table.

### 2. Line Item Modal (Razor — `_InvoiceLineItemModal.cshtml`)

A fixed-position overlay with a centered card containing the full form.

**Structure:**
```html
<div id="invoiceLineItemModal" class="modal-overlay" style="display:none;">
    <div class="modal-card">
        <div class="modal-header">
            <h3 id="invoiceLineModalTitle">Edit Line Item</h3>
            <p id="invoiceLineModalSubtitle" class="muted">Update the details for this line item.</p>
        </div>
        <form id="invoiceLineItemForm" method="post">
            <input type="hidden" id="invoiceLineModalLineId" name="lineId" />
            <input type="hidden" id="invoiceLineModalInvoiceId" name="invoiceId" />
            <input type="hidden" id="invoiceLineModalProductCode" name="productCode" />
            
            <!-- Row 1: Description (full width, required) -->
            <!-- Row 2: Subtitle + Reference URL (2-col) -->
            <!-- Row 3: Qty, Unit Price, VAT%, Cost Price (4-col) -->
            <!-- Row 4: Discount + Discount Type (2-col) -->
            <!-- Advanced: Reverse Charge checkbox (collapsed by default) -->
        </form>
        <div class="modal-footer">
            <button id="invoiceLineModalSubmitBtn" type="button" class="btn btn-primary">Save Changes</button>
            <button type="button" class="btn btn-secondary" onclick="hideInvoiceLineItemModal()">Cancel</button>
        </div>
    </div>
</div>
```

### 3. Modal JavaScript Module (`invoice-line-modal.js`)

**Public API:**
```javascript
// Opens modal in edit mode, pre-fills from data attributes on the table row
function showEditInvoiceLineModal(lineId)

// Opens modal in add mode with defaults
function showAddInvoiceLineModal()

// Closes modal without saving
function hideInvoiceLineItemModal()

// Shows Swal confirm and removes line via AJAX
function confirmRemoveInvoiceLine(lineId)
```

**Internal flow for submission:**
1. Gather form data from modal fields
2. Determine endpoint URL: `/Invoice/AddLine` or `/Invoice/UpdateLine`
3. `BlockUI.show('Saving...')`
4. `fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' }, body: formData })`
5. Parse JSON response
6. `BlockUI.hide()`
7. On success: `location.reload()`
8. On failure: `Swal.fire({ icon: 'error', ... })` — modal stays open

**Key differences from quotation version:**
- No `sectionId` parameter — invoice lines are flat
- No "Move to Section" logic
- Endpoint URLs use `/Invoice/...` instead of `/Quotation/...`
- Form data field names: `invoiceId` instead of `quotationId`, no `ProposalSectionId`

### 4. Remove Line Flow

The Remove button (×) on each table row triggers:
1. `Swal.fire({ title: 'Remove this line item?', text: 'This action cannot be undone.', icon: 'warning', showCancelButton: true, confirmButtonColor: '#C24A4A', cancelButtonColor: '#6b7c8d', confirmButtonText: 'Yes, remove it', cancelButtonText: 'Cancel' })`
2. On confirm: `BlockUI.show('Removing...')` → `fetch('/Invoice/RemoveLine', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token }, body: 'lineId=' + lineId })` → `BlockUI.hide()` → on success `location.reload()`, on error `Swal.fire({ icon: 'error', ... })`

### 5. Catalog Autocomplete (Add Mode)

When the modal opens in add mode, the product catalog autocomplete is bound to the Description field. When a user selects an item from the dropdown:
- **Description** → catalog item description
- **Unit Price** → catalog item unit price
- **Cost Price** → catalog item cost price
- **VAT%** → catalog item VAT rate
- **Product Code** → catalog item product code (hidden field)

The autocomplete fetches suggestions from the existing `/LineItemCatalog/Search` endpoint as the user types.

### 6. Advanced Section (Collapsible)

The Reverse Charge checkbox lives inside a collapsible "Advanced" section at the bottom of the modal form. It is collapsed by default. Clicking the "Advanced" header toggles visibility.

```html
<div class="modal-advanced-section">
    <button type="button" class="advanced-toggle" onclick="toggleAdvancedSection()">
        <span class="toggle-icon">▶</span> Advanced
    </button>
    <div id="invoiceLineAdvancedContent" style="display:none;">
        <label class="checkbox-label">
            <input type="checkbox" id="invoiceLineModalReverseCharge" name="isReverseCharge" />
            Reverse Charge (VAT accounted by buyer)
        </label>
    </div>
</div>
```

## Data Models

No new backend models are required. The existing models serve all needs:

### Existing Models Used

| Model | Role in Feature |
|-------|-----------------|
| `Invoice` | Page-level entity used as `@model`, provides InvoiceId, header fields, totals |
| `InvoiceLine` | Entity with all line fields (Description, Subtitle, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, LineTotal, SortOrder, ReferenceUrl, ProductCode, IsReverseCharge, InvoiceSectionId) |
| `InvoiceSection` | Section entity (not used in line grouping for this feature, but exists in model) |

### Data Flow for Modal Population (Edit Mode)

Line item data is rendered as `data-*` attributes on each table row:
```html
<tr data-line-id="@line.Id"
    data-description="@line.Description"
    data-subtitle="@(line.Subtitle ?? "")"
    data-reference-url="@(line.ReferenceUrl ?? "")"
    data-quantity="@line.Quantity.ToString("G")"
    data-unit-price="@line.UnitPrice.ToString("G")"
    data-vat-rate="@line.VatRate.ToString("G")"
    data-discount="@line.Discount.ToString("G")"
    data-discount-type="@line.DiscountType"
    data-cost-price="@(line.CostPrice?.ToString("G") ?? "")"
    data-is-reverse-charge="@(line.IsReverseCharge ? "true" : "false")"
    data-product-code="@(line.ProductCode ?? "")">
```

The JS reads these attributes to populate the modal form fields when "Edit" is clicked.

### Computed Display Values

| Value | Formula | Display Location |
|-------|---------|------------------|
| Line Total | `Quantity × UnitPrice − DiscountAmount` | Table "Total" column |
| Discount Amount | If `DiscountType == "Percentage"`: `UnitPrice × Qty × (Discount / 100)`. If `"Fixed"`: `Discount` | Table "Discount" column |

These are computed server-side (already available via `line.LineTotal`) and rendered directly in Razor. No client-side recalculation needed for the table.

### Form Data Shape (Submission)

```
invoiceId={id}
&lineId={id}                    // edit mode only
&description={text}
&subtitle={text}
&quantity={decimal}
&unitPrice={decimal}
&vatRate={decimal}
&discount={decimal}
&discountType={Percentage|Fixed}
&costPrice={decimal}
&productCode={text}
&isReverseCharge={true|false}
```

Sent as `application/x-www-form-urlencoded` with headers:
- `RequestVerificationToken: {token}`
- `X-Requested-With: XMLHttpRequest`

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Line total computation correctness

*For any* line item with quantity > 0, unit price ≥ 0, discount ≥ 0, and discount type in {Percentage, Fixed}, the computed line total SHALL equal `quantity × unitPrice − discountAmount` where discountAmount is `unitPrice × quantity × (discount / 100)` for Percentage type, or `discount` for Fixed type.

**Validates: Requirements 1.6**

### Property 2: Discount display formatting

*For any* line item, if the discount value equals 0 the discount column SHALL render a dash character (`-`) regardless of all other field values; if the discount value is greater than 0 and discountType is "Percentage" the discount column SHALL render `quantity × unitPrice × (discount / 100)` formatted as a currency value with a minus prefix; if the discount value is greater than 0 and discountType is "Fixed" the discount column SHALL render the discount value formatted as a currency value with a minus prefix.

**Validates: Requirements 1.3, 1.4, 1.5**

### Property 3: Modal pre-population round-trip

*For any* line item with arbitrary field values stored as data attributes on a table row, opening the Edit modal SHALL produce form field values identical to the source data attributes for all fields: description, subtitle, reference URL, quantity, unit price, VAT%, discount, discount type, cost price, reverse charge state, and product code.

**Validates: Requirements 2.1**

### Property 4: Reverse charge toggle round-trip

*For any* initial VAT rate value, checking the Reverse Charge checkbox SHALL set the VAT% field to 0 and make it read-only; subsequently unchecking the Reverse Charge checkbox SHALL restore the VAT% field to its previous value and make it editable.

**Validates: Requirements 2.5, 2.6**

### Property 5: Whitespace description rejection

*For any* string composed entirely of whitespace characters (spaces, tabs, newlines), attempting to submit the modal form SHALL be prevented and an inline validation message SHALL be displayed.

**Validates: Requirements 2.8, 6.4**

### Property 6: Sort order invariant

*For any* collection of line items with distinct SortOrder values, the rendered table rows SHALL appear in strictly ascending SortOrder sequence.

**Validates: Requirements 1.7**

### Property 7: Catalog autocomplete field population

*For any* catalog item with arbitrary non-null values for description, unit price, cost price, VAT rate, and product code, selecting that item from the autocomplete SHALL populate the modal's Description, Unit Price, Cost Price, VAT%, and Product Code fields with the exact corresponding catalog values.

**Validates: Requirements 3.12**

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| AJAX save fails (network error, timeout) | `BlockUI.hide()` → `Swal.fire({ icon: 'error', title: 'Error', text: 'The request timed out. Please try again.' or 'Unable to reach the server. Check your connection.', confirmButtonColor: '#0D5EA6' })`. Modal remains open. |
| AJAX save returns `{ success: false }` | `BlockUI.hide()` → `Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' })`. Modal remains open. |
| AJAX remove fails | `BlockUI.hide()` → `Swal.fire({ icon: 'error', title: 'Error', text: data.message or fallback, confirmButtonColor: '#0D5EA6' })`. |
| Form validation (required fields) | Inline validation message below the Description field. Modal stays open, no fetch call made. |
| Antiforgery token missing/expired | Server returns 400 → caught as `!response.ok` or JSON parse error → error Swal displayed. |
| Quantity ≤ 0 | HTML5 `min` attribute prevents submission. Inline validation if custom validation is triggered. |

**Timeout handling:** The fetch call uses an `AbortController` with a 30-second timeout. On abort, a specific timeout message is shown via SweetAlert2.

**No native dialogs:** All error feedback uses SweetAlert2. The implementation removes all existing `alert()` calls from the addLine and updateLine functions.

## Testing Strategy

### Unit Tests (Example-Based)

| Test | What it verifies |
|------|------------------|
| Table renders correct column count | 7 columns per row |
| Edit button opens modal with correct title | "Edit Line Item" + subtitle |
| Add button opens modal with correct title | "Add Line Item" + subtitle |
| Add mode pre-fills defaults | qty=1, discount=0, discountType=Percentage |
| Cancel closes modal without triggering fetch | No network call made |
| Advanced section collapsed by default | Reverse Charge not visible until expanded |
| Empty state displayed when no lines | Muted message rendered |
| Remove button shows Swal confirm before AJAX | Swal.fire called with correct params |
| Modal overlay click closes modal | Event listener on background |
| Escape key closes modal | Keydown listener on document |
| No native alert() calls in JS module | window.alert not invoked |
| Save Changes button uses correct endpoint | `/Invoice/UpdateLine` for edit mode |
| Add Line button uses correct endpoint | `/Invoice/AddLine` for add mode |
| BlockUI.show called before fetch | Ordering verified via mocks |
| BlockUI.hide called after response | Always called in both success and error paths |

### Property-Based Tests

Property-based testing applies to the pure computation and DOM-manipulation functions extracted for this feature:

- **Library**: fast-check (JavaScript)
- **Minimum iterations**: 100

| Property | Tag |
|----------|-----|
| Line total computation | `Feature: invoice-edit-modal-lines, Property 1: Line total equals qty × unitPrice − discountAmount` |
| Discount display formatting | `Feature: invoice-edit-modal-lines, Property 2: Zero discount shows dash, positive shows formatted amount with minus prefix` |
| Modal pre-population round-trip | `Feature: invoice-edit-modal-lines, Property 3: Edit button populates modal fields matching data attributes` |
| Reverse charge VAT toggle | `Feature: invoice-edit-modal-lines, Property 4: Check sets VAT to 0 readonly, uncheck restores previous value` |
| Whitespace description rejection | `Feature: invoice-edit-modal-lines, Property 5: Whitespace-only descriptions are rejected` |
| Sort order invariant | `Feature: invoice-edit-modal-lines, Property 6: Table rows appear in ascending SortOrder` |
| Catalog autocomplete population | `Feature: invoice-edit-modal-lines, Property 7: Selected catalog item populates all target fields` |

### Integration Tests

| Test | Scope |
|------|-------|
| AddLine endpoint accepts modal form data | POST with all fields → returns `{ success: true }` |
| UpdateLine endpoint accepts modal form data | POST with changed fields → returns `{ success: true }` |
| RemoveLine endpoint removes line | POST with lineId → line no longer in DB |
| Request includes correct headers | `RequestVerificationToken` and `X-Requested-With` present |
| Form data uses correct content type | `application/x-www-form-urlencoded` |

### Manual Testing

- Visual regression: compare table layout across 0, 1, 5, 10+ line items
- Modal responsiveness on mobile viewport
- Keyboard navigation: Tab through modal fields, Escape to close
- Catalog autocomplete within modal description field (add mode only)
- Verify Invoice Create view is unchanged
- Verify Quotation Edit view is unchanged
