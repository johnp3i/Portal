# Requirements Document

## Introduction

The Scoped Dashboard feature restricts the Home/Index dashboard to display only KPIs, charts, tables, and quick actions relevant to the authenticated user's module permissions. Currently, all authenticated users see the full business financial dashboard regardless of their access levels, exposing sensitive data beyond their scope. This feature ensures each user sees only the dashboard sections they are authorised to view, while Owners and SuperAdmins retain the full dashboard experience.

## Glossary

- **Dashboard**: The Home/Index page rendered by HomeController that displays KPI gauges, charts, tables, and quick action links summarising business operations.
- **Module_Permission**: A key-value pair (module name → access level) representing a user's granted access to a specific Portal module, as returned by IPermissionService.GetAllAccessLevelsAsync.
- **Access_Level**: One of three values — "full", "readonly", or "none" — indicating the degree of access a user has to a module.
- **Visible_Access**: An Access_Level of "full" or "readonly" (any value other than "none"), indicating the user has permission to view data from that module.
- **Privileged_User**: A user who has the "IsOwner" claim set to "true" or is in the "SuperAdmin" role. Privileged users always see the full unscoped dashboard.
- **KPI_Section**: A logical grouping of dashboard elements (gauges, charts, tables, quick actions) that correspond to a specific module's data domain.
- **Dashboard_Service**: The IDashboardService that provides KPI data, chart data, invoice lists, payment lists, and other dashboard aggregates.
- **Permission_Service**: The IPermissionService that retrieves module access levels for a given user.
- **Revenue_Section**: The KPI_Section containing revenue gauge, outstanding gauge, overdue gauge, revenue vs expenses chart, recent payments table, overdue invoices table, top customers table, and revenue-by-customer chart.
- **Invoice_Section**: The KPI_Section containing invoice status breakdown chart and recent invoices table.
- **Quotation_Section**: The KPI_Section containing the quotation stats strip (drafts, sent, accepted, customers) and recent quotations table.
- **Purchase_Section**: The KPI_Section containing the expenses gauge.
- **VAT_Section**: The KPI_Section containing the VAT summary strip (output, input, net payable).

## Requirements

### Requirement 1: Fetch User Permissions on Dashboard Load

**User Story:** As a platform operator, I want the dashboard to retrieve the current user's module permissions, so that the system can determine which sections to display.

#### Acceptance Criteria

1. WHEN the Dashboard is loaded, THE Dashboard SHALL retrieve the authenticated user's Module_Permissions via the Permission_Service.
2. WHEN the authenticated user is a Privileged_User, THE Dashboard SHALL bypass permission checks and treat all modules as having Visible_Access.
3. IF the Permission_Service fails to return permissions, THEN THE Dashboard SHALL display an empty state with a message indicating data is temporarily unavailable.

### Requirement 2: Scope Revenue KPI Section

**User Story:** As a business owner, I want revenue KPIs hidden from users without revenue access, so that sensitive financial data is protected.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "revenue" module, THE Dashboard SHALL display the Revenue_Section including the revenue gauge, outstanding gauge, overdue gauge, revenue vs expenses chart, recent payments table, overdue invoices table, top customers table, and revenue-by-customer chart.
2. WHILE the user does not have Visible_Access to the "revenue" module, THE Dashboard SHALL hide the entire Revenue_Section.
3. WHILE the user has Visible_Access to the "revenue" module, THE Dashboard_Service SHALL fetch revenue KPI data, overdue invoices, recent payments, revenue vs expenses chart data, and top customers data.
4. WHILE the user does not have Visible_Access to the "revenue" module, THE Dashboard_Service SHALL skip fetching revenue-related data to avoid unnecessary database queries.

### Requirement 3: Scope Invoice KPI Section

**User Story:** As a business owner, I want invoice KPIs hidden from users without invoice access, so that billing data is only visible to authorised users.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "invoice" module, THE Dashboard SHALL display the Invoice_Section including the invoice status breakdown chart and recent invoices table.
2. WHILE the user does not have Visible_Access to the "invoice" module, THE Dashboard SHALL hide the entire Invoice_Section.
3. WHILE the user does not have Visible_Access to the "invoice" module, THE Dashboard_Service SHALL skip fetching invoice status breakdown and recent invoice data.

### Requirement 4: Scope Quotation KPI Section

**User Story:** As a business owner, I want quotation KPIs hidden from users without quotation access, so that sales pipeline data is only visible to authorised users.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "quotation" module, THE Dashboard SHALL display the Quotation_Section including the quotation stats strip and recent quotations table.
2. WHILE the user does not have Visible_Access to the "quotation" module, THE Dashboard SHALL hide the entire Quotation_Section.
3. WHILE the user does not have Visible_Access to the "quotation" module, THE Dashboard_Service SHALL skip fetching quotation data.

### Requirement 5: Scope Purchase/Expenses KPI Section

**User Story:** As a business owner, I want expense KPIs hidden from users without purchase access, so that expenditure data is only visible to authorised users.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "purchase" module, THE Dashboard SHALL display the Purchase_Section including the expenses gauge.
2. WHILE the user does not have Visible_Access to the "purchase" module, THE Dashboard SHALL hide the Purchase_Section.
3. WHILE the user does not have Visible_Access to the "purchase" module, THE Dashboard_Service SHALL skip fetching expenses data.

### Requirement 6: Scope VAT Summary Section

**User Story:** As a business owner, I want VAT data hidden from users without VAT access, so that tax information is only visible to authorised users.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "vat" module, THE Dashboard SHALL display the VAT_Section in the quotation stats strip area.
2. WHILE the user does not have Visible_Access to the "vat" module, THE Dashboard SHALL hide the VAT_Section.
3. WHILE the user does not have Visible_Access to the "vat" module, THE Dashboard_Service SHALL skip fetching VAT summary data.

### Requirement 7: Scope Quick Action Links

**User Story:** As a user, I want to only see quick action shortcuts for modules I have access to, so that I am not presented with actions I cannot perform.

#### Acceptance Criteria

1. WHILE the user has Visible_Access to the "quotation" module, THE Dashboard SHALL display the "New Quotation" quick action link.
2. WHILE the user has Visible_Access to the "revenue" module, THE Dashboard SHALL display the "Record Payment" and "Customer Statement" quick action links.
3. WHILE the user has Visible_Access to the "invoice" module, THE Dashboard SHALL display the "Create Invoice" quick action link.
4. WHILE the user has Visible_Access to the "purchase" module, THE Dashboard SHALL display the "Record Purchase" quick action link.
5. WHILE the user has Visible_Access to the "customer" module, THE Dashboard SHALL display the "New Customer" quick action link.
6. WHILE the user does not have Visible_Access to a module, THE Dashboard SHALL hide the corresponding quick action links for that module.

### Requirement 8: Empty Dashboard State

**User Story:** As a user with limited permissions, I want to see a meaningful welcome message when I have no KPI-bearing modules, so that I understand the dashboard is intentionally empty rather than broken.

#### Acceptance Criteria

1. WHEN the user has no Visible_Access to any of the following modules: "revenue", "invoice", "quotation", "purchase", or "vat", THE Dashboard SHALL display a welcome message indicating no dashboard data is available for the user's current permissions.
2. WHEN the user has no KPI-bearing module access, THE Dashboard SHALL display the business name and a suggestion to contact an administrator if additional access is needed.
3. WHEN the user has Visible_Access to at least one KPI-bearing module, THE Dashboard SHALL display the relevant KPI sections without the empty state message.

### Requirement 9: Access Level Does Not Differentiate KPI Visibility

**User Story:** As a platform operator, I want both "full" and "readonly" access levels to show the same KPI sections, so that the scoping logic remains simple and predictable.

#### Acceptance Criteria

1. THE Dashboard SHALL treat "full" and "readonly" Access_Levels identically when determining KPI_Section visibility.
2. THE Dashboard SHALL hide a KPI_Section only when the user's Access_Level for the corresponding module is "none".

### Requirement 10: Chart Layout Adapts to Visible Sections

**User Story:** As a user, I want the dashboard layout to adapt gracefully when sections are hidden, so that the page does not display empty gaps or broken grid layouts.

#### Acceptance Criteria

1. WHEN only one chart section is visible in a two-column grid row, THE Dashboard SHALL render that section at full width.
2. WHEN both chart sections in a grid row are hidden, THE Dashboard SHALL hide the entire grid row.
3. WHEN only one table section is visible in a two-column grid row, THE Dashboard SHALL render that section at full width.
