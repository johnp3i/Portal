# Requirements Document

## Introduction

Upgrade the Portal home dashboard from a quotation-only view to a comprehensive operational dashboard. The current dashboard displays quotation KPIs (Draft, Sent, Accepted, Active Customers) and a recent quotations table. The upgraded dashboard adds revenue KPIs, financial charts, invoice/payment tables, VAT summary, and top customer rankings — providing a single-screen operational overview for business owners.

The Quick Actions row is already implemented and excluded from this specification.

## Glossary

- **Dashboard**: The Portal home page (`/Home/Index`) that displays aggregated operational data for the authenticated tenant
- **Dashboard_Service**: The service layer component responsible for computing KPI aggregates, chart data, and summary tables scoped to a BusinessId
- **Revenue_KPI_Section**: A row of four KPI cards showing Revenue This Month, Outstanding Amount, Overdue Amount, and Expenses This Month
- **Revenue_Chart**: A bar chart displaying monthly revenue versus expenses for the last 6 calendar months
- **Invoice_Status_Chart**: A donut chart showing the distribution of invoices by financial status (Paid, Partial, Unpaid, Overdue)
- **Recent_Invoices_Table**: A table displaying the 5 most recently issued invoices with status pills
- **Overdue_Invoices_Table**: A table displaying invoices past their due date with a warning banner summarising total overdue
- **Recent_Payments_Table**: A table displaying the latest payments received with payment method pills
- **VAT_Summary_Section**: A panel showing Output VAT, Input VAT, and Net Payable for the current VAT period
- **Top_Customers_Section**: A ranked table of the top 5 customers by total invoiced amount
- **Tenant**: The authenticated business identified by BusinessId; all data queries are scoped to this identifier
- **Currency_Symbol**: The tenant's configured currency symbol from BusinessProfile (e.g., €, $, £)
- **Financial_Status**: The payment state of an invoice — Unpaid (1), Partially Paid (2), Paid (3), Overdue (4)

## Requirements

### Requirement 1: Revenue KPI Cards

**User Story:** As a business owner, I want to see revenue, outstanding, overdue, and expense totals on the dashboard, so that I can assess my financial position at a glance.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute and display the sum of Amount from all non-voided payments (IsVoided = 0) where PaymentDateUtc falls within the current calendar month (1st of month 00:00:00 UTC to end of month 23:59:59 UTC) as "Revenue This Month", formatted with the Tenant's Currency_Symbol and two decimal places
2. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute and display the total outstanding balance across all issued (InvoiceStatusTypeId = 2), non-deleted (IsDeleted = 0) invoices with InvoiceFinancialStatusTypeId in (1, 2, 4) as "Outstanding", where outstanding balance per invoice equals TotalAmount minus the sum of non-voided payments recorded against that invoice, formatted with the Tenant's Currency_Symbol and two decimal places
3. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute and display the total outstanding balance for issued (InvoiceStatusTypeId = 2), non-deleted (IsDeleted = 0) invoices where DueDate is earlier than today's UTC date and the outstanding balance is greater than zero as "Overdue", formatted with the Tenant's Currency_Symbol and two decimal places
4. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute and display the sum of TotalAmount from all non-cancelled purchases where InvoiceDate falls within the current calendar month as "Expenses This Month", formatted with the Tenant's Currency_Symbol and two decimal places
5. WHEN the Dashboard loads, THE Dashboard_Service SHALL display a supporting count beneath each KPI card: the count of non-voided payments in the current calendar month beneath Revenue, the count of invoices included in the Outstanding calculation beneath Outstanding, the count of invoices included in the Overdue calculation beneath Overdue, and the count of non-cancelled purchases in the current calendar month beneath Expenses
6. THE Dashboard_Service SHALL scope all KPI queries to the authenticated Tenant's BusinessId
7. IF no records exist for a KPI calculation, THEN THE Dashboard_Service SHALL display a zero amount (formatted with the Tenant's Currency_Symbol and two decimal places) and a count of zero for that KPI card

### Requirement 2: Monthly Revenue vs Expenses Chart

**User Story:** As a business owner, I want to see a bar chart comparing revenue and expenses over the last 6 months, so that I can identify trends in profitability.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve monthly revenue totals (sum of Amount from payments where IsVoided = 0, grouped by PaymentDateUtc month) for the last 6 calendar months including the current month
2. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve monthly expense totals (sum of TotalAmount from purchases, grouped by InvoiceDate month) for the last 6 calendar months including the current month
3. THE Revenue_Chart SHALL render a grouped bar chart with revenue bars in green (#129867) and expense bars in blue (#0D5EA6) for each month
4. THE Revenue_Chart SHALL label each month on the x-axis using abbreviated month names (e.g., Jan, Feb, Mar) ordered chronologically from oldest (left) to newest (right)
5. THE Revenue_Chart SHALL display amounts on the y-axis formatted with the Tenant's Currency_Symbol and no decimal places for axis labels
6. IF a month within the 6-month window contains no revenue or no expense records, THEN THE Revenue_Chart SHALL display a zero-height bar for that month
7. THE Dashboard_Service SHALL scope all chart queries to the authenticated Tenant's BusinessId

### Requirement 3: Invoice Status Breakdown Chart

**User Story:** As a business owner, I want to see the distribution of my invoices by payment status, so that I can understand my receivables health.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute the count of invoices with InvoiceStatusTypeId = 2 (Issued) grouped by Financial_Status (Paid, Partially Paid, Unpaid, Overdue), excluding WrittenOff invoices from the chart
2. THE Invoice_Status_Chart SHALL render a donut chart with one colour segment per Financial_Status using the following mapping: green for Paid, gold for Partially Paid, blue for Unpaid, red for Overdue
3. THE Invoice_Status_Chart SHALL display the numeric count adjacent to or within each status segment
4. IF no issued invoices exist for the Tenant, THEN THE Invoice_Status_Chart SHALL display a message indicating no invoice data is available instead of rendering an empty chart
5. THE Dashboard_Service SHALL scope the invoice status query to the authenticated Tenant's BusinessId

### Requirement 4: Recent Invoices Table

**User Story:** As a business owner, I want to see my most recent invoices on the dashboard, so that I can quickly check their status without navigating away.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve the 5 most recently issued invoices ordered by invoice date descending, returning only non-deleted invoices with InvoiceStatusTypeId = 2 (Issued) scoped to the Tenant's BusinessId
2. THE Recent_Invoices_Table SHALL display columns: Invoice Number, Customer Name, Financial Status (as a colour-coded pill), and Total Amount
3. THE Recent_Invoices_Table SHALL use pill colours: green for Paid, gold for Partially Paid, blue for Unpaid, red for Overdue
4. THE Recent_Invoices_Table SHALL format the Total Amount with the Tenant's Currency_Symbol and two decimal places
5. IF fewer than 5 issued invoices exist for the Tenant, THEN THE Recent_Invoices_Table SHALL display only the invoices that exist
6. IF no issued invoices exist for the Tenant, THEN THE Recent_Invoices_Table SHALL display a message indicating no recent invoices are available

### Requirement 5: Overdue Invoices Table

**User Story:** As a business owner, I want to see which invoices are past due on the dashboard, so that I can prioritise collection follow-ups.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve all issued (InvoiceStatusTypeId = 2), non-deleted (IsDeleted = 0) invoices where the DueDate is earlier than today (UTC) and the outstanding balance (TotalAmount minus sum of non-voided payments) is greater than zero, ordered by DueDate ascending
2. THE Overdue_Invoices_Table SHALL display columns: Invoice Number, Customer Name, Due Date (formatted as dd MMM yyyy), and Outstanding Amount (formatted with the Tenant's Currency_Symbol and two decimal places)
3. THE Overdue_Invoices_Table SHALL highlight overdue rows with a light red background (#FFF8F8)
4. WHEN overdue invoices exist, THE Overdue_Invoices_Table SHALL display a warning banner below the table summarising the count of overdue invoices and the total overdue amount formatted with the Tenant's Currency_Symbol and two decimal places
5. IF no invoices are overdue, THEN THE Overdue_Invoices_Table SHALL display a message indicating no overdue invoices exist and SHALL NOT display the warning banner
6. THE Dashboard_Service SHALL scope the overdue query to the authenticated Tenant's BusinessId
7. IF the Overdue_Invoices_Table contains more than 10 invoices, THEN THE Overdue_Invoices_Table SHALL display only the first 10 rows ordered by DueDate ascending

### Requirement 6: Recent Payments Table

**User Story:** As a business owner, I want to see the latest payments received on the dashboard, so that I can confirm recent cash inflows.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve the 5 most recent non-voided payments ordered by PaymentDateUtc descending, scoped to the authenticated Tenant's BusinessId
2. THE Recent_Payments_Table SHALL display columns: Payment Date (formatted as dd MMM yyyy), Customer Name, Payment Method (as a colour-coded pill), and Amount
3. THE Recent_Payments_Table SHALL use pill colours: cyan for Bank Transfer, green for Cash, blue for Card, gold for Cheque, and grey for Other
4. THE Recent_Payments_Table SHALL format the Amount in green with the Tenant's Currency_Symbol and two decimal places
5. IF fewer than 5 non-voided payments exist for the Tenant, THEN THE Recent_Payments_Table SHALL display only the payments that exist
6. IF no non-voided payments exist for the Tenant, THEN THE Recent_Payments_Table SHALL display a message indicating no recent payments are available

### Requirement 7: VAT Summary Panel

**User Story:** As a business owner, I want to see my current VAT position on the dashboard, so that I can plan for upcoming tax obligations.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL retrieve the VAT submission data for the current VAT period, defined as the period with the latest PeriodEndDate that has IsSubmitted = 0 (open); if no open period exists, the period with the most recent PeriodEndDate SHALL be used
2. THE VAT_Summary_Section SHALL display three values: Output VAT, Input VAT, and Net Payable (Output VAT minus Input VAT), each formatted with the Tenant's Currency_Symbol and two decimal places
3. THE VAT_Summary_Section SHALL label each value with "Current period" beneath the monetary amount
4. IF no VAT submission exists for the current period, THEN THE VAT_Summary_Section SHALL display €0.00 for all three values with a note indicating no submission data is available
5. THE Dashboard_Service SHALL scope the VAT query to the authenticated Tenant's BusinessId

### Requirement 8: Top Customers by Revenue

**User Story:** As a business owner, I want to see my top 5 customers ranked by invoiced amount, so that I can identify my most valuable client relationships.

#### Acceptance Criteria

1. WHEN the Dashboard loads, THE Dashboard_Service SHALL compute the top 5 customers ranked by total invoiced amount (sum of TotalAmount from issued, non-deleted invoices across all time) in descending order
2. THE Top_Customers_Section SHALL display columns: Customer Name, Total Invoiced, and Total Paid (sum of non-voided payments received against that customer's invoices)
3. THE Top_Customers_Section SHALL format monetary values with the Tenant's Currency_Symbol and two decimal places
4. THE Top_Customers_Section SHALL display Total Paid values in green (success colour) to indicate collected revenue
5. IF fewer than 5 customers have invoices, THEN THE Top_Customers_Section SHALL display only the customers that exist without placeholder rows
6. IF no customers have invoices, THEN THE Top_Customers_Section SHALL display a message indicating no customer invoice data is available
7. THE Dashboard_Service SHALL scope the top customers query to the authenticated Tenant's BusinessId

### Requirement 9: Dashboard Layout and Ordering

**User Story:** As a business owner, I want the dashboard sections arranged in a logical visual hierarchy, so that the most critical financial data is visible first.

#### Acceptance Criteria

1. THE Dashboard SHALL render sections in the following order from top to bottom: Quotation KPIs (existing), Revenue KPI Cards, Charts (Revenue vs Expenses and Invoice Status side by side), Recent Invoices and Overdue Invoices side by side, Recent Payments and Recent Quotations (existing) side by side, VAT Summary and Top Customers side by side
2. THE Dashboard SHALL use a 4-column grid for KPI card rows
3. THE Dashboard SHALL use a 2-column grid for paired sections (charts, tables)
4. THE Dashboard SHALL apply the `.glass.card-pad` styling to all new section cards
5. THE Dashboard SHALL apply coloured left borders to Revenue KPI cards: green (#129867) for Revenue, red (#C24A4A) for Outstanding, gold (#C8912E) for Overdue, blue (#0D5EA6) for Expenses
6. WHEN the Dashboard loads and a section has no data to display, THE Dashboard SHALL still render the section container with its empty-state message rather than hiding the section entirely

### Requirement 10: Tenant Data Isolation

**User Story:** As a platform operator, I want all dashboard data scoped to the authenticated tenant, so that businesses cannot see each other's operational data.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL include a BusinessId filter in every database query executed for the dashboard
2. THE Dashboard SHALL retrieve the BusinessId from the authenticated user's session via the CurrentTenantService
3. IF the BusinessId cannot be resolved from the authenticated session, THEN THE Dashboard SHALL redirect the user to an error page and SHALL NOT render any dashboard content or partial data
4. THE Dashboard_Service SHALL treat a null or zero BusinessId as unresolved and apply the same error handling as criterion 3
