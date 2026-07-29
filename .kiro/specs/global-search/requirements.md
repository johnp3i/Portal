# Requirements Document

## Introduction

Global Search provides a universal search bar embedded in the application topbar, available on all authenticated pages. It enables users to quickly locate Invoices, Customers, Purchases, Quotations, Suppliers, and Products from a single input field. Results are scoped to the user's current business (tenant isolation) and filtered by module access permissions. The feature uses parallel per-entity queries, a debounced frontend input, and a keyboard-navigable dropdown panel.

## Glossary

- **Search_Bar**: The text input element rendered inside the topbar on every authenticated page, accepting free-text queries for the global search feature.
- **Search_Controller**: A dedicated ASP.NET Core MVC controller (`SearchController`) responsible for handling global search AJAX requests.
- **Search_Service**: The service-layer component that orchestrates parallel per-entity search queries and aggregates results.
- **Search_Dropdown**: The floating panel that appears below the Search_Bar displaying grouped search results, loading indicators, or empty-state messages.
- **Current_Business**: The active tenant (business) context derived from the authenticated user's session, used to scope all search queries.
- **Module_Permission**: The access control rule that determines which entity types a user is authorised to search, based on their granted module access.
- **Debounce_Interval**: The 300-millisecond delay applied to keystrokes before dispatching a search request, preventing excessive calls during rapid typing.
- **Entity_Group**: A labelled section within the Search_Dropdown that displays results for a single entity type (e.g., Invoices, Customers).

## Requirements

### Requirement 1: Search Bar Visibility

**User Story:** As an authenticated user, I want a search bar always visible in the topbar, so that I can initiate a search from any page without navigating away.

#### Acceptance Criteria

1. THE Search_Bar SHALL render inside the topbar on every authenticated page.
2. THE Search_Bar SHALL display placeholder text indicating its purpose (e.g., "Search invoices, customers, products...").
3. THE Search_Bar SHALL be visually styled according to the approved mockup at `.kiro/docs/mockups/global-search.html`.

---

### Requirement 2: Keyboard Shortcut to Focus

**User Story:** As a power user, I want to press Ctrl+K to focus the search bar, so that I can start searching without reaching for the mouse.

#### Acceptance Criteria

1. WHEN the user presses Ctrl+K on any authenticated page, THE Search_Bar SHALL receive input focus.
2. WHEN the Search_Bar receives focus via Ctrl+K, THE Search_Dropdown SHALL open in its empty state.
3. IF another element has focus when Ctrl+K is pressed, THEN THE Search_Bar SHALL override that focus and become the active element.

---

### Requirement 3: Debounced Query Dispatch

**User Story:** As a user, I want my keystrokes debounced before sending a search request, so that the system avoids unnecessary load during rapid typing.

#### Acceptance Criteria

1. WHILE the user is typing in the Search_Bar, THE Search_Service SHALL defer the search request until the Debounce_Interval of 300 milliseconds elapses without additional input.
2. WHEN the Debounce_Interval elapses, THE Search_Bar SHALL dispatch a single AJAX request to the Search_Controller with the current query text.
3. WHEN a new keystroke occurs before the Debounce_Interval elapses, THE Search_Bar SHALL reset the timer and discard the previous pending request.

---

### Requirement 4: Backend Search Endpoint

**User Story:** As a developer, I want a single backend endpoint that handles global search requests, so that the frontend has one consistent API to call.

#### Acceptance Criteria

1. THE Search_Controller SHALL expose an HTTP GET endpoint named `AxGetGlobalSearch` accepting a `query` string parameter.
2. WHEN the `query` parameter is null or fewer than 2 characters, THE Search_Controller SHALL return an empty result set without executing any database queries.
3. WHEN a valid query is received, THE Search_Controller SHALL delegate execution to the Search_Service and return the aggregated results as JSON.
4. IF an unhandled exception occurs during search execution, THEN THE Search_Controller SHALL return a JSON response with `success: false` and a generic error message.

---

### Requirement 5: Tenant Isolation

**User Story:** As a business owner, I want search results scoped to my business only, so that I never see data belonging to another tenant.

#### Acceptance Criteria

1. THE Search_Service SHALL filter all entity queries by the Current_Business identifier derived from the authenticated user's session.
2. THE Search_Service SHALL never return results belonging to a business other than the Current_Business.

---

### Requirement 6: Module Access Filtering

**User Story:** As an administrator, I want search results restricted by the user's module permissions, so that users only discover entities they are authorised to access.

#### Acceptance Criteria

1. BEFORE executing entity queries, THE Search_Service SHALL determine which entity types the authenticated user has Module_Permission to access.
2. THE Search_Service SHALL execute queries only for entity types the user is permitted to access.
3. WHEN a user lacks permission for all entity types, THE Search_Controller SHALL return an empty result set.

---

### Requirement 7: Parallel Entity Queries

**User Story:** As a user, I want search results returned quickly, so that the search experience feels responsive.

#### Acceptance Criteria

1. THE Search_Service SHALL execute all permitted entity queries in parallel using `Task.WhenAll`.
2. THE Search_Service SHALL aggregate results from all completed entity queries into a single response grouped by entity type.
3. IF one entity query fails, THEN THE Search_Service SHALL return results from the remaining successful queries and exclude the failed entity type from the response.

---

### Requirement 8: Entity Search Scope

**User Story:** As a user, I want to search across multiple entity types by relevant fields, so that I can find records regardless of which module they belong to.

#### Acceptance Criteria

1. WHEN searching Invoices, THE Search_Service SHALL match the query against InvoiceNumber and CustomerName columns.
2. WHEN searching Customers, THE Search_Service SHALL match the query against Name and Email columns.
3. WHEN searching Purchases, THE Search_Service SHALL match the query against InvoiceNumber, Description, and SupplierName columns.
4. WHEN searching Quotations, THE Search_Service SHALL match the query against QuotationNumber and CustomerName columns.
5. WHEN searching Suppliers, THE Search_Service SHALL match the query against Name column.
6. WHEN searching Products, THE Search_Service SHALL match the query against Name and SKU columns.
7. THE Search_Service SHALL use SQL LIKE pattern matching on indexed columns for all entity searches.

---

### Requirement 9: Result Limit Per Entity Type

**User Story:** As a user, I want concise results so that the dropdown remains manageable and fast to scan.

#### Acceptance Criteria

1. THE Search_Service SHALL return a maximum of 5 results per Entity_Group.
2. THE Search_Service SHALL apply the TOP 5 limit at the database query level to avoid fetching excess rows.

---

### Requirement 10: Grouped Result Display

**User Story:** As a user, I want results grouped by entity type, so that I can quickly identify which category a result belongs to.

#### Acceptance Criteria

1. THE Search_Dropdown SHALL display results organised into Entity_Group sections.
2. EACH Entity_Group SHALL display a header label identifying the entity type (e.g., "Invoices", "Customers").
3. WHEN an Entity_Group has zero results, THE Search_Dropdown SHALL omit that group from the display.

---

### Requirement 11: Keyboard Navigation

**User Story:** As a power user, I want to navigate search results with the keyboard, so that I can select a result without using the mouse.

#### Acceptance Criteria

1. WHEN the Search_Dropdown is open, THE Search_Bar SHALL support arrow-key navigation (Up/Down) across all visible result items.
2. WHEN the user presses the Down arrow, THE Search_Dropdown SHALL move the highlight to the next result item, cycling through Entity_Group boundaries.
3. WHEN the user presses the Up arrow, THE Search_Dropdown SHALL move the highlight to the previous result item.
4. WHEN the user presses Enter on a highlighted item, THE Search_Dropdown SHALL navigate to the detail page of that result.
5. WHEN the user presses Escape, THE Search_Dropdown SHALL close and return focus to the page.

---

### Requirement 12: Click Navigation

**User Story:** As a user, I want to click a search result to navigate to its detail page, so that I can quickly access the record I need.

#### Acceptance Criteria

1. WHEN the user clicks a result item in the Search_Dropdown, THE Search_Bar SHALL navigate the browser to the detail page URL corresponding to that entity and record.
2. THE Search_Dropdown SHALL close after navigation is initiated.

---

### Requirement 13: Dropdown States

**User Story:** As a user, I want clear visual feedback about the search state, so that I understand what is happening at all times.

#### Acceptance Criteria

1. WHEN the Search_Bar has focus and the query is empty, THE Search_Dropdown SHALL display an empty state (open panel with no results and no loading indicator).
2. WHILE a search request is in progress, THE Search_Dropdown SHALL display a loading indicator.
3. WHEN results are returned, THE Search_Dropdown SHALL display the grouped results replacing the loading indicator.
4. WHEN zero results are returned for a non-empty query, THE Search_Dropdown SHALL display a "No results found" message.

---

### Requirement 14: Dropdown Dismissal

**User Story:** As a user, I want the dropdown to close when I interact outside of it, so that it does not obstruct the page content.

#### Acceptance Criteria

1. WHEN the user clicks outside the Search_Bar and Search_Dropdown, THE Search_Dropdown SHALL close.
2. WHEN the user presses Escape, THE Search_Dropdown SHALL close.
3. WHEN the Search_Bar loses focus (blur) and the click target is not within the Search_Dropdown, THE Search_Dropdown SHALL close.

---

### Requirement 15: BlockUI and Error Handling on Frontend

**User Story:** As a user, I want the interface to remain responsive and inform me of errors, so that I am never left confused by a failed search.

#### Acceptance Criteria

1. THE Search_Bar SHALL NOT use BlockUI for search requests (search uses an inline loading indicator within the dropdown instead of a full-page block).
2. IF the AJAX request to the Search_Controller fails due to a network or server error, THEN THE Search_Dropdown SHALL display an error message within the dropdown panel.
3. THE Search_Bar SHALL NOT use `alert()`, `confirm()`, or `prompt()` for any user-facing message.
