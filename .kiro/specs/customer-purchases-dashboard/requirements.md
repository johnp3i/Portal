# Requirements Document

## Introduction

The Customer Purchases Dashboard is a per-customer page in the Portal that shows what a customer buys, how that changes over time, and what the customer still owes. It is opened from a new "Purchases" button in the Actions column of the Customers list (`/Customer`, `Views/Customer/Index.cshtml`) and is served at `/Customer/Purchases/{id}`.

The approved layout is the mockup `.kiro/docs/mockups/customer-purchases-dashboard.html`. That file is the source of truth for layout and sample behaviour. Where this document and the mockup's sample numbers differ, this document defines the rules and the mockup only illustrates them.

The page covers invoices only. Quotations are never included.

Data facts that shape the rules (verified against `[invoice].[InvoiceLine]`):

- `InvoiceLine` has no product foreign key. Products are identified by `ProductCode` (present on about 53% of lines) or, when no code exists, by `Description`.
- `CostPrice` is present on about 59% of lines. `ProductTypeId` is present on about 1.5% of lines. Price tier is present on 0% of lines.
- Financial rules follow the workspace financial conventions: outstanding balance is credit-note aware, and overdue is derived, never read from the persisted status. Written-off invoices are not counted as owed, so they appear in neither the pending nor the overdue figures.

Out of scope for this release: quotation-based figures, per-product unpaid allocation, product type and price tier analysis, cross-sell recommendations, alert rules (for example "notify me if the customer has not reordered in N days"), cross-currency conversion, and a business-wide top-customers-by-product view.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 back-office web application.
- **Purchases_Page**: The per-customer dashboard served at `/Customer/Purchases/{id}`.
- **Customers_List**: The page at `/Customer` rendered by `Views/Customer/Index.cshtml`.
- **Purchases_Service**: The service layer component that computes all dashboard figures.
- **Purchases_Repository**: The repository layer component that reads dashboard data with parameterised SQL.
- **Purchases_Controller**: The controller that serves the Purchases_Page and its Ajax endpoints.
- **Export_Service**: The component that builds the Excel workbook with ClosedXML.
- **Interest_Store**: The table that records "Notify me when available" clicks (see Requirement 15).
- **Tenant_Service**: `ICurrentTenantService`, which supplies the current `BusinessId`.
- **Customer**: A row in the customer table that belongs to one business.
- **Counted_Invoice**: An invoice of the selected customer that is not soft-deleted (`IsDeleted = 0`) and whose `InvoiceStatusTypeId` is neither Draft nor Cancelled.
- **Selected_Currency**: The single `CurrencyCode` whose figures the Purchases_Page currently shows.
- **Selected_Period**: The date range chosen in the Period filter, applied to `Invoice.InvoiceDate`.
- **Applied_Credit**: The sum of `[credit].[CreditNoteApplication].[AmountApplied]` where `IsVoided = 0` and the parent `[credit].[CreditNote]` belongs to the current business, for a given invoice.
- **Outstanding_Balance**: `TotalAmount` minus valid Payments minus Applied_Credit for an invoice, with the `FinancialStatusEngine.ComputeOutstandingBalance` semantics.
- **Written_Off_Invoice**: A Counted_Invoice whose persisted `InvoiceFinancialStatusTypeId` is WrittenOff (5).
- **Overdue_Invoice**: A Counted_Invoice that is not a Written_Off_Invoice, whose Outstanding_Balance is greater than 0 and whose `DueDate` is earlier than today (UTC).
- **Pending_Invoice**: A Counted_Invoice that is not a Written_Off_Invoice and whose Outstanding_Balance is greater than 0.
- **Catalogue_Currency**: The `CurrencyCode` used by the largest number of Counted_Invoices across the whole business, with ties broken by ascending alphabetical order of `CurrencyCode`. The product catalogue has no currency column, so list prices are treated as being in this currency.
- **Net_Revenue**: For a set of Counted_Invoices, the sum of `TotalAmount` minus the sum of Applied_Credit, in the Selected_Currency.
- **Product_Key**: The grouping identity of an invoice line: the `ProductCode` when present, otherwise the trimmed, case-insensitive `Description`.
- **Custom_Item**: A product group whose Product_Key comes from `Description` because the line has no `ProductCode`.
- **Product_Line**: An invoice line of a Counted_Invoice with `IsAdjustmentLine = 0`.
- **Purchase_Date**: The `InvoiceDate` of a Counted_Invoice that contains at least one Product_Line for a given Product_Key.
- **Typical_Gap**: The median number of days between consecutive distinct Purchase_Dates of one Product_Key.
- **KPI_Card**: One of the six summary cards in the KPI strip.
- **Teaser_Card**: A card tagged LATER (Discounts, Price drift) with a "Notify me when available" button.

## Requirements

### Requirement 1: Entry point on the Customers list

**User Story:** As a sales user, I want a Purchases button on each customer row, so that I can open that customer's purchase analysis in one click.

#### Acceptance Criteria

1. THE Customers_List SHALL display exactly one "Purchases" button in the Actions column of every customer row, positioned immediately after the "Statement" button and immediately before the "Deactivate" button.
2. WHEN a user selects the "Purchases" button for a customer, THE Customers_List SHALL navigate the browser to `/Customer/Purchases/{id}` in the same browser tab, where `{id}` is that customer's identifier.
3. THE Customers_List SHALL display the "Purchases" button for both active and inactive customers, with identical position, label, and enabled state.
4. THE Customers_List SHALL style the "Purchases" button with the same `tbl-action tbl-action--primary` classes as the "Edit" and "Statement" buttons.
5. IF a customer row has no valid customer identifier, THEN THE Customers_List SHALL NOT display the "Purchases" button for that row.
6. WHEN a user selects the "Purchases" button, THE Customers_List SHALL complete navigation to the target page without opening a confirmation dialog and without changing the current filter, search, or pagination state when the user returns via the browser Back action.

### Requirement 2: Route, access and tenant isolation

**User Story:** As a business owner, I want customer purchase data limited to my own business and authorised users, so that sensitive financial data is not exposed.

#### Acceptance Criteria

1. WHEN an authenticated user with Customer module access requests `/Customer/Purchases/{id}` for a customer that exists and belongs to the user's business, THE Purchases_Controller SHALL render the Purchases_Page for that customer.
2. IF the requesting user is unauthenticated, THEN THE Purchases_Controller SHALL deny access in the same way the existing Customer pages do for unauthenticated users and SHALL return no customer purchase data.
3. IF the requesting user is authenticated but lacks the module access that the existing Customer pages require, THEN THE Purchases_Controller SHALL deny access in the same way the existing Customer pages do and SHALL return no customer purchase data.
4. THE Purchases_Repository SHALL filter every query by the `BusinessId` supplied by the Tenant_Service.
5. IF the Tenant_Service supplies no `BusinessId` for the requesting user, THEN THE Purchases_Repository SHALL return no rows and THE Purchases_Controller SHALL return no customer purchase data.
6. IF the requested customer does not exist or belongs to a different business, THEN THE Purchases_Controller SHALL return a not-found response whose status, body and headers are identical for both cases.
7. IF a user supplies a customer identifier that does not exist or belongs to a different business in any Purchases Ajax endpoint, THEN THE Purchases_Controller SHALL return the same not-found response as criterion 6 and SHALL return no customer purchase data.
8. THE Purchases_Controller SHALL name every Ajax method with the prefix `AxGet` for HTTP GET methods or `AxPost` for HTTP POST methods.
9. IF a POST Ajax request reaches the Purchases_Controller without a valid antiforgery token, THEN THE Purchases_Controller SHALL reject the request, SHALL not execute the requested action, and SHALL not change any stored data.

### Requirement 3: Page header and navigation

**User Story:** As a sales user, I want clear context and navigation on the page, so that I know which customer I am viewing and can move to related pages.

#### Acceptance Criteria

1. WHEN the Purchases_Page loads for an existing customer, THE Purchases_Page SHALL display the breadcrumb "Sales / Customers / {customer name} / Purchases", where "Customers" links to `/Customer` and the other segments are plain text.
2. WHEN the Purchases_Page loads for an existing customer, THE Purchases_Page SHALL display, in top-to-bottom order, an eyebrow label, the heading "Purchases - {customer name}", and a subtitle stating that quotations are not included.
3. WHEN the Purchases_Page loads for an existing customer, THE Purchases_Page SHALL display the buttons "Back to Customers" linking to `/Customer`, "Statement" linking to `/Statement?customerId={id}` where {id} is the identifier of the displayed customer, and "Export Excel".
4. THE Purchases_Page SHALL render, in top-to-bottom order, a topbar, then the filter card, then the content cards, where the filter card and each content card is a `glass card-pad` section, the filter card has a bottom margin of 22px, and each stacked content card after the first has a top margin of 24px.
5. IF the customer name exceeds 100 characters, THEN THE Purchases_Page SHALL display the full name in the breadcrumb and heading without horizontal page scrolling, wrapping the text onto additional lines.
6. IF the requested customer does not exist or the user lacks access to it, THEN THE Purchases_Page SHALL display an error message indicating the customer was not found or is not accessible, SHALL NOT display any purchase data, and SHALL display the "Back to Customers" button linking to `/Customer`.

### Requirement 4: Period filter

**User Story:** As a sales user, I want to choose the time window, so that I can compare recent and historical behaviour.

#### Acceptance Criteria

1. WHEN the Purchases_Page loads without a Selected_Period in the query string, THE Purchases_Page SHALL offer the Period options "Last 12 months", "This year", "Last year", "All time" and "Custom range", with "Last 12 months" selected.
2. WHEN the user selects a Period option other than "Custom range", THE Purchases_Page SHALL reload all period-dependent sections for the Selected_Period through exactly one request.
3. WHEN the user selects "Custom range", THE Purchases_Page SHALL show a start date input and an end date input, and SHALL NOT send a period request until both dates are valid.
4. WHEN both custom dates are present, are valid calendar dates, and the start date is earlier than or equal to the end date, THE Purchases_Page SHALL reload all period-dependent sections for the custom range through exactly one request, treating both the start date and the end date as inclusive.
5. IF the custom start date is later than the custom end date, THEN THE Purchases_Page SHALL show a SweetAlert2 warning, SHALL send no period request, and SHALL keep the previously displayed results and the previous Selected_Period unchanged.
6. THE Purchases_Service SHALL interpret "Last 12 months" as the 12 calendar months ending with the current UTC month, with the current month counted from its first day up to the current UTC date and time.
7. THE Purchases_Service SHALL interpret "This year" as 1 January 00:00:00 UTC of the current UTC year through the current UTC date and time, and "Last year" as 1 January 00:00:00 UTC through 31 December 23:59:59 UTC of the previous UTC year.
8. WHILE a period request is running, THE Purchases_Page SHALL show BlockUI with a message.
9. WHEN a period request succeeds or fails, THE Purchases_Page SHALL hide BlockUI within 500 milliseconds of receiving the response.
10. IF a period request fails, THEN THE Purchases_Page SHALL show a SweetAlert2 error, and SHALL keep the previously displayed results and the previous Selected_Period unchanged.
11. WHEN the Selected_Period changes successfully, THE Purchases_Page SHALL update the query string to include the Selected_Period, including the start and end dates for "Custom range", without a full page reload. WHERE the user has explicitly chosen a Selected_Currency, THE Purchases_Page SHALL also keep it in the query string as `currency`, and WHEN the Purchases_Page loads with a `currency` value that matches a currency of the customer's Counted_Invoices, THE Purchases_Page SHALL use it as the Selected_Currency, ignoring any invalid or unknown `currency` value.
12. WHEN the Purchases_Page loads with a valid Selected_Period in the query string, THE Purchases_Page SHALL select that Period and display results for it, so that a shared URL shows the same period.
13. IF the Purchases_Page loads with a Selected_Period in the query string that is not a listed Period option or has an invalid custom range, THEN THE Purchases_Page SHALL fall back to "Last 12 months" and SHALL show a SweetAlert2 warning.

### Requirement 5: Currency selection

**User Story:** As a sales user, I want figures for one currency at a time, so that totals are never mixed across currencies.

#### Acceptance Criteria

1. WHERE the customer has Counted_Invoices in more than one `CurrencyCode`, THE Purchases_Page SHALL display a segmented currency switch containing exactly one option per distinct `CurrencyCode` found in the customer's Counted_Invoices, ordered alphabetically by code.
2. WHERE the customer has Counted_Invoices in exactly one `CurrencyCode`, THE Purchases_Page SHALL hide the currency switch and SHALL use that currency as the Selected_Currency.
3. WHEN the Purchases_Page loads or the Selected_Period changes, AND the customer has Counted_Invoices in more than one `CurrencyCode`, THE Purchases_Page SHALL set the Selected_Currency to the currency with the highest Net_Revenue in the Selected_Period, and SHALL break ties by ascending alphabetical order of `CurrencyCode`.
4. WHEN the user selects a currency in the currency switch, THE Purchases_Page SHALL recompute every section for the selected currency only, SHALL preserve the Selected_Period, and SHALL mark the selected option as active in the currency switch.
5. THE Purchases_Service SHALL exclude Counted_Invoices whose `CurrencyCode` differs from the Selected_Currency from every sum, count, average and chart value.
6. THE Purchases_Page SHALL show the Selected_Currency code or symbol adjacent to every monetary value, including table cells, summary figures, chart axis labels and chart tooltips.
7. THE Purchases_Service SHALL NOT convert amounts between currencies.
8. IF the customer has Counted_Invoices in more than one `CurrencyCode` but none in the Selected_Period, THEN THE Purchases_Page SHALL keep the currency switch visible and SHALL set the Selected_Currency to the currency of the customer's most recent Counted_Invoice.
9. IF the recomputation for a newly selected currency fails, THEN THE Purchases_Page SHALL keep the previously displayed figures and the previously active currency option, and SHALL display an error message indicating that the currency could not be loaded.

### Requirement 6: Revenue KPI cards

**User Story:** As a sales user, I want revenue KPIs, so that I can judge the size and direction of this customer's business.

#### Acceptance Criteria

1. THE Purchases_Service SHALL compute "Net revenue, last 12 months" as the sum of Net_Revenue of Counted_Invoices in the Selected_Currency dated from the first day of the month 11 months before the current month through the last day of the current month (inclusive), independent of the Selected_Period.
2. THE Purchases_Service SHALL compute the comparison value as the sum of Net_Revenue of Counted_Invoices in the Selected_Currency dated within the 12 calendar months immediately before the 12 months defined in criterion 1, independent of the Selected_Period.
3. WHEN the comparison value is greater than 0, THE Purchases_Page SHALL show the percentage change, calculated as (last 12 months value - comparison value) / comparison value x 100 and rounded to one decimal place, together with the comparison value and an up indicator if the change is greater than 0, a down indicator if the change is less than 0, or no directional indicator if the change is exactly 0.0.
4. IF the comparison value is 0 or there are no Counted_Invoices in the previous 12 months, THEN THE Purchases_Page SHALL show "No prior period" instead of a percentage and SHALL NOT divide by zero.
5. THE Purchases_Service SHALL compute "Lifetime net revenue" as the sum of Net_Revenue of all Counted_Invoices in the Selected_Currency, regardless of invoice date and independent of the Selected_Period.
6. WHEN at least one Counted_Invoice exists in the Selected_Currency, THE Purchases_Page SHALL show under the lifetime value the count of those Counted_Invoices and the month and year of the earliest dated Counted_Invoice.
7. IF no Counted_Invoice exists in the Selected_Currency, THEN THE Purchases_Page SHALL show 0 as the "Lifetime net revenue" value, SHALL show a count of 0, and SHALL NOT show a first-invoice month and year.
8. WHEN at least one Counted_Invoice falls in the Selected_Period, THE Purchases_Service SHALL compute "Average invoice" as the sum of Net_Revenue of Counted_Invoices in the Selected_Period and Selected_Currency divided by their count, and THE Purchases_Page SHALL show that count as the detail line.
9. IF no Counted_Invoice falls in the Selected_Period, THEN THE Purchases_Page SHALL show "No invoices" for "Average invoice" and SHALL NOT divide by zero.

### Requirement 7: Last purchase KPI card

**User Story:** As a sales user, I want to know how recently the customer bought, so that I can follow up in time.

#### Acceptance Criteria

1. THE Purchases_Service SHALL compute "Last purchase" from the latest `InvoiceDate` among all Counted_Invoices in the Selected_Currency, independent of the Selected_Period.
2. WHEN the Purchases_Page loads or the Selected_Currency changes, THE Purchases_Page SHALL show the number of whole calendar days (N, an integer from 0 to 36,500) between the last purchase date and the current date (UTC) as "{N} days ago", with N = 1 shown as "1 day ago" and N = 0 shown as "Today".
3. WHEN the Purchases_Page shows the last purchase, THE Purchases_Page SHALL show the date of the last purchase in the format `dd MMM yyyy` (for example, "05 Mar 2025").
4. WHERE the customer has at least 3 Counted_Invoices in the Selected_Currency, THE Purchases_Page SHALL show "buys about every {M} days", where M is the median number of whole days between consecutive distinct invoice dates (sorted ascending, duplicates on the same date counted once), rounded to the nearest whole day with 0.5 rounded up, and with a minimum displayed value of 1.
5. WHERE the customer has fewer than 3 Counted_Invoices in the Selected_Currency, THE Purchases_Page SHALL omit the frequency text.
6. IF the customer has 3 or more Counted_Invoices in the Selected_Currency but fewer than 2 distinct invoice dates, THEN THE Purchases_Page SHALL omit the frequency text.
7. IF the customer has no Counted_Invoices in the Selected_Currency, THEN THE Purchases_Page SHALL show an empty-state value of "No purchases yet" on the card and SHALL omit the date, the "{N} days ago" text, and the frequency text.
8. IF the latest `InvoiceDate` is later than the current date (UTC), THEN THE Purchases_Page SHALL show "Today" in place of "{N} days ago" and SHALL still show the date of the last purchase.

### Requirement 8: Pending and overdue KPI cards

**User Story:** As a credit controller, I want to see unpaid and overdue amounts, so that I can chase payment.

#### Acceptance Criteria

1. THE Purchases_Service SHALL compute Outstanding_Balance for each invoice as `TotalAmount` minus the sum of valid Payments minus the sum of Applied_Credit, using the same currency as the invoice and rounding to 2 decimal places.
2. WHEN the Purchases_Page loads or the Selected_Currency changes, THE Purchases_Service SHALL compute "Pending invoices" as the count and the summed Outstanding_Balance of all Pending_Invoices in the Selected_Currency, independent of the Selected_Period.
3. WHEN the Purchases_Page loads or the Selected_Currency changes, THE Purchases_Service SHALL compute "Overdue" as the summed Outstanding_Balance of all Overdue_Invoices in the Selected_Currency, independent of the Selected_Period.
4. THE Purchases_Service SHALL classify an invoice as an Overdue_Invoice only IF its Outstanding_Balance is greater than 0 AND its `DueDate` is earlier than the current UTC calendar date, and SHALL NOT read `InvoiceFinancialStatusTypeId` = 4 (Overdue) or any other persisted status to decide whether an invoice is overdue. THE Purchases_Service SHALL read the persisted `InvoiceFinancialStatusTypeId` only to recognise a Written_Off_Invoice (5).
5. WHEN the "Overdue" card is displayed and exactly one Overdue_Invoice exists, THE Purchases_Page SHALL show the count (1) and the number of whole days between its `DueDate` and the current UTC calendar date.
6. WHEN the "Overdue" card is displayed and two or more Overdue_Invoices exist, THE Purchases_Page SHALL show the count and the number of whole days between the earliest `DueDate` among them and the current UTC calendar date.
7. THE Purchases_Service SHALL exclude invoices that are Draft, Cancelled, soft-deleted, or Written_Off_Invoices from both "Pending invoices" and "Overdue".
8. IF no Pending_Invoice exists in the Selected_Currency, THEN THE Purchases_Page SHALL show a count of 0 and an amount of 0.00 on the "Pending invoices" card, and a count of 0 and an amount of 0.00 on the "Overdue" card, with no days-late value.
9. IF the Purchases_Service fails to compute "Pending invoices" or "Overdue", THEN THE Purchases_Controller SHALL handle the failure as defined in Requirement 18, and THE Purchases_Page SHALL NOT show a stale, partial or zero-filled amount on either card.
10. THE Purchases_Page SHALL style the "Pending invoices" and "Overdue" cards with the warning treatment from the mockup.

### Requirement 9: Collapsible KPI cards with cookie persistence

**User Story:** As a user, I want to collapse KPI cards and have the page remember it, so that my preferred view persists.

#### Acceptance Criteria

1. THE Purchases_Page SHALL display one toggle button on each of the six KPI_Cards, with an `aria-expanded` attribute set to "true" while the card is expanded and "false" while it is collapsed, and an `aria-label` of "Collapse card" while the card is expanded and "Expand card" while it is collapsed.
2. WHEN the user activates the toggle on an expanded KPI_Card, THE Purchases_Page SHALL collapse that card, keep its label and main value visible, hide its detail line, and update that toggle's `aria-expanded` and `aria-label` to the collapsed values.
3. WHEN the user activates the toggle on a collapsed KPI_Card, THE Purchases_Page SHALL expand that card, show its label, main value and detail line, and update that toggle's `aria-expanded` and `aria-label` to the expanded values.
4. THE Purchases_Page SHALL display a "Collapse all" link and an "Expand all" link above the KPI strip.
5. WHEN the user activates the "Collapse all" link, THE Purchases_Page SHALL collapse all six KPI_Cards, including cards that are already collapsed.
6. WHEN the user activates the "Expand all" link, THE Purchases_Page SHALL expand all six KPI_Cards, including cards that are already expanded.
7. WHEN the collapsed state of one or more KPI_Cards changes, THE Purchases_Page SHALL write the cookie `cp_kpi_collapsed` containing the comma-separated identifiers of the collapsed cards, with `max-age` of 31536000 seconds, `path=/` and `SameSite=Lax`.
8. WHEN all six KPI_Cards are expanded, THE Purchases_Page SHALL write the cookie `cp_kpi_collapsed` with an empty value.
9. WHEN the Purchases_Page loads, THE Purchases_Page SHALL read the cookie `cp_kpi_collapsed` and SHALL apply the stored state before the KPI_Cards are first rendered visible, so that no card is displayed in a state different from the stored state at any time.
10. IF the cookie `cp_kpi_collapsed` is absent when the Purchases_Page loads, THEN THE Purchases_Page SHALL display all six KPI_Cards expanded.
11. THE Purchases_Page SHALL use the KPI identifiers `rev12`, `lifetime`, `avginv`, `last`, `pending` and `overdue`, one per KPI_Card, matched case-sensitively.
12. IF the cookie `cp_kpi_collapsed` contains an identifier that is not one of the six KPI identifiers, or an empty entry, THEN THE Purchases_Page SHALL ignore that entry and SHALL apply the state of the remaining valid identifiers.
13. THE Purchases_Page SHALL apply the same stored state to the Purchases_Page of every customer in the same browser.
14. WHEN a period or currency change refreshes the KPI values, THE Purchases_Page SHALL keep each KPI_Card's collapsed or expanded state, its toggle attributes and the cookie `cp_kpi_collapsed` unchanged.
15. IF the browser blocks writing the cookie `cp_kpi_collapsed`, THEN THE Purchases_Page SHALL still apply the collapse or expand action to the current page view without displaying an error.

### Requirement 10: Monthly trend chart

**User Story:** As a sales user, I want a monthly revenue chart, so that I can see trends and seasonality.

#### Acceptance Criteria

1. WHEN the Purchases_Page loads, THE Purchases_Page SHALL render a bar chart with exactly 12 bars, one for each of the last 12 calendar months ending with the current month, ordered chronologically from oldest on the left to newest on the right, showing Net_Revenue per month in the Selected_Currency.
2. THE Purchases_Service SHALL assign Net_Revenue to the calendar month of the invoice's `InvoiceDate`, including the Applied_Credit of that invoice.
3. WHEN the Purchases_Page renders the chart, THE Purchases_Page SHALL show the number of Counted_Invoices for each month under that month's label, formatted as "N inv" where N is a non-negative integer.
4. IF a month has no Counted_Invoices, THEN THE Purchases_Page SHALL render that month as a bar of zero height, with a Net_Revenue value of zero in the Selected_Currency and the label "0 inv".
5. THE Purchases_Page SHALL mark the current month as to-date by rendering its bar in a colour different from all other bars and appending an asterisk to its month label.
6. WHEN the Purchases_Page loads, THE Purchases_Page SHALL show a checkbox labelled "Compare with same month last year" in the selected state.
7. WHILE the comparison checkbox is selected, THE Purchases_Page SHALL draw a line series with 12 points, each showing the Net_Revenue in the Selected_Currency of the same calendar month one year earlier than the corresponding bar.
8. WHEN the user clears the comparison checkbox, THE Purchases_Page SHALL remove the line series within 500 ms without sending a server request.
9. WHEN the user hovers over or focuses a bar, THE Purchases_Page SHALL show a tooltip containing the month, the Net_Revenue amount in the Selected_Currency and the Counted_Invoices count for that bar.
10. WHILE the viewport width is below 768 px, THE Purchases_Page SHALL render the chart inside a horizontally scrollable container and SHALL keep the chart at a minimum width that shows all 12 bars without overlap.
11. THE Purchases_Page SHALL expose the chart with `role="img"` and a non-empty `aria-label` that names the chart and states the covered period of 12 months.
12. THE Purchases_Page SHALL render the chart for the last 12 calendar months regardless of the Selected_Period, and SHALL state this in the chart subtitle.
13. IF the chart data fails to load or the chart cannot be rendered from the loaded data, THEN THE Purchases_Page SHALL display an error message in place of the chart, SHALL keep the rest of the page usable, and SHALL NOT render partial bars.

### Requirement 11: Product grouping and data-quality rules

**User Story:** As a sales user, I want products grouped sensibly despite imperfect data, so that the mix is meaningful.

#### Acceptance Criteria

1. WHEN the product mix is requested, THE Purchases_Service SHALL include only Product_Lines of Counted_Invoices in the Selected_Currency and Selected_Period in the product mix.
2. WHEN a Product_Line has a `ProductCode` that is not null and not empty after trimming leading and trailing whitespace, THE Purchases_Service SHALL group it by that trimmed `ProductCode`.
3. WHEN comparing `ProductCode` values for grouping, THE Purchases_Service SHALL trim leading and trailing whitespace and compare case-insensitively, so that "ab-1" and " AB-1 " fall into the same group.
4. WHEN a Product_Line has a `ProductCode` that is null or empty after trimming, and it is not a Custom_Item, THE Purchases_Service SHALL group it by `Description` trimmed and compared case-insensitively.
5. WHEN Product_Lines are Custom_Items, THE Purchases_Service SHALL show each Custom_Item as its own display row with its own `Description`, and SHALL show the text "no product code" in the code column of that row.
6. WHEN a group contains more than one distinct `Description`, THE Purchases_Service SHALL use the `Description` of the Product_Line with the most recent invoice date in the group as the display name. IF two or more lines share the most recent invoice date, THEN THE Purchases_Service SHALL use the `Description` that sorts first in case-insensitive ascending order among them.
7. THE Purchases_Service SHALL exclude Product_Lines with `IsAdjustmentLine = 1` from the product mix, the donut chart, the drill-down, the buying rhythm and the Excel product sheet.
8. THE Purchases_Service SHALL include adjustment lines in every invoice total that is taken from `Invoice.TotalAmount`.
9. THE Purchases_Service SHALL compute product revenue as the sum of `LineTotal` of the group, quantity as the sum of `Quantity` of the group, and share as the product revenue divided by the sum of product revenue of all rows, expressed as a percentage rounded to one decimal place. IF the sum of product revenue of all rows is zero, THEN THE Purchases_Service SHALL show a share of 0.0% for every row and SHALL NOT raise an error.
10. THE Purchases_Service SHALL order product rows by revenue descending, then by display name ascending using case-insensitive comparison.
11. THE Purchases_Page SHALL show a note directly under the product mix table stating that product revenue excludes adjustment lines and may differ from the KPI Net_Revenue.
12. THE Purchases_Service SHALL NOT use `ProductTypeId`, `ProductPriceTierId` or `PriceTierName` in any grouping, filtering, ordering or calculation.
13. THE Purchases_Page SHALL show the product mix table with exactly these columns, in this order: Product, Qty, Revenue, Share, "Avg price vs list", "Last bought" and Margin.
14. THE Purchases_Page SHALL NOT show any per-product unpaid, outstanding or allocated amount in the product mix table or the drill-down.
15. IF no Product_Lines remain after applying the inclusion and exclusion rules, THEN THE Purchases_Page SHALL show an empty-state message in place of the product mix table and SHALL NOT show the note about adjustment lines.
16. THE Purchases_Page SHALL show the product mix 15 rows per page with "Showing {first}-{last} of {N} products" and Previous, page-number and Next controls below the table, SHALL return to page 1 when the Selected_Period, the Selected_Currency or the search text changes, and SHALL keep the margin coverage and the explanatory note describing all rows of the Selected_Period.
17. THE Purchases_Page SHALL provide a search box above the product mix that filters rows, without a server request, to those whose display name or product code contains the search text (case-insensitive), and SHALL show "No products match your search." when none match.

### Requirement 12: Price versus list, last bought and margin

**User Story:** As a sales user, I want to see whether the customer pays list price and what margin we make, so that I can judge pricing.

#### Acceptance Criteria

1. WHEN the Purchases_Service computes "average price paid" for a product and the sum of `Quantity` across its Product_Lines in the Selected_Period is greater than 0, THE Purchases_Service SHALL calculate it as the sum of `LineTotal` divided by the sum of `Quantity`, in the Selected_Currency.
2. IF the sum of `Quantity` across a product's Product_Lines in the Selected_Period is 0 or less, THEN THE Purchases_Page SHALL show "-" in the "Avg price vs list" column for that product and SHALL NOT show an error.
3. WHERE a product group has a `ProductCode` that matches an active catalogue product whose default selling price is greater than 0, AND the Selected_Currency equals the Catalogue_Currency, THE Purchases_Page SHALL show the average price paid, the list price, and the percentage difference calculated as (average price paid minus list price) divided by list price multiplied by 100, rounded half away from zero to one decimal place, with a "+" sign for positive values and a "-" sign for negative values.
4. IF a product group has no matching active catalogue `ProductCode`, a default selling price of 0 or less, or the Selected_Currency differs from the Catalogue_Currency, THEN THE Purchases_Page SHALL show "-" in the "Avg price vs list" column for that product.
5. WHEN the Purchases_Page displays a product, THE Purchases_Page SHALL show "Last bought" as the latest Purchase_Date among that product's Product_Lines in the Selected_Period.
6. WHEN the Purchases_Service computes margin for a product, THE Purchases_Service SHALL use only Product_Lines that have a `CostPrice`, calculated as (sum of `LineTotal` minus the sum of `CostPrice` multiplied by `Quantity`) divided by the sum of `LineTotal` of those lines, expressed as a percentage rounded half away from zero to one decimal place.
7. IF a product has no Product_Line with a `CostPrice`, or the sum of `LineTotal` of its Product_Lines with a `CostPrice` is 0, THEN THE Purchases_Page SHALL show "-" in the Margin column for that product.
8. WHEN the Purchases_Page renders the product table, THE Purchases_Page SHALL display the margin coverage directly below the table as a percentage rounded to one decimal place, defined as the sum of `LineTotal` of Product_Lines with a `CostPrice` divided by the sum of `LineTotal` of all Product_Lines in the Selected_Period.
9. IF the sum of `LineTotal` of all Product_Lines in the Selected_Period is 0, THEN THE Purchases_Page SHALL show "-" as the margin coverage.
10. WHERE the margin coverage of a single product is greater than 0% and below 100%, THE Purchases_Page SHALL mark that product's margin with a visible text label "partial".

### Requirement 13: Product drill-down and donut chart

**User Story:** As a sales user, I want to see the invoices behind a product and its share of spend, so that I can verify the numbers.

#### Acceptance Criteria

1. WHEN the user selects a collapsed product row, THE Purchases_Page SHALL expand a detail row directly below it listing the Product_Lines of that product in the Selected_Period and Selected_Currency, ordered by invoice date descending, with a maximum of 200 lines displayed, and SHALL show the notice "Showing the latest 200 of {total} lines" when more than 200 lines exist.
2. WHEN the user selects an expanded product row, THE Purchases_Page SHALL collapse its detail row and retain the already loaded lines for re-expansion without a new request.
3. WHEN the drill-down lines for a product are displayed, THE Purchases_Page SHALL show for each line the invoice date, the invoice number as a link to the invoice, the quantity, the line total in the Selected_Currency and the invoice status.
4. WHEN the drill-down lines for a product are displayed, THE Purchases_Page SHALL show the invoice status as exactly one of Unpaid, Partially paid, Paid, Overdue or Written off, using the derived overdue rule of Requirement 8, SHALL evaluate the Written_Off_Invoice condition first and show "Written off" for every line of a Written_Off_Invoice before any other status is considered, and SHALL pair each status with its visible text label.
5. WHEN the user expands a product row for the first time since the page was loaded or the Selected_Period or Selected_Currency last changed, THE Purchases_Page SHALL show BlockUI, load the drill-down lines through the `AxGetProductPurchaseLines` Ajax endpoint, and hide BlockUI when the request completes.
6. IF the drill-down request fails or does not complete within 30 seconds, THEN THE Purchases_Page SHALL hide BlockUI, show a SweetAlert2 error dialog with the error icon, keep the product row collapsed, and allow the user to retry by selecting the row again.
7. IF a product has no Product_Lines in the Selected_Period and Selected_Currency, THEN THE Purchases_Page SHALL expand the detail row showing an empty-state message indicating that no lines were found.
8. WHEN the Purchases_Page loads or the Selected_Period or Selected_Currency changes, THE Purchases_Page SHALL render a donut chart with the five products of highest revenue and one "Other" slice that sums the revenue of all remaining products.
9. WHERE the product mix has five or fewer products, THE Purchases_Page SHALL omit the "Other" slice.
10. WHEN the donut chart is rendered, THE Purchases_Page SHALL show a legend with the name and the share of each slice as a percentage rounded to one decimal place, and SHALL show the total revenue in the Selected_Currency in the centre of the chart.
11. THE Purchases_Page SHALL render each product row with a focusable control that exposes `aria-expanded` set to `true` when the detail row is expanded and `false` when it is collapsed, and that expands or collapses the detail row when the user presses Enter or Space.

### Requirement 14: Buying behaviour, rhythm and payment

**User Story:** As a sales user, I want rhythm and payment insights, so that I can time follow-ups and manage credit.

#### Acceptance Criteria

1. THE Purchases_Service SHALL compute buying rhythm per Product_Key from all Purchase_Dates of Counted_Invoices in the Selected_Currency, independent of the Selected_Period.
2. THE Purchases_Service SHALL treat several invoices on the same date for one Product_Key as a single Purchase_Date.
3. WHERE a Product_Key has at least 2 distinct Purchase_Dates, THE Purchases_Service SHALL compute its Typical_Gap as the median number of whole days between consecutive Purchase_Dates, averaging the two middle gaps when the number of gaps is even, rounded half up to the nearest whole day.
4. IF a Product_Key has fewer than 2 distinct Purchase_Dates, THEN THE Purchases_Page SHALL omit that product from the buying rhythm list and SHALL show no rhythm status for it.
5. THE Purchases_Service SHALL compute days since last purchase as the whole calendar days between the latest Purchase_Date and today (UTC), and the expected date as the latest Purchase_Date plus the Typical_Gap in days.
6. WHEN days since last purchase is less than 50% of the Typical_Gap, THE Purchases_Page SHALL show the status chip "On track".
7. WHEN days since last purchase is at least 50% of the Typical_Gap and the expected date is more than 7 days after today, THE Purchases_Page SHALL show the chip "Reorder around {expected date}".
8. WHEN days since last purchase is at least 50% of the Typical_Gap and the expected date is on or before the date 7 days after today (including any date before today), THE Purchases_Page SHALL show the chip "Reorder soon".
9. THE Purchases_Page SHALL list at most 5 products in the buying rhythm card, ordered by all-time revenue descending (ties broken by Product_Key ascending), and SHALL show for each its Typical_Gap in days, its days since last purchase and its chip.
10. THE Purchases_Service SHALL compute "Average days to pay" as the average of (date of the last valid Payment minus `InvoiceDate`) in whole days, rounded to the nearest whole day, over Counted_Invoices in the Selected_Period whose Outstanding_Balance is not greater than 0 (an overpaid invoice is settled) and that have at least one valid Payment, and SHALL show it next to the average payment terms, which is the average of (`DueDate` minus `InvoiceDate`) in whole days, rounded to the nearest whole day, over the same invoices.
11. WHEN the date of the last valid Payment of an invoice from Requirement 14.10 is on or before its `DueDate`, THE Purchases_Service SHALL count that invoice as "settled on time", and THE Purchases_Page SHALL show the count of on-time invoices, the total count of invoices from Requirement 14.10, and the percentage of on-time invoices rounded to the nearest whole number.
12. THE Purchases_Page SHALL show "Currently overdue" with the amount from Requirement 8.3, independent of the Selected_Period.
13. THE Purchases_Page SHALL show "Credit notes in the period" as the count and the total of credit notes of this customer whose status is Issued or Applied (Draft and Voided credit notes are excluded), dated within the Selected_Period, and whose currency, taken from the invoice the credit note is linked to, equals the Selected_Currency. Credit notes with no linked invoice are not counted. THE Purchases_Page SHALL show a count of 0 and a total of 0 when none exist.
14. IF no invoice qualifies for the payment metrics in Requirement 14.10, THEN THE Purchases_Page SHALL show "No settled invoices yet" instead of the averages, the on-time count and the percentage.
15. THE Purchases_Page SHALL show a note stating that an invoice is overdue when its balance is above zero after payments and credit notes and its due date is before today (UTC).

### Requirement 15: Teaser cards and interest capture

**User Story:** As a user, I want to register interest in upcoming insights, so that the product team knows what to build and I am told when it ships.

#### Acceptance Criteria

1. THE Purchases_Page SHALL display exactly two Teaser_Cards tagged LATER: "Discounts" with feature key `discounts` and "Price drift" with feature key `price-drift`.
2. THE Purchases_Page SHALL show on each Teaser_Card a description of 1 to 200 characters and a "Notify me when available" button, and SHALL NOT show computed customer figures on the Teaser_Cards in this release.
3. WHEN the user selects "Notify me when available", THE Purchases_Page SHALL show BlockUI, send an `AxPostRegisterFeatureInterest` request containing the feature key and the antiforgery token, and hide BlockUI when the response arrives or the request fails.
4. WHEN the server confirms the registration, THE Purchases_Page SHALL change the button text to "You will be notified", disable the button and show a SweetAlert2 dialog with the `success` icon.
5. WHEN a registration request is received for a combination of BusinessId, UserId and feature key that already has a row, THE Interest_Store SHALL keep the existing row unchanged, SHALL NOT create a second row, and THE Purchases_Controller SHALL return a success response.
6. WHEN the Purchases_Page loads and the current user already has a row for a feature key, THE Purchases_Page SHALL render that Teaser_Card with the disabled "You will be notified" button.
7. IF the feature key is empty, longer than 50 characters, or not one of `discounts` and `price-drift`, THEN THE Purchases_Controller SHALL reject the request with a validation error, SHALL NOT create a row in the Interest_Store, and THE Purchases_Page SHALL show a SweetAlert2 dialog with the `warning` icon.
8. IF the registration request fails because of a network error, a server error or a timeout, THEN THE Purchases_Page SHALL hide BlockUI, keep the button enabled with the text "Notify me when available", show a SweetAlert2 dialog with the `error` icon, and THE Interest_Store SHALL NOT contain a new row for that request.
9. THE Interest_Store SHALL be a new table `[insight].[FeatureInterest]` in a new `[insight]` schema, with the columns `Id` (INT IDENTITY primary key), `BusinessId` (INT NOT NULL, foreign key to the business table), `UserId` (NVARCHAR(450) NOT NULL, the Membership user identifier), `FeatureKey` (NVARCHAR(50) NOT NULL) and `CreatedAtUtc` (DATETIME NOT NULL DEFAULT GETUTCDATE()), with a unique index on (`BusinessId`, `UserId`, `FeatureKey`).
10. THE creation script for the Interest_Store SHALL begin with `USE [Portal]` and `GO`, SHALL create the `[insight]` schema when it does not exist, and the matching EF Core entity SHALL map `CreatedAtUtc` with `HasDefaultValueSql("GETUTCDATE()")`.
11. THE Portal SHALL treat the interest capture as feature-interest tracking only, and SHALL NOT evaluate alert rules or send notifications in this release.

### Requirement 16: Pending invoices table

**User Story:** As a credit controller, I want a list of unpaid invoices for this customer, so that I can see exactly what is owed and when.

#### Acceptance Criteria

1. WHEN the Purchases_Page loads or the Selected_Currency changes, THE Purchases_Page SHALL list every Pending_Invoice of the customer in the Selected_Currency, independent of the Selected_Period.
2. THE Purchases_Page SHALL show for each row the invoice number, the issue date, the due date, the total, the paid amount, the credit-note amount, the Outstanding_Balance and a status pill.
3. THE Purchases_Page SHALL show the invoice number in each row as a link that opens the detail page of that invoice.
4. IF an invoice is an Overdue_Invoice, THEN THE Purchases_Service SHALL set the status pill to "Overdue {N} days", where N is the whole number of calendar days (UTC) from `DueDate` to today, with a minimum value of 1. WHEN N is 1, THE Purchases_Service SHALL use "Overdue 1 day".
5. IF an invoice is not an Overdue_Invoice and has valid Payments or Applied_Credit greater than 0, THEN THE Purchases_Service SHALL set the status pill to "Partially paid, due in {N} days", where N is the whole number of calendar days (UTC) from today to `DueDate`. WHEN N is 1, THE Purchases_Service SHALL use "Partially paid, due in 1 day".
6. IF an invoice is not an Overdue_Invoice and has no valid Payments and Applied_Credit equal to 0, THEN THE Purchases_Service SHALL set the status pill to "Unpaid, due in {N} days", where N is the whole number of calendar days (UTC) from today to `DueDate`. WHEN N is 1, THE Purchases_Service SHALL use "Unpaid, due in 1 day".
7. IF an invoice is not an Overdue_Invoice and `DueDate` is today (UTC), THEN THE Purchases_Service SHALL replace "due in {N} days" in the status pill with "due today".
8. THE Purchases_Page SHALL order rows by `DueDate` ascending, so that the invoice with the earliest `DueDate` is first, and SHALL order rows with equal `DueDate` by invoice number ascending.
9. THE Purchases_Page SHALL show a footer row containing the sum of the Outstanding_Balance of all listed rows, which equals the value on the "Pending invoices" KPI_Card for the same Selected_Currency.
10. IF the customer has no Pending_Invoice in the Selected_Currency, THEN THE Purchases_Page SHALL show the message "No pending invoices" inside the card and SHALL NOT show the table header or the footer row.
11. WHILE the table is wider than its card, THE Purchases_Page SHALL show the table inside a horizontally scrollable container, so that all columns remain reachable without scrolling the whole page.

### Requirement 17: Excel export

**User Story:** As a sales manager, I want to export the product mix and pending invoices, so that I can share or analyse them offline.

#### Acceptance Criteria

1. WHEN the user selects "Export Excel", THE Purchases_Page SHALL request an `.xlsx` file for the Selected_Period and Selected_Currency.
2. THE Export_Service SHALL build the workbook with ClosedXML following the existing Excel export approach used in the Portal.
3. THE Export_Service SHALL write a "Product mix" sheet with the same rows, in the same order and with the same values as the on-screen product mix table, using the same grouping rules as Requirement 11, with the on-screen Product cell written as two columns (Product, Code) and the on-screen "Avg price vs list" cell written as three columns (Avg price paid, List price, Difference %), followed by Qty, Revenue, Share, Last bought and Margin, so that every monetary value is a numeric cell and Share, Difference % and Margin are numeric percentages.
4. THE Export_Service SHALL write a "Pending invoices" sheet with the same rows, columns and row order as the on-screen pending invoices table, including the total row.
5. THE Export_Service SHALL write the customer name, the Selected_Period, the Selected_Currency and the generation time in UTC in the rows above the table on each sheet, before the first table header row.
6. THE Export_Service SHALL write monetary values as numeric cells with a number format that shows exactly 2 decimal places, and SHALL NOT write them as text.
7. THE Purchases_Controller SHALL name the file `Purchases_{customer name}_{yyyyMMdd}.xlsx`, where `yyyyMMdd` is the UTC generation date and the customer name has characters that are invalid in file names removed.
8. IF the customer name is empty after invalid characters are removed, THEN THE Purchases_Controller SHALL use "Customer" in place of the customer name in the file name.
9. IF the export fails, THEN THE Purchases_Controller SHALL log the exception, THE Purchases_Controller SHALL return a failure response with no file, and THE Purchases_Page SHALL show a SweetAlert2 error dialog.
10. WHEN the export request is running, THE Purchases_Page SHALL show BlockUI and SHALL hide it when the download starts or the request fails.
11. THE Export_Service SHALL apply the same tenant check as Requirement 2 before building the workbook.
12. IF the tenant check fails, THEN THE Export_Service SHALL NOT build the workbook, THE Purchases_Controller SHALL return a failure response with no file, and THE Purchases_Page SHALL show a SweetAlert2 error dialog.

### Requirement 18: Empty and error states

**User Story:** As a user, I want clear messages when there is no data or something fails, so that I am never left with a blank or broken page.

#### Acceptance Criteria

1. IF the customer has zero Counted_Invoices in every currency, THEN THE Purchases_Page SHALL show exactly one empty-state card with the text "No invoices yet for this customer" and SHALL hide the filter card, the KPI strip, the chart, the product mix, the buying behaviour section and the pending invoices table.
2. WHERE the customer has at least one Counted_Invoice but zero Counted_Invoices in the Selected_Period, THE Purchases_Page SHALL show an empty-state message in each of the product mix, the donut and the buying rhythm sections, and SHALL still show the KPI_Cards that are independent of the Selected_Period with their values unchanged.
3. IF a data request fails, THEN THE Purchases_Controller SHALL catch `Exception ex`, log the exception through `SystemLoggerExtensions`, and return `success = false` with a general message that contains no exception message and no stack trace.
4. IF a period or currency refresh request fails or does not complete within 30 seconds, THEN THE Purchases_Page SHALL hide BlockUI, show a SweetAlert2 error dialog with the general message, and keep the previously shown data, Selected_Period and selected currency unchanged.
5. IF the Purchases_Page receives a response with `success = false` from a refresh request, THEN THE Purchases_Page SHALL hide BlockUI and show a SweetAlert2 error dialog, so that BlockUI is never left visible after a failure.
6. THE Purchases_Page SHALL NOT use native `alert()`, `confirm()` or `prompt()` dialogs.
7. THE Purchases_Repository SHALL rethrow every exception it catches using `throw;`, and every `catch` block in the feature SHALL declare `Exception ex`.
8. THE Purchases_Page SHALL NOT expose exception messages or stack traces to the user.

### Requirement 19: Architecture, data access and financial correctness

**User Story:** As a maintainer, I want the feature to follow the Portal's layering and financial rules, so that its numbers stay consistent with the rest of the system.

#### Acceptance Criteria

1. THE Purchases_Controller SHALL handle HTTP concerns only and SHALL delegate all calculations to the Purchases_Service, such that no arithmetic on monetary values, no status derivation and no date comparison for Overdue_Invoice classification is performed in the Purchases_Controller.
2. THE Purchases_Service SHALL read data only through the Purchases_Repository, such that the Purchases_Service holds no direct database context, connection or SQL text.
3. THE Purchases_Repository SHALL use parameterised SQL through `SqlParameter` with null-safe values (`?? (object)DBNull.Value` for every nullable parameter), and SHALL NOT build SQL by string concatenation of user input.
4. THE Purchases_Repository SHALL use full table names in SQL without table aliases, with the `[schema].[Table]` form in FROM and JOIN clauses and `Table.Column` in other clauses.
5. THE Purchases_Service SHALL compute Outstanding_Balance as total amount minus valid payments minus applied credit notes, using the `FinancialStatusEngine.ComputeOutstandingBalance` semantics, and SHALL compute Applied_Credit as the sum of non-voided (`IsVoided = 0`) credit note applications whose parent credit note belongs to the business, using the same joins as `DashboardService.GetKpiDataAsync` and `ReceivablesQueryService.GetReceivablesAsync`.
6. THE Purchases_Service SHALL exclude quotations and quotation lines from every calculation, such that adding or removing a quotation changes no KPI_Card value, table row, table footer or drill-down value by any amount.
7. THE Purchases_Service SHALL use one shared definition of Pending_Invoice (not a Written_Off_Invoice AND Outstanding_Balance greater than 0) and Overdue_Invoice (not a Written_Off_Invoice AND Outstanding_Balance greater than 0 AND due date earlier than today in UTC, derived at calculation time and not read from the persisted invoice financial status, which is read only to recognise a Written_Off_Invoice) for the KPI cards, the pending invoices table, the payment behaviour card and the drill-down status.
8. WHEN the same inputs are provided, THE Purchases_Service SHALL return values for the "Pending invoices" KPI_Card and the footer of the pending invoices table that are equal in both count and monetary total, to the currency's smallest unit (2 decimal places).
9. IF the Purchases_Repository throws an exception during a data read, THEN THE Purchases_Repository SHALL rethrow the exception without swallowing it, AND THE Purchases_Controller SHALL log the error and return an error response, AND no partial or zero-filled KPI_Card values SHALL be shown as if they were valid.

### Requirement 20: Performance

**User Story:** As a user, I want the page to open quickly, so that I can use it during a customer call.

#### Acceptance Criteria

1. WHEN the Purchases_Page is requested, THE Purchases_Controller SHALL load every dashboard section in a single server round trip, excluding product drill-down lines which load on demand.
2. THE Purchases_Repository SHALL filter invoice reads by `BusinessId` and `CustomerId`.
3. THE Purchases_Repository SHALL read invoice lines for all invoices of the customer in set-based queries, and SHALL NOT run a query per invoice or per product.
4. IF an equivalent index does not exist on `[invoice].[Invoice]` covering (`BusinessId`, `CustomerId`, `InvoiceDate`), THEN THE Portal SHALL provide that index.
5. IF an equivalent index does not exist on `[invoice].[InvoiceLine]` on (`InvoiceId`) that includes the columns used by the product mix, THEN THE Portal SHALL provide that index.
6. WHEN a customer has up to 1000 Counted_Invoices and the Purchases_Page is requested, THE Purchases_Page SHALL return the complete initial response within 2000 milliseconds at the 95th percentile, measured on the production database over a sample of at least 100 requests.
7. WHEN the user changes the Period or Currency, THE Purchases_Page SHALL refresh the dashboard sections without a full page reload, and SHALL complete the refresh within 2000 milliseconds at the 95th percentile for a customer with up to 1000 Counted_Invoices.
8. IF the refresh after a Period or Currency change fails, THEN THE Purchases_Page SHALL keep the previously displayed data and the previous Period and Currency selections, and SHALL display an error message indicating that the refresh failed.

### Requirement 21: Responsive layout and accessibility

**User Story:** As a user on a phone or tablet, I want the dashboard to remain usable, so that I can check a customer on the move.

#### Acceptance Criteria

1. WHILE the viewport width is 1400px or more, THE Purchases_Page SHALL show the KPI strip in exactly 6 columns (one row).
2. WHILE the viewport width is between 561px and 1399px inclusive, THE Purchases_Page SHALL show the KPI strip in exactly 3 columns.
3. WHILE the viewport width is 560px or less, THE Purchases_Page SHALL show the KPI strip in exactly 2 columns.
4. THE Purchases_Page SHALL show the product mix card at the full content width, SHALL show the monthly trend card and the share-of-spend card side by side WHILE the viewport width is 1400px or more and stacked WHILE it is 1399px or less, and SHALL stack the two-column behaviour cards WHILE the viewport width is 900px or less.
5. THE Purchases_Page SHALL place every table in a container that scrolls horizontally when the table width exceeds the container width, so that no table column is clipped and the page itself does not scroll horizontally.
6. WHEN the header buttons do not fit in one row, THE Purchases_Page SHALL wrap them onto additional rows, so that every header button is fully visible and none is clipped or overlapped.
7. THE Purchases_Page SHALL use Manrope for headings, Inter for body text, and the colours #0D5EA6, #57B8E8, #129867, #C8912E and #C24A4A as the MyChair design system accent colours.
8. THE Purchases_Page SHALL provide a text alternative for each chart that states the chart title and its data values, so that the chart content is available without viewing the chart.
9. THE Purchases_Page SHALL pair every status colour with a visible text label, so that no status is conveyed by colour alone.
10. IF a viewport width changes across a breakpoint (1400px, 900px or 560px) while the Purchases_Page is displayed, THEN THE Purchases_Page SHALL apply the layout for the new width without a page reload and without losing the current filter selections or scroll position within tables.

## Decisions Recorded

- Written-off invoices (persisted status 5) are excluded from Pending and Overdue figures, the pending table and the payment-behaviour overdue amount.
- "Credit notes in the period" counts only Issued or Applied credit notes, with currency taken from the linked invoice; Draft, Voided and unlinked credit notes are not counted.
- List prices are shown only when the Selected_Currency equals the Catalogue_Currency (the business's most-used invoice currency); otherwise "-" is shown.

## Open Points for Review

- Revenue basis: this document defines Net_Revenue as `TotalAmount` (including tax) minus Applied_Credit, while product revenue uses `LineTotal`. The two can differ by VAT and credit notes, and Requirement 11.11 tells the user so. If the business wants ex-VAT figures everywhere, Net_Revenue should use `Subtotal` instead.
- Schema name: `[insight]` is proposed for the new table. It follows the lowercase style of `[invoice]` and `[credit]`. Confirm before the script is written.
- Rhythm thresholds (50% of the Typical_Gap and 7 days) are proposed values. The mockup sample chips are illustrative and do not all follow one rule.
