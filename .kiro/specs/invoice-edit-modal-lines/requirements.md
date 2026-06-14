# Requirements Document

## Introduction

This feature redesigns the Invoice Edit view's line item management from inline add/edit forms to a modal-based approach. Currently, the Invoice Edit view uses a hidden "Add" form that expands below the table and inline "Edit" forms that replace table rows. The add and update operations use native `alert()` for error feedback and lack BlockUI coverage, creating an inconsistent experience compared to the remove operation which already uses SweetAlert2 and BlockUI. The new design replaces inline forms with a single shared modal dialog for both adding and editing line items, introduces a compact styled table for scanning line items at a glance, adds product catalog autocomplete in add mode, and standardises all AJAX operations to use the BlockUI → fetch → SweetAlert2 pattern.

## Glossary

- **Line_Item_Table**: A compact HTML table within the line items section that displays invoice lines with columns for row number, description (with subtitle), quantity, unit price, discount display, total, and action buttons
- **Line_Item_Modal**: A centered overlay dialog containing the full line item form (all fields) used for both adding and editing invoice line items
- **Invoice_Edit_View**: The Razor view at `/Invoice/Edit/{id}` where users manage invoice header details, line items, and sections
- **Catalog_Autocomplete**: A typeahead system that suggests line items from the product catalog when typing in the description field during add mode
- **BlockUI_Pattern**: The standard AJAX flow of BlockUI.show → fetch POST → BlockUI.hide → SweetAlert2 feedback or page reload

## Requirements

### Requirement 1: Compact Line Item Table Display

**User Story:** As an invoice author, I want to see line items as a compact styled table, so that I can scan all items quickly without visual clutter from inline forms.

#### Acceptance Criteria

1. WHEN the Invoice_Edit_View is rendered, THE Line_Item_Table SHALL display line items with columns: row number, description (with subtitle below), quantity, unit price, discount, total, and actions (Edit button, Remove button)
2. THE Line_Item_Table SHALL display the description in bold font weight and the subtitle in a smaller muted style below the description within the same cell
3. WHEN a line item has a Discount value equal to 0, THE Line_Item_Table SHALL display a dash character (`-`) in the discount column
4. WHEN a line item has a Discount value greater than 0 and DiscountType is "Percentage", THE Line_Item_Table SHALL display the computed discount amount (quantity × unitPrice × discount / 100) with a green colour and minus prefix, formatted as a currency value
5. WHEN a line item has a Discount value greater than 0 and DiscountType is "Fixed", THE Line_Item_Table SHALL display the fixed discount amount with a green colour and minus prefix, formatted as a currency value
6. THE Line_Item_Table SHALL display the computed line total in bold font weight in the total column, where line total equals (quantity × unitPrice) minus the discount amount as defined in criteria 4 and 5
7. THE Line_Item_Table SHALL order line items by their SortOrder value in ascending order
8. WHEN a section contains zero line items, THE Line_Item_Table SHALL display an empty state message indicating no items have been added, with an option to add a line item

### Requirement 2: Edit Line Item Modal

**User Story:** As an invoice author, I want to click "Edit" on a line item to open a modal with the full form, so that I can update all fields without cluttering the page with inline edit forms.

#### Acceptance Criteria

1. WHEN the user clicks the Edit button on a line item row, THE Invoice_Edit_View SHALL open the Line_Item_Modal pre-populated with that line item's current values for all editable fields: description, subtitle, reference URL, quantity, unit price, VAT%, cost price, discount, discount type, and reverse charge state
2. THE Line_Item_Modal in edit mode SHALL display the title "Edit Line Item" and a subtitle "Update the details for this line item."
3. THE Line_Item_Modal SHALL present fields in this layout: Description (full width, required), Subtitle and Reference URL (2-column row), Quantity, Unit Price, VAT%, and Cost Price (4-column row), Discount and Discount Type (2-column row)
4. THE Line_Item_Modal SHALL include an Advanced section containing a Reverse Charge checkbox labelled "Reverse Charge (VAT accounted by buyer)"
5. WHEN the Reverse Charge checkbox is checked, THE Line_Item_Modal SHALL set the VAT% field to 0 and make the VAT% field read-only with a visually muted appearance indicating it is non-editable
6. WHEN the Reverse Charge checkbox is unchecked, THE Line_Item_Modal SHALL restore the VAT% field to the value it held immediately before the checkbox was checked and make the field editable
7. THE Line_Item_Modal in edit mode SHALL display a "Save Changes" primary button and a "Cancel" secondary button in the footer
8. IF the user clicks "Save Changes" and the Description field is empty, THEN THE Line_Item_Modal SHALL prevent submission and indicate that the Description field is required
9. WHEN the user clicks "Save Changes" and all required fields are valid, THE Invoice_Edit_View SHALL submit the form data via AJAX to the `/Invoice/UpdateLine` endpoint following the BlockUI_Pattern
10. WHEN the UpdateLine request succeeds, THE Line_Item_Modal SHALL close and the page SHALL reload to reflect changes
11. IF the UpdateLine request fails, THEN THE Invoice_Edit_View SHALL display a SweetAlert2 error message with the failure reason and the Line_Item_Modal SHALL remain open so the user does not lose entered data
12. WHEN the user clicks "Cancel", clicks the modal overlay background, or presses the Escape key, THE Line_Item_Modal SHALL close without saving

### Requirement 3: Add Line Item Modal

**User Story:** As an invoice author, I want to click "+ Add Line" to open a modal with an empty form, so that I can add new items without an inline form expanding below the table.

#### Acceptance Criteria

1. WHEN the user clicks the "+ Add Line" button, THE Invoice_Edit_View SHALL open the Line_Item_Modal in add mode with all fields empty except the pre-filled defaults specified in criterion 3
2. THE Line_Item_Modal in add mode SHALL display the title "Add Line Item" and a subtitle "Add a new line item to this invoice."
3. THE Line_Item_Modal in add mode SHALL pre-fill Quantity with 1, Discount with 0, and Discount Type with "Percentage", with all other fields (Description, Unit Price, VAT%, Cost Price, Product Code) empty
4. THE Line_Item_Modal in add mode SHALL display an "Add Line" primary button and a "Cancel" secondary button in the footer
5. WHEN the user clicks "Add Line", THE Invoice_Edit_View SHALL validate that Description is not empty and Quantity is greater than 0 before submitting; IF validation fails, THEN THE Line_Item_Modal SHALL indicate the invalid fields and prevent submission
6. WHEN the form passes validation and the user clicks "Add Line", THE Invoice_Edit_View SHALL submit the form data including the antiforgery token via AJAX POST to the `/Invoice/AddLine` endpoint following the pattern: BlockUI.show → fetch POST → BlockUI.hide → handle response
7. WHEN the AddLine request succeeds, THE Line_Item_Modal SHALL close and the page SHALL reload to show the new line item
8. IF the AddLine request fails or the server returns a non-success response, THEN THE Invoice_Edit_View SHALL display a SweetAlert2 error message with the failure reason and the Line_Item_Modal SHALL remain open so the user does not lose entered data
9. WHEN the user clicks "Cancel" or clicks the modal overlay background, THE Line_Item_Modal SHALL close without submitting any data
10. THE Line_Item_Modal in add mode SHALL include the hidden InvoiceId field set to the current invoice identifier
11. THE Line_Item_Modal in add mode SHALL support the Catalog_Autocomplete on the description field to suggest items from the product catalog
12. WHEN the user selects a catalog item from the Catalog_Autocomplete, THE Line_Item_Modal SHALL populate the Description, Unit Price, Cost Price, VAT%, and Product Code fields with the catalog item values

### Requirement 4: Remove Line Item with Standardised Feedback

**User Story:** As an invoice author, I want the remove operation to continue using SweetAlert2 confirmation and BlockUI, so that all line item operations follow a consistent interaction pattern.

#### Acceptance Criteria

1. WHEN the user clicks the Remove button on a line item row, THE Invoice_Edit_View SHALL display a SweetAlert2 confirmation dialog with the title "Remove this line item?", body text "This action cannot be undone.", a warning icon, a "Yes, remove it" confirm button, and a "Cancel" cancel button
2. IF the user dismisses or cancels the SweetAlert2 confirmation dialog, THEN THE Invoice_Edit_View SHALL take no further action and leave the line item unchanged
3. WHEN the user confirms the removal, THE Invoice_Edit_View SHALL call BlockUI.show, submit a POST request to the `/Invoice/RemoveLine` endpoint with the line item ID and the antiforgery token, and then call BlockUI.hide upon receiving a response
4. WHEN the RemoveLine request returns a success response, THE Invoice_Edit_View SHALL reload the page to reflect the removed line item
5. IF the RemoveLine request returns a failure response, THEN THE Invoice_Edit_View SHALL display a SweetAlert2 error dialog showing the message returned by the server
6. IF the RemoveLine request fails due to a network error or timeout, THEN THE Invoice_Edit_View SHALL call BlockUI.hide and display a SweetAlert2 error dialog indicating that the server could not be reached

### Requirement 5: Replace Native Alert Calls with SweetAlert2

**User Story:** As a developer, I want all error feedback to use SweetAlert2 instead of native `alert()`, so that the Invoice Edit view complies with the application UI notification standards.

#### Acceptance Criteria

1. WHEN an AddLine or UpdateLine AJAX request returns a non-success response (`success: false`), THE Invoice_Edit_View SHALL display a SweetAlert2 dialog with icon `error`, a title of "Error", the server-provided failure message as text, and confirmButtonColor `#0D5EA6`
2. IF an AddLine or UpdateLine AJAX request fails due to a network error or timeout, THEN THE Invoice_Edit_View SHALL display a SweetAlert2 dialog with icon `error`, a title of "Error", a text message indicating an unexpected error occurred, and confirmButtonColor `#0D5EA6`
3. THE Invoice_Edit_View SHALL NOT use native JavaScript `alert()`, `confirm()`, or `prompt()` dialogs for any line item operation including AddLine, UpdateLine, and RemoveLine
4. WHEN the user initiates an AddLine or UpdateLine operation, THE Invoice_Edit_View SHALL call BlockUI.show before the fetch request begins and SHALL call BlockUI.hide after the response is received or an error is caught, before displaying any SweetAlert2 feedback
5. WHEN an AddLine or UpdateLine request succeeds, THE Invoice_Edit_View SHALL call BlockUI.hide and then reload the page without displaying a SweetAlert2 success dialog

### Requirement 6: Modal Form Field Completeness

**User Story:** As an invoice author, I want all InvoiceLine fields available in the modal form, so that I can manage every aspect of a line item from a single dialog.

#### Acceptance Criteria

1. THE Line_Item_Modal SHALL include the following editable fields: Description (text, required, max 500 characters), Subtitle (text, optional, max 1000 characters), Reference URL (text, optional, max 2048 characters), Quantity (number, required, min 0.01), Unit Price (number, required, min 0), VAT Rate (number, 0–99.99), Cost Price (number, optional, min 0), Discount (number, min 0, max 100 when Discount Type is Percentage), Discount Type (select: Percentage or Fixed)
2. THE Line_Item_Modal SHALL include the Reverse Charge checkbox in an Advanced collapsible section that is collapsed by default
3. THE Line_Item_Modal SHALL include a hidden Product Code field that is populated by the Catalog_Autocomplete in add mode
4. IF the user attempts to submit the Line_Item_Modal with a Description that is empty or contains only whitespace characters, THEN THE Line_Item_Modal SHALL display an inline validation message below the Description field and prevent form submission
5. WHEN the user corrects the Description field to contain at least one non-whitespace character, THE Line_Item_Modal SHALL remove the inline validation message and allow form submission

### Requirement 7: Preserve Existing AJAX Endpoint Integration

**User Story:** As a developer, I want the modal-based approach to reuse the existing AJAX endpoints, so that no back-end changes are required.

#### Acceptance Criteria

1. THE Line_Item_Modal form submission SHALL use the existing Invoice controller endpoint URLs: `/Invoice/AddLine` for adding and `/Invoice/UpdateLine` for editing
2. THE Line_Item_Modal form submission SHALL include the antiforgery token in the `RequestVerificationToken` header and SHALL include the `X-Requested-With: XMLHttpRequest` header so the controller identifies the request as AJAX
3. THE Line_Item_Modal form submission SHALL send data as `application/x-www-form-urlencoded` with field names matching the existing endpoint parameter names: description, quantity, unitPrice, vatRate, discount, discountType, costPrice, subtitle, productCode, isReverseCharge, invoiceId, and lineId (for edit mode)
4. THE Line_Item_Modal form submission SHALL include the IsReverseCharge boolean value in the request payload as a string "true" or "false"
5. THE Line_Item_Modal form submission SHALL use an AbortController with a 30-second timeout, and IF the request is aborted due to timeout, THEN THE Line_Item_Modal SHALL display a SweetAlert2 error message indicating the request timed out and SHALL remain open preserving user input

### Requirement 8: Invoice Edit View Scope

**User Story:** As a product owner, I want this redesign applied only to the Invoice Edit view, so that the Invoice Create view and other views remain unchanged.

#### Acceptance Criteria

1. THE modal-based line item management SHALL apply exclusively to the Invoice Edit view at the route `/Invoice/Edit/{id}`
2. THE Invoice Create view at the route `/Invoice/Create` SHALL continue using its current inline line item management approach without modification
3. THE Quotation Edit view at the route `/Quotation/Edit/{id}` and all other document management views SHALL remain unaffected by this change
4. IF a user navigates to the Invoice Create view, THEN THE view SHALL render without referencing the modal-based line item scripts or styles introduced for the Invoice Edit view
