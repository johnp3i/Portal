# Requirements Document

## Introduction

This feature implements formal invoice number generation and management for the Portal (Bili) platform's subscription billing system. While Stripe handles payment collection, 3 Inventors requires its own compliant invoices for accounting, VAT reporting, and legal compliance in Cyprus (EU). The system introduces a sequential, platform-prefixed invoice numbering scheme (`BILI-INV-{yyyy}-{NNNN}`), persists the formal invoice number alongside existing billing records, updates the PDF generation to use the proper format, and provides an email delivery mechanism for sending invoices to subscribers.

## Glossary

- **Invoice_Number_Generator**: The service responsible for producing sequential, platform-prefixed invoice numbers following the pattern `{PlatformCode}-INV-{yyyy}-{NNNN}`.
- **Billing_Invoice**: The existing `[billing].[Invoice]` record representing a subscription payment period charge.
- **Invoice_Settings**: The `InvoiceSettings` configuration class loaded from appsettings.json, containing company details and the PlatformCode used in invoice number generation.
- **PDF_Renderer**: The component that produces a downloadable PDF invoice document using PuppeteerSharp from a Razor view template.
- **Invoice_Email_Service**: The service responsible for sending invoice PDF attachments or notification emails to subscribers upon payment.
- **Billing_Service**: The existing `BillingService` class that provides billing history retrieval and PDF invoice generation for business owners.
- **Webhook_Processing_Service**: The existing `WebhookProcessingService` that processes Stripe webhook events and creates Billing_Invoice records upon `invoice.paid` events.
- **Sequence_Counter**: The persistent counter in `[billing].[InvoiceSequence]` that tracks the next available invoice number per year to guarantee uniqueness and sequential ordering.

## Requirements

### Requirement 1: Invoice Number Format and Generation

**User Story:** As a platform operator, I want all subscription invoices to follow a consistent, legally compliant numbering pattern, so that invoices satisfy Cyprus VAT reporting requirements and are identifiable by platform.

#### Acceptance Criteria

1. THE Invoice_Number_Generator SHALL produce invoice numbers matching the pattern `{PlatformCode}-INV-{yyyy}-{NNNN}` where PlatformCode is read from Invoice_Settings (maximum 10 alphanumeric characters), yyyy is the four-digit UTC year of invoice creation, and NNNN is a zero-padded sequential number starting from 0001 each calendar year.
2. WHEN a new Billing_Invoice is created, THE Invoice_Number_Generator SHALL determine the year component using the UTC date at the time of creation and assign the next sequential number for that year, persisting it atomically with the invoice record.
3. THE Invoice_Number_Generator SHALL guarantee uniqueness of invoice numbers across all invoices within the same year by enforcing a database-level unique constraint.
4. WHEN two or more invoice creation requests arrive concurrently, THE Invoice_Number_Generator SHALL serialize number assignment so that no duplicate occurs; gaps in the sequence are permitted only if a transaction is rolled back due to a failure after number allocation.
5. WHEN the UTC calendar year changes, THE Invoice_Number_Generator SHALL reset the sequential counter to 0001 for the new year.
6. IF the PlatformCode value in Invoice_Settings is null, empty, or contains non-alphanumeric characters, THEN THE Invoice_Number_Generator SHALL throw a configuration error and prevent invoice creation.
7. IF the sequential counter for a given year exceeds 9999, THEN THE Invoice_Number_Generator SHALL extend the sequence digits beyond four characters (e.g., 10000) without truncation, preserving uniqueness.

### Requirement 2: Invoice Sequence Persistence

**User Story:** As a platform operator, I want the invoice sequence counter to be persisted in the database, so that sequential numbering survives application restarts and multi-instance deployments.

#### Acceptance Criteria

1. THE Sequence_Counter SHALL be stored in a dedicated `[billing].[InvoiceSequence]` table with columns: Year (INT, NOT NULL, primary key), LastNumber (INT, NOT NULL, default 0), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
2. WHEN a new invoice number is requested for a year that has no existing Sequence_Counter row, THE Invoice_Number_Generator SHALL create a new row with LastNumber set to 1 and return sequence number 0001.
3. WHEN a new invoice number is requested for a year that has an existing Sequence_Counter row, THE Invoice_Number_Generator SHALL atomically increment LastNumber and return the incremented value zero-padded to four digits.
4. THE Sequence_Counter increment operation SHALL use a database-level atomic operation to serialize concurrent access, ensuring that simultaneous requests each receive a unique sequential number without gaps.
5. IF the Sequence_Counter LastNumber reaches 9999 for a given year, THEN THE Invoice_Number_Generator SHALL reject the request and return an error indicating the annual sequence limit has been exceeded.
6. IF the atomic increment operation fails due to a database error, THEN THE Invoice_Number_Generator SHALL propagate the exception to the caller without modifying any state.

### Requirement 3: Billing Invoice Schema Extension

**User Story:** As a developer, I want the billing invoice table to store the formal invoice number, so that the number is available for display, PDF rendering, and reporting without regeneration.

#### Acceptance Criteria

1. THE Billing_Invoice table SHALL include an `InvoiceNumber` column of type NVARCHAR(50) that is nullable to support existing records without retroactive assignment.
2. THE Billing_Invoice table SHALL enforce a filtered unique index on the `InvoiceNumber` column that applies only to non-null values, allowing multiple existing records to retain NULL without constraint violation.
3. WHEN the Webhook_Processing_Service creates a new Billing_Invoice from a Stripe `invoice.paid` event, THE system SHALL invoke the Invoice_Number_Generator and persist the resulting InvoiceNumber on the Billing_Invoice record within the same database transaction that creates the invoice.
4. IF the InvoiceNumber generation or assignment fails during invoice creation, THEN THE system SHALL roll back the entire transaction, log an Error-level entry, and return a non-2xx HTTP response to Stripe so that the webhook event is retried.

### Requirement 4: Update Existing BillingService PDF Generation

**User Story:** As a subscriber, I want my downloaded invoice PDF to display the formal BILI-INV-yyyy-NNNN number, so that the document is valid for my accounting records.

#### Acceptance Criteria

1. WHEN generating an invoice PDF, THE Billing_Service SHALL use the persisted InvoiceNumber from the Billing_Invoice record instead of the legacy `INV-{Id:D6}` format.
2. IF a Billing_Invoice record has a null InvoiceNumber (legacy record), THEN THE Billing_Service SHALL fall back to the legacy format `INV-{Id:D6}` for backward compatibility.
3. THE PDF_Renderer SHALL display the company name, company address, company VAT number, and company email from Invoice_Settings on the invoice document.
4. THE PDF_Renderer SHALL display the PlatformCode-based invoice number prominently in the invoice header area.

### Requirement 5: Invoice PDF Content Completeness

**User Story:** As an accountant reviewing subscription invoices, I want each PDF to contain all legally required fields for a Cyprus VAT invoice, so that the documents are compliant for tax submissions.

#### Acceptance Criteria

1. THE PDF_Renderer SHALL include the following issuer fields from Invoice_Settings: CompanyName, CompanyAddress, CompanyCountryCode, CompanyVatNumber, and CompanyEmail.
2. THE PDF_Renderer SHALL include the subscriber (customer) fields: business name, VAT registration number (if available), and address.
3. THE PDF_Renderer SHALL include the invoice metadata: InvoiceNumber, invoice date, billing period start, and billing period end.
4. THE PDF_Renderer SHALL include line items with description, quantity, unit price, and line total.
5. THE PDF_Renderer SHALL include totals: subtotal, VAT amount (with applicable rate), and grand total.
6. THE PDF_Renderer SHALL include payment information: payment method and payment date when available.

### Requirement 6: Invoice Email Delivery

**User Story:** As a subscriber, I want to receive an email notification with my invoice when a payment is processed, so that I have a record of the charge without needing to log into the platform.

#### Acceptance Criteria

1. WHEN a Billing_Invoice is created from a Stripe `invoice.paid` event and the invoice creation transaction has been committed, THE Invoice_Email_Service SHALL send an invoice notification email to the business owner's registered email address within 60 seconds of the transaction commit.
2. THE invoice notification email SHALL contain the InvoiceNumber, the billing period (PeriodStart and PeriodEnd dates), the amount charged (formatted with currency), and a link to download the PDF from the platform.
3. THE Invoice_Email_Service SHALL use the existing email infrastructure with the "Invoices" department email account (invoices@3inventors.com) via the EmailDepartmentEnum.Invoices channel.
4. IF the email delivery fails due to an SMTP error or timeout, THEN THE Invoice_Email_Service SHALL log a Warning-level entry with the recipient email, InvoiceNumber, and exception details, and SHALL NOT roll back the invoice creation transaction or retry the delivery.
5. THE Invoice_Email_Service SHALL send the email asynchronously after the invoice creation transaction has been committed, ensuring that a failure in email delivery does not block the webhook response to Stripe.
6. IF the business owner has no registered email address or the email address is empty, THEN THE Invoice_Email_Service SHALL log a Warning-level entry with the BusinessId and InvoiceNumber and SHALL NOT attempt email delivery.
7. THE Invoice_Email_Service SHALL NOT send more than one notification email per Billing_Invoice record to prevent duplicate emails on webhook redelivery.

### Requirement 7: Invoice Number for Existing Records (Backfill)

**User Story:** As a platform operator, I want existing billing invoices to optionally receive formal invoice numbers through a manual backfill operation, so that historical records can be brought into compliance if needed.

#### Acceptance Criteria

1. THE system SHALL provide a backfill method that assigns InvoiceNumbers to existing Billing_Invoice records that have a null InvoiceNumber, ordered by CreatedAtUtc ascending.
2. WHEN backfilling, THE Invoice_Number_Generator SHALL assign numbers sequentially based on the original invoice creation year, maintaining chronological order.
3. THE backfill operation SHALL be idempotent: invoices that already have an InvoiceNumber SHALL be skipped.
4. THE backfill operation SHALL execute within a single database transaction per year to maintain sequence integrity.

### Requirement 8: Invoice Number Parsing and Formatting

**User Story:** As a developer, I want a utility that can parse and format invoice numbers, so that components can extract year and sequence information and validate invoice number format.

#### Acceptance Criteria

1. THE Invoice_Number_Generator SHALL provide a format method that accepts a PlatformCode, year, and sequence number and returns a formatted invoice number string.
2. THE Invoice_Number_Generator SHALL provide a parse method that accepts an invoice number string and returns the PlatformCode, year, and sequence number components.
3. WHEN a malformed invoice number string is provided to the parse method, THE Invoice_Number_Generator SHALL return a failure result with a descriptive error.
4. FOR ALL valid invoice numbers, formatting then parsing SHALL produce equivalent component values (round-trip property).

### Requirement 9: VAT Calculation on Subscription Invoices

**User Story:** As a platform operator registered in Cyprus, I want subscription invoices to correctly reflect VAT obligations, so that invoices are compliant with EU VAT rules.

#### Acceptance Criteria

1. WHEN generating a subscription invoice for a customer whose BusinessProfile.Country matches the platform's own country (Cyprus), THE system SHALL apply the standard Cyprus VAT rate of 19% to the subscription amount.
2. WHEN generating a subscription invoice for a customer whose BusinessProfile.Country is an EU member state other than Cyprus and whose BusinessProfile.VatRegistrationNumber is non-empty, THE system SHALL apply the reverse charge mechanism (0% VAT) to the subscription amount.
3. WHEN generating a subscription invoice for a customer whose BusinessProfile.Country is an EU member state other than Cyprus and whose BusinessProfile.VatRegistrationNumber is empty or null, THE system SHALL apply the standard Cyprus VAT rate of 19% to the subscription amount.
4. WHEN generating a subscription invoice for a customer whose BusinessProfile.Country is not an EU member state, THE system SHALL apply 0% VAT to the subscription amount.
5. IF the customer's BusinessProfile.Country is empty or null at the time of invoice generation, THEN THE system SHALL default to applying the standard Cyprus VAT rate of 19% and log a warning indicating the missing country for the business.
6. THE PDF_Renderer SHALL display the applicable VAT rate as a percentage and the calculated VAT amount on every subscription invoice.
7. WHEN the reverse charge mechanism applies, THE PDF_Renderer SHALL include the notation "Reverse Charge - Article 196 Council Directive 2006/112/EC" on the invoice.

### Requirement 10: Logging and Auditability

**User Story:** As a platform operator, I want all invoice generation actions to be logged, so that I can trace and audit invoice issuance for compliance purposes.

#### Acceptance Criteria

1. WHEN an invoice number is successfully generated, THE system SHALL log an Information-level entry with the InvoiceNumber, BusinessId, and Billing_Invoice Id.
2. WHEN an invoice email is sent, THE system SHALL log an Information-level entry with the recipient email and InvoiceNumber.
3. IF an invoice number generation fails, THEN THE system SHALL log an Error-level entry with the BusinessId, attempted year, and exception details.
4. IF an invoice email delivery fails, THEN THE system SHALL log a Warning-level entry with the recipient email, InvoiceNumber, and exception details.
