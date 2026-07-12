# Implementation Plan: Document Attachments

## Overview

This plan implements file attachment capabilities for 7 entity types (Invoice, CreditNote, Quotation, Payment, Purchase, Supplier, Customer). Files are stored on the local filesystem (via `IFileStorageService` abstraction for future Azure Blob) while metadata lives in a new `[document]` schema table. The feature is gated behind the `attachments` module key (Professional plan and above).

The implementation creates the `[document]` SQL schema with 1 table, a file storage abstraction layer, a three-layer validation system (extension + Content-Type + magic bytes), a reusable Razor partial panel with AJAX interactions, and a ViewComponent for list view count badges.

## Tasks

- [x] 1. Database schema and migration
  - [x] 1.1 Create `[document]` schema and `DocumentAttachment` table migration
    - Create migration file `Portal.Database/Migrations/114_CreateDocumentAttachmentTable.sql`
    - `USE [Portal]` header per SQL standards
    - `CREATE SCHEMA [document]` with `IF NOT EXISTS` idempotent guard
    - `CREATE TABLE [document].[DocumentAttachment]` with all columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → [portal].[Business]), EntityType (NVARCHAR(50) NOT NULL), EntityId (INT NOT NULL), FileName (NVARCHAR(255) NOT NULL), OriginalFileName (NVARCHAR(255) NOT NULL), ContentType (NVARCHAR(100) NOT NULL), StoragePath (NVARCHAR(500) NOT NULL), FileSizeBytes (BIGINT NOT NULL), UploadedByUserId (NVARCHAR(450) NOT NULL), IsDeleted (BIT NOT NULL DEFAULT 0), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Named constraints: `PK_DocumentAttachment`, `FK_DocumentAttachment_Business`, `DF_DocumentAttachment_IsDeleted`, `DF_DocumentAttachment_CreatedAtUtc`
    - Filtered nonclustered index: `IX_DocumentAttachment_BusinessId_EntityType_EntityId` on (BusinessId, EntityType, EntityId) WHERE IsDeleted = 0
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Entity model, DTOs, and DbContext registration
  - [x] 2.1 Create `DocumentAttachment` entity class
    - Create `Portal.Infrastructure/Entities/DocumentAttachment.cs`
    - Properties: Id, BusinessId, EntityType, EntityId, FileName, OriginalFileName, ContentType, StoragePath, FileSizeBytes, UploadedByUserId, IsDeleted, CreatedAtUtc
    - _Requirements: 1.1_

  - [x] 2.2 Create DTOs and request models
    - Create `Portal.Infrastructure/Models/AttachmentDto.cs` — Id, OriginalFileName, ContentType, FileSizeBytes, CreatedAtUtc, UploadedByDisplayName, IsOwnedByCurrentUser
    - Create `Portal.Infrastructure/Models/UploadAttachmentRequest.cs` — BusinessId, UserId, EntityType, EntityId, File (IFormFile)
    - Create `Portal.Web/ViewComponents/AttachmentPanelViewModel.cs` — EntityType, EntityId, Attachments (List<AttachmentDto>), MaxAttachments (default 5), IsReadOnly
    - _Requirements: 3.3, 8.2, 9.1_

  - [x] 2.3 Register `DocumentAttachment` entity in DbContext
    - Add `DbSet<DocumentAttachment>` to the portal DbContext
    - Configure entity: table name `DocumentAttachment`, schema `document`, FK to Business, column types, default values
    - Configure `CreatedAtUtc` with `HasDefaultValueSql("GETUTCDATE()")`
    - _Requirements: 1.1_

- [x] 3. File storage service (abstraction + local implementation)
  - [x] 3.1 Create `IFileStorageService` interface
    - Create `Portal.Infrastructure/Services/IFileStorageService.cs`
    - Methods: `UploadAsync(int businessId, string entityType, int entityId, string originalFileName, Stream fileStream)` → returns storage path, `DownloadAsync(string storagePath)` → returns Stream, `DeleteAsync(string storagePath)`, `ExistsAsync(string storagePath)` → returns bool
    - _Requirements: 2.1_

  - [x] 3.2 Create `LocalFileStorageService` implementation
    - Create `Portal.Infrastructure/Services/LocalFileStorageService.cs`
    - Implements `IFileStorageService`
    - Base path: `wwwroot/uploads/` resolved via `IWebHostEnvironment.WebRootPath`
    - Path structure: `{businessId}/{entityType}/{entityId}/{guid}_{originalFileName}`
    - GUID-prefixed filenames for collision prevention
    - Creates directories on-demand (`Directory.CreateDirectory`)
    - Throws descriptive exception on failure without exposing internal paths
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 4. File type validator helper
  - [x] 4.1 Create `FileTypeValidator` static helper
    - Create `Portal.Infrastructure/Helpers/FileTypeValidator.cs`
    - Three-layer validation: extension check, Content-Type check, magic-byte verification
    - Allowed extensions: .pdf, .png, .jpg, .jpeg, .webp
    - Allowed Content-Types: application/pdf, image/png, image/jpeg, image/webp
    - Magic bytes: PDF (%PDF-), PNG (89 50 4E 47), JPEG (FF D8 FF), WEBP (52 49 46 46 ... 57 45 42 50)
    - Returns a `ValidationResult` with IsValid, ErrorMessage
    - Validates extension↔Content-Type consistency
    - Stream position reset after reading magic bytes
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 12.5_

  - [ ]* 4.2 Write property test for file type validation
    - **Property 3: File type validation**
    - Verify that accept/reject decision is correct for all combinations of extension, Content-Type, and magic bytes
    - **Validates: Requirements 5.1, 5.2, 5.3, 12.5**

- [x] 5. Checkpoint — Verify storage layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Repository layer
  - [x] 6.1 Create `DocumentAttachmentRepository`
    - Create `Portal.Infrastructure/Repositories/DocumentAttachmentRepository.cs`
    - Extends `GenericStoredProcedureRepository<DocumentAttachment>`
    - Methods:
      - `InsertAsync(DocumentAttachment)` — INSERT INTO [document].[DocumentAttachment], returns new Id via SCOPE_IDENTITY()
      - `GetByIdAsync(int id, int businessId)` — SELECT with BusinessId filter and IsDeleted = 0
      - `GetByEntityAsync(int businessId, string entityType, int entityId)` — all non-deleted, ordered by CreatedAtUtc DESC
      - `GetCountAsync(int businessId, string entityType, int entityId)` — COUNT where IsDeleted = 0
      - `GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds)` — batch GROUP BY for list views
      - `SoftDeleteAsync(int id, int businessId)` — UPDATE SET IsDeleted = 1
    - Follow repository-standards: try/catch (Exception ex) { throw; }, full table names, SqlParameter, null-safe with DBNull.Value
    - _Requirements: 1.1, 1.3, 1.4, 8.1, 8.3, 10.1_

- [x] 7. Service layer
  - [x] 7.1 Create `IDocumentAttachmentService` interface
    - Create `Portal.Infrastructure/Services/IDocumentAttachmentService.cs`
    - Methods: `UploadAsync(UploadAttachmentRequest)` → ServiceResult<AttachmentDto>, `DownloadAsync(int attachmentId, int businessId)` → ServiceResult<Stream>, `DeleteAsync(int attachmentId, string userId, int businessId, bool isOwner)` → ServiceResult, `GetByEntityAsync(int businessId, string entityType, int entityId)` → List<AttachmentDto>, `GetCountAsync(int businessId, string entityType, int entityId)` → int, `GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds)` → Dictionary<int, int>
    - _Requirements: 3.1, 6.1, 7.1, 8.1, 10.1_

  - [x] 7.2 Create `DocumentAttachmentService` implementation
    - Create `Portal.Infrastructure/Services/DocumentAttachmentService.cs`
    - Implements `IDocumentAttachmentService`
    - Inject: `DocumentAttachmentRepository`, `IFileStorageService`, `ICurrentTenantService`
    - **UploadAsync**: validate file size (≤ 5 MB), validate file type (via FileTypeValidator), check count limit (≤ 5 per entity), call IFileStorageService.UploadAsync, insert metadata via repository, return AttachmentDto
    - **DownloadAsync**: get attachment by id + businessId, verify IsDeleted = false, check file exists via IFileStorageService.ExistsAsync, return stream
    - **DeleteAsync**: get attachment, verify ownership (uploader OR isOwner), soft-delete via repository
    - **GetByEntityAsync**: query repository, map to AttachmentDto list
    - **GetCountAsync** / **GetCountsForEntitiesAsync**: delegate to repository
    - Returns `ServiceResult` or `ServiceResult<T>` for all operations
    - _Requirements: 3.1, 3.2, 3.4, 3.5, 4.1, 4.2, 4.3, 5.1, 6.1, 6.2, 6.3, 6.4, 7.1, 7.2, 7.3, 7.4, 8.1, 8.3, 12.1, 12.3_

  - [ ]* 7.3 Write property tests for attachment service logic
    - **Property 1: Soft-delete listing filter** — listing returns only non-deleted attachments for correct business
    - **Property 2: Listing order invariant** — results ordered by CreatedAtUtc DESC
    - **Property 4: Attachment count limit per entity** — rejects upload when count >= 5, independent per entity
    - **Property 5: Download tenant isolation** — download succeeds iff BusinessId matches AND IsDeleted = false
    - **Property 6: Delete authorization** — succeeds iff user is uploader OR owner
    - **Validates: Requirements 1.4, 4.2, 4.3, 6.1, 6.2, 7.1, 7.2, 7.3, 8.1, 8.3, 12.1, 12.3, 15.4**

- [x] 8. Checkpoint — Verify service layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Controller and DI registration
  - [x] 9.1 Create `AttachmentController`
    - Create `Portal.Web/Controllers/AttachmentController.cs`
    - Attributes: `[Authorize]`, `[ModuleAccess(PortalModules.Attachments)]`
    - Inject: `IDocumentAttachmentService`, `ICurrentTenantService`
    - **AxPostUpload(IFormFile file, string entityType, int entityId)**: resolve BusinessId + UserId from claims, validate parent entity existence, call service.UploadAsync, return Json(new { success, data })
    - **AxGetList(string entityType, int entityId)**: call service.GetByEntityAsync, return Json(new { success, data })
    - **AxGetDownload(int id)**: call service.DownloadAsync, return FileStreamResult with correct Content-Type and OriginalFileName
    - **AxPostDelete(int id)**: resolve user context, call service.DeleteAsync, return Json(new { success, message })
    - **AxGetCounts(string entityType, [FromQuery] int[] entityIds)**: call service.GetCountsForEntitiesAsync, return Json(new { success, data })
    - All POST endpoints include `[ValidateAntiForgeryToken]`
    - Error responses follow existing pattern: Json(new { success = false, message = "..." })
    - _Requirements: 3.1, 3.3, 3.4, 3.5, 4.1, 5.1, 6.1, 6.2, 6.3, 6.4, 7.1, 7.5, 8.1, 8.2, 8.3, 10.1, 12.1, 14.1_

  - [x] 9.2 Register `attachments` module key in `PortalModules.cs`
    - Add `public const string Attachments = "attachments";` to `PortalModules` class
    - _Requirements: 11.1_

  - [x] 9.3 Register services and repository in DI container
    - In `Program.cs`, add `AddScoped` registrations:
      - `IFileStorageService` → `LocalFileStorageService`
      - `IDocumentAttachmentService` → `DocumentAttachmentService`
      - `DocumentAttachmentRepository`
    - _Requirements: 2.1_

- [x] 10. Razor partial views
  - [x] 10.1 Create `_AttachmentPanel.cshtml` partial view
    - Create `Portal.Web/Views/Shared/_AttachmentPanel.cshtml`
    - Model: `AttachmentPanelViewModel` (EntityType, EntityId, Attachments, MaxAttachments, IsReadOnly)
    - Panel with 3px left-border accent (#0D5EA6)
    - **Attachment list**: file name, human-readable file size, upload date, uploader name, download/delete buttons
    - **Thumbnails**: image preview for PNG/JPG/WEBP, PDF icon for PDF files
    - **Upload zone**: drag-and-drop area with `accept=".pdf,.png,.jpg,.jpeg,.webp"` and `capture` attribute for mobile camera
    - **Count badge**: "3 of 5 attachments used"
    - **Disabled state**: when max reached, disable upload control and show limit message
    - **Empty state**: upload CTA when no attachments
    - **AJAX interactions**:
      - Upload: BlockUI.show → fetch AxPostUpload → BlockUI.hide → Swal.fire(success/error) → refresh list
      - Delete: Swal.fire confirmation → BlockUI.show → fetch AxPostDelete → BlockUI.hide → Swal.fire result → refresh list
      - Download: direct link to AxGetDownload
    - Responsive: stack cards vertically at < 576px
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 13.1, 13.2, 14.1, 14.2, 14.3, 14.4, 14.5_

  - [x] 10.2 Create `_AttachmentPanelSoftGate.cshtml` partial view
    - Create `Portal.Web/Views/Shared/_AttachmentPanelSoftGate.cshtml`
    - Friendly teaser panel explaining attachments feature requires Professional plan
    - Matches existing soft-gate styling in the application
    - _Requirements: 11.2_

- [x] 11. Lightbox for image preview
  - [x] 11.1 Implement image lightbox overlay
    - Full-screen overlay triggered by tapping image thumbnail
    - Pinch-to-zoom support on touch devices
    - Close button and download button accessible on mobile
    - Can be inline JavaScript in _AttachmentPanel or a shared script
    - _Requirements: 13.3, 13.4_

- [x] 12. AttachmentCountViewComponent for list views
  - [x] 12.1 Create `AttachmentCountViewComponent`
    - Create `Portal.Web/ViewComponents/AttachmentCountViewComponent.cs`
    - Accepts `entityType` and `entityIds` (array) parameters
    - Calls `IDocumentAttachmentService.GetCountsForEntitiesAsync` for batch lookup
    - Renders paperclip icon + count badge for entities with count > 0
    - Returns empty content for entities with 0 attachments
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 13. Checkpoint — Verify UI layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Integration into existing detail pages
  - [x] 14.1 Embed `_AttachmentPanel` on Purchase detail page
    - Add partial view reference with EntityType = "Purchase", EntityId from model
    - Conditionally render full panel (Professional+) or soft-gate teaser (Starter)
    - _Requirements: 3.1, 9.1, 11.2, 11.3_

  - [x] 14.2 Embed `_AttachmentPanel` on Invoice detail page
    - Add partial view reference with EntityType = "Invoice", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 3.1, 9.1, 11.2, 11.3_

  - [x] 14.3 Embed `_AttachmentPanel` on CreditNote detail page
    - Add partial view reference with EntityType = "CreditNote", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 3.1, 9.1, 11.2, 11.3_

  - [x] 14.4 Embed `_AttachmentPanel` on Quotation detail page
    - Add partial view reference with EntityType = "Quotation", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 3.1, 9.1, 11.2, 11.3_

  - [x] 14.5 Embed `_AttachmentPanel` on Payment detail page
    - Add partial view reference with EntityType = "Payment", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 15.5_

  - [x] 14.6 Embed `_AttachmentPanel` on Supplier detail page
    - Add partial view reference with EntityType = "Supplier", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 15.1, 15.2_

  - [x] 14.7 Embed `_AttachmentPanel` on Customer detail page
    - Add partial view reference with EntityType = "Customer", EntityId from model
    - Same conditional rendering logic
    - _Requirements: 15.1, 15.3_

- [x] 15. Integration into existing list views
  - [x] 15.1 Add attachment count indicator to Purchase list view
    - Invoke `AttachmentCountViewComponent` for visible entity IDs
    - Show paperclip icon + count badge on rows with attachments
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.2 Add attachment count indicator to Invoice list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.3 Add attachment count indicator to CreditNote list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.4 Add attachment count indicator to Quotation list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.5 Add attachment count indicator to Payment list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.6 Add attachment count indicator to Supplier list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 15.7 Add attachment count indicator to Customer list view
    - Same pattern as Purchase list
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 16. Permission gating seed
  - [x] 16.1 Create seed script for `attachments` PlanFeature record
    - Add `attachments` module entry to the PlanFeature lookup table for Professional and Enterprise tiers
    - Follow existing seeding patterns in `Portal.Database/Seeds/`
    - _Requirements: 11.1, 11.2, 11.3_

- [x] 17. Checkpoint — Full integration build
  - Ensure all tests pass, ask the user if questions arise.

- [x] 19. Final checkpoint — Full build and validation
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional property-based tests and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation throughout implementation
- The `[document]` schema isolates attachment tables from core schemas (`[portal]`, `[purchase]`, etc.)
- `LocalFileStorageService` is the dev implementation; Azure Blob implementation is a future task behind the same interface
- GUID-prefixed filenames prevent collisions — no overwrites possible
- Soft-delete means physical files are never removed during normal operation (audit/recovery preservation)
- Three-layer file validation (extension + Content-Type + magic bytes) prevents content-type spoofing
- All AJAX endpoints follow `AxPost`/`AxGet` naming convention per coding golden rules
- BlockUI + SweetAlert2 pattern used for all meaningful AJAX operations per UI feedback standards
- Repository layer follows repository-standards: try/catch(Exception ex){throw;}, full table names, SqlParameter, null-safe with DBNull.Value
- The attachment count ViewComponent uses batch queries to avoid N+1 on list views
- Per-entity 5-attachment limit is independent — Supplier with 5 attachments does not affect linked Purchase

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "3.1"] },
    { "id": 3, "tasks": ["3.2", "4.1"] },
    { "id": 4, "tasks": ["4.2", "6.1"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["7.2"] },
    { "id": 7, "tasks": ["7.3", "9.1", "9.2"] },
    { "id": 8, "tasks": ["9.3", "10.1", "10.2"] },
    { "id": 9, "tasks": ["11.1", "12.1"] },
    { "id": 10, "tasks": ["14.1", "14.2", "14.3", "14.4", "14.5", "14.6", "14.7"] },
    { "id": 11, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5", "15.6", "15.7"] },
    { "id": 12, "tasks": ["16.1"] },
    { "id": 13, "tasks": ["18.1", "18.2", "18.3", "18.4", "18.5"] }
  ]
}
```
