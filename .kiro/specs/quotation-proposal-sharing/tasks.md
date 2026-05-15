# Implementation Plan: Quotation Proposal Sharing

## Overview

Implement branded proposal generation and sharing from existing quotations. The plan follows a bottom-up approach: database schema first, then entities, EF Core configuration, repositories, services, controllers, Razor views, and finally DI wiring.

## Tasks

- [ ] 1. Database migrations
  - [x] 1.1 Create migration script for ProposalShare, ProposalSection, ProposalShareLogo tables and QuotationLine alterations
    - Create file `Portal.Database/Migrations/022_CreateProposalSharingTables.sql`
    - Create `[quotation].[ProposalSection]` table with Id, QuotationId (FK), Name, SortOrder, ColumnConfiguration
    - Create `[quotation].[ProposalShare]` table with Id, QuotationId (FK), BusinessId (FK), ShareToken (UNIQUE), SnapshotHtml, CustomerEmail, ExpiresAtUtc, CreatedAtUtc, CreatedByUserId, IsActive
    - Create `[quotation].[ProposalShareLogo]` junction table with Id, ProposalShareId (FK), BusinessLogoId (FK), Placement (CHECK 'Hero'/'Meta'), SortOrder
    - ALTER `[quotation].[QuotationLine]` ADD ReferenceUrl NVARCHAR(2048) NULL, ProposalSectionId INT NULL with FK to ProposalSection ON DELETE SET NULL
    - Create all indexes as specified in design
    - _Requirements: 1.5, 2.1, 3.1, 3.5, 3.6, 9.1_

  - [x] 1.2 Create migration script for BusinessLogo table
    - Create file `Portal.Database/Migrations/023_CreateBusinessLogoTable.sql`
    - Create `[portal].[BusinessLogo]` table with Id, BusinessId (FK), DisplayName, FileName, ContentType, FileSizeBytes, PublicUrl, CreatedAtUtc
    - Create index on BusinessId
    - _Requirements: 10.1, 10.3, 10.5_

- [ ] 2. Entity classes and models
  - [x] 2.1 Create ProposalSection entity
    - Create file `Portal.Infrastructure/Entities/ProposalSection.cs`
    - Properties: Id, QuotationId, Name, SortOrder, ColumnConfiguration, navigation to Quotation and QuotationLines collection
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 2.2 Create ProposalShare entity
    - Create file `Portal.Infrastructure/Entities/ProposalShare.cs`
    - Properties: Id, QuotationId, BusinessId, ShareToken, SnapshotHtml, CustomerEmail, ExpiresAtUtc, CreatedAtUtc, CreatedByUserId, IsActive, navigation to Quotation and Business
    - _Requirements: 3.5, 8.1_

  - [x] 2.3 Create BusinessLogo entity
    - Create file `Portal.Infrastructure/Entities/BusinessLogo.cs`
    - Properties: Id, BusinessId, DisplayName, FileName, ContentType, FileSizeBytes, PublicUrl, CreatedAtUtc, navigation to Business
    - _Requirements: 10.1, 10.3_

  - [x] 2.4 Modify QuotationLine entity to add ReferenceUrl and ProposalSectionId
    - Modify `Portal.Infrastructure/Entities/QuotationLine.cs`
    - Add `public string? ReferenceUrl { get; set; }` and `public int? ProposalSectionId { get; set; }` and navigation `public ProposalSection? ProposalSection { get; set; }`
    - _Requirements: 9.1, 2.1_

  - [x] 2.5 Create ProposalRenderModel and related DTOs
    - Create file `Portal.Infrastructure/Models/ProposalRenderModel.cs`
    - Include ProposalRenderModel, ProposalSectionRenderModel, ProposalLineRenderModel, ProposalLogoRenderModel classes
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 2.2, 2.3, 11.1, 11.2_

- [ ] 3. EF Core configuration
  - [x] 3.1 Update PortalDbContext with new DbSets and entity configuration
    - Modify `Portal.Infrastructure/Data/PortalDbContext.cs`
    - Add DbSets: ProposalShares, ProposalSections, BusinessLogos
    - Add ConfigureProposalShare, ConfigureProposalSection, ConfigureBusinessLogo methods
    - Configure QuotationLine ReferenceUrl (MaxLength 2048) and ProposalSectionId FK in existing ConfigureQuotationLine
    - Add global query filters for ProposalShare (BusinessId) and BusinessLogo (BusinessId)
    - _Requirements: 7.3, 3.6_

- [ ] 4. Repositories
  - [x] 4.1 Create ProposalShareRepository
    - Create file `Portal.Infrastructure/Repositories/ProposalShareRepository.cs`
    - Methods: GetByTokenAsync, GetActiveByQuotationIdAsync, GetByQuotationIdAsync, InsertAsync, DeactivateByQuotationIdAsync
    - Follow existing repository pattern (GenericStoredProcedureRepository, raw SQL, SqlParameter, try/catch/throw)
    - _Requirements: 3.5, 4.1, 8.2, 8.3_

  - [x] 4.2 Create BusinessLogoRepository
    - Create file `Portal.Infrastructure/Repositories/BusinessLogoRepository.cs`
    - Methods: GetByBusinessIdAsync, GetByIdAsync, InsertAsync, DeleteAsync, GetCountByBusinessIdAsync
    - _Requirements: 10.1, 10.3, 10.4_

  - [x] 4.3 Create ProposalSectionRepository
    - Create file `Portal.Infrastructure/Repositories/ProposalSectionRepository.cs`
    - Methods: GetByQuotationIdAsync, InsertAsync, UpdateAsync, DeleteAsync
    - _Requirements: 2.1_

  - [x] 4.4 Update QuotationLineRepository to include ReferenceUrl and ProposalSectionId columns
    - Modify `Portal.Infrastructure/Repositories/QuotationLineRepository.cs`
    - Add ReferenceUrl and ProposalSectionId to SELECT, INSERT, and UPDATE queries
    - _Requirements: 9.1, 9.2_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Service interfaces and implementations
  - [x] 6.1 Create ILogoService interface and LogoService implementation
    - Create file `Portal.Infrastructure/Services/ILogoService.cs`
    - Create file `Portal.Web/Services/LogoService.cs`
    - Methods: UploadAsync (validate format, size, count ≤ 20, save to wwwroot/uploads/logos/, insert DB record), GetByBusinessIdAsync, DeleteAsync (remove file + DB record)
    - Validate: PNG/JPG/SVG/WebP only, max 2MB, max 20 per business
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [ ]* 6.2 Write property tests for LogoService validation
    - **Property 23: Logo upload validation (format, size, count)**
    - **Property 24: Logo deletion removes from library**
    - **Validates: Requirements 10.1, 10.2, 10.3, 10.4**

  - [x] 6.3 Create IViewRenderService interface and ViewRenderService implementation
    - Create file `Portal.Web/Services/IViewRenderService.cs`
    - Create file `Portal.Web/Services/ViewRenderService.cs`
    - Inject IRazorViewEngine, ITempDataProvider, IServiceProvider
    - Method: RenderViewToStringAsync(viewName, model) — creates ActionContext, resolves view, renders to StringWriter
    - _Requirements: 1.1_

  - [x] 6.4 Create IProposalRenderer interface and ProposalRenderer implementation
    - Create file `Portal.Infrastructure/Services/IProposalRenderer.cs`
    - Create file `Portal.Web/Services/ProposalRenderer.cs`
    - Inject IViewRenderService
    - Method: RenderAsync(ProposalRenderModel) — calls ViewRenderService with "~/Views/Proposal/Snapshot.cshtml"
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 6.5 Create IProposalService interface and ProposalService implementation
    - Create file `Portal.Infrastructure/Services/IProposalService.cs`
    - Create file `Portal.Web/Services/ProposalService.cs`
    - Inject ProposalShareRepository, ProposalSectionRepository, QuotationRepository, QuotationLineRepository, BusinessRepository, CustomerRepository, BusinessLogoRepository, IProposalRenderer, IEmailService, ICurrentTenantService
    - ShareAsync: validate customer email, validate expiration (≥1 day future), load all data, build ProposalRenderModel, render HTML, generate 32-byte token (RandomNumberGenerator → Base64Url), deactivate previous shares, insert new ProposalShare, send email, return share
    - GetByTokenAsync, GetActiveShareByQuotationIdAsync, GetSharesByQuotationIdAsync
    - _Requirements: 1.7, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 6.1, 6.4, 8.1, 8.3_

  - [ ]* 6.6 Write property tests for ProposalService
    - **Property 8: Share token minimum length (≥32 bytes)**
    - **Property 9: Share token uniqueness**
    - **Property 10: Expiration date validation (≥1 day future)**
    - **Property 16: Customer email required for sharing**
    - **Property 20: Reshare deactivates previous token**
    - **Validates: Requirements 3.1, 3.4, 3.6, 6.4, 8.3**

- [ ] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Proposal Razor view (Snapshot.cshtml)
  - [x] 8.1 Create the proposal snapshot Razor view
    - Create file `Portal.Web/Views/Proposal/Snapshot.cshtml`
    - Strongly-typed view with @model ProposalRenderModel
    - Self-contained HTML with inline CSS only (no external stylesheets or scripts)
    - Use MyChair design system: Manrope headings, Inter body, primary blue #0D5EA6, accent cyan #57B8E8
    - Render hero logos (max-height: 68px) and metadata card logo (max-height: 40px)
    - Render business profile, customer details, quotation header
    - Render each ProposalSection as a distinct card with heading and table
    - Render subscription columns (Monthly Price, Daily Cost, Annual Price) vs one-time columns (Qty, Unit Price, Final Price) based on ColumnConfiguration
    - Render line descriptions as hyperlinks when ReferenceUrl is present (target="_blank")
    - Include @page and @media print rules for A4 PDF output
    - Include "Download PDF" button (hidden in print)
    - Match design from proposal_mock.html
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.9, 1.10, 2.2, 2.3, 2.4, 5.1, 5.2, 5.3, 9.4, 11.3, 11.4, 11.5_

  - [ ]* 8.2 Write property tests for ProposalRenderer output
    - **Property 1: Rendered snapshot contains all input data**
    - **Property 2: Rendered snapshot is self-contained (no external dependencies)**
    - **Property 3: ReferenceUrl renders as hyperlink**
    - **Property 5: Print CSS inclusion**
    - **Property 6: Section column configuration rendering**
    - **Property 7: One section card per ProposalSection**
    - **Property 25: Logo rendering dimensions**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.10, 2.2, 2.3, 2.4, 9.4, 11.3, 11.4**

- [ ] 9. Controllers
  - [x] 9.1 Create ProposalController (public, unauthenticated)
    - Create file `Portal.Web/Controllers/ProposalController.cs`
    - [AllowAnonymous] controller with route `/proposal/{token}`
    - View action: lookup by token, return 404 if not found, return branded expiry page if expired, return stored HTML (Content-Type: text/html) if valid
    - Add Cache-Control: no-store header
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 9.2 Write property tests for ProposalController
    - **Property 12: Valid non-expired token returns stored HTML**
    - **Property 13: Expired token returns expiry page**
    - **Property 14: Invalid token returns 404**
    - **Property 15: No internal IDs exposed in public HTML**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4**

  - [x] 9.3 Create LogoController (authenticated)
    - Create file `Portal.Web/Controllers/LogoController.cs`
    - [Authorize] + [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    - Actions: Index (logo library page), Upload (POST, IFormFile + displayName), Delete (POST, id)
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [x] 9.4 Extend QuotationController with Share, ShareDialog, CopyShareLink actions
    - Modify `Portal.Web/Controllers/QuotationController.cs`
    - Add [HttpGet] ShareDialog(int id) — returns share configuration partial (logo selection, expiration date picker)
    - Add [HttpPost] Share(int id, ShareProposalViewModel model) — calls IProposalService.ShareAsync, redirects back to Detail with success message
    - Add [HttpPost] CopyShareLink(int id) — returns JSON with active share URL
    - Inject IProposalService and ILogoService into constructor
    - _Requirements: 3.2, 3.3, 3.4, 6.1, 7.1, 7.3, 8.2, 8.3, 11.1, 11.2_

  - [ ]* 9.5 Write property tests for access control
    - **Property 18: Authorization enforcement (403 for unauthorized users)**
    - **Property 19: Tenant isolation on share**
    - **Validates: Requirements 7.1, 7.2, 7.3**

- [ ] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Views and UI integration
  - [x] 11.1 Create ShareDialog partial view
    - Create file `Portal.Web/Views/Quotation/_ShareDialog.cshtml`
    - Modal dialog with: expiration date picker (default 3 days), logo selection checkboxes (hero logos), logo selection radio (meta logo), share button
    - _Requirements: 3.3, 3.4, 11.1, 11.2_

  - [x] 11.2 Update Quotation Detail view with share button and status
    - Modify `Portal.Web/Views/Quotation/Detail.cshtml`
    - Add "Share Proposal" button that opens the share dialog
    - Display share status (Active/Expired) if previously shared
    - Add "Copy Link" button for active shares
    - _Requirements: 8.2, 8.3_

  - [x] 11.3 Update Quotation Edit view with ReferenceUrl field on line items
    - Modify `Portal.Web/Views/Quotation/Edit.cshtml`
    - Add optional ReferenceUrl input field to the line item form
    - _Requirements: 9.2_

  - [x] 11.4 Create Logo library management view
    - Create file `Portal.Web/Views/Logo/Index.cshtml`
    - Display uploaded logos with display name, preview, delete button
    - Upload form with file input and display name field
    - Show count (X/20) limit indicator
    - _Requirements: 10.1, 10.4_

  - [x] 11.5 Create proposal expiry page view
    - Create file `Portal.Web/Views/Proposal/Expired.cshtml`
    - Branded page with "This proposal link has expired" message
    - Display business contact information
    - _Requirements: 4.2_

- [ ] 12. Email template
  - [x] 12.1 Create proposal notification email template and sending logic
    - Add email template method/view for proposal sharing notification
    - Include: proposal URL, quotation reference, business name, expiration date
    - Use MyChair design system styling (inline CSS)
    - Send via existing IEmailSender using Sales department
    - _Requirements: 6.1, 6.2, 6.3, 6.5_

  - [ ]* 12.2 Write property test for email content
    - **Property 17: Email contains required fields (URL, reference, business name, expiration)**
    - **Validates: Requirements 6.2**

- [ ] 13. DI registration and static files configuration
  - [x] 13.1 Register all new services and repositories in Program.cs
    - Modify `Portal.Web/Program.cs`
    - Register: ProposalShareRepository, BusinessLogoRepository, ProposalSectionRepository
    - Register: ILogoService → LogoService, IViewRenderService → ViewRenderService, IProposalRenderer → ProposalRenderer, IProposalService → ProposalService
    - Ensure wwwroot/uploads/logos/ directory exists for static file serving
    - _Requirements: All_

- [ ] 14. ReferenceUrl validation
  - [x] 14.1 Add ReferenceUrl validation to QuotationService
    - Modify the existing QuotationService (or relevant service) to validate ReferenceUrl on line add/update
    - Validate: well-formed absolute URL, http/https scheme only, max 2048 characters
    - Reject invalid URLs with ArgumentException
    - _Requirements: 9.1, 9.3_

  - [ ]* 14.2 Write property test for ReferenceUrl validation
    - **Property 22: ReferenceUrl validation (accepts valid http/https, rejects invalid)**
    - **Validates: Requirements 9.1, 9.3**

- [ ] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The implementation language is C# as specified in the design
- Logo files are stored in wwwroot/uploads/logos/ and served as static files (no auth required)
- ViewRenderService uses IRazorViewEngine to render Razor views to string
- ProposalController is [AllowAnonymous] — the only unauthenticated endpoint
- Share token: RandomNumberGenerator.GetBytes(32) → Base64Url encoding
- The proposal Razor view should match the design in proposal_mock.html
