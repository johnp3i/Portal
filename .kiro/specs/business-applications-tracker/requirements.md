# Requirements Document

## Introduction

This document defines the requirements for the Business Applications Tracker (Compliance Filings) module — a feature that enables businesses to track statutory filing deadlines such as tax returns, social insurance contributions, VAT returns, annual levies, and employer declarations. SuperAdmins maintain a country-specific template catalog of filing types, and business users import relevant templates to track their own submissions. The module supports a status workflow (Pending → In Progress → Submitted → Approved / Rejected), overdue warnings, attachment upload for submission evidence, a dashboard widget for upcoming filings, and a calendar year view. The feature is gated to the Professional plan tier and above via the `compliance` module key.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application that provides multi-tenant back-office operations
- **Business**: A registered organization on the Portal with users, subscriptions, and data
- **SuperAdmin**: A platform-level administrator who manages global configuration, templates, and system-wide settings
- **Application_Category**: A classification grouping for filing types (e.g., Tax, Employee, Regulatory, Business Registration), stored in the `[compliance]` schema
- **Application_Type**: A template record defining a statutory filing (Name, Description, Country, Category, Frequency, DefaultDueMonth, DefaultDueDay), created and managed by SuperAdmin, stored in the `[compliance]` schema
- **Business_Application**: A per-business instance of a filing obligation derived from an Application_Type, containing DueDate, Status, ReferenceNumber, Notes, and timestamps, stored in the `[compliance]` schema
- **Application_Attachment**: A file record linked to a Business_Application representing submission evidence (PDF), stored in the `[compliance]` schema
- **Compliance_Controller**: The MVC controller handling HTTP requests for business-facing compliance tracking operations
- **Admin_Compliance_Controller**: The MVC controller handling SuperAdmin template catalog management operations
- **Filing_Frequency**: An enumeration of how often a filing recurs — Monthly, Quarterly, Annual, or One-off
- **Application_Status**: The workflow status of a Business_Application — Pending, InProgress, Submitted, Approved, or Rejected
- **Dashboard_Widget**: A card component on the main dashboard displaying upcoming filing deadlines within configurable time horizons (30/60/90 days)
- **Calendar_View**: A year-view calendar showing all filing deadlines for a business within a selected year
- **Overdue_Warning**: A visual indicator shown when a filing's due date is approaching (7 days, 3 days) or has passed

## Requirements

### Requirement 1: Application Category Data Model

**User Story:** As a system architect, I want filing categories stored in a dedicated reference table, so that application types can be organized into logical groupings (Tax, Employee, Regulatory, Business Registration).

#### Acceptance Criteria

1. THE Portal database SHALL contain an ApplicationCategory table in the `[compliance]` schema with columns: Id (INT IDENTITY PK), Name (NVARCHAR(100) NOT NULL), Description (NVARCHAR(500) NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE ApplicationCategory table SHALL be seeded with initial values: Tax, Employee, Regulatory, Business Registration
3. THE ApplicationCategory.Name column SHALL have a unique constraint to prevent duplicate category names

### Requirement 2: Application Type Template Data Model

**User Story:** As a system architect, I want a template catalog of filing types stored in the database, so that SuperAdmins can define country-specific statutory obligations that businesses can import.

#### Acceptance Criteria

1. THE Portal database SHALL contain an ApplicationType table in the `[compliance]` schema with columns: Id (INT IDENTITY PK), Name (NVARCHAR(200) NOT NULL), Description (NVARCHAR(1000) NULL), Country (NVARCHAR(100) NOT NULL), ApplicationCategoryId (INT NOT NULL FK → ApplicationCategory), Frequency (NVARCHAR(20) NOT NULL), DefaultDueMonth (INT NULL), DefaultDueDay (INT NULL), IsActive (BIT NOT NULL DEFAULT 1), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE ApplicationType.Frequency column SHALL accept only the values: Monthly, Quarterly, Annual, One-off
3. THE ApplicationType.DefaultDueMonth SHALL contain values 1–12 representing the month in which the filing is typically due (NULL for Monthly filings where every month applies)
4. THE ApplicationType.DefaultDueDay SHALL contain values 1–31 representing the day of the month by which the filing is typically due
5. THE ApplicationType table SHALL have a composite unique constraint on (Name, Country) to prevent duplicate templates for the same country
6. THE ApplicationType table SHALL be seeded with default templates for Cyprus: IR7 Annual Tax Return (Annual, Category: Tax), Social Insurance Monthly (Monthly, Category: Employee), VAT Return (Quarterly, Category: Tax), Annual Levy (Annual, Category: Regulatory), Employer's Declaration (Annual, Category: Employee)

### Requirement 3: Business Application Data Model

**User Story:** As a system architect, I want per-business filing instances stored in the database, so that each business can independently track their own submissions against template-defined obligations.

#### Acceptance Criteria

1. THE Portal database SHALL contain a BusinessApplication table in the `[compliance]` schema with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → Business), ApplicationTypeId (INT NOT NULL FK → ApplicationType), DueDate (DATE NOT NULL), Status (NVARCHAR(20) NOT NULL DEFAULT 'Pending'), ReferenceNumber (NVARCHAR(100) NULL), Notes (NVARCHAR(2000) NULL), SubmittedAtUtc (DATETIME NULL), ApprovedAtUtc (DATETIME NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE BusinessApplication.Status column SHALL accept only the values: Pending, InProgress, Submitted, Approved, Rejected
3. THE BusinessApplication table SHALL have a foreign key from BusinessId to the Business table
4. THE BusinessApplication table SHALL have an index on (BusinessId, DueDate) for efficient queries of upcoming filings
5. THE BusinessApplication table SHALL have an index on (BusinessId, Status) for efficient filtering by status

### Requirement 4: Application Attachment Data Model

**User Story:** As a system architect, I want attachment records linked to business applications, so that users can upload submission evidence (e.g., confirmation PDFs) for each filing.

#### Acceptance Criteria

1. THE Portal database SHALL contain an ApplicationAttachment table in the `[compliance]` schema with columns: Id (INT IDENTITY PK), BusinessApplicationId (INT NOT NULL FK → BusinessApplication), FileName (NVARCHAR(255) NOT NULL), OriginalFileName (NVARCHAR(255) NOT NULL), FilePath (NVARCHAR(500) NOT NULL), ContentType (NVARCHAR(100) NOT NULL), FileSizeBytes (BIGINT NOT NULL), UploadedByUserId (NVARCHAR(450) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE ApplicationAttachment table SHALL have a foreign key from BusinessApplicationId to the BusinessApplication table with cascade delete disabled
3. THE ApplicationAttachment table SHALL enforce a maximum of 3 attachments per BusinessApplication

### Requirement 5: SuperAdmin Template Catalog Management

**User Story:** As a SuperAdmin, I want to create, edit, and deactivate filing type templates, so that I can maintain an accurate catalog of statutory obligations per country.

#### Acceptance Criteria

1. WHEN a SuperAdmin accesses the compliance template management page, THE Admin_Compliance_Controller SHALL display all ApplicationType records with their associated ApplicationCategory names, ordered by Country then Name
2. WHEN a SuperAdmin creates a new ApplicationType, THE Admin_Compliance_Controller SHALL validate that Name is not empty, Country is not empty, Frequency is a valid value, and ApplicationCategoryId references an active category
3. WHEN a SuperAdmin edits an existing ApplicationType, THE Admin_Compliance_Controller SHALL update the record and return a success response
4. WHEN a SuperAdmin deactivates an ApplicationType, THE Admin_Compliance_Controller SHALL set IsActive to false — existing BusinessApplication records referencing the type SHALL remain unaffected
5. WHEN a SuperAdmin creates or edits an ApplicationType, THE Admin_Compliance_Controller SHALL validate that no duplicate (Name, Country) combination exists among active templates
6. WHEN a SuperAdmin accesses the category management section, THE Admin_Compliance_Controller SHALL allow creating and editing ApplicationCategory records

### Requirement 6: Business Template Import

**User Story:** As a business user, I want to import filing templates relevant to my business, so that I can begin tracking my statutory obligations without manually entering each filing type.

#### Acceptance Criteria

1. WHEN a business user accesses the import flow, THE Compliance_Controller SHALL display available ApplicationType templates filtered by country, grouped by ApplicationCategory
2. WHEN a business user selects one or more templates to import, THE Compliance_Controller SHALL create BusinessApplication records for the current year with due dates calculated from the template's DefaultDueMonth and DefaultDueDay
3. WHEN importing a Monthly frequency template, THE Compliance_Controller SHALL create 12 BusinessApplication records (one per month) for the selected year with DueDate set to the DefaultDueDay of each month
4. WHEN importing a Quarterly frequency template, THE Compliance_Controller SHALL create 4 BusinessApplication records (one per quarter) for the selected year with DueDate calculated from the template's defaults
5. WHEN importing an Annual frequency template, THE Compliance_Controller SHALL create 1 BusinessApplication record with DueDate set to the template's DefaultDueMonth and DefaultDueDay in the selected year
6. WHEN importing a One-off frequency template, THE Compliance_Controller SHALL create 1 BusinessApplication record with DueDate specified by the user during import
7. IF a business already has active (non-completed) applications for the same ApplicationTypeId in the same period, THEN THE Compliance_Controller SHALL warn the user about potential duplicates before importing
8. THE import operation SHALL be scoped to the authenticated user's BusinessId

### Requirement 7: Business Application List View

**User Story:** As a business user, I want to view all my filing obligations in a filterable list, so that I can monitor their status and identify which filings need attention.

#### Acceptance Criteria

1. WHEN a business user accesses the compliance list page, THE Compliance_Controller SHALL display all BusinessApplication records for the user's BusinessId with columns: Application Name (from ApplicationType), Category, DueDate, Status, ReferenceNumber, and an attachment indicator
2. THE list view SHALL support filtering by: ApplicationCategory, Status, and date range (DueDate from/to)
3. THE list view SHALL display records ordered by DueDate ascending (nearest deadlines first) by default
4. THE list view SHALL support pagination with 15 records per page
5. WHEN a Business_Application has a DueDate within 7 days from today and Status is Pending or InProgress, THE list view SHALL display a warning indicator (amber)
6. WHEN a Business_Application has a DueDate within 3 days from today and Status is Pending or InProgress, THE list view SHALL display an urgent indicator (red)
7. WHEN a Business_Application has a DueDate in the past and Status is Pending or InProgress, THE list view SHALL display an overdue indicator (red, bold)

### Requirement 8: Business Application Detail and Edit

**User Story:** As a business user, I want to view and update my filing records, so that I can track progress, add notes, attach evidence, and update status as filings are submitted and approved.

#### Acceptance Criteria

1. WHEN a business user opens an application detail page, THE Compliance_Controller SHALL display: Application Name, Category, Frequency, DueDate, Status, ReferenceNumber, Notes, SubmittedAtUtc, ApprovedAtUtc, and the list of attachments
2. WHEN a business user updates the Status field, THE Compliance_Controller SHALL validate the transition is permitted according to the workflow rules
3. WHEN Status transitions to Submitted, THE Compliance_Controller SHALL set SubmittedAtUtc to the current UTC timestamp
4. WHEN Status transitions to Approved, THE Compliance_Controller SHALL set ApprovedAtUtc to the current UTC timestamp
5. WHEN a business user updates ReferenceNumber or Notes, THE Compliance_Controller SHALL persist the changes and return a success response
6. THE detail page SHALL display the application's current overdue/warning state consistent with the list view indicators

### Requirement 9: Status Workflow Rules

**User Story:** As a platform operator, I want controlled status transitions, so that filing records follow a logical progression and cannot be set to invalid states.

#### Acceptance Criteria

1. THE system SHALL enforce the following valid status transitions: Pending → InProgress, Pending → Submitted, InProgress → Submitted, Submitted → Approved, Submitted → Rejected, Rejected → InProgress
2. IF a user attempts a status transition not in the permitted set, THEN THE Compliance_Controller SHALL reject the request with an error message indicating the transition is not allowed
3. WHEN Status transitions to Submitted, Approved, or Rejected, THE system SHALL NOT allow the Status to return to Pending
4. WHEN Status is Approved, THE system SHALL NOT allow any further status transitions — the record is finalized

### Requirement 10: Attachment Upload for Business Applications

**User Story:** As a business user, I want to upload PDF evidence of submission to my filing records, so that I can maintain proof of compliance within the platform.

#### Acceptance Criteria

1. WHEN a business user uploads a file to a Business_Application, THE Compliance_Controller SHALL validate the file is a PDF (application/pdf, .pdf extension) and does not exceed 5 MB
2. WHEN a valid file is uploaded, THE system SHALL store the file using the existing File_Storage_Service and create an ApplicationAttachment record
3. IF an application already has 3 attachments, THEN THE Compliance_Controller SHALL reject the upload with an error message indicating the maximum attachment count has been reached
4. WHEN an upload succeeds, THE Compliance_Controller SHALL return a JSON response with the attachment Id, OriginalFileName, FileSizeBytes, and CreatedAtUtc
5. THE upload SHALL be scoped to the user's BusinessId — the controller SHALL verify the Business_Application belongs to the user's business before accepting the file
6. WHEN a business user requests deletion of an attachment, THE Compliance_Controller SHALL remove the ApplicationAttachment record and the physical file from storage

### Requirement 11: Dashboard Widget — Upcoming Filings

**User Story:** As a business user, I want to see upcoming filing deadlines on my dashboard, so that I can stay aware of approaching obligations without navigating to the compliance module.

#### Acceptance Criteria

1. THE Dashboard SHALL display an "Upcoming Filings" widget showing Business_Applications due within the next 30 days that have Status of Pending or InProgress
2. THE widget SHALL display each filing's Application Name, DueDate, and Status
3. THE widget SHALL show a maximum of 5 items, with a "View All" link navigating to the full compliance list
4. WHEN a filing is due within 7 days, THE widget SHALL highlight the item with a warning colour
5. WHEN a filing is overdue (past DueDate with Status Pending or InProgress), THE widget SHALL highlight the item with a danger colour and display "OVERDUE" label
6. WHEN no filings are due within 30 days, THE widget SHALL display a message: "No filings due in the next 30 days"
7. THE widget SHALL be scoped to the authenticated user's BusinessId

### Requirement 12: Calendar Year View

**User Story:** As a business user, I want to see all my filing deadlines on a calendar for the entire year, so that I can visualize my compliance schedule and plan ahead.

#### Acceptance Criteria

1. WHEN a business user accesses the calendar view, THE Compliance_Controller SHALL display a 12-month grid showing all BusinessApplication records for the selected year belonging to the user's BusinessId
2. THE calendar view SHALL display filing deadlines as coloured markers on their respective DueDate cells — colour coded by Status (Pending: grey, InProgress: blue, Submitted: amber, Approved: green, Rejected: red)
3. THE calendar view SHALL allow the user to select the year (current year displayed by default, with navigation to previous and next years)
4. WHEN a user clicks on a calendar day containing filings, THE view SHALL display a popover or expandable section listing the filing names and statuses for that day
5. THE calendar view SHALL highlight today's date for orientation
6. THE calendar view SHALL visually distinguish overdue filings (past DueDate with Pending or InProgress status) with a distinct marker style

### Requirement 13: Overdue Warnings and Notifications

**User Story:** As a business user, I want visual warnings when filings are approaching or past their due dates, so that I can take action before deadlines are missed.

#### Acceptance Criteria

1. WHEN a Business_Application has a DueDate exactly 7 days from today and Status is Pending or InProgress, THE list view and detail page SHALL display a "Due in 7 days" warning badge in amber
2. WHEN a Business_Application has a DueDate exactly 3 days from today and Status is Pending or InProgress, THE list view and detail page SHALL display a "Due in 3 days" urgent badge in red
3. WHEN a Business_Application has a DueDate in the past and Status is Pending or InProgress, THE list view and detail page SHALL display an "OVERDUE" badge in danger colour with the number of days overdue
4. WHEN a Business_Application Status transitions to Submitted, Approved, or Rejected, THE system SHALL suppress all overdue/warning indicators for that record
5. THE overdue calculation SHALL use UTC date comparison to ensure consistency across time zones

### Requirement 14: Plan Permission Gating

**User Story:** As a platform operator, I want the compliance module gated to Professional plan and above, so that it serves as a value differentiator for the paid tier.

#### Acceptance Criteria

1. THE PlanPermissionFilter SHALL gate access to the Compliance_Controller and Admin_Compliance_Controller using the `compliance` module key
2. WHEN a Foundation plan user navigates to the compliance section, THE Portal SHALL display a soft-gate teaser view explaining the feature is available on Professional plan and above, with a brief value description and upgrade link
3. WHEN a Professional or Enterprise plan user accesses the compliance section, THE Portal SHALL render the full compliance tracking interface
4. THE dashboard widget SHALL only render for users whose business subscription includes the `compliance` module

### Requirement 15: Tenant Isolation and Security

**User Story:** As a security-conscious operator, I want strict tenant isolation on all compliance operations, so that a user from one business can never access filings or attachments belonging to another business.

#### Acceptance Criteria

1. THE Compliance_Controller SHALL resolve the authenticated user's BusinessId from session claims and use it as a mandatory filter on every query and operation
2. IF a user attempts to access a Business_Application belonging to a different business via direct URL, THEN THE Compliance_Controller SHALL return HTTP 404 without revealing that the record exists
3. THE attachment download endpoint SHALL verify both the BusinessApplication.BusinessId matches the requesting user's BusinessId before serving the file
4. THE import flow SHALL only create records scoped to the authenticated user's BusinessId regardless of any BusinessId value submitted in the request

### Requirement 16: AJAX Interaction Pattern

**User Story:** As a user, I want status updates, attachment uploads, and import operations to execute via AJAX without full page reloads, so that the experience feels responsive.

#### Acceptance Criteria

1. WHEN a status update is initiated, THE UI SHALL display BlockUI to prevent interaction, execute the AJAX request, then unblock and display a SweetAlert2 success or error message
2. WHEN an attachment upload begins, THE UI SHALL display BlockUI, execute the upload request, then unblock and refresh the attachment list with a SweetAlert2 result message
3. WHEN an attachment delete is requested, THE UI SHALL display a SweetAlert2 confirmation dialog with a warning icon before proceeding
4. WHEN the template import is confirmed, THE UI SHALL display BlockUI during processing and show a SweetAlert2 success message with the count of filings created
5. ALL AJAX error responses SHALL unblock the UI and display a SweetAlert2 error message with the specific failure reason

### Requirement 17: Mobile Responsive Design

**User Story:** As a mobile user, I want to access and manage my filing obligations from a phone or tablet, so that I can update statuses and upload evidence on the go.

#### Acceptance Criteria

1. THE compliance list view SHALL render responsively, with columns stacking or hiding non-essential data (e.g., ReferenceNumber) on viewports narrower than 576px
2. THE calendar view SHALL adapt to mobile viewports by switching from a 12-month grid to a scrollable single-month view on viewports narrower than 768px
3. THE dashboard widget SHALL render as a full-width card on mobile viewports
4. THE attachment upload control SHALL support the mobile device file picker and camera access for capturing PDF documents
5. THE detail/edit page SHALL stack form fields vertically on narrow viewports with adequate touch target sizing (minimum 44px height)


### Requirement 18: Estimated Amount per Filing

**User Story:** As a business user, I want to record the estimated cost of each compliance filing, so that I can forecast expenses and track compliance-related costs.

#### Acceptance Criteria

1. WHEN an ApplicationType template is created by a SuperAdmin, THEN they SHALL be able to specify an optional `EstimatedAmount` (decimal, nullable) representing the typical cost of the filing.
2. WHEN a business user imports templates, THEN each generated BusinessApplication SHALL inherit the template's `EstimatedAmount` as the default.
3. WHEN a business user views the filing detail page, THEN the `EstimatedAmount` SHALL be displayed as an editable field that can be overridden per filing.
4. WHEN a business user saves filing details, THEN the system SHALL persist the `EstimatedAmount` alongside ReferenceNumber and Notes.
5. WHEN the filing list is displayed, THEN the `EstimatedAmount` SHALL appear as a column showing the cost (or "—" if null).
6. WHEN a business user creates a custom filing, THEN they SHALL be able to specify an optional `EstimatedAmount`.
7. THE `EstimatedAmount` column SHALL be of type DECIMAL(18,2) NULL on both `[compliance].[ApplicationType]` and `[compliance].[BusinessApplication]`.

### Requirement 19: Multi-Year Filing Frequency

**User Story:** As a business user, I want to track compliance filings that recur every multiple years (e.g., every 4 years for a tourism licence), so that I can manage long-cycle obligations alongside annual/monthly ones.

#### Acceptance Criteria

1. WHEN a SuperAdmin creates an ApplicationType template, THEN they SHALL be able to select "Multi-Year" as a frequency option.
2. WHEN "Multi-Year" frequency is selected, THEN a `FrequencyInterval` field SHALL become required (integer, e.g., 2, 3, 4, 5 years).
3. WHEN a business user imports a Multi-Year template, THEN the system SHALL generate one filing record for the selected import year (same as Annual behaviour during import).
4. THE frequency label for Multi-Year templates SHALL display as "Every X years" (e.g., "Every 4 years") in the UI.
5. WHEN displayed in the Import view, Multi-Year templates SHALL show "Multi-Year — 1 record" with the interval noted.
6. THE `FrequencyInterval` column SHALL be of type INT NULL on `[compliance].[ApplicationType]`.
7. THE existing frequency CHECK constraint SHALL be updated to include 'Multi-Year' as a valid value.
