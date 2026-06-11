# Implementation Plan: Quotation Acceptance (Proposal Acceptance)

## Overview

This implementation adds a formal proposal acceptance workflow to the shared proposal viewing system. A customer viewing a shared proposal (`/proposal/{token}`) can accept it via a checkbox and button, creating an immutable audit record (IP, user-agent, terms text, UTC timestamp). The business owner sees acceptance status on the quotation detail page and in the quotation list. The feature builds on the existing `ProposalShare` infrastructure with a new `ProposalAcceptance` table (1:0..1 relationship), a service layer, controller extensions, and client-side UI injected into the HTML snapshot.

## Tasks

- [x] 1. Database migration and entity setup
  - [x] 1.1 Create the `[quotation].[ProposalAcceptance]` table migration
    - Create `Portal.Database/Migrations/093_CreateProposalAcceptanceTable.sql`
    - Table columns: Id (INT IDENTITY PK), ProposalShareId (INT NOT NULL FK), AcceptedTerms (NVARCHAR(500) NOT NULL), AcceptedAtUtc (DATETIMEOFFSET NOT NULL), IpAddress (NVARCHAR(45) NOT NULL), UserAgent (NVARCHAR(500) NOT NULL), CreatedAtUtc (DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET())
    - Add UNIQUE nonclustered constraint `UX_ProposalAcceptance_ProposalShareId` on ProposalShareId
    - Add foreign key `FK_ProposalAcceptance_ProposalShare` referencing `[quotation].[ProposalShare]([Id])`
    - Use idempotent `IF NOT EXISTS` pattern
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 3.1_

  - [x] 1.2 Create the `ProposalAcceptance` entity class
    - Create `Portal.Infrastructure/Entities/ProposalAcceptance.cs`
    - Properties: Id, ProposalShareId, AcceptedTerms, AcceptedAtUtc (DateTimeOffset), IpAddress, UserAgent, CreatedAtUtc (DateTimeOffset)
    - Navigation property: ProposalShare
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 1.3 Register EF Core configuration for `ProposalAcceptance`
    - Add `DbSet<ProposalAcceptance>` to `PortalDbContext`
    - Configure entity in `OnModelCreating`: table name `ProposalAcceptance` in `quotation` schema, key, unique index on ProposalShareId, property max lengths, required constraints, CreatedAtUtc default value `SYSDATETIMEOFFSET()`
    - Configure 1:0..1 relationship with ProposalShare via `HasOne`/`WithOne`/`HasForeignKey`
    - _Requirements: 3.1, 7.5_

- [x] 2. Repository and service layer
  - [x] 2.1 Create `ProposalAcceptanceRepository`
    - Create `Portal.Infrastructure/Repositories/ProposalAcceptanceRepository.cs`
    - Inherit from `GenericStoredProcedureRepository<ProposalAcceptance>`
    - Implement `InsertAsync(ProposalAcceptance entity)` — INSERT only via `ExecuteSqlRawAsync` with full table name and SqlParameter inputs
    - Implement `GetByProposalShareIdAsync(int proposalShareId)` — SELECT query returning single record or null
    - Implement `GetAcceptedShareIdsAsync(IEnumerable<int> shareIds)` — Batch SELECT returning HashSet of accepted share IDs for quotation list page
    - Use try/catch with rethrow pattern, null-safe SqlParameters
    - No update or delete methods exposed (enforces immutability)
    - _Requirements: 7.5, 3.1_

  - [x] 2.2 Create `IProposalAcceptanceService` interface and `ProposalAcceptanceService` implementation
    - Create `Portal.Infrastructure/Services/IProposalAcceptanceService.cs` with methods: `AcceptAsync(string shareToken, string ipAddress, string userAgent)`, `GetByProposalShareIdAsync(int proposalShareId)`, and `GetAcceptedShareIdsAsync(IEnumerable<int> shareIds)`
    - Create `Portal.Web/Services/ProposalAcceptanceService.cs` implementing the interface
    - Inject `ProposalAcceptanceRepository` and `ProposalShareRepository`
    - `AcceptAsync` logic: look up share by token, validate IsActive and non-expired, check for existing acceptance (return AlreadyAccepted), build entity with constant terms text + IP + user-agent + UTC timestamp, call repository INSERT, catch DbUpdateException for UNIQUE violation (race condition → treat as duplicate)
    - Return `ProposalAcceptanceResult` with Success/Message/AcceptedAtUtc/AlreadyAccepted
    - _Requirements: 2.1, 3.1, 3.2, 6.1, 6.2, 7.1, 7.2, 7.3, 7.4_

  - [x] 2.3 Create `ProposalAcceptanceResult` model and `ProposalAcceptanceConstants`
    - Create `Portal.Infrastructure/Models/ProposalAcceptanceResult.cs` with properties: Success (bool), Message (string?), AcceptedAtUtc (DateTimeOffset?), AlreadyAccepted (bool)
    - Create `Portal.Infrastructure/Constants/ProposalAcceptanceConstants.cs` with the fixed terms text: "I accept this proposal and agree to proceed with the quoted work."
    - _Requirements: 7.1_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Controller and UI integration
  - [x] 4.1 Extend `ProposalController` GET action to inject acceptance UI
    - After rendering the proposal snapshot HTML, check if an acceptance already exists for this share via `IProposalAcceptanceService.GetByProposalShareIdAsync`
    - If accepted: inject read-only "Accepted on {date}" HTML message below the proposal content
    - If not accepted and share is active/non-expired: inject acceptance form HTML (checkbox with terms text + disabled "Accept Proposal" button) below the proposal content
    - Include inline JavaScript: checkbox enables/disables button, button click triggers acceptance POST via fetch with BlockUI
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 4.2 Add POST `/proposal/{token}/accept` endpoint to `ProposalController`
    - New `[HttpPost]` action `AcceptProposal(string token)`
    - Extract IP from `HttpContext.Connection.RemoteIpAddress`
    - Extract user-agent from `Request.Headers["User-Agent"]`
    - Call `_acceptanceService.AcceptAsync(token, ipAddress, userAgent)`
    - Return `Json(new { success, message, acceptedAt, alreadyAccepted })`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.2, 6.2_

  - [x] 4.3 Extend `QuotationController.Detail` to display acceptance status
    - Load active share for the quotation via `_proposalService.GetActiveShareByQuotationIdAsync`
    - If active share exists, check for acceptance via `_acceptanceService.GetByProposalShareIdAsync`
    - Set `ViewBag.AcceptanceStatus` to "accepted", "awaiting", or null (no share)
    - Set `ViewBag.AcceptedAtUtc` when status is "accepted"
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.4 Update Quotation Detail view to render acceptance status
    - In the Quotation Detail Razor view, add a section below the share info area
    - If `ViewBag.AcceptanceStatus == "accepted"`: display "Accepted on {date}" badge with success styling (#129867)
    - If `ViewBag.AcceptanceStatus == "awaiting"`: display "Awaiting acceptance" badge with warning styling (#C8912E)
    - If null: no acceptance UI shown
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.5 Extend `QuotationController.Index` to populate acceptance status on list items
    - After loading paged quotation results, collect all quotation IDs from the page
    - For each quotation, load active shares and collect share IDs into a dictionary (quotationId → shareId)
    - Batch-load accepted share IDs via `_acceptanceService.GetAcceptedShareIdsAsync(shareIds)`
    - Set `AcceptanceStatus` property on each `QuotationListDto` to "accepted", "awaiting", or null
    - Add `AcceptanceStatus` nullable string property to `QuotationListDto`
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 4.6 Update Quotation Index view to render acceptance note below Reference
    - In the quotation list table, below the quotation reference in the first column:
    - If `AcceptanceStatus == "accepted"`: display "✓ Accepted" note (green #129867, font-size 11px, font-weight 600)
    - If `AcceptanceStatus == "awaiting"`: display "⏳ Awaiting acceptance" note (amber #C8912E, font-size 11px, font-weight 600)
    - If null: no note shown
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 5. DI registration
  - [x] 5.1 Register new services in the DI container
    - Register `ProposalAcceptanceRepository` as Scoped
    - Register `IProposalAcceptanceService` → `ProposalAcceptanceService` as Scoped
    - Inject `IProposalAcceptanceService` into `ProposalController` constructor
    - Inject `IProposalAcceptanceService` into `QuotationController` constructor
    - _Requirements: 2.1, 4.1, 5.1_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Property-based tests
  - [x] 7.1 Write property test: Acceptance persistence round-trip (Property 1)
    - **Property 1: Acceptance persistence round-trip**
    - **Validates: Requirements 2.1, 7.1, 7.2, 7.3, 7.4**
    - Create `Portal.Tests/PropertyBased/ProposalAcceptancePersistencePropertyTests.cs`
    - Generate random valid IP addresses (IPv4/IPv6), random user-agent strings, and active non-expired share states
    - Call `AcceptAsync`, then `GetByProposalShareIdAsync`, verify stored ProposalShareId matches the share, AcceptedTerms equals the constant text, IpAddress matches input, UserAgent matches input

  - [x] 7.2 Write property test: Uniqueness — at most one acceptance per share (Property 2)
    - **Property 2: Uniqueness — at most one acceptance per ProposalShare**
    - **Validates: Requirements 3.1**
    - Create `Portal.Tests/PropertyBased/ProposalAcceptanceUniquenessPropertyTests.cs`
    - Generate a share with an existing acceptance record, call `AcceptAsync` again
    - Assert result has `AlreadyAccepted = true`, and total count of acceptance records for that share remains exactly one

  - [x] 7.3 Write property test: Rejection on inactive or expired shares (Property 3)
    - **Property 3: Rejection on inactive or expired shares**
    - **Validates: Requirements 6.2**
    - Create `Portal.Tests/PropertyBased/ProposalAcceptanceRejectionPropertyTests.cs`
    - Generate shares with `IsActive = false` or `ExpiresAtUtc` in the past
    - Call `AcceptAsync`, assert result has `Success = false` and no acceptance record is persisted

- [x] 8. Unit tests
  - [x] 8.1 Write unit tests for ProposalAcceptanceService
    - Create `Portal.Tests/Unit/Services/ProposalAcceptanceServiceTests.cs`
    - Test: returns success with correct fields for fresh active share
    - Test: returns AlreadyAccepted with date for duplicate attempt
    - Test: returns error for inactive share
    - Test: returns error for expired share
    - Test: handles DbUpdateException (UNIQUE constraint violation) gracefully as duplicate
    - Test: stores constant AcceptanceTermsText from ProposalAcceptanceConstants
    - _Requirements: 2.1, 3.1, 3.2, 6.2, 7.1_

  - [x] 8.2 Write unit tests for ProposalController acceptance endpoints
    - Create `Portal.Tests/Unit/Controllers/ProposalControllerAcceptanceTests.cs`
    - Test: GET injects acceptance form for active non-accepted share
    - Test: GET injects read-only message for already-accepted share
    - Test: GET does not inject acceptance UI for inactive/expired share
    - Test: POST returns success JSON on first acceptance
    - Test: POST returns alreadyAccepted JSON on duplicate
    - Test: POST returns error JSON for expired share
    - _Requirements: 1.1, 1.4, 2.2, 2.3, 3.2, 6.1_

  - [x] 8.3 Write unit tests for QuotationController acceptance status display
    - Create `Portal.Tests/Unit/Controllers/QuotationControllerAcceptanceStatusTests.cs`
    - Test: Detail sets ViewBag.AcceptanceStatus to "accepted" when acceptance exists
    - Test: Detail sets ViewBag.AcceptanceStatus to "awaiting" when share exists but no acceptance
    - Test: Detail sets ViewBag.AcceptanceStatus to null when no active share
    - Test: Index populates AcceptanceStatus on QuotationListDto correctly for all three states
    - _Requirements: 4.1, 4.2, 4.3, 5.1, 5.2, 5.3_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases using xUnit + Moq
- The `ProposalAcceptance` table uses DATETIMEOFFSET for UTC timestamp precision
- The UNIQUE constraint on ProposalShareId enforces the one-acceptance-per-share invariant at DB level, handling race conditions
- The repository exposes only INSERT and SELECT — no UPDATE or DELETE — enforcing immutability by design
- SQL migrations use idempotent `IF NOT EXISTS` patterns consistent with existing migrations
- All repository code follows the established try/catch + rethrow pattern per repository-standards steering
- The acceptance POST endpoint is AllowAnonymous, matching the existing ProposalController pattern
- The `GetAcceptedShareIdsAsync` batch method avoids N+1 queries on the quotation list page
- QuotationListDto is extended with a nullable `AcceptanceStatus` property for the list view

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.3"] },
    { "id": 3, "tasks": ["2.2"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["4.1", "4.2", "4.3", "4.5"] },
    { "id": 6, "tasks": ["4.4", "4.6"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 8, "tasks": ["8.1", "8.2", "8.3"] }
  ]
}
```
