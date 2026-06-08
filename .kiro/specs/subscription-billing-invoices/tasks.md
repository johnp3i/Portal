# Implementation Plan: Subscription Billing Invoices

## Overview

This implementation plan introduces formal invoice number generation and management for the Portal (Bili) platform's subscription billing system. The work covers database migrations, core services (invoice number generation, VAT calculation, email delivery, backfill), updated PDF rendering, webhook integration, and comprehensive property-based and unit tests using FsCheck + xUnit.

## Tasks

- [x] 1. Database migrations and entity updates
  - [x] 1.1 Create the `[billing].[InvoiceSequence]` table migration
    - Create `Portal.Database/Migrations/085_CreateInvoiceSequenceTable.sql`
    - Table columns: Year (INT, PK), LastNumber (INT, NOT NULL, DEFAULT 0), CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
    - Add CHECK constraint `CK_InvoiceSequence_LastNumber` ensuring LastNumber >= 0
    - Use idempotent `IF NOT EXISTS` pattern
    - _Requirements: 2.1_

  - [x] 1.2 Add InvoiceNumber and IsEmailSent columns to `[billing].[Invoice]`
    - Create `Portal.Database/Migrations/086_AddInvoiceNumberToBillingInvoice.sql`
    - Add `InvoiceNumber` NVARCHAR(50) NULL column
    - Add `IsEmailSent` BIT NOT NULL DEFAULT 0 column
    - Create filtered unique nonclustered index `UX_Invoice_InvoiceNumber` on InvoiceNumber WHERE InvoiceNumber IS NOT NULL
    - Use idempotent `IF NOT EXISTS` pattern for each alteration
    - _Requirements: 3.1, 3.2, 6.7_

  - [x] 1.3 Create the `InvoiceSequence` entity and update `BillingInvoice` entity
    - Add `Portal.Infrastructure/Entities/Billing/InvoiceSequence.cs` with Year, LastNumber, CreatedAtUtc properties
    - Add `InvoiceNumber` (string?) and `IsEmailSent` (bool) properties to the existing `BillingInvoice` entity
    - Register `InvoiceSequence` DbSet in `PortalDbContext` and add EF Core configuration for both entities (default values, constraints)
    - _Requirements: 2.1, 3.1_

- [x] 2. Core invoice number generation service
  - [x] 2.1 Implement `InvoiceSequenceRepository`
    - Create `Portal.Infrastructure/Repositories/InvoiceSequenceRepository.cs`
    - Implement `IInvoiceSequenceRepository.IncrementAndGetAsync(int year)` using MERGE with HOLDLOCK and OUTPUT for atomic insert-or-update
    - Throw `InvalidOperationException` if LastNumber exceeds 9999
    - Use full table names in SQL, SqlParameter for inputs, try/catch with rethrow
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.2 Implement `InvoiceNumberGenerator` with Format/Parse utilities
    - Create `Portal.Web/Services/Billing/InvoiceNumberGenerator.cs` implementing `IInvoiceNumberGenerator`
    - Inject `IInvoiceSequenceRepository` and `IOptions<InvoiceSettings>`
    - `GenerateNextAsync(DateTime utcNow)`: validate PlatformCode, call repository, return formatted number
    - `Format(string platformCode, int year, int sequence)`: produce `{PlatformCode}-INV-{yyyy}-{NNNN}` string
    - `Parse(string invoiceNumber)`: return `InvoiceNumberComponents` record or null for invalid input
    - Validate PlatformCode is non-null, non-empty, alphanumeric only — throw `InvalidOperationException` otherwise
    - Create `InvoiceNumberComponents` record in the same file or a shared models location
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 8.1, 8.2, 8.3, 8.4_

  - [x] 2.3 Write property test: Invoice number format validity (Property 1)
    - **Property 1: Invoice number format validity**
    - **Validates: Requirements 1.1, 8.1**
    - Create `Portal.Tests/PropertyBased/Billing/InvoiceNumberFormatPropertyTests.cs`
    - Generate random alphanumeric PlatformCodes (1-10 chars), years (2020-2099), sequences (1-99999)
    - Assert output matches regex `^[A-Za-z0-9]{1,10}-INV-\d{4}-\d{4,}$` and components match input

  - [x] 2.4 Write property test: Format/Parse round-trip (Property 2)
    - **Property 2: Format/Parse round-trip**
    - **Validates: Requirements 8.4**
    - Create test in `Portal.Tests/PropertyBased/Billing/InvoiceNumberRoundTripPropertyTests.cs`
    - For random valid inputs, assert `Parse(Format(code, year, seq))` yields identical components

  - [x] 2.5 Write property test: Parse rejects malformed input (Property 3)
    - **Property 3: Parse rejects malformed input**
    - **Validates: Requirements 8.3**
    - Create test in `Portal.Tests/PropertyBased/Billing/InvoiceNumberParseRejectPropertyTests.cs`
    - Generate random strings not matching the valid pattern, assert Parse returns null

  - [x] 2.6 Write property test: PlatformCode validation (Property 4)
    - **Property 4: PlatformCode validation rejects invalid codes**
    - **Validates: Requirements 1.6**
    - Create test in `Portal.Tests/PropertyBased/Billing/PlatformCodeValidationPropertyTests.cs`
    - Generate null, empty, or strings with non-alphanumeric characters, assert GenerateNextAsync throws

- [x] 3. VAT calculation service
  - [x] 3.1 Implement `VatCalculationService`
    - Create `Portal.Web/Services/Billing/VatCalculationService.cs` implementing `IVatCalculationService`
    - Implement `Calculate(decimal netAmount, string? customerCountry, string? customerVatNumber)` returning `VatCalculationResult`
    - Logic: Cyprus → 19%, EU with VAT number → 0% reverse charge, EU without VAT number → 19%, non-EU → 0%, null/empty country → 19% with warning log
    - Maintain static readonly array of EU member state ISO codes
    - Include reverse charge notation string "Reverse Charge - Article 196 Council Directive 2006/112/EC"
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

  - [x] 3.2 Write property test: VAT calculation correctness (Property 5)
    - **Property 5: VAT calculation correctness**
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4**
    - Create `Portal.Tests/PropertyBased/Billing/VatCalculationPropertyTests.cs`
    - Generate random positive amounts, country codes (CY, EU non-CY, non-EU, null), and VAT numbers
    - Assert correct rate, VatAmount = netAmount × rate, GrossAmount = netAmount + VatAmount, reverse charge flag

  - [x] 3.3 Write unit tests for VatCalculationService edge cases
    - Create `Portal.Tests/Unit/Services/VatCalculationServiceTests.cs`
    - Test: null country defaults to 19%
    - Test: empty string country defaults to 19%
    - Test: Cyprus customer gets 19%
    - Test: EU customer with VAT number gets 0% reverse charge
    - Test: EU customer without VAT number gets 19%
    - Test: non-EU customer gets 0% (no reverse charge)
    - Test: decimal precision and rounding behavior
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Invoice email service
  - [x] 5.1 Implement `InvoiceEmailService`
    - Create `Portal.Web/Services/Billing/InvoiceEmailService.cs` implementing `IInvoiceEmailService`
    - Inject billing invoice repository, email sender, and logger
    - `SendInvoiceNotificationAsync(int billingInvoiceId)`: load invoice, check IsEmailSent flag, skip if true, load business owner email, compose email with InvoiceNumber/period/amount/download link, send via EmailDepartmentEnum.Invoices, mark IsEmailSent = true and save
    - Log Warning if no email address or delivery fails; do not throw
    - Log Information on successful send with recipient and InvoiceNumber
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 10.2, 10.4_

  - [x] 5.2 Write property test: No duplicate emails per invoice (Property 6)
    - **Property 6: No duplicate emails per invoice**
    - **Validates: Requirements 6.7**
    - Create `Portal.Tests/PropertyBased/Billing/InvoiceEmailIdempotencyPropertyTests.cs`
    - For random invoice records, call SendInvoiceNotificationAsync multiple times, assert email sent at most once

  - [x] 5.3 Write unit tests for InvoiceEmailService
    - Create `Portal.Tests/Unit/Services/InvoiceEmailServiceTests.cs`
    - Test: email sent on first call with correct department
    - Test: no email sent when IsEmailSent is already true
    - Test: no email sent when business has no email address (logs warning)
    - Test: SMTP failure logged as warning, no exception thrown
    - _Requirements: 6.3, 6.4, 6.6, 6.7_

- [x] 6. Invoice backfill service
  - [x] 6.1 Implement `InvoiceBackfillService`
    - Create `Portal.Web/Services/Billing/InvoiceBackfillService.cs` implementing `IInvoiceBackfillService`
    - `BackfillAsync()`: query invoices with null InvoiceNumber grouped by year, ordered by CreatedAtUtc ascending
    - For each year group, wrap in a single transaction: generate sequential numbers using InvoiceNumberGenerator, assign and save
    - Skip records that already have an InvoiceNumber
    - Return total count of records updated
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 6.2 Write property test: Backfill chronological ordering (Property 7)
    - **Property 7: Backfill chronological ordering with correct year**
    - **Validates: Requirements 7.1, 7.2**
    - Create `Portal.Tests/PropertyBased/Billing/BackfillOrderingPropertyTests.cs`
    - Generate random sets of invoice records with varying dates, run backfill, assert year component matches CreatedAtUtc.Year and earlier records get lower sequence numbers within same year

  - [x] 6.3 Write property test: Backfill idempotence (Property 8)
    - **Property 8: Backfill idempotence**
    - **Validates: Requirements 7.3**
    - Create `Portal.Tests/PropertyBased/Billing/BackfillIdempotencePropertyTests.cs`
    - Run backfill twice on same data set, assert second run produces no changes and all InvoiceNumbers remain identical

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Update BillingService PDF generation
  - [x] 8.1 Extend `BillingInvoicePdfModel` with VAT and issuer fields
    - Add properties: CompanyCountryCode, CompanyVatNumber, CompanyEmail, VatRate, VatAmount, IsReverseCharge, ReverseChargeNotation, SubscriberVatNumber
    - _Requirements: 5.1, 5.2, 5.5, 9.6, 9.7_

  - [x] 8.2 Update `BillingService.GenerateInvoicePdfAsync` to use formal invoice number and VAT
    - Inject `IVatCalculationService` and `IOptions<InvoiceSettings>` into BillingService
    - Use persisted `InvoiceNumber` from BillingInvoice; fall back to `INV-{Id:D6}` if null
    - Call VatCalculationService to compute VAT and populate PDF model
    - Populate issuer fields from InvoiceSettings (CompanyName, CompanyAddress, CompanyCountryCode, CompanyVatNumber, CompanyEmail)
    - Populate subscriber fields (business name, VAT number, address)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 9.6, 9.7_

  - [x] 8.3 Update invoice PDF Razor view template
    - Display formal invoice number prominently in header
    - Display all issuer fields (company name, address, country, VAT number, email)
    - Display subscriber fields (name, VAT number, address)
    - Display line items with description, quantity, unit price, line total
    - Display subtotal, VAT rate %, VAT amount, grand total
    - Display payment method and payment date
    - Include "Reverse Charge - Article 196 Council Directive 2006/112/EC" notation when applicable
    - _Requirements: 4.4, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 9.6, 9.7_

  - [x] 8.4 Write unit tests for updated BillingService PDF generation
    - Create `Portal.Tests/Unit/Services/BillingServiceInvoicePdfTests.cs`
    - Test: uses InvoiceNumber when present
    - Test: falls back to legacy format when InvoiceNumber is null
    - Test: PDF model contains all issuer fields from InvoiceSettings
    - Test: PDF model contains reverse charge notation for EU customer with VAT
    - Test: PDF model contains correct VAT calculation
    - _Requirements: 4.1, 4.2, 4.3, 5.1, 9.7_

- [x] 9. Update WebhookProcessingService for invoice number assignment
  - [x] 9.1 Integrate InvoiceNumberGenerator into `HandleInvoicePaid`
    - Inject `IInvoiceNumberGenerator` and `IInvoiceEmailService` into WebhookProcessingService
    - In the `HandleInvoicePaid` method, after beginning the transaction, call `GenerateNextAsync(DateTime.UtcNow)` to obtain the invoice number
    - Set `InvoiceNumber` on the BillingInvoice entity before inserting
    - After transaction commit, call `SendInvoiceNotificationAsync(billingInvoiceId)` asynchronously (fire-and-forget pattern with error logging)
    - On failure of number generation or assignment, let exception propagate to roll back entire transaction and return non-2xx to Stripe
    - Log Information-level entry on successful generation with InvoiceNumber, BusinessId, InvoiceId
    - _Requirements: 3.3, 3.4, 6.1, 6.5, 10.1, 10.3_

  - [x] 9.2 Write unit tests for updated WebhookProcessingService
    - Create `Portal.Tests/Unit/Services/WebhookInvoiceNumberAssignmentTests.cs`
    - Test: InvoiceNumber is set on BillingInvoice within same transaction
    - Test: email service called after transaction commit
    - Test: full transaction rollback when InvoiceNumberGenerator throws
    - Test: Information log emitted on success
    - Test: Error log emitted on failure
    - _Requirements: 3.3, 3.4, 6.5, 10.1, 10.3_

- [x] 10. DI registration and configuration
  - [x] 10.1 Register all new services in DI container
    - Register `IInvoiceSequenceRepository` → `InvoiceSequenceRepository`
    - Register `IInvoiceNumberGenerator` → `InvoiceNumberGenerator`
    - Register `IVatCalculationService` → `VatCalculationService`
    - Register `IInvoiceEmailService` → `InvoiceEmailService`
    - Register `IInvoiceBackfillService` → `InvoiceBackfillService`
    - Bind `InvoiceSettings` section from configuration (already partially exists)
    - Ensure all services are registered as Scoped (one per request)
    - _Requirements: 1.1, 1.6_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases using xUnit + Moq
- The project uses Database-First EF Core — entity updates in task 1.3 must align with scaffolding conventions
- SQL migrations use idempotent `IF NOT EXISTS` patterns consistent with existing migrations
- All repository code follows the established try/catch + rethrow pattern per repository-standards steering

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3"] },
    { "id": 2, "tasks": ["2.1", "3.1"] },
    { "id": 3, "tasks": ["2.2", "3.2", "3.3"] },
    { "id": 4, "tasks": ["2.3", "2.4", "2.5", "2.6", "5.1", "6.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.2", "6.3", "8.1"] },
    { "id": 6, "tasks": ["8.2"] },
    { "id": 7, "tasks": ["8.3", "8.4"] },
    { "id": 8, "tasks": ["9.1", "10.1"] },
    { "id": 9, "tasks": ["9.2"] }
  ]
}
```
