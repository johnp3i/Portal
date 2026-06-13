# Requirements Document

## Introduction

This feature adds inline customer creation capability to the Quotation and Invoice forms. Currently, when a user needs to assign a new customer to a quotation or invoice, they must navigate away to the customer creation page, create the customer, and return. This feature introduces an "Add New" button adjacent to the customer dropdown that opens a modal containing a customer creation form. On successful save via AJAX, the customer dropdown refreshes to include and auto-select the newly created customer — all without a page reload.

## Glossary

- **Inline_Customer_Creation_System**: The subsystem responsible for rendering the customer creation modal, submitting customer data via AJAX, and refreshing the customer dropdown on the Quotation and Invoice forms.
- **Customer_Dropdown**: The `<select>` element on the Quotation and Invoice create/edit forms that lists all active customers belonging to the current business tenant.
- **Add_New_Button**: A button rendered adjacent to the Customer_Dropdown that triggers the customer creation modal.
- **Customer_Modal**: A modal dialog containing the customer creation form fields (Name, ContactPerson, Email, TelephoneNumber, MobileNumber, AddressLine1, AddressLine2, City, PostalCode, Country).
- **CustomerController**: The existing ASP.NET Core MVC controller that manages customer CRUD operations.
- **Customer**: The entity stored in `[customer].[Customer]` representing a client registered under a specific Business tenant.

## Requirements

### Requirement 1: Display Add New Button Adjacent to Customer Dropdown

**User Story:** As a user creating a quotation or invoice, I want to see an "Add New" button next to the customer dropdown, so that I can create a new customer without leaving the form.

#### Acceptance Criteria

1. THE Inline_Customer_Creation_System SHALL display an Add_New_Button immediately to the right of the Customer_Dropdown on the Quotation create form, within the same flex row.
2. THE Inline_Customer_Creation_System SHALL display an Add_New_Button immediately to the right of the Customer_Dropdown on the Quotation edit form, within the same flex row.
3. THE Inline_Customer_Creation_System SHALL display an Add_New_Button immediately to the right of the Customer_Dropdown on the Invoice create form, within the same flex row.
4. THE Inline_Customer_Creation_System SHALL display an Add_New_Button immediately to the right of the Customer_Dropdown on the Invoice edit form, within the same flex row.
5. THE Add_New_Button SHALL use the MyChair Design System styling consistent with secondary action buttons (btn btn-secondary).

### Requirement 2: Open Customer Creation Modal

**User Story:** As a user, I want clicking the "Add New" button to open a modal with a customer form, so that I can enter customer details without navigating away.

#### Acceptance Criteria

1. WHEN the user clicks the Add_New_Button, THE Inline_Customer_Creation_System SHALL open the Customer_Modal as an overlay on the current page with a semi-transparent backdrop that prevents interaction with the underlying form.
2. THE Customer_Modal SHALL contain input fields for: Name (required, max 200 characters), ContactPerson (max 200 characters), Email (max 200 characters), TelephoneNumber (max 30 characters), MobileNumber (max 30 characters), AddressLine1 (max 200 characters), AddressLine2 (max 200 characters), City (max 100 characters), PostalCode (max 20 characters), and Country (max 100 characters).
3. THE Customer_Modal SHALL display a "Save" button and a "Cancel" button.
4. THE Customer_Modal SHALL follow the existing modal styling patterns of the MyChair Design System with border-radius of 24px, padding of 32px, a max-width of 460px, and a box-shadow consistent with other Portal modals.
5. WHEN the Customer_Modal opens, THE Inline_Customer_Creation_System SHALL clear all form fields to ensure a fresh state and set keyboard focus to the Name input field.
6. WHEN the user presses the Escape key while the Customer_Modal is open, THE Inline_Customer_Creation_System SHALL close the modal without making any server request.

### Requirement 3: Client-Side Validation in Modal

**User Story:** As a user, I want immediate feedback when required fields are missing, so that I can correct the form before submitting.

#### Acceptance Criteria

1. WHEN the user clicks "Save" in the Customer_Modal without entering a Name (empty or whitespace-only), THE Inline_Customer_Creation_System SHALL display an inline validation message adjacent to the Name field indicating that Name is required, and prevent form submission.
2. IF the user provides a non-empty Email value that does not match a valid email format (local-part@domain pattern), THEN THE Inline_Customer_Creation_System SHALL display an inline validation message adjacent to the Email field indicating the email format is invalid, and prevent form submission.
3. WHEN the user corrects a field that previously failed validation and clicks "Save" again, THE Inline_Customer_Creation_System SHALL clear the previous validation message for that field before re-evaluating.
4. IF the user leaves the Email field empty, THEN THE Inline_Customer_Creation_System SHALL not display a validation error for Email since it is an optional field.

### Requirement 4: Submit Customer via AJAX

**User Story:** As a user, I want the new customer to be saved without a page reload, so that my in-progress quotation or invoice form data is preserved.

#### Acceptance Criteria

1. WHEN the user clicks "Save" in the Customer_Modal with valid data, THE Inline_Customer_Creation_System SHALL block the UI using BlockUI with the message "Creating customer...", preventing all user interaction until the request completes.
2. WHEN the user clicks "Save" in the Customer_Modal with valid data, THE Inline_Customer_Creation_System SHALL submit the customer data to the CustomerController via a POST AJAX request including the antiforgery token.
3. WHEN the server returns a success response, THE Inline_Customer_Creation_System SHALL unblock the UI using BlockUI.hide().
4. WHEN the server returns a success response, THE Inline_Customer_Creation_System SHALL close the Customer_Modal.
5. WHEN the server returns a success response, THE Inline_Customer_Creation_System SHALL display a SweetAlert2 success notification confirming the customer was created.
6. IF the server returns an error response, THEN THE Inline_Customer_Creation_System SHALL unblock the UI using BlockUI.hide() and display a SweetAlert2 error notification with the server-provided error message.
7. IF an unexpected network error or server error occurs during submission, THEN THE Inline_Customer_Creation_System SHALL unblock the UI using BlockUI.hide() and display a SweetAlert2 error notification with a generic error message.
8. WHEN the AJAX submission completes (success or failure), THE Inline_Customer_Creation_System SHALL preserve all user-entered data in the underlying quotation or invoice form fields.

### Requirement 5: Refresh Customer Dropdown After Successful Creation

**User Story:** As a user, I want the customer dropdown to automatically include and select the newly created customer, so that I can continue building my quotation or invoice seamlessly.

#### Acceptance Criteria

1. WHEN the server returns a success response containing the new customer Id and Name, THE Inline_Customer_Creation_System SHALL append a new option to the end of the Customer_Dropdown with the returned Id as value and Name as display text.
2. WHEN the new option is appended to the Customer_Dropdown, THE Inline_Customer_Creation_System SHALL set the Customer_Dropdown selected value to the newly created customer Id.
3. WHEN the Customer_Dropdown is refreshed after successful customer creation, THE Inline_Customer_Creation_System SHALL preserve all other form field values the user has entered on the quotation or invoice form without causing a full page reload.
4. IF the server success response does not contain a valid customer Id or Name, THEN THE Inline_Customer_Creation_System SHALL leave the Customer_Dropdown unchanged and display a SweetAlert2 error notification indicating the customer was created but the dropdown could not be updated.

### Requirement 6: Server-Side AJAX Endpoint for Inline Customer Creation

**User Story:** As a system operator, I want a dedicated AJAX endpoint that creates a customer and returns JSON, so that the inline creation modal can function without page navigation.

#### Acceptance Criteria

1. THE CustomerController SHALL expose a POST action that accepts customer form data and returns a JSON response containing a success flag, the new customer Id, and the customer Name.
2. WHEN the AJAX endpoint receives valid customer data, THE CustomerController SHALL create the Customer entity associated with the current business tenant and persist it to the database.
3. IF the AJAX endpoint receives invalid data (missing Name), THEN THE CustomerController SHALL return a JSON error response with a descriptive message without creating a Customer record.
4. THE AJAX endpoint SHALL require authentication and the antiforgery token validation.
5. IF a duplicate customer Name already exists for the same business tenant, THEN THE CustomerController SHALL return a JSON error response indicating the customer name already exists.

### Requirement 7: Close Modal Without Saving

**User Story:** As a user, I want to be able to cancel the customer creation and return to my form, so that I can dismiss the modal if I change my mind.

#### Acceptance Criteria

1. WHEN the user clicks the "Cancel" button in the Customer_Modal, THE Inline_Customer_Creation_System SHALL close the modal without making any server request.
2. WHEN the user clicks outside the Customer_Modal overlay area, THE Inline_Customer_Creation_System SHALL close the modal without making any server request.
3. WHEN the Customer_Modal is closed without saving, THE Inline_Customer_Creation_System SHALL preserve all existing data in the underlying quotation or invoice form.

