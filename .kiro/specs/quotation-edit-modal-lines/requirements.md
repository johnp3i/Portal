# Requirements Document

## Introduction

This feature redesigns the Quotation Edit view's line item management from inline forms to a modal-based approach. Currently, every line item displays all its fields (description, subtitle, reference URL, qty, unit price, VAT%, discount, discount type, cost price, reverse charge) inline simultaneously. With 4+ items, the page becomes visually dense and overwhelming. The new design shows line items as a compact, scannable table and opens a modal for full-form editing or creation.

## Glossary

- **Line_Item_Table**: A compact HTML table within a section card that displays line items with columns for row number, description, subtitle, quantity, unit price, discount, total, and action buttons
- **Line_Item_Modal**: A centered overlay dialog containing the full line item form (all fields) used for both adding and editing line items
- **Section_Card**: A card container representing a named section or the General section, displaying its line items table and metadata
- **General_Section**: The default section that always appears at the bottom of the sections list and contains unassigned line items
- **Quotation_Edit_View**: The Razor view at `/Quotation/Edit/{id}` where users manage quotation header details, sections, and line items
- **Line_Save_Module**: The existing `quotation-line-save.js` AJAX interception module that handles AddLine and UpdateLine form submissions via fetch
- **Section_Summary**: A brief metadata line below the section title showing item count and subtotal for that section
- **Catalog_Autocomplete**: The existing typeahead system that suggests line items from the product catalog when typing in the description field

## Requirements

### Requirement 1: Compact Line Item Table Display

**User Story:** As a quotation author, I want to see line items as a compact table within each section card, so that I can scan all items quickly without visual overload.

#### Acceptance Criteria

1. WHEN the Quotation_Edit_View is rendered, THE Section_Card SHALL display line items in a Line_Item_Table with columns: row number, description (with subtitle below), quantity, unit price, discount, total, and actions (Edit button, Remove button)
2. THE Line_Item_Table SHALL display the description in bold font weight and the subtitle in a smaller muted style below the description within the same cell
3. WHEN a line item has no discount applied, THE Line_Item_Table SHALL display a dash character in the discount column
4. WHEN a line item has a discount applied, THE Line_Item_Table SHALL display the calculated discount amount with a green colour and minus prefix
5. THE Line_Item_Table SHALL display the computed line total (quantity × unit price − discount) in bold font weight in the total column

### Requirement 2: Edit Line Item Modal

**User Story:** As a quotation author, I want to click "Edit" on a line item to open a modal with the full form, so that I can update all fields without cluttering the page.

#### Acceptance Criteria

1. WHEN the user clicks the Edit button on a line item row, THE Quotation_Edit_View SHALL open the Line_Item_Modal pre-populated with that line item's current values
2. THE Line_Item_Modal SHALL display the title "Edit Line Item" and a subtitle "Update the details for this line item."
3. THE Line_Item_Modal SHALL present fields in this layout: description (full width, required), subtitle and reference URL (2-column row), quantity, unit price, VAT%, and cost price (4-column row), discount, discount type, and move-to-section selector (3-column row)
4. THE Line_Item_Modal SHALL include an Advanced section containing a Reverse Charge checkbox labelled "Reverse Charge (VAT accounted by buyer)"
5. WHEN the Reverse Charge checkbox is checked, THE Line_Item_Modal SHALL set the VAT% field to 0 and make the VAT% field read-only
6. THE Line_Item_Modal SHALL display a "Save Changes" primary button and a "Cancel" secondary button in the footer
7. WHEN the user clicks "Save Changes", THE Line_Save_Module SHALL submit the form data via AJAX to the existing UpdateLine endpoint following the BlockUI → fetch → response → feedback pattern
8. WHEN the UpdateLine request succeeds, THE Line_Item_Modal SHALL close and the page SHALL reload to reflect changes
9. WHEN the user clicks "Cancel" or clicks the modal overlay background, THE Line_Item_Modal SHALL close without saving

### Requirement 3: Add Line Item Modal

**User Story:** As a quotation author, I want to click "+ Add Line Item" to open a modal with an empty form, so that I can add new items without inline form clutter.

#### Acceptance Criteria

1. WHEN the user clicks the "+ Add Line Item" button within a Section_Card, THE Quotation_Edit_View SHALL open the Line_Item_Modal in add mode with empty fields
2. THE Line_Item_Modal in add mode SHALL display the title "Add Line Item" and a subtitle "Add a new item to this section."
3. THE Line_Item_Modal in add mode SHALL pre-fill quantity with 1, VAT% with the business default, and discount with 0
4. THE Line_Item_Modal in add mode SHALL display an "Add Line" green button and a "Cancel" secondary button in the footer
5. WHEN the user clicks "Add Line", THE Line_Save_Module SHALL submit the form data via AJAX to the existing AddLine endpoint following the BlockUI → fetch → response → feedback pattern
6. WHEN the AddLine request succeeds, THE Line_Item_Modal SHALL close and the page SHALL reload to show the new line item
7. THE Line_Item_Modal in add mode SHALL include the hidden ProposalSectionId field set to the section from which "+ Add Line Item" was clicked
8. THE Line_Item_Modal in add mode SHALL support the Catalog_Autocomplete on the description field to suggest items from the product catalog

### Requirement 4: Section Card Layout with Summary

**User Story:** As a quotation author, I want each section card to show a summary (item count, subtotal) and section management controls, so that I have context at a glance.

#### Acceptance Criteria

1. THE Section_Card SHALL display a section header row containing: section name (bold, large), Section_Summary text, and action buttons (reorder up/down, Edit Section, Remove)
2. THE Section_Summary SHALL display the item count and subtotal in the format "{count} item(s) · Subtotal {currency}{amount}"
3. WHEN the section has zero line items, THE Section_Summary SHALL display "0 items · Subtotal {currency}0.00"
4. THE Section_Card for named sections SHALL retain existing reorder (↑↓), Edit Section, and Remove buttons with their current behaviour

### Requirement 5: General Section Informational Banner

**User Story:** As a quotation author, I want the General section to show an informational message explaining its behaviour, so that I understand why items appear there and how to reorder them.

#### Acceptance Criteria

1. THE General_Section card SHALL display an informational banner above the Line_Item_Table
2. THE informational banner SHALL contain the text "The General section always appears at the bottom. Create a named section and move items to reorder."
3. THE informational banner SHALL include an info icon and use a muted, non-intrusive visual style
4. THE General_Section card SHALL use a dashed border style and muted background to visually distinguish it from named sections

### Requirement 6: Remove Line Item from Table

**User Story:** As a quotation author, I want to remove a line item directly from the table row, so that I can quickly delete items without opening a modal.

#### Acceptance Criteria

1. WHEN the user clicks the Remove button (×) on a line item row, THE Quotation_Edit_View SHALL display a SweetAlert2 confirmation dialog asking "Remove this line item?"
2. WHEN the user confirms the removal, THE Quotation_Edit_View SHALL submit the removal via AJAX to the existing RemoveLine endpoint following the BlockUI → fetch → response → reload pattern
3. WHEN the RemoveLine request succeeds, THE Quotation_Edit_View SHALL reload the page to reflect the removed line item
4. IF the RemoveLine request fails, THEN THE Quotation_Edit_View SHALL display a SweetAlert2 error message with the failure reason

### Requirement 7: Move Line to Section via Modal

**User Story:** As a quotation author, I want to move a line item to a different section from within the Edit modal, so that I can reorganize items while editing them.

#### Acceptance Criteria

1. THE Line_Item_Modal in edit mode SHALL include a "Move to Section" dropdown listing all available sections including "General"
2. THE "Move to Section" dropdown SHALL display the line item's current section as the selected value
3. WHEN the user changes the section selection and saves, THE Line_Save_Module SHALL call the existing move-line endpoint to reassign the line item to the selected section
4. WHEN the move operation succeeds, THE Quotation_Edit_View SHALL reload the page showing the line item in its new section

### Requirement 8: Preserve Existing AJAX Save Pattern

**User Story:** As a developer, I want the modal-based approach to reuse the existing AJAX save infrastructure, so that no back-end changes are required.

#### Acceptance Criteria

1. THE Line_Item_Modal form submission SHALL use the same endpoint URLs as the current inline forms (AddLine and UpdateLine controller actions)
2. THE Line_Item_Modal form submission SHALL include the antiforgery token in the request
3. THE Line_Item_Modal form submission SHALL follow the established pattern: BlockUI.show → fetch POST → BlockUI.hide → Swal.fire result or page reload
4. IF the AJAX request fails or times out, THEN THE Line_Item_Modal SHALL display a SweetAlert2 error message and remain open so the user does not lose entered data

### Requirement 9: Quotation Edit View Scope

**User Story:** As a product owner, I want this redesign applied only to the Quotation Edit view, so that the Create view remains unchanged until a future iteration.

#### Acceptance Criteria

1. THE modal-based line item management SHALL apply exclusively to the Quotation Edit view at the route `/Quotation/Edit/{id}`
2. THE Quotation Create view SHALL continue using its current line item management approach without modification
