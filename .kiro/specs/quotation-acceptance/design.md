# Design Document: Quotation Acceptance (Proposal Acceptance)

## Overview

This feature extends the shared proposal viewing system with a formal acceptance workflow. When a customer opens a shared proposal link (`/proposal/{token}`), they can acknowledge the proposal by ticking a checkbox and clicking "Accept Proposal". The system captures a tamper-resistant audit record (IP, user-agent, terms text, UTC timestamp) and enforces a one-acceptance-per-share invariant. The business owner sees acceptance status on the quotation detail page and in the quotation list table.

The design leverages the existing `ProposalShare` infrastructure — the new `ProposalAcceptance` table has a 1:0..1 relationship with `ProposalShare` (at most one acceptance per share). The acceptance endpoint is unauthenticated (public), mirroring the existing `ProposalController` pattern, but includes server-side validation that the share is active and non-expired before persisting.

## Architecture

```mermaid
flowchart TD
    subgraph Public ["Public (Anonymous)"]
        A[Customer Browser] -->|GET /proposal/token| B[ProposalController]
        A -->|POST /proposal/token/accept| B
    end

    subgraph Authenticated ["Authenticated (Business Owner)"]
        C[Business Owner] -->|GET /Quotation/Detail/id| D[QuotationController]
        C -->|GET /Quotation (list)| D
    end

    B --> E[IProposalAcceptanceService]
    D --> E
    E --> F[ProposalAcceptanceRepository]
    F --> G[(SQL Server - quotation.ProposalAcceptance)]
    E --> H[ProposalShareRepository]
```

**Key architectural decisions:**

1. **Separate table, not a column on ProposalShare** — Acceptance is an immutable audit event with its own fields (IP, user-agent, terms). A separate table keeps the ProposalShare entity focused on sharing concerns and enforces immutability at the schema level.

2. **UNIQUE constraint on ProposalShareId** — Enforces the one-acceptance-per-share invariant at the database level, preventing race conditions from concurrent submissions.

3. **No UPDATE/DELETE operations** — The repository exposes only INSERT and SELECT methods. The SQL table has no UPDATE triggers or procedures. Immutability is enforced by code design.

4. **AllowAnonymous endpoint** — The acceptance POST lives on `ProposalController` alongside the existing GET, matching the public access pattern. Validation occurs server-side (active + non-expired check).

## Components and Interfaces

### IProposalAcceptanceService

```csharp
public interface IProposalAcceptanceService
{
    /// <summary>
    /// Records an acceptance for the given share token.
    /// Validates share is active and non-expired.
    /// Returns the acceptance record or an error result.
    /// </summary>
    Task<ProposalAcceptanceResult> AcceptAsync(string shareToken, string ipAddress, string userAgent);

    /// <summary>
    /// Gets the acceptance record for a given ProposalShare ID, or null if not yet accepted.
    /// </summary>
    Task<ProposalAcceptance?> GetByProposalShareIdAsync(int proposalShareId);

    /// <summary>
    /// Returns the set of ProposalShareIds (from the provided list) that have been accepted.
    /// Used for batch-loading acceptance status on list pages.
    /// </summary>
    Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds);
}
```

### ProposalAcceptanceResult

```csharp
public class ProposalAcceptanceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public bool AlreadyAccepted { get; set; }
}
```

### ProposalAcceptanceRepository

```csharp
public class ProposalAcceptanceRepository : GenericStoredProcedureRepository<ProposalAcceptance>
{
    public ProposalAcceptanceRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(ProposalAcceptance entity) { /* INSERT only */ }
    public async Task<ProposalAcceptance?> GetByProposalShareIdAsync(int proposalShareId) { /* SELECT */ }
    public async Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds) { /* Batch SELECT */ }
}
```

**No update or delete methods are exposed** — this enforces Requirement 7.5 (immutability).

The `GetAcceptedShareIdsAsync` method accepts a list of ProposalShareIds and returns those that have acceptance records. It uses a parameterised `IN` clause for efficient batch lookup on the quotation list page.

### Controller Endpoints

| Controller | Method | Route | Auth | Purpose |
|------------|--------|-------|------|---------|
| ProposalController | GET | `/proposal/{token}` | Anonymous | Existing — extended to inject acceptance UI HTML |
| ProposalController | POST | `/proposal/{token}/accept` | Anonymous | New — records acceptance, returns JSON |
| QuotationController | GET | `/Quotation` | Authenticated | Existing — extended to show acceptance note in list |
| QuotationController | GET | `/Quotation/Detail/{id}` | Authenticated | Existing — extended to load acceptance status |

### ProposalController Changes

The existing `ViewProposal` GET action will be extended to:
1. Check if an acceptance already exists for this share (via `IProposalAcceptanceService.GetByProposalShareIdAsync`)
2. Inject either the acceptance form HTML (checkbox + button) or the read-only "Accepted on {date}" message into the snapshot HTML, placed after the proposal content.

New POST action:
```csharp
[HttpPost("/proposal/{token}/accept")]
public async Task<IActionResult> AcceptProposal(string token)
{
    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var userAgent = Request.Headers["User-Agent"].ToString();

    var result = await _acceptanceService.AcceptAsync(token, ipAddress, userAgent);
    return Json(new { success = result.Success, message = result.Message, 
                      acceptedAt = result.AcceptedAtUtc, alreadyAccepted = result.AlreadyAccepted });
}
```

### QuotationController.Detail Changes

The existing `Detail` action will be extended to:
1. Load the active share for the quotation via `_proposalService.GetActiveShareByQuotationIdAsync`
2. If an active share exists, check for acceptance via `_acceptanceService.GetByProposalShareIdAsync`
3. Set `ViewBag.AcceptanceStatus` to one of: `"accepted"`, `"awaiting"`, or `null` (no share)
4. Set `ViewBag.AcceptedAtUtc` when status is `"accepted"`

### QuotationController.Index Changes (Quotation List)

The existing `Index` action is extended to populate `AcceptanceStatus` on each `QuotationListDto`:
1. After loading the paged quotation results, collect all quotation IDs from the page
2. For each quotation, load the active share via batch or iteration using `_proposalService.GetActiveShareByQuotationIdAsync`
3. Collect all active share IDs into a dictionary (quotationId → shareId)
4. Batch-load accepted share IDs via `_acceptanceService.GetAcceptedShareIdsAsync(shareIds)`
5. Set `item.AcceptanceStatus` to `"accepted"` or `"awaiting"` for each quotation that has an active share

The `QuotationListDto` has an `AcceptanceStatus` property (nullable string) that is `null` for quotations with no active share, `"awaiting"` for shared-but-not-accepted, and `"accepted"` for shared-and-accepted.

**Quotation List View (Index.cshtml):**
Below the quotation reference in the table's first column, a small note is rendered:
- `"✓ Accepted"` — green text (#129867), font-size 11px, font-weight 600
- `"⏳ Awaiting acceptance"` — amber text (#C8912E), font-size 11px, font-weight 600
- No note shown when `AcceptanceStatus` is null (no active share)

## Data Models

### Database Schema: `[quotation].[ProposalAcceptance]`

```sql
CREATE TABLE [quotation].[ProposalAcceptance]
(
    [Id]               INT                IDENTITY(1,1)  NOT NULL,
    [ProposalShareId]  INT                               NOT NULL,
    [AcceptedTerms]    NVARCHAR(500)                     NOT NULL,
    [AcceptedAtUtc]    DATETIMEOFFSET                    NOT NULL,
    [IpAddress]        NVARCHAR(45)                      NOT NULL,
    [UserAgent]        NVARCHAR(500)                     NOT NULL,
    [CreatedAtUtc]     DATETIMEOFFSET                    NOT NULL  
        CONSTRAINT [DF_ProposalAcceptance_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),

    CONSTRAINT [PK_ProposalAcceptance] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ProposalAcceptance_ProposalShare] 
        FOREIGN KEY ([ProposalShareId]) REFERENCES [quotation].[ProposalShare] ([Id]),
    CONSTRAINT [UX_ProposalAcceptance_ProposalShareId] 
        UNIQUE NONCLUSTERED ([ProposalShareId])
);
```

**Column rationale:**
- `ProposalShareId` (FK, UNIQUE) — Links to the share and enforces one-per-share at DB level.
- `AcceptedTerms` (NVARCHAR(500)) — Stores the exact text shown to the customer. Max 500 chars is generous for the fixed terms string.
- `AcceptedAtUtc` (DATETIMEOFFSET) — Server-clock timestamp of acceptance. Separate from `CreatedAtUtc` for semantic clarity (acceptance time vs. row insertion time — in practice they're nearly identical).
- `IpAddress` (NVARCHAR(45)) — Accommodates IPv6 (max 45 chars including zone ID).
- `UserAgent` (NVARCHAR(500)) — Modern user-agents are typically 100-300 chars; 500 provides headroom.
- `CreatedAtUtc` — Mandatory per project convention.

### Entity Model

```csharp
namespace Portal.Infrastructure.Entities;

/// <summary>
/// An immutable audit record capturing a customer's formal acceptance of a shared proposal.
/// Schema: [quotation].[ProposalAcceptance]
/// </summary>
public class ProposalAcceptance
{
    public int Id { get; set; }

    public int ProposalShareId { get; set; }

    public string AcceptedTerms { get; set; } = null!;

    public DateTimeOffset AcceptedAtUtc { get; set; }

    public string IpAddress { get; set; } = null!;

    public string UserAgent { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    // Navigation property
    public ProposalShare ProposalShare { get; set; } = null!;
}
```

### EF Core Configuration

```csharp
private static void ConfigureProposalAcceptance(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ProposalAcceptance>(entity =>
    {
        entity.ToTable("ProposalAcceptance", "quotation");

        entity.HasKey(e => e.Id);

        entity.HasOne(e => e.ProposalShare)
            .WithOne()
            .HasForeignKey<ProposalAcceptance>(e => e.ProposalShareId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        entity.HasIndex(e => e.ProposalShareId)
            .IsUnique()
            .HasDatabaseName("UX_ProposalAcceptance_ProposalShareId");

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
namespace Portal.Infrastructure.Constants;

public static class ProposalAcceptanceConstants
{
    public const string AcceptanceTermsText =
        "I accept this proposal and agree to proceed with the quoted work.";
}
```

### QuotationListDto Extension

The existing `QuotationListDto` is extended with an acceptance status field for the quotation list view:

```csharp
public class QuotationListDto
{
    // ... existing properties ...

    /// <summary>
    /// Acceptance status for the quotation's shared proposal link.
    /// Null = no active share, "awaiting" = shared but not accepted, "accepted" = accepted by customer.
    /// </summary>
    public string? AcceptanceStatus { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Acceptance persistence round-trip

*For any* valid ProposalShare (active, non-expired) and any HTTP context (with an IP address and user-agent string), submitting an acceptance SHALL persist a record where the stored `ProposalShareId` matches the share, the stored `AcceptedTerms` equals the constant acceptance text, the stored `IpAddress` matches the request IP, and the stored `UserAgent` matches the request user-agent header.

**Validates: Requirements 2.1, 7.1, 7.2, 7.3, 7.4**

### Property 2: Uniqueness — at most one acceptance per ProposalShare

*For any* ProposalShare that already has an Acceptance_Record, attempting to accept again SHALL be rejected (not persisted), and the total number of acceptance records for that share SHALL remain exactly one.

**Validates: Requirements 3.1**

### Property 3: Rejection on inactive or expired shares

*For any* ProposalShare where `IsActive = false` OR `ExpiresAtUtc <= DateTimeOffset.UtcNow`, submitting an acceptance request SHALL be rejected with an error, and no Acceptance_Record SHALL be persisted.

**Validates: Requirements 6.2**

## Error Handling

| Scenario | Handling | User Feedback |
|----------|----------|---------------|
| Share token not found (GET) | Return 404 | Browser shows not-found page |
| Share inactive/expired (GET) | Render Unavailable view | Existing behavior — no acceptance UI shown |
| Share inactive/expired (POST) | Return JSON `{ success: false, message }` | SweetAlert2 error: "This share link is no longer valid." |
| Duplicate acceptance (POST) | Return JSON `{ success: false, alreadyAccepted: true, acceptedAt }` | SweetAlert2 info: "Already accepted on {date}" |
| Database error on INSERT (POST) | Log error, return JSON `{ success: false, message }` | SweetAlert2 error: "An unexpected error occurred. Please try again." |
| UNIQUE constraint violation (race condition) | Catch `DbUpdateException`, treat as duplicate | SweetAlert2 info: "Already accepted on {date}" |

**Race condition handling:** If two browser tabs submit simultaneously, the UNIQUE constraint on `ProposalShareId` ensures only one INSERT succeeds. The second attempt catches the constraint violation and returns the "already accepted" response.

## Testing Strategy

### Property-Based Tests (xUnit + FsCheck)

Each correctness property is implemented as a property-based test with minimum 100 iterations:

- **Property 1**: Generate random IP addresses (IPv4/IPv6), random user-agent strings, and valid share states. Call `AcceptAsync`, then `GetByProposalShareIdAsync`, verify all fields match.
- **Property 2**: Generate a share with an existing acceptance record. Call `AcceptAsync` again. Assert rejection and count remains 1.
- **Property 3**: Generate shares with `IsActive=false` or `ExpiresAtUtc` in the past. Call `AcceptAsync`. Assert rejection and no record persisted.

**PBT Library**: FsCheck (C#/.NET property-based testing library)
**Minimum iterations**: 100 per property
**Tag format**: `// Feature: quotation-acceptance, Property {N}: {title}`

### Unit Tests (Example-Based)

- Service returns success with correct fields for a fresh active share
- Service returns `AlreadyAccepted` with date for duplicate attempt
- Service returns error for inactive share
- Service returns error for expired share
- Repository INSERT persists all columns correctly
- Repository SELECT by ProposalShareId returns correct record
- Controller returns proper JSON structure for success/error/duplicate cases
- Detail page sets correct ViewBag values for each acceptance state
- Index action populates AcceptanceStatus on QuotationListDto correctly

### Integration Tests

- Full POST → DB → GET round-trip with real SQL Server
- UNIQUE constraint violation produces graceful handling
- Concurrent acceptance attempts — only one succeeds
- Detail page renders acceptance status correctly for all three states (accepted, awaiting, no share)
