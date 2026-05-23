# Requirements Document

## Introduction

This feature adds granular filtering controls to the purchases table on the Supplier Dashboard page (`/Supplier/Dashboard/{id}`). Currently the page only supports a coarse VAT Period filter that scopes the entire dashboard. The new filters allow users to narrow down the purchases table by description text, expense category, and date range — without affecting the KPI cards or charts. These filters work alongside the existing period filter and integrate with the existing server-side pagination.

## Glossary

- **Filter_Panel**: The UI section rendered as a `.glass.card-pad` card containing the purchase filter controls (description search, category dropdown, date range pickers) and action buttons.
- **Dashboard_Controller**: The `SupplierController.Dashboard` action that handles HTTP GET requests for the Supplier Dashboard page.
- **Dashboard_Service**: The `SupplierDashboardService` class responsible for computing dashboard analytics and fetching paginated purchases.
- **Purchase_Query**: The LINQ query within the Dashboard_Service that retrieves purchases for the table, already scoped by supplier and period.
- **Expense_Category**: A classification entity (`ExpenseCategory`) belonging to the current business, used to categorise purchases.
- **Pagination_Control**: The existing pagination links rendered below the purchases table that navigate between pages of results.

## Requirements

### Requirement 1: Description Search Filter

**User Story:** As a business user, I want to search purchases by description text, so that I can quickly locate specific expense entries within a supplier's purchase history.

#### Acceptance Criteria

1. THE Filter_Panel SHALL render a text input labelled "Description" with a placeholder of "Search description..." and a maximum input length of 200 characters.
2. WHEN the user submits the filter form with a description value, THE Dashboard_Controller SHALL accept a `description` query string parameter and apply it alongside any active `periodId` and `page` parameters.
3. WHEN a description filter value is provided, THE Purchase_Query SHALL return only purchases whose Description column contains the filter value as a case-insensitive substring, combined with any active period filter using AND logic.
4. WHEN the description filter value is empty or whitespace-only, THE Purchase_Query SHALL treat the filter as not applied and return all purchases matching other active filters.
5. WHEN a description filter value exceeds 200 characters, THE Dashboard_Controller SHALL truncate the value to 200 characters before applying the filter.
6. WHEN a description filter is submitted, THE Dashboard_Controller SHALL reset pagination to page 1 before returning results.
7. WHEN a description filter is active, THE Filter_Panel SHALL preserve the submitted description value in the text input after page load and THE Dashboard_Controller SHALL include the description parameter in all pagination link URLs.
8. WHEN the user clicks the "Clear" button on the Filter_Panel, THE Dashboard_Controller SHALL remove the description parameter from the query string and reset pagination to page 1.

### Requirement 2: Category Dropdown Filter

**User Story:** As a business user, I want to filter purchases by expense category, so that I can review spending within a specific cost classification for a supplier.

#### Acceptance Criteria

1. THE Filter_Panel SHALL render a dropdown labelled "Category" with a default option of "All Categories" that submits no categoryId parameter value.
2. THE Filter_Panel SHALL populate the category dropdown with all Expense_Category records belonging to the current business where IsActive equals true, ordered alphabetically by name ascending.
3. WHEN the user submits the filter form with a category value, THE Dashboard_Controller SHALL accept a `categoryId` query string parameter of type nullable integer.
4. IF the categoryId query string parameter is present but cannot be parsed as an integer, THEN THE Dashboard_Controller SHALL ignore the filter and treat it as not applied.
5. WHEN a categoryId is provided that corresponds to an active Expense_Category belonging to the current business, THE Purchase_Query SHALL return only purchases whose ExpenseCategoryId matches the provided value.
6. IF the categoryId does not correspond to an active Expense_Category belonging to the current business, THEN THE Dashboard_Controller SHALL ignore the filter and treat it as not applied.
7. WHEN the user changes the category filter selection and submits, THE Paging_Control SHALL reset to page 1.
8. WHEN a category filter is active, THE Filter_Panel SHALL preserve the selected option in the dropdown after page load, and THE Server SHALL include the categoryId parameter in all pagination links so that page navigation retains the active category filter.

### Requirement 3: Date Range Filter

**User Story:** As a business user, I want to filter purchases by a date range, so that I can review spending within a specific time window for a supplier.

#### Acceptance Criteria

1. THE Filter_Panel SHALL render two date inputs of type `date` labelled "From" and "To" for specifying the start and end of the date range.
2. WHEN the user submits the filter form with a `dateFrom` value, THE Dashboard_Controller SHALL accept a `dateFrom` query string parameter of type nullable DateOnly.
3. WHEN the user submits the filter form with a `dateTo` value, THE Dashboard_Controller SHALL accept a `dateTo` query string parameter of type nullable DateOnly.
4. WHEN a dateFrom value is provided, THE Purchase_Query SHALL return only purchases whose InvoiceDate is on or after the dateFrom value.
5. WHEN a dateTo value is provided, THE Purchase_Query SHALL return only purchases whose InvoiceDate is on or before the dateTo value.
6. IF both dateFrom and dateTo are provided and dateFrom is later than dateTo, THEN THE Dashboard_Controller SHALL ignore both date filters and treat them as not applied, returning results as if neither date parameter was supplied.
7. WHEN a date range filter is active, THE Filter_Panel SHALL preserve the submitted date values in the respective date inputs after page load.
8. WHEN both a date range filter and a periodId filter are active simultaneously, THE Purchase_Query SHALL apply both filters as a logical AND, returning only purchases whose InvoiceDate falls within the date range AND whose VatSubmissionPeriodId matches the selected period.

### Requirement 4: Filter Combination and Pagination Integration

**User Story:** As a business user, I want all purchase filters to work together and with pagination, so that I can progressively narrow results and navigate through filtered pages.

#### Acceptance Criteria

1. WHEN multiple filters are active simultaneously, THE Purchase_Query SHALL apply all active filters using logical AND — returning only purchases that satisfy every active filter condition, regardless of the order in which filters were specified.
2. WHEN the user submits the filter form, THE Pagination_Control SHALL reset the page to 1.
3. WHEN the user navigates between pages, THE Pagination_Control SHALL include all active filter parameters (`description`, `categoryId`, `dateFrom`, `dateTo`) and the current `periodId` in the pagination URL query string so that the next page reflects the same filtered dataset.
4. THE Dashboard_Controller SHALL accept all filter parameters (`description` as string, `categoryId` as nullable integer, `dateFrom` as nullable DateOnly, `dateTo` as nullable DateOnly) alongside the existing `periodId` and `page` parameters, treating absent or null parameters as inactive filters.
5. WHEN the "Clear" button is clicked, THE Filter_Panel SHALL remove all purchase filter values (`description`, `categoryId`, `dateFrom`, `dateTo`) and redirect to the dashboard URL at page 1 with only the current `periodId` preserved if it was active.
6. WHEN purchase filters are active, THE Dashboard_Service SHALL apply those filters only to the purchases table query — the KPI cards, spend share chart, monthly spend chart, and period spend chart SHALL remain scoped solely by the `periodId` filter and SHALL NOT be affected by purchase filter values.

### Requirement 5: Filter Panel Layout and Presentation

**User Story:** As a business user, I want the filter controls to be visually consistent with the rest of the portal, so that the interface feels cohesive and intuitive.

#### Acceptance Criteria

1. THE Filter_Panel SHALL be rendered as a `section` element with classes `glass card-pad` positioned in the DOM after the KPI cards section and before the purchases table section.
2. THE Filter_Panel SHALL use a flexbox layout with `gap:14px`, `align-items:flex-end`, and `flex-wrap:wrap`.
3. THE Filter_Panel SHALL render each filter field inside a `.field` wrapper element with a minimum width of 180px.
4. THE Filter_Panel SHALL render a "Filter" button with class `btn btn-primary` and a "Clear" button with class `btn btn-secondary`, both wrapped in a container with `padding-bottom:2px` to align with the input field baselines.
5. THE Filter_Panel SHALL use `margin-bottom:22px` to separate it from the purchases table card below.
6. WHEN no purchases match the active filters, THE purchases table section SHALL display the text "No purchases found." inside a `.empty-state` container within the data table card, and SHALL not render the purchases table or pagination controls.
