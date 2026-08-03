# Requirements Document: Sales Products Rename & Catalog Linking

## Introduction

The Sales Pipeline module has a "Products" section that stores items/services offered to leads. This conflicts with the "Products" page in the Catalog section (which stores line-item catalog entries for invoices/quotations). This feature renames the sales module's "Products" to "Products & Services" and adds optional linking between sales products and the Product Catalog.

## Glossary

- **Sales Product**: An item in `[sales].[SalesProduct]` — represents something offered to a lead/opportunity
- **Catalog Product**: An item in `[product].[Product]` — represents a line-item template for invoices and quotations
- **Linking**: An optional FK from Sales Product to Catalog Product, enabling price/description inheritance

## Requirements

### Requirement 1: Rename "Products" to "Products & Services"

**User Story:** As a user, I want the Opportunities section's "Products" renamed to "Products & Services", so that it's clear this covers both physical products and service offerings — distinct from the invoice line-item catalog.

#### Acceptance Criteria

1. THE navigation item SHALL display "Products & Services" instead of "Products" in the Opportunities section.
2. THE page heading SHALL display "Products & Services".
3. THE "New Product" button SHALL be renamed to "New Product / Service".
4. ALL references in modals, confirmations, and messages SHALL use "product/service" terminology.

### Requirement 2: Optional Link to Product Catalog

**User Story:** As a user, I want to optionally link a sales product to an existing catalog item, so that pricing and descriptions stay consistent between what I offer in the pipeline and what appears on invoices.

#### Acceptance Criteria

1. THE `[sales].[SalesProduct]` table SHALL have a new nullable column `ProductId` (INT NULL, FK → `[product].[Product]`).
2. WHEN creating or editing a sales product, THE form SHALL include an optional "Link to Catalog" dropdown showing active catalog products.
3. WHEN a sales product is linked to a catalog item, THE sales product's detail view SHALL display the catalog item's code, selling price, and VAT rate as reference information.
4. THE link SHALL be optional — sales products can exist without a catalog link.
5. WHEN a linked catalog item's price changes, THE sales product SHALL NOT auto-update — it shows reference info only (point-in-time awareness, not live sync).

### Requirement 3: Navigation Consistency

**User Story:** As a user, I want to clearly distinguish between the Opportunities "Products & Services" and the Catalog "Products" in the sidebar.

#### Acceptance Criteria

1. THE Catalog section SHALL retain its "Products" label (it refers to the line-item catalog).
2. THE Opportunities section SHALL display "Products & Services" (it refers to sales offerings).
3. IF both sections are visible, THE user SHALL not confuse which is which due to distinct section contexts.
