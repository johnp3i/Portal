# Requirements Document

## Introduction

This feature extends the Portal quotation module with two complementary capabilities: (1) a Line Item Catalog that automatically builds a reusable library of line items from sent/accepted quotations, enabling quick auto-fill when creating new quotation lines; and (2) enhanced Quotation Sections that allow users to organize line items into named, reorderable sections with descriptions and notes, rendered as distinct cards in both the edit view and the proposal snapshot.

## Glossary

- **Line_Item_Catalog**: A per-business library of reusable quotation line item templates, automatically populated from quotation status transitions and searchable when creating new lines.
- **Catalog_Entry**: A single record in the Line_Item_Catalog containing a description, unit price, VAT rate, reference URL, discount, and discount type.
- **Proposal_Section**: A named grouping of quotation lines within a quotation, containing a header, optional description, assigned line items, and optional notes displayed below the line items.
- **Default_Section**: An implicit section that contains all QuotationLines not explicitly assigned to a named Proposal_Section.
- **Portal**: The ASP.NET Core MVC web application.
- **Business_User**: An authenticated user with quotation module permissions for the relevant business.
- **Quotation**: A commercial proposal document containing priced line items sent to a Customer.
- **QuotationLine**: An individual priced item within a Quotation.

## Requirements

### Requirement 1: Automatic Catalog Population on Status Transition

**User Story:** As a Business_User, I want quotation line items to be automatically saved to a reusable catalog when a quotation is sent or accepted, so that I build a library of commonly used items over time without manual effort.

#### Acceptance Criteria

1. WHEN a Quotation transitions to the "Sent" status (QuotationStatusTypeId = 2), THE Portal SHALL save each QuotationLine from that Quotation to the Line_Item_Catalog for the owning business.
2. WHEN a Quotation transitions to the "Accepted" status (QuotationStatusTypeId = 3), THE Portal SHALL save each QuotationLine from that Quotation to the Line_Item_Catalog for the owning business.
3. THE Portal SHALL store the following fields for each Catalog_Entry: Description, UnitPrice, VatRate, ReferenceUrl, Discount, and DiscountType.
4. WHEN a QuotationLine has the same Description as an existing Catalog_Entry for the same business, THE Portal SHALL update the existing Catalog_Entry with the latest values (upsert by Description per BusinessId).
5. THE Portal SHALL associate each Catalog_Entry with the BusinessId of the Quotation from which the line item originated.
6. THE Portal SHALL record the timestamp of when each Catalog_Entry was last updated.

### Requirement 2: Catalog Search and Auto-Fill

**User Story:** As a Business_User, I want to search through my catalog of previously used line items when creating a new quotation line, so that I can quickly populate fields without retyping common items.

#### Acceptance Criteria

1. WHEN a Business_User is creating or editing a QuotationLine, THE Portal SHALL provide a search interface to query the Line_Item_Catalog by description text.
2. WHEN the Business_User selects a Catalog_Entry from the search results, THE Portal SHALL auto-fill the QuotationLine fields (Description, UnitPrice, VatRate, ReferenceUrl, Discount, DiscountType) with the values from the selected Catalog_Entry.
3. THE Portal SHALL allow the Business_User to override any auto-filled field value after selection from the catalog.
4. THE Portal SHALL filter catalog search results to only show Catalog_Entries belonging to the current business.
5. WHEN the search query is fewer than 2 characters, THE Portal SHALL not execute a catalog search.
6. THE Portal SHALL return catalog search results ordered by most recently updated first.

### Requirement 3: Catalog Entry Management

**User Story:** As a Business_User, I want to view and manage my line item catalog, so that I can remove outdated entries or correct item details.

#### Acceptance Criteria

1. THE Portal SHALL provide a catalog management view listing all Catalog_Entries for the current business.
2. THE Portal SHALL allow the Business_User to delete a Catalog_Entry from the Line_Item_Catalog.
3. THE Portal SHALL allow the Business_User to edit the fields of an existing Catalog_Entry (Description, UnitPrice, VatRate, ReferenceUrl, Discount, DiscountType).
4. WHEN a Catalog_Entry is deleted, THE Portal SHALL not affect any existing QuotationLines that were previously populated from that entry.
5. THE Portal SHALL display the last updated timestamp for each Catalog_Entry in the management view.

### Requirement 4: Quotation Section Structure

**User Story:** As a Business_User, I want to organize my quotation line items into named sections, so that the quotation and proposal clearly separate different categories of work or cost (e.g., Software Cost, Hardware Cost, Configuration/Setup).

#### Acceptance Criteria

1. THE Portal SHALL allow a Quotation to contain multiple named Proposal_Sections, each with a unique Name within that Quotation.
2. THE Portal SHALL store a SortOrder for each Proposal_Section to control display ordering.
3. THE Portal SHALL allow each Proposal_Section to have an optional Description field (free text explaining the section purpose).
4. THE Portal SHALL allow each Proposal_Section to have an optional Notes field (free text displayed below the section line items in the proposal).
5. THE Portal SHALL retain the existing ColumnConfiguration field on Proposal_Section for controlling column display.
6. WHEN a QuotationLine is not assigned to any Proposal_Section, THE Portal SHALL treat the line as belonging to a Default_Section.

### Requirement 5: Section Management in Edit View

**User Story:** As a Business_User, I want to add, remove, and reorder sections on the quotation edit page, so that I can structure the quotation to match my commercial offering.

#### Acceptance Criteria

1. THE Portal SHALL render each Proposal_Section as a distinct visual card on the quotation edit page, containing the section header, description, line items, and notes.
2. THE Portal SHALL allow the Business_User to add a new Proposal_Section to a Quotation with a name and optional description.
3. THE Portal SHALL allow the Business_User to remove a Proposal_Section from a Quotation.
4. WHEN a Proposal_Section is removed, THE Portal SHALL reassign all QuotationLines from that section to the Default_Section (set ProposalSectionId to NULL).
5. THE Portal SHALL allow the Business_User to reorder Proposal_Sections by updating their SortOrder values.
6. THE Portal SHALL allow the Business_User to move a QuotationLine from one Proposal_Section to another.
7. THE Portal SHALL allow the Business_User to edit the Name, Description, and Notes of an existing Proposal_Section.

### Requirement 6: Proposal Section Schema Extension

**User Story:** As a developer, I want the ProposalSection table extended with Description and Notes columns, so that sections can carry additional context rendered in the proposal.

#### Acceptance Criteria

1. THE Portal database SHALL add a nullable Description column (NVARCHAR(2000)) to the [quotation].[ProposalSection] table.
2. THE Portal database SHALL add a nullable Notes column (NVARCHAR(4000)) to the [quotation].[ProposalSection] table.
3. THE ProposalSection entity SHALL expose Description and Notes as nullable string properties.
4. THE existing ProposalSection records SHALL retain their current data after the schema migration (non-breaking change).

### Requirement 7: Proposal Snapshot Section Rendering

**User Story:** As a customer viewing a shared proposal, I want to see clearly separated sections with their headers, descriptions, line item tables, and notes, so that I can understand the breakdown of the commercial offer.

#### Acceptance Criteria

1. THE Proposal_Renderer SHALL render each Proposal_Section as a separate card containing: the section Name as a heading, the Description below the heading (when present), a table of line items, and the Notes below the table (when present).
2. THE Proposal_Renderer SHALL render Proposal_Sections in their SortOrder sequence.
3. WHERE a Quotation has lines in the Default_Section only (no named sections), THE Proposal_Renderer SHALL render all lines in a single table without a section heading.
4. THE Proposal_Renderer SHALL apply the ColumnConfiguration of each section to determine which columns appear in that section table.

### Requirement 8: Line Item Catalog Data Isolation

**User Story:** As a platform operator, I want catalog data to be strictly isolated per business, so that one business cannot access another business's catalog entries.

#### Acceptance Criteria

1. THE Portal SHALL enforce that all Line_Item_Catalog queries filter by the authenticated user's current BusinessId.
2. THE Portal SHALL store BusinessId as a non-nullable foreign key on each Catalog_Entry.
3. WHEN a user switches business context, THE Portal SHALL only display Catalog_Entries belonging to the newly selected business.
