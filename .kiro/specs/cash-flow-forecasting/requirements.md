# Requirements Document

## Introduction

This document defines the requirements for the Cash Flow Forecasting module — a forward-looking financial projection feature that provides business owners with visibility into their future cash position. The module computes projected inflows from outstanding invoices (weighted by customer payment reliability), projected outflows from historical expense averages, and renders a running balance line chart over 30, 60, or 90 days.

Key capabilities include a configurable starting balance, alert threshold for low-balance warnings, scenario modelling (toggle individual invoices out of the projection), and a compact dashboard widget showing the next 30 days. The projection is computed on-demand per request using live data from the existing Invoice, Payment, Purchase, and Customer tables.

The module is gated to the Professional subscription plan using the existing `cashflow` module key and permission infrastructure. Starter users see a soft-gate teaser card on the Revenue Dashboard encouraging upgrade.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **CashFlow_Service**: The service layer responsible for computing cash flow projections for a given business
- **CashFlow_Controller**: The MVC controller handling HTTP requests for the Cash Flow views and configuration
- **CashFlow_Settings**: A per-business configuration record storing the starting bank balance and alert threshold
- **Starting_Balance**: The current bank balance entered by the business owner as the baseline for all projections
- **Alert_Threshold**: A configurable minimum balance value; when the projected balance drops below this amount, a visual warning is triggered
- **Projection_Horizon**: The number of days into the future the projection covers (30, 60, or 90 days)
- **Projected_Inflow**: Expected cash received from outstanding invoices, positioned at their adjusted due dates
- **Projected_Outflow**: Expected cash spent, derived from the historical monthly average of purchases per expense category
- **Confidence_Weight**: An adjustment factor per customer representing the average number of days late that customer pays, applied to shift the projected payment date
- **Days_Late_Average**: The mean difference in days between DueDate and actual PaymentDateUtc across a customer's paid invoices (minimum zero)
- **Adjusted_Due_Date**: The invoice DueDate plus the customer's Days_Late_Average, representing when payment is realistically expected
- **Running_Balance**: The cumulative cash position calculated as Starting_Balance plus cumulative inflows minus cumulative outflows over time
- **Scenario_Exclusion**: A session-scoped toggle that removes a specific invoice from the projection to model "what if this invoice is not paid"
- **Outstanding_Invoice**: An invoice with InvoiceFinancialStatusTypeId of 1 (Unpaid), 2 (PartiallyPaid), or 4 (Overdue)
- **Outstanding_Amount**: For Unpaid/Overdue invoices: TotalAmount; for PartiallyPaid invoices: TotalAmount minus the sum of non-voided payments
- **Historical_Period**: The lookback window (6 months) used to calculate average monthly outflows per expense category
- **Dashboard_Widget**: A compact mini-chart on the Home Dashboard showing the next 30 days of projected cash position
- **Soft_Gate_Teaser**: A locked preview card shown to Starter users indicating the feature requires a plan upgrade
- **Plan_Permission_Filter**: The global authorization filter that blocks access to modules not included in the business subscription plan

## Requirements

### Requirement 1: Cash Flow Settings Configuration

**User Story:** As a business owner, I want to configure my current bank balance and a minimum balance alert threshold, so that projections start from an accurate baseline and I am warned when cash is projected to run low.

#### Acceptance Criteria

1. THE CashFlow_Settings SHALL store a Starting_Balance (decimal), Alert_Threshold (decimal), and LastUpdatedUtc (datetime) per Business
2. WHEN a user navigates to the Cash Flow settings page, THE CashFlow_Controller SHALL display the current Starting_Balance and Alert_Threshold values, or defaults of 0.00 if no settings exist
3. WHEN a user submits updated settings, THE CashFlow_Controller SHALL validate that Starting_Balance is a non-negative decimal value
4. WHEN a user submits updated settings, THE CashFlow_Controller SHALL validate that Alert_Threshold is a non-negative decimal value
5. WHEN valid settings are submitted, THE CashFlow_Service SHALL persist the Starting_Balance, Alert_Threshold, and set LastUpdatedUtc to the current UTC time
6. IF Starting_Balance or Alert_Threshold is negative, THEN THE CashFlow_Controller SHALL return a validation error and not persist the values

### Requirement 2: Projected Inflows Calculation

**User Story:** As a business owner, I want to see projected cash inflows from my outstanding invoices positioned by their expected payment dates, so that I can anticipate when money will arrive.

#### Acceptance Criteria

1. WHEN the CashFlow_Service computes projected inflows, THE CashFlow_Service SHALL query all Outstanding_Invoices (InvoiceFinancialStatusTypeId in 1, 2, 4) for the current Business
2. WHEN the CashFlow_Service computes the inflow amount for an Unpaid or Overdue invoice, THE CashFlow_Service SHALL use the invoice TotalAmount as the projected inflow
3. WHEN the CashFlow_Service computes the inflow amount for a PartiallyPaid invoice, THE CashFlow_Service SHALL calculate TotalAmount minus the sum of non-voided Payment amounts linked to that invoice
4. WHEN the CashFlow_Service positions an inflow on the timeline, THE CashFlow_Service SHALL use the Adjusted_Due_Date (DueDate plus the customer's Days_Late_Average)
5. WHEN an Adjusted_Due_Date falls before today, THE CashFlow_Service SHALL position that inflow on today's date (overdue payments are projected as arriving today)
6. THE CashFlow_Service SHALL only include inflows whose Adjusted_Due_Date falls within the selected Projection_Horizon

### Requirement 3: Customer Confidence Weighting

**User Story:** As a business owner, I want the system to adjust projected payment dates based on each customer's actual payment behaviour, so that projections reflect reality rather than optimistic due dates.

#### Acceptance Criteria

1. WHEN the CashFlow_Service computes the Days_Late_Average for a customer, THE CashFlow_Service SHALL calculate the mean of (PaymentDateUtc minus DueDate) in days across all non-voided payments for that customer's invoices within the current Business
2. WHEN a payment was made before the DueDate, THE CashFlow_Service SHALL treat the days-late value as zero for that payment (early payments do not reduce the average below zero)
3. IF a customer has no payment history (no paid invoices), THEN THE CashFlow_Service SHALL use a Days_Late_Average of zero (trust the DueDate as-is)
4. THE CashFlow_Service SHALL round the Days_Late_Average to the nearest whole number when computing the Adjusted_Due_Date

### Requirement 4: Projected Outflows Calculation

**User Story:** As a business owner, I want to see projected cash outflows based on my historical spending patterns, so that I can anticipate recurring expenses.

#### Acceptance Criteria

1. WHEN the CashFlow_Service computes projected outflows, THE CashFlow_Service SHALL calculate the average monthly TotalAmount per ExpenseCategory from non-cancelled purchases in the last 6 months for the current Business
2. WHEN the CashFlow_Service distributes monthly outflows across the projection, THE CashFlow_Service SHALL spread each category's monthly average evenly across the days of each projected month
3. IF a category has fewer than 2 months of purchase data in the Historical_Period, THEN THE CashFlow_Service SHALL exclude that category from the outflow projection (insufficient data for a meaningful average)
4. THE CashFlow_Service SHALL include all ExpenseCategories with sufficient data regardless of PurchaseType (both Stock and Expense purchases contribute to outflows)

### Requirement 5: Running Balance and Projection Chart

**User Story:** As a business owner, I want to see a line chart showing my projected cash position over time, so that I can visualise when cash might run low or when large inflows are expected.

#### Acceptance Criteria

1. WHEN the CashFlow_Service computes the running balance, THE CashFlow_Service SHALL start from the configured Starting_Balance and add daily net cash flow (inflows minus outflows) for each day in the Projection_Horizon
2. WHEN the CashFlow_Controller renders the projection chart, THE CashFlow_Controller SHALL display a line chart with the x-axis representing days and the y-axis representing the running balance in the business currency
3. WHEN a user selects a Projection_Horizon, THE CashFlow_Controller SHALL allow selection of 30, 60, or 90 days
4. WHEN the running balance crosses below the Alert_Threshold on any day, THE CashFlow_Controller SHALL highlight that region of the chart with a visual warning (shaded danger zone)
5. WHEN the chart is rendered, THE CashFlow_Controller SHALL display the Alert_Threshold as a horizontal reference line on the chart
6. THE CashFlow_Controller SHALL display the projected balance at day 30, day 60, and day 90 as labelled data points on the chart

### Requirement 6: Inflow and Outflow Breakdown Views

**User Story:** As a business owner, I want to see which invoices contribute to inflows and which expense categories contribute to outflows, so that I can understand what drives my cash position.

#### Acceptance Criteria

1. WHEN a user views the inflow breakdown, THE CashFlow_Controller SHALL display a list of outstanding invoices showing: customer name, invoice number, outstanding amount, original due date, adjusted due date, and days-late average for that customer
2. WHEN a user views the outflow breakdown, THE CashFlow_Controller SHALL display a list of expense categories showing: category name, average monthly amount, and the number of months of data used in the calculation
3. WHEN the inflow breakdown is displayed, THE CashFlow_Controller SHALL order invoices by Adjusted_Due_Date ascending (soonest expected payment first)
4. WHEN the outflow breakdown is displayed, THE CashFlow_Controller SHALL order categories by average monthly amount descending (largest expense first)

### Requirement 7: Scenario Modelling

**User Story:** As a business owner, I want to toggle individual invoices out of the projection to see the impact on my cash position, so that I can plan for "what if this customer doesn't pay" scenarios.

#### Acceptance Criteria

1. WHEN a user toggles an invoice out of the projection, THE CashFlow_Controller SHALL exclude that invoice's projected inflow from the running balance calculation and re-render the chart
2. WHEN a user toggles an excluded invoice back into the projection, THE CashFlow_Controller SHALL include that invoice's projected inflow in the running balance calculation and re-render the chart
3. THE CashFlow_Controller SHALL visually indicate which invoices are currently excluded from the projection in the inflow breakdown list
4. THE CashFlow_Controller SHALL maintain scenario exclusions in the browser session only; exclusions are not persisted to the database
5. WHEN the page is reloaded, THE CashFlow_Controller SHALL reset all scenario exclusions to their default state (all invoices included)

### Requirement 8: Dashboard Widget

**User Story:** As a business owner, I want to see a compact cash flow mini-chart on my Home Dashboard, so that I get a quick glance at my 30-day cash position without navigating to the full module.

#### Acceptance Criteria

1. WHILE the current business is on the Professional or Enterprise plan and has configured a Starting_Balance, THE Dashboard SHALL display a compact cash flow widget showing a mini-line-chart of the next 30 days running balance
2. WHEN the dashboard widget is rendered, THE Dashboard SHALL display the current projected balance at day 30 as a numeric value below the chart
3. WHEN the projected balance drops below the Alert_Threshold within the next 30 days, THE Dashboard widget SHALL display a warning indicator with the date the threshold is first breached
4. WHEN a user clicks the dashboard widget, THE Dashboard SHALL navigate to the full Cash Flow Forecasting page
5. IF the business has not configured a Starting_Balance (no CashFlow_Settings record exists), THEN THE Dashboard widget SHALL display a setup prompt directing the user to configure their starting balance

### Requirement 9: Plan Permission Gating

**User Story:** As a platform operator, I want the Cash Flow module restricted to Professional plan subscribers, so that the feature is monetised appropriately within the subscription tier system.

#### Acceptance Criteria

1. WHEN a user on the Starter plan attempts to access the CashFlow_Controller, THE Plan_Permission_Filter SHALL block access and display the soft-gate upgrade view indicating the Professional plan is required
2. WHEN a user on the Professional or Enterprise plan accesses the CashFlow_Controller, THE Plan_Permission_Filter SHALL allow access using the existing `cashflow` module key
3. THE CashFlow_Controller SHALL use the `[ModuleAccess(PortalModules.Cashflow)]` attribute to register itself with the permission infrastructure

### Requirement 10: Soft-Gate Teaser for Starter Users

**User Story:** As a Starter plan user, I want to see a teaser of the Cash Flow feature on the Revenue Dashboard, so that I am aware of the feature and motivated to upgrade.

#### Acceptance Criteria

1. WHILE the current business is on the Starter plan, THE Revenue Dashboard SHALL display a locked Cash Flow teaser card showing a brief description of what the module provides
2. WHEN a Starter user clicks the Cash Flow teaser card, THE Revenue Dashboard SHALL navigate to the soft-gate upgrade view for the cashflow module
3. WHILE the current business is on the Professional or Enterprise plan, THE Revenue Dashboard SHALL not display the Cash Flow teaser card

### Requirement 11: Tenant Isolation

**User Story:** As a platform operator, I want all cash flow data scoped to the current business, so that no business can see another business's financial projections.

#### Acceptance Criteria

1. THE CashFlow_Service SHALL filter all Invoice queries by the current tenant's BusinessId
2. THE CashFlow_Service SHALL filter all Payment queries by the current tenant's BusinessId
3. THE CashFlow_Service SHALL filter all Purchase queries by the current tenant's BusinessId
4. THE CashFlow_Service SHALL filter CashFlow_Settings queries by the current tenant's BusinessId
5. WHEN the CashFlow_Service resolves the current tenant, THE CashFlow_Service SHALL use the existing ICurrentTenantService to obtain the BusinessId

### Requirement 12: On-Demand Computation

**User Story:** As a business owner, I want the cash flow projection computed from live data each time I view it, so that I always see the most current picture without stale cached data.

#### Acceptance Criteria

1. WHEN a user requests the cash flow projection, THE CashFlow_Service SHALL compute the projection from current Invoice, Payment, Purchase, and CashFlow_Settings data at the time of the request
2. THE CashFlow_Service SHALL not cache or persist projection results between requests
3. WHEN underlying data changes (new payment recorded, new invoice created, purchase added), THE CashFlow_Service SHALL reflect those changes in the next projection request without manual refresh or cache invalidation
