# Requirements Document

## Introduction

Convert the quotation line item Save and Add Line buttons from full form POST submissions (which cause page refreshes and lose unsaved changes on other lines) to AJAX fetch() calls. The feature adds a blockUI spinner overlay during save operations, inline success feedback on completion, and error messaging on failure — all without losing form state.

## Glossary

- **Line_Save_Handler**: The client-side JavaScript module that intercepts form submissions and performs AJAX requests for UpdateLine and AddLine operations
- **BlockUI_Overlay**: A full-page or scoped spinner overlay displayed while an AJAX save operation is in progress
- **Success_Indicator**: A brief visual feedback element (e.g., green flash on the saved line card) shown after a successful save
- **Error_Message**: An inline error notification displayed on the affected line card when a save operation fails
- **QuotationController**: The ASP.NET Core MVC controller handling quotation line CRUD operations
- **Line_Card**: The DOM element (`.line-card`) representing a single quotation line item form

## Requirements

### Requirement 1: AJAX Form Submission for UpdateLine

**User Story:** As a user editing a quotation, I want saving a line item to happen without a page refresh, so that my unsaved changes on other lines are preserved.

#### Acceptance Criteria

1. WHEN the user clicks the Save button on a line item form, THE Line_Save_Handler SHALL intercept the form submission and prevent the default POST behaviour
2. WHEN the form submission is intercepted, THE Line_Save_Handler SHALL serialize the form data and submit it via a fetch() POST request to the UpdateLine endpoint
3. WHEN the fetch() request is sent, THE Line_Save_Handler SHALL include the anti-forgery token from the form in the request
4. THE QuotationController SHALL return a JSON response with a success flag and optional error message when the request includes an AJAX header (X-Requested-With: XMLHttpRequest)
5. WHEN the AJAX request completes successfully, THE Line_Save_Handler SHALL leave all other form inputs on the page unchanged

### Requirement 2: AJAX Form Submission for AddLine

**User Story:** As a user adding a new line item, I want the add operation to not lose my unsaved edits on existing lines.

#### Acceptance Criteria

1. WHEN the user clicks the Add Line button, THE Line_Save_Handler SHALL intercept the form submission and prevent the default POST behaviour
2. WHEN the AddLine fetch() request completes successfully, THE Line_Save_Handler SHALL reload the page to render the new line item with its server-assigned ID
3. IF the AddLine fetch() request fails, THEN THE Line_Save_Handler SHALL display the error message inline without reloading the page

### Requirement 3: BlockUI Spinner Overlay

**User Story:** As a user, I want to see a loading indicator while my save is in progress, so that I know the system is working and I don't accidentally double-submit.

#### Acceptance Criteria

1. WHEN a line save or add AJAX request begins, THE BlockUI_Overlay SHALL display a semi-transparent overlay with a spinner over the page
2. WHILE the BlockUI_Overlay is visible, THE BlockUI_Overlay SHALL prevent user interaction with the underlying form elements
3. WHEN the AJAX request completes (success or failure), THE BlockUI_Overlay SHALL be removed from the page
4. IF the AJAX request does not complete within 30 seconds, THEN THE BlockUI_Overlay SHALL be removed and THE Error_Message SHALL display a timeout notification

### Requirement 4: Success Feedback

**User Story:** As a user, I want to see confirmation that my line item was saved successfully, so that I have confidence the data was persisted.

#### Acceptance Criteria

1. WHEN the UpdateLine AJAX request returns a success response, THE Success_Indicator SHALL apply a brief green highlight animation to the saved Line_Card
2. THE Success_Indicator SHALL automatically fade out after 2 seconds without requiring user interaction
3. WHEN the success animation completes, THE Line_Card SHALL return to its normal visual state

### Requirement 5: Error Feedback

**User Story:** As a user, I want to see a clear error message if my save fails, so that I can take corrective action without losing my entered data.

#### Acceptance Criteria

1. IF the AJAX request returns a failure response, THEN THE Error_Message SHALL display the server-provided error text adjacent to the affected Line_Card
2. IF the AJAX request fails due to a network error, THEN THE Error_Message SHALL display a generic connectivity error message
3. WHEN an error is displayed, THE Line_Save_Handler SHALL preserve all current form field values on the page
4. THE Error_Message SHALL remain visible until the user dismisses it or performs another save on the same line

### Requirement 6: Controller JSON Response

**User Story:** As a developer, I want the controller to return JSON for AJAX requests so that the client can handle success and error states programmatically.

#### Acceptance Criteria

1. WHEN the UpdateLine action receives a request with the X-Requested-With header set to XMLHttpRequest, THE QuotationController SHALL return a JSON object containing a boolean success property and an optional message property
2. WHEN the AddLine action receives a request with the X-Requested-With header set to XMLHttpRequest, THE QuotationController SHALL return a JSON object containing a boolean success property and an optional message property
3. WHEN the request does not include the AJAX header, THE QuotationController SHALL continue to return the existing redirect response for backward compatibility
4. IF a validation or business logic error occurs during an AJAX request, THEN THE QuotationController SHALL return a JSON object with success set to false and the error description in the message property

### Requirement 7: Graceful Degradation

**User Story:** As a user with JavaScript disabled or on a degraded connection, I want the forms to still work via traditional POST, so that I am not locked out of saving.

#### Acceptance Criteria

1. WHILE JavaScript is disabled or fails to load, THE line item forms SHALL continue to function as standard HTML form POSTs with page redirect behaviour
2. THE Line_Save_Handler SHALL attach event listeners only after the DOM is fully loaded, preserving the native form action as a fallback
