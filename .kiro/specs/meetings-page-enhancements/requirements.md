# Requirements Document

## Introduction

The Meetings page at /Sales/Meetings currently renders server-side with a flat list of all meetings and no filtering, pagination, or edit capability. This enhancement converts the page to an AJAX-driven, filterable, paginated view (matching the established Tasks page pattern), adds an Edit Meeting modal, introduces urgency indicators and relative time display, and fixes a bug where the Create Meeting modal re-opens after successful creation when query parameters are present.

## Glossary

- **Meetings_Page**: The view at /Sales/Meetings that displays all scheduled meetings for the current tenant
- **Filter_Panel**: A glass card-pad section above the data table containing filter controls for narrowing displayed meetings
- **Meetings_Table**: The data-table element that renders paginated meeting rows via AJAX
- **Edit_Modal**: A modal dialog pre-populated with existing meeting data for updating a meeting via the AxPostUpdateMeeting endpoint
- **Create_Modal**: The existing modal dialog for scheduling a new meeting via the AxPostCreateMeeting endpoint
- **Pagination_Controls**: Navigation controls below the table showing page info and page buttons, fixed at 15 items per page
- **Quick_Date_Presets**: Shortcut buttons that populate Date From and Date To fields with predefined ranges
- **Urgency_Indicator**: A visual marker (coloured pill or row highlight) communicating the time-sensitivity of a meeting
- **Relative_Time_Label**: A human-readable time difference label (e.g., "in 2 hours", "3 days ago") shown alongside the scheduled date
- **MeetingType**: A lookup table containing meeting classification values (e.g., In-Person, Video Call, Phone Call)
- **AJAX_Endpoint**: A controller method prefixed with AxGet or AxPost that returns JSON for client-side rendering

## Requirements

### Requirement 1: AJAX-Driven Meeting List

**User Story:** As a sales user, I want the meetings table to load via AJAX, so that I can filter and paginate without full page reloads.

#### Acceptance Criteria

1. WHEN the Meetings_Page loads, THE Meetings_Table SHALL fetch meeting data from an AJAX_Endpoint and render results client-side
2. THE AJAX_Endpoint SHALL accept the following optional filter parameters: status, meetingTypeId, dateFrom, dateTo, and page
3. THE AJAX_Endpoint SHALL return a JSON response containing: success flag, data array, totalCount, currentPage, and totalPages
4. THE Meetings_Table SHALL display a loading indicator while the AJAX request is in progress
5. IF the AJAX request fails, THEN THE Meetings_Table SHALL display an error message within the table body

### Requirement 2: Filter Panel

**User Story:** As a sales user, I want to filter meetings by status, type, and date range, so that I can quickly find relevant meetings.

#### Acceptance Criteria

1. THE Filter_Panel SHALL be rendered in a glass card-pad section with margin-bottom of 22px above the Meetings_Table card
2. THE Filter_Panel SHALL contain a Status dropdown with options: All, Upcoming, Completed, and Cancelled
3. THE Filter_Panel SHALL contain a Meeting Type dropdown populated from the MeetingType lookup table
4. THE Filter_Panel SHALL contain Date From and Date To input fields of type date
5. THE Filter_Panel SHALL contain a Filter button that triggers a data reload at page 1 with current filter values
6. THE Filter_Panel SHALL contain a Clear button that resets all filter fields to default and reloads data at page 1
7. WHEN the Status filter is set to "Upcoming", THE AJAX_Endpoint SHALL return only meetings where ScheduledAtUtc is in the future and IsCancelled is false
8. WHEN the Status filter is set to "Completed", THE AJAX_Endpoint SHALL return only meetings where ScheduledAtUtc is in the past and IsCancelled is false
9. WHEN the Status filter is set to "Cancelled", THE AJAX_Endpoint SHALL return only meetings where IsCancelled is true
10. WHEN Date From is provided, THE AJAX_Endpoint SHALL return only meetings with ScheduledAtUtc on or after that date
11. WHEN Date To is provided, THE AJAX_Endpoint SHALL return only meetings with ScheduledAtUtc on or before the end of that date

### Requirement 3: Quick Date Presets

**User Story:** As a sales user, I want quick date range shortcuts, so that I can filter by common periods without manually entering dates.

#### Acceptance Criteria

1. THE Filter_Panel SHALL display Quick_Date_Presets below the filter fields row with the following options: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time
2. WHEN a Quick_Date_Preset button is clicked, THE Filter_Panel SHALL populate the Date From and Date To fields with the corresponding date range
3. WHEN the "All Time" preset is clicked, THE Filter_Panel SHALL clear both Date From and Date To fields
4. WHEN a Quick_Date_Preset button is clicked, THE Meetings_Table SHALL reload data at page 1 with the new date range applied

### Requirement 4: Pagination

**User Story:** As a sales user, I want meetings displayed in pages of 15, so that the page remains performant with large datasets.

#### Acceptance Criteria

1. THE AJAX_Endpoint SHALL return a maximum of 15 meetings per page
2. THE Pagination_Controls SHALL display "Showing X–Y of Z meetings" text below the table
3. THE Pagination_Controls SHALL display page number buttons allowing navigation between pages
4. WHEN a page button is clicked, THE Meetings_Table SHALL reload with the selected page number while preserving current filter values
5. WHEN no meetings match the current filters, THE Meetings_Table SHALL display an empty state message "No meetings found."
6. WHEN no meetings match the current filters, THE Pagination_Controls SHALL be hidden

### Requirement 5: Default Sort Order

**User Story:** As a sales user, I want meetings sorted by scheduled date descending by default, so that the most relevant upcoming or recent meetings appear first.

#### Acceptance Criteria

1. THE AJAX_Endpoint SHALL sort meetings by ScheduledAtUtc in descending order (newest first)

### Requirement 6: Urgency Indicators

**User Story:** As a sales user, I want visual urgency cues on meetings, so that I can immediately identify meetings needing attention.

#### Acceptance Criteria

1. WHILE a meeting is scheduled for today and is not cancelled, THE Meetings_Table SHALL display an amber-coloured "Today" pill for that meeting
2. WHILE a meeting is scheduled in the future (not today) and is not cancelled, THE Meetings_Table SHALL display a blue-coloured "Upcoming" pill for that meeting
3. WHILE a meeting is in the past, is not cancelled, and has no Outcome recorded, THE Meetings_Table SHALL display a red-coloured "Needs Outcome" pill for that meeting
4. WHILE a meeting is in the past, is not cancelled, and has an Outcome recorded, THE Meetings_Table SHALL display a green-coloured "Completed" pill for that meeting
5. WHILE a meeting is cancelled, THE Meetings_Table SHALL display a red-coloured "Cancelled" pill for that meeting

### Requirement 7: Relative Time Display

**User Story:** As a sales user, I want to see how far away or how long ago each meeting is, so that I can gauge urgency at a glance.

#### Acceptance Criteria

1. THE Meetings_Table SHALL display a Relative_Time_Label alongside the formatted scheduled date for each meeting
2. WHEN a meeting is scheduled within the next 24 hours, THE Relative_Time_Label SHALL display in hours (e.g., "in 2 hours")
3. WHEN a meeting is scheduled more than 24 hours in the future, THE Relative_Time_Label SHALL display in days (e.g., "in 3 days")
4. WHEN a meeting occurred within the past 24 hours, THE Relative_Time_Label SHALL display in hours (e.g., "2 hours ago")
5. WHEN a meeting occurred more than 24 hours in the past, THE Relative_Time_Label SHALL display in days (e.g., "3 days ago")

### Requirement 8: Edit Meeting Modal

**User Story:** As a sales user, I want to edit an existing meeting, so that I can update details or record the outcome after a meeting has taken place.

#### Acceptance Criteria

1. THE Meetings_Table SHALL display an "Edit" button in the Actions column for each meeting
2. WHEN the Edit button is clicked, THE Edit_Modal SHALL open pre-populated with the meeting's current values (Subject, MeetingTypeId, ScheduledAtUtc, DurationMinutes, Location, Notes, Outcome)
3. THE Edit_Modal SHALL include an Outcome textarea field allowing the user to record meeting results
4. WHEN the Edit_Modal form is submitted with valid data, THE Edit_Modal SHALL send an update request to the AxPostUpdateMeeting endpoint
5. WHEN the update request succeeds, THE Edit_Modal SHALL close and THE Meetings_Table SHALL reload the current page
6. IF the update request fails, THEN THE Edit_Modal SHALL display the error message returned by the endpoint using SweetAlert2
7. THE Edit_Modal SHALL display BlockUI during the update request and hide it after the response is received
8. WHEN the Edit_Modal form is submitted, THE Edit_Modal SHALL validate that Subject and ScheduledAtUtc are not empty before sending the request

### Requirement 9: Fix Create Modal Re-Opening After Creation

**User Story:** As a sales user, I want the Create Meeting modal to stay closed after I successfully schedule a meeting, so that I am not prompted to create another meeting unintentionally.

#### Acceptance Criteria

1. WHEN a meeting is successfully created via the Create_Modal, THE Meetings_Page SHALL redirect to /Sales/Meetings without any query parameters
2. THE Meetings_Page SHALL strip the leadRequestId and contactId query parameters from the URL before performing the post-creation redirect
3. WHEN the Meetings_Page loads without a leadRequestId query parameter, THE Create_Modal SHALL remain closed

