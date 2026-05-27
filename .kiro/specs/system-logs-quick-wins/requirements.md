# Requirements Document

## Introduction

This feature adds four quick-win enhancements to the existing System Logs Viewer (`/Admin/SystemLogs`). The enhancements provide at-a-glance health metrics via KPI cards, real-time monitoring via auto-refresh, faster debugging workflows via one-click correlation ID copying, and incident collaboration via CSV export. All enhancements are SuperAdmin-only and build on the existing filter card, data table, expandable detail rows, and pagination infrastructure already in place.

## Glossary

- **System_Logs_Viewer**: The SuperAdmin-only MVC page at `/Admin/SystemLogs` that displays application log entries from the Portal.Logging database in a searchable, filterable, paginated data table with expandable detail rows.
- **System_Logs_Controller**: The MVC controller restricted to the SuperAdmin role that exposes endpoints for the System Logs Viewer page and search functionality.
- **System_Log_Query_Service**: The service (ISystemLogQueryService) that provides filtered, paginated access to log records from the Portal.Logging database.
- **KPI_Cards_Section**: A row of summary metric cards displayed between the topbar and the filter card, following the same glass card pattern with left border accent used in the Supplier Dashboard and Revenue Dashboard.
- **Auto_Refresh_Toggle**: A UI toggle control that, when enabled, polls the search endpoint at a fixed interval to display new log entries without user interaction.
- **Copy_Correlation_Button**: A clickable button rendered in the Correlation ID cell of the data table that copies the correlation ID value to the system clipboard.
- **CSV_Export**: A feature that downloads the current filtered result set as a comma-separated values file for offline analysis and sharing.
- **Log_Entry**: A single record in the `[dbo].[Logs]` table containing: Id, Message, MessageTemplate, Level, TimeStamp, Exception, Properties, CorrelationId, UserId, BusinessId, SourceContext, RequestPath, MachineName.
- **Logging_DbContext**: The dedicated Entity Framework Core DbContext configured to connect to the Portal.Logging database with NoTracking behaviour.
- **SuperAdmin**: A platform role that bypasses all module access checks and has full administrative privileges.

## Requirements

### Requirement 1: Error Count KPI Cards

**User Story:** As a super admin, I want to see at-a-glance counts of errors, warnings, and total log entries at the top of the System Logs page, so that I can instantly assess application health without applying filters.

#### Acceptance Criteria

1. THE System_Logs_Viewer SHALL display a KPI_Cards_Section between the topbar and the filter card containing three KPI cards: "Errors (24h)", "Warnings (24h)", and "Total Entries Today".
2. THE KPI_Cards_Section SHALL follow the established KPI card pattern: each card rendered as a `glass card-pad` element with a 4px left border accent, Manrope 26px bold value, Inter 11px uppercase label, and Inter 12px muted subtitle.
3. THE "Errors (24h)" KPI card SHALL display the count of Log_Entry records with Level equal to "Error" where TimeStamp is within the last 24 hours, with a left border colour of #C24A4A (danger red) and value text colour of #C24A4A. The subtitle SHALL read "Last 24 hours".
4. THE "Warnings (24h)" KPI card SHALL display the count of Log_Entry records with Level equal to "Warning" where TimeStamp is within the last 24 hours, with a left border colour of #C8912E (warning amber) and value text colour of #C8912E. The subtitle SHALL read "Last 24 hours".
5. THE "Total Entries Today" KPI card SHALL display the count of all Log_Entry records where TimeStamp falls on the current calendar day (UTC midnight to current time), with a left border colour of #0D5EA6 (primary blue) and value text colour of #0D5EA6. The subtitle SHALL read "Since midnight UTC".
6. WHEN the System Logs page loads, THE System_Logs_Controller SHALL return the three KPI count values alongside the existing dropdown data (levels and source contexts).
7. THE System_Log_Query_Service SHALL expose a method `GetKpiCountsAsync()` to retrieve the KPI counts (error count last 24h, warning count last 24h, total entries today) in a single database round-trip.
8. WHEN auto-refresh is enabled and a refresh cycle completes, THE System_Logs_Viewer SHALL also update the KPI card values with fresh counts from the server.
9. IF the KPI count query fails, THEN THE System_Logs_Viewer SHALL display "—" in each KPI card value and SHALL NOT prevent the rest of the page from loading.
10. THE KPI card count values SHALL be formatted with locale-appropriate thousands separators (e.g., "1,234" for English locale).

### Requirement 2: Auto-Refresh Toggle

**User Story:** As a super admin, I want to enable automatic polling for new log entries, so that I can monitor deployments and live issues without manually clicking the filter button.

#### Acceptance Criteria

1. THE System_Logs_Viewer SHALL display an auto-refresh toggle button in the filter card section, positioned after the Clear button, with auto-refresh disabled by default on page load.
2. WHEN the auto-refresh toggle is in the disabled state, THE System_Logs_Viewer SHALL display the toggle with an "Auto-Refresh" label and an inactive visual state (outline style, no active indicator).
3. WHEN the auto-refresh toggle is clicked while disabled, THE System_Logs_Viewer SHALL enable auto-refresh, change the toggle to an active visual state (primary colour background with a pulsing dot indicator), and begin polling the search endpoint every 30 seconds.
4. WHEN the auto-refresh toggle is clicked while enabled, THE System_Logs_Viewer SHALL disable auto-refresh, revert the toggle to the inactive visual state, and stop the polling interval.
5. WHILE auto-refresh is enabled, THE System_Logs_Viewer SHALL execute the search request using the currently applied filter values and current page number, preserving all active filters without resetting them.
6. WHILE auto-refresh is enabled, THE System_Logs_Viewer SHALL NOT display BlockUI during automatic refresh cycles to avoid disrupting the monitoring experience.
7. WHEN the user manually clicks the Filter button or changes the page while auto-refresh is enabled, THE System_Logs_Viewer SHALL reset the polling timer to restart the 30-second interval from that point.
8. WHEN the user navigates away from the System Logs page (page unload or browser navigation), THE System_Logs_Viewer SHALL clear the auto-refresh interval to prevent orphaned polling requests.
9. IF an auto-refresh polling request fails due to a network error or server error response, THEN THE System_Logs_Viewer SHALL silently skip that refresh cycle, retain the currently displayed data unchanged, and attempt the next poll at the regular 30-second interval without disabling auto-refresh.

### Requirement 3: Copy Correlation ID Button

**User Story:** As a super admin, I want to copy a correlation ID to my clipboard with one click, so that I can quickly paste it into filter fields or share it with team members when tracing requests across log entries.

#### Acceptance Criteria

1. WHEN a Log_Entry has a non-null and non-empty CorrelationId value, THE System_Logs_Viewer SHALL render a copy button with a clipboard icon adjacent to the correlation ID text in the table cell, with an accessible aria-label of "Copy correlation ID".
2. WHEN a Log_Entry has a null or empty CorrelationId value, THE System_Logs_Viewer SHALL display a dash character ("—") and SHALL NOT render the copy button.
3. WHEN the copy button is clicked, THE System_Logs_Viewer SHALL copy the full CorrelationId value to the system clipboard using the Clipboard API (navigator.clipboard.writeText).
4. WHEN the clipboard write operation succeeds, THE System_Logs_Viewer SHALL display a tooltip positioned near the copy button with the text "Copied!" that automatically dismisses after 2 seconds.
5. IF the clipboard write operation fails or the Clipboard API is unavailable, THEN THE System_Logs_Viewer SHALL display a SweetAlert2 error notification with the message "Failed to copy to clipboard."
6. WHEN the copy button is clicked, THE System_Logs_Viewer SHALL prevent the click event from propagating to the parent row to avoid triggering the detail row expansion.

### Requirement 4: Export to CSV

**User Story:** As a super admin, I want to export the current filtered log results as a CSV file, so that I can share log data with team members or attach it to incident reports.

#### Acceptance Criteria

1. THE System_Logs_Viewer SHALL display an "Export CSV" button in the filter card section, positioned after the auto-refresh toggle.
2. WHEN the Export CSV button is clicked, THE System_Logs_Viewer SHALL request all matching records for the currently applied filters from the server, ignoring pagination (not limited to the current page).
3. THE System_Logs_Controller SHALL expose a dedicated export endpoint that accepts the same filter parameters as the search endpoint but returns all matching records up to a maximum of 10,000 records.
4. IF the filtered result set exceeds 10,000 records, THEN THE System_Logs_Controller SHALL return exactly 10,000 records (ordered by TimeStamp descending) and include a boolean `isTruncated` property set to true in the JSON response.
5. IF the export result is truncated, THEN THE System_Logs_Viewer SHALL display a SweetAlert2 informational message stating "Export limited to 10,000 records. Apply more specific filters to narrow the result set." before initiating the download.
6. THE CSV file SHALL include the following columns in order: TimeStamp, Level, Message, Exception, UserId, CorrelationId, SourceContext, RequestPath, MachineName. Field values containing commas, double quotes, or line breaks SHALL be enclosed in double quotes, and any embedded double-quote characters SHALL be escaped by doubling them (RFC 4180). The first row SHALL be a header row containing the column names.
7. THE CSV file SHALL use UTF-8 encoding with a byte order mark (BOM) for compatibility with Microsoft Excel.
8. THE CSV file SHALL be named using the pattern `SystemLogs_YYYY-MM-DD_HHmmss.csv` where the timestamp reflects the client's local date and time at the moment the download is initiated.
9. WHEN the export request is in progress, THE System_Logs_Viewer SHALL display BlockUI with the message "Exporting logs..." and hide it upon completion or error.
10. IF the export endpoint returns an error, THEN THE System_Logs_Viewer SHALL display a SweetAlert2 error notification with the message "Export failed. Please try again."
11. WHEN no records match the current filters, THE System_Logs_Viewer SHALL display a SweetAlert2 informational message stating "No records to export." and SHALL NOT generate a CSV file.
12. WHILE an export request is in progress, THE System_Logs_Viewer SHALL suspend auto-refresh polling (if enabled) and resume the 30-second polling interval after the export completes or fails.
