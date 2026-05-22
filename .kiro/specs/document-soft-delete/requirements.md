# Requirements Document

## Introduction

The Document Soft Delete feature introduces the ability for managers to soft-delete invoices and quotations that are in Draft status. Rather than permanently removing records from the database, a new `IsDeleted` BIT column is added to both the Invoice and Quotation tables. When a document is soft-deleted, it is flagged as deleted and excluded from all listing pages. To prevent accidental deletion, the action requires a two-step confirmation flow using sequential SweetAlert2 dialogs before the operation proceeds.

## Glossary

- **Soft_Delete_Service**: The service layer component responsible for validating eligibility and marking a document as soft-deleted by setting its IsDeleted flag to true.
- **Invoice_Detail_Page**: The MVC view displaying a single invoice's full information including header, sections, line items, and action buttons.
- **Quotation_Detail_Page**: The MVC view displaying a single quotation's full information including header, sections, line items, and action buttons.
- **Invoice_List_Page**: The MVC view displaying the list of invoices for the current business.
- **Quotation_List_Page**: The MVC view displaying the list of quotations for the current business.
- **Draft_Status**: The document status indicating the document has not been finalized. For invoices this is InvoiceStatusTypeId = 1; for quotations this is QuotationStatusTypeId = 1.
- **IsDeleted**: A BIT column on the Invoice and Quotation tables indicating whether the document has been soft-deleted (1 = deleted, 0 = active).
- **First_Confirmation_Dialog**: The initial SweetAlert2 dialog asking the manager to confirm the delete action.
- **Second_Confirmation_Dialog**: A follow-up SweetAlert2 dialog with stronger warning language requiring the manager to confirm a second time before the delete proceeds.

## Requirements

### Requirement 1: Invoice Table Schema Extension

**User Story:** As a system administrator, I want an IsDeleted column on the Invoice table, so that invoices can be soft-deleted without losing data.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a BIT column named IsDeleted on the [invoice].[Invoice] table with a named default constraint [DF_Invoice_IsDeleted] defaulting to 0.
2. THE Portal_Database SHALL enforce that IsDeleted is NOT NULL on the [invoice].[Invoice] table.
3. THE Portal_Database SHALL contain a non-clustered index named [IX_Invoice_BusinessId_IsDeleted] on the [invoice].[Invoice] table covering columns (BusinessId, IsDeleted) to support filtered queries that exclude soft-deleted invoices.

### Requirement 2: Quotation Table Schema Extension

**User Story:** As a system administrator, I want an IsDeleted column on the Quotation table, so that quotations can be soft-deleted without losing data.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a BIT column named IsDeleted on the [quotation].[Quotation] table with a NOT NULL constraint and a default constraint named DF_Quotation_IsDeleted with a value of 0.
2. WHEN the IsDeleted column is added to the [quotation].[Quotation] table, THE Portal_Database SHALL assign a value of 0 to all existing rows.
3. THE Portal_Database SHALL ensure the migration script adding IsDeleted is idempotent, performing no action if the column already exists on the [quotation].[Quotation] table.

### Requirement 3: Invoice Soft Delete Eligibility

**User Story:** As a manager, I want the delete action to only be available for draft invoices, so that finalized or sent invoices cannot be accidentally removed.

#### Acceptance Criteria

1. WHEN the manager is viewing the Invoice_Detail_Page and the invoice has Draft_Status (InvoiceStatusTypeId = 1), THE Invoice_Detail_Page SHALL display a "Delete" button in the action area.
2. WHEN the manager is viewing the Invoice_Detail_Page and the invoice does not have Draft_Status (InvoiceStatusTypeId is not 1), THE Invoice_Detail_Page SHALL NOT display a "Delete" button.
3. WHEN the manager clicks the "Delete" button, THE Invoice_Detail_Page SHALL display a confirmation dialog requiring the manager to confirm or cancel the delete action before submitting the request to the Soft_Delete_Service.
4. WHEN the Soft_Delete_Service receives a delete request for an invoice that has Draft_Status, THE Soft_Delete_Service SHALL mark the invoice as soft-deleted and the invoice SHALL no longer appear in the invoice list.
5. IF the Soft_Delete_Service receives a delete request for an invoice that does not have Draft_Status, THEN THE Soft_Delete_Service SHALL reject the request, preserve the invoice unchanged, and return an error message indicating that only invoices with Draft_Status can be deleted.

### Requirement 4: Quotation Soft Delete Eligibility

**User Story:** As a manager, I want the delete action to only be available for draft quotations, so that finalized or sent quotations cannot be accidentally removed.

#### Acceptance Criteria

1. WHEN the manager is viewing the Quotation_Detail_Page and the quotation has a QuotationStatusTypeId of 1 (Draft), THE Quotation_Detail_Page SHALL display a "Delete" button in the action area.
2. WHEN the manager is viewing the Quotation_Detail_Page and the quotation has a QuotationStatusTypeId other than 1 (Draft), THE Quotation_Detail_Page SHALL NOT display a "Delete" button.
3. WHEN the manager clicks the "Delete" button, THE Quotation_Detail_Page SHALL display a SweetAlert2 confirmation dialog requesting the manager to confirm the deletion before proceeding.
4. WHEN the manager confirms the deletion and the quotation has a QuotationStatusTypeId of 1 (Draft), THE Soft_Delete_Service SHALL mark the quotation as soft-deleted and the quotation SHALL no longer appear in the quotation list.
5. IF the Soft_Delete_Service receives a delete request for a quotation that does not have a QuotationStatusTypeId of 1 (Draft), THEN THE Soft_Delete_Service SHALL reject the request, preserve the quotation unchanged, and return an error message indicating that only draft quotations can be deleted.
6. IF the Soft_Delete_Service receives a delete request for a quotation that no longer exists or has already been soft-deleted, THEN THE Soft_Delete_Service SHALL reject the request and return an error message indicating the quotation is unavailable.

### Requirement 5: Multi-Step Confirmation for Invoice Deletion

**User Story:** As a manager, I want to be asked twice before deleting an invoice, so that I cannot accidentally delete a document with a single misclick.

#### Acceptance Criteria

1. WHEN the manager clicks the "Delete" button on the Invoice_Detail_Page, THE Invoice_Detail_Page SHALL display the First_Confirmation_Dialog with the title "Are you sure?" and a message explaining that the invoice will be deleted.
2. WHEN the manager confirms the First_Confirmation_Dialog, THE Invoice_Detail_Page SHALL display the Second_Confirmation_Dialog with the title "Final Warning" and a message stating this action cannot be easily undone.
3. WHEN the manager cancels the First_Confirmation_Dialog, THE Invoice_Detail_Page SHALL take no further action and remain on the current page.
4. WHEN the manager cancels the Second_Confirmation_Dialog, THE Invoice_Detail_Page SHALL take no further action and remain on the current page.
5. WHEN the manager confirms the Second_Confirmation_Dialog, THE Invoice_Detail_Page SHALL block the UI using BlockUI, send a soft-delete request to the Soft_Delete_Service, unblock the UI upon receiving a response, display a success confirmation message, and redirect the manager away from the deleted invoice's detail page.
6. IF the soft-delete request to the Soft_Delete_Service fails or returns an error, THEN THE Invoice_Detail_Page SHALL unblock the UI using BlockUI, display an error message indicating the invoice could not be deleted, and remain on the current page with the invoice data unchanged.

### Requirement 6: Multi-Step Confirmation for Quotation Deletion

**User Story:** As a manager, I want to be asked twice before deleting a quotation, so that I cannot accidentally delete a document with a single misclick.

#### Acceptance Criteria

1. WHEN the manager clicks the "Delete" button on the Quotation_Detail_Page, THE Quotation_Detail_Page SHALL display the First_Confirmation_Dialog with the title "Are you sure?" and a message that includes the quotation reference and states the quotation will be deleted.
2. WHEN the manager confirms the First_Confirmation_Dialog, THE Quotation_Detail_Page SHALL display the Second_Confirmation_Dialog with a message stating this action cannot be easily undone and requiring the manager to confirm a second time.
3. WHEN the manager cancels the First_Confirmation_Dialog, THE Quotation_Detail_Page SHALL close the dialog, take no further action, and remain on the current page with the quotation unchanged.
4. WHEN the manager cancels the Second_Confirmation_Dialog, THE Quotation_Detail_Page SHALL close the dialog, take no further action, and remain on the current page with the quotation unchanged.
5. WHEN the manager confirms the Second_Confirmation_Dialog, THE Quotation_Detail_Page SHALL display BlockUI with a processing message and send a soft-delete request to the Soft_Delete_Service.
6. WHEN the Soft_Delete_Service returns a success response, THE Quotation_Detail_Page SHALL hide BlockUI, display a success notification confirming the quotation was deleted, and redirect the manager to the Quotation_List_Page.
7. IF the Soft_Delete_Service returns an error response or the request fails, THEN THE Quotation_Detail_Page SHALL hide BlockUI and display an error notification indicating the deletion could not be completed.

### Requirement 7: Invoice Soft Delete Execution

**User Story:** As a manager, I want the delete operation to mark the invoice as deleted without permanently removing it, so that data can be recovered if needed.

#### Acceptance Criteria

1. WHEN the Soft_Delete_Service receives a valid invoice soft-delete request, THE Soft_Delete_Service SHALL set the IsDeleted column to 1 and update the UpdatedAtUtc column to the current UTC timestamp on the target invoice record within a single atomic operation so that both columns are updated together or neither is modified.
2. IF the target invoice does not exist, THEN THE Soft_Delete_Service SHALL reject the request and return an error message indicating the invoice was not found.
3. IF the target invoice does not belong to the current business, THEN THE Soft_Delete_Service SHALL reject the request and return an error message indicating the invoice does not belong to the business.
4. IF the target invoice already has IsDeleted equal to 1, THEN THE Soft_Delete_Service SHALL reject the request and return an error message indicating the invoice has already been deleted.
5. IF the soft-delete database operation fails due to a database error, THEN THE Invoice_Detail_Page SHALL hide BlockUI and display a SweetAlert2 error dialog with the failure message, and the invoice record SHALL remain unchanged.

### Requirement 8: Quotation Soft Delete Execution

**User Story:** As a manager, I want the delete operation to mark the quotation as deleted without permanently removing it, so that data can be recovered if needed.

#### Acceptance Criteria

1. WHEN the Soft_Delete_Service receives a valid quotation soft-delete request, THE Soft_Delete_Service SHALL set the IsDeleted column to 1 and update the UpdatedAtUtc column to the current UTC timestamp on the target quotation record in a single atomic operation so that both columns are updated together or neither is modified.
2. IF the target quotation does not exist, does not belong to the current business, or already has IsDeleted equal to 1, THEN THE Soft_Delete_Service SHALL reject the request and return an error message indicating the reason the quotation cannot be deleted.
3. IF the soft-delete operation fails due to a database or infrastructure error, THEN THE Quotation_Detail_Page SHALL hide BlockUI and display a SweetAlert2 error dialog with the failure message.
4. WHEN the Soft_Delete_Service successfully soft-deletes the quotation, THE Soft_Delete_Service SHALL return a success response to the Quotation_Detail_Page.

### Requirement 9: Listing Page Filtering

**User Story:** As a manager, I want soft-deleted documents hidden from listing pages, so that my active document lists remain clean and relevant.

#### Acceptance Criteria

1. WHEN the Invoice_List_Page loads, THE Invoice_List_Page SHALL display only invoices where IsDeleted equals 0 for the current business.
2. WHEN the Quotation_List_Page loads, THE Quotation_List_Page SHALL display only quotations where IsDeleted equals 0 for the current business.
3. THE Invoice_List_Page SHALL NOT display any invoice where IsDeleted equals 1.
4. THE Quotation_List_Page SHALL NOT display any quotation where IsDeleted equals 1.
5. WHEN the Invoice_List_Page applies status, financial status, or customer filters, THE Invoice_List_Page SHALL apply those filters only to invoices where IsDeleted equals 0.
6. WHEN the Quotation_List_Page applies status, customer, or date range filters, THE Quotation_List_Page SHALL apply those filters only to quotations where IsDeleted equals 0.
7. IF the current business has no invoices where IsDeleted equals 0, THEN THE Invoice_List_Page SHALL display an empty list with zero records.

### Requirement 10: Post-Deletion Navigation

**User Story:** As a manager, I want to be redirected to the document list after a successful deletion, so that I am not left viewing a deleted document.

#### Acceptance Criteria

1. WHEN the Soft_Delete_Service successfully soft-deletes an invoice, THE system SHALL hide BlockUI and display a SweetAlert2 success dialog with a success icon indicating the invoice has been deleted.
2. WHEN the manager dismisses the success dialog after invoice deletion (by clicking the confirm button or closing the dialog), THE system SHALL redirect the manager to the Invoice_List_Page.
3. WHEN the Soft_Delete_Service successfully soft-deletes a quotation, THE system SHALL hide BlockUI and display a SweetAlert2 success dialog with a success icon indicating the quotation has been deleted.
4. WHEN the manager dismisses the success dialog after quotation deletion (by clicking the confirm button or closing the dialog), THE system SHALL redirect the manager to the Quotation_List_Page.
5. IF the Soft_Delete_Service fails to soft-delete an invoice or quotation, THEN THE system SHALL hide BlockUI and display a SweetAlert2 error dialog with the failure message, and the manager SHALL remain on the current Detail Page.
