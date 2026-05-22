# Requirements Document

## Introduction

The Document Sharing feature provides a unified system for sharing quotations and invoices with customers via secure, time-limited public links. Currently, quotations have a basic sharing mechanism (ProposalShare table) but invoices have none. This feature unifies both under a consistent pattern: generate an HTML snapshot, store it with a secure token, and serve it on a public URL. Managers can optionally send an automated email notification with a branded HTML template (blue accent for quotations, green/teal accent for invoices). A dedicated "Shared Links" management page provides visibility into all shared documents with their status and revocation controls.

## Glossary

- **Sharing_Service**: The unified service that orchestrates document sharing for both quotations and invoices — generating tokens, storing snapshots, managing expiration, and dispatching emails.
- **Invoice_Share**: A database record in the `[invoice]` schema representing a point-in-time HTML snapshot of an invoice shared via a secure link.
- **Proposal_Share**: The existing database record in the `[quotation]` schema representing a shared quotation snapshot (to be enhanced with the unified pattern).
- **Share_Token**: A cryptographically secure, URL-safe string that uniquely identifies a shared document link.
- **Snapshot_HTML**: A self-contained HTML rendering of a document captured at the moment of sharing.
- **Manager**: An authenticated Portal user with access to the Quotation or Invoice module who initiates sharing.
- **Customer**: The external recipient who views the shared document via the public link.
- **Shared_Links_Page**: A dedicated management page listing all shared quotations and invoices with their current status.
- **Invoice_View_Controller**: The public, unauthenticated controller serving shared invoice snapshots at `/invoice-view/{token}`.
- **Proposal_Controller**: The existing public, unauthenticated controller serving shared quotation snapshots at `/proposal/{token}`.
- **Email_Service**: The existing service responsible for sending branded HTML emails via SMTP.
- **Invoice_Renderer**: The service that renders an invoice into a self-contained HTML snapshot for sharing.

## Requirements

### Requirement 1: Share a Quotation via Link Only

**User Story:** As a Manager, I want to create a shareable link for a quotation without sending an email, so that I can share the link manually through my preferred channel.

#### Acceptance Criteria

1. WHEN the Manager requests a share link for a quotation without selecting the email option, THE Sharing_Service SHALL generate a Share_Token, render the Snapshot_HTML, persist a Proposal_Share record, and return the public URL without sending any email.
2. THE Sharing_Service SHALL generate a cryptographically secure, URL-safe Share_Token of at least 32 bytes for each share operation.
3. WHEN a Proposal_Share record is created, THE Sharing_Service SHALL set the default expiration to 7 days from creation if no custom expiration is provided.
4. WHERE the Manager provides a custom expiration date, THE Sharing_Service SHALL use that date as the ExpiresAtUtc value.
5. THE Sharing_Service SHALL deactivate any previously active Proposal_Share for the same quotation before creating a new one.

### Requirement 2: Share a Quotation via Link with Email

**User Story:** As a Manager, I want to share a quotation and automatically send a branded email to the customer, so that the customer is notified immediately.

#### Acceptance Criteria

1. WHEN the Manager requests a share with email for a quotation, THE Sharing_Service SHALL generate the share link and send a branded HTML email to the customer's email address.
2. THE Email_Service SHALL render the quotation email with a BLUE accent colour (#0D5EA6) for the header bar and CTA button.
3. THE Email_Service SHALL include the quotation reference number, total amount, valid-until date, and a "View Proposal" CTA button linking to `/proposal/{token}` in the email body.
4. IF the email dispatch fails, THEN THE Sharing_Service SHALL still persist the Proposal_Share record and log the failure without rolling back the share.

### Requirement 3: Share an Invoice via Link Only

**User Story:** As a Manager, I want to create a shareable link for an invoice without sending an email, so that I can distribute the link through my preferred channel.

#### Acceptance Criteria

1. WHEN the Manager requests a share link for an invoice without selecting the email option, THE Sharing_Service SHALL generate a Share_Token, render the Snapshot_HTML via the Invoice_Renderer, persist an Invoice_Share record, and return the public URL without sending any email.
2. THE Sharing_Service SHALL generate a cryptographically secure, URL-safe Share_Token of at least 32 bytes for each invoice share operation.
3. WHEN an Invoice_Share record is created, THE Sharing_Service SHALL set the default expiration to 7 days from creation if no custom expiration is provided.
4. WHERE the Manager provides a custom expiration date, THE Sharing_Service SHALL use that date as the ExpiresAtUtc value.
5. THE Sharing_Service SHALL deactivate any previously active Invoice_Share for the same invoice before creating a new one.

### Requirement 4: Share an Invoice via Link with Email

**User Story:** As a Manager, I want to share an invoice and automatically send a branded email to the customer, so that the customer receives the invoice notification promptly.

#### Acceptance Criteria

1. WHEN the Manager requests a share with email for an invoice, THE Sharing_Service SHALL generate the share link and send a branded HTML email to the customer's email address.
2. THE Email_Service SHALL render the invoice email with a GREEN/TEAL accent colour (#129867) for the header bar and CTA button.
3. THE Email_Service SHALL include the invoice number, total amount, due date, and a "View Invoice" CTA button linking to `/invoice-view/{token}` in the email body.
4. IF the email dispatch fails, THEN THE Sharing_Service SHALL still persist the Invoice_Share record and log the failure without rolling back the share.

### Requirement 5: Public Invoice View Endpoint

**User Story:** As a Customer, I want to view a shared invoice via a public URL, so that I can review the invoice details without needing a Portal account.

#### Acceptance Criteria

1. THE Invoice_View_Controller SHALL serve shared invoice snapshots at the public endpoint `/invoice-view/{token}`.
2. WHEN a valid, active, non-expired token is provided, THE Invoice_View_Controller SHALL return the stored Snapshot_HTML with content type `text/html`.
3. WHEN an expired token is provided, THE Invoice_View_Controller SHALL return a generic "This link is no longer available" page.
4. WHEN a cancelled (IsActive = false) token is provided, THE Invoice_View_Controller SHALL return a generic "This link is no longer available" page.
5. WHEN an invalid or non-existent token is provided, THE Invoice_View_Controller SHALL return a 404 Not Found response.
6. THE Invoice_View_Controller SHALL set the `Cache-Control` response header to `no-store` to prevent intermediary caching.

### Requirement 6: Public Quotation View Endpoint Enhancement

**User Story:** As a Customer, I want to see a clear message when a quotation link is cancelled or expired, so that I understand the link is no longer valid.

#### Acceptance Criteria

1. WHEN an expired token is provided to the Proposal_Controller, THE Proposal_Controller SHALL return a generic "This link is no longer available" page.
2. WHEN a cancelled (IsActive = false) token is provided to the Proposal_Controller, THE Proposal_Controller SHALL return a generic "This link is no longer available" page.

### Requirement 7: Link Expiration Management

**User Story:** As a Manager, I want shared links to expire automatically and be revocable at any time, so that I maintain control over document access.

#### Acceptance Criteria

1. THE Sharing_Service SHALL set a default expiration of 7 days from creation for all new share links when no custom expiration is specified.
2. WHERE the Manager specifies a custom expiration date, THE Sharing_Service SHALL validate that the date is at least 1 day in the future.
3. WHEN the Manager cancels a share link, THE Sharing_Service SHALL set the IsActive flag to false on the corresponding share record.
4. WHEN a share link is cancelled, THE Sharing_Service SHALL make the change effective immediately for subsequent public URL requests.
5. THE Proposal_Controller and Invoice_View_Controller SHALL check both the IsActive flag and ExpiresAtUtc on every request to determine link validity.

### Requirement 8: Shared Links Management Page

**User Story:** As a Manager, I want a dedicated page listing all shared quotations and invoices, so that I can monitor and manage all shared documents in one place.

#### Acceptance Criteria

1. THE Shared_Links_Page SHALL display all shared quotations and invoices for the current business in a unified list.
2. THE Shared_Links_Page SHALL display the following for each shared item: document type (Quotation/Invoice), document reference number, customer name, customer email, creation date, expiration date, and current status.
3. THE Shared_Links_Page SHALL display the status of each link as one of: Active, Expired, or Cancelled.
4. WHEN a link's ExpiresAtUtc is in the past and IsActive is true, THE Shared_Links_Page SHALL display the status as "Expired".
5. WHEN a link's IsActive is false, THE Shared_Links_Page SHALL display the status as "Cancelled".
6. WHEN a link's ExpiresAtUtc is in the future and IsActive is true, THE Shared_Links_Page SHALL display the status as "Active".
7. THE Shared_Links_Page SHALL provide a "Cancel" action for each active link, allowing the Manager to revoke access.
8. THE Shared_Links_Page SHALL be accessible to authenticated users with access to either the Quotation or Invoice module.

### Requirement 9: Invoice Share Database Schema

**User Story:** As a developer, I want a dedicated Invoice_Share table following the same pattern as Proposal_Share, so that invoice sharing data is stored consistently.

#### Acceptance Criteria

1. THE Invoice_Share table SHALL be created in the `[invoice]` schema with columns: Id, InvoiceId, BusinessId, ShareToken, SnapshotHtml, CustomerEmail, ExpiresAtUtc, CreatedAtUtc, CreatedByUserId, and IsActive.
2. THE Invoice_Share table SHALL have a unique constraint on ShareToken to prevent duplicate tokens.
3. THE Invoice_Share table SHALL have a foreign key to `[invoice].[Invoice]` on InvoiceId.
4. THE Invoice_Share table SHALL have a foreign key to `[portal].[Business]` on BusinessId.
5. THE Invoice_Share table SHALL have a non-clustered index on InvoiceId for efficient lookups.
6. THE Invoice_Share table SHALL have a non-clustered index on BusinessId for tenant-scoped queries.

### Requirement 10: Invoice HTML Snapshot Rendering

**User Story:** As a Manager, I want the shared invoice to be a self-contained HTML snapshot, so that the customer sees a consistent, styled document regardless of future changes.

#### Acceptance Criteria

1. WHEN an invoice is shared, THE Invoice_Renderer SHALL produce a self-contained HTML document including all styles inline.
2. THE Invoice_Renderer SHALL include the business name, address, logo, invoice number, invoice date, due date, customer details, all line items with sections, subtotal, tax amount, total amount, and payment details in the rendered output.
3. THE Invoice_Renderer SHALL use the existing `Portal.Web/Views/Invoice/Preview.cshtml` template as the basis for the HTML snapshot.
4. THE Invoice_Renderer SHALL capture the invoice state at the moment of sharing, producing an immutable snapshot that does not change if the source invoice is later modified.

### Requirement 11: Tenant Isolation for Shared Links

**User Story:** As a Manager, I want shared links to be scoped to my business, so that I cannot see or manage links belonging to other businesses.

#### Acceptance Criteria

1. THE Sharing_Service SHALL scope all share queries by the current BusinessId.
2. THE Shared_Links_Page SHALL only display share records belonging to the authenticated user's current business.
3. WHEN the Manager attempts to cancel a share link, THE Sharing_Service SHALL verify the link belongs to the current business before deactivating it.

