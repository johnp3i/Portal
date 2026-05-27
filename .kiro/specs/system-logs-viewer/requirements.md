# Requirements Document

## Introduction

The System Logs Viewer provides SuperAdmin visibility into application-level logs (errors, warnings, request activity) stored in the dedicated `Portal.Logging` SQL Server database (`[dbo].[Logs]` table). This enables operational monitoring and debugging directly from the Portal administration interface without requiring direct database access. The viewer follows the same UI pattern as the existing Audit Log viewer (filter card + data table + pagination + expandable detail rows) and is accessible from the Administration section sidebar at `/Admin/SystemLogs`.

## Glossary

- **System_Logs_Viewer**: The SuperAdmin-only MVC page at `/Admin/SystemLogs` that displays application log entries from the Portal.Logging database in a searchable, filterable, paginated data table with expandable detail rows.
- **System_Logs_Controller**: The MVC controller restricted to the SuperAdmin role that exposes endpoints for the System Logs Viewer page and search functionality.
- **System_Log_Query_Service**: A service (ISystemLogQueryService) that provides filtered, paginated access to log records from the Portal.Logging database by level, date range, user, correlation ID, source context, and request path.
- **System_Log_Query_Repository**: A repository that queries the `[dbo].[Logs]` table in the Portal.Logging database using a dedicated DbContext or connection separate from PortalDbContext.
- **Logging_DbContext**: A dedicated Entity Framework Core DbContext configured to connect to the Portal.Logging database, separate from PortalDbContext and MembershipDbContext.
- **Log_Entry**: A single record in the `[dbo].[Logs]` table containing: Id, Message, MessageTemplate, Level, TimeStamp, Exception, Properties, CorrelationId, UserId, BusinessId, SourceContext, RequestPath, MachineName.
- **Log_Level**: The severity classification of a log entry: Debug, Information, Warning, Error, or Fatal.
- **SuperAdmin**: A platform role that bypasses all module access checks and has full administrative privileges.
- **Portal_System**: The Portal web application as a whole.
- **Current_Tenant**: The BusinessId resolved via ICurrentTenantService, used to scope queries to the active business.

## Requirements

### Requirement 1: Separate Database Connection

**User Story:** As a platform developer, I want the System Logs Viewer to use a dedicated DbContext for the Portal.Logging database, so that log queries are isolated from the main Portal and Membership database contexts and do not interfere with transactional operations.

#### Acceptance Criteria

1. THE Portal_System SHALL register a Logging_DbContext configured with the `LoggingDb` connection string pointing to the Portal.Logging database.
2. THE Logging_DbContext SHALL be registered as a scoped service in the dependency injection container, separate from PortalDbContext and MembershipDbContext.
3. THE Logging_DbContext SHALL map the `[dbo].[Logs]` table with all columns: Id (BIGINT), Message (NVARCHAR(MAX)), MessageTemplate (NVARCHAR(MAX)), Level (NVARCHAR(128)), TimeStamp (DATETIME2), Exception (NVARCHAR(MAX)), Properties (NVARCHAR(MAX)), CorrelationId (NVARCHAR(128)), UserId (NVARCHAR(450)), BusinessId (INT), SourceContext (NVARCHAR(512)), RequestPath (NVARCHAR(512)), MachineName (NVARCHAR(128)).
4. THE Logging_DbContext SHALL be configured as read-only by disabling change tracking (QueryTrackingBehavior.NoTracking) since the System Logs Viewer only reads log data.
5. THE System_Log_Query_Repository SHALL use the Logging_DbContext for all queries against the Logs table.

### Requirement 2: System Log Query Service

**User Story:** As a super admin, I want to search and filter application logs by multiple criteria, so that I can investigate errors, trace request flows, and monitor application health.

#### Acceptance Criteria

1. THE System_Log_Query_Service SHALL accept filter parameters: Level (string, one of "Debug", "Information", "Warning", "Error", or "Fatal", optional), DateFrom (DateTime, optional), DateTo (DateTime, optional), UserId (string, max 450 characters, optional), CorrelationId (string, max 128 characters, optional), SourceContext (string, max 512 characters, optional), and RequestPath (string, max 512 characters, optional).
2. WHEN no filter parameters are provided, THE System_Log_Query_Service SHALL return all Log_Entry records ordered by TimeStamp descending.
3. WHEN one or more filter parameters are provided, THE System_Log_Query_Service SHALL apply all specified filters using AND logic, where DateFrom is inclusive (>=) and DateTo is inclusive (<=).
4. THE System_Log_Query_Service SHALL support pagination with PageNumber (integer, minimum 1, default 1) and PageSize (integer, minimum 1, maximum 200, default 50) parameters.
5. THE System_Log_Query_Service SHALL return a paged result containing: the list of Log_Entry records, total record count, current page number, and total page count.
6. THE System_Log_Query_Service SHALL return records ordered by TimeStamp descending (most recent first).
7. IF PageNumber exceeds the total page count, THEN THE System_Log_Query_Service SHALL return an empty record list with the correct total record count and total page count.
8. IF PageSize is less than 1 or greater than 200, THEN THE System_Log_Query_Service SHALL clamp the value to the nearest bound (1 or 200) before executing the query.
9. THE System_Log_Query_Service SHALL perform Level filtering using case-insensitive string comparison to handle variations in Serilog level casing.

### Requirement 3: System Logs Controller

**User Story:** As a super admin, I want a dedicated admin endpoint to access system log data, so that only authorized administrators can view application-level logs.

#### Acceptance Criteria

1. THE System_Logs_Controller SHALL require the SuperAdmin role for all actions.
2. THE System_Logs_Controller SHALL be accessible at the route prefix `/Admin/SystemLogs`.
3. WHEN a GET request is made to the index action, THE System_Logs_Controller SHALL return the System Logs Viewer page with filter dropdown data (distinct log levels and source contexts from existing log records).
4. WHEN a GET request is made to the search action with filter parameters, THE System_Logs_Controller SHALL invoke the System_Log_Query_Service and return a JSON response containing a success flag, the paged log results, total record count, current page number, and total page count.
5. IF DateFrom is greater than DateTo when both are provided, THEN THE System_Logs_Controller SHALL return a JSON error response with success set to false and a message indicating the date range is invalid.
6. IF the System_Log_Query_Service throws an exception during the search action, THEN THE System_Logs_Controller SHALL return a JSON error response with success set to false and a message indicating the search could not be completed.
7. THE System_Logs_Controller SHALL apply the ModuleAccessAttribute with the "audit" module and Full access level, consistent with the existing AuditController access pattern.

### Requirement 4: System Logs Viewer UI

**User Story:** As a super admin, I want a searchable, filterable, paginated system logs viewer with expandable detail rows, so that I can visually browse application logs, identify errors, and inspect full exception details without direct database access.

#### Acceptance Criteria

1. THE System_Logs_Viewer SHALL display a filter card with fields: Log Level (dropdown with options: All, Error, Warning, Information, Debug), Date From (date picker), Date To (date picker), User (text input for UserId), Correlation ID (text input), Source Context (dropdown populated from distinct values), and Request Path (text input).
2. THE System_Logs_Viewer SHALL display log records in a data table with columns: TimeStamp (formatted as yyyy-MM-dd HH:mm:ss), Level (displayed as a colored badge), Message (truncated to 120 characters with ellipsis), User, Source Context, Correlation ID, and a detail expand control.
3. THE System_Logs_Viewer SHALL display Level badges with the following color scheme: Error displayed with a red background (#C24A4A), Warning displayed with an amber background (#C8912E), Information displayed with a blue background (#0D5EA6), and Debug displayed with a grey background (#6B7B8D).
4. WHEN the page loads, THE System_Logs_Viewer SHALL automatically invoke the search endpoint with no filters applied and display the first page of results ordered by TimeStamp descending.
5. WHEN the filter button is clicked, THE System_Logs_Viewer SHALL call the search endpoint with the selected filter values, reset to page 1, and display the results.
6. WHEN the clear button is clicked, THE System_Logs_Viewer SHALL reset all filter fields to their default values and reload the first page of unfiltered results.
7. THE System_Logs_Viewer SHALL paginate results with a default page size of 50 records per page.
8. THE System_Logs_Viewer SHALL display pagination controls below the data table showing a "Showing X–Y of Z" info label, current page number, total pages, and Previous/Next navigation buttons with individual page number buttons.
9. WHEN a record detail row is expanded, THE System_Logs_Viewer SHALL display the full Message text, the full Exception and stack trace (when present), the Properties content (formatted), the Request Path, and the Machine Name.
10. WHEN a record has no Exception value, THE System_Logs_Viewer SHALL hide the Exception section in the detail row rather than displaying an empty section.
11. THE System_Logs_Viewer SHALL call BlockUI.show() before AJAX requests and BlockUI.hide() after completion in both success and error response paths.
12. THE System_Logs_Viewer SHALL follow the MyChair Design System: Manrope headings, Inter body text, glass card containers, and the filter card (margin-bottom 22px) + data table layout pattern.
13. WHEN no records match the filter criteria, THE System_Logs_Viewer SHALL display an empty state message "No log entries found matching the selected filters." rendered within the data table card.
14. IF the user selects a Date From value greater than the Date To value, THEN THE System_Logs_Viewer SHALL display a SweetAlert2 validation message indicating the invalid date range and SHALL NOT submit the search request.
15. THE System_Logs_Viewer SHALL only allow one detail row to be expanded at a time — expanding a new row SHALL collapse any previously expanded row.

### Requirement 5: Navigation Integration

**User Story:** As a super admin, I want the System Logs Viewer accessible from the Administration section in the sidebar, so that I can navigate to it alongside the existing Audit Log and Users management pages.

#### Acceptance Criteria

1. THE Portal_System SHALL display a "System Logs" navigation link in the Administration section of the sidebar, positioned after the existing Audit Log link and before the Users link.
2. THE navigation link SHALL point to the route `/Admin/SystemLogs`.
3. THE navigation link SHALL only be visible to users with the SuperAdmin role, consistent with the Audit Log and Users links.
4. WHEN the user is on the System Logs Viewer page, THE sidebar SHALL highlight the "System Logs" link as the active navigation item.

### Requirement 6: Dependency Registration

**User Story:** As a platform developer, I want all System Logs Viewer services and repositories registered in the DI container, so that the feature integrates cleanly with the existing application startup configuration.

#### Acceptance Criteria

1. THE Portal_System SHALL register the Logging_DbContext as a scoped service with the `LoggingDb` connection string.
2. THE Portal_System SHALL register the System_Log_Query_Repository as a scoped service.
3. THE Portal_System SHALL register the ISystemLogQueryService and its implementation as a scoped service.
4. THE Portal_System SHALL register all System Logs Viewer dependencies in the same service registration location as other Portal services (Program.cs or the relevant DI extension method).

