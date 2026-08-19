# Requirements Document

## Introduction

The Sales Pipeline Enhancements (Phase 2) extend the existing Sales module with four capabilities that improve lead visibility, communication quality, decision-making, and activity tracking. These enhancements build on the existing `[sales]` schema and follow the established patterns in SalesController, LeadRequestService, and the pipeline Kanban board.

The four enhancements are:
1. **Lead Priority & Days Since Last Activity** — Add a priority indicator (Hot/Warm/Cold) to leads and display elapsed days since the last recorded activity on each pipeline card, enabling salespersons to prioritise follow-ups at a glance.
2. **Additional Template Placeholders** — Extend the response template rendering engine with nine new placeholders to support richer, more personalised communication.
3. **Operational Metrics Dashboard** — Introduce a dedicated /Sales/Insights page computing key performance indicators: new leads, response SLA, conversion rates, revenue breakdowns, and average sales cycle duration.
4. **Unified Timeline View** — Replace the separated sections on the Lead Detail page with a single chronological timeline showing all lead events in order.

## Glossary

- **Sales_Controller**: The ASP.NET Core MVC controller responsible for pipeline views, contact management, lead request actions, meeting scheduling, response template configuration, and the new Insights page
- **LeadRequest_Service**: The service responsible for LeadRequest CRUD, pipeline stage transitions, assignment, priority management, and activity date tracking
- **Response_Service**: The service responsible for preparing suggested lead response emails from templates, rendering placeholders (including new placeholders), and recording sent responses
- **Insights_Service**: The service responsible for computing and returning operational sales metrics for the Insights dashboard
- **Timeline_Service**: The service responsible for aggregating all lead-related events into a single chronological timeline
- **LeadRequest**: A specific enquiry or interest expression from a Contact about a Product, now extended with a LeadPriorityTypeId field
- **LeadPriorityType**: A lookup table defining lead priority levels: Hot, Warm, Cold
- **Pipeline_View**: The Kanban board view of active leads grouped by pipeline stage, now enhanced with priority indicators and days-since-last-activity display
- **LeadResponseTemplate**: A configurable email template per product with placeholders, now supporting an extended set of placeholder tokens
- **Placeholder**: A token in the form `{{TokenName}}` embedded in a response template body, replaced with real values at render time
- **Insights_Page**: A dedicated view at /Sales/Insights displaying computed sales metrics and KPIs
- **Timeline_View**: A unified chronological list of all events associated with a single lead, displayed on the Lead Detail page
- **Timeline_Event**: A single entry in the timeline representing an action or state change (creation, email, call, meeting, proposal, stage change, assignment change, customer conversion)
- **Days_Since_Last_Activity**: The number of calendar days between the current UTC date and the most recent activity timestamp on a lead (response sent, meeting scheduled, stage changed, or assignment changed)
- **Response_SLA**: The percentage of leads whose first response was sent within the ResponseTimeInHours window measured from LeadRequest.CreatedAtUtc to the earliest LeadResponse.SentAtUtc for that lead (or within 24 hours if no template exists)
- **Sales_Cycle_Duration**: The number of calendar days between a lead's CreatedAtUtc and its ClosedAtUtc (the timestamp when it reached a terminal stage Won or Lost)
- **ClosedAtUtc**: A nullable datetime column on LeadRequest, set automatically when the lead reaches a terminal stage (Won or Lost), used for efficient metrics computation
- **Page_Size**: The number of records displayed per page in list views, fixed at 15

## Requirements

### Requirement 1: Lead Priority Type Data Model

**User Story:** As a business operator, I want to assign a priority level (Hot, Warm, or Cold) to each lead, so that I can quickly identify which leads deserve immediate attention.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[sales].[LeadPriorityType]` lookup table with columns: Id (PK, int identity), Name (nvarchar(50), required), DisplayOrder (int, required), Colour (nvarchar(10), required), CreatedAtUtc (datetime, default GETUTCDATE())
2. THE Portal_Database SHALL seed the `[sales].[LeadPriorityType]` table with values: Hot (DisplayOrder 1, Colour '#E53E3E'), Warm (DisplayOrder 2, Colour '#DD6B20'), Cold (DisplayOrder 3, Colour '#3182CE')
3. THE Portal_Database SHALL add a nullable LeadPriorityTypeId column (FK to [sales].[LeadPriorityType]) to the existing `[sales].[LeadRequest]` table
4. THE Portal_Database SHALL add a nullable ClosedAtUtc column (datetime) to the existing `[sales].[LeadRequest]` table, representing the UTC timestamp when the lead reached a terminal stage (Won or Lost)
5. WHEN a create lead request is submitted without a LeadPriorityTypeId, THE LeadRequest_Service SHALL insert the LeadRequest with LeadPriorityTypeId set to null (no priority assigned)
6. WHEN a set priority request is submitted with a valid LeadPriorityTypeId, THE LeadRequest_Service SHALL update the LeadPriorityTypeId on the LeadRequest record
7. WHEN a clear priority request is submitted, THE LeadRequest_Service SHALL set the LeadPriorityTypeId to null on the LeadRequest record
8. WHEN a lead's LeadStatusTypeId is changed to a terminal stage (Won or Lost), THE LeadRequest_Service SHALL set ClosedAtUtc to the current UTC time if it is currently null
9. WHEN a lead's LeadStatusTypeId is changed from a terminal stage back to a non-terminal stage (reopen), THE LeadRequest_Service SHALL set ClosedAtUtc back to null

### Requirement 2: Lead Priority Display on Pipeline Cards

**User Story:** As a salesperson, I want to see the priority level (Hot/Warm/Cold) on each pipeline Kanban card, so that I can visually identify high-priority leads without opening the detail page.

#### Acceptance Criteria

1. THE Pipeline_View SHALL display a coloured priority badge on each lead card when a LeadPriorityTypeId is assigned, showing the priority name in the corresponding colour from LeadPriorityType
2. WHEN a lead has no LeadPriorityTypeId assigned (null), THE Pipeline_View SHALL display no priority badge on that lead card
3. THE LeadRequest_Service SHALL include LeadPriorityTypeId, PriorityName, and PriorityColour fields in the LeadCardDto returned for the pipeline view
4. THE Sales_Controller SHALL provide a priority assignment action on the Lead Detail page as a dropdown listing all active LeadPriorityType values plus a "Clear Priority" option

### Requirement 3: Days Since Last Activity on Pipeline Cards

**User Story:** As a salesperson, I want to see how many days have passed since the last activity on each lead directly on the pipeline card, so that I can identify stale leads that need attention.

#### Acceptance Criteria

1. THE LeadRequest_Service SHALL compute the Days_Since_Last_Activity for each lead by determining the most recent timestamp among: the last LeadResponse SentAtUtc, the last Meeting ScheduledAtUtc (for meetings linked to the lead), the last ActivityFeed entry CreatedAtUtc (for the lead), and the LeadRequest CreatedAtUtc as a fallback when no other activity exists
2. THE LeadRequest_Service SHALL include a DaysSinceLastActivity integer field in the LeadCardDto returned for the pipeline view
3. THE Pipeline_View SHALL display the DaysSinceLastActivity value on each lead card with the text format "{N}d ago" (e.g., "3d ago")
4. WHEN the DaysSinceLastActivity value is zero, THE Pipeline_View SHALL display "Today" instead of "0d ago"
5. WHEN the DaysSinceLastActivity value exceeds 7, THE Pipeline_View SHALL display the days-since indicator in a warning colour (#C8912E) to highlight stale leads
6. WHEN the DaysSinceLastActivity value exceeds 14, THE Pipeline_View SHALL display the days-since indicator in a danger colour (#C24A4A) to highlight critically stale leads

### Requirement 4: Additional Response Template Placeholders

**User Story:** As a business operator, I want additional placeholders available in my response templates, so that I can create richer personalised emails that include salesperson details, meeting information, and company data without manual editing.

#### Acceptance Criteria

1. THE Response_Service SHALL support the following additional placeholders in LeadResponseTemplate BodyTemplate rendering: {{AssignedSalesperson}}, {{MeetingDate}}, {{MeetingLink}}, {{ProposalLink}}, {{Company}}, {{Phone}}, {{BusinessWebsite}}, {{NextStage}}, {{SupportEmail}}
2. WHEN rendering the {{AssignedSalesperson}} placeholder, THE Response_Service SHALL replace it with the display name of the team member assigned to the lead (via TeamMemberId), or an empty string if no team member is assigned
3. WHEN rendering the {{MeetingDate}} placeholder, THE Response_Service SHALL replace it with the ScheduledAtUtc of the most recent upcoming Meeting linked to the lead, formatted as "dd MMM yyyy HH:mm", or an empty string if no upcoming meeting exists
4. WHEN rendering the {{MeetingLink}} placeholder, THE Response_Service SHALL replace it with the Location field of the most recent upcoming Meeting linked to the lead (when the Location contains a URL), or an empty string if unavailable
5. WHEN rendering the {{ProposalLink}} placeholder, THE Response_Service SHALL replace it with the shared proposal URL of the most recent Quotation linked to the lead (via LeadRequestId), or an empty string if no linked quotation exists
6. WHEN rendering the {{Company}} placeholder, THE Response_Service SHALL replace it with the CompanyName of the Contact associated with the lead, or an empty string if CompanyName is null
7. WHEN rendering the {{Phone}} placeholder, THE Response_Service SHALL replace it with the PhoneNumber of the Contact associated with the lead, or an empty string if PhoneNumber is null
8. WHEN rendering the {{BusinessWebsite}} placeholder, THE Response_Service SHALL replace it with the Website URL configured for the authenticated Business, or an empty string if no website is configured
9. WHEN rendering the {{NextStage}} placeholder, THE Response_Service SHALL replace it with the name of the next pipeline stage following the lead's current LeadStatusTypeId (ordered by DisplayOrder), or "Completed" if the current stage is a Terminal_Stage
10. WHEN rendering the {{SupportEmail}} placeholder, THE Response_Service SHALL replace it with the support email address configured for the authenticated Business, or an empty string if no support email is configured
11. WHEN a placeholder value is not available (null or empty), THE Response_Service SHALL replace the placeholder token with an empty string rather than leaving the raw token visible
12. THE Sales_Controller SHALL display the new placeholders in the template editor placeholder guide alongside the existing placeholders ({{ContactName}}, {{ProductName}}, {{BusinessName}}, {{ResponseTime}})

### Requirement 5: Operational Metrics — New Leads Count

**User Story:** As a business operator, I want to see how many new leads were created in a given period, so that I can measure inbound interest volume and identify trends.

#### Acceptance Criteria

1. THE Insights_Service SHALL compute the count of LeadRequest records created within the specified date range (filtered by CreatedAtUtc) for the authenticated Business
2. THE Insights_Page SHALL display the new leads count as a prominent metric card with the label "New Leads" and the numeric value
3. THE Insights_Page SHALL allow the user to select a date range filter (default: current month) that applies to all metrics on the page

### Requirement 6: Operational Metrics — Response SLA Performance

**User Story:** As a business operator, I want to see what percentage of leads received their first response within the SLA window, so that I can evaluate team responsiveness.

#### Acceptance Criteria

1. THE Insights_Service SHALL compute the Response_SLA percentage by: for each lead created within the specified date range that received at least one LeadResponse, calculating the elapsed hours between LeadRequest.CreatedAtUtc and the earliest LeadResponse.SentAtUtc for that lead, then dividing the count of leads where this elapsed time is within the configured ResponseTimeInHours (from the matching template, or 24 hours as default) by the total count of leads that received at least one response
2. THE Insights_Page SHALL display the Response SLA as a percentage metric card with the label "Response SLA" and a visual indicator (green above 80%, amber between 50-80%, red below 50%)
3. IF no leads received a response in the period, THEN THE Insights_Service SHALL return null for Response SLA and the Insights_Page SHALL display "No data" instead of a percentage

### Requirement 7: Operational Metrics — Conversion Rates

**User Story:** As a business operator, I want to see demo conversion rate, proposal conversion rate, and win rate, so that I can identify bottlenecks in my sales funnel.

#### Acceptance Criteria

1. THE Insights_Service SHALL compute Demo Conversion Rate as the percentage of leads whose stage changed to "Meeting Scheduled" (as recorded in the ActivityFeed with action 'stage_changed') within the specified date range, out of all leads that were at "New" or "Contacted" stage at any point during the date range
2. THE Insights_Service SHALL compute Proposal Conversion Rate as the percentage of leads whose stage changed to "Proposal Sent" (as recorded in the ActivityFeed with action 'stage_changed') within the specified date range, out of all leads that were at any non-terminal stage during the date range
3. THE Insights_Service SHALL compute Win Rate as the percentage of leads whose ClosedAtUtc falls within the specified date range and whose terminal stage is "Won", out of all leads whose ClosedAtUtc falls within the date range (Won + Lost only, excluding Inactive)
4. THE Insights_Page SHALL display each conversion rate as a separate metric card with the metric name, percentage value, and a colour-coded indicator (green above 30%, amber between 15-30%, red below 15%)
5. IF no leads reached a terminal stage in the period, THEN THE Insights_Service SHALL return null for Win Rate and the Insights_Page SHALL display "No data"

### Requirement 8: Operational Metrics — Revenue by Product and Source

**User Story:** As a business operator, I want to see revenue broken down by product and by lead source, so that I can identify which products and channels generate the most revenue.

#### Acceptance Criteria

1. THE Insights_Service SHALL compute Revenue by Product by summing the total value of linked Invoices (via LeadRequestId) grouped by the LeadRequest's ProductId (resolved to Product Name), for leads where ClosedAtUtc falls within the specified date range and the terminal stage is Won
2. THE Insights_Service SHALL compute Revenue by Source by summing the total value of linked Invoices (via LeadRequestId) grouped by the LeadRequest's LeadSourceTypeId (resolved to LeadSourceType Name), for leads where ClosedAtUtc falls within the specified date range and the terminal stage is Won
3. THE Insights_Page SHALL display Revenue by Product as a ranked list showing: Product Name, Total Revenue (formatted as currency), and percentage of total revenue
4. THE Insights_Page SHALL display Revenue by Source as a ranked list showing: Source Name, Total Revenue (formatted as currency), and percentage of total revenue
5. WHEN a lead has no linked Invoice, THE Insights_Service SHALL exclude that lead from revenue calculations
6. WHEN a lead has no ProductId (general enquiry), THE Insights_Service SHALL group its revenue under "General Enquiry" in the Revenue by Product breakdown

### Requirement 9: Operational Metrics — Average Sales Cycle Duration

**User Story:** As a business operator, I want to see the average number of days from lead creation to close, so that I can benchmark and improve my team's sales velocity.

#### Acceptance Criteria

1. THE Insights_Service SHALL compute Average Sales Cycle Duration as the mean number of calendar days between CreatedAtUtc and ClosedAtUtc, for all leads where ClosedAtUtc falls within the specified date range and the terminal stage is Won or Lost
2. THE Insights_Page SHALL display the Average Sales Cycle Duration as a metric card with the label "Avg. Sales Cycle" and the value formatted as "{N} days"
3. IF no leads have a ClosedAtUtc within the period (excluding Inactive), THEN THE Insights_Service SHALL return null for Average Sales Cycle Duration and the Insights_Page SHALL display "No data"
4. THE Insights_Service SHALL exclude leads marked as Inactive from the sales cycle computation (Inactive is an administrative close, not a true sales outcome)

### Requirement 10: Insights Page Navigation and Layout

**User Story:** As a business operator, I want the Insights page accessible from the Sales sidebar navigation, so that I can quickly access performance metrics alongside my pipeline.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose an Insights action at the route /Sales/Insights that renders the Operational Metrics dashboard page
2. THE Sales_Controller SHALL add an "Insights" sub-navigation item to the Sales module sidebar, positioned after "Pipeline" and before "Contacts"
3. THE Insights_Page SHALL display a date range filter at the top with preset options: This Week, This Month (default), Last Month, This Quarter, Last 6 Months, This Year, and a Custom range picker
4. THE Insights_Page SHALL display all metric cards in a responsive grid layout (3 columns on desktop, 2 on tablet, 1 on mobile)
5. THE Insights_Page SHALL reload metric data via an AJAX request when the date range filter changes, using BlockUI during the request

### Requirement 11: Unified Timeline Data Aggregation

**User Story:** As a business operator, I want all activities related to a lead aggregated into a single chronological timeline, so that I can see the complete history of interactions at a glance without switching between tabs.

#### Acceptance Criteria

1. THE Timeline_Service SHALL aggregate events from two distinct source types into a single ordered collection for a given LeadRequest: (A) **Direct entity sources** — lead creation (from LeadRequest.CreatedAtUtc), response events (from LeadResponse.SentAtUtc with ResponseText), and meetings (from Meeting.ScheduledAtUtc with Subject and Outcome); (B) **ActivityFeed sources** — stage changes (action 'stage_changed'), assignment changes (actions 'assigned' or 'unassigned'), proposals linked (action 'proposal_linked'), invoices linked (action 'invoice_linked'), customer conversion (action 'marked_as_won'), and task events (action 'task_created'). The Timeline_Service SHALL NOT duplicate events that exist in both ActivityFeed and direct entity tables — responses and meetings SHALL be sourced exclusively from their entity tables, while all other event types SHALL be sourced exclusively from the ActivityFeed.
2. THE Timeline_Service SHALL return timeline events ordered by their timestamp descending (most recent first)
3. THE Timeline_Service SHALL return each timeline event with: EventType (string identifier), Timestamp (DateTime), Title (human-readable summary), Description (optional detail text), ActorName (resolved from TeamMember.DisplayName via PerformedByTeamMemberId for ActivityFeed entries, or from the response/meeting's creator for entity sources, defaulting to "System" when no actor is available), and Colour (hex colour code associated with the event type)
4. THE Timeline_Service SHALL filter all timeline queries by the authenticated user's BusinessId via the LeadRequest's BusinessId

### Requirement 12: Unified Timeline Display on Lead Detail

**User Story:** As a business operator, I want the Lead Detail page to show a unified chronological timeline instead of separate response and meeting sections, so that I see all activity in context and in order.

#### Acceptance Criteria

1. THE Sales_Controller SHALL expose an AxGetLeadTimeline action that returns the aggregated timeline events for a given LeadRequest ID as a JSON array
2. THE Lead Detail page SHALL display a "Timeline" section that renders all timeline events in a vertical chronological list with: a coloured icon or dot indicating the event type, the event title, the actor name, the relative timestamp (e.g., "3 days ago"), and an expandable description when available
3. WHEN the timeline contains more than 20 events, THE Lead Detail page SHALL paginate the timeline showing 20 events per page with a "Load More" button to fetch the next page
4. THE Lead Detail page SHALL retain the existing Meetings section as a secondary detail panel (accessible via tab or scroll) for full meeting CRUD operations, while the timeline provides the read-only chronological view
5. WHEN a new activity is recorded on the lead (response sent, meeting scheduled, stage changed), THE Lead Detail page SHALL prepend the new event to the timeline without requiring a full page reload (optimistic UI update after successful AJAX call)
6. THE Timeline_View SHALL visually distinguish event types using the colour codes: lead creation (#8a9bab), response/email (#129867), meeting (#C8912E), stage change (#0D5EA6), assignment (#0D5EA6), proposal/invoice linked (#57B8E8), customer conversion (#129867)
