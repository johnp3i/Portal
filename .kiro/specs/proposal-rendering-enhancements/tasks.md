# Implementation Plan: Proposal Rendering Enhancements

## Overview

This plan implements four rendering enhancements for the proposal snapshot: per-section totals boxes (Subscription and OneTime), narrative section type, section emphasis with accent color, and line item subtitle formatting. All changes are additive schema extensions with safe defaults, following the existing ASP.NET Core MVC 8 + SQL Server + Database-First patterns using raw SQL repositories.

## Tasks

- [x] 1. Database schema migration
  - [x] 1.1 Create migration script for ProposalSection and QuotationLine enhancements
    - Create `Portal.Database/Migrations/031_AddProposalRenderingEnhancements.sql`
    - ALTER TABLE [quotation].[ProposalSection] ADD [SectionType] NVARCHAR(20) NOT NULL DEFAULT 'LineItems'
    - ALTER TABLE [quotation].[ProposalSection] ADD [IsEmphasized] BIT NOT NULL DEFAULT 0
    - ALTER TABLE [quotation].[ProposalSection] ADD [AccentColor] NVARCHAR(20) NULL
    - ALTER TABLE [quotation].[QuotationLine] ADD [Subtitle] NVARCHAR(1000) NULL
    - Existing records retain current data with safe defaults
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [x] 2. Entity and DbContext updates
  - [x] 2.1 Update ProposalSection entity with SectionType, IsEmphasized, and AccentColor properties
    - Add `public string SectionType { get; set; } = "LineItems";` to `Portal.Infrastructure/Entities/ProposalSection.cs`
    - Add `public bool IsEmphasized { get; set; }` to `Portal.Infrastructure/Entities/ProposalSection.cs`
    - Add `public string? AccentColor { get; set; }` to `Portal.Infrastructure/Entities/ProposalSection.cs`
    - _Requirements: 3.5, 4.6_

  - [x] 2.2 Update QuotationLine entity with Subtitle property
    - Add `public string? Subtitle { get; set; }` to `Portal.Infrastructure/Entities/QuotationLine.cs`
    - _Requirements: 5.2_

  - [x] 2.3 Update PortalDbContext entity configurations
    - In ConfigureProposalSection: add `.Property(e => e.SectionType).IsRequired().HasMaxLength(20).HasDefaultValue("LineItems")`
    - In ConfigureProposalSection: add `.Property(e => e.IsEmphasized).IsRequired().HasDefaultValue(false)`
    - In ConfigureProposalSection: add `.Property(e => e.AccentColor).HasMaxLength(20)`
    - In ConfigureQuotationLine: add `.Property(e => e.Subtitle).HasMaxLength(1000)`
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 3. Repository layer updates
  - [x] 3.1 Update ProposalSectionRepository to include new columns
    - Update SELECT query in GetByQuotationIdAsync to include [SectionType], [IsEmphasized], [AccentColor]
    - Update SELECT query in GetByIdAsync to include [SectionType], [IsEmphasized], [AccentColor]
    - Update INSERT query in InsertAsync to include @SectionType, @IsEmphasized, @AccentColor parameters
    - Update UPDATE query in UpdateAsync to include [SectionType], [IsEmphasized], [AccentColor] columns
    - Use null-safe SqlParameter for AccentColor (`?? (object)DBNull.Value`)
    - _Requirements: 3.4, 4.5, 4.6_

  - [x] 3.2 Update QuotationLine repository queries to include Subtitle column
    - Update SELECT queries to include [Subtitle] column
    - Update INSERT queries to include @Subtitle parameter
    - Update UPDATE queries to include [Subtitle] = @Subtitle
    - Use null-safe SqlParameter for Subtitle (`?? (object)DBNull.Value`)
    - _Requirements: 5.1, 5.2_

- [x] 4. Checkpoint - Ensure schema and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer updates
  - [x] 5.1 Update ProposalSectionService to accept new fields
    - Update AddSectionAsync signature to accept sectionType, isEmphasized, accentColor parameters
    - Update UpdateSectionAsync signature to accept sectionType, isEmphasized, accentColor parameters
    - Validate SectionType is either "LineItems" or "Narrative" — throw ArgumentException for invalid values
    - Persist new fields through repository calls
    - _Requirements: 3.1, 4.1, 4.5, 4.6_

  - [x] 5.2 Update IProposalSectionService interface with new parameters
    - Update AddSectionAsync signature to include string sectionType = "LineItems", bool isEmphasized = false, string? accentColor = null
    - Update UpdateSectionAsync signature to include string? sectionType = null, bool? isEmphasized = null, string? accentColor = null
    - _Requirements: 3.1, 4.1_

  - [ ]* 5.3 Write property tests for SectionType validation and emphasis persistence
    - **Property 6: SectionType persistence and default value** — Section inserted without explicit SectionType reads back as "LineItems"; section inserted with "Narrative" reads back as "Narrative"
    - **Property 8: Emphasis and AccentColor field round-trip** — IsEmphasized and AccentColor values survive insert/read cycle unchanged
    - **Validates: Requirements 3.1, 3.4, 4.1, 4.5**

- [x] 6. Render model extensions
  - [x] 6.1 Update ProposalSectionRenderModel with new properties
    - Add `public string SectionType { get; set; } = "LineItems";` to ProposalSectionRenderModel
    - Add `public bool IsEmphasized { get; set; }` to ProposalSectionRenderModel
    - Add `public string? AccentColor { get; set; }` to ProposalSectionRenderModel
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 6.2 Update ProposalLineRenderModel with Subtitle property
    - Add `public string? Subtitle { get; set; }` to ProposalLineRenderModel
    - _Requirements: 8.4_

  - [x] 6.3 Update render model construction logic to populate new fields
    - When building ProposalSectionRenderModel, map SectionType, IsEmphasized, AccentColor from entity
    - When building ProposalLineRenderModel, map Subtitle from QuotationLine entity
    - _Requirements: 8.5, 8.6_

  - [ ]* 6.4 Write property test for render model field mapping
    - **Property 12: Render model field mapping from entities** — ProposalSectionRenderModel and ProposalLineRenderModel fields match source entity values
    - **Validates: Requirements 8.5, 8.6**

- [x] 7. Checkpoint - Ensure service and model layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Proposal snapshot view — per-section totals boxes
  - [x] 8.1 Implement per-section totals box for Subscription sections
    - After the line items table in each Subscription section, render a Section_Totals_Box
    - Calculate Total Monthly = sum of LineTotal for all lines in section
    - Calculate Total Daily Cost = Math.Round(Total Monthly / 30, 2) with "/day" suffix
    - Calculate Total Annual = Total Monthly × 12
    - Format all values with business CurrencySymbol
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 8.2 Implement per-section totals box for OneTime sections
    - After the line items table in each OneTime section, render a Section_Totals_Box
    - Calculate Section Subtotal = sum of LineTotal for all lines in section
    - Format with business CurrencySymbol
    - _Requirements: 2.1, 2.2, 2.3_

  - [ ]* 8.3 Write property tests for section totals calculations
    - **Property 1: Subscription section totals calculation** — Total Monthly = sum(LineTotals), Daily = Round(Monthly/30, 2), Annual = Monthly × 12
    - **Property 2: OneTime section subtotal calculation** — Subtotal = sum(LineTotals)
    - **Validates: Requirements 1.2, 1.3, 2.1, 2.2**

- [x] 9. Proposal snapshot view — narrative sections
  - [x] 9.1 Implement narrative section rendering
    - When section.SectionType == "Narrative", render as content card with Name as heading and Description as rich text body
    - Do NOT render a line items table for Narrative sections
    - Do NOT render a totals box for Narrative sections
    - _Requirements: 3.2, 3.3_

  - [ ]* 9.2 Write property test for narrative section rendering
    - **Property 5: Narrative section rendering** — Narrative sections render Name as heading and Description as body, with no table element
    - **Validates: Requirements 3.2, 3.3**

- [x] 10. Proposal snapshot view — section emphasis and accent color
  - [x] 10.1 Implement Signal Card pattern for emphasized sections
    - When section.IsEmphasized is true, apply 4px left border accent to the section card
    - Use section.AccentColor if non-null, otherwise default to #0D5EA6
    - Apply to both Narrative and LineItems section types
    - _Requirements: 4.2, 4.3, 4.4_

  - [ ]* 10.2 Write property test for emphasis accent color resolution
    - **Property 7: Emphasis accent color resolution** — IsEmphasized=true with AccentColor uses that color; IsEmphasized=true with null AccentColor uses #0D5EA6
    - **Validates: Requirements 4.2, 4.3, 4.4**

- [x] 11. Proposal snapshot view — line item subtitle and subscription columns
  - [x] 11.1 Implement line item subtitle rendering
    - Render QuotationLine Description as bold title in line items table
    - When Subtitle is non-null and non-empty, render below title in smaller muted font
    - When Subtitle is null/empty, render only Description without extra spacing
    - _Requirements: 5.3, 5.4, 5.5_

  - [x] 11.2 Update subscription column rendering with daily cost suffix
    - Ensure Daily Cost column displays value with "/day" suffix (e.g., "€5.40/day")
    - Ensure Daily Cost = Math.Round(UnitPrice / 30, 2) per line
    - Ensure Annual Price = UnitPrice × 12 per line
    - Format all monetary values with CurrencySymbol
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 11.3 Write property tests for subtitle rendering and subscription columns
    - **Property 9: Subtitle rendering** — Non-null Subtitle renders both title and subtitle; null Subtitle renders only title
    - **Property 11: Per-line subscription column calculations** — Daily = Round(UnitPrice/30, 2), Annual = UnitPrice × 12
    - **Property 3: Monetary value formatting with currency symbol** — Formatted string starts with CurrencySymbol followed by value to 2dp
    - **Property 4: Daily cost "/day" suffix formatting** — Daily cost strings end with "/day"
    - **Validates: Requirements 5.3, 5.4, 5.5, 6.2, 6.3, 6.4, 6.5**

- [x] 12. Checkpoint - Ensure snapshot rendering compiles and renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Edit UI updates for new section fields
  - [x] 13.1 Update Add Section modal with SectionType and emphasis fields
    - Add SectionType dropdown (LineItems / Narrative) to the Add Section modal
    - Add IsEmphasized checkbox to the Add Section modal
    - Add AccentColor input (optional hex color) to the Add Section modal
    - Wire new fields through the API call to POST /api/sections/add
    - _Requirements: 3.1, 4.1_

  - [x] 13.2 Update Edit Section modal with SectionType and emphasis fields
    - Add SectionType dropdown to the Edit Section modal
    - Add IsEmphasized checkbox to the Edit Section modal
    - Add AccentColor input to the Edit Section modal
    - Wire new fields through the API call to POST /api/sections/update
    - _Requirements: 3.1, 4.1_

  - [x] 13.3 Update line item forms with Subtitle field
    - Add Subtitle textarea to the Add Line form in _SectionCards.cshtml
    - Add Subtitle textarea to the Edit Line form in _SectionCards.cshtml
    - Wire Subtitle through the existing AddLine/UpdateLine form submissions
    - _Requirements: 5.6_

- [x] 14. Controller updates for new fields
  - [x] 14.1 Update ProposalSectionController request models and actions
    - Add SectionType, IsEmphasized, AccentColor to AddSectionRequest model
    - Add SectionType, IsEmphasized, AccentColor to UpdateSectionRequest model
    - Pass new fields through to service layer in AddSection and UpdateSection actions
    - _Requirements: 3.1, 4.1_

  - [x] 14.2 Update QuotationController line actions to handle Subtitle
    - Update AddLine action to accept and persist Subtitle parameter
    - Update UpdateLine action to accept and persist Subtitle parameter
    - _Requirements: 5.6_

  - [ ]* 14.3 Write property test for Subtitle field round-trip
    - **Property 10: Subtitle field round-trip** — QuotationLine with any Subtitle value (including null) survives insert/read cycle unchanged
    - **Validates: Requirements 5.1, 5.6**

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit as specified in the design document
- All repositories follow the GenericStoredProcedureRepository pattern with raw SQL, full table names, and null-safe SqlParameter usage
- All schema changes use safe defaults ensuring backward compatibility with existing data
- The rendering logic is entirely within the Razor view (Snapshot.cshtml) — totals are calculated in-view as they are purely presentational
