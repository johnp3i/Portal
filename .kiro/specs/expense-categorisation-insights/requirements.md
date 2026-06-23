# Requirements Document

## Introduction

This document defines the requirements for the Expense Categorisation Insights module — a visual analytics and budget management feature that aggregates existing Purchase and ExpenseCategory data to provide spend-by-category charts, trend analysis, budget threshold alerts, supplier breakdowns, and CSV export capabilities.

The module is gated to the Professional subscription plan using the existing `expense_insights` module key and permission infrastructure. Starter users see a soft-gate teaser on the Purchase list encouraging upgrade.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **Expense_Insights_Service**: The service layer responsible for aggregating and computing expense analytics for a given period
- **Expense_Insights_Controller**: The MVC controller handling HTTP requests for the expense insights views and exports
- **Purchase**: An expense entry in the [purchase].Purchase table representing money spent by the Business
- **ExpenseCategory**: A business-specific classification for purchases, with Id, BusinessId, Name, IsActive, and ExpenseTypeId
- **ExpenseType**: A lookup classifying expense categories as Services (1) or Goods (2)
- **PurchaseType**: A lookup classifying purchases as Asset (1), Stock (2), or Expense (3)
- **Supplier**: A vendor entity from whom Purchases are made, with Id, BusinessId, Name, IsActive
- **ExpenseCategoryLimit**: An existing table containing budget thresholds per category with AnnualLimitEur and PeriodLimitEur columns
- **Period**: A date range (start date to end date) used to scope expense calculations
- **Category_Spend**: The sum of TotalAmount for all non-cancelled Purchase records within a category and period
- **Month_Over_Month_Variance**: The percentage change in category spend between the current month and the previous month
- **Budget_Threshold_Alert**: A visual warning indicating a category has exceeded or is approaching its configured spending limit
- **Soft_Gate_Teaser**: A locked preview card shown to Starter users indicating the feature requires a plan upgrade
- **Plan_Permission_Filter**: The global authorization filter that blocks access to modules not included in the business subscription plan
- **Chart.js**: The client-side JavaScript charting library already available in the project for rendering charts

## Requirements

### Requirement 1: Expense Aggregation by Category

**User Story:** As a business owner, I want to see my expenses broken down by category for a selected period, so that I can understand where my money is going.

#### Acceptance Criteria

1. WHEN the Expense_Insights_Service computes Category_Spend for a period defined by a start date and end date, THE Expense_Insights_Service SHALL sum the TotalAmount of all non-cancelled Purchase records grouped by ExpenseCategoryId where InvoiceDate falls within the period (start date inclusive, end date inclusive) and BusinessId matches the current tenant
2. WHEN the Expense_Insights_Service computes the category breakdown, THE Expense_Insights_Service SHALL include the category name, total spend, percentage of overall spend rounded to two decimal places, and the parent ExpenseType classification (Services or Goods)
3. IF a category has a NULL ExpenseTypeId, THEN THE Expense_Insights_Service SHALL return the ExpenseType classification as "Uncategorised" for that category in the breakdown
4. WHEN the category breakdown is computed, THE Expense_Insights_Service SHALL order categories by total spend descending (largest expense first)
5. THE Expense_Insights_Service SHALL exclude purchases where IsCancelled is true from all aggregation calculations
6. THE Expense_Insights_Service SHALL include categories with IsActive equal to false in the breakdown if those categories have non-cancelled purchases within the selected period
7. IF no non-cancelled purchases exist for the selected period, THEN THE Expense_Insights_Service SHALL return an empty category breakdown with a total spend of zero and all percentage values as zero

### Requirement 2: Period-Based Filtering

**User Story:** As a business owner, I want to filter my expense insights by different time periods, so that I can analyse spending patterns across months, quarters, and years.

#### Acceptance Criteria

1. THE Expense_Insights_Service SHALL support the following predefined periods: Current Month, Previous Month, Current Quarter, Current Year, Custom date range
2. WHEN "Current Month" is selected, THE Expense_Insights_Service SHALL resolve the start date to the first day of the current calendar month and the end date to the current UTC date
3. WHEN "Previous Month" is selected, THE Expense_Insights_Service SHALL resolve the start date to the first day of the preceding calendar month and the end date to the last day of that month
4. WHEN "Current Quarter" is selected, THE Expense_Insights_Service SHALL resolve the start date to the first day of the current calendar quarter (Jan, Apr, Jul, or Oct) and the end date to the current UTC date
5. WHEN "Current Year" is selected, THE Expense_Insights_Service SHALL resolve the start date to January 1st of the current year and the end date to the current UTC date
6. WHEN a custom date range is provided, THE Expense_Insights_Service SHALL validate that the start date is before or equal to the end date and that the range does not exceed 366 days
7. IF a custom date range has a start date after the end date, THEN THE Expense_Insights_Service SHALL return a validation error indicating the date order is invalid and not compute figures
8. WHEN the period filter changes, THE Expense_Insights_Controller SHALL reload all charts, breakdown table, and summary data for the newly selected period via an AJAX request following the BlockUI flow

### Requirement 3: Expense Insights View

**User Story:** As a business owner, I want a dedicated page showing my expense analytics with charts and a breakdown table, so that I can visualise spending patterns at a glance.

#### Acceptance Criteria

1. WHEN a user navigates to the Expense Insights page, THE Expense_Insights_Controller SHALL display a period selector with the following options: Current Month (1st of current month to today), Previous Month (1st to last day of previous month), Current Quarter (1st of current quarter to today), Current Year (1st January of current year to today), and Custom date range (user-specified start and end dates where start date must not be after end date)
2. WHEN a period is selected, THE Expense_Insights_Controller SHALL display summary cards showing: Total Spend (sum of TotalAmount from non-cancelled purchases in the period, formatted to 2 decimal places), Number of Categories with Spend (count of distinct ExpenseCategoryId values appearing in non-cancelled purchases in the period), Top Category Name (the ExpenseCategory.Name with the highest sum of TotalAmount in the period), and Average Spend Per Category (Total Spend divided by Number of Categories with Spend, formatted to 2 decimal places)
3. WHEN a period is selected, THE Expense_Insights_Controller SHALL display a breakdown table listing each expense category that has at least one non-cancelled purchase in the period, with columns: Category Name, Expense Type (from ExpenseType.Name: "Services" or "Goods", or blank if unassigned), Total Spend (sum of TotalAmount for that category in the period), Percentage of Total (category Total Spend divided by overall Total Spend, displayed as a percentage to 1 decimal place), and Month-Over-Month Variance
4. WHEN the breakdown table is displayed, THE Expense_Insights_Controller SHALL order rows by Total Spend descending
5. IF no non-cancelled purchases exist for the selected period, THEN THE Expense_Insights_Controller SHALL display an empty state message indicating no expense data exists for the chosen period and hide the breakdown table and summary cards
6. IF a Custom date range is selected and the start date is after the end date, THEN THE Expense_Insights_Controller SHALL display a validation warning and not execute the query
7. IF only one month of data exists within the selected period (making a prior-month comparison impossible), THEN THE Expense_Insights_Controller SHALL display the Month-Over-Month Variance column as "N/A" for all rows

### Requirement 4: Pie and Bar Chart Visualisation

**User Story:** As a business owner, I want to see pie and bar charts showing my spend by category for the current period, so that I can quickly identify which categories dominate my expenses.

#### Acceptance Criteria

1. WHEN a period is selected and two or more categories have spend data, THE Expense_Insights_Controller SHALL render a pie chart using Chart.js displaying each category's share of total spend for the period
2. WHEN a period is selected, THE Expense_Insights_Controller SHALL render a bar chart using Chart.js displaying each category's absolute spend amount for the period, with bars sorted in descending order by spend amount
3. IF fewer than two categories have spend data for the selected period, THEN THE Expense_Insights_Controller SHALL display only the bar chart and hide the pie chart
4. IF zero categories have spend data for the selected period, THEN THE Expense_Insights_Controller SHALL display an empty-state message indicating no expense data exists for the period, and hide both charts
5. WHEN a chart segment or bar is hovered, THE Chart SHALL display a tooltip showing the category name, the spend amount formatted using the business profile currency symbol with two decimal places, and the percentage of total rounded to one decimal place
6. THE charts SHALL use the MyChair Design System colour palette for category segments, assigning colours in descending spend-amount order starting from Primary Blue and cycling through the defined accent colours

### Requirement 5: Trend Lines Over Time

**User Story:** As a business owner, I want to see how my category spending has changed over the last 6 to 12 months, so that I can identify seasonal patterns and spending trends.

#### Acceptance Criteria

1. WHEN the trend view is rendered, THE Expense_Insights_Service SHALL compute monthly totals per category for the last 12 calendar months ending at the current UTC month, by summing TotalAmount of all non-cancelled Purchase records where InvoiceDate falls within each calendar month and BusinessId matches the current tenant
2. WHEN the trend data is available, THE Expense_Insights_Controller SHALL render a line chart using Chart.js with one line per category, where the X-axis represents months (labelled as "MMM yyyy"), the Y-axis represents spend amount starting at zero, and each data point shows the monthly total for that category
3. WHEN a category has zero spend in a given month, THE Expense_Insights_Service SHALL include that month as zero in the trend data (no gaps in the line)
4. THE Expense_Insights_Controller SHALL limit the trend chart to the top 5 categories by total spend over the 12-month window; categories with zero total spend across the entire 12-month window SHALL be excluded from the chart
5. WHEN the business has non-cancelled purchases in fewer than 2 distinct calendar months (determined by distinct year-month values of InvoiceDate), THE Expense_Insights_Controller SHALL display a message indicating insufficient data for trend analysis instead of rendering the chart

### Requirement 6: Budget Configuration

**User Story:** As a business owner, I want to configure monthly spending limits for my expense categories, so that I can set budgets and be warned when approaching or exceeding them.

#### Acceptance Criteria

1. WHEN a user navigates to the budget configuration section, THE Expense_Insights_Controller SHALL display a list of all active expense categories for the business with their current PeriodLimitEur value from the ExpenseCategoryLimit table, showing categories that have no existing ExpenseCategoryLimit record with an empty budget field
2. WHEN a user sets a budget limit for a category that has no existing ExpenseCategoryLimit record, THE Expense_Insights_Controller SHALL create a new ExpenseCategoryLimit record with the PeriodLimitEur value for that category and business
3. WHEN a user updates a budget limit for a category that already has an ExpenseCategoryLimit record, THE Expense_Insights_Controller SHALL update the existing PeriodLimitEur value for that record
4. WHEN a user clears a budget limit for a category, THE Expense_Insights_Controller SHALL set the PeriodLimitEur value to null, indicating no budget limit
5. THE Expense_Insights_Controller SHALL validate that budget limit values are positive decimal numbers greater than zero and not exceeding 999,999,999.99
6. IF a user provides a non-positive, non-numeric, or out-of-range budget value, THEN THE Expense_Insights_Controller SHALL return a validation error indicating the value must be between 0.01 and 999,999,999.99 and SHALL NOT persist the invalid value
7. IF the save operation fails due to a server error, THEN THE Expense_Insights_Controller SHALL return an error response and the previously stored budget value SHALL remain unchanged

### Requirement 7: Budget Threshold Alerts

**User Story:** As a business owner, I want to be visually warned when a category's spending has exceeded or is approaching its budget limit, so that I can take corrective action before overspending.

#### Acceptance Criteria

1. WHEN the selected period's Category_Spend reaches or exceeds 100 percent of the configured PeriodLimitEur for that category, THE Expense_Insights_Controller SHALL display an exceeded-budget alert indicator with danger styling next to the category in the breakdown table, showing the current spend and the configured limit value
2. WHEN the selected period's Category_Spend reaches 80 percent or more of the configured PeriodLimitEur but remains below 100 percent, THE Expense_Insights_Controller SHALL display an approaching-budget alert indicator with warning styling next to the category in the breakdown table, showing the current spend and the configured limit value
3. WHEN no PeriodLimitEur is configured for a category, THE Expense_Insights_Controller SHALL not display any budget alert indicator for that category
4. WHEN the Expense Insights page loads or the period filter changes, THE Expense_Insights_Controller SHALL display a summary count of categories that have exceeded their budget and a separate count of categories approaching their budget, scoped to the same selected period used in the breakdown table
5. THE budget threshold alerts SHALL be visual indicators within the page only — no email notifications, push notifications, or external alerts are generated
6. WHEN computing budget threshold status, THE Expense_Insights_Controller SHALL use the same Category_Spend calculation defined in Requirement 1 (sum of TotalAmount for non-cancelled Purchase records within the category, filtered by BusinessId and the selected period's date range)

### Requirement 8: Top Suppliers per Category

**User Story:** As a business owner, I want to see which suppliers account for the most spending within each category, so that I can negotiate better terms or diversify my supply chain.

#### Acceptance Criteria

1. WHEN the category breakdown is displayed, THE Expense_Insights_Service SHALL compute the top 3 suppliers ranked by descending sum of TotalAmount within each category for the selected period, using SupplierId to break ties (lower SupplierId first)
2. WHEN a user expands a category row in the breakdown table, THE Expense_Insights_Controller SHALL display the top suppliers for that category showing supplier name, total spend (sum of TotalAmount), and percentage of category spend computed as (supplier spend / total non-cancelled spend in that category for the period) × 100 rounded to one decimal place
3. THE Expense_Insights_Service SHALL only include purchases where IsCancelled is false when computing supplier totals
4. IF a category has fewer than 3 suppliers with non-cancelled purchases in the period, THEN THE Expense_Insights_Service SHALL return only the suppliers that have spend data without padding or placeholder entries
5. WHEN computing top suppliers, THE Expense_Insights_Service SHALL filter by BusinessId using the current tenant to ensure tenant isolation
6. IF a category has zero total non-cancelled spend in the selected period, THEN THE Expense_Insights_Service SHALL return an empty supplier list for that category and the expand action SHALL display a message indicating no supplier data is available

### Requirement 9: Month-Over-Month Variance Highlighting

**User Story:** As a business owner, I want to see how each category's spending has changed compared to the previous month, so that I can quickly spot unusual increases or decreases.

#### Acceptance Criteria

1. WHEN the breakdown table is rendered for a selected month, THE Expense_Insights_Service SHALL compute the Month_Over_Month_Variance as ((current_month_spend - previous_month_spend) / previous_month_spend) multiplied by 100 for each category, where "previous month" is the calendar month immediately preceding the selected month, and the result SHALL be rounded to one decimal place
2. WHEN the variance is positive, THE Expense_Insights_Controller SHALL display the percentage with an upward arrow indicator using warning styling (increase in spending)
3. WHEN the variance is negative, THE Expense_Insights_Controller SHALL display the percentage with a downward arrow indicator using success styling (decrease in spending)
4. IF the previous month has zero spend for a category but the current month has spend, THEN THE Expense_Insights_Service SHALL display "New" instead of a percentage
5. IF the current month has zero spend for a category but the previous month had spend, THEN THE Expense_Insights_Service SHALL display "-100%" with a downward arrow indicator
6. IF both the current month and the previous month have zero spend for a category, THEN THE Expense_Insights_Service SHALL display a dash character ("—") with no directional indicator
7. IF no previous month data exists for the business (the selected month is the earliest month with recorded expenses), THEN THE Expense_Insights_Service SHALL display "N/A" in the variance column with no directional indicator

### Requirement 10: CSV Export

**User Story:** As a business owner, I want to export my category expense breakdown as a CSV file, so that I can import the data into spreadsheets or share it with my accountant.

#### Acceptance Criteria

1. WHEN a user clicks the CSV export button, THE Expense_Insights_Controller SHALL generate a UTF-8 encoded, comma-delimited CSV file containing one row per category that has at least one expense recorded in the currently selected period
2. THE CSV export SHALL include the following columns: Category Name (text), Expense Type (text), Total Spend (numeric, 2 decimal places, no currency symbol), Percentage of Total (numeric, 1 decimal place, no % symbol), Month-Over-Month Variance (numeric, 2 decimal places, negative values prefixed with minus sign), Budget Limit (numeric, 2 decimal places, or empty if no limit set), Budget Status (text)
3. WHEN the CSV is generated, THE Expense_Insights_Controller SHALL return it as a downloadable file with Content-Disposition attachment and a filename in the format "ExpenseInsights_[BusinessName]_[StartDate]_[EndDate].csv" where dates use YYYYMMDD format and BusinessName has spaces replaced with underscores and special characters removed
4. THE CSV export SHALL include a header row as the first line of the file with column names matching the defined column list exactly
5. WHEN budget alert data is available for a category, THE CSV SHALL include the Budget Status column with values: "Exceeded", "Approaching", "Within Limit", or "No Limit" for each category, where "No Limit" is used when no budget limit has been configured for that category
6. IF the selected period contains no expense data, THEN THE Expense_Insights_Controller SHALL return an empty CSV file containing only the header row
7. IF no period is currently selected, THEN THE Expense_Insights_Controller SHALL default to the current calendar month as the export period

### Requirement 11: Plan Permission Gating

**User Story:** As a platform operator, I want the Expense Insights module restricted to Professional plan subscribers, so that the feature is monetised appropriately within the subscription tier system.

#### Acceptance Criteria

1. WHEN a user whose business is subscribed to the Starter plan attempts to access the Expense_Insights_Controller, THE Plan_Permission_Filter SHALL block the request and return the PlanSoftGate view with the module display name set to "Expense Insights" and the required plan name set to "Professional"
2. WHEN a user whose business is subscribed to the Professional or Enterprise plan accesses the Expense_Insights_Controller, THE Plan_Permission_Filter SHALL allow the request to proceed, verifying the `expense_insights` module key exists in the business's plan at access level `full`
3. THE Expense_Insights_Controller SHALL use the `[ModuleAccess(PortalModules.ExpenseInsights)]` attribute to register itself with the permission infrastructure
4. THE PlanFeature seed data SHALL include the `expense_insights` module key with access level `full` for the Professional and Enterprise plans, and SHALL NOT include it for the Starter plan

### Requirement 12: Purchase List Soft-Gate Teaser

**User Story:** As a Starter plan user, I want to see a teaser of the Expense Insights feature on my Purchase list, so that I am aware of the feature and motivated to upgrade.

#### Acceptance Criteria

1. WHILE the current business is on the Starter plan, THE Purchase list page SHALL display a locked Expense Insights teaser card below the purchase data table, containing a lock icon, the heading "Expense Insights", a one-sentence value description of the analytics module, and a call-to-action link labelled "Learn More"
2. WHEN a Starter user clicks the Expense Insights teaser card or its call-to-action link, THE Purchase list page SHALL navigate to the PlanSoftGate view passing module key `expense_insights` so that the existing soft-gate upgrade page is displayed
3. WHILE the current business is on the Professional or Enterprise plan, THE Purchase list page SHALL not render the Expense Insights teaser card in the DOM
4. THE Purchase list page SHALL determine the current business plan by querying the existing ISubscriptionPlanService; IF the plan check service is unavailable or the subscription has no active plan, THEN the teaser card SHALL not be displayed

### Requirement 13: Tenant Isolation

**User Story:** As a platform operator, I want all expense insights data scoped to the current business, so that no business can see another business's expense data.

#### Acceptance Criteria

1. THE Expense_Insights_Service SHALL include a WHERE BusinessId = @BusinessId filter on every Purchase query, including aggregate computations (sums, counts, grouped breakdowns)
2. THE Expense_Insights_Service SHALL include a WHERE BusinessId = @BusinessId filter on every ExpenseCategory query, including aggregate computations (sums, counts, grouped breakdowns)
3. THE Expense_Insights_Service SHALL include a WHERE BusinessId = @BusinessId filter on every Supplier query, including aggregate computations (sums, counts, grouped breakdowns)
4. WHEN the Expense_Insights_Service resolves the current tenant, THE Expense_Insights_Service SHALL obtain the BusinessId exclusively from ICurrentTenantService.CurrentBusinessId and SHALL NOT accept a BusinessId as an input parameter from callers
5. IF ICurrentTenantService.CurrentBusinessId returns 0, THEN THE Expense_Insights_Service SHALL return an empty result set without executing any database query

### Requirement 14: Mobile Responsiveness

**User Story:** As a business owner using a mobile device, I want the expense insights page to be fully usable on smaller screens, so that I can review my spending analytics on the go.

#### Acceptance Criteria

1. WHEN the viewport width is 375px, THE Expense Insights page SHALL display all content without horizontal page-level overflow, all interactive elements (buttons, dropdowns, links) SHALL have a minimum touch target of 44×44px, and all text SHALL remain readable without requiring horizontal scrolling or zooming
2. WHEN the viewport width is 810px, THE Expense Insights page SHALL display all content without horizontal page-level overflow, all interactive elements SHALL have a minimum touch target of 44×44px, and all text SHALL remain readable without requiring horizontal scrolling or zooming
3. WHEN the viewport width is below 768px, THE Expense Insights page layout SHALL stack chart visualisations vertically in a single column rather than displaying them side by side
4. WHEN the viewport width is below 768px, THE breakdown table SHALL use horizontal scrolling within its container to accommodate all columns without truncating cell data
5. THE period selector and export controls SHALL remain visible without scrolling past the fold and SHALL maintain a minimum touch target of 44×44px at viewport widths of 375px, 768px, and 810px
6. WHILE the viewport width is between 375px and 810px inclusive, THE Expense Insights page SHALL not display any content clipped by overflow:hidden or obscured behind other elements
