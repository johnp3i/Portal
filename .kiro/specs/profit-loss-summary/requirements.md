# Requirements Document

## Introduction

This document defines the requirements for the Profit & Loss Summary module — a period-based financial reporting feature that computes Revenue, Cost of Goods Sold (COGS), Operating Expenses, Gross Profit, and Net Profit for a business. The module aggregates existing Payment records (revenue) and Purchase records (expenses), classified by PurchaseType and ExpenseCategory, to produce a standard P&L statement with trend comparison and PDF export.

The module is gated to the Professional subscription plan using the existing `pnl` module key and permission infrastructure. Starter users see a soft-gate teaser on the Dashboard encouraging upgrade.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **P&L_Service**: The service layer responsible for computing Profit & Loss figures for a given period
- **P&L_Controller**: The MVC controller handling HTTP requests for the P&L views and exports
- **Payment**: A monetary transaction recorded against an Invoice in the [revenue].Payment table; represents cash received
- **Purchase**: An expense entry in the [purchase].Purchase table representing money spent by the Business
- **PurchaseType**: A lookup classifying purchases as Asset (1), Stock (2), or Expense (3)
- **ExpenseCategory**: A business-specific classification for purchases, linked to an ExpenseType
- **ExpenseType**: A lookup classifying expense categories as Services (1) or Goods (2)
- **Revenue**: The sum of non-voided Payment amounts received within a given period
- **COGS**: Cost of Goods Sold — the sum of non-cancelled Purchase amounts where PurchaseTypeId is Stock (2)
- **Operating_Expenses**: The sum of non-cancelled Purchase amounts where PurchaseTypeId is Expense (3)
- **Gross_Profit**: Revenue minus COGS
- **Net_Profit**: Gross_Profit minus Operating_Expenses
- **Gross_Margin**: Gross_Profit divided by Revenue, expressed as a percentage
- **Net_Margin**: Net_Profit divided by Revenue, expressed as a percentage
- **Period**: A date range (start date to end date) used to scope financial calculations
- **Trend_Comparison**: A comparison of the current period's figures against the same period in the previous year
- **Soft_Gate_Teaser**: A locked preview card shown to Starter users indicating the feature requires a plan upgrade
- **Plan_Permission_Filter**: The global authorization filter that blocks access to modules not included in the business subscription plan

## Requirements

### Requirement 1: P&L Computation Logic

**User Story:** As a business owner, I want the system to compute my Profit & Loss figures from existing payment and purchase data, so that I can understand my financial performance without manual calculations.

#### Acceptance Criteria

1. WHEN the P&L_Service computes Revenue for a period, THE P&L_Service SHALL sum the Amount of all non-voided Payment records where PaymentDateUtc falls within the period and BusinessId matches the current tenant
2. WHEN the P&L_Service computes COGS for a period, THE P&L_Service SHALL sum the TotalAmount of all non-cancelled Purchase records where PurchaseTypeId equals 2 (Stock), InvoiceDate falls within the period, and BusinessId matches the current tenant
3. WHEN the P&L_Service computes Operating_Expenses for a period, THE P&L_Service SHALL sum the TotalAmount of all non-cancelled Purchase records where PurchaseTypeId equals 3 (Expense), InvoiceDate falls within the period, and BusinessId matches the current tenant
4. WHEN the P&L_Service computes Gross_Profit, THE P&L_Service SHALL calculate Revenue minus COGS
5. WHEN the P&L_Service computes Net_Profit, THE P&L_Service SHALL calculate Gross_Profit minus Operating_Expenses
6. WHEN the P&L_Service computes Gross_Margin, THE P&L_Service SHALL calculate (Gross_Profit divided by Revenue) multiplied by 100, returning 0 when Revenue is zero
7. WHEN the P&L_Service computes Net_Margin, THE P&L_Service SHALL calculate (Net_Profit divided by Revenue) multiplied by 100, returning 0 when Revenue is zero

### Requirement 2: Period-Based Calculations

**User Story:** As a business owner, I want to view my P&L for different time periods, so that I can analyse financial performance across months, quarters, and years.

#### Acceptance Criteria

1. THE P&L_Service SHALL support the following predefined periods: Current Month, Previous Month, Current Quarter, Current Year
2. THE P&L_Service SHALL support a custom date range period where the user specifies a start date and end date
3. WHEN a predefined period is selected, THE P&L_Service SHALL resolve the start and end dates based on the current UTC date
4. WHEN a custom date range is provided, THE P&L_Service SHALL validate that the start date is before or equal to the end date
5. IF a custom date range has a start date after the end date, THEN THE P&L_Service SHALL return a validation error and not compute figures

### Requirement 3: P&L Summary View

**User Story:** As a business owner, I want to see my P&L figures displayed as summary cards with a breakdown table, so that I can quickly assess financial health and drill into expense details.

#### Acceptance Criteria

1. WHEN a user navigates to the P&L page, THE P&L_Controller SHALL display a period selector allowing the user to choose Current Month, Previous Month, Current Quarter, Current Year, or Custom date range
2. WHEN a period is selected, THE P&L_Controller SHALL display summary cards for Revenue, COGS, Gross Profit, Operating Expenses, and Net Profit with their respective monetary values
3. WHEN summary cards are displayed, THE P&L_Controller SHALL show Gross_Margin percentage on the Gross Profit card and Net_Margin percentage on the Net Profit card
4. WHEN a period is selected, THE P&L_Controller SHALL display a breakdown table showing each ExpenseCategory with its total amount and percentage of total expenses
5. WHEN the breakdown table is displayed, THE P&L_Controller SHALL group line items by PurchaseType (COGS section and Operating Expenses section)
6. IF Revenue, COGS, and Operating_Expenses are all zero for the selected period, THEN THE P&L_Controller SHALL display an empty state message indicating no financial data exists for the period

### Requirement 4: Trend Comparison

**User Story:** As a business owner, I want to compare my current P&L figures against the same period last year, so that I can identify growth trends and areas of concern.

#### Acceptance Criteria

1. WHEN a period is selected, THE P&L_Service SHALL compute the equivalent period from the previous year (same date range shifted back by one year)
2. WHEN trend comparison data is available, THE P&L_Controller SHALL display the percentage change for Revenue, COGS, Gross Profit, Operating Expenses, and Net Profit compared to the same period last year
3. WHEN a percentage change is positive, THE P&L_Controller SHALL display the change with an upward indicator; WHEN negative, THE P&L_Controller SHALL display the change with a downward indicator
4. IF no data exists for the comparison period (same period last year), THEN THE P&L_Controller SHALL display a message indicating no comparison data is available instead of showing zero-percent change

### Requirement 5: PDF Export

**User Story:** As a business owner, I want to export my P&L statement as a PDF, so that I can share it with accountants, investors, or for record keeping.

#### Acceptance Criteria

1. WHEN a user clicks the export button on the P&L page, THE P&L_Controller SHALL generate a PDF document containing the full P&L statement for the currently selected period
2. THE PDF export SHALL include: business name, period dates, Revenue, COGS, Gross Profit, Gross Margin, Operating Expenses, Net Profit, Net Margin, and the expense category breakdown table
3. WHEN the PDF is generated, THE P&L_Controller SHALL return it as a downloadable file with a filename in the format "PnL_[BusinessName]_[StartDate]_[EndDate].pdf"
4. THE PDF export SHALL include trend comparison figures when comparison data is available for the selected period

### Requirement 6: Plan Permission Gating

**User Story:** As a platform operator, I want the P&L module restricted to Professional plan subscribers, so that the feature is monetised appropriately within the subscription tier system.

#### Acceptance Criteria

1. WHEN a user on the Starter plan attempts to access the P&L_Controller, THE Plan_Permission_Filter SHALL block access and display the soft-gate upgrade view indicating the Professional plan is required
2. WHEN a user on the Professional or Enterprise plan accesses the P&L_Controller, THE Plan_Permission_Filter SHALL allow access using the existing `pnl` module key
3. THE P&L_Controller SHALL use the `[ModuleAccess(PortalModules.Pnl)]` attribute to register itself with the permission infrastructure

### Requirement 7: Dashboard Soft-Gate Teaser

**User Story:** As a Starter plan user, I want to see a teaser of the P&L feature on my Dashboard, so that I am aware of the feature and motivated to upgrade.

#### Acceptance Criteria

1. WHILE the current business is on the Starter plan, THE Dashboard SHALL display a locked P&L teaser card showing a preview of what the P&L module provides
2. WHEN a Starter user clicks the P&L teaser card, THE Dashboard SHALL navigate to the soft-gate upgrade view for the pnl module
3. WHILE the current business is on the Professional or Enterprise plan, THE Dashboard SHALL not display the P&L teaser card

### Requirement 8: Tenant Isolation

**User Story:** As a platform operator, I want all P&L data scoped to the current business, so that no business can see another business's financial data.

#### Acceptance Criteria

1. THE P&L_Service SHALL filter all Payment queries by the current tenant's BusinessId
2. THE P&L_Service SHALL filter all Purchase queries by the current tenant's BusinessId
3. WHEN the P&L_Service resolves the current tenant, THE P&L_Service SHALL use the existing ICurrentTenantService to obtain the BusinessId

### Requirement 9: Expense Category Breakdown

**User Story:** As a business owner, I want to see which expense categories contribute most to my costs, so that I can identify areas to reduce spending.

#### Acceptance Criteria

1. WHEN the P&L_Service computes the expense breakdown, THE P&L_Service SHALL group non-cancelled purchases by ExpenseCategory and calculate the sum of TotalAmount per category within the period
2. WHEN the breakdown is displayed, THE P&L_Controller SHALL show each category's name, total amount, and percentage contribution to total expenses (COGS + Operating Expenses combined)
3. WHEN the breakdown is displayed, THE P&L_Controller SHALL order categories by total amount descending (largest expense first)
4. THE P&L_Service SHALL include the ExpenseCategory name and its parent ExpenseType classification (Services or Goods) in the breakdown result
