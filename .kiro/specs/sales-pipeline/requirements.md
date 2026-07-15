# Requirements Document

## Introduction

The Sales Pipeline module (Phase 1) introduces a native commercial pipeline to the Portal platform under the `[sales]` schema. It replaces the need for a third-party CRM by providing unified lead tracking, contact management, meeting scheduling, suggested response emails, and proposal generation — all integrated with the platform's Identity, Permissions, and Audit infrastructure.

Phase 1 delivers a fully functional lead-to-customer pipeline with manual operations and smart suggestions (system prepares a response email from a template; user reviews and clicks "Send"). The module is multi-tenant (all entities scoped by BusinessId) and industry-agnostic.

The pipeline lifecycle flows: Lead → Contact → Follow-Up → Meeting → Proposal → Won/Lost → Customer. Entities follow the platform's established cancellation pattern (IsCancelled + CancellationTimestamp + CancellationDescription for customer-initiated cancellations, IsActive for internal soft-delete).

## Glossary

- **Sales_Controller**: The ASP.NET Core MVC controller responsible for pipeline views, contact management, lead request actions, meeting scheduling, and response template configuration
- **Contact_Service**: The service responsible for Contact CRUD operations, deduplication checks, and Contact-to-Customer conversion logic
- **LeadRequest_Service**: The service responsible for LeadRequest CRUD, pipeline stage transitions, assignment, and linking to proposals and invoices
- **Meeting_Service**: The service responsible for Meeting CRUD, ICS file generation, MeetingProductRequest tracking, and MeetingOpportunity recording
- **Response_Service**: The service responsible for preparing suggested lead response emails from templates, rendering placeholders, and recording sent responses
- **Product_Service**: The service responsible for sales Product catalogue CRUD (the products a business sells, used for pipeline tracking)
- **Contact**: A person who has expressed interest or been recorded manually in the sales pipeline, unique by email or phone within a business. Sales-only — not a general-purpose contact
- **LeadRequest**: A specific enquiry or interest expression from a Contact about a Product. One Contact can have many LeadRequests (interest history)
- **LeadStatusType**: Pipeline stages that track lead progression: New, Contacted, Follow-Up, Meeting Scheduled, Proposal Sent, Won, Lost, Inactive
- **LeadResponse**: A response action taken on a LeadRequest — in Phase 1, always user-initiated (suggested by system, sent by user)
- **LeadResponseTemplate**: A configurable email template per product with placeholders, used to prepare suggested response emails
- **Meeting**: A scheduled meeting related to a lead, with type (Online, On-Site, Phone Call, Video Call), duration, and outcome recording
- **MeetingProductRequest**: Specific products discussed or requested during a meeting (discovered needs beyond the original lead product)
- **MeetingOpportunity**: Broader business opportunities discovered during a meeting — not tied to a specific product
- **ICS_File**: An iCalendar (.ics) file generated for a meeting that can be downloaded and imported into any calendar application
- **Pipeline_View**: A visual representation of leads organised by pipeline stage, displayed as a Kanban board or filterable table
- **Terminal_Stage**: A pipeline stage (Won, Lost, Inactive) that represents an end state for the lead
- **Page_Size**: The number of records displayed per page in list views, fixed at 15

## Requirements

### Requirement 1: Sales Schema and Contact Data Model

**User Story:** As a platform operator, I want a dedicated sales schema with a Contact table supporting deduplication, so that the system has a normalised foundation for sales pipeline data.

#### Acceptance Criteria

1. THE Portal_Database SHALL create a `[sales]` schema if it does not already exist, prior to creating any tables within that schema
2. THE Portal_Database SHALL contain a `[sales].[Contact]` table with columns: Id (PK, int identity), BusinessId (FK to [dbo].[Business], required), FirstName (nvarchar(100), required), LastName (nvarchar(100), nullable), Email (nvarchar(320), nullable), PhoneNumber (nvarchar(30), nullable), CompanyName (nvarchar(200), nullable), JobTitle (nvarchar(100), nullable), Country (nvarchar(100), nullable), Notes (nvarchar(max), nullable), IsActive (bit, default 1), CreatedAtUtc (datetime, default GETUTCDATE())
3. THE Portal_Database SHALL enforce a partial unique index on (BusinessId, Email) WHERE Email IS NOT NULL in the `[sales].[Contact]` table
4. THE Portal_Database SHALL enforce a partial unique index on (BusinessId, PhoneNumber) WHERE PhoneNumber IS NOT NULL in the `[sales].[Contact]` table
5. IF a create contact request specifies an Email that already exists for the same BusinessId, THEN THE Contact_Service SHALL return an error indicating that a contact with this email already exists and display the existing contact's name
6. IF a create contact request specifies a PhoneNumber that already exists for the same BusinessId, THEN THE Contact_Service SHALL return an error indicating that a contact with this phone number already exists and display the existing contact's name
7. IF a create contact request specifies neither Email nor PhoneNumber, THEN THE Contact_Service SHALL return a validation error indicating that at least one of Email or PhoneNumber is required
8. THE Contact_Service SHALL filter all contact queries by the authenticated user's BusinessId resolved from the current authentication claims

### Requirement 2: Product Catalogue for Sales Pipeline

**User Story:** As a business operator, I want to maintain a catalogue of products I sell, so that I can track which products leads are interested in and associate products with pipeline activity.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[Product]` table with columns: Id (PK, int identity), BusinessId (FK to [dbo].[Business], required), Name (nvarchar(200), required), Description (nvarchar(500), nullable), IsActive (bit, default 1), CreatedAtUtc (datetime, default GETUTCDATE())
2. WHEN a create product request is submitted with a valid Name, THE Product_Service SHALL insert a new Product record with IsActive set to true and CreatedAtUtc set to the current UTC time
3. WHEN an edit product request is submitted with a valid Name, THE Product_Service SHALL update the Product record with the new values
4. WHEN a deactivate product request is submitted, THE Product_Service SHALL set IsActive to false on the Product record
5. THE Product_Service SHALL filter all product queries by the authenticated user's BusinessId
6. THE Sales_Controller SHALL display a searchable, paginated product list with columns: Name, Description, IsActive status, and CreatedAtUtc
7. THE Sales_Controller SHALL paginate the product list with a Page_Size of 15 records per page

### Requirement 3: Lead Request and Pipeline Stage Tracking

**User Story:** As a business operator, I want to record enquiries from contacts and track them through pipeline stages, so that I have full visibility over my sales funnel and can manage leads through to conversion.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[LeadRequest]` table with columns: Id (PK, int identity), BusinessId (FK to [dbo].[Business], required), ContactId (FK to [sales].[Contact], required), ProductId (FK to [sales].[Product], nullable), LeadSourceTypeId (FK to [sales].[LeadSourceType], required), LeadSourceReferenceTypeId (FK to [sales].[LeadSourceReferenceType], nullable), LeadStatusTypeId (FK to [sales].[LeadStatusType], required, default 1), SourceUrl (nvarchar(500), nullable), RequestText (nvarchar(max), nullable), AssignedToUserId (nvarchar(450), nullable), IsCancelled (bit, default 0), CancellationTimestamp (datetime, nullable), CancellationDescription (nvarchar(500), nullable), IsActive (bit, default 1), CreatedAtUtc (datetime, default GETUTCDATE())
2. THE Portal_Database SHALL contain a `[sales].[LeadSourceType]` lookup table seeded with values: Website, Referral, Event, Cold Call, Partner, Social Media, Other
3. THE Portal_Database SHALL contain a `[sales].[LeadSourceReferenceType]` lookup table seeded with values: Facebook, Instagram, LinkedIn, Google Ads, Twitter/X, Email Campaign, Direct, Other
4. THE Portal_Database SHALL contain a `[sales].[LeadStatusType]` lookup table seeded with values: New (DisplayOrder 1, IsTerminal 0), Contacted (DisplayOrder 2, IsTerminal 0), Follow-Up (DisplayOrder 3, IsTerminal 0), Meeting Scheduled (DisplayOrder 4, IsTerminal 0), Proposal Sent (DisplayOrder 5, IsTerminal 0), Won (DisplayOrder 6, IsTerminal 1), Lost (DisplayOrder 7, IsTerminal 1), Inactive (DisplayOrder 8, IsTerminal 1)
5. WHEN a create lead request is submitted with a valid ContactId, LeadSourceTypeId, and optional ProductId, THE LeadRequest_Service SHALL insert a new LeadRequest record with LeadStatusTypeId set to 1 (New), IsCancelled set to false, IsActive set to true, and CreatedAtUtc set to the current UTC time
6. WHEN a pipeline stage change request is submitted, THE LeadRequest_Service SHALL update the LeadStatusTypeId to the requested stage value
7. WHEN an assign lead request is submitted with a valid UserId, THE LeadRequest_Service SHALL update the AssignedToUserId on the LeadRequest record
8. WHEN a cancel lead request is submitted with a CancellationDescription, THE LeadRequest_Service SHALL set IsCancelled to true, CancellationTimestamp to the current UTC time, and CancellationDescription to the provided value
9. WHEN a deactivate lead request is submitted, THE LeadRequest_Service SHALL set IsActive to false on the LeadRequest record
10. THE LeadRequest_Service SHALL filter all lead request queries by the authenticated user's BusinessId and WHERE IsActive equals true
11. THE LeadRequest_Service SHALL allow manual pipeline stage transitions between any non-terminal stage and any other stage, including terminal stages

### Requirement 4: Pipeline View

**User Story:** As a business operator, I want a visual pipeline view showing leads organised by stage, so that I can quickly assess the state of my sales funnel and prioritise actions.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose a Pipeline action that renders a visual pipeline view showing all active LeadRequests grouped by their LeadStatusType
2. THE Pipeline_View SHALL display leads in a Kanban-style board layout with one column per pipeline stage, ordered by DisplayOrder from LeadStatusType
3. THE Pipeline_View SHALL display each lead card with: Contact name (FirstName + LastName), Product name (or "General Enquiry" when ProductId is null), assigned user name (or "Unassigned"), and CreatedAtUtc formatted as a relative date
4. THE Pipeline_View SHALL display the count of leads in each stage column header
5. WHEN the user applies a filter by AssignedToUserId, THE Pipeline_View SHALL display only leads assigned to the selected user
6. WHEN the user applies a filter by ProductId, THE Pipeline_View SHALL display only leads associated with the selected product
7. THE Pipeline_View SHALL provide a table view alternative that displays leads in a filterable, paginated table with columns: Contact Name, Product, Stage, Source, Assigned To, Created Date, with a Page_Size of 15
8. WHEN the user clicks a lead card or table row, THE Sales_Controller SHALL navigate to the lead detail view showing full lead information, response history, meetings, and linked proposals

### Requirement 5: Lead Response (Suggested Email)

**User Story:** As a business operator, I want the system to prepare a response email from a template when I respond to a lead, so that I can quickly send professional responses while maintaining personal oversight.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[LeadResponse]` table with columns: Id (PK, int identity), LeadRequestId (FK to [sales].[LeadRequest], required), LeadResponseTypeId (FK to [sales].[LeadResponseType], required), LeadResponseTemplateId (FK to [sales].[LeadResponseTemplate], nullable), RespondedByUserId (nvarchar(450), nullable), ResponseText (nvarchar(max), nullable), IsAutomated (bit, default 0), SentAtUtc (datetime, required), CreatedAtUtc (datetime, default GETUTCDATE())
2. THE Portal_Database SHALL contain a `[sales].[LeadResponseType]` lookup table seeded with values: Email, Telephone, SMS, WhatsApp, In Person
3. WHEN the user initiates a response action on a lead, THE Response_Service SHALL search for an active LeadResponseTemplate matching the lead's ProductId (or a template with null ProductId as fallback) for the same BusinessId
4. WHEN a matching template is found, THE Response_Service SHALL render the template by replacing placeholders ({ContactFirstName}, {ProductName}, {BusinessName}) with actual values and present the prepared email for user review
5. WHEN no matching template is found, THE Response_Service SHALL present a blank response form for the user to compose manually
6. WHEN the user reviews and confirms sending the response, THE Response_Service SHALL insert a LeadResponse record with IsAutomated set to false, SentAtUtc set to the current UTC time, and RespondedByUserId set to the authenticated user's identifier
7. WHEN a lead response of type Email is sent successfully, THE LeadRequest_Service SHALL update the LeadStatusTypeId to 2 (Contacted) if the current status is 1 (New)
8. THE Response_Service SHALL NOT send any response automatically without explicit user confirmation (Phase 1 is suggested-only, not automated)

### Requirement 6: Lead Response Templates

**User Story:** As a business operator, I want to configure email templates per product with placeholders, so that I have consistent professional response messaging ready for each product enquiry.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[LeadResponseTemplate]` table with columns: Id (PK, int identity), BusinessId (FK to [dbo].[Business], required), ProductId (FK to [sales].[Product], nullable), LeadResponseTypeId (FK to [sales].[LeadResponseType], required), Name (nvarchar(200), required), Subject (nvarchar(300), nullable), BodyTemplate (nvarchar(max), required), ResponseTimeInHours (int, required), IsActive (bit, default 1), CreatedAtUtc (datetime, default GETUTCDATE())
2. WHEN a create template request is submitted with a valid Name, BodyTemplate, and LeadResponseTypeId, THE Response_Service SHALL insert a new LeadResponseTemplate record with IsActive set to true
3. WHEN an edit template request is submitted, THE Response_Service SHALL update the template record with the new values
4. WHEN a deactivate template request is submitted, THE Response_Service SHALL set IsActive to false on the template record
5. THE Response_Service SHALL support the following placeholders in BodyTemplate: {ContactFirstName}, {ProductName}, {BusinessName}, {MeetingBookingLink}
6. WHEN a template is rendered and a placeholder value is not available (e.g., ProductName when ProductId is null), THE Response_Service SHALL replace the placeholder with an empty string
7. THE Sales_Controller SHALL display a template management view with columns: Name, Product (or "All Products"), Response Type, ResponseTimeInHours, IsActive status
8. THE Response_Service SHALL filter all template queries by the authenticated user's BusinessId

### Requirement 7: Meeting Management

**User Story:** As a business operator, I want to schedule meetings with contacts, record outcomes, and download ICS files, so that I can manage my sales appointments and track meeting results within the pipeline.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[Meeting]` table with columns: Id (PK, int identity), BusinessId (FK to [dbo].[Business], required), LeadRequestId (FK to [sales].[LeadRequest], nullable), ContactId (FK to [sales].[Contact], required), MeetingTypeId (FK to [sales].[MeetingType], required), Subject (nvarchar(300), required), ScheduledAtUtc (datetime, required), DurationMinutes (int, required, default 60), Location (nvarchar(300), nullable), Notes (nvarchar(max), nullable), Outcome (nvarchar(max), nullable), IsCancelled (bit, default 0), CancellationTimestamp (datetime, nullable), CancellationDescription (nvarchar(500), nullable), IsActive (bit, default 1), CreatedByUserId (nvarchar(450), required), CreatedAtUtc (datetime, default GETUTCDATE())
2. THE Portal_Database SHALL contain a `[sales].[MeetingType]` lookup table seeded with values: Online, On-Site, Phone Call, Video Call
3. WHEN a create meeting request is submitted with a valid ContactId, MeetingTypeId, Subject, and ScheduledAtUtc, THE Meeting_Service SHALL insert a new Meeting record with IsCancelled set to false, IsActive set to true, and CreatedByUserId set to the authenticated user's identifier
4. WHEN a meeting is created and linked to a LeadRequest (LeadRequestId is not null), THE LeadRequest_Service SHALL update the LeadStatusTypeId to 4 (Meeting Scheduled) if the current status is 1 (New), 2 (Contacted), or 3 (Follow-Up)
5. WHEN an update meeting request is submitted, THE Meeting_Service SHALL update the meeting record with the new values including Outcome
6. WHEN a cancel meeting request is submitted with a CancellationDescription, THE Meeting_Service SHALL set IsCancelled to true, CancellationTimestamp to the current UTC time, and CancellationDescription to the provided value
7. WHEN an ICS download request is submitted for a meeting, THE Meeting_Service SHALL generate a valid .ics file containing: VEVENT with DTSTART (ScheduledAtUtc), DTEND (ScheduledAtUtc + DurationMinutes), SUMMARY (Subject), LOCATION (Location or empty), and DESCRIPTION (Notes or empty)
8. THE Meeting_Service SHALL return the ICS file with content type "text/calendar" and filename formatted as "meeting-{Id}.ics"
9. THE Sales_Controller SHALL display a meeting list for a lead showing: Subject, Meeting Type, Scheduled Date, Duration, Outcome summary (truncated to 100 characters), and IsCancelled status
10. THE Meeting_Service SHALL filter all meeting queries by the authenticated user's BusinessId

### Requirement 8: Meeting Product Requests and Opportunities

**User Story:** As a business operator, I want to record products discussed during meetings and broader business opportunities discovered, so that I can track discovered needs and potential revenue beyond the original lead.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[MeetingProductRequest]` table with columns: Id (PK, int identity), MeetingId (FK to [sales].[Meeting], required), ProductId (FK to [sales].[Product], required), RequestText (nvarchar(max), nullable), IsActive (bit, default 1), IsCancelled (bit, default 0), CreatedAtUtc (datetime, default GETUTCDATE())
2. THE Portal_Database SHALL contain a `[sales].[MeetingOpportunity]` table with columns: Id (PK, int identity), MeetingId (FK to [sales].[Meeting], required), Title (nvarchar(300), required), Description (nvarchar(max), nullable), EstimatedValue (decimal(18,2), nullable), IsActive (bit, default 1), CreatedAtUtc (datetime, default GETUTCDATE())
3. WHEN a meeting product request is created with a valid MeetingId and ProductId, THE Meeting_Service SHALL insert a new MeetingProductRequest record with IsActive set to true and IsCancelled set to false
4. WHEN a meeting opportunity is created with a valid MeetingId and Title, THE Meeting_Service SHALL insert a new MeetingOpportunity record with IsActive set to true
5. THE Sales_Controller SHALL display meeting product requests and opportunities on the meeting detail view, grouped by meeting
6. THE Meeting_Service SHALL filter all MeetingProductRequest and MeetingOpportunity queries by joining through Meeting.BusinessId

### Requirement 9: Proposal and Invoice Linking to Leads

**User Story:** As a business operator, I want to link existing proposals and invoices to leads, so that I can track which commercial documents were generated from pipeline activity and measure conversion.

#### Acceptance Criteria

1. THE Portal_Database SHALL add a nullable LeadRequestId column (FK to [sales].[LeadRequest]) to the existing `[dbo].[Quotation]` table
2. THE Portal_Database SHALL add a nullable LeadRequestId column (FK to [sales].[LeadRequest]) to the existing `[dbo].[Invoice]` table
3. WHEN a proposal is created from a lead detail view, THE LeadRequest_Service SHALL set the LeadRequestId on the new Quotation record to the originating LeadRequest's Id
4. WHEN a proposal is linked to a lead and the current LeadStatusTypeId is 1 (New), 2 (Contacted), 3 (Follow-Up), or 4 (Meeting Scheduled), THE LeadRequest_Service SHALL update the LeadStatusTypeId to 5 (Proposal Sent)
5. WHEN an invoice is created from a lead detail view, THE LeadRequest_Service SHALL set the LeadRequestId on the new Invoice record to the originating LeadRequest's Id
6. THE Sales_Controller SHALL display linked proposals and invoices on the lead detail view with: Document reference number, Date, Total amount, and Status
7. THE Sales_Controller SHALL provide a "Create Proposal" action on the lead detail view that navigates to the quotation creation flow with LeadRequestId pre-populated
8. THE Sales_Controller SHALL provide a "Create Invoice" action on the lead detail view that navigates to the invoice creation flow with LeadRequestId pre-populated

### Requirement 10: Contact to Customer Conversion

**User Story:** As a business operator, I want to mark a lead as Won and convert the contact to a customer, so that I can seamlessly transition a prospect into the billing and invoicing workflow.

#### Acceptance Criteria

1. THE Portal_Database SHALL add a nullable ContactId column (FK to [sales].[Contact]) to the existing `[dbo].[Customer]` table
2. WHEN the user triggers a "Mark as Won" action on a lead, THE LeadRequest_Service SHALL update the LeadStatusTypeId to 6 (Won)
3. WHEN a lead is marked as Won, THE Contact_Service SHALL check if a Customer record already exists for the same BusinessId with a matching Email or matching name (FirstName + LastName)
4. WHEN no matching Customer exists, THE Contact_Service SHALL create a new Customer record with: FirstName, LastName, Email, PhoneNumber, and CompanyName mapped from the Contact, and ContactId set to the Contact's Id
5. WHEN a matching Customer already exists, THE Contact_Service SHALL link the Customer to the Contact by setting ContactId on the Customer record (if not already set) and display a message indicating the customer already exists
6. WHEN a lead is marked as Won and the linked Invoice (via LeadRequestId) has a paid financial status, THE LeadRequest_Service SHALL support this as an alternative trigger for the Won transition (automated Won on invoice payment)
7. THE Sales_Controller SHALL present the "Mark as Won" action as a SweetAlert2 confirmation dialog showing the contact details that will be converted to a customer record

### Requirement 11: Lead Assignment

**User Story:** As a business operator, I want to assign leads to specific team members, so that I can distribute workload and establish clear ownership of each sales opportunity.

#### Acceptance Criteria

1. WHEN an assign lead request is submitted with a valid UserId that belongs to the same BusinessId, THE LeadRequest_Service SHALL update the AssignedToUserId on the LeadRequest record
2. WHEN an unassign lead request is submitted, THE LeadRequest_Service SHALL set AssignedToUserId to null on the LeadRequest record
3. THE Sales_Controller SHALL provide an assignment dropdown on the lead detail view populated with active users belonging to the same BusinessId
4. THE Pipeline_View SHALL display the assigned user's name on each lead card (or "Unassigned" when AssignedToUserId is null)
5. THE Pipeline_View SHALL support filtering by AssignedToUserId to show only leads owned by a specific user

### Requirement 12: Contact Management View

**User Story:** As a business operator, I want a dedicated contacts management page with search and full CRUD, so that I can maintain my prospect database and view each contact's interest history.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose a Contacts action that renders a searchable, paginated contacts list with columns: Name (FirstName + LastName), Email, PhoneNumber, CompanyName, Lead Count (number of associated LeadRequests), IsActive status, and CreatedAtUtc
2. THE Sales_Controller SHALL paginate the contacts list with a Page_Size of 15 records per page
3. WHEN a search term is entered, THE Sales_Controller SHALL filter contacts whose FirstName, LastName, Email, PhoneNumber, or CompanyName contains the search term (case-insensitive partial match)
4. THE Sales_Controller SHALL provide a Create Contact form with fields: FirstName (required), LastName, Email, PhoneNumber, CompanyName, JobTitle, Country, and Notes
5. THE Sales_Controller SHALL provide an Edit action for each contact that opens the form pre-populated with the contact's current values
6. THE Sales_Controller SHALL provide a Deactivate action for each active contact that triggers a SweetAlert2 confirmation dialog before setting IsActive to false
7. WHEN the user views a contact detail, THE Sales_Controller SHALL display the contact's complete interest history: all associated LeadRequests with their current pipeline stage, product, and creation date, ordered by CreatedAtUtc descending

### Requirement 13: Lead Detail View

**User Story:** As a business operator, I want a comprehensive lead detail page showing all related activity, so that I have a single view of the lead's full context including responses, meetings, proposals, and status history.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose a LeadDetail action that renders a comprehensive view for a single LeadRequest including: Contact information, Product name, Lead Source, Current pipeline stage, Assigned user, RequestText, and CreatedAtUtc
2. THE Sales_Controller SHALL display a response history section showing all LeadResponse records for the lead, ordered by SentAtUtc descending, with: Response Type, ResponseText (truncated to 200 characters), Sent date, and Responded By user name
3. THE Sales_Controller SHALL display a meetings section showing all Meetings linked to the lead, ordered by ScheduledAtUtc descending, with: Subject, Meeting Type, Scheduled Date, Duration, and Outcome summary
4. THE Sales_Controller SHALL display a linked documents section showing: all Quotations where LeadRequestId matches, and all Invoices where LeadRequestId matches, with document reference, date, total, and status
5. THE Sales_Controller SHALL display pipeline stage change controls allowing the user to move the lead to any stage via a dropdown or button group
6. THE Sales_Controller SHALL display a "Respond" action button that initiates the suggested response flow (Requirement 5)
7. THE Sales_Controller SHALL display a "Schedule Meeting" action button that opens the meeting creation form pre-populated with the lead's ContactId and LeadRequestId
8. IF the lead's LeadStatusTypeId is a Terminal_Stage (Won, Lost, Inactive), THEN THE Sales_Controller SHALL visually indicate the terminal state and still allow stage changes (reopen capability)

### Requirement 14: Tenant Isolation

**User Story:** As a platform operator, I want all sales pipeline queries scoped to the authenticated business tenant, so that businesses cannot view or modify each other's sales data.

#### Acceptance Criteria

1. THE Contact_Service SHALL filter all contact queries by the authenticated user's BusinessId resolved from the current authentication claims
2. THE LeadRequest_Service SHALL filter all lead request queries by the authenticated user's BusinessId
3. THE Meeting_Service SHALL filter all meeting queries by the authenticated user's BusinessId
4. THE Response_Service SHALL filter all lead response and template queries by the authenticated user's BusinessId (directly or via join through LeadRequest or LeadResponseTemplate)
5. THE Product_Service SHALL filter all sales product queries by the authenticated user's BusinessId
6. IF the authenticated user's BusinessId cannot be resolved from the authentication claims, THEN all sales services SHALL return zero results for all queries
7. IF a request references a Contact, LeadRequest, Meeting, or Product that does not belong to the authenticated user's BusinessId, THEN the corresponding service SHALL treat the resource as not found and return no data

### Requirement 15: Sales Module Navigation and Sidebar

**User Story:** As a business operator, I want the sales module accessible from the platform sidebar with clear sub-navigation, so that I can navigate between pipeline, contacts, products, meetings, and templates efficiently.

#### Acceptance Criteria

1. THE Sales_Controller SHALL register a "Sales" top-level sidebar menu item with an appropriate icon
2. THE Sales_Controller SHALL provide sub-navigation items: Pipeline (default landing), Contacts, Products, Meetings, and Templates
3. WHEN the user navigates to the Sales module, THE Sales_Controller SHALL render the Pipeline view as the default landing page
4. THE Sales_Controller SHALL apply the existing platform permission and subscription tier checks before rendering sales module pages
5. WHILE the user's subscription plan does not include the Sales module feature, THE Sales_Controller SHALL display a subscription upgrade prompt instead of the module content
