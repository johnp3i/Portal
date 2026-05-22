# Requirements Document

## Introduction

The Supplier Dashboard is a detailed analytics page for individual suppliers within the Portal platform. It provides business users with spend visibility, purchase history, and comparative analytics for each supplier relationship. The dashboard is accessed via the existing Supplier module and presents KPIs, interactive charts, and a filterable purchases table — all computed from existing purchase data without schema changes.

## Glossary

- **Supplier_Dashboard**: The analytics view at `/Supplier/Dashboard/{id}` displaying spend metrics, charts, and purchase history for a single supplier.
- **Supplier_Controller**: The existing MVC controller (`SupplierController`) handling supplier CRUD operations, extended with a new `Dashboard` action.
- **Dashboard_Service**: The service layer component responsible for computing supplier analytics (KPIs, chart data, purchase listings).
- **Purchase**: An expense entry in `[purchase].[Purchase]` representing money spent by the business, linked to a supplier and optionally to a VAT submission period.
- **VAT_Period**: A `VatSubmissionPeriod` entity representing a quarterly reporting window with start date, end date, and label.
- **KPI_Card**: A summary metric card displaying a single computed value (Total Spend, Total Purchases, Average Monthly Spend).
- **Period_Filter**: A dropdown control allowing the user to scope all dashboard data to a specific VAT period or "All Time".
- **Spend_Share_Chart**: A donut chart comparing the current supplier's spend against the top 5 other suppliers plus an "Others" aggregate slice.
- **Monthly_Spend_Chart**: A vertical bar chart showing the supplier's spend per calendar month within the selected period.
- **Period_Spend_Chart**: A vertical bar chart showing the supplier's spend per VAT period, with the selected period visually highlighted.
- **Purchases_Table**: A paginated HTML table listing individual purchase records for the supplier within the selected period.
- **Chart_JS**: The Chart.js library (v4.x) loaded via CDN, used to render all dashboard charts client-side.

## Requirements

### Requirement 1: Sidebar Navigation Entry

**User Story:** As a portal user, I want to see a "Suppliers" item in the sidebar navigation, so that I can access supplier management directly from any page.

#### Acceptance Criteria

1. THE Sidebar SHALL display a "Suppliers" navigation item within the Workspace navigation group.
2. WHEN the user clicks the "Suppliers" navigation item, THE Sidebar SHALL navigate the user to the Supplier Index page (`/Supplier`).
3. WHILE the user is viewing any page under the Supplier module, THE Sidebar SHALL visually highlight the "Suppliers" navigation item as active.

### Requirement 2: Dashboard Link in Supplier List

**User Story:** As a portal user, I want a "Dashboard" action link on each supplier row in the Supplier list page, so that I can quickly navigate to a supplier's analytics.

#### Acceptance Criteria

1. THE Supplier_Controller Index view SHALL display a "Dashboard" link in the Actions column for each supplier row.
2. WHEN the user clicks the "Dashboard" link for a supplier, THE Supplier_Controller SHALL navigate the user to `/Supplier/Dashboard/{id}` where `{id}` is the supplier's identifier.
3. THE "Dashboard" link SHALL only appear for suppliers that have an `IsActive` status of true.

### Requirement 3: Dashboard Page Route and Authorization

**User Story:** As a portal user, I want the Supplier Dashboard to be accessible at a predictable URL and protected by module-level authorization, so that only authorized users can view supplier analytics.

#### Acceptance Criteria

1. THE Supplier_Controller SHALL expose an HTTP GET action at the route `/Supplier/Dashboard/{id}` accepting an integer supplier identifier.
2. THE Supplier_Controller SHALL require the user to be authenticated and authorized for the Purchase module before serving the Dashboard view.
3. IF the supplied supplier identifier does not correspond to an existing supplier belonging to the user's business, THEN THE Supplier_Controller SHALL return a 404 Not Found response.
4. IF the supplier exists but has no purchase history, THEN THE Supplier_Dashboard SHALL render with zero-value KPIs and an empty purchases table.

### Requirement 4: Dashboard Topbar

**User Story:** As a portal user, I want to see the supplier's name, collaboration start date, and active status at the top of the dashboard, so that I have immediate context about the supplier relationship.

#### Acceptance Criteria

1. THE Supplier_Dashboard topbar SHALL display the supplier's Name as the page heading using a 42px Manrope font.
2. THE Supplier_Dashboard topbar SHALL display the supplier's `CreatedAtUtc` value formatted as "dd MMM yyyy" with the label "Collaboration since".
3. THE Supplier_Dashboard topbar SHALL display the supplier's active status as a pill badge showing "Active" (green) or "Inactive" (red).
4. THE Supplier_Dashboard topbar SHALL display an eyebrow label reading "Supplier Dashboard" above the supplier name.

### Requirement 5: KPI Cards

**User Story:** As a portal user, I want to see key spend metrics for the supplier at a glance, so that I can quickly assess the financial relationship.

#### Acceptance Criteria

1. THE Supplier_Dashboard SHALL display three KPI_Card components in a single row with equal width distribution.
2. THE first KPI_Card SHALL display "Total Spend (Excl. VAT)" computed as the sum of `AmountExcludingVat` from all non-cancelled purchases for the supplier within the selected period.
3. THE second KPI_Card SHALL display "Total Purchases" computed as the count of all non-cancelled purchases for the supplier within the selected period.
4. THE third KPI_Card SHALL display "Average Monthly Spend" computed as the Total Spend divided by the number of distinct calendar months containing at least one purchase within the selected period.
5. WHEN the Period_Filter is set to "All Time", THE KPI_Cards SHALL compute values across all non-cancelled purchases for the supplier regardless of VAT period assignment.
6. THE KPI_Card values SHALL be formatted with the business currency symbol and two decimal places for monetary amounts.

### Requirement 6: Period Filter

**User Story:** As a portal user, I want to filter the dashboard data by VAT period, so that I can analyze supplier spend within specific reporting windows.

#### Acceptance Criteria

1. THE Supplier_Dashboard SHALL display a Period_Filter dropdown containing "All Time" as the first option followed by each VAT_Period belonging to the user's business, ordered by `PeriodStartDate` ascending.
2. WHEN the user selects a VAT_Period from the Period_Filter, THE Supplier_Dashboard SHALL reload all KPI values, chart data, and the Purchases_Table to reflect only purchases assigned to the selected period.
3. WHEN the user selects "All Time" from the Period_Filter, THE Supplier_Dashboard SHALL display data across all purchases regardless of period assignment.
4. THE Period_Filter SHALL display each VAT_Period using its `PeriodLabel` value.
5. THE Supplier_Dashboard SHALL provide a "Clear" button adjacent to the Period_Filter that resets the selection to "All Time".

### Requirement 7: Spend Share Donut Chart

**User Story:** As a portal user, I want to see how this supplier's spend compares to other suppliers, so that I can understand relative spend concentration.

#### Acceptance Criteria

1. THE Spend_Share_Chart SHALL render as a donut chart using Chart_JS occupying the left 33% of the charts section.
2. THE Spend_Share_Chart SHALL display the current supplier's total spend (excl. VAT) as one slice, the top 5 other suppliers by spend as individual slices, and all remaining suppliers aggregated into an "Others" slice.
3. THE Spend_Share_Chart SHALL compute spend values scoped to the selected VAT_Period, or across all time when "All Time" is selected.
4. THE Spend_Share_Chart SHALL use the primary blue color (#0D5EA6) for the current supplier's slice.
5. IF the current supplier has no spend in the selected period, THEN THE Spend_Share_Chart SHALL display the current supplier's slice as zero while still showing other suppliers' data.
6. THE Spend_Share_Chart SHALL display a legend below the chart identifying each slice by supplier name.

### Requirement 8: Monthly Spend Bar Chart

**User Story:** As a portal user, I want to see the supplier's spend broken down by month, so that I can identify spending trends over time.

#### Acceptance Criteria

1. THE Monthly_Spend_Chart SHALL render as a vertical bar chart using Chart_JS positioned in the upper-right area (67% width, stacked vertically with the Period_Spend_Chart).
2. WHEN a specific VAT_Period is selected, THE Monthly_Spend_Chart SHALL display one bar per calendar month within that period's date range, labeled with the abbreviated month name.
3. WHEN "All Time" is selected, THE Monthly_Spend_Chart SHALL display one bar per calendar month across the entire purchase history for the supplier.
4. THE Monthly_Spend_Chart SHALL compute each bar's value as the sum of `AmountExcludingVat` for non-cancelled purchases in that calendar month.
5. THE Monthly_Spend_Chart SHALL use the primary blue color (#0D5EA6) for all bars with a border radius of 6px.

### Requirement 9: Per-Period Spend Bar Chart

**User Story:** As a portal user, I want to see the supplier's spend across all VAT periods, so that I can compare quarterly performance.

#### Acceptance Criteria

1. THE Period_Spend_Chart SHALL render as a vertical bar chart using Chart_JS positioned below the Monthly_Spend_Chart in the right column.
2. THE Period_Spend_Chart SHALL display one bar per VAT_Period belonging to the user's business, labeled with the period's abbreviated label.
3. THE Period_Spend_Chart SHALL compute each bar's value as the sum of `AmountExcludingVat` for non-cancelled purchases assigned to that period.
4. WHEN a specific VAT_Period is selected in the Period_Filter, THE Period_Spend_Chart SHALL highlight the corresponding bar using the primary blue color (#0D5EA6) and render all other bars in accent cyan (#57B8E8).
5. WHEN "All Time" is selected, THE Period_Spend_Chart SHALL render all bars in the primary blue color (#0D5EA6).

### Requirement 10: Purchases Table

**User Story:** As a portal user, I want to see a detailed list of purchases from this supplier, so that I can review individual transactions.

#### Acceptance Criteria

1. THE Purchases_Table SHALL display columns: Date, Description, Category, Excl. VAT, VAT, and Total.
2. THE Purchases_Table SHALL display only non-cancelled purchases for the current supplier, filtered by the selected VAT_Period (or all purchases when "All Time" is selected).
3. THE Purchases_Table SHALL sort purchases by `InvoiceDate` in ascending order.
4. THE Purchases_Table SHALL paginate results with 10 records per page.
5. THE Purchases_Table SHALL display pagination controls showing the current page range and total record count (e.g., "Showing 1–10 of 28 purchases").
6. THE Purchases_Table "Date" column SHALL format `InvoiceDate` as "dd MMM yyyy".
7. THE Purchases_Table "Category" column SHALL display the related `ExpenseCategory` name.
8. THE Purchases_Table monetary columns SHALL be right-aligned and formatted with the business currency symbol and two decimal places.

### Requirement 11: Back Navigation

**User Story:** As a portal user, I want a clear way to return to the Supplier list from the dashboard, so that I can navigate efficiently.

#### Acceptance Criteria

1. THE Supplier_Dashboard SHALL display a "Back to Suppliers" link positioned below the Purchases_Table section, right-aligned.
2. WHEN the user clicks the "Back to Suppliers" link, THE Supplier_Dashboard SHALL navigate the user to the Supplier Index page (`/Supplier`).
3. THE "Back to Suppliers" link SHALL be styled as an outlined button following the MyChair Design System.

### Requirement 12: Chart Library Integration

**User Story:** As a developer, I want Chart.js loaded via CDN on the dashboard page, so that charts render without adding server-side dependencies.

#### Acceptance Criteria

1. THE Supplier_Dashboard view SHALL include a script reference to Chart.js version 4.x from the jsDelivr CDN.
2. THE Supplier_Dashboard SHALL initialize all charts after the DOM has loaded and chart data has been provided by the server.
3. IF the Chart_JS CDN fails to load, THEN THE Supplier_Dashboard SHALL display a fallback message "Charts unavailable" in place of each chart canvas.

### Requirement 13: Data Computation Rules

**User Story:** As a developer, I want all dashboard metrics computed from existing purchase data without schema changes, so that the feature integrates cleanly with the current database.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL compute all metrics by querying the `[purchase].[Purchase]` table joined with `[purchase].[Supplier]` and `[vat].[VatSubmissionPeriod]`.
2. THE Dashboard_Service SHALL exclude cancelled purchases (`IsCancelled = true`) from all computations.
3. THE Dashboard_Service SHALL scope all queries to the authenticated user's `BusinessId`.
4. THE Dashboard_Service SHALL not require any database schema modifications or new tables.
5. WHEN computing the Spend_Share_Chart data, THE Dashboard_Service SHALL rank all suppliers by total spend (excl. VAT) within the selected period, return the top 5 other suppliers individually, and aggregate the remainder into an "Others" total.

### Requirement 14: Design System Compliance

**User Story:** As a designer, I want the Supplier Dashboard to follow the MyChair Design System, so that it is visually consistent with the rest of the platform.

#### Acceptance Criteria

1. THE Supplier_Dashboard SHALL use Manrope font for all headings and Inter font for body text.
2. THE Supplier_Dashboard SHALL use glass card containers with 30px border-radius and the standard shadow (`0 20px 50px rgba(13,94,166,.10)`).
3. THE Supplier_Dashboard SHALL use the primary blue (#0D5EA6) as the dominant accent color for KPI values, chart elements, and interactive controls.
4. THE Supplier_Dashboard layout SHALL follow the established page structure: topbar, filter section, main content cards, as defined in the layout standards.
5. THE Supplier_Dashboard SHALL render within the existing sidebar + content grid layout without modifying the shared layout template.
