# Requirements Document

## Introduction

The Activity Log is a business-facing redesign of the existing Audit Log viewer. It transforms the raw, developer-oriented audit data table into a timeline-style activity feed that business managers can use to track who did what and when across their operations. The underlying `[audit].[AuditLog]` table, `AuditLogQueryService`, and `AuditLogQueryRepository` remain unchanged — this feature builds a new presentation layer on top of the existing query infrastructure. The Activity Log is available on Professional and Enterprise subscription plans (using the existing `audit_log` plan feature key), accessed via a business-level route rather than the admin-only path.

## Glossary

- **Activity_Log_Controller**: An MVC controller at a business-level route (e.g., `/Activity`) that serves the Activity Log page and AJAX endpoints. Requires the `audit_log` module via ModuleAccess with ReadOnly level (not SuperAdmin-only).
- **Activity_Summary_Service**: A service that transforms raw AuditLog records into plain-English activity summaries using the Action, TableName, RecordId, OldValues, and NewValues fields.
- **Activity_Feed**: The timeline-style UI that displays activity entries with colored dot indicators, plain-English summaries, relative timestamps, and expandable detail panels.
- **Quick_Stats_Service**: A service that computes weekly summary statistics from AuditLog data: total changes this week, count of distinct active team members, most active area (TableName), and timestamp of the last activity.
- **User_Name_Resolver**: A component that resolves AuditLog.UserId values to display names by querying the MembershipDbContext UserBusinesses relationship.
- **Activity_Filter**: Business-friendly filter parameters: "What changed" (mapped from TableName), "Who made the change" (mapped from UserId), "What type of change" (mapped from Action), Date From, and Date To.
- **Relative_Timestamp_Formatter**: A utility that converts UTC DateTime values into human-readable relative strings such as "2 min ago", "Yesterday at 14:32", or "3 days ago".
- **Portal_System**: The Portal web application as a whole.
- **Current_Tenant**: The BusinessId resolved via ICurrentTenantService, used to scope all queries to the active business.
- **AuditLog**: The existing append-only table in the [audit] schema that stores tracked data changes with columns: Id, BusinessId, UserId, Action, TableName, RecordId, OldValues, NewValues, Timestamp.

## Requirements

### Requirement 1: Activity Log Route and Access Control

**User Story:** As a business manager, I want to access my Activity Log from a business-level menu location, so that I can track team activity without needing super admin privileges.

#### Acceptance Criteria

1. THE Activity_Log_Controller SHALL be accessible at the route `/Activity` under standard authenticated access.
2. THE Activity_Log_Controller SHALL apply the ModuleAccess attribute with module key `audit_log` and access level ReadOnly.
3. THE Activity_Log_Controller SHALL NOT require the SuperAdmin role — any authenticated user with the `audit_log` module assigned at ReadOnly or Full level SHALL have access.
4. THE Activity_Log_Controller SHALL scope all data queries to the Current_Tenant BusinessId via ICurrentTenantService.
5. THE Portal_System SHALL place the Activity Log navigation entry in the "Business Operations" sidebar section, not the "Administration" section.
6. WHEN a user without the `audit_log` module in their subscription plan attempts to access `/Activity`, THE Portal_System SHALL display the UpgradeRequired view.

### Requirement 2: Activity Summary Transformation

**User Story:** As a business manager, I want to see plain-English descriptions of changes instead of raw table and action names, so that I can understand what happened without technical knowledge.

#### Acceptance Criteria

1. THE Activity_Summary_Service SHALL transform each AuditLog record into a plain-English summary string that includes: the actor name, the action verb, the entity type, the entity identifier, and contextual detail when available.
2. WHEN Action is "Insert", THE Activity_Summary_Service SHALL generate a summary using the verb "created" followed by the entity type and identifier (e.g., "John P. created Invoice INV-2026-0089").
3. WHEN Action is "Update", THE Activity_Summary_Service SHALL generate a summary using the verb "edited" followed by the entity type and a brief description of what changed derived from the NewValues JSON keys (e.g., "Maria T. edited Customer Acme Solutions Ltd — updated email address").
4. WHEN Action is "Delete", THE Activity_Summary_Service SHALL generate a summary using the verb "deleted" followed by the entity type and identifier with a parenthetical summary of key values at deletion (e.g., "John P. deleted Purchase PUR-2026-0034 (Office Supplies, €145.00)").
5. WHEN Action is "Update" and the changed fields include a status-type column (columns ending in "StatusTypeId" or named "Status"), THE Activity_Summary_Service SHALL generate a summary using "changed status of" and include the old and new status values (e.g., "John P. changed status of Invoice INV-2026-0089 from Draft to Issued").
6. THE Activity_Summary_Service SHALL resolve the entity identifier from the RecordId field combined with NewValues or OldValues JSON to produce a human-readable reference (e.g., invoice number, customer name, quotation number).
7. IF the NewValues or OldValues JSON cannot be parsed or the entity identifier cannot be resolved, THEN THE Activity_Summary_Service SHALL fall back to displaying the raw TableName and RecordId in the summary.
8. THE Activity_Summary_Service SHALL map TableName values to business-friendly entity types: "Invoice" for Invoice-related tables, "Quotation" for Quotation-related tables, "Customer" for Customer table, "Purchase" for Purchase-related tables, "Payment" for Payment-related tables, "Credit Note" for CreditNote-related tables, and "Settings" for configuration tables.

### Requirement 3: User Name Resolution

**User Story:** As a business manager, I want to see team member names instead of user IDs in the activity feed, so that I understand who performed each action.

#### Acceptance Criteria

1. THE User_Name_Resolver SHALL resolve each AuditLog.UserId to a display name by querying MembershipDbContext for the UserBusiness record matching the UserId and Current_Tenant BusinessId, returning "{FirstName} {LastInitial}." format (e.g., "John P.").
2. WHEN UserId is null, THE User_Name_Resolver SHALL return "System" as the display name.
3. IF a UserId cannot be resolved to a user record in MembershipDbContext, THEN THE User_Name_Resolver SHALL return "Unknown User" as the display name.
4. THE User_Name_Resolver SHALL batch-resolve all unique UserIds in a page of results in a single database query to avoid N+1 performance issues.

### Requirement 4: Relative Timestamp Formatting

**User Story:** As a business manager, I want to see how long ago each activity occurred in natural language, so that I can quickly gauge recency without calculating time differences.

#### Acceptance Criteria

1. WHEN the activity Timestamp is less than 60 seconds from the current UTC time, THE Relative_Timestamp_Formatter SHALL display "Just now".
2. WHEN the activity Timestamp is between 1 minute and 59 minutes from the current UTC time, THE Relative_Timestamp_Formatter SHALL display "{N} min ago" where N is the rounded-down number of minutes.
3. WHEN the activity Timestamp is between 1 hour and 23 hours from the current UTC time, THE Relative_Timestamp_Formatter SHALL display "{N} hour ago" (singular) or "{N} hours ago" (plural) where N is the rounded-down number of hours.
4. WHEN the activity Timestamp falls on the calendar day immediately before the current UTC date, THE Relative_Timestamp_Formatter SHALL display "Yesterday at {HH:mm}" using the business's local time.
5. WHEN the activity Timestamp is between 2 and 6 calendar days before the current UTC date, THE Relative_Timestamp_Formatter SHALL display "{N} days ago".
6. WHEN the activity Timestamp is 7 or more calendar days before the current UTC date, THE Relative_Timestamp_Formatter SHALL display the full date formatted as "dd MMM yyyy".
7. THE Relative_Timestamp_Formatter SHALL compute all comparisons using UTC to ensure consistency across time zones.

### Requirement 5: Quick Stats Computation

**User Story:** As a business manager, I want to see a weekly summary at the top of my Activity Log, so that I get an at-a-glance overview of team activity levels.

#### Acceptance Criteria

1. THE Quick_Stats_Service SHALL compute the total number of AuditLog records for the Current_Tenant within the last 7 calendar days (from current UTC date minus 6 days at 00:00 UTC through the current UTC timestamp).
2. THE Quick_Stats_Service SHALL compute the count of distinct UserId values that appear in AuditLog records for the Current_Tenant within the last 7 calendar days, excluding null UserId values (system actions).
3. THE Quick_Stats_Service SHALL determine the most active area by finding the TableName with the highest record count for the Current_Tenant within the last 7 calendar days, mapped to its business-friendly name.
4. THE Quick_Stats_Service SHALL return the Timestamp of the most recent AuditLog record for the Current_Tenant, formatted using the Relative_Timestamp_Formatter.
5. IF no AuditLog records exist for the Current_Tenant within the last 7 calendar days, THEN THE Quick_Stats_Service SHALL return zero for changes count, zero for team members, "None" for most active area, and "No recent activity" for last activity.

### Requirement 6: Activity Feed UI

**User Story:** As a business manager, I want to browse my team's activity in a visual timeline with expandable details, so that I can quickly scan recent changes and drill into specifics when needed.

#### Acceptance Criteria

1. THE Activity_Feed SHALL display a quick stats row at the top of the page showing four stat cards: "Changes this week" (count), "By team members" (count with "people" label), "Most active area" (area name), and "Last activity" (relative timestamp).
2. THE Activity_Feed SHALL display a filter card below the stats row with fields: "What changed" (dropdown mapped to TableName categories), "Who made the change" (dropdown of team member display names plus "Everyone" and "System" options), "What type of change" (dropdown: "All changes", "Created", "Edited", "Deleted", "Status changed"), "Date from" (date input), "Date to" (date input), a Filter button, and a Clear button.
3. THE Activity_Feed SHALL display activities in a timeline layout with a vertical line connecting entries, where each entry shows: a colored dot indicator, a plain-English summary, a relative timestamp, and an expand/collapse control.
4. THE Activity_Feed SHALL use colored dot indicators per action type: green for Created, blue for Edited, red for Deleted, and amber for Status Changed.
5. WHEN a user clicks on an activity row or its expand control, THE Activity_Feed SHALL toggle the detail panel for that entry with a slide-down animation.
6. WHEN the detail panel is expanded for a Created action, THE Activity_Feed SHALL display a "Created with values" table showing field names and their initial values parsed from NewValues JSON.
7. WHEN the detail panel is expanded for an Update action, THE Activity_Feed SHALL display a "What changed" table showing field names with old values (styled with strikethrough in red) and new values (styled in bold green) parsed from OldValues and NewValues JSON.
8. WHEN the detail panel is expanded for a Delete action, THE Activity_Feed SHALL display a "Deleted record" table showing field names and their values at the time of deletion parsed from OldValues JSON.
9. THE Activity_Feed SHALL display pagination controls below the activity list showing "Showing X–Y of Z" info and page number buttons, with a default page size of 8 records per page.
10. WHEN the page loads, THE Activity_Feed SHALL automatically fetch the first page of activities with no filters applied and display results ordered by Timestamp descending.
11. WHEN the Filter button is clicked, THE Activity_Feed SHALL fetch results matching the selected filter criteria, reset to page 1, and update the display.
12. WHEN the Clear button is clicked, THE Activity_Feed SHALL reset all filter fields to their default values (all options, no dates) and fetch the unfiltered first page.
13. THE Activity_Feed SHALL call BlockUI.show('Loading activity...') before AJAX requests and BlockUI.hide() after completion in both success and error response paths.
14. IF an AJAX request fails, THEN THE Activity_Feed SHALL display a SweetAlert2 error dialog with title "Error" and message "Could not load activity data. Please try again." using confirmButtonColor '#0D5EA6'.
15. WHEN no activity records match the current filters, THE Activity_Feed SHALL display an empty state message within the main content card.
16. THE Activity_Feed SHALL follow the MyChair Design System: Manrope headings (42px page title), Inter body text, glass card containers with 20px border-radius, filter card with margin-bottom 22px, and the standard topbar with eyebrow label "Business Operations".

### Requirement 7: Business-Friendly Filter Mapping

**User Story:** As a business manager, I want filters labeled in business terms rather than technical database terms, so that I can find what I need without understanding the underlying schema.

#### Acceptance Criteria

1. THE Activity_Filter "What changed" dropdown SHALL present options mapped from TableName values: "Everything" (no filter), "Invoices" (Invoice, InvoiceLine tables), "Quotations" (Quotation, QuotationLine, QuotationContact tables), "Customers" (Customer table), "Purchases" (Purchase table), "Payments" (Payment table), "Credit Notes" (CreditNote, CreditNoteLine tables), and "Settings" (Business, BusinessProfile tables).
2. THE Activity_Filter "Who made the change" dropdown SHALL present "Everyone" (no filter) followed by display names of all users in the current business resolved from MembershipDbContext, plus "System" (filters to null UserId).
3. THE Activity_Filter "What type of change" dropdown SHALL present: "All changes" (no filter), "Created" (maps to Action "Insert"), "Edited" (maps to Action "Update"), "Deleted" (maps to Action "Delete"), and "Status changed" (maps to Action "Update" where changed fields include status-type columns).
4. WHEN the "Status changed" filter is selected, THE Activity_Log_Controller SHALL filter results to Update actions where OldValues or NewValues JSON contains a key ending in "StatusTypeId" or named "Status".
5. THE Activity_Filter "Date from" and "Date to" inputs SHALL accept date values and pass them to the query service as inclusive bounds.

### Requirement 8: Entity Detail Links

**User Story:** As a business manager, I want entity names and identifiers in the activity feed to link to their detail pages, so that I can navigate directly to the referenced record.

#### Acceptance Criteria

1. WHEN an activity summary references an Invoice, THE Activity_Feed SHALL render the invoice identifier as a hyperlink navigating to the Invoice detail page at `/Invoice/Details/{id}`.
2. WHEN an activity summary references a Customer, THE Activity_Feed SHALL render the customer name as a hyperlink navigating to the Customer detail page at `/Customer/Details/{id}`.
3. WHEN an activity summary references a Quotation, THE Activity_Feed SHALL render the quotation identifier as a hyperlink navigating to the Quotation detail page at `/Quotation/Details/{id}`.
4. WHEN an activity summary references a Purchase, THE Activity_Feed SHALL render the purchase identifier as a hyperlink navigating to the Purchase detail page at `/Purchase/Details/{id}`.
5. IF the referenced entity has been deleted (Action is "Delete"), THEN THE Activity_Feed SHALL render the entity identifier as plain text without a hyperlink.
6. IF the entity type does not have a known detail page route, THEN THE Activity_Feed SHALL render the identifier as plain text.

### Requirement 9: Mobile Responsive Layout

**User Story:** As a business manager using a mobile device, I want the Activity Log to adapt to smaller screens, so that I can review team activity on the go.

#### Acceptance Criteria

1. WHEN viewport width is 640px or less, THE Activity_Feed SHALL stack the quick stats row into a 2-column grid instead of 4-column.
2. WHEN viewport width is 640px or less, THE Activity_Feed SHALL display filter fields in a vertical stack with each field taking full width.
3. WHEN viewport width is 640px or less, THE Activity_Feed SHALL hide the vertical timeline line and reduce horizontal padding on activity rows.
4. WHEN viewport width is 640px or less, THE Activity_Feed SHALL render detail panels at full width without left margin offset.

### Requirement 10: Sidebar Navigation Placement

**User Story:** As a business manager, I want the Activity Log to appear in a logical location in the sidebar, so that I can find it intuitively alongside other business operations.

#### Acceptance Criteria

1. THE Portal_System SHALL display an "Activity Log" navigation item in the "Business Operations" sidebar section.
2. THE Portal_System SHALL remove the existing "Audit Log" link from the "Administration" sidebar section.
3. THE Portal_System SHALL use an appropriate activity/timeline icon for the Activity Log sidebar item.
4. WHEN the user does not have the `audit_log` module in their subscription plan, THE Portal_System SHALL hide the "Activity Log" sidebar item.
