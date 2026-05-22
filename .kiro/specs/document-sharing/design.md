# Design Document: Document Sharing

## Overview

The Document Sharing feature extends the existing quotation sharing mechanism to invoices and unifies both under a consistent architecture. The system follows the established Controller → Service → Repository pattern with raw SQL data access.

The core flow for both document types is identical:
1. Manager triggers share from the document detail page
2. Service generates a cryptographically secure token (32 bytes, URL-safe Base64)
3. Service renders an HTML snapshot (immutable point-in-time capture)
4. Service persists the share record with token, snapshot, and expiration
5. Optionally sends a branded HTML email notification
6. Public controller serves the snapshot at a token-based URL

Key design decisions:
- **Separate services** for quotation and invoice sharing (not a unified generic service) — keeps each service focused and avoids complex generics, matching the existing `ProposalService` pattern
- **Shared "Unavailable" view** — both expired and cancelled links show the same generic page ("This link is no longer available") to avoid leaking state information
- **InvoiceShareRepository** mirrors `ProposalShareRepository` exactly in method signatures and SQL patterns
- **IInvoiceRenderer** uses Razor view rendering (same approach as `IProposalRenderer`) against the existing `Preview.cshtml` template

## Architecture

```mermaid
graph TD
    subgraph "Authenticated (Portal)"
        A[Invoice Detail Page] -->|Share Dialog| B[InvoiceController.Share]
        C[Quotation Detail Page] -->|Share Dialog| D[QuotationController.Share]
        E[SharedLinksController] -->|Management| F[Shared Links Page]
    end

    subgraph "Services"
        B --> G[InvoiceSharingService]
        D --> H[ProposalService - existing]
        E --> G
        E --> H
        G --> I[IInvoiceRenderer]
        G --> J[InvoiceShareRepository]
        G --> K[IEmailService]
        H --> L[IProposalRenderer]
        H --> M[ProposalShareRepository]
        H --> K
    end

    subgraph "Public (Unauthenticated)"
        N[/invoice-view/{token}/] --> O[InvoiceViewController]
        P[/proposal/{token}/] --> Q[ProposalController - existing]
        O --> J
        Q --> M
    end

    subgraph "Database"
        J --> R[(invoice.InvoiceShare)]
        M --> S[(quotation.ProposalShare)]
    end
```

## Components and Interfaces

### 1. InvoiceShare Entity

```csharp
namespace Portal.Infrastructure.Entities;

public class InvoiceShare
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int BusinessId { get; set; }
    public string ShareToken { get; set; } = null!;
    public string SnapshotHtml { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = null!;
    public bool IsActive { get; set; }

    // Navigation
    public Invoice Invoice { get; set; } = null!;
    public Business Business { get; set; } = null!;
}
```

### 2. InvoiceShareRepository

Mirrors `ProposalShareRepository` with methods:
- `InsertAsync(InvoiceShare entity)`
- `GetByTokenAsync(string token)`
- `GetActiveByInvoiceIdAsync(int invoiceId)`
- `GetByInvoiceIdAsync(int invoiceId)`
- `GetByBusinessIdAsync(int businessId)` — for Shared Links page
- `DeactivateByInvoiceIdAsync(int invoiceId)`
- `DeactivateByIdAsync(int id, int businessId)` — for cancel action

### 3. ProposalShareRepository (Extended)

New methods added to existing repository:
- `GetByBusinessIdAsync(int businessId)` — for Shared Links page
- `DeactivateByIdAsync(int id, int businessId)` — for cancel action with tenant check

### 4. IInvoiceSharingService

```csharp
namespace Portal.Infrastructure.Services;

public interface IInvoiceSharingService
{
    Task<InvoiceShare> ShareAsync(int invoiceId, DateTimeOffset expiresAtUtc, bool sendEmail, string userId);
    Task<InvoiceShare?> GetByTokenAsync(string token);
    Task<InvoiceShare?> GetActiveShareByInvoiceIdAsync(int invoiceId);
    Task<List<InvoiceShare>> GetSharesByBusinessIdAsync(int businessId);
    Task CancelShareAsync(int shareId);
}
```

### 5. IInvoiceRenderer

```csharp
namespace Portal.Infrastructure.Services;

public interface IInvoiceRenderer
{
    Task<string> RenderAsync(int invoiceId);
}
```

The implementation uses `IRazorViewEngine` and `ITempDataProvider` to render `Views/Invoice/Preview.cshtml` to a string, populating the same ViewBag data (Lines, Sections, CustomerName, LogoUrl, BusinessName, Profile, PaymentDetails) that the existing preview action uses. The `autoPrint` flag is set to `false` and the "Download PDF" button is removed from the snapshot output.

### 6. IEmailService (Extended)

```csharp
// Add to existing interface:
Task SendInvoiceEmailAsync(string toEmail, string shareToken, string invoiceNumber, 
    string businessName, decimal totalAmount, DateOnly dueDate, DateTimeOffset expiresAtUtc);
```

### 7. InvoiceViewController (Public)

```csharp
[AllowAnonymous]
public class InvoiceViewController : Controller
{
    // GET /invoice-view/{token}
    // Returns: SnapshotHtml (200), Unavailable view (expired/cancelled), NotFound (invalid)
}
```

### 8. ProposalController (Enhanced)

The existing controller is updated to:
- Check `IsActive` flag in addition to expiration
- Return a generic "Unavailable" view (shared with InvoiceViewController) instead of the current "Expired" view

### 9. SharedLinksController

```csharp
[Authorize]
public class SharedLinksController : Controller
{
    // GET /shared-links — renders management page
    // POST /shared-links/cancel-proposal/{id} — cancels a proposal share
    // POST /shared-links/cancel-invoice/{id} — cancels an invoice share
}
```

### 10. Email Templates

Two branded HTML email templates following the same structure:

| Element | Quotation Email | Invoice Email |
|---------|----------------|---------------|
| Accent colour | `#0D5EA6` (blue) | `#129867` (green/teal) |
| Header text | "New Proposal" | "New Invoice" |
| Reference field | Quotation reference | Invoice number |
| Amount field | Total amount | Total amount |
| Date field | Valid until | Due date |
| CTA button text | "View Proposal" | "View Invoice" |
| CTA link | `/proposal/{token}` | `/invoice-view/{token}` |

Both templates are self-contained HTML with inline styles (no external CSS), compatible with major email clients.

## Data Models

### InvoiceShare Table Schema

```sql
CREATE TABLE [invoice].[InvoiceShare]
(
    [Id]              INT                IDENTITY(1,1)  NOT NULL,
    [InvoiceId]       INT                               NOT NULL,
    [BusinessId]      INT                               NOT NULL,
    [ShareToken]      NVARCHAR(128)                     NOT NULL,
    [SnapshotHtml]    NVARCHAR(MAX)                     NOT NULL,
    [CustomerEmail]   NVARCHAR(200)                     NOT NULL,
    [ExpiresAtUtc]    DATETIMEOFFSET                    NOT NULL,
    [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL  DEFAULT (SYSDATETIMEOFFSET()),
    [CreatedByUserId] NVARCHAR(450)                     NOT NULL,
    [IsActive]        BIT                               NOT NULL  DEFAULT (1),

    CONSTRAINT [PK_InvoiceShare] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_InvoiceShare_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]),
    CONSTRAINT [FK_InvoiceShare_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
    CONSTRAINT [UX_InvoiceShare_ShareToken] UNIQUE NONCLUSTERED ([ShareToken])
);

CREATE NONCLUSTERED INDEX [IX_InvoiceShare_InvoiceId] ON [invoice].[InvoiceShare] ([InvoiceId]);
CREATE NONCLUSTERED INDEX [IX_InvoiceShare_BusinessId] ON [invoice].[InvoiceShare] ([BusinessId]);
```

### Shared Links View Model

```csharp
public class SharedLinkViewModel
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = null!;  // "Quotation" or "Invoice"
    public string DocumentReference { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string Status { get; set; } = null!;  // "Active", "Expired", "Cancelled"
    public bool IsActive { get; set; }
}
```

Status derivation logic:
- `IsActive == false` → "Cancelled"
- `IsActive == true && ExpiresAtUtc <= now` → "Expired"
- `IsActive == true && ExpiresAtUtc > now` → "Active"


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Token generation produces URL-safe tokens of sufficient length

*For any* share operation (quotation or invoice), the generated ShareToken SHALL decode from URL-safe Base64 to at least 32 bytes, and SHALL contain only characters from the set `[A-Za-z0-9_-]` (no `+`, `/`, or `=`).

**Validates: Requirements 1.2, 3.2**

### Property 2: Custom expiration date is persisted exactly

*For any* valid custom expiration date (at least 1 day in the future) provided to the sharing service, the persisted share record's ExpiresAtUtc SHALL equal the provided date exactly.

**Validates: Requirements 1.4, 3.4**

### Property 3: New quotation share deactivates previous active share

*For any* quotation that has an existing active ProposalShare, creating a new share SHALL result in the previous share's IsActive being set to false, and exactly one active share existing for that quotation.

**Validates: Requirements 1.5**

### Property 4: New invoice share deactivates previous active share

*For any* invoice that has an existing active InvoiceShare, creating a new share SHALL result in the previous share's IsActive being set to false, and exactly one active share existing for that invoice.

**Validates: Requirements 3.5**

### Property 5: Quotation email contains required elements with blue accent

*For any* quotation email rendered by the EmailService, the HTML output SHALL contain the colour code `#0D5EA6`, the quotation reference number, the total amount, the valid-until date, and a link containing `/proposal/{token}`.

**Validates: Requirements 2.2, 2.3**

### Property 6: Invoice email contains required elements with green accent

*For any* invoice email rendered by the EmailService, the HTML output SHALL contain the colour code `#129867`, the invoice number, the total amount, the due date, and a link containing `/invoice-view/{token}`.

**Validates: Requirements 4.2, 4.3**

### Property 7: Email failure does not roll back share record

*For any* share operation where the email service throws an exception, the share record (ProposalShare or InvoiceShare) SHALL still be persisted in the database with IsActive = true.

**Validates: Requirements 2.4, 4.4**

### Property 8: Valid active non-expired token returns snapshot HTML

*For any* share record (ProposalShare or InvoiceShare) where IsActive is true and ExpiresAtUtc is in the future, requesting the public endpoint with that token SHALL return the stored SnapshotHtml with content type `text/html` and `Cache-Control: no-store` header.

**Validates: Requirements 5.2, 5.6**

### Property 9: Expired or cancelled token returns unavailable page; invalid token returns 404

*For any* share record where IsActive is false OR ExpiresAtUtc is in the past, requesting the public endpoint SHALL return the "unavailable" page. *For any* token string that does not match any share record, the endpoint SHALL return HTTP 404.

**Validates: Requirements 5.3, 5.4, 5.5, 6.1, 6.2, 7.4, 7.5**

### Property 10: Expiration validation rejects dates less than 1 day in the future

*For any* expiration date that is less than 1 day from the current UTC time, the sharing service SHALL reject the operation with an error. *For any* date at least 1 day in the future, the operation SHALL be accepted.

**Validates: Requirements 7.2**

### Property 11: Cancel sets IsActive to false

*For any* active share record, invoking the cancel operation SHALL set IsActive to false on that record, and the change SHALL be immediately reflected in subsequent queries.

**Validates: Requirements 7.3**

### Property 12: Status derivation function

*For any* share record: if `IsActive == false` then status is "Cancelled"; else if `ExpiresAtUtc <= now` then status is "Expired"; else status is "Active". No other status values are possible.

**Validates: Requirements 8.3, 8.4, 8.5, 8.6**

### Property 13: Invoice renderer produces self-contained HTML with all required fields

*For any* invoice with populated data, the rendered HTML SHALL be a complete HTML document with all styles inline (no external stylesheet links), and SHALL contain the business name, invoice number, invoice date, due date, customer name, line item descriptions, subtotal, tax amount, and total amount.

**Validates: Requirements 10.1, 10.2**

### Property 14: Snapshot immutability

*For any* invoice that is shared and subsequently modified, retrieving the share record's SnapshotHtml SHALL return the HTML as it was at the moment of sharing, not reflecting the subsequent modifications.

**Validates: Requirements 10.4**

### Property 15: Tenant isolation for share operations

*For any* share query or cancel operation, the service SHALL only return or modify records where BusinessId matches the current tenant's BusinessId. Attempting to cancel a share belonging to a different business SHALL be rejected.

**Validates: Requirements 11.1, 11.2, 11.3**

### Property 16: Unique token constraint prevents duplicates

*For any* two share records (within the same or different tables), no two records SHALL have the same ShareToken value. Attempting to insert a duplicate token SHALL fail.

**Validates: Requirements 9.2**

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Email dispatch fails | Share record is persisted; failure is logged via `ILogger`; no exception propagates to caller |
| Invoice not found for sharing | `InvalidOperationException` thrown; controller returns 404 |
| Customer has no email | `ArgumentException` thrown; controller returns validation error to UI |
| Token not found on public endpoint | Return HTTP 404 |
| Token expired or cancelled | Return "Unavailable" view (HTTP 200 with informational page) |
| Custom expiration < 1 day in future | `ArgumentException` thrown; controller returns validation error |
| Cancel attempted on other business's share | `InvalidOperationException` thrown; controller returns 403 |
| Duplicate token (extremely unlikely) | SQL unique constraint violation; retry with new token |
| Razor rendering fails | Exception propagates; share is not created; controller returns 500 |

Error handling follows the existing pattern: repositories rethrow, services handle business logic errors, controllers catch and return appropriate HTTP responses.

## Testing Strategy

### Property-Based Testing

Library: **FsCheck.Xunit** (integrates with xUnit, the project's test framework)

Each correctness property above maps to a single property-based test with minimum 100 iterations. Tests are tagged with:
```
// Feature: document-sharing, Property {N}: {title}
```

Key generators needed:
- `ShareToken` generator: random URL-safe Base64 strings
- `DateTimeOffset` generator: dates in past, future, and boundary (exactly 1 day from now)
- `InvoiceShare` / `ProposalShare` generators: random valid entities with varying IsActive and ExpiresAtUtc combinations
- `Invoice` generator: random invoices with lines, sections, and customer data

### Unit Tests

Unit tests complement property tests for specific examples and edge cases:
- Default 7-day expiration when no custom date provided (Requirements 1.3, 3.3, 7.1)
- Shared Links page accessible with Quotation module access (Requirement 8.8)
- Shared Links page accessible with Invoice module access (Requirement 8.8)
- Cancel button only shown for active links (Requirement 8.7)
- `/invoice-view/{token}` route resolves correctly (Requirement 5.1)
- Email not sent when `sendEmail = false` (Requirements 1.1, 3.1)

### Integration Tests

- Full share flow: create invoice → share → retrieve via public URL → verify HTML content
- Cancel flow: share → cancel → verify public URL returns unavailable
- Tenant isolation: create share for Business A → attempt access from Business B → verify rejection

### Test Configuration

```csharp
[Property(MaxTest = 100)]  // Minimum 100 iterations per property test
```

Each property test references its design document property number in the test method name and XML doc comment.
