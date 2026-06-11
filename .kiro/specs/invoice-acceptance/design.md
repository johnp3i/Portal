# Design Document: Invoice Acceptance

## Overview

This feature extends the shared invoice viewing system with a formal acceptance workflow. When a customer opens a shared invoice link (`/invoice-view/{token}`), they can acknowledge the invoice by ticking a checkbox and clicking "Accept Invoice". The system captures a tamper-resistant audit record (IP, user-agent, terms text, UTC timestamp) and enforces a one-acceptance-per-share invariant. The business owner sees acceptance status on the invoice detail page and in the invoice list table.

The design leverages the existing `InvoiceShare` infrastructure — the new `InvoiceAcceptance` table has a 1:0..1 relationship with `InvoiceShare` (at most one acceptance per share). The acceptance endpoint is unauthenticated (public), mirroring the existing `InvoiceViewController` pattern, but includes server-side validation that the share is active and non-expired before persisting.

## Architecture

```mermaid
flowchart TD
    subgraph Public ["Public (Anonymous)"]
        A[Customer Browser] -->|GET /invoice-view/token| B[InvoiceViewController]
        A -->|POST /invoice-view/token/accept| B
    end

    subgraph Authenticated ["Authenticated (Business Owner)"]
        C[Business Owner] -->|GET /Invoice/Detail/id| D[InvoiceController]
        C -->|GET /Invoice (list)| D
    end

    B --> E[IInvoiceAcceptanceService]
    D --> E
    E --> F[InvoiceAcceptanceRepository]
    F --> G[(SQL Server - invoice.InvoiceAcceptance)]
    E --> H[InvoiceShareRepository]
```

**Key architectural decisions:**

1. **Separate table, not a column on InvoiceShare** — Acceptance is an immutable audit event with its own fields (IP, user-agent, terms). A separate table keeps the InvoiceShare entity focused on sharing concerns and enforces immutability at the schema level.

2. **UNIQUE constraint on InvoiceShareId** — Enforces the one-acceptance-per-share invariant at the database level, preventing race conditions from concurrent submissions.

3. **No UPDATE/DELETE operations** — The repository exposes only INSERT and SELECT methods. The SQL table has no UPDATE triggers or procedures. Immutability is enforced by code design.

4. **AllowAnonymous endpoint** — The acceptance POST lives on `InvoiceViewController` alongside the existing GET, matching the public access pattern. Validation occurs server-side (active + non-expired check).

## Components and Interfaces

### IInvoiceAcceptanceService

```csharp
public interface IInvoiceAcceptanceService
{
    /// <summary>
    /// Records an acceptance for the given share token.
    /// Validates share is active and non-expired.
    /// Returns the acceptance record or an error result.
    /// </summary>
    Task<InvoiceAcceptanceResult> AcceptAsync(string shareToken, string ipAddress, string userAgent);

    /// <summary>
    /// Gets the acceptance record for a given InvoiceShare ID, or null if not yet accepted.
    /// </summary>
    Task<InvoiceAcceptance?> GetByInvoiceShareIdAsync(int invoiceShareId);

    /// <summary>
    /// Returns the set of InvoiceShareIds (from the provided list) that have been accepted.
    /// Used for batch-loading acceptance status on list pages.
    /// </summary>
    Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds);
}
```

### InvoiceAcceptanceResult

```csharp
public class InvoiceAcceptanceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public bool AlreadyAccepted { get; set; }
}
```

### InvoiceAcceptanceRepository

```csharp
public class InvoiceAcceptanceRepository : GenericStoredProcedureRepository<InvoiceAcceptance>
{
    public InvoiceAcceptanceRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(InvoiceAcceptance entity) { /* INSERT only */ }
    public async Task<InvoiceAcceptance?> GetByInvoiceShareIdAsync(int invoiceShareId) { /* SELECT */ }
    public async Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds) { /* Batch SELECT */ }
}
```

**No update or delete methods are exposed** — this enforces Requirement 6.5 (immutability).

The `GetAcceptedShareIdsAsync` method accepts a list of InvoiceShareIds and returns those that have acceptance records. It uses a parameterised `IN` clause for efficient batch lookup on the invoice list page.

### Controller Endpoints

| Controller | Method | Route | Auth | Purpose |
|------------|--------|-------|------|---------|
| InvoiceViewController | GET | `/invoice-view/{token}` | Anonymous | Existing — extended to inject acceptance UI HTML |
| InvoiceViewController | POST | `/invoice-view/{token}/accept` | Anonymous | New — records acceptance, returns JSON |
| InvoiceController | GET | `/Invoice` | Authenticated | Existing — extended to show acceptance note in list |
| InvoiceController | GET | `/Invoice/Detail/{id}` | Authenticated | Existing — extended to load acceptance status |

### InvoiceViewController Changes

The existing `ViewInvoice` GET action will be extended to:
1. Check if an acceptance already exists for this share (via `IInvoiceAcceptanceService.GetByInvoiceShareIdAsync`)
2. Inject either the acceptance form HTML (checkbox + button) or the read-only "Accepted on {date}" message into the snapshot HTML, placed after the download button.

New POST action:
```csharp
[HttpPost("/invoice-view/{token}/accept")]
public async Task<IActionResult> AcceptInvoice(string token)
{
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = Request.Headers["User-Agent"].ToString();

    var result = await _acceptanceService.AcceptAsync(token, ipAddress, userAgent);
    return Json(new { success = result.Success, message = result.Message, 
                      acceptedAt = result.AcceptedAtUtc, alreadyAccepted = result.AlreadyAccepted });
}
```

### InvoiceController.Detail Changes

The existing `Detail` action will be extended to:
1. Load the active share for the invoice (already available via `_sharingService.GetActiveShareByInvoiceIdAsync`)
2. If an active share exists, check for acceptance via `_acceptanceService.GetByInvoiceShareIdAsync`
3. Set `ViewBag.AcceptanceStatus` to one of: `"accepted"`, `"awaiting"`, or `null` (no share)
4. Set `ViewBag.AcceptedAtUtc` when status is `"accepted"`

## Data Models

### Database Schema: `[invoice].[InvoiceAcceptance]`

```sql
CREATE TABLE [invoice].[InvoiceAcceptance]
(
    [Id]              INT                IDENTITY(1,1)  NOT NULL,
    [InvoiceShareId]  INT                               NOT NULL,
    [AcceptedTerms]   NVARCHAR(500)                     NOT NULL,
    [AcceptedAtUtc]   DATETIMEOFFSET                    NOT NULL,
    [IpAddress]       NVARCHAR(45)                      NOT NULL,
    [UserAgent]       NVARCHAR(500)                     NOT NULL,
    [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL  
        CONSTRAINT [DF_InvoiceAcceptance_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),

    CONSTRAINT [PK_InvoiceAcceptance] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_InvoiceAcceptance_InvoiceShare] 
        FOREIGN KEY ([InvoiceShareId]) REFERENCES [invoice].[InvoiceShare] ([Id]),
    CONSTRAINT [UX_InvoiceAcceptance_InvoiceShareId] 
        UNIQUE NONCLUSTERED ([InvoiceShareId])
);
```

**Column rationale:**
- `InvoiceShareId` (FK, UNIQUE) — Links to the share and enforces one-per-share at DB level.
- `AcceptedTerms` (NVARCHAR(500)) — Stores the exact text shown to the customer. Max 500 chars is generous for the fixed terms string.
- `AcceptedAtUtc` (DATETIMEOFFSET) — Server-clock timestamp of acceptance. Separate from `CreatedAtUtc` for semantic clarity (acceptance time vs. row insertion time — in practice they're nearly identical).
- `IpAddress` (NVARCHAR(45)) — Accommodates IPv6 (max 45 chars including zone ID).
- `UserAgent` (NVARCHAR(500)) — Modern user-agents are typically 100-300 chars; 500 provides headroom.
- `CreatedAtUtc` — Mandatory per project convention.

### Entity Model

```csharp
namespace Portal.Infrastructure.Entities;

/// <summary>
/// An immutable audit record capturing a customer's formal acceptance of a shared invoice.
/// Schema: [invoice].[InvoiceAcceptance]
/// </summary>
public class InvoiceAcceptance
{
    public int Id { get; set; }
    public int InvoiceShareId { get; set; }
    public string AcceptedTerms { get; set; } = null!;
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public string IpAddress { get; set; } = null!;
    public string UserAgent { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }

    // Navigation property
    public InvoiceShare InvoiceShare { get; set; } = null!;
}
```

### EF Core Configuration

```csharp
private static void ConfigureInvoiceAcceptance(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<InvoiceAcceptance>(entity =>
    {
        entity.ToTable("InvoiceAcceptance", "invoice");

        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.InvoiceShare)
            .WithOne()
            .HasForeignKey<InvoiceAcceptance>(e => e.InvoiceShareId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        entity.HasIndex(e => e.InvoiceShareId)
            .IsUnique()
            .HasDatabaseName("UX_InvoiceAcceptance_InvoiceShareId");

        entity.Property(e => e.AcceptedTerms)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        entity.Property(e => e.UserAgent)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");
    });
}
```

### Constants

```csharp
public static class InvoiceAcceptanceConstants
{
    public const string AcceptanceTermsText = 
        "I accept this invoice as correct and agree to pay by the due date.";
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Acceptance persistence round-trip

*For any* valid InvoiceShare (active, non-expired) and any HTTP context (with an IP address and user-agent string), submitting an acceptance SHALL persist a record where the stored `InvoiceShareId` matches the share, the stored `AcceptedTerms` equals the constant acceptance text, the stored `IpAddress` matches the request IP, and the stored `UserAgent` matches the request user-agent header.

**Validates: Requirements 2.1, 6.1, 6.2, 6.3, 6.4**

### Property 2: Uniqueness — at most one acceptance per InvoiceShare

*For any* InvoiceShare that already has an Acceptance_Record, attempting to accept again SHALL be rejected (not persisted), and the total number of acceptance records for that share SHALL remain exactly one.

**Validates: Requirements 3.1**

### Property 3: Rejection on inactive or expired shares

*For any* InvoiceShare where `IsActive = false` OR `ExpiresAtUtc <= DateTimeOffset.UtcNow`, submitting an acceptance request SHALL be rejected with an error, and no Acceptance_Record SHALL be persisted.

**Validates: Requirements 5.2**

## Error Handling

| Scenario | Handling | User Feedback |
|----------|----------|---------------|
| Share token not found (GET) | Return 404 | Browser shows not-found page |
| Share inactive/expired (GET) | Render Unavailable view | Existing behavior — no acceptance UI shown |
| Share inactive/expired (POST) | Return JSON `{ success: false, message }` | SweetAlert2 error: "This share link is no longer valid." |
| Duplicate acceptance (POST) | Return JSON `{ success: false, alreadyAccepted: true, acceptedAt }` | SweetAlert2 info: "Already accepted on {date}" |
| Database error on INSERT (POST) | Log error, return JSON `{ success: false, message }` | SweetAlert2 error: "An unexpected error occurred. Please try again." |
| UNIQUE constraint violation (race condition) | Catch `DbUpdateException`, treat as duplicate | SweetAlert2 info: "Already accepted on {date}" |

**Race condition handling:** If two browser tabs submit simultaneously, the UNIQUE constraint on `InvoiceShareId` ensures only one INSERT succeeds. The second attempt catches the constraint violation and returns the "already accepted" response.

## Testing Strategy

### Property-Based Tests (xUnit + FsCheck or similar)

Each correctness property is implemented as a property-based test with minimum 100 iterations:

- **Property 1**: Generate random IP addresses (IPv4/IPv6), random user-agent strings, and valid share states. Call `AcceptAsync`, then `GetByInvoiceShareIdAsync`, verify all fields match.
- **Property 2**: Generate a share with an existing acceptance. Call `AcceptAsync` again. Assert rejection and count remains 1.
- **Property 3**: Generate shares with `IsActive=false` or `ExpiresAtUtc` in the past. Call `AcceptAsync`. Assert rejection and no record persisted.

**PBT Library**: FsCheck (C#/.NET property-based testing library)
**Minimum iterations**: 100 per property
**Tag format**: `// Feature: invoice-acceptance, Property {N}: {title}`

### Unit Tests (Example-Based)

- Service returns success with correct fields for a fresh active share
- Service returns `AlreadyAccepted` with date for duplicate attempt
- Service returns error for inactive share
- Service returns error for expired share
- Repository INSERT persists all columns correctly
- Repository SELECT by InvoiceShareId returns correct record
- Controller returns proper JSON structure for success/error/duplicate cases

### Integration Tests

- Full POST → DB → GET round-trip with real SQL Server
- UNIQUE constraint violation produces graceful handling
- Concurrent acceptance attempts — only one succeeds
- Detail page renders acceptance status correctly for all three states (accepted, awaiting, no share)
