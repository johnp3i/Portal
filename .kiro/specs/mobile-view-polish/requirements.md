# Requirements: Mobile View Polish

## Introduction

The Portal platform has a mobile responsive layout implemented (Layers 1–11 in `mobile.css`) with structural CSS policies for tables, forms, KPI cards, charts, filters, buttons, and page headers. However, many views still use inline styles, non-standard class names, or custom layouts that don't automatically inherit the design policies. This spec defines the systematic per-view audit and polish required to make every view render cleanly on phone (≤768px) and tablet (769–1100px) viewports.

The design policies are documented in `.kiro/docs/Mobile_Design_Policies.md`.

## Glossary

- **Design_Policy**: A reusable CSS pattern in `mobile.css` that targets a component type (tables, forms, KPI cards, etc.)
- **Policy_Class**: A CSS class (e.g., `.dashboard-card`, `.index-table`, `.filter-toggle`, `.topbar-actions`) that activates a design policy for a specific element
- **data-mobile**: A data attribute on `<td>` elements that controls cell visibility and placement in the mobile card layout (`status`, `amount`, `hide`)
- **Phone_Viewport**: Screen width ≤768px
- **Tablet_Viewport**: Screen width 769–1100px

## Requirements

### Requirement 1: Dashboard View Polish

**User Story:** As a mobile user viewing the Dashboard, I want all KPI gauges, charts, stats strip, and mini-tables to render cleanly without cropping or overlapping, so I can scan operational data at a glance.

#### Acceptance Criteria

1. THE gauge row (Revenue, Outstanding, Overdue, Expenses) SHALL render as a 2×2 grid on Phone_Viewport with readable values and progress bars that fit within each card
2. THE quotation stats strip SHALL render each stat as a distinct row with value left-aligned and label right-aligned on Phone_Viewport
3. THE VAT summary within the stats strip SHALL render as a clearly separated card below the quotation stats
4. THE charts (Revenue vs Expenses, Invoice Status) SHALL stack vertically on Phone_Viewport and fill the full available width
5. THE mini-tables (Recent Invoices, Overdue Invoices, Recent Payments, Recent Quotations, Top Customers) SHALL render as card-list layout on Phone_Viewport with all data visible (no horizontal scroll)
6. THE quick action buttons SHALL render as a 2-column grid of compact tiles on Phone_Viewport

### Requirement 2: Invoice Index Polish

**User Story:** As a mobile user on the Invoice List page, I want filters collapsed, buttons compact, and the invoice table showing key data without horizontal scrolling.

#### Acceptance Criteria

1. THE filter panel SHALL be collapsed by default on Phone_Viewport with a visible "Filters" toggle button
2. WHEN the "Filters" toggle is tapped, THE filter panel SHALL expand showing inputs in a compact 2-column grid
3. THE "Create Invoice" button SHALL render full-width on Phone_Viewport
4. THE "Export CSV" and "Export PDF" buttons SHALL render as compact inline buttons in a secondary row
5. THE invoice table SHALL render as row cards on Phone_Viewport showing: Invoice # (top-left), Status pill (top-right), Customer (bottom-left), Total (bottom-right)
6. THE Invoice Date, Due Date, Financial Status, and Actions columns SHALL be hidden on Phone_Viewport
7. ON Tablet_Viewport, THE table SHALL use horizontal scroll with all columns visible

### Requirement 3: Quotation Index Polish

**User Story:** As a mobile user on the Quotation List page, I want the same compact layout as Invoice Index with key quotation data visible at a glance.

#### Acceptance Criteria

1. THE filter panel SHALL be collapsed by default on Phone_Viewport with a "Filters" toggle button
2. THE "Create Quotation" button SHALL render full-width on Phone_Viewport
3. THE quotation table SHALL render as row cards on Phone_Viewport showing: Reference (top-left), Status pill (top-right), Customer (bottom-left), Total (bottom-right)
4. THE Valid Until and Actions columns SHALL be hidden on Phone_Viewport
5. ON Tablet_Viewport, THE table SHALL use horizontal scroll with all columns visible

### Requirement 4: Invoice Detail Polish

**User Story:** As a mobile user viewing an invoice detail, I want all sections (header, status, line items, totals, actions) stacked vertically with clear separation and readable values.

#### Acceptance Criteria

1. THE action buttons (Edit, Preview, Download PDF, Print, Share, Duplicate, Delete) SHALL stack vertically as full-width buttons on Phone_Viewport
2. THE invoice meta fields (Customer, Invoice Number, Invoice Date, Due Date) SHALL stack vertically on Phone_Viewport
3. THE line items table SHALL render as card layout or horizontally scrollable on Phone_Viewport
4. THE totals section SHALL be clearly visible with full-width values
5. THE status section with Issue/Record Payment buttons SHALL render at full width

### Requirement 5: Invoice Create/Edit Polish

**User Story:** As a mobile user creating or editing an invoice, I want all form fields at full-width with comfortable touch targets and the line item grid usable without horizontal scroll.

#### Acceptance Criteria

1. ALL form fields SHALL render at full-width in a single column on Phone_Viewport with minimum 44px height
2. THE line item entry grid SHALL collapse to a single-column stacked layout on Phone_Viewport
3. THE "Add Line Item" button SHALL render at full-width on Phone_Viewport
4. THE customer selection and date fields SHALL have appropriate input types for mobile keyboards
5. ON Tablet_Viewport, form fields SHALL render in a 2-column grid

### Requirement 6: Quotation Detail Polish

**User Story:** As a mobile user viewing a quotation detail, I want all sections stacked, readable, and actions easily accessible.

#### Acceptance Criteria

1. THE action buttons SHALL stack vertically as full-width buttons on Phone_Viewport
2. THE quotation meta fields SHALL stack vertically on Phone_Viewport
3. THE line items SHALL render as card layout or scrollable container on Phone_Viewport
4. THE section breakdown (if present) SHALL stack vertically with full-width cards

### Requirement 7: Quotation Create/Edit Polish

**User Story:** As a mobile user creating or editing a quotation, I want all form fields and line item inputs usable with single-column layout.

#### Acceptance Criteria

1. ALL form fields SHALL render at full-width in a single column on Phone_Viewport
2. THE line item grid SHALL collapse to single-column stacked entries on Phone_Viewport
3. THE "Add Line Item" and "Add Section" buttons SHALL render full-width
4. THE catalog search/lookup SHALL be usable on Phone_Viewport
5. ON Tablet_Viewport, form fields SHALL render in a 2-column grid

### Requirement 8: Revenue Dashboard Polish

**User Story:** As a mobile user on the Revenue Dashboard, I want KPI cards, action buttons, and receivables data clearly visible without clutter.

#### Acceptance Criteria

1. THE KPI cards (Outstanding, Overdue, Paid This Month, Partially Paid) SHALL render as full-width stacked cards with left-border colour accent on Phone_Viewport
2. THE action buttons (View Receivables, Customer Statement, Record Payment) SHALL render as full-width stacked buttons on Phone_Viewport
3. THE receivables table (if shown) SHALL use row-card layout on Phone_Viewport
4. ON Tablet_Viewport, KPI cards SHALL render as a 2-column grid

### Requirement 9: Customer Views Polish

**User Story:** As a mobile user managing customers, I want the customer list and form to be usable on phone with full-width fields and readable table data.

#### Acceptance Criteria

1. THE Customer Index table SHALL render as row cards on Phone_Viewport (Name top-left, Email/Phone bottom)
2. THE Customer Create/Edit form SHALL render all fields at full-width single-column on Phone_Viewport
3. THE filter panel (if present) SHALL be collapsible on Phone_Viewport

### Requirement 10: Purchase Views Polish

**User Story:** As a mobile user managing purchases, I want the purchase list, create form, and bulk entry grid to be usable on mobile.

#### Acceptance Criteria

1. THE Purchase Index table SHALL use row-card layout or horizontal scroll on Phone_Viewport
2. THE Purchase Create/Edit form SHALL render all fields at full-width single-column on Phone_Viewport
3. THE Bulk Entry grid SHALL use horizontal scroll with momentum on Phone_Viewport
4. THE filter panel SHALL be collapsible on Phone_Viewport

### Requirement 11: Supplier Views Polish

**User Story:** As a mobile user viewing supplier data, I want KPI cards and purchase tables to render cleanly.

#### Acceptance Criteria

1. THE Supplier Dashboard KPI cards SHALL render as a 2-column grid on Phone_Viewport
2. THE purchases table SHALL use horizontal scroll on Phone_Viewport
3. THE Supplier Index table SHALL use row-card layout on Phone_Viewport

### Requirement 12: VAT Views Polish

**User Story:** As a mobile user managing VAT periods, I want the period list, detail breakdown, and submission data clearly readable.

#### Acceptance Criteria

1. THE VAT Periods Index table SHALL use row-card or horizontal scroll layout on Phone_Viewport
2. THE VAT Detail meta grid SHALL stack vertically on Phone_Viewport
3. THE breakdown tables SHALL use horizontal scroll with momentum on Phone_Viewport
4. THE VAT chart SHALL fill the full available width

### Requirement 13: Credit Note Views Polish

**User Story:** As a mobile user managing credit notes, I want the same clean layout as Invoice views.

#### Acceptance Criteria

1. THE Credit Note Index table SHALL use row-card layout on Phone_Viewport
2. THE Credit Note Detail SHALL stack all sections vertically
3. THE Credit Note Create form SHALL render at full-width single-column on Phone_Viewport
4. THE summary cards (Total Issued, Total Value, Pending) SHALL render as 2-column grid

### Requirement 14: Admin Views Polish

**User Story:** As a mobile admin user, I want audit logs, system logs, user management, and module access tables to be scannable on mobile.

#### Acceptance Criteria

1. THE data tables in all Admin views SHALL use horizontal scroll with scroll-hint on Phone_Viewport
2. THE filter controls SHALL stack vertically on Phone_Viewport
3. THE action buttons SHALL be full-width on Phone_Viewport

### Requirement 15: Business Profile Polish

**User Story:** As a mobile user editing my business profile, I want all form sections stacked vertically and readable.

#### Acceptance Criteria

1. THE Business Profile form SHALL render all fields at full-width single-column on Phone_Viewport
2. THE logo upload section SHALL render at full-width on Phone_Viewport
3. ON Tablet_Viewport, THE form SHALL use a 2-column grid for fields

### Requirement 16: Customer Statement Polish

**User Story:** As a mobile user viewing customer statements, I want filters compact and the statement table readable.

#### Acceptance Criteria

1. THE filter panel SHALL stack vertically on Phone_Viewport
2. THE statement table SHALL use horizontal scroll with scroll-hint on Phone_Viewport
3. THE summary totals SHALL render as full-width values above the table
