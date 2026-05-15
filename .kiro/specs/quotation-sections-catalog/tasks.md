# Implementation Plan: Quotation Sections & Line Item Catalog

## Overview

This plan implements two complementary features: a Line Item Catalog (auto-populated from quotation transitions, searchable via JSON API) and enhanced Quotation Sections (Description/Notes columns, card-based Edit view, updated proposal rendering). Tasks follow the existing ASP.NET Core MVC 8 + SQL Server + Database-First patterns using raw SQL repositories.

## Tasks

- [x] 1. Database schema migrations
  - [x] 1.1 Create migration script to add Description and Notes columns to ProposalSection table
    - Create `Portal.Database/Migrations/029_AddDescriptionNotesToProposalSection.sql`
    - ALTER TABLE [quotation].[ProposalSection] ADD [Description] NVARCHAR(2000) NULL
    - ALTER TABLE [quotation].[ProposalSection] ADD [Notes] NVARCHAR(4000) NULL
    - _Requirements: 6.1, 6.2, 6.4_

  - [x] 1.2 Create migration script for the LineItemCatalog table
    - Create `Portal.Database/Migrations/030_CreateLineItemCatalogTable.sql`
    - CREATE TABLE [quotation].[LineItemCatalog] with Id, BusinessId, Description, UnitPrice, VatRate, ReferenceUrl, Discount, DiscountType, UpdatedAtUtc
    - Include PK, FK to [portal].[Business], UNIQUE constraint on (BusinessId, Description), and indexes
    - _Requirements: 1.3, 1.5, 8.2_

- [x] 2. Entity and DbContext updates
  - [x] 2.1 Update ProposalSection entity with Description and Notes properties
    - Add `public string? Description { get; set; }` and `public string? Notes { get; set; }` to `Portal.Infrastructure/Entities/ProposalSection.cs`
    - _Requirements: 6.3_

  - [x] 2.2 Create LineItemCatalog entity
    - Create `Portal.Infrastructure/Entities/LineItemCatalog.cs` with Id, BusinessId, Description, UnitPrice, VatRate, ReferenceUrl, Discount, DiscountType, UpdatedAtUtc, and Business navigation property
    - _Requirements: 1.3, 1.5, 8.2_

  - [x] 2.3 Update PortalDbContext with LineItemCatalog configuration and global query filter
    - Add DbSet<LineItemCatalog>, entity configuration (table mapping, column types, indexes, unique constraint), and global query filter on BusinessId
    - Update ProposalSection configuration to include Description (MaxLength 2000) and Notes (MaxLength 4000)
    - _Requirements: 8.1, 8.2_

- [x] 3. Repository layer
  - [x] 3.1 Create LineItemCatalogRepository
    - Create `Portal.Infrastructure/Repositories/LineItemCatalogRepository.cs` extending GenericStoredProcedureRepository<LineItemCatalog>
    - Implement: SearchByDescriptionAsync(int businessId, string query) — LIKE-based search ordered by UpdatedAtUtc DESC
    - Implement: UpsertAsync(LineItemCatalog entity) — INSERT or UPDATE by BusinessId + Description using MERGE or conditional pattern
    - Implement: GetAllByBusinessIdAsync(int businessId) — full list ordered by UpdatedAtUtc DESC
    - Implement: GetByIdAsync(int id) — single entry lookup
    - Implement: DeleteAsync(int id) — remove entry
    - Implement: UpdateAsync(LineItemCatalog entity) — edit entry fields
    - Use full table names in SQL, parameterized queries, null-safe SqlParameter patterns
    - _Requirements: 1.4, 2.1, 2.4, 2.6, 3.1, 3.2, 3.3, 8.1_

  - [x] 3.2 Update ProposalSectionRepository to include Description and Notes columns
    - Update SELECT queries in GetByQuotationIdAsync to include [Description] and [Notes]
    - Update INSERT query in InsertAsync to include @Description and @Notes parameters
    - Update UPDATE query in UpdateAsync to include [Description] = @Description and [Notes] = @Notes
    - Add GetByIdAsync(int id) method for single section lookup
    - _Requirements: 4.3, 4.4, 5.7, 6.3_

- [x] 4. Checkpoint - Ensure schema and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer — Line Item Catalog
  - [x] 5.1 Create ILineItemCatalogService interface
    - Create `Portal.Infrastructure/Services/ILineItemCatalogService.cs`
    - Define: SearchAsync(int businessId, string query), PopulateFromQuotationAsync(int quotationId, int businessId), GetAllAsync(int businessId), GetByIdAsync(int id, int businessId), DeleteAsync(int id, int businessId), UpdateAsync(LineItemCatalog entry, int businessId)
    - _Requirements: 1.1, 1.2, 2.1, 3.1, 3.2, 3.3_

  - [x] 5.2 Create LineItemCatalogService implementation
    - Create `Portal.Infrastructure/Services/LineItemCatalogService.cs`
    - SearchAsync: validate minimum 2-char query length, delegate to repository, return empty list for short queries
    - PopulateFromQuotationAsync: fetch quotation lines by quotationId, upsert each into catalog with current timestamp
    - GetAllAsync: delegate to repository GetAllByBusinessIdAsync
    - DeleteAsync: validate ownership (BusinessId match), delegate to repository
    - UpdateAsync: validate ownership, delegate to repository
    - _Requirements: 1.1, 1.2, 1.4, 2.4, 2.5, 3.2, 3.3, 8.1_

  - [ ]* 5.3 Write property tests for LineItemCatalogService
    - **Property 3: Catalog upsert deduplication** — Two lines with same Description for same business result in exactly one catalog entry with latest values
    - **Property 5: Catalog search minimum query length** — Queries of length 0 or 1 return empty results
    - **Validates: Requirements 1.4, 2.5**

- [x] 6. Service layer — Proposal Section orchestration
  - [x] 6.1 Create IProposalSectionService interface
    - Create `Portal.Infrastructure/Services/IProposalSectionService.cs`
    - Define: GetByQuotationIdAsync, AddSectionAsync, RemoveSectionAsync, ReorderSectionsAsync, MoveLineToSectionAsync, UpdateSectionAsync
    - _Requirements: 4.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [x] 6.2 Create ProposalSectionService implementation
    - Create `Portal.Infrastructure/Services/ProposalSectionService.cs`
    - AddSectionAsync: validate non-empty name, compute next SortOrder, insert via repository
    - RemoveSectionAsync: set ProposalSectionId = NULL on all lines in section, then delete section
    - ReorderSectionsAsync: bulk update SortOrder based on ordered list of section IDs
    - MoveLineToSectionAsync: update QuotationLine.ProposalSectionId to target section (or NULL for default)
    - UpdateSectionAsync: update Name, Description, Notes fields
    - _Requirements: 4.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [ ]* 6.3 Write property tests for ProposalSectionService
    - **Property 12: Section deletion reassigns lines to default** — After section deletion, all N lines have ProposalSectionId = NULL
    - **Property 13: Section reordering preserves all sections** — After reorder, SortOrder reflects new permutation, no sections lost
    - **Property 14: Line move between sections** — After move, line's ProposalSectionId equals target, other fields unchanged
    - **Validates: Requirements 5.4, 5.5, 5.6**

- [x] 7. Integrate catalog population into QuotationService
  - [x] 7.1 Modify TransitionStatusAsync to call catalog population
    - Inject ILineItemCatalogService into QuotationService constructor
    - After successful status update to Sent (2) or Accepted (3), call PopulateFromQuotationAsync
    - Wrap catalog population in try/catch — log errors but do NOT roll back the status transition (catalog is supplementary)
    - _Requirements: 1.1, 1.2_

  - [ ]* 7.2 Write property test for catalog population on status transition
    - **Property 1: Catalog population on status transition** — After transition to status 2 or 3, catalog contains entry for each unique description among quotation lines
    - **Validates: Requirements 1.1, 1.2**

- [x] 8. Checkpoint - Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. API controller for catalog search
  - [x] 9.1 Create LineItemCatalogController with search endpoint
    - Create `Portal.Web/Controllers/LineItemCatalogController.cs`
    - GET /api/catalog/search?q={query} — returns JSON array of matching catalog entries filtered by current business
    - Inject ILineItemCatalogService and ICurrentTenantService
    - Return 200 with results or empty array
    - _Requirements: 2.1, 2.4, 2.5, 2.6, 8.1_

  - [x] 9.2 Add catalog management actions (List, Edit, Delete)
    - Add Index action returning management view with all catalog entries for current business
    - Add Edit GET/POST actions for updating a catalog entry
    - Add Delete POST action for removing a catalog entry
    - _Requirements: 3.1, 3.2, 3.3, 3.5_

  - [ ]* 9.3 Write property test for catalog tenant isolation
    - **Property 4: Catalog tenant isolation** — All search results have BusinessId equal to the querying business
    - **Validates: Requirements 2.4, 8.1, 8.3**

- [x] 10. Section management controller actions
  - [x] 10.1 Add section CRUD actions to QuotationController (or dedicated controller)
    - Add POST action to create a new ProposalSection (name, description)
    - Add POST action to delete a ProposalSection (reassigns lines to default)
    - Add POST action to reorder sections (accepts ordered list of section IDs)
    - Add POST action to move a QuotationLine to a different section
    - Add POST action to update section Name, Description, Notes
    - _Requirements: 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [x] 11. Edit view restructuring — sections as cards
  - [x] 11.1 Update Quotation Edit view to render sections as distinct cards
    - Each ProposalSection rendered as a card with: section Name heading, Description below heading, line items table, Notes below table
    - Default section (lines with ProposalSectionId = NULL) rendered as a card without explicit heading or with "General" label
    - Add UI controls for: add section, remove section, reorder sections, move line between sections, edit section fields
    - _Requirements: 5.1, 4.6_

  - [x] 11.2 Add catalog autocomplete to line item creation/editing
    - Add JavaScript/AJAX call to GET /api/catalog/search?q={query} on description field input
    - Show dropdown with matching catalog entries when query >= 2 characters
    - On selection, auto-fill Description, UnitPrice, VatRate, ReferenceUrl, Discount, DiscountType fields
    - Allow user to override any auto-filled value
    - _Requirements: 2.1, 2.2, 2.3, 2.5_

- [x] 12. Catalog management view
  - [x] 12.1 Create catalog management views (Index, Edit)
    - Create `Portal.Web/Views/LineItemCatalog/Index.cshtml` — table listing all entries with Description, UnitPrice, VatRate, Discount, DiscountType, UpdatedAtUtc, and Edit/Delete actions
    - Create `Portal.Web/Views/LineItemCatalog/Edit.cshtml` — form for editing catalog entry fields
    - Display last updated timestamp for each entry
    - _Requirements: 3.1, 3.3, 3.5_

- [x] 13. Proposal snapshot rendering updates
  - [x] 13.1 Update ProposalSectionRenderModel with Description and Notes
    - Add `public string? Description { get; set; }` and `public string? Notes { get; set; }` to ProposalRenderModel (or ProposalSectionRenderModel)
    - Update model construction logic to populate Description and Notes from ProposalSection entity
    - _Requirements: 7.1_

  - [x] 13.2 Update proposal snapshot view to render section Description and Notes
    - Render Description below section heading when present
    - Render Notes below line items table when present
    - Render sections in SortOrder sequence
    - When quotation has only default section lines (no named sections), render single table without section heading
    - Apply ColumnConfiguration per section for column visibility
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 14. Dependency injection registration
  - [x] 14.1 Register new services and repositories in DI container
    - Register LineItemCatalogRepository, ProposalSectionService, LineItemCatalogService in Program.cs or service registration extension
    - Register ILineItemCatalogService → LineItemCatalogService
    - Register IProposalSectionService → ProposalSectionService
    - _Requirements: 1.1, 2.1, 5.2_

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit as specified in the design document
- All repositories follow the GenericStoredProcedureRepository pattern with raw SQL, full table names, and null-safe SqlParameter usage
- Catalog population failure does NOT roll back the status transition — it is supplementary functionality
