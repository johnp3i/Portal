# Requirements Document

## Introduction

The Document Duplication feature enables managers to duplicate existing invoices and quotations to create new documents in Draft status. This supports recurring billing scenarios where customers pay the same invoice every fixed period — instead of recreating documents from scratch, the manager duplicates an existing one. The duplicate is a standalone document with fresh dates, a new auto-generated reference number, and all line items and sections copied from the source.

## Glossary

- **Duplication_Service**: The service layer component responsible for orchestrating the creation of a new document by copying data from an existing source document.
- **Invoice_Detail_Page**: The MVC view displaying a single invoice's full information including header, sections, line items, and action buttons.
- **Quotation_Detail_Page**: The MVC view displaying a single quotation's full information including header, sections, line items, and action buttons.
- **Source_Document**: The existing invoice or quotation from which data is copied during duplication.
- **Duplicate_Document**: The new invoice or quotation created as a result of the duplication operation.
- **Invoice_Number**: The auto-generated sequential reference for invoices following the pattern `INV-{BusinessId}-{Sequential:D5}`.
- **Quotation_Reference**: The auto-generated sequential reference for quotations following the pattern `QUO-{BusinessId}-{Sequential:D5}`.
- **Duration_Gap**: The number of days between the invoice date and due date on the Source_Document, used to calculate the due date on the Duplicate_Document.
- **Line_Item**: A priced entry within a document containing description, quantity, unit price, VAT rate, discount, and other pricing fields.
- **Section**: A named grouping of line items within a document containing name, description, notes, sort order, section type, column configuration, and emphasis settings.

## Requirements

### Requirement 1: Invoice Duplication Trigger

**User Story:** As a manager, I want a "Duplicate" button on the Invoice Detail Page, so that I can quickly create a new invoice based on an existing one without starting from scratch.

#### Acceptance Criteria

1. WHEN the manager is viewing the Invoice_Detail_Page, THE Invoice_Detail_Page SHALL display a "Duplicate" button in the action area.
2. WHEN the manager clicks the "Duplicate" button, THE Invoice_Detail_Page SHALL display a SweetAlert2 confirmation dialog asking the manager to confirm the duplication.
3. WHEN the manager confirms the duplication, THE Invoice_Detail_Page SHALL block the UI using BlockUI and send a duplication request to the Duplication_Service.
4. IF the duplication request fails, THEN THE Invoice_Detail_Page SHALL hide BlockUI and display a SweetAlert2 error dialog with the failure message.

### Requirement 2: Quotation Duplication Trigger

**User Story:** As a manager, I want a "Duplicate" button on the Quotation Detail Page, so that I can quickly create a new quotation based on an existing one without starting from scratch.

#### Acceptance Criteria

1. WHEN the manager is viewing the Quotation_Detail_Page, THE Quotation_Detail_Page SHALL display a "Duplicate" button in the action area.
2. WHEN the manager clicks the "Duplicate" button, THE Quotation_Detail_Page SHALL display a SweetAlert2 confirmation dialog asking the manager to confirm the duplication.
3. WHEN the manager confirms the duplication, THE Quotation_Detail_Page SHALL block the UI using BlockUI and send a duplication request to the Duplication_Service.
4. IF the duplication request fails, THEN THE Quotation_Detail_Page SHALL hide BlockUI and display a SweetAlert2 error dialog with the failure message.

### Requirement 3: Invoice Duplication Logic

**User Story:** As a manager, I want the duplicated invoice to be a complete copy with fresh dates and a new number, so that I can use it as a new billing document without manual data entry.

#### Acceptance Criteria

1. WHEN the Duplication_Service receives an invoice duplication request, THE Duplication_Service SHALL create a new Invoice with InvoiceStatusTypeId set to 1 (Draft) and InvoiceFinancialStatusTypeId set to 1 (Unpaid).
2. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL generate the next sequential Invoice_Number for the current business.
3. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL set the InvoiceDate to today's date.
4. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL set the DueDate to today's date plus the Duration_Gap from the Source_Document.
5. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL copy the CustomerId from the Source_Document.
6. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL set QuotationId to null on the Duplicate_Document.
7. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL copy the Notes, IsGrandTotalShown, IsQuotationReferenceShown, and CurrencyCode fields from the Source_Document.

### Requirement 4: Quotation Duplication Logic

**User Story:** As a manager, I want the duplicated quotation to be a complete copy with a fresh validity period and a new reference, so that I can send it as a new proposal without manual data entry.

#### Acceptance Criteria

1. WHEN the Duplication_Service receives a quotation duplication request, THE Duplication_Service SHALL create a new Quotation with QuotationStatusTypeId set to 1 (Draft).
2. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL generate the next sequential Quotation_Reference for the current business.
3. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL set ValidUntil to today's date plus the same number of days that existed between the Source_Document's CreatedAtUtc date and its ValidUntil date, or null if the Source_Document had no ValidUntil.
4. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL copy the CustomerId from the Source_Document.
5. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL copy the Notes and IsGrandTotalShown fields from the Source_Document.
6. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL set QuotationContactId to null on the Duplicate_Document.

### Requirement 5: Line Item Duplication

**User Story:** As a manager, I want all line items copied to the new document, so that I do not have to re-enter pricing details for recurring work.

#### Acceptance Criteria

1. WHEN the Duplication_Service duplicates an invoice, THE Duplication_Service SHALL copy all InvoiceLine records from the Source_Document to the Duplicate_Document preserving Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, SortOrder, ReferenceUrl, and Subtitle.
2. WHEN the Duplication_Service duplicates a quotation, THE Duplication_Service SHALL copy all QuotationLine records from the Source_Document to the Duplicate_Document preserving Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, SortOrder, ReferenceUrl, and Subtitle.
3. WHEN a Line_Item in the Source_Document belongs to a Section, THE Duplication_Service SHALL assign the copied Line_Item to the corresponding copied Section in the Duplicate_Document.
4. WHEN a Line_Item in the Source_Document does not belong to a Section, THE Duplication_Service SHALL leave the section assignment as null on the copied Line_Item.

### Requirement 6: Section Duplication

**User Story:** As a manager, I want all sections copied to the new document with their structure intact, so that the document layout is preserved.

#### Acceptance Criteria

1. WHEN the Duplication_Service duplicates an invoice, THE Duplication_Service SHALL copy all InvoiceSection records from the Source_Document to the Duplicate_Document preserving Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, and IsTotalsTableShown.
2. WHEN the Duplication_Service duplicates a quotation, THE Duplication_Service SHALL copy all ProposalSection records from the Source_Document to the Duplicate_Document preserving Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, and IsTotalsTableShown.

### Requirement 7: Financial Recalculation

**User Story:** As a manager, I want the financial totals on the duplicate to be accurate, so that the document is immediately correct without manual adjustment.

#### Acceptance Criteria

1. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL calculate each Line_Item's LineTotal from its Quantity, UnitPrice, Discount, DiscountType, and VatRate.
2. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL calculate the Subtotal as the sum of all line totals before tax.
3. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL calculate the TaxAmount as the sum of all VAT amounts across line items.
4. WHEN the Duplication_Service creates the Duplicate_Document, THE Duplication_Service SHALL calculate the TotalAmount as Subtotal plus TaxAmount.

### Requirement 8: Document Independence

**User Story:** As a manager, I want the duplicate to be completely standalone, so that editing it does not affect the original and vice versa.

#### Acceptance Criteria

1. THE Duplicate_Document SHALL have no foreign key reference or navigational link back to the Source_Document.
2. WHEN the Duplicate_Document is created, THE Duplication_Service SHALL assign new primary key identifiers to the Duplicate_Document, its sections, and its line items.
3. WHEN the Duplicate_Document is modified after creation, THE Source_Document SHALL remain unchanged.

### Requirement 9: Post-Duplication Navigation

**User Story:** As a manager, I want to be taken directly to the new document after duplication, so that I can review or edit it immediately.

#### Acceptance Criteria

1. WHEN the Duplication_Service successfully creates the Duplicate_Document, THE system SHALL redirect the manager to the Duplicate_Document's Detail Page.
2. WHEN the redirect occurs, THE system SHALL hide BlockUI before navigating to the new page.

### Requirement 10: Transactional Integrity

**User Story:** As a manager, I want the duplication to either fully succeed or fully fail, so that I never end up with a partially created document.

#### Acceptance Criteria

1. THE Duplication_Service SHALL execute the entire duplication operation (document creation, section copying, line item copying, financial calculation) within a single database transaction.
2. IF any step within the duplication transaction fails, THEN THE Duplication_Service SHALL roll back all changes and return an error message.
3. IF the Source_Document does not exist or does not belong to the current business, THEN THE Duplication_Service SHALL reject the request and return a descriptive error message.
