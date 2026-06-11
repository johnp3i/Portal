# Implementation Plan: Invoice Acceptance

## Overview

This implementation adds a formal invoice acceptance workflow to the shared invoice viewing system. A customer viewing a shared invoice can accept it via a checkbox and button, creating an immutable audit record (IP, user-agent, terms text, UTC timestamp). The business owner sees acceptance status on the invoice detail page. The feature builds on the existing `InvoiceShare` infrastructure with a new `InvoiceAcceptance` table (1:0..1 relationship), a service layer, controller extensions, and client-side UI injected into the HTML snapshot.

## Tasks

- [x] 1. Database migration and entity setup
  - [x] 1.1 Create the `[invoice].[InvoiceAcceptance]` table migration
    - Create `Portal.Database/Migrations/092_CreateInvoiceAcceptanceTable.sql`
    - Table columns: Id (INT IDENTITY PK), InvoiceShareId (INT NOT NULL FK), AcceptedTerms (NVARCHAR(500) NOT NULL), AcceptedAtUtc (DATETIMEOFFSET NOT NULL), IpAddress (NVARCHAR(45) NOT NULL), UserAgent (NVARCHAR(500) NOT NULL), CreatedAtUtc (DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET())
    - Add UNIQUE nonclustered constraint `UX_InvoiceAcceptance_InvoiceShareId` on InvoiceShareId
    - Add foreign key `FK_InvoiceAcceptance_InvoiceShare` referencing `[invoice].[InvoiceShare]([Id])`
    - Use idempotent `IF NOT EXISTS` pattern
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 3.1_

  - [x] 1.2 Create the `InvoiceAcceptance` entity class
    - Create `Portal.Infrastructure/Entities/InvoiceAcceptance.cs`
    - Properties: Id, InvoiceShareId, AcceptedTerms, AcceptedAtUtc (DateTimeOffset), IpAddress, UserAgent, CreatedAtUtc (DateTimeOffset)
    - Navigation property: InvoiceShare
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 1.3 Register EF Core configuration for `InvoiceAcceptance`
    - Add `DbSet<InvoiceAcceptance>` to `PortalDbContext`
    - Configure entity in `OnModelCreating`: table name `InvoiceAcceptance` in `invoice` schema, key, unique index on InvoiceShareId, property max lengths, required constraints, CreatedAtUtc default value `SYSDATETIMEOFFSET()`
    - Configure 1:0..1 relationship with InvoiceShare via `HasOne`/`WithOne`/`HasForeignKey`
    - _Requirements: 3.1, 6.5_

- [x] 2. Repository and service layer
  - [x] 2.1 Create `InvoiceAcceptanceRepository`
    - Create `Portal.Infrastructure/Repositories/InvoiceAcceptanceRepository.cs`
    - Inherit from `GenericStoredProcedureRepository<InvoiceAcceptance>`
    - Implement `InsertAsync(InvoiceAcceptance entity)` — INSERT only via `ExecuteSqlRawAsync` with full table name and SqlParameter inputs
    - Implement `GetByInvoiceShareIdAsync(int invoiceShareId)` — SELECT query returning single record or null
    - Use try/catch with rethrow pattern, null-safe SqlParameters
    - No update or delete methods exposed (enforces immutability)
    - _Requirements: 6.5, 3.1_

  - [x] 2.2 Create `IInvoiceAcceptanceService` interface and `InvoiceAcceptanceService` implementation
    - Create `Portal.Infrastructure/Services/IInvoiceAcceptanceService.cs` with methods: `AcceptAsync(string shareToken, string ipAddress, string userAgent)` and `GetByInvoiceShareIdAsync(int invoiceShareId)`
    - Create `Portal.Web/Services/InvoiceAcceptanceService.cs` implementing the interface
    - Inject `InvoiceAcceptanceRepository` and `IInvoiceSharingService`
    - `AcceptAsync` logic: look up share by token, validate IsActive and non-expired, check for existing acceptance (return AlreadyAccepted), build entity with constant terms text + IP + user-agent + UTC timestamp, call repository INSERT, catch DbUpdateException for UNIQUE violation (race condition → treat as duplicate)
    - Return `InvoiceAcceptanceResult` with Success/Message/AcceptedAtUtc/AlreadyAccepted
    - _Requirements: 2.1, 3.1, 3.2, 5.2, 6.1, 6.2, 6.3, 6.4_

  - [x] 2.3 Create `InvoiceAcceptanceResult` model and `InvoiceAcceptanceConstants`
    - Create `Portal.Infrastructure/Models/InvoiceAcceptanceResult.cs` with properties: Success (bool), Message (string?), AcceptedAtUtc (DateTimeOffset?), AlreadyAccepted (bool)
    - Create `Portal.Infrastructure/Constants/InvoiceAcceptanceConstants.cs` with the fixed terms text: "I accept this invoice as correct and agree to pay by the due date."
    - _Requirements: 6.1_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Controller and UI integration
  - [x] 4.1 Extend `InvoiceViewController` GET action to inject acceptance UI
    - After the existing download button injection, check if an acceptance already exists for this share via `IInvoiceAcceptanceService.GetByInvoiceShareIdAsync`
    - If accepted: inject read-only "Accepted on {date}" HTML message below the download button
    - If not accepted: inject acceptance form HTML (checkbox with terms text + disabled "Accept Invoice" button) below the download button
    - Include inline JavaScript: checkbox enables/disables button, button click triggers acceptance POST via fetch with BlockUI
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 4.2 Add POST `/invoice-view/{token}/accept` endpoint to `InvoiceViewController`
    - New `[HttpPost]` action `AcceptInvoice(string token)`
    - Extract IP from `HttpContext.Connection.RemoteIpAddress`
    - Extract user-agent from `Request.Headers["User-Agent"]`
    - Call `_acceptanceService.AcceptAsync(token, ipAddress, userAgent)`
    - Return `Json(new { success, message, acceptedAt, alreadyAccepted })`
    - _Requirements: 2.1, 2.2, 2.3, 3.2, 5.2_

  - [x] 4.3 Extend `InvoiceController.Detail` to display acceptance status
    - Load active share for the invoice via `_sharingService.GetActiveShareByInvoiceIdAsync`
    - If active share exists, check for acceptance via `_acceptanceService.GetByInvoiceShareIdAsync`
    - Set `ViewBag.AcceptanceStatus` to "accepted", "awaiting", or null (no share)
    - Set `ViewBag.AcceptedAtUtc` when status is "accepted"
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.4 Update Invoice Detail view to render acceptance status
    - In the Invoice Detail Razor view, add a section below the share info area
    - If `ViewBag.AcceptanceStatus == "accepted"`: display "Accepted on {date}" badge with success styling
    - If `ViewBag.AcceptanceStatus == "awaiting"`: display "Awaiting acceptance" badge with warning styling
    - If null: no acceptance UI shown
    - Use MyChair design system colors (Success #129867, Warning #C8912E)
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 5. DI registration
  - [x] 5.1 Register new services in the DI container
    - Register `InvoiceAcceptanceRepository` as Scoped
    - Register `IInvoiceAcceptanceService` → `InvoiceAcceptanceService` as Scoped
    - Inject `IInvoiceAcceptanceService` into `InvoiceViewController` constructor
    - Inject `IInvoiceAcceptanceService` into `InvoiceController` constructor
    - _Requirements: 2.1, 4.1_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Property-based tests
  - [x] 7.1 Write property test: Acceptance persistence round-trip (Property 1)
    - **Property 1: Acceptance persistence round-trip**
    - **Validates: Requirements 2.1, 6.1, 6.2, 6.3, 6.4**
    - Create `Portal.Tests/PropertyBased/InvoiceAcceptancePersistencePropertyTests.cs`
    - Generate random valid IP addresses (IPv4/IPv6), random user-agent strings, and active non-expired share states
    - Call `AcceptAsync`, then `GetByInvoiceShareIdAsync`, verify stored InvoiceShareId matches the share, AcceptedTerms equals the constant text, IpAddress matches input, UserAgent matches input

  - [x] 7.2 Write property test: Uniqueness — at most one acceptance per share (Property 2)
    - **Property 2: Uniqueness — at most one acceptance per InvoiceShare**
    - **Validates: Requirements 3.1**
    - Create `Portal.Tests/PropertyBased/InvoiceAcceptanceUniquenessPropertyTests.cs`
    - Generate a share with an existing acceptance record, call `AcceptAsync` again
    - Assert result has `AlreadyAccepted = true`, and total count of acceptance records for that share remains exactly one

  - [x] 7.3 Write property test: Rejection on inactive or expired shares (Property 3)
    - **Property 3: Rejection on inactive or expired shares**
    - **Validates: Requirements 5.2**
    - Create `Portal.Tests/PropertyBased/InvoiceAcceptanceRejectionPropertyTests.cs`
    - Generate shares with `IsActive = false` or `ExpiresAtUtc` in the past
    - Call `AcceptAsync`, assert result has `Success = false` and no acceptance record is persisted

- [x] 8. Unit tests
  - [x] 8.1 Write unit tests for InvoiceAcceptanceService
    - Create `Portal.Tests/Unit/Services/InvoiceAcceptanceServiceTests.cs`
    - Test: returns success with correct fields for fresh active share
    - Test: returns AlreadyAccepted with date for duplicate attempt
    - Test: returns error for inactive share
    - Test: returns error for expired share
    - Test: handles DbUpdateException (UNIQUE constraint violation) gracefully as duplicate
    - Test: stores constant AcceptanceTermsText
    - _Requirements: 2.1, 3.1, 3.2, 5.2, 6.1_

  - [x] 8.2 Write unit tests for InvoiceViewController acceptance endpoints
    - Create `Portal.Tests/Unit/Controllers/InvoiceViewControllerAcceptanceTests.cs`
    - Test: GET injects acceptance form for active non-accepted share
    - Test: GET injects read-only message for already-accepted share
    - Test: GET does not inject acceptance UI for inactive/expired share
    - Test: POST returns success JSON on first acceptance
    - Test: POST returns alreadyAccepted JSON on duplicate
    - Test: POST returns error JSON for expired share
    - _Requirements: 1.1, 1.4, 2.2, 2.3, 3.2, 5.1_

  - [x] 8.3 Write unit tests for InvoiceController acceptance status display
    - Create `Portal.Tests/Unit/Controllers/InvoiceControllerAcceptanceStatusTests.cs`
    - Test: sets ViewBag.AcceptanceStatus to "accepted" when acceptance exists
    - Test: sets ViewBag.AcceptanceStatus to "awaiting" when share exists but no acceptance
    - Test: sets ViewBag.AcceptanceStatus to null when no active share
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases using xUnit + Moq
- The `InvoiceAcceptance` table uses DATETIMEOFFSET (not DATETIME) as specified in the design for UTC timestamp precision
- The UNIQUE constraint on InvoiceShareId enforces the one-acceptance-per-share invariant at DB level, handling race conditions
- The repository exposes only INSERT and SELECT — no UPDATE or DELETE — enforcing immutability by design
- SQL migrations use idempotent `IF NOT EXISTS` patterns consistent with existing migrations
- All repository code follows the established try/catch + rethrow pattern per repository-standards steering
- The acceptance POST endpoint is AllowAnonymous, matching the existing InvoiceViewController pattern

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.3"] },
    { "id": 3, "tasks": ["2.2"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 6, "tasks": ["4.4"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 8, "tasks": ["8.1", "8.2", "8.3"] }
  ]
}
```
