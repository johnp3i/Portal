# Requirements Document

## Introduction

The Payment Schedules Overview feature adds a dedicated page at `/Revenue/PaymentSchedules` that provides a bird's-eye view of all active payment schedules across invoices for a business. The page aggregates KPI metrics, displays a monthly payment timeline, and offers a filterable table of active schedules with progress indicators. This page reuses the existing Payment Schedule entities, repositories, and computation engines — it only adds read-only aggregation queries and a new view layer.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application operated by business tenants
- **Overview_Page**: The `/Revenue/PaymentSchedules` page showing aggregated payment schedule data
- **Payment_Schedule**: A structured plan attached to an invoice defining how the outstanding balance will be collected across multiple instalments over time
- **Instalment**: A single planned payment within a Payment Schedule, with a target amount and optional due date
- **Instalment_Status**: The current state of an instalment — one of: Pending, Due, Overdue, Paid, PartiallyPaid (computed at read time via InstalmentStatusEngine)
- **Schedule_Status**: The computed aggregate status of a Payment Schedule — one of: On Track, Has Overdue, Completed
- **KPI_Card**: A summary metric card displaying a single aggregated financial value with colour-coded accent
- **Monthly_Timeline**: A visual representation of expected payment amounts grouped by month with proportional bars
- **Active_Schedule**: A Payment Schedule where IsActive = 1
- **Currency_Symbol**: The business's configured currency symbol from BusinessProfile.CurrencySymbol
- **Schedule_Payments_Permission**: The existing user-level permission (`schedule_payments`) that gates access to this page

## Requirements

### Requirement 1: Page Access and Navigation

**User Story:** As a business user with schedule_payments permission, I want to access a dedicated Payment Schedules overview page from the sidebar navigation, so that I can monitor all active instalment plans in one place.

#### Acceptance Criteria

1. THE Portal SHALL display a "Payment Schedules" navigation link in the sidebar Finance section, positioned after the Cash Flow link
2. THE Portal SHALL display the "Payment Schedules" sidebar link only to users who have the Schedule_Payments_Permission
3. WHEN a user with Schedule_Payments_Permission navigates to `/Revenue/PaymentSchedules`, THE Portal SHALL render the Overview_Page
4. WHEN a user without Schedule_Payments_Permission attempts to access `/Revenue/PaymentSchedules`, THE Portal SHALL redirect the user to the Revenue Dashboard
5. THE Portal SHALL display the page topbar with "Revenue" as the eyebrow label and "Payment Schedules" as the heading
6. THE Portal SHALL display a subtitle reading "Monitor all instalment plans and expected payments across your invoices." below the heading

### Requirement 2: KPI Summary Cards

**User Story:** As a business user, I want to see key financial metrics at a glance, so that I can quickly understand the overall state of my payment schedules.

#### Acceptance Criteria

1. THE Overview_Page SHALL display four KPI_Card elements in a horizontal row above the Monthly_Timeline
2. THE Overview_Page SHALL display a "Total Scheduled" KPI_Card showing the sum of all Active_Schedule total amounts, with a blue accent
3. THE Overview_Page SHALL display a "Collected" KPI_Card showing the sum of all MatchedAmount values across all instalments in Active_Schedules, with a green accent
4. THE Overview_Page SHALL display a "Due This Month" KPI_Card showing the sum of instalment amounts that have a due date within the current calendar month and have Instalment_Status of Due or Overdue or Pending, with an amber accent
5. THE Overview_Page SHALL display an "Overdue" KPI_Card showing the sum of instalment amounts where Instalment_Status is Overdue, with a red accent
6. THE Overview_Page SHALL format all KPI values using the Currency_Symbol from the business's profile
7. WHEN the business has no Active_Schedules, THE Overview_Page SHALL display all KPI values as zero using the Currency_Symbol

### Requirement 3: Monthly Payment Timeline

**User Story:** As a business user, I want to see a monthly breakdown of expected payments, so that I can plan cash flow and anticipate when collections will arrive.

#### Acceptance Criteria

1. THE Overview_Page SHALL display a "Monthly Payment Plan" section with a horizontal bar chart grouped by month
2. THE Overview_Page SHALL display year selector buttons allowing the user to filter the timeline to a specific year
3. WHEN the user selects a year, THE Overview_Page SHALL display only months from that year that have at least one instalment with a due date in that month
4. THE Overview_Page SHALL display each timeline row showing: month name, a horizontal bar proportional to the expected amount, the total amount, and the instalment count for that month
5. THE Overview_Page SHALL calculate the bar width proportionally where the month with the highest total amount occupies the full bar width
6. WHEN a month contains instalments with Instalment_Status of Overdue, THE Overview_Page SHALL display that month's row with a red colour and append "(overdue)" to the month name
7. THE Overview_Page SHALL display a "No date assigned" row for instalments that have no due date set, showing the total amount and count of dateless instalments
8. WHEN the Overview_Page first loads, THE Overview_Page SHALL default to the current year in the year selector
9. THE Overview_Page SHALL only show year buttons for years that contain at least one instalment due date across all Active_Schedules

### Requirement 4: Schedule Filters

**User Story:** As a business user, I want to filter the active schedules table by status, invoice number, or customer name, so that I can quickly find specific schedules.

#### Acceptance Criteria

1. THE Overview_Page SHALL display a filter section with three filter controls: Status dropdown, Invoice text search, and Customer text search
2. THE Overview_Page SHALL provide Status filter options: All, Has Overdue, On Track, Completed
3. WHEN the user enters text in the Invoice filter field, THE Overview_Page SHALL filter the Active Schedules table to show only rows where the invoice number contains the search text (case-insensitive)
4. WHEN the user enters text in the Customer filter field, THE Overview_Page SHALL filter the Active Schedules table to show only rows where the customer name contains the search text (case-insensitive)
5. WHEN the user selects a Status filter value, THE Overview_Page SHALL filter the Active Schedules table to show only rows matching that Schedule_Status
6. WHEN the user clicks the "Filter" button, THE Overview_Page SHALL apply all active filter criteria simultaneously and refresh the table results
7. WHEN the user clicks the "Clear" button, THE Overview_Page SHALL reset all filter controls to their default values (Status: All, text fields: empty) and display all Active Schedules

### Requirement 5: Active Schedules Table

**User Story:** As a business user, I want to see a table of all active payment schedules with key details and progress indicators, so that I can review the status of each schedule without navigating to individual invoices.

#### Acceptance Criteria

1. THE Overview_Page SHALL display an Active Schedules table with columns: Invoice, Customer, Schedule Total, Paid, Remaining, Next Due, Progress, Status
2. THE Overview_Page SHALL display the Invoice column as a clickable link that navigates to the invoice detail page (`/Revenue/InvoiceDetail/{invoiceId}`)
3. THE Overview_Page SHALL display the Customer column showing the customer name associated with the invoice
4. THE Overview_Page SHALL display the Schedule Total column showing the sum of all instalment amounts for that schedule, formatted with Currency_Symbol
5. THE Overview_Page SHALL display the Paid column showing the sum of all MatchedAmount values for that schedule, formatted with Currency_Symbol
6. THE Overview_Page SHALL display the Remaining column showing the difference between Schedule Total and Paid, formatted with Currency_Symbol
7. THE Overview_Page SHALL display the Next Due column showing the due date of the earliest instalment that has Instalment_Status of Due, Overdue, or Pending (in that priority order)
8. THE Overview_Page SHALL display the Progress column as a mini progress bar with a percentage label calculated as (Paid / Schedule Total) × 100
9. THE Overview_Page SHALL display the Status column as a colour-coded badge: "On Track" (green) when no instalments are overdue, "Has Overdue" (red) when at least one instalment has Instalment_Status of Overdue, "Completed" (grey) when all instalments have Instalment_Status of Paid
10. THE Overview_Page SHALL order the table rows by Next Due date ascending, with schedules having overdue instalments shown first
11. WHEN the business has no Active_Schedules, THE Overview_Page SHALL display an empty state message within the table area

### Requirement 6: Pagination

**User Story:** As a business user, I want the schedules table to be paginated, so that the page remains performant even with many active schedules.

#### Acceptance Criteria

1. THE Overview_Page SHALL paginate the Active Schedules table with a default page size of 10 rows
2. THE Overview_Page SHALL display pagination information showing "Showing {start}-{end} of {total}" below the table
3. THE Overview_Page SHALL display page number buttons for navigation between pages
4. WHEN the user clicks a page number button, THE Overview_Page SHALL display the corresponding page of results
5. WHEN filters are applied, THE Overview_Page SHALL reset pagination to page 1 and recalculate total pages based on filtered results

### Requirement 7: Data Loading

**User Story:** As a business user, I want the page to load data efficiently, so that I see results quickly without the page freezing.

#### Acceptance Criteria

1. WHEN the Overview_Page loads, THE Portal SHALL fetch all overview data via an AJAX request using the BlockUI pattern (BlockUI.show → fetch → BlockUI.hide → render)
2. WHEN the user applies filters, THE Portal SHALL fetch filtered results via an AJAX request using the BlockUI pattern
3. WHEN the user changes the year selector in the Monthly Timeline, THE Portal SHALL update the timeline display without a full page reload
4. IF the AJAX request fails, THEN THE Portal SHALL display a SweetAlert2 error message indicating the data could not be loaded
5. THE Portal SHALL provide a controller endpoint following the AxGet naming convention to serve the overview data as JSON

### Requirement 8: Responsive Layout

**User Story:** As a business user, I want the overview page to be usable on smaller screens, so that I can check payment schedules from a tablet.

#### Acceptance Criteria

1. WHEN the viewport width is 768px or less, THE Overview_Page SHALL display the KPI cards in a 2×2 grid instead of a single row
2. WHEN the viewport width is 768px or less, THE Overview_Page SHALL hide the instalment count column in the Monthly Timeline
3. WHEN the viewport width is 768px or less, THE Overview_Page SHALL reduce the page heading size to 32px
