# Requirements Document

## Introduction

The Invoicing module enables Portal users to create invoices either by converting accepted quotations or by creating standalone invoices. Quotation-to-invoice conversion is deterministic and transactional, producing an immutable snapshot of line items. The module enforces idempotency (one invoice per quotation), sequential invoice numbering per business, and full tenant isolation. Invoices progress through document lifecycle states (Draft, Issued, Cancelled) and financial states (Unpaid, PartiallyPaid, Paid, Overdue, WrittenOff).

## Glossary

- **Invoice_Service**: The application service responsible for invoice creation, conversion, and lifecycle management
- **Conversion_Service**: The component within Invoice_Service that handles quotation-to-invoice conversion
- **Invoice_Repository**: The data access layer for Invoice and InvoiceLine entities
- **Invoice_Section_Repository**: The data access layer for InvoiceSection entities
- **Invoice_Controller**: The MVC controller handling HTTP requests for invoice operations
- **Invoice_Number_Generator**: The component responsible for producing sequential invoice numbers per business
- **Invoice_UI**: The user interface screens for listing, viewing, and managing invoices
- **Invoice_Section**: A named grouping of invoice lines within an invoice, mirroring ProposalSection (supports LineItems and Narrative types, column configuration, emphasis, totals visibility)
- **Audit_Logger**: The component that records significant invoice operations to the AuditLog table
- **Portal_Database**: The SQL Server database containing the invoice schema tables

## Requirements

### Requirement 1: Quotation-to-Invoice Conversion

**User Story:** As a business user, I want to convert an accepted quotation into an invoice, so that I can bill my customer based on the agreed quotation.

#### Acceptance Criteria

1. WHEN a user requests conversion of a Quotation with QuotationStatusTypeId = 3 (Accepted) that has one or more QuotationLines, THE Conversion_Service SHALL create a new Invoice linked to that Quotation within a single database transaction
2. WHEN conversion succeeds, THE Conversion_Service SHALL transition the source Quotation to QuotationStatusTypeId = 4 (Converted) within the same transaction
3. WHEN conversion succeeds, THE Conversion_Service SHALL copy all QuotationLines into InvoiceLines preserving Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, LineTotal, SortOrder, ReferenceUrl, Subtitle, and section assignment as an immutable snapshot
4. WHEN conversion succeeds, THE Conversion_Service SHALL compute and store the Invoice Subtotal, TaxAmount, and TotalAmount from the copied InvoiceLines
5. WHEN conversion succeeds, THE Conversion_Service SHALL set the new Invoice to InvoiceStatusTypeId = 1 (Draft) and InvoiceFinancialStatusTypeId = 1 (Unpaid)
6. WHEN conversion succeeds, THE Conversion_Service SHALL assign the Invoice the same CustomerId and BusinessId as the source Quotation
7. WHEN conversion succeeds, THE Conversion_Service SHALL copy the Quotation's IsGrandTotalShown setting to the new Invoice
8. WHEN conversion succeeds AND the source Quotation has ProposalSections, THE Conversion_Service SHALL copy all ProposalSections into InvoiceSections preserving Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, and IsTotalsTableShown
9. WHEN conversion succeeds AND InvoiceSections are created, THE Conversion_Service SHALL map each InvoiceLine's InvoiceSectionId to the corresponding newly created InvoiceSection
10. IF a conversion request is made for a Quotation with QuotationStatusTypeId other than 3 (Accepted), THEN THE Conversion_Service SHALL reject the request and return a precondition failure
11. IF a conversion request is made for a Quotation that has zero QuotationLines, THEN THE Conversion_Service SHALL reject the request and return a validation error
12. IF any step within the conversion transaction fails, THEN THE Conversion_Service SHALL roll back the entire transaction leaving both Quotation and Invoice tables unchanged

### Requirement 2: Conversion Idempotency

**User Story:** As a business user, I want the system to prevent duplicate invoice creation from the same quotation, so that I do not accidentally bill a customer twice.

#### Acceptance Criteria

1. THE Portal_Database SHALL enforce a filtered unique index on Invoice.QuotationId (WHERE QuotationId IS NOT NULL) to prevent multiple invoices from the same quotation
2. IF a conversion request is made for a Quotation that already has an associated Invoice, THEN THE Conversion_Service SHALL reject the request and return a duplicate conversion error
3. WHEN concurrent conversion requests arrive for the same Quotation, THE Portal_Database SHALL allow only one to succeed via the unique index constraint

### Requirement 3: Standalone Invoice Creation

**User Story:** As a business user, I want to create an invoice without a linked quotation, so that I can bill customers for ad-hoc work or services not covered by a quotation.

#### Acceptance Criteria

1. WHEN a user submits a valid standalone invoice request with a CustomerId, InvoiceDate, DueDate, and one or more line items, THE Invoice_Service SHALL create a new Invoice with QuotationId set to NULL
2. THE Invoice_Service SHALL compute Subtotal, TaxAmount, and TotalAmount from the provided line items
3. WHEN a standalone invoice is created, THE Invoice_Service SHALL set InvoiceStatusTypeId = 1 (Draft) and InvoiceFinancialStatusTypeId = 1 (Unpaid)
4. IF a standalone invoice request is missing required fields (CustomerId, InvoiceDate, DueDate) or has zero line items, THEN THE Invoice_Service SHALL reject the request and return a validation error
5. WHEN creating a standalone invoice, THE Invoice_Service SHALL support line items with Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, ReferenceUrl, Subtitle, and optional InvoiceSectionId
6. WHEN creating a standalone invoice, THE Invoice_Service SHALL support creating InvoiceSections with Name, SortOrder, ColumnConfiguration, SectionType (LineItems or Narrative), Description, Notes, IsEmphasized, AccentColor, Label, and IsTotalsTableShown
7. WHEN creating a standalone invoice, THE Invoice_Service SHALL support setting IsGrandTotalShown on the Invoice to control grand total card visibility

### Requirement 4: Invoice Number Generation

**User Story:** As a business user, I want each invoice to have a unique sequential number, so that I can reference invoices consistently and meet accounting requirements.

#### Acceptance Criteria

1. WHEN a new Invoice is created (via conversion or standalone), THE Invoice_Number_Generator SHALL assign a sequential InvoiceNumber unique within the BusinessId scope
2. THE Invoice_Number_Generator SHALL produce numbers in a deterministic ascending sequence with no gaps under normal operation
3. WHEN concurrent invoice creation occurs for the same BusinessId, THE Invoice_Number_Generator SHALL ensure no duplicate InvoiceNumbers are assigned
4. THE Invoice_Number_Generator SHALL format the InvoiceNumber as a string value not exceeding 50 characters

### Requirement 5: Invoice Lifecycle Status Management

**User Story:** As a business user, I want to manage the lifecycle status of my invoices, so that I can track which invoices are drafts, issued to customers, or cancelled.

#### Acceptance Criteria

1. WHEN an Invoice has InvoiceStatusTypeId = 1 (Draft), THE Invoice_Service SHALL allow transition to InvoiceStatusTypeId = 2 (Issued)
2. WHEN an Invoice has InvoiceStatusTypeId = 1 (Draft), THE Invoice_Service SHALL allow transition to InvoiceStatusTypeId = 3 (Cancelled)
3. WHEN an Invoice has InvoiceStatusTypeId = 2 (Issued), THE Invoice_Service SHALL allow transition to InvoiceStatusTypeId = 3 (Cancelled)
4. IF a status transition is requested that violates the allowed transitions, THEN THE Invoice_Service SHALL reject the request and return an invalid transition error
5. WHEN a status transition succeeds, THE Invoice_Service SHALL update the UpdatedAtUtc timestamp

### Requirement 6: Tenant Isolation

**User Story:** As a business user, I want to see only my own business invoices, so that my financial data remains private and separate from other tenants.

#### Acceptance Criteria

1. THE Invoice_Repository SHALL filter all invoice queries by the authenticated user's BusinessId
2. THE Invoice_Service SHALL verify that the target CustomerId belongs to the same BusinessId before creating an invoice
3. IF a user attempts to access an Invoice belonging to a different BusinessId, THEN THE Invoice_Service SHALL reject the request and return an authorization error

### Requirement 7: Invoice List View

**User Story:** As a business user, I want to view a filterable list of my invoices, so that I can quickly find and manage invoices by status, financial status, or customer.

#### Acceptance Criteria

1. WHEN a user navigates to the invoice list, THE Invoice_UI SHALL display all invoices for the current BusinessId ordered by InvoiceDate descending
2. THE Invoice_UI SHALL provide filter controls for InvoiceStatusTypeId, InvoiceFinancialStatusTypeId, and CustomerId
3. THE Invoice_UI SHALL display InvoiceNumber, CustomerName, InvoiceDate, DueDate, TotalAmount, InvoiceStatusType, and InvoiceFinancialStatusType for each invoice
4. WHEN a filter is applied, THE Invoice_UI SHALL display only invoices matching the selected criteria

### Requirement 8: Invoice Detail View

**User Story:** As a business user, I want to view the full details of an invoice including line items and source quotation, so that I can review what was billed and trace it back to the original quotation.

#### Acceptance Criteria

1. WHEN a user navigates to an invoice detail screen, THE Invoice_UI SHALL display the Invoice header (InvoiceNumber, Customer, InvoiceDate, DueDate, Status, FinancialStatus, Notes)
2. THE Invoice_UI SHALL display all InvoiceLines grouped by InvoiceSection (where applicable) with Description, Subtitle, Quantity, UnitPrice, VatRate, Discount, DiscountType, LineTotal, and SortOrder
3. THE Invoice_UI SHALL display the computed Subtotal, TaxAmount, and TotalAmount
4. WHERE the Invoice has a non-null QuotationId, THE Invoice_UI SHALL display a link to the source Quotation detail screen
5. WHERE the Invoice has InvoiceStatusTypeId = 1 (Draft), THE Invoice_UI SHALL display status transition actions (Issue, Cancel)
6. WHERE the Invoice has InvoiceSections, THE Invoice_UI SHALL render sections with their configured ColumnConfiguration, SectionType, emphasis, and per-section totals (when IsTotalsTableShown is enabled)
7. WHERE the Invoice has IsGrandTotalShown enabled, THE Invoice_UI SHALL display the grand total summary card with per-section breakdown

### Requirement 9: Convert-to-Invoice Action on Quotation Detail

**User Story:** As a business user, I want a "Convert to Invoice" button on the quotation detail screen, so that I can initiate conversion directly from the quotation I am reviewing.

#### Acceptance Criteria

1. WHILE a Quotation has QuotationStatusTypeId = 3 (Accepted), THE Invoice_UI SHALL display a "Convert to Invoice" action button on the Quotation detail screen
2. WHILE a Quotation has QuotationStatusTypeId other than 3 (Accepted), THE Invoice_UI SHALL hide the "Convert to Invoice" action button
3. WHEN the user clicks "Convert to Invoice", THE Invoice_Controller SHALL invoke the Conversion_Service and redirect to the new Invoice detail screen on success
4. IF conversion fails, THEN THE Invoice_UI SHALL display the error message returned by the Conversion_Service

### Requirement 10: Invoice API Endpoints

**User Story:** As a developer, I want well-defined controller endpoints for invoice operations, so that the UI and future integrations can interact with the invoicing module consistently.

#### Acceptance Criteria

1. THE Invoice_Controller SHALL expose a GET endpoint to list invoices for the current BusinessId with optional filter parameters
2. THE Invoice_Controller SHALL expose a GET endpoint to retrieve a single invoice by Id including its InvoiceLines
3. THE Invoice_Controller SHALL expose a POST endpoint to create a standalone invoice with line items
4. THE Invoice_Controller SHALL expose a POST endpoint to convert a Quotation into an Invoice by QuotationId
5. THE Invoice_Controller SHALL expose a POST endpoint to transition an Invoice status (Issue, Cancel)
6. IF any endpoint receives invalid input, THEN THE Invoice_Controller SHALL return appropriate validation error responses

### Requirement 11: Audit Logging

**User Story:** As a business administrator, I want all invoice creation and status changes to be recorded in the audit log, so that I can trace who did what and when for compliance purposes.

#### Acceptance Criteria

1. WHEN an Invoice is created (via conversion or standalone), THE Audit_Logger SHALL record an entry with Action = "Created", TableName = "Invoice", RecordId = the new Invoice Id, and NewValues containing the invoice data
2. WHEN an Invoice status transition occurs, THE Audit_Logger SHALL record an entry with Action = "StatusChanged", TableName = "Invoice", RecordId = the Invoice Id, OldValues containing the previous status, and NewValues containing the new status
3. WHEN a Quotation-to-Invoice conversion occurs, THE Audit_Logger SHALL record an entry with Action = "Converted", TableName = "Quotation", RecordId = the Quotation Id, and NewValues referencing the created Invoice Id
4. THE Audit_Logger SHALL populate BusinessId and UserId from the current authenticated context for all audit entries

### Requirement 12: Invoice Presentation Structure (Sections and Extended Line Properties)

**User Story:** As a business user, I want invoices to support the same section grouping, discount columns, VAT rates, and presentation options as quotations, so that my invoices are comprehensive and consistent with my proposals.

#### Acceptance Criteria

1. THE Portal_Database SHALL provide an InvoiceSection table with columns: Id, InvoiceId, Name, SortOrder, ColumnConfiguration, SectionType (LineItems or Narrative), Description, Notes, IsEmphasized, AccentColor, Label, IsTotalsTableShown
2. THE Portal_Database SHALL extend the InvoiceLine table with columns: VatRate, Discount, DiscountType, CostPrice, ReferenceUrl, Subtitle, InvoiceSectionId (nullable FK to InvoiceSection)
3. THE Portal_Database SHALL extend the Invoice table with column: IsGrandTotalShown (BIT, default 1)
4. WHEN an InvoiceLine has a Discount greater than zero, THE Invoice_UI SHALL display the discount in a dedicated Discount column (showing percentage or fixed amount)
5. WHEN an InvoiceSection has IsTotalsTableShown enabled, THE Invoice_UI SHALL display a per-section totals breakdown (subtotal, discount, VAT, total) below that section's line items
6. WHEN the Invoice has IsGrandTotalShown enabled, THE Invoice_UI SHALL display a full-width summary table showing per-section costs (subtotal, discount, VAT, total per section) followed by a right-aligned grand totals card
7. THE Invoice_Service SHALL support CRUD operations on InvoiceSections (add, update, remove, reorder) following the same patterns as ProposalSection management
8. THE Invoice_Service SHALL support moving InvoiceLines between InvoiceSections following the same patterns as QuotationLine section assignment
