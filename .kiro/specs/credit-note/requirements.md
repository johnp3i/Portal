# Requirements Document

## Introduction

The Credit Note module enables businesses to issue formal credits against existing invoices. A credit note reduces the outstanding balance on an invoice (similar to a payment) and adjusts Output VAT in the assigned VAT period. The module supports a full lifecycle (Draft → Issued → Applied → Voided), auto-generated sequential numbering (CN-YYYY-NNNN), line-item detail with VAT calculation, application tracking, PDF preview, and audit logging. All operations are tenant-isolated by BusinessId.

## Glossary

- **Credit_Note_Module**: The MVC module providing credit note issuance, lifecycle management, application, and reporting within the Portal platform.
- **Credit_Note_Controller**: The MVC controller that exposes endpoints for listing, creating, issuing, applying, voiding, and previewing credit notes.
- **Credit_Note_Service**: The service (ICreditNoteService) containing business logic for credit note creation, validation, lifecycle transitions, application, and voiding.
- **Credit_Note_Repository**: The repository handling all data access for credit note and credit note line entities in the `[credit]` schema.
- **Credit_Note**: A financial document issued against a source invoice that formally reduces the amount owed by the customer. Stored in `[credit].[CreditNote]`.
- **Credit_Note_Line**: An individual line item within a credit note specifying the description, quantity, unit price, VAT rate, and line total. Stored in `[credit].[CreditNoteLine]`.
- **Credit_Note_Status_Type**: A reference table defining the lifecycle states of a credit note: Draft (1), Issued (2), Applied (3), Voided (4). Stored in `[credit].[CreditNoteStatusType]`.
- **Credit_Note_Application**: A record tracking when and how a credit note amount was applied against the source invoice's outstanding balance. Stored in `[credit].[CreditNoteApplication]`.
- **Source_Invoice**: The existing invoice (in Issued status) against which a credit note is raised.
- **Outstanding_Balance**: The remaining unpaid amount on an invoice, calculated as TotalAmount minus total payments minus total applied credit notes.
- **VAT_Period**: A VAT submission period to which a credit note is assigned, affecting Output VAT calculations.
- **Financial_Status_Engine**: The existing service (IInvoiceFinancialStatusService) that computes TotalPaid, Outstanding, and FinancialStatus for invoices.
- **Audit_Log**: The existing audit logging infrastructure that records all significant data changes via the EF Core SaveChanges interceptor.

## Requirements

### Requirement 1: Credit Note Creation

**User Story:** As a business user, I want to create a credit note against an existing invoice with line items and a mandatory reason, so that I can formally document credits owed to customers.

#### Acceptance Criteria

1. WHEN the user initiates credit note creation, THE Credit_Note_Module SHALL display a form with: source invoice selection (dropdown of eligible invoices), auto-generated credit note number (read-only), issue date, reason (mandatory textarea, maximum 1000 characters), and editable line items (description limited to 250 characters, quantity, unit price, VAT rate).
2. WHEN a source invoice is selected, THE Credit_Note_Module SHALL display the invoice's outstanding balance and pre-populate the customer name as a read-only field.
3. IF the user attempts to create a credit note against an invoice with InvoiceStatusTypeId not equal to 2 (Issued), THEN THE Credit_Note_Service SHALL reject the request and return an error message indicating that credit notes can only be issued against invoices with Issued status.
4. IF the user attempts to save a credit note with zero line items, THEN THE Credit_Note_Service SHALL reject the request and return an error message indicating that at least one line item is required.
5. IF the user attempts to save a credit note with an empty or whitespace-only reason field, THEN THE Credit_Note_Service SHALL reject the request and return an error message indicating that a reason is required.
6. THE Credit_Note_Service SHALL compute each Credit_Note_Line line total as Quantity multiplied by UnitPrice, where Quantity must be a positive integer between 1 and 10,000 and UnitPrice must be a positive decimal between 0.01 and 999,999.99 with up to two decimal places.
7. THE Credit_Note_Service SHALL compute the credit note Subtotal as the sum of all line totals.
8. THE Credit_Note_Service SHALL compute the credit note TaxAmount by summing (LineTotal multiplied by VatRate divided by 100) for each line, where VatRate must be a decimal between 0 and 100 inclusive.
9. THE Credit_Note_Service SHALL compute the credit note TotalAmount as Subtotal plus TaxAmount.
10. IF the computed TotalAmount exceeds the source invoice's Outstanding Balance (defined as Invoice.TotalAmount minus the sum of non-voided payments minus the sum of TotalAmount from all previously issued credit notes with CreditNoteStatusTypeId not equal to 1 against the same invoice), THEN THE Credit_Note_Service SHALL reject creation and return an error message indicating the credit note total exceeds the available balance.
11. WHEN a credit note is saved, THE Credit_Note_Service SHALL assign it an initial status of Draft (CreditNoteStatusTypeId = 1).
12. THE Credit_Note_Service SHALL scope all credit note records to the current user's BusinessId for tenant isolation.
13. IF the user attempts to save a credit note with more than 50 line items, THEN THE Credit_Note_Service SHALL reject the request and return an error message indicating the maximum number of line items has been exceeded.

### Requirement 2: Credit Note Numbering

**User Story:** As a business user, I want credit notes to have auto-generated sequential numbers in a recognisable format, so that I can easily reference and track them.

#### Acceptance Criteria

1. THE Credit_Note_Service SHALL generate credit note numbers following the pattern CN-YYYY-NNNN, where YYYY is the four-digit year of the issue date and NNNN is a zero-padded sequential number ranging from 0001 to 9999.
2. THE Credit_Note_Service SHALL determine the next sequential number by querying the highest existing credit note number for the same BusinessId and year, then incrementing by one.
3. WHEN no credit notes exist for the current BusinessId and year, THE Credit_Note_Service SHALL assign the number CN-YYYY-0001.
4. THE Credit_Note_Repository SHALL enforce uniqueness of credit note numbers within a BusinessId using a unique filtered index.
5. IF a uniqueness constraint violation occurs during credit note creation, THEN THE Credit_Note_Service SHALL retry the number generation by re-querying the highest existing number for the same BusinessId and year, up to a maximum of 3 attempts, before returning an error indicating the credit note could not be created.
6. IF the sequential number for a BusinessId and year would exceed 9999, THEN THE Credit_Note_Service SHALL reject the creation and return an error indicating the annual credit note limit has been reached.

### Requirement 3: Credit Note Lifecycle

**User Story:** As a business user, I want credit notes to follow a clear lifecycle (Draft → Issued → Applied → Voided), so that I can track their progress and ensure proper financial controls.

#### Acceptance Criteria

1. THE Credit_Note_Status_Type reference table SHALL contain exactly four statuses: Draft (1), Issued (2), Applied (3), Voided (4).
2. WHEN a credit note in Draft status is submitted for issuing, THE Credit_Note_Service SHALL update the status to Issued and record the IssuedAtUtc timestamp.
3. WHEN a credit note in Issued status is fully applied to one or more invoices, THE Credit_Note_Service SHALL update the status to Applied.
4. WHEN a credit note in Draft or Issued status is submitted for voiding, THE Credit_Note_Service SHALL update the status to Voided and record the VoidedAtUtc timestamp.
5. IF a status transition is requested that is not in the allowed set (Draft→Issued, Issued→Applied, Draft→Voided, Issued→Voided), THEN THE Credit_Note_Service SHALL reject the request and return an error response indicating the current status and the disallowed target status.
6. WHILE a credit note is in Applied or Voided status, THE Credit_Note_Service SHALL reject any status transition request and return an error response indicating the credit note is in a terminal state.
7. WHILE a credit note is in Draft status, THE Credit_Note_Service SHALL allow editing of line items, reason, and issue date.
8. IF an edit is attempted on a credit note in Issued, Applied, or Voided status, THEN THE Credit_Note_Service SHALL reject the edit request and return an error response indicating that the credit note is not editable in its current status.

### Requirement 4: Credit Note Application

**User Story:** As a business user, I want to apply an issued credit note against its source invoice, so that the invoice's outstanding balance is reduced accordingly.

#### Acceptance Criteria

1. WHEN the user applies a credit note, THE Credit_Note_Service SHALL create a Credit_Note_Application record linking the credit note to the source invoice with the applied amount, application date, and applying user.
2. IF the credit note status is not Issued (CreditNoteStatusTypeId != 2), THEN THE Credit_Note_Service SHALL reject the application with a validation error indicating that only credit notes in Issued status may be applied.
3. THE Credit_Note_Service SHALL apply the full TotalAmount of the credit note against the source invoice in a single application (partial application is not supported).
4. WHEN a credit note is applied, THE Credit_Note_Service SHALL transition the credit note status to Applied (CreditNoteStatusTypeId = 3).
5. WHEN a credit note is applied, THE Financial_Status_Engine SHALL recalculate the source invoice's outstanding balance as TotalAmount minus the sum of non-voided payments minus the sum of all applied credit note amounts for that invoice.
6. WHEN a credit note is applied, THE Financial_Status_Engine SHALL update the source invoice's InvoiceFinancialStatusTypeId based on the recalculated outstanding balance: Paid (3) if outstanding balance equals zero, PartiallyPaid (2) if outstanding balance is greater than zero but less than TotalAmount.
7. IF the credit note TotalAmount exceeds the source invoice's current outstanding balance at the time of application, THEN THE Credit_Note_Service SHALL reject the application with a validation error indicating the credit note amount exceeds the remaining balance.
8. THE Credit_Note_Service SHALL execute the application and financial status update within a single database transaction to ensure atomicity.
9. IF the source invoice has an InvoiceFinancialStatusTypeId of Paid (3), Cancelled, or WrittenOff (5), THEN THE Credit_Note_Service SHALL reject the application with a validation error indicating the invoice is not eligible for credit note application.

### Requirement 5: Credit Note Voiding

**User Story:** As a business user, I want to void a credit note with a two-step confirmation, so that I can reverse incorrect credits while maintaining an audit trail.

#### Acceptance Criteria

1. WHEN the user requests to void a credit note, THE Credit_Note_Module SHALL display a two-step confirmation dialog using SweetAlert2: the first step SHALL present a warning dialog stating the consequences of voiding (including financial reversal if Applied), and the second step SHALL require the user to confirm the destructive action with a danger-coloured confirm button (confirmButtonColor: '#C24A4A').
2. WHEN the user confirms voiding, THE Credit_Note_Service SHALL transition the credit note status to Voided (CreditNoteStatusTypeId = 4) and record the VoidedAtUtc timestamp.
3. WHEN a previously Applied credit note is voided, THE Credit_Note_Service SHALL reverse the financial impact by adding the applied amount back to the source invoice's Outstanding_Balance.
4. WHEN a previously Applied credit note is voided, THE Financial_Status_Engine SHALL recalculate and update the source invoice's InvoiceFinancialStatusTypeId based on the restored Outstanding_Balance (Unpaid if Outstanding_Balance equals TotalAmount, PartiallyPaid if Outstanding_Balance is greater than zero but less than TotalAmount).
5. WHEN a previously Applied credit note is voided, THE Credit_Note_Service SHALL mark the associated Credit_Note_Application record as voided (IsVoided = true).
6. THE Credit_Note_Service SHALL execute the void operation and any financial reversal within a single database transaction.
7. THE Credit_Note_Service SHALL only allow voiding of credit notes in Draft (CreditNoteStatusTypeId = 1), Issued (CreditNoteStatusTypeId = 2), or Applied (CreditNoteStatusTypeId = 3) status.
8. IF the void operation fails due to a database or transaction error, THEN THE Credit_Note_Service SHALL roll back all changes and THE Credit_Note_Module SHALL display an error message indicating that the void operation could not be completed.
9. WHEN a credit note in Draft or Issued status is voided, THE Credit_Note_Service SHALL transition the status to Voided and record VoidedAtUtc without performing any financial reversal.

### Requirement 6: VAT Impact

**User Story:** As a business user, I want credit notes to reduce Output VAT in the assigned VAT period, so that my VAT submissions accurately reflect credits issued.

#### Acceptance Criteria

1. THE Credit_Note SHALL store a mandatory VatSubmissionPeriodId (non-null foreign key to [vat].[VatSubmissionPeriod]) referencing the VAT period to which the credit note's tax impact is assigned.
2. WHEN the VAT submission computation is executed for a period, THE VatSubmissionService SHALL subtract the sum of TaxAmount from all credit notes in Issued or Applied status assigned to that period from the total Output VAT.
3. WHEN the VAT submission computation is executed for a period, THE VatSubmissionService SHALL exclude credit notes in Draft or Voided status from the Output VAT reduction.
4. THE Credit_Note_Module SHALL display a VAT period dropdown during credit note creation, populated with periods belonging to the current BusinessId whose associated VatSubmission record either does not exist or has IsSubmitted equal to false, defaulting to the period with the latest PeriodStartDate among those eligible.
5. IF the user attempts to assign a credit note to a VAT period whose VatSubmission is already marked as submitted (IsSubmitted = true), THEN THE Credit_Note_Service SHALL reject the assignment with a validation error indicating the period is already filed.
6. IF a credit note is issued or applied and its assigned VAT period is subsequently marked as submitted, THEN THE Credit_Note_Service SHALL prevent voiding of that credit note unless the submission is first unmarked.

### Requirement 7: Credit Note List View

**User Story:** As a business user, I want to view a paginated list of credit notes with filters and search, so that I can quickly find and manage credit notes.

#### Acceptance Criteria

1. THE Credit_Note_Module SHALL display a paginated list of credit notes showing: credit note number, customer name, source invoice reference, issue date, total amount (formatted to 2 decimal places with currency symbol), status (as a coloured pill), and reason, sorted by issue date descending (most recent first) by default.
2. THE Credit_Note_Module SHALL provide filter controls for: status (dropdown with All/Draft/Issued/Applied/Voided), customer (dropdown), date range (from/to date inputs), and a text search field (maximum 100 characters, performing case-insensitive contains matching against credit note number and customer name).
3. WHEN the user clicks the Filter button, THE Credit_Note_Controller SHALL return only credit notes matching all active filter criteria (AND logic) for the current BusinessId.
4. IF the applied filters return zero results, THEN THE Credit_Note_Module SHALL display an empty state message within the table card indicating no credit notes match the current filters.
5. THE Credit_Note_Module SHALL display pagination controls showing the current range (e.g., "Showing 1–10 of 25") and total count, with a default page size of 10 records and navigation buttons (Prev, numbered pages, Next).
6. THE Credit_Note_Module SHALL display status pills with the following colour mapping: Draft = gold (#C8912E background tint), Issued = green (#129867 background tint), Applied = blue (#0D5EA6 background tint), Voided = red (#C24A4A background tint).
7. WHEN the user clicks the Clear button, THE Credit_Note_Module SHALL reset all filter controls to their default values (status = All, customer = All, date fields empty, search field empty) and reload the unfiltered list.

### Requirement 8: Credit Note Detail View

**User Story:** As a business user, I want to view full credit note details including line items, totals, and application history, so that I can review all information about a specific credit note.

#### Acceptance Criteria

1. THE Credit_Note_Module SHALL display a detail view showing: credit note number, customer name, status (as a coloured pill using the same colour mapping defined in Requirement 7), issue date, source invoice reference (as a clickable link to the invoice detail), VAT period, and reason.
2. THE Credit_Note_Module SHALL display a line items table showing: description, quantity, unit price, VAT rate, and line total for each Credit_Note_Line.
3. THE Credit_Note_Module SHALL display a totals section showing: subtotal, VAT amount, and credit total (displayed as a negative value prefixed with a minus sign and styled with danger colour #C24A4A).
4. THE Credit_Note_Module SHALL display an application history section showing a table with columns: date applied, invoice reference (as a clickable link to the invoice detail), amount applied, and applying user for each Credit_Note_Application record.
5. IF no Credit_Note_Application records exist for the credit note, THEN THE Credit_Note_Module SHALL display an empty state message within the application history section indicating that no applications have been recorded.
6. WHEN the credit note is in Draft status, THE Credit_Note_Module SHALL display action buttons for "Issue Credit Note", "Edit", and "Void".
7. WHEN the credit note is in Issued status, THE Credit_Note_Module SHALL display action buttons for "Apply to Invoice", "Preview PDF", and "Void".
8. WHEN the credit note is in Applied status, THE Credit_Note_Module SHALL display action buttons for "Preview PDF" and "Void".
9. WHEN the credit note is in Voided status, THE Credit_Note_Module SHALL display no action buttons.
10. WHEN the credit note is fully applied (status = Applied), THE Credit_Note_Module SHALL display a success indicator stating "Fully applied — no remaining balance".

### Requirement 9: KPI Summary Cards

**User Story:** As a business user, I want to see summary metrics at the top of the credit notes list, so that I can quickly assess credit note activity for the current month.

#### Acceptance Criteria

1. THE Credit_Note_Module SHALL display three KPI cards in a single row above the filter section: "Total Issued", "Total Value", and "Pending Application", rendered using a three-column equal-width grid with 18px gap between cards.
2. THE "Total Issued" KPI card SHALL display the count of credit notes with status Issued or Applied where CreatedAtUtc falls within the current calendar month (1st 00:00:00 UTC to current moment) for the current BusinessId, with a left border colour of #0D5EA6 (primary blue), the count displayed as a whole integer, and subtitle "This month".
3. THE "Total Value" KPI card SHALL display the sum of TotalAmount for all credit notes with status Issued or Applied where CreatedAtUtc falls within the current calendar month for the current BusinessId, formatted with the business currency symbol, two decimal places, and thousands separators (e.g., "€4,280.00"), with a left border colour of #C24A4A (danger red) and subtitle "Credits issued this month".
4. THE "Pending Application" KPI card SHALL display the count of credit notes with status Issued (not yet applied) for the current BusinessId regardless of creation date, with a left border colour of #C8912E (warning amber), the count displayed as a whole integer, and subtitle "Not yet applied to invoices".
5. IF no credit notes match the filter criteria for a KPI card, THEN THE Credit_Note_Module SHALL display "0" for count-based cards (Total Issued, Pending Application) and the business currency symbol followed by "0.00" for the value card (Total Value).
6. IF the KPI data fails to load, THEN THE Credit_Note_Module SHALL display a dash character ("—") in place of each KPI value and display an error message indicating that summary data could not be retrieved.

### Requirement 10: PDF Preview

**User Story:** As a business user, I want to preview and download a credit note as a PDF document, so that I can share it with customers or keep it for records.

#### Acceptance Criteria

1. WHEN the user clicks "Preview PDF" on a credit note in Issued or Applied status, THE Credit_Note_Module SHALL generate a PDF document containing: business details (name, address, VAT number), customer details, credit note number, issue date, source invoice reference, reason, line items table, and totals.
2. IF the credit note is not in Issued or Applied status, THEN THE Credit_Note_Module SHALL not display the "Preview PDF" action for that credit note.
3. THE Credit_Note_Module SHALL render the PDF using the same HTML-to-PDF approach used by the existing Customer Statement PDF export, with a generation timeout of 30 seconds.
4. THE PDF file SHALL be named using the pattern `CreditNote_{CreditNoteNumber}.pdf` (e.g., `CreditNote_CN-2026-0012.pdf`).
5. WHEN the PDF is generated successfully, THE Credit_Note_Module SHALL return the PDF as a file download to the browser.
6. IF PDF generation fails or times out, THEN THE Credit_Note_Module SHALL display an error message indicating that the PDF could not be generated and prompt the user to try again.

### Requirement 11: Audit Logging

**User Story:** As a business user, I want all credit note actions to be logged in the audit trail, so that I can track who did what and when for compliance purposes.

#### Acceptance Criteria

1. WHEN a credit note is created, THE Audit_Log SHALL record an entry with Action set to "CreditNoteCreated", the credit note Id as RecordId, the credit note number and creating user's UserId in NewValues, the BusinessId, and a UTC timestamp.
2. WHEN a credit note status changes, THE Audit_Log SHALL record an entry with Action set to "CreditNoteStatusChanged", the credit note Id as RecordId, the previous status in OldValues, the new status and acting user's UserId in NewValues, the BusinessId, and a UTC timestamp.
3. WHEN a credit note is applied to an invoice, THE Audit_Log SHALL record an entry with Action set to "CreditNoteApplied", the credit note Id as RecordId, the invoice Id, applied amount, and acting user's UserId in NewValues, the BusinessId, and a UTC timestamp.
4. WHEN a credit note is voided after application, THE Audit_Log SHALL record an entry with Action set to "CreditNoteReversed", the credit note Id as RecordId, the invoice Id, reversed amount, and acting user's UserId in NewValues, the BusinessId, and a UTC timestamp.
5. THE Credit_Note_Module SHALL record entity-level field changes (inserts, updates, deletes) automatically via the existing EF Core SaveChanges interceptor, and SHALL write explicit audit log entries for the business events defined in criteria 1 through 4.

### Requirement 12: Validation Rules

**User Story:** As a business user, I want the system to enforce validation rules that prevent invalid credit notes, so that financial integrity is maintained.

#### Acceptance Criteria

1. IF the user attempts to create a credit note against an invoice that is not in Issued status (InvoiceStatusTypeId ≠ 2), THEN THE Credit_Note_Service SHALL reject the request with a validation message indicating that credit notes can only be raised against invoices in Issued status.
2. IF the user attempts to save a credit note with zero line items, THEN THE Credit_Note_Service SHALL reject the request with a validation message stating that at least one line item is required.
3. IF the user attempts to save a credit note with an empty or whitespace-only reason field, THEN THE Credit_Note_Service SHALL reject the request with a validation message stating that a reason is required.
4. IF the computed credit note TotalAmount exceeds the source invoice's Outstanding_Balance, THEN THE Credit_Note_Service SHALL reject the request with a validation message displaying the maximum allowable amount (the current Outstanding_Balance value).
5. IF the user enters a line item with a quantity less than or equal to zero or greater than 999,999, THEN THE Credit_Note_Service SHALL reject the line item with a validation message indicating the allowed quantity range (0.0001 to 999,999).
6. IF the user enters a line item with a unit price less than or equal to zero or greater than 999,999,999.99, THEN THE Credit_Note_Service SHALL reject the line item with a validation message indicating the allowed unit price range (0.01 to 999,999,999.99).
7. IF the user enters a line item with a VAT rate below zero or above 100, THEN THE Credit_Note_Service SHALL reject the line item with a validation message indicating the allowed VAT rate range (0 to 100).
8. IF the user enters a line item with an empty or whitespace-only description, THEN THE Credit_Note_Service SHALL reject the line item with a validation message stating that a description is required.
9. THE Credit_Note_Service SHALL enforce a maximum length of 1000 characters on the reason field and a maximum length of 500 characters on each line item description field, rejecting values that exceed these limits with a validation message indicating the maximum allowed length.
10. THE Credit_Note_Service SHALL return all applicable validation errors for a submission in a single response rather than failing on the first error encountered.
