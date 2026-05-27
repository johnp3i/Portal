# Requirements Document

## Introduction

The Audit & System Administration module provides business audit trail visibility and system-level administration tools for the Portal platform. It delivers automatic audit logging via an EF Core interceptor (complementing existing manual audit entries), a searchable audit log viewer for administrators, and a super admin user/module access management interface. This module is scoped to the SuperAdmin role and operates within the existing multi-tenant (BusinessId-scoped) architecture.

## Glossary

- **Audit_Interceptor**: An EF Core SaveChangesInterceptor that automatically captures entity changes (Insert, Update, Delete) and writes AuditLog records without requiring manual InsertAsync calls in service code.
- **AuditLog**: The append-only table in the [dbo] schema that stores all tracked data changes. Columns: Id, BusinessId, UserId, Action, TableName, RecordId, OldValues, NewValues, Timestamp.
- **Audit_Query_Service**: A service (IAuditLogQueryService) that provides filtered, paginated access to AuditLog records by table name, action type, user, and date range.
- **Audit_Controller**: An MVC controller restricted to SuperAdmin role that exposes audit log query endpoints for the admin UI.
- **Audit_Viewer**: The admin-facing UI page that displays audit log records in a searchable, filterable, paginated data table following the MyChair Design System.
- **Module_Access_Manager**: The super admin interface for granting and revoking module-level access (full, readonly, none) per user within a business.
- **User_Management_Screen**: The admin UI that lists all users within the current business, showing their status, roles, and module permissions.
- **SuperAdmin**: A platform role that bypasses all module access checks and has full administrative privileges.
- **Portal_System**: The Portal web application as a whole.
- **Current_Tenant**: The BusinessId resolved via ICurrentTenantService, used to scope all queries to the active business.

## Requirements

### Requirement 1: Automatic Audit Logging Interceptor

**User Story:** As a platform developer, I want entity changes to be automatically captured as audit records during SaveChanges, so that audit coverage is consistent without requiring manual InsertAsync calls in every service method.

#### Acceptance Criteria

1. WHEN PortalDbContext.SaveChangesAsync is invoked, THE Audit_Interceptor SHALL detect all Added, Modified, and Deleted entity entries in the ChangeTracker and write one AuditLog record per changed entity.
2. THE Audit_Interceptor SHALL populate the AuditLog.Action field with "Insert" for Added entities, "Update" for Modified entities, and "Delete" for Deleted entities.
3. THE Audit_Interceptor SHALL serialize the current property values as JSON into AuditLog.NewValues for Added and Modified entities, excluding navigation properties and shadow properties.
4. THE Audit_Interceptor SHALL serialize the original property values as JSON into AuditLog.OldValues for Modified and Deleted entities, excluding navigation properties and shadow properties.
5. THE Audit_Interceptor SHALL populate AuditLog.TableName with the entity's mapped table name from EF Core metadata.
6. WHEN the changed entity has an identity-generated primary key (Added entities), THE Audit_Interceptor SHALL capture the AuditLog.RecordId after the database INSERT completes so that the generated key value is available, converted to string.
7. THE Audit_Interceptor SHALL resolve AuditLog.BusinessId from ICurrentTenantService.CurrentBusinessId.
8. THE Audit_Interceptor SHALL resolve AuditLog.UserId from the current authenticated user's ClaimTypes.NameIdentifier claim via IHttpContextAccessor.
9. IF IHttpContextAccessor.HttpContext is null or the ClaimTypes.NameIdentifier claim is not present, THEN THE Audit_Interceptor SHALL set AuditLog.UserId to null and still persist the audit record.
10. THE Audit_Interceptor SHALL exclude AuditLog entities from interception to prevent infinite recursion.
11. THE Audit_Interceptor SHALL not interfere with existing manual audit log entries written by services — both automatic and manual entries coexist in the same AuditLog table.
12. IF SaveChangesAsync fails with an exception, THEN THE Audit_Interceptor SHALL not persist any audit records for that failed operation.
13. THE Audit_Interceptor SHALL record only properties where EF Core's IsModified flag is true in OldValues and NewValues for Update operations, excluding unchanged properties.
14. THE Audit_Interceptor SHALL populate AuditLog.Timestamp with the current UTC date and time at the moment the change is detected.

### Requirement 2: Audit Log Query Service

**User Story:** As a super admin, I want to search and filter audit log records by multiple criteria, so that I can investigate specific changes and trace data modifications.

#### Acceptance Criteria

1. THE Audit_Query_Service SHALL accept filter parameters: TableName (string, max 200 characters, optional), Action (string, one of "Insert", "Update", or "Delete", optional), UserId (string, max 450 characters, optional), DateFrom (DateTime, optional), and DateTo (DateTime, optional).
2. THE Audit_Query_Service SHALL scope all queries to the Current_Tenant BusinessId.
3. WHEN no filter parameters are provided, THE Audit_Query_Service SHALL return all AuditLog records for the current business ordered by Timestamp descending.
4. WHEN one or more filter parameters are provided, THE Audit_Query_Service SHALL apply all specified filters using AND logic, where DateFrom is inclusive (>=) and DateTo is inclusive (<=).
5. THE Audit_Query_Service SHALL support pagination with PageNumber (integer, minimum 1, default 1) and PageSize (integer, minimum 1, maximum 100, default 20) parameters.
6. THE Audit_Query_Service SHALL return a paged result containing: the list of AuditLog records, total record count, current page number, and total page count.
7. THE Audit_Query_Service SHALL return records ordered by Timestamp descending (most recent first).
8. IF PageNumber exceeds the total page count, THEN THE Audit_Query_Service SHALL return an empty record list with the correct total record count and total page count.
9. IF PageSize is less than 1 or greater than 100, THEN THE Audit_Query_Service SHALL clamp the value to the nearest bound (1 or 100) before executing the query.

### Requirement 3: Audit Controller

**User Story:** As a super admin, I want a dedicated admin endpoint to access audit log data, so that only authorized administrators can view the audit trail.

#### Acceptance Criteria

1. THE Audit_Controller SHALL require the SuperAdmin role for all actions.
2. WHEN a GET request is made to the index action, THE Audit_Controller SHALL return the Audit Viewer page.
3. WHEN a GET request is made to the search action with filter parameters, THE Audit_Controller SHALL invoke the Audit_Query_Service and return a JSON response containing a success flag, the paged audit log results, total record count, current page number, and total page count.
4. IF DateFrom is greater than DateTo when both are provided, THEN THE Audit_Controller SHALL return a JSON error response with success set to false and a message indicating the date range is invalid.
5. IF the Audit_Query_Service throws an exception during the search action, THEN THE Audit_Controller SHALL return a JSON error response with success set to false and a message indicating the search could not be completed.
6. THE Audit_Controller SHALL apply the ModuleAccessAttribute with the "audit" module and Full access level.

### Requirement 4: Audit Log Viewer UI

**User Story:** As a super admin, I want a searchable, filterable, paginated audit log viewer, so that I can visually browse and investigate data changes across the platform.

#### Acceptance Criteria

1. THE Audit_Viewer SHALL display a filter card with fields: Table Name (dropdown), Action (dropdown with Insert/Update/Delete options), User (dropdown), Date From (date picker), and Date To (date picker).
2. THE Audit_Viewer SHALL display audit records in a data table with columns: Timestamp (formatted as yyyy-MM-dd HH:mm:ss), User, Action, Table, Record ID, and a detail expand/view control.
3. WHEN the page loads, THE Audit_Viewer SHALL automatically invoke the search endpoint with no filters applied and display the first page of results ordered by Timestamp descending.
4. WHEN the filter button is clicked, THE Audit_Viewer SHALL call the search endpoint with the selected filter values, reset to page 1, and display the results.
5. THE Audit_Viewer SHALL display pagination controls below the data table showing a "Showing X–Y of Z" info label, current page number, total pages, and Previous/Next navigation buttons with individual page number buttons.
6. THE Audit_Viewer SHALL paginate results with a default page size of 20 records per page.
7. WHEN a record detail is expanded, THE Audit_Viewer SHALL display the OldValues and NewValues as formatted JSON, and for Update actions SHALL visually distinguish changed properties using a contrasting background color on changed property rows.
8. THE Audit_Viewer SHALL call BlockUI.show() before AJAX requests and BlockUI.hide() after completion in both success and error response paths.
9. THE Audit_Viewer SHALL follow the MyChair Design System: Manrope headings, Inter body text, glass card containers, and the filter card (margin-bottom 22px) + data table layout pattern.
10. WHEN no records match the filter criteria, THE Audit_Viewer SHALL display an empty state message indicating no audit records were found, rendered within the data table card.
11. IF the user selects a Date From value greater than the Date To value, THEN THE Audit_Viewer SHALL display a validation message indicating the invalid date range and SHALL NOT submit the search request.
12. WHEN the page loads, THE Audit_Viewer SHALL populate the Table Name dropdown with distinct table names from existing audit records and populate the User dropdown with users belonging to the current business.

### Requirement 5: Super Admin Module Access Management

**User Story:** As a super admin, I want to grant and revoke module access per user, so that I can control which platform modules each team member can access and at what level.

#### Acceptance Criteria

1. THE Module_Access_Manager SHALL display a list of all active users within the current business with their current module permissions, scoped to the Current_Tenant BusinessId.
2. WHEN the super admin selects a user, THE Module_Access_Manager SHALL display all available modules (from PortalModules.All) with the user's current access level for each, defaulting to None for modules with no existing UserBusinessPermission record.
3. WHEN the super admin changes a module access level for a user, THE Module_Access_Manager SHALL update the existing UserBusinessPermission record with the new access level, or create a new UserBusinessPermission record if none exists for that user-module combination.
4. THE Module_Access_Manager SHALL support three access levels per module: Full, ReadOnly, and None.
5. WHEN access level is set to None, THE Module_Access_Manager SHALL deactivate the UserBusinessPermission record by setting IsActive to false and DeactivatedAtUtc to the current UTC timestamp.
6. WHEN access level is changed from None to Full or ReadOnly, THE Module_Access_Manager SHALL reactivate or create a UserBusinessPermission record with IsActive set to true and DeactivatedAtUtc set to null.
7. THE Module_Access_Manager SHALL require confirmation via SweetAlert2 before applying permission changes, using confirmButtonColor '#C24A4A' for revocation (setting to None) and '#0D5EA6' for grants (setting to Full or ReadOnly).
8. THE Module_Access_Manager SHALL write an AuditLog entry for every permission change, recording Action as "Update", TableName as "UserBusinessPermission", RecordId as the UserBusinessPermission Id, and OldValues/NewValues containing the previous and new access level values.
9. THE Module_Access_Manager SHALL prevent the super admin from modifying their own permissions by disabling all access level controls for the currently authenticated user's row.
10. IF a permission change fails due to a server or database error, THEN THE Module_Access_Manager SHALL display an error message via SweetAlert2 indicating the operation could not be completed, and SHALL NOT modify the displayed access level from its previous state.
11. WHEN a permission change is successfully persisted, THE Module_Access_Manager SHALL display a success notification via SweetAlert2 confirming the module name and new access level applied.

### Requirement 6: Admin User Management Screen

**User Story:** As a super admin, I want a user management screen that shows all users in my business, so that I can view user status, manage access, and maintain team oversight.

#### Acceptance Criteria

1. THE User_Management_Screen SHALL display a data table listing all users associated with the current business, showing: Full Name, Email, Role, Status (Active/Inactive), and Last Login date formatted as "dd MMM yyyy HH:mm" or displaying "Never" if the user has not logged in.
2. THE User_Management_Screen SHALL provide a filter card with fields: search by name/email (text input, case-insensitive contains match, minimum 1 character) and status filter (Active/Inactive/All dropdown, default All).
3. WHEN the super admin clicks a user row, THE User_Management_Screen SHALL navigate to the Module_Access_Manager view for that user.
4. THE User_Management_Screen SHALL display a button to invite new users, linking to the existing invitation flow (InvitationController.Create).
5. WHEN the super admin clicks the deactivate action for a user, THE User_Management_Screen SHALL set UserBusiness.IsActive to false and UserBusiness.DeactivatedAtUtc to the current UTC timestamp.
6. WHEN the super admin clicks the reactivate action for a user, THE User_Management_Screen SHALL set UserBusiness.IsActive to true and UserBusiness.DeactivatedAtUtc to null.
7. WHEN the super admin initiates a deactivation action, THE User_Management_Screen SHALL display a SweetAlert2 confirmation dialog with confirmButtonColor '#C24A4A' before executing the deactivation.
8. THE User_Management_Screen SHALL write an AuditLog entry for user activation and deactivation actions, recording the UserId of the affected user, the Action performed ("Deactivate" or "Reactivate"), and the TableName "UserBusiness".
9. THE User_Management_Screen SHALL support pagination with a default page size of 20, and reset to page 1 when filter criteria are changed.
10. THE User_Management_Screen SHALL follow the MyChair Design System layout: topbar with heading, filter card with margin-bottom 22px, and data table card with pagination.
11. IF the super admin attempts to deactivate their own account, THEN THE User_Management_Screen SHALL prevent the action and display an informational SweetAlert2 message indicating that self-deactivation is not permitted.
12. IF a deactivation or reactivation request fails, THEN THE User_Management_Screen SHALL display a SweetAlert2 error dialog with a message indicating the operation could not be completed.
