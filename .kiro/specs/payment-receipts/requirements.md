# Requirements Document

## Introduction

This feature introduces Payment Receipts — formal documents acknowledging that a payment (full or partial) has been received against one or more invoices. Receipts are auto-generated when payments are recorded (configurable business setting) and can be shared with customers via PDF download, secure link, or email.

Additionally, a Signature Library allows businesses to upload, manage, and attach digital signatures to receipts and other documents. Signature usage is controlled by a permission system — the business owner/manager uploads signatures, and only authorised users can apply them to documents.

## Glossary

- **Payment_Receipt**: A formal document confirming receipt of payment. References one or more payments and their associated invoices.
- **Receipt_Number**: A sequential identifier following the pattern `REC-{BusinessId}-{Sequence}` (e.g., REC-1-00001).
- **Signature**: A digital image (PNG/SVG) representing an authorised person's signature, stored at the business level.
- **Signature_Library**: The collection of all uploaded signatures for a business.
- **Signature_Permission**: A user-level permission controlling whether a user can apply signatures to documents.
- **Auto_Receipt**: A business-level setting that, when enabled, automatically generates a receipt each time a payment is recorded.
- **Receipt_Share**: A secure link allowing a customer to view/download a receipt without authentication.

## Requirements

### Requirement 1: Payment Receipt Entity

**User Story:** As a platform developer, I want a dedicated table to store payment receipts with their metadata, so that receipts can be managed, shared, and audited.

#### Acceptance Criteria

1. THE database SHALL contain a `[revenue].[PaymentReceipt]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), ReceiptNumber (NVARCHAR(50) NOT NULL UNIQUE per business), CustomerId (INT NOT NULL FK), ReceiptDate (DATETIME NOT NULL), TotalAmountReceived (DECIMAL(18,2) NOT NULL), OutstandingBalanceAfter (DECIMAL(18,2) NOT NULL), PaymentMethodTypeId (INT NOT NULL FK), PaymentReference (NVARCHAR(200) NULL), Notes (NVARCHAR(500) NULL), SignatureId (INT NULL FK to Signature), IsVoided (BIT NOT NULL DEFAULT 0), CreatedByUserId (NVARCHAR(450) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE database SHALL contain a `[revenue].[PaymentReceiptLine]` table linking receipts to payments: Id (INT IDENTITY PK), PaymentReceiptId (INT NOT NULL FK), PaymentId (INT NOT NULL FK), InvoiceId (INT NOT NULL FK), Amount (DECIMAL(18,2) NOT NULL), InvoiceNumber (NVARCHAR(50) NOT NULL), InvoiceTotal (DECIMAL(18,2) NOT NULL), InvoiceOutstandingBefore (DECIMAL(18,2) NOT NULL), InvoiceOutstandingAfter (DECIMAL(18,2) NOT NULL).
3. THE database SHALL contain a `[revenue].[PaymentReceiptShare]` table for sharing: Id (INT IDENTITY PK), PaymentReceiptId (INT NOT NULL FK), BusinessId (INT NOT NULL FK), ShareToken (NVARCHAR(100) NOT NULL UNIQUE), SnapshotHtml (NVARCHAR(MAX) NOT NULL), CustomerEmail (NVARCHAR(200) NOT NULL), ExpiresAtUtc (DATETIMEOFFSET NOT NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIMEOFFSET NOT NULL), CreatedByUserId (NVARCHAR(450) NOT NULL).
4. ALL tables SHALL include `CreatedAtUtc` with DEFAULT GETUTCDATE().

### Requirement 2: Receipt Number Sequencing

**User Story:** As a business user, I want receipt numbers to follow a predictable sequential format, so that my accounting records are orderly and traceable.

#### Acceptance Criteria

1. THE receipt number SHALL follow the format `REC-{BusinessId}-{Sequence}` padded to 5 digits (e.g., REC-1-00001, REC-1-00002).
2. THE sequence SHALL be unique per business and auto-increment.
3. THE system SHALL use a sequence table or MAX+1 approach to prevent gaps and collisions.
4. THE receipt number SHALL be generated atomically at creation time.

### Requirement 3: Receipt Generation from Payment

**User Story:** As a business user, I want to generate a receipt when a payment is recorded (full or partial), so that I have formal proof of payment to send to my customer.

#### Acceptance Criteria

1. WHEN a payment is recorded against a single invoice, THE system SHALL be able to generate a receipt covering that payment.
2. WHEN a global payment is recorded (covering multiple invoices), THE system SHALL generate ONE receipt covering all allocations from that parent payment.
3. THE receipt SHALL include: all invoice numbers covered, amount per invoice, total received, outstanding balance per invoice after payment, payment method, reference, and date.
4. THE receipt SHALL clearly state whether it is for "Payment in Full" or "Partial Payment" per invoice line.
5. THE receipt SHALL be accessible from: Invoice Detail (per payment row), Revenue Dashboard (recent payments), and Customer Statement (after recording).

### Requirement 4: Auto-Generation Setting

**User Story:** As a business owner, I want to configure whether receipts are generated automatically when payments are recorded, so that I don't have to manually create them each time.

#### Acceptance Criteria

1. THE `[portal].[Business]` table SHALL include a new column `IsAutoReceiptEnabled` (BIT NOT NULL DEFAULT 0).
2. WHEN `IsAutoReceiptEnabled = 1` AND a payment is recorded (per-invoice or global), THE system SHALL automatically generate a receipt after successful payment recording.
3. WHEN `IsAutoReceiptEnabled = 0`, THE system SHALL NOT auto-generate — the user must manually trigger receipt generation.
4. THE setting SHALL be configurable from the My Business settings page.
5. THE auto-generated receipt SHALL use the default signature (if one is set and the recording user has signature permission).

### Requirement 5: Receipt Document Layout

**User Story:** As a business user, I want the receipt to look professional with my business branding, so that it reflects well on my business when sent to customers.

#### Acceptance Criteria

1. THE receipt SHALL include: business logo (primary), business name and address, customer name and address, receipt number, receipt date, payment details table (invoice number, invoice total, amount received, outstanding after), total amount received, payment method, payment reference, optional signature, optional notes.
2. THE receipt layout SHALL follow the same design system as invoices (same fonts, colours, spacing).
3. THE receipt SHALL be renderable as HTML (for sharing) and downloadable as PDF.
4. FOR partial payments, THE receipt SHALL clearly show "Partial Payment" label and the remaining balance.
5. FOR full payments covering multiple invoices, THE receipt SHALL list each invoice as a line item.

### Requirement 6: Receipt Sharing

**User Story:** As a business user, I want to share receipts with customers via a secure link or email, so that they have proof of their payment.

#### Acceptance Criteria

1. THE user SHALL be able to generate a share link for a receipt (same pattern as invoice sharing).
2. THE share link SHALL be publicly accessible without authentication.
3. THE shared receipt SHALL render the HTML snapshot stored at creation time.
4. THE user SHALL be able to email the receipt directly to the customer.
5. THE share link SHALL have a configurable expiry (default: 90 days).
6. THE receipt PDF SHALL be downloadable from the share link page.

### Requirement 7: Signature Library

**User Story:** As a business owner, I want to upload digital signatures that can be attached to receipts and other documents, so that my documents look professionally signed.

#### Acceptance Criteria

1. THE database SHALL contain a `[portal].[Signature]` table: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), Label (NVARCHAR(100) NOT NULL), FileName (NVARCHAR(200) NOT NULL), ContentType (NVARCHAR(50) NOT NULL), FilePath (NVARCHAR(500) NOT NULL), IsDefault (BIT NOT NULL DEFAULT 0), IsActive (BIT NOT NULL DEFAULT 1), UploadedByUserId (NVARCHAR(450) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE system SHALL accept PNG and SVG image uploads (transparent background recommended).
3. THE system SHALL allow multiple signatures per business.
4. THE user SHALL be able to set one signature as the default.
5. THE user SHALL be able to label each signature (e.g., "John Smith — Director").
6. THE user SHALL be able to deactivate a signature without deleting the file.
7. ONLY users with the `signature_manage` permission SHALL be able to upload, edit, or deactivate signatures.

### Requirement 8: Signature Permission Model

**User Story:** As a business owner, I want to control which users can use signatures on documents, so that only authorised personnel can sign official receipts.

#### Acceptance Criteria

1. THE permission system SHALL include a new permission: `signature_use` — controls whether a user can apply signatures when generating receipts.
2. THE permission system SHALL include a new permission: `signature_manage` — controls whether a user can upload, edit, label, set default, or deactivate signatures.
3. WHEN generating a receipt, IF the user does NOT have `signature_use` permission, THE signature field SHALL be hidden/disabled.
4. WHEN generating a receipt, IF the user HAS `signature_use` permission, THE form SHALL show the signature selector (defaulting to the business's default signature).
5. THE owner/SuperAdmin SHALL always have both `signature_manage` and `signature_use` implicitly.
6. THE audit log SHALL record: who uploaded a signature, who applied it to a document, and when.

### Requirement 9: Signature Selection on Receipt

**User Story:** As an authorised user, I want to choose which signature appears on a receipt, so that the correct person's signature is shown.

#### Acceptance Criteria

1. WHEN generating a receipt, THE form SHALL include a "Signature" dropdown showing all active signatures for the business.
2. THE dropdown SHALL pre-select the default signature (if one exists).
3. THE user SHALL be able to select "No signature" to omit the signature block.
4. THE selected signature image SHALL be embedded in the receipt HTML/PDF.
5. WHEN auto-generating receipts, THE system SHALL use the default signature (if the recording user has `signature_use` permission).

### Requirement 10: Receipt Voiding

**User Story:** As a business user, I want to void a receipt when a payment is voided or an error is discovered, so that my records stay accurate.

#### Acceptance Criteria

1. WHEN a payment is voided, THE system SHALL automatically void any receipt associated with that payment.
2. A voided receipt SHALL be marked with `IsVoided = 1` and visually indicated in the UI.
3. THE user SHALL also be able to manually void a receipt (with SweetAlert2 confirmation).
4. Voided receipts SHALL NOT be deleted — they remain for audit purposes.
5. THE share link for a voided receipt SHALL show "This receipt has been voided."

### Requirement 11: Receipt Listing and History

**User Story:** As a business user, I want to view all generated receipts in one place, so that I can find and manage them easily.

#### Acceptance Criteria

1. THE system SHALL provide a Receipt list page showing all receipts for the business.
2. THE list SHALL include: Receipt Number, Customer, Date, Amount, Status (Active/Voided), Actions (View, Share, Download PDF, Void).
3. THE list SHALL support filtering by customer, date range, and status.
4. THE list SHALL be sorted by receipt date descending (most recent first).
5. THE Receipt page SHALL be accessible from the Revenue/Finance navigation section.

### Requirement 12: Multi-Invoice Receipt (Global Payment)

**User Story:** As a business user, when a customer pays for multiple invoices at once, I want one receipt covering all invoices, so that the document matches the single bank transfer received.

#### Acceptance Criteria

1. WHEN a global payment is recorded (ParentPaymentId is the source), THE receipt SHALL list all child allocations as line items.
2. EACH line item SHALL show: Invoice Number, Invoice Total, Amount Applied, Outstanding After.
3. THE receipt total SHALL equal the parent payment's Amount (or Amount - CreditAmount if overpayment).
4. IF the global payment has a CreditAmount > 0, THE receipt SHALL include a note: "Credit of [amount] held on account."
5. THE receipt SHALL reference the parent payment's reference and method.

### Requirement 13: Tenant Isolation

**User Story:** As a business user, I want receipts and signatures scoped to my business only.

#### Acceptance Criteria

1. ALL receipt queries SHALL filter by BusinessId.
2. ALL signature queries SHALL filter by BusinessId.
3. THE share token SHALL validate that the receipt belongs to the business before rendering.
4. THE signature upload path SHALL be scoped to the business's storage directory.
