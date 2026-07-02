# Requirements Document

## Introduction

The Reminder History page provides a comprehensive, paginated log of all payment reminders sent by the current business. It allows users to search, filter, and review reminder activity across all invoices — including sent/failed status, escalation tier, delivery method, and open tracking results. The page lives under the Payment Reminders navigation section and reuses the established layout pattern (topbar → filter card → data table card → pagination).

## Glossary

- **History_Page**: The Razor view at `Views/PaymentReminder/History.cshtml` that renders the Reminder History interface
- **History_Endpoint**: The AJAX controller action `AxGetAllReminderHistory` on `PaymentReminderController` that returns paginated, filtered reminder log data
- **Reminder_Log**: A single record in the `[reminder].[PaymentReminderLog]` table representing one sent (or attempted) reminder email
- **Tier_Filter**: A dropdown allowing the user to filter by escalation tier (All, Friendly, Firm, Formal)
- **Status_Filter**: A dropdown allowing the user to filter by send status (All, Sent, Failed)
- **Method_Filter**: A dropdown allowing the user to filter by delivery method (All, Auto, Manual, Test)
- **Date_Range_Filter**: A pair of date pickers (From/To) limiting results to reminders sent within the specified UTC date range
- **Customer_Filter**: A text input that performs a case-insensitive search against the customer name
- **Pagination_Controls**: Server-side page navigation rendered below the data table showing current page info and page buttons
- **Sidebar_Link**: The navigation sub-item "Reminder History" rendered in the sidebar under Payment Reminders

## Requirements

### Requirement 1: Page Access and Navigation

**User Story:** As a portal user with payment reminder access, I want a dedicated Reminder History page in the sidebar, so that I can review all past reminder activity without leaving the Payment Reminders section.

#### Acceptance Criteria

1. WHEN the user has `payment_reminder_manual` module access, THE Sidebar_Link SHALL render a nav-sub-item labelled "Reminder History" under the "Payment Reminders" parent item
2. WHEN the user navigates to the History_Page URL, THE History_Page SHALL return a 200 OK response for authenticated users with `payment_reminder_manual` access
3. IF the user does not have `payment_reminder_manual` module access, THEN THE Sidebar_Link SHALL not be rendered in the navigation
4. IF an unauthenticated user requests the History_Page URL, THEN THE History_Page SHALL redirect to the login page

### Requirement 2: Page Layout and Structure

**User Story:** As a portal user, I want the Reminder History page to follow the same layout as other Payment Reminder pages, so that the experience is consistent and familiar.

#### Acceptance Criteria

1. THE History_Page SHALL display a topbar with eyebrow text "Payment Reminders", heading "Reminder History", and a muted description paragraph
2. THE History_Page SHALL display a filter card section (`.glass.card-pad`) with `margin-bottom:22px` below the topbar
3. THE History_Page SHALL display a data table card section (`.glass.card-pad`) below the filter card containing the results table and pagination

### Requirement 3: Filter Controls

**User Story:** As a portal user, I want to filter reminder history by tier, status, date range, customer, and method, so that I can quickly find specific reminder records.

#### Acceptance Criteria

1. THE History_Page SHALL display a Tier_Filter dropdown with options: All (default), Friendly, Firm, Formal
2. THE History_Page SHALL display a Status_Filter dropdown with options: All (default), Sent, Failed
3. THE History_Page SHALL display a Method_Filter dropdown with options: All (default), Auto, Manual, Test
4. THE History_Page SHALL display a Date_Range_Filter consisting of a "From" date picker and a "To" date picker
5. THE History_Page SHALL display a Customer_Filter text input for case-insensitive customer name search
6. WHEN the user clicks the "Filter" button, THE History_Page SHALL request page 1 from the History_Endpoint with all current filter values applied
7. WHEN the user clicks the "Clear" button, THE History_Page SHALL reset all filter controls to their default values and reload page 1

### Requirement 4: Data Table Columns and Rendering

**User Story:** As a portal user, I want to see all relevant reminder details in a structured table, so that I can understand what was sent, when, and to whom.

#### Acceptance Criteria

1. THE History_Page SHALL render a table with columns in this order: Date, Invoice Number, Customer Name, Tier, Recipient Email, Method, Status, Opened
2. THE History_Page SHALL display the "Date" column formatted as the `SentAtUtc` value in `dd MMM yyyy` format
3. WHEN a Reminder_Log row contains an invoice number, THE History_Page SHALL render the invoice number as a hyperlink navigating to the Invoice Detail page for that invoice
4. THE History_Page SHALL render the "Tier" column as a coloured badge (Friendly = green, Firm = amber, Formal = red)
5. THE History_Page SHALL render the "Method" column as a badge indicating "Auto" (when IsManualTrigger = false and IsTestSend = false), "Manual" (when IsManualTrigger = true and IsTestSend = false), or "Test" (when IsTestSend = true)
6. THE History_Page SHALL render the "Status" column as a badge: "Sent" (green) when IsSentSuccessfully = true, or "Failed" (red) when IsSentSuccessfully = false
7. THE History_Page SHALL render the "Opened" column as a badge: "Opened" (green) when IsOpened = true, "Not opened" (muted) when IsOpened = false and IsSentSuccessfully = true, or "—" when IsSentSuccessfully = false

### Requirement 5: Server-Side Pagination

**User Story:** As a portal user, I want paginated results so that the page loads quickly even with thousands of reminder records.

#### Acceptance Criteria

1. THE History_Endpoint SHALL accept `page` and `pageSize` parameters with defaults of page = 1 and pageSize = 20
2. THE History_Endpoint SHALL return a JSON response containing `data` (array of reminder records for the requested page), `totalCount` (total matching records), `page` (current page number), and `pageSize` (records per page)
3. THE History_Page SHALL display pagination info text showing "Showing X–Y of Z" below the table
4. THE History_Page SHALL render page navigation buttons allowing the user to move between pages
5. WHEN a page navigation button is clicked, THE History_Page SHALL request the corresponding page from the History_Endpoint with the current filter values preserved

### Requirement 6: AJAX Data Loading

**User Story:** As a portal user, I want data loading to feel responsive with clear loading indicators and error feedback.

#### Acceptance Criteria

1. WHEN the History_Page initiates a data request, THE History_Page SHALL activate BlockUI before the fetch call
2. WHEN the History_Endpoint returns a response (success or failure), THE History_Page SHALL deactivate BlockUI
3. IF the History_Endpoint returns a failure response, THEN THE History_Page SHALL display a SweetAlert2 error dialog with the error message
4. WHEN the History_Page loads for the first time, THE History_Page SHALL automatically fetch page 1 with default filter values

### Requirement 7: History Endpoint (Controller)

**User Story:** As a developer, I want a well-structured AJAX endpoint that follows project conventions, so that the backend is consistent and maintainable.

#### Acceptance Criteria

1. THE History_Endpoint SHALL be an `[HttpGet]` action named `AxGetAllReminderHistory` on `PaymentReminderController`
2. THE History_Endpoint SHALL accept filter parameters: `tier` (string, optional), `status` (string, optional), `method` (string, optional), `dateFrom` (DateTime, optional), `dateTo` (DateTime, optional), `customer` (string, optional), `page` (int, default 1), `pageSize` (int, default 20)
3. THE History_Endpoint SHALL enforce tenant isolation by filtering on the current business ID
4. THE History_Endpoint SHALL return `Json(new { success = true, data, totalCount, page, pageSize })` on success
5. IF an exception occurs, THEN THE History_Endpoint SHALL return `Json(new { success = false, message = "Failed to load reminder history." })`

### Requirement 8: Service Layer Query

**User Story:** As a developer, I want the paginated history query encapsulated in the service layer, so that data access logic is separated from controller concerns.

#### Acceptance Criteria

1. THE IPaymentReminderService SHALL expose a method for retrieving paginated reminder history accepting businessId, filter parameters, page, and pageSize
2. THE service method SHALL query `[reminder].[PaymentReminderLog]` joined with Customer and Invoice tables to retrieve customer name and invoice number
3. THE service method SHALL filter results by BusinessId matching the provided businessId parameter
4. WHEN a tier filter value is provided (not "All" or null), THE service method SHALL filter results where EscalationTier equals the provided tier
5. WHEN a status filter value is "Sent", THE service method SHALL filter results where IsSentSuccessfully = true
6. WHEN a status filter value is "Failed", THE service method SHALL filter results where IsSentSuccessfully = false
7. WHEN a method filter value is "Auto", THE service method SHALL filter results where IsManualTrigger = false and IsTestSend = false
8. WHEN a method filter value is "Manual", THE service method SHALL filter results where IsManualTrigger = true and IsTestSend = false
9. WHEN a method filter value is "Test", THE service method SHALL filter results where IsTestSend = true
10. WHEN dateFrom is provided, THE service method SHALL filter results where SentAtUtc is on or after dateFrom
11. WHEN dateTo is provided, THE service method SHALL filter results where SentAtUtc is before the end of the dateTo day (dateTo + 1 day)
12. WHEN a customer search value is provided, THE service method SHALL filter results where the customer name contains the search text (case-insensitive)
13. THE service method SHALL order results by SentAtUtc descending (most recent first)
14. THE service method SHALL return the total count of matching records and only the records for the requested page (using OFFSET/FETCH or equivalent)

### Requirement 9: Empty State

**User Story:** As a portal user, I want a clear message when there are no reminder records matching my filters, so that I know the system is working but there are simply no results.

#### Acceptance Criteria

1. WHEN the History_Endpoint returns zero records, THE History_Page SHALL hide the data table and pagination controls
2. WHEN the History_Endpoint returns zero records, THE History_Page SHALL display an empty state message: "No reminders found matching your filters."
