# Requirements Document

## Introduction

Move the global Activity Feed from the /Sales/Pipeline page to a dedicated standalone page at /Sales/Activity. The new page provides full filtering, pagination, and AJAX-driven loading following the same patterns used by Meetings and Tasks pages. On the Pipeline page, replace the removed feed with a compact "Recent Lead Activity" section showing the last few events relevant to visible pipeline leads.

## Glossary

- **Activity_Feed_Page**: The new standalone page at /Sales/Activity displaying a paginated, filterable list of all lead-related activity events for the business
- **Activity_Feed_Service**: The existing `IActivityFeedService` / `ActivityFeedService` that records and retrieves activity entries from `[sales].[ActivityFeed]`
- **Filter_Panel**: The glass card-pad section at the top of the Activity_Feed_Page containing Action Type dropdown, Date From, Date To, and Quick Date Preset buttons
- **Quick_Date_Presets**: The row of shortcut buttons for common date ranges: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time (excludes "Next Month")
- **Activity_Table**: The data table on the Activity_Feed_Page displaying activity entries with columns for timestamp, action, description, contact/lead name, and performer
- **Pagination_Controls**: The navigation controls below the Activity_Table showing page info and page number buttons at 15 items per page
- **Recent_Lead_Activity**: The compact section on the Pipeline page replacing the removed global Activity Feed, showing the last 5–10 events across visible pipeline leads
- **Action_Type**: The string value stored in `[sales].[ActivityFeed].[Action]` column that categorizes the type of event (e.g., stage_changed, meeting_scheduled, response_sent, lead_created)
- **Sales_Navigation**: The sidebar navigation group under the "Opportunities" section containing links to all Sales module pages

## Requirements

### Requirement 1: Activity Feed Page Route and View

**User Story:** As a sales user, I want a dedicated Activity page so that I can view and filter all lead activity without navigating away from the pipeline board.

#### Acceptance Criteria

1. WHEN a user navigates to /Sales/Activity, THE Activity_Feed_Page SHALL render a full-page view with a topbar, filter panel, and activity data table
2. THE Activity_Feed_Page SHALL display a topbar with the eyebrow label "Sales Pipeline", the heading "Activity", and a subtitle describing the page purpose
3. THE Activity_Feed_Page SHALL load activity data via an AJAX GET request on page load without requiring a full page refresh

### Requirement 2: Navigation Menu Link

**User Story:** As a sales user, I want an "Activity" link in the sidebar navigation so that I can access the Activity Feed page directly from any page in the Sales module.

#### Acceptance Criteria

1. THE Sales_Navigation SHALL include an "Activity" link that navigates to /Sales/Activity
2. THE Sales_Navigation SHALL position the Activity link between the Meetings link and the Tasks link
3. WHILE a user is on the /Sales/Activity page, THE Sales_Navigation SHALL highlight the Activity link as active

### Requirement 3: Filter Panel

**User Story:** As a sales user, I want to filter activity entries by action type and date range so that I can find specific events quickly.

#### Acceptance Criteria

1. THE Filter_Panel SHALL display an Action Type dropdown with "All" as the default option and all distinct action types as selectable options
2. THE Filter_Panel SHALL display a Date From input and a Date To input for specifying a date range
3. THE Filter_Panel SHALL display a "Filter" button that triggers data reload with the selected filter criteria and a "Clear" button that resets all filters and reloads data
4. THE Filter_Panel SHALL display a Quick_Date_Presets row containing buttons for: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time
5. THE Filter_Panel SHALL NOT include a "Next Month" preset button
6. WHEN a user clicks a Quick_Date_Preset button, THE Filter_Panel SHALL populate the Date From and Date To fields with the corresponding date range and trigger a data reload
7. THE Filter_Panel SHALL render inside a glass card-pad section with margin-bottom of 22px, matching the Meetings page layout pattern

### Requirement 4: Activity Data Table with AJAX Loading

**User Story:** As a sales user, I want to see activity entries in a structured table loaded via AJAX so that the page remains responsive and data loads efficiently.

#### Acceptance Criteria

1. THE Activity_Table SHALL display columns for: Timestamp, Action, Description, Contact/Lead, Performed By
2. WHEN activity data is loading, THE Activity_Feed_Page SHALL display a BlockUI overlay to indicate processing
3. WHEN the AJAX request completes successfully, THE Activity_Table SHALL render the returned entries in reverse chronological order (newest first)
4. IF the AJAX request fails, THEN THE Activity_Feed_Page SHALL unblock the UI and display a SweetAlert2 error message
5. WHEN no activity entries match the current filters, THE Activity_Table SHALL display an empty state message within the table body

### Requirement 5: Pagination

**User Story:** As a sales user, I want paginated results so that large activity logs remain performant and navigable.

#### Acceptance Criteria

1. THE Pagination_Controls SHALL display 15 items per page
2. THE Pagination_Controls SHALL display a page information label showing the current range and total count (e.g., "Showing 1–15 of 120")
3. THE Pagination_Controls SHALL display page navigation buttons allowing the user to move between pages
4. WHEN a user clicks a page navigation button, THE Activity_Feed_Page SHALL load the corresponding page via AJAX without a full page refresh
5. WHEN the user applies a filter, THE Pagination_Controls SHALL reset to page 1

### Requirement 6: AJAX Endpoint for Activity Feed

**User Story:** As a developer, I want a server-side AJAX endpoint that returns filtered and paginated activity data so that the Activity_Feed_Page can retrieve data dynamically.

#### Acceptance Criteria

1. THE SalesController SHALL expose an HTTP GET endpoint named AxGetActivityFeedPage that accepts parameters: actionType (string, optional), dateFrom (DateTime, optional), dateTo (DateTime, optional), page (int, default 1)
2. WHEN the endpoint is called, THE Activity_Feed_Service SHALL return activity entries filtered by the specified action type and date range, paginated at 15 items per page, ordered by CreatedAtUtc descending
3. THE endpoint SHALL return a JSON response containing: success flag, data array, totalCount, currentPage, and pageSize
4. IF an error occurs during data retrieval, THEN THE endpoint SHALL return a JSON response with success set to false and an error message

### Requirement 7: Pipeline Page — Remove Global Activity Feed

**User Story:** As a sales user, I want the Pipeline page to focus on pipeline management without the full activity feed, so that the page loads faster and is less cluttered.

#### Acceptance Criteria

1. THE Pipeline page SHALL NOT render the "Recent Activity" section that previously displayed the global activity feed
2. THE Pipeline page SHALL NOT call the AxGetGlobalActivityFeed endpoint on page load

### Requirement 8: Pipeline Page — Recent Lead Activity Section

**User Story:** As a sales user, I want to see a compact summary of recent lead activity on the Pipeline page so that I stay aware of the latest events without needing to navigate away.

#### Acceptance Criteria

1. THE Pipeline page SHALL display a "Recent Lead Activity" section below the Pipeline KPI Footer section
2. THE Recent_Lead_Activity section SHALL display between 5 and 10 of the most recent activity entries across all active pipeline leads
3. WHEN the Pipeline page loads, THE Recent_Lead_Activity section SHALL load data via an AJAX GET request
4. THE Recent_Lead_Activity section SHALL display each entry with: a timestamp, the action description, and the associated contact or lead name
5. THE Recent_Lead_Activity section SHALL NOT include pagination controls or a "load more" mechanism
6. THE Recent_Lead_Activity section SHALL render inside a glass card-pad container with margin-top of 22px, consistent with other Pipeline page sections

### Requirement 9: AJAX Endpoint for Recent Lead Activity

**User Story:** As a developer, I want a server-side endpoint that returns the most recent activity events for the Pipeline page summary.

#### Acceptance Criteria

1. THE SalesController SHALL expose an HTTP GET endpoint named AxGetRecentLeadActivity that returns the most recent 10 activity entries across all active pipeline leads for the current business
2. THE endpoint SHALL return entries ordered by CreatedAtUtc descending
3. THE endpoint SHALL return a JSON response containing: success flag and data array
4. IF an error occurs during data retrieval, THEN THE endpoint SHALL return a JSON response with success set to false and an error message
