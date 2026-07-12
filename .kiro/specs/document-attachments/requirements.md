# Requirements Document

## Introduction

This document defines the requirements for the Document Attachments feature — a file attachment capability that allows business users to upload, view, download, and delete documents on Invoices, Credit Notes, Quotations, Payments, Purchases, Suppliers, and Customers. Files are stored on the filesystem (local for development, Azure Blob for production behind an abstraction) while metadata is persisted in the database. The feature is gated to the Professional plan tier and above via the existing subscription permission system using the `attachments` module key.

Supplier and Customer attachments serve as entity-level document storage — for collaboration agreements, contracts, certificates, and correspondence that relate to the business relationship rather than a specific transaction.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **Document_Attachment**: A metadata record representing a file attached to a business entity, stored in the `[document]` schema
- **Entity_Type**: A string identifier indicating the parent record type: 'Purchase', 'Invoice', 'CreditNote', 'Quotation', 'Payment', 'Supplier', or 'Customer'
- **Entity_Id**: The integer primary key of the parent record within the specified Entity_Type
- **File_Storage_Service**: An injectable service (IFileStorageService) that abstracts file storage operations, enabling local filesystem storage in development and Azure Blob in production
- **Storage_Path**: The relative path where a file is physically stored, structured as `{businessId}/{entityType}/{entityId}/{uniqueFileName}`
- **Attachment_Controller**: The MVC controller handling upload, download, and delete HTTP requests for attachments
- **Uploader**: The authenticated user who originally uploaded a specific attachment
- **Attachment_Panel**: A reusable Razor partial view component displayed on Purchase, Invoice, and Quotation detail pages for managing attachments
- **Soft_Gate_View**: A friendly teaser shown to Starter plan users indicating the attachments feature requires Professional plan or above
- **Allowed_Content_Types**: The set of permitted file MIME types — application/pdf, image/png, image/jpeg, image/webp
- **Allowed_Extensions**: The set of permitted file extensions — .pdf, .png, .jpg, .jpeg, .webp

## Requirements

### Requirement 1: Document Attachment Data Model

**User Story:** As a system architect, I want attachment metadata stored in a dedicated database table, so that the system can track which files belong to which business records without storing file content in the database.

#### Acceptance Criteria

1. THE Portal database SHALL contain a DocumentAttachment table in the `[document]` schema with columns: Id (INT IDENTITY), BusinessId (INT NOT NULL), EntityType (NVARCHAR(50) NOT NULL), EntityId (INT NOT NULL), FileName (NVARCHAR(255) NOT NULL), OriginalFileName (NVARCHAR(255) NOT NULL), ContentType (NVARCHAR(100) NOT NULL), StoragePath (NVARCHAR(500) NOT NULL), FileSizeBytes (BIGINT NOT NULL), UploadedByUserId (NVARCHAR(450) NOT NULL), IsDeleted (BIT NOT NULL DEFAULT 0), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE DocumentAttachment table SHALL have a foreign key from BusinessId to the Business table
3. THE DocumentAttachment table SHALL have a composite index on (BusinessId, EntityType, EntityId) for efficient lookups of attachments per record
4. THE DocumentAttachment table SHALL use soft-delete via the IsDeleted column rather than physical deletion of metadata records

### Requirement 2: File Storage Abstraction

**User Story:** As a developer, I want file storage abstracted behind an interface, so that the system can use local filesystem in development and Azure Blob Storage in production without changing business logic.

#### Acceptance Criteria

1. THE Portal SHALL define an IFileStorageService interface with methods for uploading a file (returning a storage path), downloading a file (returning a stream), and deleting a file (given a storage path)
2. WHEN the application runs in development mode, THE File_Storage_Service SHALL store files under `wwwroot/uploads/{businessId}/{entityType}/{entityId}/`
3. THE File_Storage_Service SHALL generate unique file names (using a GUID prefix) to prevent collisions when multiple files share the same original name
4. IF a file storage operation fails, THEN THE File_Storage_Service SHALL throw an exception with a descriptive message without exposing internal storage paths to the caller

### Requirement 3: File Upload

**User Story:** As a business user, I want to upload file attachments on Purchase, Invoice, and Quotation detail pages, so that I can associate supporting documents with my financial records.

#### Acceptance Criteria

1. WHEN a user submits a file upload request, THE Attachment_Controller SHALL validate the file against allowed content types and size limits before storing it
2. WHEN a valid file is uploaded, THE File_Storage_Service SHALL persist the file to storage and THE Attachment_Controller SHALL create a DocumentAttachment metadata record in the database
3. WHEN a file is uploaded successfully, THE Attachment_Controller SHALL return a JSON response containing the new attachment's Id, OriginalFileName, ContentType, FileSizeBytes, and CreatedAtUtc
4. THE upload operation SHALL be scoped to the authenticated user's BusinessId — the controller SHALL resolve BusinessId from the user's session claims
5. WHEN a file upload request is received, THE Attachment_Controller SHALL verify the parent entity (Purchase, Invoice, CreditNote, Quotation, Payment, Supplier, or Customer) exists and belongs to the user's business before accepting the file

### Requirement 4: File Size and Count Limits

**User Story:** As a platform operator, I want file size and attachment count limits enforced, so that storage costs remain predictable and no single record accumulates excessive files.

#### Acceptance Criteria

1. IF a file exceeds 5 MB (5,242,880 bytes), THEN THE Attachment_Controller SHALL reject the upload and return an error message stating the maximum allowed file size
2. IF a record already has 5 non-deleted attachments, THEN THE Attachment_Controller SHALL reject the upload and return an error message stating the maximum attachment count has been reached
3. THE count limit SHALL apply per combination of EntityType and EntityId — each Purchase, Invoice, or Quotation record may have up to 5 attachments independently

### Requirement 5: File Type Validation

**User Story:** As a security-conscious operator, I want file uploads restricted to safe document and image types, so that executable or dangerous files cannot be uploaded to the platform.

#### Acceptance Criteria

1. THE Attachment_Controller SHALL accept only files with extensions: .pdf, .png, .jpg, .jpeg, .webp
2. THE Attachment_Controller SHALL validate both the file extension and the Content-Type header against the Allowed_Content_Types set (application/pdf, image/png, image/jpeg, image/webp)
3. IF a file's extension does not match its declared Content-Type, THEN THE Attachment_Controller SHALL reject the upload with an error message indicating a content type mismatch
4. IF a file has a disallowed extension or content type, THEN THE Attachment_Controller SHALL reject the upload and return an error message listing the accepted file types

### Requirement 6: File Download

**User Story:** As a business user, I want to download previously uploaded attachments, so that I can retrieve supporting documents when needed.

#### Acceptance Criteria

1. WHEN a user requests a file download, THE Attachment_Controller SHALL verify the requested DocumentAttachment belongs to the user's BusinessId before serving the file
2. IF a user requests a DocumentAttachment that belongs to a different Business, THEN THE Attachment_Controller SHALL return HTTP 404 without revealing that the attachment exists
3. WHEN a valid download request is processed, THE Attachment_Controller SHALL return the file as a FileStreamResult with the correct Content-Type and the OriginalFileName as the download filename
4. IF the physical file is missing from storage but the metadata exists, THEN THE Attachment_Controller SHALL return an error indicating the file is unavailable

### Requirement 7: File Deletion

**User Story:** As a business user, I want to delete attachments I uploaded (or any attachment if I am an admin), so that outdated or incorrect documents can be removed.

#### Acceptance Criteria

1. WHEN a user requests deletion of an attachment they uploaded, THE Attachment_Controller SHALL soft-delete the DocumentAttachment record by setting IsDeleted to true
2. WHEN a business Owner requests deletion of any attachment within their business, THE Attachment_Controller SHALL soft-delete the DocumentAttachment record regardless of who uploaded it
3. IF a non-owner user attempts to delete an attachment uploaded by another user, THEN THE Attachment_Controller SHALL reject the request with an error message indicating insufficient permissions
4. WHEN a soft-delete is performed, THE Attachment_Controller SHALL not remove the physical file from storage — the file remains for potential audit or recovery purposes
5. THE Attachment_Controller SHALL return a JSON success response after deletion confirming the attachment Id that was removed

### Requirement 8: Attachment Listing

**User Story:** As a business user, I want to see all attachments associated with a record, so that I can review, download, or manage the supporting documents.

#### Acceptance Criteria

1. WHEN a user requests attachments for a specific EntityType and EntityId, THE Attachment_Controller SHALL return only non-deleted attachments belonging to the user's BusinessId
2. THE attachment list response SHALL include for each attachment: Id, OriginalFileName, ContentType, FileSizeBytes, CreatedAtUtc, and the UploadedByUserId display name
3. THE attachment list SHALL be ordered by CreatedAtUtc descending (most recent first)

### Requirement 9: Attachment Panel UI Component

**User Story:** As a developer, I want a reusable Razor partial view for managing attachments, so that the same upload/list/delete interface can be embedded on Purchase, Invoice, and Quotation detail pages consistently.

#### Acceptance Criteria

1. THE Attachment_Panel SHALL accept parameters for EntityType and EntityId to scope which attachments are displayed and where uploads are associated
2. THE Attachment_Panel SHALL display a list of existing attachments with file name, file size formatted in human-readable units, upload date, and action buttons for download and delete
3. THE Attachment_Panel SHALL display a file upload control that accepts only the Allowed_Extensions file types
4. THE Attachment_Panel SHALL show an image thumbnail preview for PNG, JPG, and WEBP files, and a PDF icon for PDF files
5. THE Attachment_Panel SHALL display the current attachment count relative to the maximum (e.g., "3 of 5 attachments used")
6. WHEN the maximum attachment count is reached, THE Attachment_Panel SHALL disable the upload control and display a message indicating the limit has been reached

### Requirement 10: Attachment Count Indicator on List Views

**User Story:** As a business user, I want to see at a glance which records have attachments, so that I can quickly identify documents with supporting files from list pages.

#### Acceptance Criteria

1. WHEN displaying Purchase, Invoice, CreditNote, Quotation, Payment, Supplier, or Customer list views, THE Portal SHALL show an attachment count indicator (paperclip icon with count) on records that have one or more non-deleted attachments
2. WHEN a record has zero non-deleted attachments, THE Portal SHALL not display any attachment indicator for that record
3. THE attachment count indicator SHALL reflect the current count of non-deleted attachments for the record

### Requirement 11: Plan Permission Gating

**User Story:** As a platform operator, I want the attachments feature gated to Professional plan and above, so that it serves as a value differentiator for paid tiers.

#### Acceptance Criteria

1. THE PlanPermissionFilter SHALL gate access to the Attachment_Controller using the `attachments` module key
2. WHEN a Starter plan user navigates to a page with the Attachment_Panel, THE Portal SHALL display the Soft_Gate_View teaser explaining the feature is available on Professional plan and above
3. WHEN a Professional or Enterprise plan user accesses a page with the Attachment_Panel, THE Portal SHALL render the full Attachment_Panel with upload, download, and delete capabilities

### Requirement 12: Tenant Isolation and Security

**User Story:** As a security-conscious operator, I want strict tenant isolation on all attachment operations, so that a user from one business can never access files belonging to another business.

#### Acceptance Criteria

1. THE Attachment_Controller SHALL resolve the authenticated user's BusinessId from session claims and use it as a mandatory filter on every query and operation
2. THE File_Storage_Service SHALL organize files in business-scoped directories — files from Business A SHALL never share a directory path with files from Business B
3. THE download endpoint SHALL verify both the DocumentAttachment.BusinessId matches the requesting user's BusinessId AND the attachment's IsDeleted flag is false before serving the file
4. IF a direct URL to a file is guessed or shared, THE system SHALL still enforce BusinessId validation — no file SHALL be served without authenticated, business-scoped authorization
5. THE Attachment_Controller SHALL validate Content-Type headers against actual file content signatures (magic bytes) for PDF and image files to prevent content-type spoofing

### Requirement 13: Mobile Responsive Upload and Preview

**User Story:** As a mobile user, I want to upload files from my device camera or file picker and preview images in a lightbox, so that I can manage attachments conveniently from a phone or tablet.

#### Acceptance Criteria

1. THE Attachment_Panel upload control SHALL include the `capture` attribute enabling camera access on mobile devices
2. THE Attachment_Panel SHALL render responsively, stacking attachment cards vertically on viewports narrower than 576px
3. WHEN a user taps an image attachment thumbnail on mobile, THE Portal SHALL open the image in a full-screen lightbox overlay with pinch-to-zoom support
4. THE lightbox SHALL include a close button and a download button accessible on touch devices

### Requirement 14: AJAX Interaction Pattern

**User Story:** As a user, I want upload, download, and delete operations to execute via AJAX without full page reloads, so that the experience feels fast and responsive.

#### Acceptance Criteria

1. WHEN a file upload begins, THE Attachment_Panel SHALL display BlockUI to prevent user interaction until the upload completes
2. WHEN an upload succeeds, THE Attachment_Panel SHALL unblock the UI, display a SweetAlert2 success message, and refresh the attachment list without a full page reload
3. WHEN an upload fails (validation error or server error), THE Attachment_Panel SHALL unblock the UI and display a SweetAlert2 error message with the specific failure reason
4. WHEN a delete operation is requested, THE Attachment_Panel SHALL display a SweetAlert2 confirmation dialog before proceeding with the delete request
5. WHEN a delete confirmation is accepted, THE Attachment_Panel SHALL use BlockUI during the AJAX call and show a SweetAlert2 success or error result upon completion


### Requirement 15: Entity-Level vs Transaction-Level Attachments

**User Story:** As a business user, I want to attach collaboration agreements and contracts to Supplier and Customer records, so that I can maintain relationship documentation alongside transactional documents.

#### Acceptance Criteria

1. THE Attachment_Panel on Supplier and Customer detail pages SHALL function identically to the panel on transactional records (Purchase, Invoice, etc.) — same upload, download, delete, and limit rules apply.
2. THE Attachment_Panel on Supplier detail pages SHALL use EntityType = 'Supplier' and EntityId = the supplier's Id.
3. THE Attachment_Panel on Customer detail pages SHALL use EntityType = 'Customer' and EntityId = the customer's Id.
4. THE 5-attachment limit SHALL apply independently per entity — a Supplier with 5 attachments does not affect the count on any Purchase from that supplier.
5. THE Attachment_Panel on Payment detail pages SHALL use EntityType = 'Payment' and EntityId = the payment's Id, supporting issued check scans or bank transfer confirmations.
