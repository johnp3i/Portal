# Requirements Document

## Introduction

This feature adds server-side pagination and a name-based search filter to the Supplier Index page (`/Supplier`). Currently, the page loads all suppliers in a single table without any filtering or pagination controls. As the supplier registry grows, this becomes difficult to navigate. Adding a search input to filter by supplier name and server-side paging will improve usability and maintain consistent performance regardless of data volume. The implementation reuses the existing shared `PagedResult<T>` model and `_PagingControl.cshtml` partial view established in the table-paging-search spec.

## Glossary

- **Supplier_List_Page**: The Index view at `/Supplier` that displays all suppliers in a tabular format with Name, Status, Created, and Actions columns
- **Paging_Control**: The shared UI component (`_PagingControl.cshtml`) displaying page navigation (previous, next, page numbers) and current page information
- **Search_Input**: A text input field that allows users to filter suppliers by matching against the supplier name
- **Page_Size**: The number of records displayed per page (default 15)
- **Server**: The ASP.NET Core MVC backend (SupplierController and SupplierService) that processes paging and search parameters and returns the appropriate data subset
- **Supplier_Repository**: The data access layer that queries the `[purchase].[Supplier]` table with pagination and filtering

## Requirements

### Requirement 1: Server-Side Pagination for Supplier List

**User Story:** As a portal user, I want the supplier list to display records in pages, so that I can navigate large supplier registries efficiently without loading all records at once.

#### Acceptance Criteria

1. WHEN the Supplier_List_Page loads, THE Server SHALL return only the first page of suppliers ordered by supplier name ascending, limited to the configured Page_Size (default 15, maximum 100)
2. WHEN a user navigates to a specific page number, THE Server SHALL return only the records for that page based on the current Page_Size and active search filter
3. IF the requested page number is less than 1 or exceeds the total page count, THEN THE Server SHALL return the first page of results
4. THE Paging_Control SHALL display the current page number, total page count, and total record count
5. WHEN the user clicks the "Next" button and a next page exists, THE Paging_Control SHALL navigate to the next page
6. WHEN the user clicks the "Previous" button and a previous page exists, THE Paging_Control SHALL navigate to the previous page
7. WHEN the user is on the first page, THE Paging_Control SHALL disable the "Previous" button
8. WHEN the user is on the last page, THE Paging_Control SHALL disable the "Next" button
9. WHEN a search filter is applied alongside pagination, THE Server SHALL filter suppliers where the supplier name contains the search text (case-insensitive partial match), apply the filter first, and then paginate the filtered result set
10. WHEN the user submits a search, THE Paging_Control SHALL reset to page 1
11. WHEN the user navigates to a different page, THE Server SHALL preserve the active search term in the URL query string and THE Supplier_List_Page SHALL pre-populate the search input with the value from the query string
12. IF the filtered result set contains zero records, THEN THE Supplier_List_Page SHALL display an empty state message within the content area and THE Paging_Control SHALL not render navigation buttons

### Requirement 2: Text Search for Supplier List

**User Story:** As a portal user, I want to search suppliers by name, so that I can quickly find a specific supplier without scrolling through pages.

#### Acceptance Criteria

1. THE Supplier_List_Page SHALL display a Search_Input within a filter panel above the data table
2. WHEN the user enters text (1 to 200 characters) into the Search_Input and submits, THE Server SHALL filter suppliers where the supplier name contains the search text (case-insensitive partial match) and reset pagination to page 1
3. WHEN the Search_Input is empty, THE Server SHALL return all suppliers (subject to pagination)
4. WHEN a search term is active, THE Search_Input SHALL retain the entered value after the page reloads
5. THE filter panel SHALL include a "Search" button to submit the filter and a "Clear" button to reset the search term and return to page 1
6. WHEN no suppliers match the search term, THE Supplier_List_Page SHALL display an empty state message indicating no results were found

### Requirement 3: Pagination State Preservation

**User Story:** As a portal user, I want my search term to persist when I navigate between pages, so that I do not lose my filtering context.

#### Acceptance Criteria

1. WHEN the user navigates to a different page, THE Server SHALL preserve the active search term and all active filter parameters in the URL query string so that the resulting page reflects the same filtered dataset
2. WHEN the page loads with a search query string parameter that contains a non-empty value (1 to 200 characters), THE Supplier_List_Page SHALL pre-populate the Search_Input with the decoded value from the query string
3. IF the page loads with a search query string parameter that is empty or absent, THEN THE Supplier_List_Page SHALL display the Search_Input in its default empty state and apply no search filter
4. WHEN the user clicks the "Clear" button, THE Supplier_List_Page SHALL remove the search term and all filter parameters from the URL and navigate to page 1 with no active filters

### Requirement 4: Layout and Design Compliance

**User Story:** As a portal user, I want the supplier page to follow the same visual patterns as other list pages, so that the interface feels consistent and professional.

#### Acceptance Criteria

1. THE filter panel SHALL be rendered in a separate `.glass.card-pad` section with `margin-bottom:22px` above the data table card
2. THE filter panel SHALL use flexbox layout with `gap:14px`, `align-items:flex-end`, and `flex-wrap:wrap`, and filter action buttons SHALL be wrapped in a container with `padding-bottom:2px` to align with input baselines
3. THE Search_Input field SHALL have a minimum width of 180px
4. THE Paging_Control SHALL be rendered below the table within the same data table card using the shared `_PagingControl.cshtml` partial view, passing ViewData keys: `CurrentPage`, `TotalPages`, `TotalCount`, `PageSize`, `HasPreviousPage`, and `HasNextPage`
5. WHEN no suppliers match the current search filter, THE Supplier_List_Page SHALL display an empty state message inside a `.empty-state` container within the data table card, indicating that no suppliers were found for the current search term

