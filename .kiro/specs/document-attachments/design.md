# Design Document: Document Attachments

## Overview

The Document Attachments feature adds file upload, listing, download, and soft-delete capabilities to the Portal. Attachments can be associated with any of the 7 supported entity types (Invoice, CreditNote, Quotation, Payment, Purchase, Supplier, Customer) and are scoped per-business via the existing multi-tenant architecture.

Files are stored on the local filesystem in development (with an `IFileStorageService` abstraction enabling Azure Blob in production). Metadata lives in a new `[document]` schema table. The feature is gated behind the `attachments` module key (Professional plan and above) using the existing `PlanPermissionFilter` infrastructure.

The UI component is a reusable Razor partial (`_AttachmentPanel.cshtml`) embedded on detail pages, with a drag-and-drop upload zone, AJAX-driven interactions (BlockUI + SweetAlert2), and a paperclip + count badge indicator on list views.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| New `[document]` schema | Groups attachment tables logically, follows existing schema-per-module pattern (`[audit]`, `[vat]`, `[credit]`) |
| Soft-delete (no physical file removal) | Preserves files for audit/recovery; metadata `IsDeleted` flag controls visibility |
| GUID-prefixed filenames | Prevents collisions when multiple files share the same original name |
| Magic-byte validation | Prevents content-type spoofing by verifying actual file signatures |
| Per-entity 5-attachment limit | Keeps storage predictable; independent per EntityType+EntityId combination |
| Single reusable partial | Consistent UX across all 7 entity types; reduces maintenance surface |
| Left-border accent on panel | Visual "belongs to" cue as shown in approved mockup |

### Mockup References

- **Attachment Panel (3 states):** `.kiro/docs/mockups/document-attachments-panel.html` — Populated, empty, and soft-gate teaser states
- **Association View:** `.kiro/docs/mockups/document-attachment-association.html` — Purchase detail, Invoice detail with signed copy, and list view with paperclip indicators

---

## Architecture

```mermaid
graph TD
    subgraph "Portal.Web"
        A[AttachmentController] --> B[DocumentAttachmentService]
        C[_AttachmentPanel.cshtml] -->|AJAX| A
        D[Detail Pages] -->|embed| C
        E[List Views] -->|count badge| F[AttachmentCountViewComponent]
    end

    subgraph "Portal.Infrastructure"
        B --> G[DocumentAttachmentRepository]
        B --> H[IFileStorageService]
        B --> I[AuditLogRepository]
        B --> J[ICurrentTenantService]
        G --> K[(SQL Server<br/>[document].DocumentAttachment)]
        H --> L[LocalFileStorageService]
    end

    subgraph "Filters"
        M[PlanPermissionFilter] -->|gates| A
    end
```

### Request Flow

1. User interacts with `_AttachmentPanel.cshtml` (upload/download/delete)
2. AJAX request hits `AttachmentController` (protected by `PlanPermissionFilter` via `attachments` module key)
3. Controller resolves `BusinessId` from `ICurrentTenantService` and validates the parent entity exists
4. `DocumentAttachmentService` orchestrates file storage + metadata persistence
5. `DocumentAttachmentRepository` handles SQL operations on `[document].DocumentAttachment`
6. `IFileStorageService` handles physical file I/O
7. JSON response returned; panel refreshes via JavaScript

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| `AttachmentController` | HTTP concerns, request validation, auth context resolution, JSON responses |
| `DocumentAttachmentService` | Business logic orchestration, limit enforcement, authorization checks, file type validation |
| `DocumentAttachmentRepository` | SQL CRUD against `[document].DocumentAttachment` table |
| `IFileStorageService` / `LocalFileStorageService` | Physical file persistence, path generation, stream retrieval |
| `_AttachmentPanel.cshtml` | UI rendering, AJAX calls, drag-and-drop, BlockUI/SweetAlert2 integration |

---

## Components and Interfaces

### 1. IFileStorageService

**Location:** `Portal.Infrastructure/Services/IFileStorageService.cs`

```csharp
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a file and returns the relative storage path.
    /// </summary>
    Task<string> UploadAsync(int businessId, string entityType, int entityId, string originalFileName, Stream fileStream);

    /// <summary>
    /// Downloads a file by its storage path, returning a readable stream.
    /// </summary>
    Task<Stream> DownloadAsync(string storagePath);

    /// <summary>
    /// Deletes a file from storage (used for cleanup scenarios, not soft-delete).
    /// </summary>
    Task DeleteAsync(string storagePath);

    /// <summary>
    /// Checks whether a file exists at the given storage path.
    /// </summary>
    Task<bool> ExistsAsync(string storagePath);
}
```

### 2. LocalFileStorageService

**Location:** `Portal.Infrastructure/Services/LocalFileStorageService.cs`

Implements `IFileStorageService` for development environments. Stores files under `wwwroot/uploads/{businessId}/{entityType}/{entityId}/{guid}_{originalFileName}`.

Key behaviors:
- Generates GUID-prefixed filenames for uniqueness
- Creates directories on-demand
- Throws `FileStorageException` (custom) on failure without exposing internal paths
- Base path injected via `IWebHostEnvironment.WebRootPath`

### 3. DocumentAttachmentService

**Location:** `Portal.Infrastructure/Services/DocumentAttachmentService.cs`

```csharp
public interface IDocumentAttachmentService
{
    Task<ServiceResult<AttachmentDto>> UploadAsync(UploadAttachmentRequest request);
    Task<ServiceResult<Stream>> DownloadAsync(int attachmentId, int businessId);
    Task<ServiceResult> DeleteAsync(int attachmentId, string userId, int businessId, bool isOwner);
    Task<List<AttachmentDto>> GetByEntityAsync(int businessId, string entityType, int entityId);
    Task<int> GetCountAsync(int businessId, string entityType, int entityId);
    Task<Dictionary<int, int>> GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds);
}
```

Orchestrates:
- File type/size/count validation
- Magic-byte content verification
- Delegation to `IFileStorageService` for physical storage
- Delegation to `DocumentAttachmentRepository` for metadata CRUD
- Authorization logic (uploader vs owner)
- Audit logging via `AuditLogRepository`

### 4. DocumentAttachmentRepository

**Location:** `Portal.Infrastructure/Repositories/DocumentAttachmentRepository.cs`

Extends `GenericStoredProcedureRepository<DocumentAttachment>`. Methods:

| Method | Description |
|--------|-------------|
| `InsertAsync(DocumentAttachment)` | Creates metadata record, returns new Id |
| `GetByIdAsync(int id, int businessId)` | Single attachment by Id + business scope |
| `GetByEntityAsync(int businessId, string entityType, int entityId)` | All non-deleted attachments for an entity, ordered by CreatedAtUtc DESC |
| `GetCountAsync(int businessId, string entityType, int entityId)` | Count of non-deleted attachments for an entity |
| `GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds)` | Batch count lookup for list views |
| `SoftDeleteAsync(int id, int businessId)` | Sets IsDeleted = 1 |

All queries use full table names (no aliases), `try/catch (Exception ex) { throw; }`, and `?? (object)DBNull.Value` for nullable parameters.

### 5. AttachmentController

**Location:** `Portal.Web/Controllers/AttachmentController.cs`

```csharp
[ModuleAccess(PortalModules.Attachments)]
public class AttachmentController : Controller
{
    // AJAX endpoints following AxPost/AxGet naming convention:
    // [HttpPost] AxPostUpload(IFormFile file, string entityType, int entityId)
    // [HttpGet]  AxGetList(string entityType, int entityId)
    // [HttpGet]  AxGetDownload(int id)
    // [HttpPost] AxPostDelete(int id)
    // [HttpGet]  AxGetCounts(string entityType, [FromQuery] int[] entityIds)
}
```

### 6. _AttachmentPanel.cshtml (Razor Partial)

**Location:** `Portal.Web/Views/Shared/_AttachmentPanel.cshtml`

Accepts a model:
```csharp
public class AttachmentPanelViewModel
{
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public List<AttachmentDto> Attachments { get; set; }
    public int MaxAttachments { get; set; } = 5;
    public bool IsReadOnly { get; set; }
}
```

Renders:
- 3px left-border accent panel (#0D5EA6)
- Attachment list with thumbnails/icons, file name, size, date, uploader, action buttons
- Drop zone for drag-and-drop (with `accept` and `capture` attributes)
- Count badge ("3 of 5")
- Disabled state when limit reached
- Empty state with upload CTA
- Soft-gate teaser (separate partial: `_AttachmentPanelSoftGate.cshtml`)

### 7. AttachmentCountViewComponent

**Location:** `Portal.Web/ViewComponents/AttachmentCountViewComponent.cs`

Invoked on list views to render the paperclip + count badge. Accepts `entityType` and `entityId`, queries `DocumentAttachmentService.GetCountAsync()`.

### 8. FileTypeValidator (Static Helper)

**Location:** `Portal.Infrastructure/Helpers/FileTypeValidator.cs`

```csharp
public static class FileTypeValidator
{
    // Allowed extensions + content types mapping
    // Magic byte signatures for PDF, PNG, JPEG, WEBP
    public static ValidationResult Validate(string fileName, string contentType, Stream fileStream);
}
```

Performs three-layer validation:
1. Extension check (`.pdf`, `.png`, `.jpg`, `.jpeg`, `.webp`)
2. Content-Type header check against allowed MIME types
3. Magic-byte verification (first N bytes of stream match expected file signature)

---

## Data Models

### DocumentAttachment Table

```sql
CREATE SCHEMA [document]
GO

CREATE TABLE [document].[DocumentAttachment]
(
    [Id]                INT             IDENTITY(1,1)   NOT NULL,
    [BusinessId]        INT                             NOT NULL,
    [EntityType]        NVARCHAR(50)                    NOT NULL,
    [EntityId]          INT                             NOT NULL,
    [FileName]          NVARCHAR(255)                   NOT NULL,
    [OriginalFileName]  NVARCHAR(255)                   NOT NULL,
    [ContentType]       NVARCHAR(100)                   NOT NULL,
    [StoragePath]       NVARCHAR(500)                   NOT NULL,
    [FileSizeBytes]     BIGINT                          NOT NULL,
    [UploadedByUserId]  NVARCHAR(450)                   NOT NULL,
    [IsDeleted]         BIT                             NOT NULL  CONSTRAINT [DF_DocumentAttachment_IsDeleted] DEFAULT (0),
    [CreatedAtUtc]      DATETIME                        NOT NULL  CONSTRAINT [DF_DocumentAttachment_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_DocumentAttachment] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_DocumentAttachment_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_DocumentAttachment_BusinessId_EntityType_EntityId]
    ON [document].[DocumentAttachment] ([BusinessId], [EntityType], [EntityId])
    WHERE [IsDeleted] = 0;
GO
```

### Column Descriptions

| Column | Purpose |
|--------|---------|
| `Id` | Primary key |
| `BusinessId` | Tenant isolation FK |
| `EntityType` | Discriminator: 'Invoice', 'CreditNote', 'Quotation', 'Payment', 'Purchase', 'Supplier', 'Customer' |
| `EntityId` | FK to the parent record in the respective entity table |
| `FileName` | GUID-prefixed stored filename (e.g., `a1b2c3d4_receipt.pdf`) |
| `OriginalFileName` | User's original filename for display and download |
| `ContentType` | MIME type (e.g., `application/pdf`) |
| `StoragePath` | Relative path: `{businessId}/{entityType}/{entityId}/{fileName}` |
| `FileSizeBytes` | File size for display and validation |
| `UploadedByUserId` | Identity user ID for ownership/authorization |
| `IsDeleted` | Soft-delete flag |
| `CreatedAtUtc` | Audit timestamp |

### Entity Model (C#)

```csharp
public class DocumentAttachment
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string FileName { get; set; }
    public string OriginalFileName { get; set; }
    public string ContentType { get; set; }
    public string StoragePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string UploadedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

### DTOs

```csharp
public class AttachmentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string UploadedByDisplayName { get; set; }
    public bool IsOwnedByCurrentUser { get; set; }
}

public class UploadAttachmentRequest
{
    public int BusinessId { get; set; }
    public string UserId { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public IFormFile File { get; set; }
}
```

### Storage Path Structure

```
wwwroot/uploads/
└── {businessId}/
    └── {entityType}/
        └── {entityId}/
            ├── a1b2c3d4_invoice-scan.pdf
            └── e5f6g7h8_receipt-photo.jpg
```

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Soft-delete listing filter

*For any* business, entity type, and entity ID, the attachment listing SHALL return only attachments where `IsDeleted = false` AND `BusinessId` matches the requesting user's business — regardless of how many deleted or cross-tenant records exist in the database.

**Validates: Requirements 1.4, 8.1, 12.1**

### Property 2: Listing order invariant

*For any* set of non-deleted attachments belonging to the same entity, the listing SHALL return them ordered by `CreatedAtUtc` descending — the most recently uploaded attachment always appears first.

**Validates: Requirements 8.3**

### Property 3: File type validation

*For any* file with a given extension, Content-Type header, and magic bytes, the validation function SHALL accept the file if and only if: (a) the extension is in the allowed set, (b) the Content-Type is in the allowed set, (c) the extension and Content-Type are consistent with each other, and (d) the magic bytes match the expected signature for the declared type.

**Validates: Requirements 3.1, 5.1, 5.2, 5.3, 12.5**

### Property 4: Attachment count limit per entity

*For any* entity (identified by EntityType + EntityId), if the entity already has N non-deleted attachments where N >= 5, the system SHALL reject any new upload attempt for that entity — and this limit is independent across entities (uploading to Entity A does not affect the count for Entity B).

**Validates: Requirements 4.2, 4.3, 15.4**

### Property 5: Download tenant isolation

*For any* attachment and any requesting user, the download SHALL succeed if and only if: the attachment's `BusinessId` matches the user's business AND the attachment's `IsDeleted` flag is false. All other combinations SHALL return HTTP 404.

**Validates: Requirements 6.1, 6.2, 12.3, 12.4**

### Property 6: Delete authorization

*For any* user and any attachment within the same business, the soft-delete operation SHALL succeed if and only if: the user is the attachment's uploader OR the user is the business Owner. A non-owner attempting to delete another user's attachment SHALL be rejected with an insufficient permissions error.

**Validates: Requirements 7.1, 7.2, 7.3**

### Property 7: Soft-delete preserves physical file

*For any* attachment that undergoes soft-delete, the physical file at the attachment's `StoragePath` SHALL remain present on disk — the soft-delete operation modifies only the metadata `IsDeleted` flag.

**Validates: Requirements 7.4**

### Property 8: Storage path business isolation

*For any* two uploads belonging to different businesses, their storage paths SHALL have non-overlapping directory prefixes — specifically, the first path segment SHALL be the `BusinessId`, ensuring files from Business A never share a directory with files from Business B. Additionally, for any single original filename uploaded multiple times, each stored filename SHALL be unique (via GUID prefix).

**Validates: Requirements 2.2, 2.3, 12.2**

### Property 9: Upload response completeness

*For any* successful upload, the JSON response SHALL contain all required fields (`Id`, `OriginalFileName`, `ContentType`, `FileSizeBytes`, `CreatedAtUtc`) with values matching the persisted metadata record.

**Validates: Requirements 3.3**

### Property 10: Attachment count indicator accuracy

*For any* entity, the attachment count indicator SHALL equal the number of non-deleted attachments — it SHALL be zero (and hidden) when no non-deleted attachments exist, and SHALL equal the precise count of non-deleted records otherwise.

**Validates: Requirements 10.1, 10.2, 10.3**

### Property 11: Entity existence validation on upload

*For any* upload request specifying an EntityType and EntityId, the system SHALL reject the upload if no matching parent entity record exists in the user's business — preventing orphaned attachments to non-existent records.

**Validates: Requirements 3.5**

---

## Error Handling

| Scenario | Response | HTTP Status |
|----------|----------|-------------|
| File exceeds 5MB | `{ success: false, message: "File size exceeds the maximum of 5 MB." }` | 400 |
| Disallowed extension/type | `{ success: false, message: "File type not allowed. Accepted: PDF, PNG, JPG, WEBP." }` | 400 |
| Extension/Content-Type mismatch | `{ success: false, message: "File extension does not match content type." }` | 400 |
| Magic-byte mismatch | `{ success: false, message: "File content does not match the declared file type." }` | 400 |
| Attachment count limit reached | `{ success: false, message: "Maximum of 5 attachments per record reached." }` | 400 |
| Parent entity not found | `{ success: false, message: "The parent record was not found." }` | 404 |
| Attachment not found / wrong business | `{ success: false, message: "Attachment not found." }` | 404 |
| Physical file missing (download) | `{ success: false, message: "The file is unavailable. Please contact support." }` | 404 |
| Insufficient delete permissions | `{ success: false, message: "You do not have permission to delete this attachment." }` | 403 |
| File storage I/O failure | `{ success: false, message: "Failed to process file. Please try again." }` | 500 |

All error responses follow the existing `Json(new { success, message })` pattern used by `AxPost`/`AxGet` endpoints. Storage exceptions never expose internal file paths.

---

## Testing Strategy

### Property-Based Tests (using FsCheck with xUnit)

Each correctness property above will be implemented as a property-based test with minimum 100 iterations. Tests will use the existing `Portal.Tests` project infrastructure.

**Library:** FsCheck.Xunit (already available in the project based on existing property tests)

**Test organization:**
- `Portal.Tests/PropertyBased/AttachmentListingPropertyTests.cs` — Properties 1, 2
- `Portal.Tests/PropertyBased/FileTypeValidationPropertyTests.cs` — Property 3
- `Portal.Tests/PropertyBased/AttachmentLimitPropertyTests.cs` — Property 4
- `Portal.Tests/PropertyBased/AttachmentTenantIsolationPropertyTests.cs` — Properties 5, 8
- `Portal.Tests/PropertyBased/AttachmentDeleteAuthPropertyTests.cs` — Properties 6, 7
- `Portal.Tests/PropertyBased/AttachmentUploadPropertyTests.cs` — Properties 9, 11
- `Portal.Tests/PropertyBased/AttachmentCountPropertyTests.cs` — Property 10

Each test will be tagged with:
```csharp
// Feature: document-attachments, Property {N}: {property_text}
```

### Unit Tests (xUnit)

- Specific edge cases: exactly 5MB file, exactly 5 attachments, empty filename, null stream
- Controller response shapes for each endpoint
- `FileTypeValidator` with known magic-byte samples
- `LocalFileStorageService` path generation and directory creation
- Soft-gate view rendering for Starter plan users

### Integration Tests

- End-to-end upload → list → download → delete cycle
- Multi-tenant isolation with two businesses
- Concurrent upload race condition (two users uploading the 5th file simultaneously)
- File cleanup and orphan detection

### Manual/Visual Tests

- Mobile upload with camera capture attribute
- Lightbox pinch-to-zoom on touch devices
- Responsive layout at < 576px breakpoint
- Drag-and-drop visual feedback
- BlockUI + SweetAlert2 sequence timing
