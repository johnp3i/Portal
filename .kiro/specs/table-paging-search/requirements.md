# Requirements Document

## Introduction

This feature adds server-side pagination and text-based search filtering to the Invoice and Quotation list tables in the Portal application. It also corrects a CSS layout issue where the dropdown arrow (chevron) on filter select elements is mispositioned to the far right of the container instead of being adjacent to the dropdown content.

Currently, both list views load all records at once without pagination, which degrades performance as data grows and makes it difficult for users to locate specific records. Adding paging with configurable page sizes and a free-text search input will improve usability and performance. The dropdown arrow fix ensures the filter controls render correctly within the MyChair Design System.

## Glossary

- **Invoice_List_Page**: The Index view at `/Invoice` that displays all invoices in a tabular format with filter controls
- **Quotation_List_Page**: The Index view at `/Quotation` that displays all quotations in a tabular format with filter controls
- **Paging_Control**: A UI component displaying page navigation (previous, next, page numbers) and current page information
- **Search_Input**: A text input field that allows users to filter table records by matching against multiple text columns
- **Page_Size**: The number of records displayed per page (default 15)
- **Filter_Dropdown**: The `<select>` elements used for status, financial status, and customer filtering on the list pages
- **Server**: The ASP.NET Core MVC backend that processes paging, search, and filter parameters and returns the appropriate data subset

## Requirements

### Requirement 1: Server-Side Pagination for Invoice List

**User Story:** As a portal user, I want the invoice list to display records in pages, so that I can navigate large datasets efficiently without loading all records at once.

#### Acceptance Criteria

1. WHEN the Invoice_List_Page loads, THE Server SHALL return only the first page of invoices limited to the configured Page_Size (default 15)
2. WHEN a user navigates to a specific page number, THE Server SHALL return only the records for that page based on the current Page_Size and active filters
3. THE Paging_Control SHALL display the current page number, total page count, and total record count
4. WHEN the user clicks the "Next" button and a next page exists, THE Paging_Control SHALL navigate to the next page
5. WHEN the user clicks the "Previous" button and a previous page exists, THE Paging_Control SHALL navigate to the previous page
6. WHEN the user is on the first page, THE Paging_Control SHALL disable the "Previous" button
7. WHEN the user is on the last page, THE Paging_Control SHALL disable the "Next" button
8. WHEN filters are applied alongside pagination, THE Server SHALL apply filters first and then paginate the filtered result set
9. WHEN the user changes a filter or submits a search, THE Paging_Control SHALL reset to page 1

### Requirement 2: Server-Side Pagination for Quotation List

**User Story:** As a portal user, I want the quotation list to display records in pages, so that I can navigate large datasets efficiently without loading all records at once.

#### Acceptance Criteria

1. WHEN the Quotation_List_Page loads, THE Server SHALL return only the first page of quotations limited to the configured Page_Size (default 15)
2. WHEN a user navigates to a specific page number, THE Server SHALL return only the records for that page based on the current Page_Size and active filters
3. THE Paging_Control SHALL display the current page number, total page count, and total record count
4. WHEN the user clicks the "Next" button and a next page exists, THE Paging_Control SHALL navigate to the next page
5. WHEN the user clicks the "Previous" button and a previous page exists, THE Paging_Control SHALL navigate to the previous page
6. WHEN the user is on the first page, THE Paging_Control SHALL disable the "Previous" button
7. WHEN the user is on the last page, THE Paging_Control SHALL disable the "Next" button
8. WHEN filters are applied alongside pagination, THE Server SHALL apply filters first and then paginate the filtered result set
9. WHEN the user changes a filter or submits a search, THE Paging_Control SHALL reset to page 1

### Requirement 3: Text Search for Invoice List

**User Story:** As a portal user, I want to search invoices by text, so that I can quickly find a specific invoice by number or customer name without scrolling through pages.

#### Acceptance Criteria

1. THE Invoice_List_Page SHALL display a Search_Input above the table
2. WHEN the user enters text into the Search_Input and submits, THE Server SHALL filter invoices where the invoice number or customer name contains the search text (case-insensitive)
3. WHEN the Search_Input is empty, THE Server SHALL return all invoices (subject to other active filters and pagination)
4. WHEN a search term is active, THE Search_Input SHALL retain the entered value after the page reloads
5. WHEN a search term is combined with dropdown filters, THE Server SHALL apply all filters together (AND logic) before pagination

### Requirement 4: Text Search for Quotation List

**User Story:** As a portal user, I want to search quotations by text, so that I can quickly find a specific quotation by reference or customer name without scrolling through pages.

#### Acceptance Criteria

1. THE Quotation_List_Page SHALL display a Search_Input above the table
2. WHEN the user enters text into the Search_Input and submits, THE Server SHALL filter quotations where the reference or customer name contains the search text (case-insensitive)
3. WHEN the Search_Input is empty, THE Server SHALL return all quotations (subject to other active filters and pagination)
4. WHEN a search term is active, THE Search_Input SHALL retain the entered value after the page reloads
5. WHEN a search term is combined with dropdown filters, THE Server SHALL apply all filters together (AND logic) before pagination

### Requirement 5: Fix Filter Dropdown Arrow Positioning

**User Story:** As a portal user, I want the filter dropdown arrows to appear correctly next to the dropdown content, so that the interface looks polished and the controls are visually clear.

#### Acceptance Criteria

1. THE Filter_Dropdown SHALL render the chevron arrow icon immediately adjacent to the right edge of the select element content area (with standard padding)
2. THE Filter_Dropdown SHALL constrain its width to fit within the form grid column rather than stretching to the full page width
3. THE Filter_Dropdown chevron SHALL remain vertically centered within the select element
4. WHEN the page is viewed on screens narrower than 1100px, THE Filter_Dropdown SHALL stack vertically and maintain correct arrow positioning

### Requirement 6: Pagination State Preservation

**User Story:** As a portal user, I want my filter selections and search terms to persist when I navigate between pages, so that I do not lose my filtering context.

#### Acceptance Criteria

1. WHEN the user navigates to a different page, THE Server SHALL preserve all active filter values (status, financial status, customer, date range, search term) in the URL query string
2. WHEN the page loads with query string parameters, THE Invoice_List_Page SHALL pre-populate all filter controls and the Search_Input with the values from the query string
3. WHEN the page loads with query string parameters, THE Quotation_List_Page SHALL pre-populate all filter controls and the Search_Input with the values from the query string
