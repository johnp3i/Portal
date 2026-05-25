# Requirements Document

## Introduction

The Customer Statement of Account module provides a per-customer financial summary showing all invoices issued, payments received, and a running balance for a selected period. It enables businesses to generate, view, and export statements that consolidate a customer's transaction history into a single chronological view. The module also adds server-side pagination to the existing Customer Registry list view, which currently loads all customers without paging.

The existing database schema already contains the necessary tables: `[customer].[Customer]`, `[invoice].[Invoice]`, `[invoice].[InvoiceLine]`, and `[revenue].[Payment]`. The Revenue Control module provides the Payment entity, PaymentRepository, PaymentService, and FinancialStatusEngine. This module builds on those foundations to produce statement views and PDF exports.

## Glossary

- **Statement_Service**: The service responsible for computing opening balance, assembling statement lines, and producing the complete statement model for a customer and period
- **Statement_Controller**: The ASP.NET Core MVC controller that handles statement page requests, generation actions, and PDF export
- **Statement_Line**: A single row in the statement representing either an invoice debit, a payment credit, or a balance marker (opening/closing)
- **Opening_Balance**: The sum of all unpaid invoice amounts minus all valid payments for the customer prior to the statement period start date
- **Closing_Balance**: The running balance value at the end of the statement period, equal to Opening_Balance plus total debits minus total credits within the period
- **Running_Balance**: The cumulative balance after each chronological transaction, starting from the Opening_Balance
- **Valid_Payment**: A Payment record where IsVoided = 0
- **Statement_Period**: The date range (from date to to date) selected by the user for statement generation
- **Customer_Registry_Controller**: The existing controller that manages the customer list view with search and filter functionality
- **Page_Size**: The number of records displayed per page, fixed at 15 for the Customer Registry
- **Email_History_Table**: A table displayed on the Statement page showing a chronological record of all statements previously emailed for the selected customer

## Requirements

### Requirement 1: Statement Generation Logic

**User Story:** As a business operator, I want to generate a statement of account for a customer over a selected period, so that I can see all financial activity and the resulting balance in one view.

#### Acceptance Criteria

1. WHEN a statement is requested for a customer and period, THE Statement_Service SHALL compute the Opening_Balance as the sum of Invoice.TotalAmount (where InvoiceStatusTypeId = 2 and IsDeleted = 0) minus the sum of Payment.Amount (where IsVoided = 0) for all invoices belonging to that customer and business with InvoiceDate before the period start date
2. WHEN a statement is requested, THE Statement_Service SHALL retrieve all invoices for the customer with InvoiceDate within the Statement_Period and InvoiceStatusTypeId = 2 (Issued) and IsDeleted = 0, scoped to the requesting user's BusinessId
3. WHEN a statement is requested, THE Statement_Service SHALL retrieve all payments for the customer's invoices with PaymentDateUtc within the Statement_Period and IsVoided = 0, scoped to the requesting user's BusinessId
4. WHEN a statement is requested, THE Statement_Service SHALL produce Statement_Line records sorted chronologically by date, interleaving invoice lines (debit) and payment lines (credit)
5. WHEN a statement is requested, THE Statement_Service SHALL compute the Running_Balance for each Statement_Line by adding the debit amount or subtracting the credit amount from the previous line's Running_Balance, starting from the Opening_Balance
6. WHEN a statement is requested, THE Statement_Service SHALL compute the Closing_Balance as the Running_Balance of the last Statement_Line after all transactions in the period have been processed
7. WHEN a statement is requested, THE Statement_Service SHALL compute summary totals: total invoiced (sum of all invoice TotalAmount values in the period) and total paid (sum of all payment Amount values in the period)
8. WHEN two transactions share the same date, THE Statement_Service SHALL order invoices before payments for that date
9. IF no invoices and no payments exist within the Statement_Period for the selected customer, THEN THE Statement_Service SHALL return an empty statement containing only the Opening_Balance as both the Opening_Balance and Closing_Balance with zero for total invoiced and total paid

### Requirement 2: Statement Line Model

**User Story:** As a business operator, I want each statement line to show consistent transaction details, so that I can identify every charge and payment at a glance.

#### Acceptance Criteria

1. THE Statement_Service SHALL produce each Statement_Line with the following fields: Date (date of the transaction), Type (one of: Opening, Invoice, Payment, or Closing), Reference (string, maximum 200 characters), Description (string, maximum 500 characters), Debit amount (decimal with two decimal places, range 0.00 to 999,999,999.99), Credit amount (decimal with two decimal places, range 0.00 to 999,999,999.99), and Running_Balance (decimal with two decimal places)
2. WHEN the Statement_Line represents an invoice, THE Statement_Service SHALL set the Date to Invoice.InvoiceDate, the Reference to Invoice.InvoiceNumber, the Description to Invoice.Notes, the Debit to Invoice.TotalAmount, and the Credit to 0.00
3. WHEN the Statement_Line represents a payment, THE Statement_Service SHALL set the Date to Payment.PaymentDateUtc, the Reference to PaymentMethodType.Name concatenated with " · Ref: " and Payment.Reference, the Description to Payment.Notes, the Debit to 0.00, and the Credit to Payment.Amount
4. IF Payment.Reference is null or empty WHEN building a payment Statement_Line, THEN THE Statement_Service SHALL set the Reference to PaymentMethodType.Name only, omitting the separator and reference suffix
5. IF Payment.Notes is null or empty WHEN building a payment Statement_Line, THEN THE Statement_Service SHALL set the Description to an empty string
6. THE Statement_Service SHALL include an Opening_Balance line as the first entry with Date set to the statement period start date, Type set to Opening, Reference set to "Balance brought forward", no debit or credit values, and Running_Balance set to the computed Opening_Balance
7. THE Statement_Service SHALL include a Closing_Balance line as the last entry with Date set to the statement period end date, Type set to Closing, Reference set to "Balance carried forward", no debit or credit values, and Running_Balance set to the computed Closing_Balance
8. THE Statement_Service SHALL order all Statement_Lines chronologically by Date between the Opening_Balance and Closing_Balance entries

### Requirement 3: Statement Controller and Page

**User Story:** As a business operator, I want to access the statement page with filter controls, so that I can select a customer and date range to generate a statement.

#### Acceptance Criteria

1. THE Statement_Controller SHALL expose an Index action that renders the statement page with a filter panel containing a customer dropdown, a from-date input, and a to-date input
2. WHEN the Index action is accessed with a CustomerId query parameter, THE Statement_Controller SHALL pre-select that customer in the dropdown
3. WHEN the Generate action is invoked with a valid CustomerId, from-date, and to-date, THE Statement_Controller SHALL call the Statement_Service and return the statement data as a JSON response containing the success flag, opening balance, closing balance, total invoiced, total paid, and a chronologically-ordered list of transaction entries within the specified date range
4. IF the Generate action is invoked without a CustomerId, THEN THE Statement_Controller SHALL return a JSON error response with success set to false and a message indicating that a customer must be selected
5. IF the Generate action is invoked with a from-date that is after the to-date, THEN THE Statement_Controller SHALL return a JSON error response with success set to false and a message indicating an invalid date range
6. IF the Generate action is invoked without a from-date or without a to-date, THEN THE Statement_Controller SHALL return a JSON error response with success set to false and a message indicating that both dates are required
7. THE Statement_Controller SHALL populate the customer dropdown with all active customers belonging to the current business tenant, ordered alphabetically by customer name
8. WHEN the Generate action is invoked with a valid CustomerId that does not belong to the current business tenant, THE Statement_Controller SHALL return a JSON error response with success set to false and a message indicating the customer was not found

### Requirement 4: Statement UI Display

**User Story:** As a business operator, I want to view the generated statement on screen with a header section and transaction table, so that I can review the customer's account status before exporting.

#### Acceptance Criteria

1. THE Statement_Controller SHALL render a statement header section displaying: customer name, customer address, customer contact details, and the Statement_Period dates
2. THE Statement_Controller SHALL render four summary KPI cards showing: Opening_Balance, Total Invoiced (with invoice count), Total Paid (with payment count), and Closing_Balance
3. THE Statement_Controller SHALL render a transaction history table with columns: Date, Type, Reference, Debit (Invoiced), Credit (Paid), and Running Balance
4. THE Statement_Controller SHALL visually distinguish invoice rows from payment rows using colour-coded type pills (gold for Invoice, green for Payment, blue for Opening, red for Closing)
5. THE Statement_Controller SHALL render a period totals footer row showing the sum of debits, sum of credits, and the Closing_Balance
6. WHEN the Closing_Balance is greater than zero, THE Statement_Controller SHALL display the balance value styled as outstanding (red)
7. WHEN the Closing_Balance equals zero, THE Statement_Controller SHALL display the balance value styled as settled (green)

### Requirement 5: PDF Export

**User Story:** As a business operator, I want to download the statement as a PDF document, so that I can send it to the customer or keep it for records.

#### Acceptance Criteria

1. WHEN the Download PDF action is invoked with a valid CustomerId and Statement_Period, THE Statement_Controller SHALL generate a PDF document rendered in landscape orientation containing the same header section, summary KPI values, and transaction history table (including opening balance row, all transaction rows, closing balance row, and period totals footer) as the on-screen view
2. THE Statement_Controller SHALL return the PDF as a file download with content type application/pdf and a filename following the pattern: Statement_{CustomerName}_{FromDate}_{ToDate}.pdf where CustomerName has spaces replaced with underscores and any characters invalid for filenames removed, FromDate and ToDate use the format yyyyMMdd
3. THE Statement_Controller SHALL render the PDF using an HTML-to-PDF approach consistent with the existing rendering pattern in the project
4. IF PDF generation fails or exceeds 30 seconds, THEN THE Statement_Controller SHALL return an error response with a message indicating the nature of the failure and log the failure
5. IF the Download PDF action is invoked with a CustomerId that does not exist or does not belong to the authenticated business tenant, THEN THE Statement_Controller SHALL return an error response indicating that the customer was not found

### Requirement 6: Statement Access Points

**User Story:** As a business operator, I want to access the statement from multiple locations in the application, so that I can quickly generate a statement regardless of where I am working.

#### Acceptance Criteria

1. THE Customer_Registry_Controller SHALL display a "Statement" link in the Actions column of the customer list table for each customer row
2. WHEN the Statement link is clicked from the Customer Registry, THE Customer_Registry_Controller SHALL navigate to the Statement page with the CustomerId pre-populated in the filter
3. THE Revenue_Control_System SHALL provide a navigation path to the Statement page from the Revenue Control dashboard

### Requirement 7: Email Statement

**User Story:** As a business operator, I want to email the statement directly to the customer, so that I can share account information without manual file handling.

#### Acceptance Criteria

1. WHEN the Email Statement action is invoked, THE Statement_Controller SHALL generate the PDF and send it as an email attachment to the customer's registered email address
2. IF the customer has no registered email address, THEN THE Statement_Controller SHALL return an error indicating that no email address is available for the customer
3. WHEN the email is sent successfully, THE Statement_Controller SHALL display a success confirmation via SweetAlert2
4. IF email sending fails, THEN THE Statement_Controller SHALL display an error message via SweetAlert2 and log the failure

### Requirement 8: Audit Logging

**User Story:** As a business operator, I want statement generation and export events to be logged, so that I have a record of when statements were produced and by whom.

#### Acceptance Criteria

1. WHEN a statement is generated (on-screen view), THE Statement_Service SHALL create an audit log entry recording: BusinessId, UserId, CustomerId, Statement_Period, and timestamp
2. WHEN a PDF is downloaded, THE Statement_Service SHALL create an audit log entry recording the download event with BusinessId, UserId, CustomerId, and Statement_Period
3. WHEN a statement is emailed, THE Statement_Service SHALL create an audit log entry recording the email event with BusinessId, UserId, CustomerId, recipient email address, and Statement_Period

### Requirement 9: Customer Registry Pagination

**User Story:** As a business operator, I want the Customer Registry list to be paginated, so that the page loads efficiently and I can navigate through large customer lists.

#### Acceptance Criteria

1. THE Customer_Registry_Controller SHALL return customers in pages of 15 records (Page_Size)
2. THE Customer_Registry_Controller SHALL display pagination information showing "Showing X-Y of Z" where X is the first record number, Y is the last record number, and Z is the total record count
3. THE Customer_Registry_Controller SHALL render page navigation controls including numbered page buttons, a Previous button (disabled on page 1), and a Next button (disabled on the last page)
4. WHEN a search term is active, THE Customer_Registry_Controller SHALL filter customers whose Name, ContactPerson, or Email contains the search term (case-insensitive partial match), apply pagination to the filtered result set, and update the total count accordingly
5. WHEN a status filter is active, THE Customer_Registry_Controller SHALL filter customers by their IsActive value (Active or Inactive), apply pagination to the filtered result set, and update the total count accordingly
6. WHEN the user navigates to a different page, THE Customer_Registry_Controller SHALL maintain the current search term and status filter selections
7. WHEN the page first loads or WHEN filter criteria change, THE Customer_Registry_Controller SHALL reset to page 1
8. IF the requested page number exceeds the total number of pages, THEN THE Customer_Registry_Controller SHALL return the last available page (or an empty state with zero results if no records match the current filters)

### Requirement 10: Tenant Isolation

**User Story:** As a platform operator, I want all statement data to be scoped to the authenticated business tenant, so that businesses cannot view each other's customer financial data.

#### Acceptance Criteria

1. THE Statement_Service SHALL filter all invoice queries by the authenticated user's BusinessId resolved from the current authentication claims
2. THE Statement_Service SHALL filter all payment queries by the authenticated user's BusinessId resolved from the current authentication claims
3. THE Statement_Service SHALL filter all customer queries by the authenticated user's BusinessId resolved from the current authentication claims
4. THE Customer_Registry_Controller SHALL filter all customer pagination queries by the authenticated user's BusinessId resolved from the current authentication claims
5. IF the authenticated user's BusinessId cannot be resolved from the authentication claims, THEN THE Statement_Service SHALL return zero results for all queries
6. IF a request references a customer, invoice, or payment that does not belong to the authenticated user's BusinessId, THEN THE Statement_Service SHALL treat the resource as not found and return no data for that resource
7. WHEN the Statement_Service creates or records a new payment or statement entry, THE Statement_Service SHALL stamp the record with the authenticated user's BusinessId

### Requirement 11: Email History

**User Story:** As a business operator, I want to see a history of all statements emailed to a customer, so that I can track what was sent, when, and by whom.

#### Acceptance Criteria

1. THE Statement_Controller SHALL render an Email_History_Table on the Statement page displaying all previously emailed statements for the currently selected customer
2. THE Email_History_Table SHALL include the following columns: Date Sent, Statement Period (displaying the from-date and to-date of the emailed statement), Recipient Email, and Sent By (the user who triggered the email)
3. THE Email_History_Table SHALL display records ordered by Date Sent descending (most recent first)
4. WHEN a customer is selected in the Statement page filter, THE Statement_Controller SHALL load the Email_History_Table with records belonging only to that customer
5. THE Statement_Service SHALL filter all email history queries by the authenticated user's BusinessId resolved from the current authentication claims
6. WHEN a statement is successfully emailed, THE Statement_Service SHALL persist an email history record containing: BusinessId, CustomerId, statement period from-date, statement period to-date, recipient email address, the UserId of the sender, and the timestamp of sending
7. IF no email history records exist for the selected customer, THEN THE Statement_Controller SHALL display an empty state message within the Email_History_Table indicating that no statements have been emailed for this customer
