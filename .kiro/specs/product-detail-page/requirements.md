# Requirements Document: Product Detail & Insights Page

## Introduction

The Product Catalogue currently operates as a simple CRUD list with a modal for editing. This feature adds a dedicated Detail page for each product that surfaces sales performance, customer insights, pricing history, trend analysis, and demand forecasting — transforming the catalogue into a revenue intelligence tool.

## Glossary

- **Product**: A catalogue item in `[product].[Product]` used on invoice/quotation line items
- **Invoice Line**: A row in `[invoice].[InvoiceLine]` that references a product (via ProductCode or direct association)
- **Sales Product**: An item in `[sales].[Product]` optionally linked to a catalogue product via `ProductId` FK

## Requirements

### Requirement 1: Product Detail Page

**User Story:** As a business user, I want a dedicated page for each product showing its complete profile, so that I can see everything about it in one place.

#### Acceptance Criteria

1. THE system SHALL provide a product detail page at `/Product/Detail/{id}`.
2. THE page SHALL display: Product Code, Description, Selling Price, Cost Price, VAT Rate, Supplier (if linked), Product Type, Active status, Created date, Last Used date.
3. THE page SHALL include an "Edit" button that opens the existing edit modal.
4. THE page SHALL enforce tenant isolation (BusinessId check).

### Requirement 2: Sales Performance KPIs

**User Story:** As a business user, I want to see how much revenue a product generates, so that I can identify my best-performing items.

#### Acceptance Criteria

1. THE page SHALL display: Total Revenue (sum of line totals from issued invoices containing this product).
2. THE page SHALL display: Total Units Sold (sum of quantities from issued invoice lines).
3. THE page SHALL display: Average Selling Price (total revenue / total units — shows actual vs default price).
4. THE page SHALL display: Gross Margin (revenue - (cost price × total units)).
5. THE page SHALL display: Last Sold Date (most recent invoice date containing this product).
6. THE KPIs SHALL only include non-deleted, issued invoices (StatusTypeId = 2).

### Requirement 3: Customer Insights

**User Story:** As a business user, I want to know which customers buy this product most, so that I can focus my sales efforts.

#### Acceptance Criteria

1. THE page SHALL display a "Top Customers" section showing the top 5 customers by revenue for this product.
2. EACH customer entry SHALL show: Customer Name, Units Purchased, Total Revenue, Last Purchase Date.
3. THE page SHALL display: Unique Customer Count (how many distinct customers have purchased this product).
4. THE page SHALL display: Repeat Purchase Rate (percentage of customers who bought this product more than once).

### Requirement 4: Monthly Trend Chart

**User Story:** As a business user, I want to see the sales trend for a product over time, so that I can spot seasonality and growth patterns.

#### Acceptance Criteria

1. THE page SHALL display a line chart showing monthly revenue from this product for the last 12 months.
2. MONTHS with zero sales SHALL show as zero (not skipped).
3. THE chart SHALL use Chart.js (consistent with Cash Flow forecasting).
4. THE chart SHALL be available on Foundation tier.

### Requirement 5: Price History

**User Story:** As a business user, I want to see the full pricing history of a product, so that I can track how prices changed over time.

#### Acceptance Criteria

1. THE page SHALL display a "Price History" table showing all historical price changes.
2. EACH entry SHALL show: Selling Price, Cost Price, Effective From date, Changed By (user name).
3. THE table SHALL be sorted by Effective From descending (most recent first).
4. THIS replaces the truncated price history currently shown in the edit modal.

### Requirement 6: Demand Forecasting (Professional tier)

**User Story:** As a business user with Professional plan, I want to see projected demand for a product, so that I can plan inventory and pricing.

#### Acceptance Criteria

1. THE page SHALL display a "Forecast" section showing projected units and revenue for the next 30/60/90 days.
2. THE projection SHALL be based on the product's average monthly sales over the last 6 months.
3. IF the product type is "Stock", THE page SHALL show a reorder advisory when projected demand exceeds a configurable threshold.
4. THE forecast section SHALL be gated to Professional tier and above.
5. WHEN on Foundation tier, THE section SHALL show a soft-gate teaser.

### Requirement 7: Pipeline Activity (if linked to Sales Products)

**User Story:** As a business user, I want to see which active sales leads reference this product, so that I can track upcoming demand.

#### Acceptance Criteria

1. IF this catalogue product is linked to one or more Sales Products (via `ProductId` FK), THE page SHALL display a "Pipeline" section.
2. THE section SHALL show: count of active leads referencing this product, total estimated value, conversion rate (won / total leads with this product).
3. EACH active lead SHALL be listed with: Lead name, Stage, Estimated Value, Assigned To.
4. IF no Sales Products are linked, THIS section SHALL NOT appear.

### Requirement 8: Navigation & Linking

**User Story:** As a user navigating from other pages, I want to reach the Product Detail easily.

#### Acceptance Criteria

1. THE Product Catalogue list page SHALL have a clickable product code/name that links to the Detail page.
2. THE Sales Products "Linked Catalogue" column SHALL link to the Detail page.
3. Invoice line items (in detail view) with a product code SHALL link to the Detail page.
4. THE Detail page SHALL have a breadcrumb: Catalogue > Products > {Product Code}.
