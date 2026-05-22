# Implementation Plan: Document Sharing

## Overview

This plan implements the Document Sharing feature — extending the existing quotation sharing mechanism to invoices and unifying both under a consistent architecture. Includes the InvoiceShare database table, InvoiceShareRepository, IInvoiceRenderer (Razor-based), IInvoiceSharingService, branded email templates, public endpoints, share dialog on Invoice Detail, SharedLinksController with management page, and DI registration. Tasks follow the existing ASP.NET Core MVC 8 + SQL Server + Database-First patterns using raw SQL repositories.

## Tasks

- [x] 1. Database migration — InvoiceShare table
  - [x] 1.1 Create migration 042: Create InvoiceShare table
    - Create `Portal.Database/Migrations/042_CreateInvoiceShareTable.sql`
    - CREATE TABLE [invoice].[InvoiceShare] with Id (IDENTITY), InvoiceId, BusinessId, ShareToken (NVARCHAR(128) UNIQUE), SnapshotHtml (NVARCHAR(MAX)), CustomerEmail (NVARCHAR(200)), ExpiresAtUtc (DATETIMEOFFSET), CreatedAtUtc (DATETIMEOFFSET DEFAULT SYSDATETIMEOFFSET()), CreatedByUserId (NVARCHAR(450)), IsActive (BIT DEFAULT 1)
    - Add PK_InvoiceShare, FK_InvoiceShare_Invoice → [invoice].[Invoice], FK_InvoiceShare_Business → [portal].[Business]
    - Add UX_InvoiceShare_ShareToken unique nonclustered index on ShareToken
    - Add IX_InvoiceShare_InvoiceId nonclustered index on InvoiceId
    - Add IX_InvoiceShare_BusinessId nonclustered index on BusinessId
    - Use idempotent IF NOT EXISTS pattern matching existing migrations
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

- [x] 2. Entity and repository layer
  - [x] 2.1 Create InvoiceShare entity
    - Create `Portal.Infrastructure/Entities/InvoiceShare.cs`
    - Properties: Id, InvoiceId, BusinessId, ShareToken, SnapshotHtml, CustomerEmail, ExpiresAtUtc, CreatedAtUtc, CreatedByUserId, IsActive
    - Navigation properties: Invoice, Business
    - Mirror the existing ProposalShare entity pattern exactly
    - _Requirements: 9.1_

  - [x] 2.2 Create InvoiceShareRepository
    - Create `Portal.Infrastructure/Repositories/InvoiceShareRepository.cs` extending GenericStoredProcedureRepository<InvoiceShare>
    - Implement: InsertAsync(InvoiceShare entity) — INSERT into [invoice].[InvoiceShare]
    - Implement: GetByTokenAsync(string token) — SELECT by ShareToken
    - Implement: GetActiveByInvoiceIdAsync(int invoiceId) — SELECT WHERE InvoiceId = @InvoiceId AND IsActive = 1
    - Implement: GetByInvoiceIdAsync(int invoiceId) — SELECT all shares for an invoice
    - Implement: GetByBusinessIdAsync(int businessId) — SELECT all shares for a business (Shared Links page)
    - Implement: DeactivateByInvoiceIdAsync(int invoiceId) — UPDATE IsActive = 0 WHERE InvoiceId AND IsActive = 1
    - Implement: DeactivateByIdAsync(int id, int businessId) — UPDATE IsActive = 0 WHERE Id AND BusinessId (tenant-safe cancel)
    - Use full table names in SQL, parameterized queries, null-safe SqlParameter patterns, try/catch with rethrow
    - _Requirements: 3.1, 3.5, 9.1, 11.1, 11.3_

  - [x] 2.3 Extend ProposalShareRepository with new methods
    - Add GetByBusinessIdAsync(int businessId) — SELECT all shares for a business (Shared Links page)
    - Add DeactivateByIdAsync(int id, int businessId) — UPDATE IsActive = 0 WHERE Id AND BusinessId (tenant-safe cancel)
    - Follow existing repository patterns with full table names and parameterized queries
    - _Requirements: 8.1, 8.7, 11.1, 11.3_

- [x] 3. Checkpoint — Ensure schema and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Invoice renderer service
  - [x] 4.1 Create IInvoiceRenderer interface
    - Create `Portal.Infrastructure/Services/IInvoiceRenderer.cs`
    - Define: Task<string> RenderAsync(int invoiceId)
    - _Requirements: 10.1, 10.3_

  - [x] 4.2 Implement InvoiceRenderer using Razor view rendering
    - Create `Portal.Infrastructure/Services/InvoiceRenderer.cs`
    - Inject: IRazorViewEngine, ITempDataProvider, IServiceProvider, InvoiceRepository, InvoiceLineRepository, InvoiceSectionRepository, CustomerRepository, BusinessProfileRepository, BusinessLogoRepository, BusinessPaymentDetailRepository
    - Render `Views/Invoice/Preview.cshtml` to string using RazorViewEngine
    - Populate ViewBag with same data as the existing Preview action (Lines, Sections, CustomerName, LogoUrl, BusinessName, Profile, PaymentDetails)
    - Set autoPrint = false and remove "Download PDF" button from snapshot output
    - Produce self-contained HTML with all styles inline (no external stylesheet links)
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [ ]* 4.3 Write property test for invoice renderer
    - **Property 13: Invoice renderer produces self-contained HTML with all required fields**
    - **Validates: Requirements 10.1, 10.2**

- [x] 5. Invoice sharing service
  - [x] 5.1 Create IInvoiceSharingService interface
    - Create `Portal.Infrastructure/Services/IInvoiceSharingService.cs`
    - Define: ShareAsync(int invoiceId, DateTimeOffset expiresAtUtc, bool sendEmail, string userId)
    - Define: GetByTokenAsync(string token)
    - Define: GetActiveShareByInvoiceIdAsync(int invoiceId)
    - Define: GetSharesByBusinessIdAsync(int businessId)
    - Define: CancelShareAsync(int shareId)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 7.3_

  - [x] 5.2 Implement InvoiceSharingService
    - Create `Portal.Infrastructure/Services/InvoiceSharingService.cs`
    - Inject: IInvoiceRenderer, InvoiceShareRepository, InvoiceRepository, CustomerRepository, IEmailService, ICurrentTenantService, ILogger<InvoiceSharingService>
    - ShareAsync: validate invoice exists and belongs to business, validate customer has email, validate expiration ≥ 1 day in future (default 7 days if not specified), generate 32-byte cryptographically secure URL-safe Base64 token, deactivate previous active share for same invoice, render HTML snapshot via IInvoiceRenderer, persist InvoiceShare record, optionally send email (catch and log failures without rolling back share)
    - GetByTokenAsync: delegate to repository
    - GetActiveShareByInvoiceIdAsync: delegate to repository
    - GetSharesByBusinessIdAsync: delegate to repository with tenant BusinessId
    - CancelShareAsync: verify share belongs to current business, call DeactivateByIdAsync
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.4, 7.1, 7.2, 7.3, 7.4, 7.5, 11.1, 11.3_

  - [ ]* 5.3 Write property test for token generation
    - **Property 1: Token generation produces URL-safe tokens of sufficient length**
    - **Validates: Requirements 1.2, 3.2**

  - [ ]* 5.4 Write property test for expiration persistence
    - **Property 2: Custom expiration date is persisted exactly**
    - **Validates: Requirements 1.4, 3.4**

  - [ ]* 5.5 Write property test for invoice share deactivation
    - **Property 4: New invoice share deactivates previous active share**
    - **Validates: Requirements 3.5**

  - [ ]* 5.6 Write property test for email failure resilience
    - **Property 7: Email failure does not roll back share record**
    - **Validates: Requirements 2.4, 4.4**

  - [ ]* 5.7 Write property test for expiration validation
    - **Property 10: Expiration validation rejects dates less than 1 day in the future**
    - **Validates: Requirements 7.2**

  - [ ]* 5.8 Write property test for cancel operation
    - **Property 11: Cancel sets IsActive to false**
    - **Validates: Requirements 7.3**

  - [ ]* 5.9 Write property test for tenant isolation
    - **Property 15: Tenant isolation for share operations**
    - **Validates: Requirements 11.1, 11.2, 11.3**

- [x] 6. Checkpoint — Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Email templates and IEmailService extension
  - [x] 7.1 Extend IEmailService with invoice email method
    - Add to `Portal.Infrastructure/Services/IEmailService.cs`: SendInvoiceEmailAsync(string toEmail, string shareToken, string invoiceNumber, string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc)
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 7.2 Implement quotation email template (blue accent #0D5EA6)
    - Update existing SendProposalEmailAsync implementation with branded HTML template
    - Self-contained HTML with inline styles, no external CSS
    - Include: blue header bar (#0D5EA6), quotation reference, total amount, valid-until date, "View Proposal" CTA button linking to `/proposal/{token}`
    - Compatible with major email clients
    - _Requirements: 2.2, 2.3_

  - [x] 7.3 Implement invoice email template (green/teal accent #129867)
    - Implement SendInvoiceEmailAsync with branded HTML template
    - Self-contained HTML with inline styles, no external CSS
    - Include: green/teal header bar (#129867), invoice number, total amount, due date, "View Invoice" CTA button linking to `/invoice-view/{token}`
    - Compatible with major email clients
    - _Requirements: 4.2, 4.3_

  - [ ]* 7.4 Write property test for quotation email content
    - **Property 5: Quotation email contains required elements with blue accent**
    - **Validates: Requirements 2.2, 2.3**

  - [ ]* 7.5 Write property test for invoice email content
    - **Property 6: Invoice email contains required elements with green accent**
    - **Validates: Requirements 4.2, 4.3**

- [ ] 8. Public endpoints
  - [x] 8.1 Create InvoiceViewController (public, unauthenticated)
    - Create `Portal.Web/Controllers/InvoiceViewController.cs` with [AllowAnonymous]
    - GET /invoice-view/{token}: lookup share by token, if not found return 404, if expired or IsActive = false return "Unavailable" view, if valid return SnapshotHtml with Content-Type text/html and Cache-Control: no-store
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 8.2 Update ProposalController to check IsActive flag and use Unavailable view
    - Modify ViewProposal action: check IsActive flag in addition to expiration
    - Replace "Expired" view with generic "Unavailable" view ("This link is no longer available")
    - Share the Unavailable view between InvoiceViewController and ProposalController
    - _Requirements: 6.1, 6.2, 7.5_

  - [x] 8.3 Create shared Unavailable view
    - Create `Portal.Web/Views/Shared/Unavailable.cshtml`
    - Display generic message: "This link is no longer available"
    - Clean, branded layout without leaking state information (no distinction between expired/cancelled)
    - _Requirements: 5.3, 5.4, 6.1, 6.2_

  - [ ]* 8.4 Write property test for valid token response
    - **Property 8: Valid active non-expired token returns snapshot HTML**
    - **Validates: Requirements 5.2, 5.6**

  - [ ]* 8.5 Write property test for expired/cancelled/invalid token responses
    - **Property 9: Expired or cancelled token returns unavailable page; invalid token returns 404**
    - **Validates: Requirements 5.3, 5.4, 5.5, 6.1, 6.2, 7.4, 7.5**

- [x] 9. Share dialog on Invoice Detail page
  - [x] 9.1 Add Share action to InvoiceController
    - POST /Invoice/Share — accepts invoiceId, optional custom expiration date, sendEmail flag
    - Validate expiration date (minimum 1 day in future), call IInvoiceSharingService.ShareAsync
    - Return JSON with share URL on success, validation errors on failure
    - _Requirements: 3.1, 3.3, 3.4, 7.2_

  - [x] 9.2 Add share dialog UI to Invoice Detail view
    - Add "Share" button to Invoice Detail page (similar to existing quotation share pattern)
    - Modal dialog with: customer email (pre-filled from invoice customer), expiration date picker (default 7 days), send email checkbox
    - Display generated share URL after successful share
    - Show existing active share link if one exists
    - _Requirements: 3.1, 3.3, 3.4, 7.1_

  - [ ]* 9.3 Write property test for quotation share deactivation
    - **Property 3: New quotation share deactivates previous active share**
    - **Validates: Requirements 1.5**

- [ ] 10. SharedLinksController and management page
  - [x] 10.1 Create SharedLinksController
    - Create `Portal.Web/Controllers/SharedLinksController.cs` with [Authorize]
    - Accessible to users with either Quotation or Invoice module access
    - GET /shared-links — fetch all proposal shares and invoice shares for current business, combine into unified SharedLinkViewModel list, render management page
    - POST /shared-links/cancel-proposal/{id} — call ProposalShareRepository.DeactivateByIdAsync with tenant check
    - POST /shared-links/cancel-invoice/{id} — call IInvoiceSharingService.CancelShareAsync with tenant check
    - Derive status: IsActive = false → "Cancelled", IsActive = true && ExpiresAtUtc ≤ now → "Expired", else → "Active"
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 11.1, 11.2_

  - [x] 10.2 Create SharedLinkViewModel
    - Create `Portal.Infrastructure/Models/SharedLinkViewModel.cs`
    - Properties: Id, DocumentType ("Quotation"/"Invoice"), DocumentReference, CustomerName, CustomerEmail, CreatedAtUtc, ExpiresAtUtc, Status ("Active"/"Expired"/"Cancelled"), IsActive
    - _Requirements: 8.2, 8.3_

  - [x] 10.3 Create Shared Links management view
    - Create `Portal.Web/Views/SharedLinks/Index.cshtml`
    - Display unified table: Document Type, Reference, Customer Name, Customer Email, Created Date, Expiry Date, Status (with colour coding: Active=green, Expired=amber, Cancelled=red)
    - Show "Cancel" button only for active links
    - Follow MyChair Design System (cards, border radius, soft shadows)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7_

  - [ ]* 10.4 Write property test for status derivation
    - **Property 12: Status derivation function**
    - **Validates: Requirements 8.3, 8.4, 8.5, 8.6**

- [x] 11. DI registration
  - [x] 11.1 Register new services and repositories in DI container
    - Register InvoiceShareRepository
    - Register IInvoiceRenderer → InvoiceRenderer
    - Register IInvoiceSharingService → InvoiceSharingService
    - Update IEmailService registration if implementation class changed
    - _Requirements: 3.1, 10.1_

- [x] 12. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit as specified in the design document (minimum 100 iterations per property)
- All repositories follow the GenericStoredProcedureRepository pattern with raw SQL, full table names, and null-safe SqlParameter usage
- The InvoiceShareRepository mirrors ProposalShareRepository exactly in method signatures and SQL patterns
- Email templates are self-contained HTML with inline styles for email client compatibility
- The "Unavailable" view is shared between InvoiceViewController and ProposalController to avoid leaking state information
