# Implementation Plan: Sales Pipeline Enhancements (Phase 2)

## Overview

Phase 2 extends the Sales Pipeline module with lead priority indicators, days-since-last-activity tracking, nine additional template placeholders, an operational metrics dashboard (Insights page), and a unified timeline view. Implementation proceeds bottom-up: database migrations → entities → repositories → services → controller → views → client-side JS.

## Tasks

- [x] 1. Database migrations and entity layer
  - [x] 1.1 Create database migration #140: CreateLeadPriorityTypeTable
    - Create SQL migration script with `USE [Guardian]` header
    - Create `[sales].[LeadPriorityType]` table with columns: Id (int identity PK), Name (nvarchar(50) NOT NULL), DisplayOrder (int NOT NULL), Colour (nvarchar(10) NOT NULL), CreatedAtUtc (datetime NOT NULL DEFAULT GETUTCDATE())
    - Seed three rows: Hot (DisplayOrder 1, Colour '#E53E3E'), Warm (DisplayOrder 2, Colour '#DD6B20'), Cold (DisplayOrder 3, Colour '#3182CE')
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Create database migration #141: AddLeadPriorityTypeIdToLeadRequest
    - Add nullable `LeadPriorityTypeId INT NULL` column to `[sales].[LeadRequest]`
    - Add FK constraint `FK_LeadRequest_LeadPriorityType` referencing `[sales].[LeadPriorityType](Id)`
    - _Requirements: 1.3_

  - [x] 1.3 Create database migration #142: AddClosedAtUtcToLeadRequest
    - Add nullable `ClosedAtUtc DATETIME NULL` column to `[sales].[LeadRequest]`
    - _Requirements: 1.4_

  - [x] 1.4 Create LeadPriorityType entity class
    - Create `Portal.Infrastructure.Entities.Sales.LeadPriorityType` with properties: Id, Name, DisplayOrder, Colour, CreatedAtUtc
    - Register entity in DbContext with `[sales].[LeadPriorityType]` table mapping
    - _Requirements: 1.1_

  - [x] 1.5 Extend LeadRequest entity with new properties
    - Add `LeadPriorityTypeId` (int?), `ClosedAtUtc` (DateTime?), and navigation property `LeadPriorityType?` to the existing LeadRequest entity
    - Update DbContext configuration for the new columns and FK relationship
    - _Requirements: 1.3, 1.4_

  - [x] 1.6 Create new DTOs for Phase 2
    - Create `LeadPriorityTypeDto` (Id, Name, Colour)
    - Create `InsightsMetricsDto` (NewLeadsCount, ResponseSlaPercentage, DemoConversionRate, ProposalConversionRate, WinRate, RevenueByProduct, RevenueBySource, AverageSalesCycleDays)
    - Create `RevenueBreakdownDto` (Name, TotalRevenue, Percentage)
    - Create `ConversionRatesDto` (DemoConversionRate, ProposalConversionRate, WinRate)
    - Create `TimelineEventDto` (EventType, Timestamp, Title, Description, ActorName, Colour)
    - Extend `LeadCardDto` with LeadPriorityTypeId, PriorityName, PriorityColour, DaysSinceLastActivity
    - Extend `LeadRequestDetailDto` with LeadPriorityTypeId, PriorityName, PriorityColour
    - _Requirements: 2.3, 5.1, 5.2, 7.4, 8.3, 8.4, 11.3, 12.2_

  - [x] 1.7 Extend TemplatePlaceholderValues class with 9 new properties
    - Add AssignedSalesperson, MeetingDate, MeetingLink, ProposalLink, Company, Phone, BusinessWebsite, NextStage, SupportEmail (all string, default empty)
    - _Requirements: 4.1_

- [x] 2. Repository layer
  - [x] 2.1 Create LeadPriorityTypeRepository
    - Extend `GenericStoredProcedureRepository<LeadPriorityType>`
    - Implement `GetAllAsync()` — query all rows from `[sales].[LeadPriorityType]` ordered by DisplayOrder
    - Use full table names in SQL (no aliases), catch `Exception ex`, rethrow
    - _Requirements: 1.1, 1.2, 2.4_

  - [x] 2.2 Extend LeadRequestRepository with priority and ClosedAtUtc methods
    - Implement `UpdatePriorityAsync(int id, int? leadPriorityTypeId, int businessId)` — UPDATE LeadPriorityTypeId WHERE Id = @Id AND BusinessId = @BusinessId
    - Implement `SetClosedAtUtcAsync(int id, DateTime? closedAtUtc, int businessId)` — UPDATE ClosedAtUtc WHERE Id = @Id AND BusinessId = @BusinessId
    - Implement `GetLastActivityDatesAsync(List<int> leadRequestIds, int businessId)` — batch query using VALUES/MAX pattern across LeadResponse, Meeting, ActivityFeed, and LeadRequest.CreatedAtUtc
    - Use full table names, parameterised queries, catch `Exception ex`
    - _Requirements: 1.6, 1.7, 1.8, 1.9, 3.1_

  - [x] 2.3 Extend LeadResponseRepository for timeline and SLA queries
    - Implement `GetByLeadRequestIdAsync(int leadRequestId)` — returns all responses for a lead, ordered by SentAtUtc desc
    - Implement `GetEarliestResponseDatesAsync(List<int> leadRequestIds, int businessId)` — for SLA computation, returns earliest SentAtUtc per lead
    - Use full table names, catch `Exception ex`
    - _Requirements: 6.1, 11.1_

  - [x] 2.4 Extend MeetingRepository for timeline queries
    - Implement `GetByLeadRequestIdAsync(int leadRequestId)` — returns all non-cancelled meetings for a lead, ordered by ScheduledAtUtc desc
    - Implement `GetUpcomingByLeadRequestIdAsync(int leadRequestId)` — returns the next upcoming meeting (ScheduledAtUtc > GETUTCDATE()) for placeholder resolution
    - Use full table names, catch `Exception ex`
    - _Requirements: 4.3, 4.4, 11.1_

  - [x] 2.5 Extend ActivityFeedRepository for timeline and metrics queries
    - Implement `GetByLeadRequestIdAsync(int leadRequestId)` — returns all activity entries for a lead, ordered by CreatedAtUtc desc
    - Implement `GetStageChangesInRangeAsync(DateTime startDate, DateTime endDate, int businessId)` — for conversion rate computation
    - Use full table names, catch `Exception ex`
    - _Requirements: 7.1, 7.2, 11.1_

  - [x] 2.6 Extend InvoiceRepository for revenue queries
    - Implement `GetRevenueByProductAsync(DateTime startDate, DateTime endDate, int wonStatusTypeId, int businessId)` — grouped sum with ISNULL for 'General Enquiry'
    - Implement `GetRevenueBySourceAsync(DateTime startDate, DateTime endDate, int wonStatusTypeId, int businessId)` — grouped sum by LeadSourceType.Name
    - Use full table names, catch `Exception ex`
    - _Requirements: 8.1, 8.2, 8.5, 8.6_

  - [x] 2.7 Extend QuotationRepository for proposal link resolution
    - Implement `GetLatestByLeadRequestIdAsync(int leadRequestId)` — returns most recent quotation linked to the lead for {{ProposalLink}} placeholder
    - Use full table names, catch `Exception ex`
    - _Requirements: 4.5_

- [x] 3. Checkpoint — Ensure all migrations and repositories compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Service layer — Lead priority and ClosedAtUtc lifecycle
  - [x] 4.1 Implement priority management in LeadRequestService
    - Implement `SetPriorityAsync(int leadRequestId, int leadPriorityTypeId)` — validate priority type ID (1-3), call repository UpdatePriorityAsync
    - Implement `ClearPriorityAsync(int leadRequestId)` — call repository UpdatePriorityAsync with null
    - Implement `GetPriorityTypesAsync()` — returns List<LeadPriorityTypeDto> from LeadPriorityTypeRepository
    - Handle invalid priority type IDs with `ServiceResult.Fail("Invalid priority type.")`
    - _Requirements: 1.5, 1.6, 1.7, 2.3, 2.4_

  - [ ]* 4.2 Write property test for priority assignment round-trip
    - **Property 1: Priority Assignment Round-Trip**
    - Test: For any lead and valid priority (1-3), SetPriority then ClearPriority results in null; SetPriority results in the specified value; new leads start with null
    - **Validates: Requirements 1.5, 1.6, 1.7**

  - [x] 4.3 Implement ClosedAtUtc lifecycle in LeadRequestService.ChangeStageAsync
    - Extend existing `ChangeStageAsync` method: when transitioning TO a terminal stage (Won/Lost), set ClosedAtUtc = DateTime.UtcNow if currently null
    - When transitioning FROM a terminal stage to non-terminal, set ClosedAtUtc = null
    - Call repository `SetClosedAtUtcAsync` for the lifecycle update
    - _Requirements: 1.8, 1.9_

  - [ ]* 4.4 Write property test for ClosedAtUtc lifecycle
    - **Property 2: ClosedAtUtc Lifecycle**
    - Test: Transitioning to terminal sets ClosedAtUtc within 2s of UtcNow; transitioning from terminal to non-terminal sets ClosedAtUtc to null
    - **Validates: Requirements 1.8, 1.9**

  - [x] 4.5 Implement DaysSinceLastActivity computation in LeadRequestService
    - Extend `GetPipelineDataAsync` to include DaysSinceLastActivity in each LeadCardDto
    - Call `GetLastActivityDatesAsync` batch query and compute calendar days from max activity date to UtcNow
    - Include PriorityName and PriorityColour via join with LeadPriorityType data
    - _Requirements: 3.1, 3.2, 2.3_

  - [ ]* 4.6 Write property test for DaysSinceLastActivity computation
    - **Property 3: Days Since Last Activity Computation**
    - Test: For any combination of response/meeting/activity timestamps, DaysSinceLastActivity equals days between max timestamp and UtcNow, with CreatedAtUtc as floor
    - **Validates: Requirements 3.1, 3.2, 2.3**

- [x] 5. Service layer — Extended template placeholders
  - [x] 5.1 Implement BuildExtendedPlaceholderValuesAsync in ResponseService
    - Implement private method resolving all 9 new placeholder values: AssignedSalesperson (TeamMember lookup), MeetingDate/MeetingLink (upcoming meeting), ProposalLink (latest quotation SharedUrl), Company/Phone (contact), BusinessWebsite/SupportEmail (business), NextStage (next LeadStatusType by DisplayOrder)
    - All null sources resolve to empty string
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [x] 5.2 Extend RenderTemplate method with 9 new placeholder replacements
    - Add .Replace() calls for all 9 new placeholders: {{AssignedSalesperson}}, {{MeetingDate}}, {{MeetingLink}}, {{ProposalLink}}, {{Company}}, {{Phone}}, {{BusinessWebsite}}, {{NextStage}}, {{SupportEmail}}
    - Ensure no raw {{...}} tokens remain in rendered output
    - _Requirements: 4.1, 4.11_

  - [ ]* 5.3 Write property test for template rendering (no raw tokens)
    - **Property 5: Template Rendering Leaves No Raw Tokens**
    - Test: For any template body with any subset of 13 placeholders, rendered output contains zero {{...}} tokens
    - **Validates: Requirements 4.1, 4.11**

  - [ ]* 5.4 Write property test for placeholder value resolution
    - **Property 6: Template Placeholder Value Resolution**
    - Test: Verify each placeholder resolves correctly based on source data; null sources produce empty string; NextStage returns "Completed" for terminal stages
    - **Validates: Requirements 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10**

- [ ] 6. Service layer — InsightsService (new service)
  - [~] 6.1 Create IInsightsService interface and InsightsService class
    - Define interface with methods: GetMetricsAsync, GetNewLeadsCountAsync, GetResponseSlaPercentageAsync, GetConversionRatesAsync, GetRevenueByProductAsync, GetRevenueBySourceAsync, GetAverageSalesCycleDaysAsync
    - Register in DI container
    - Inject ICurrentTenantService for BusinessId scoping, plus required repositories
    - _Requirements: 5.1, 6.1, 7.1, 8.1, 9.1_

  - [~] 6.2 Implement GetNewLeadsCountAsync
    - Count LeadRequest records with CreatedAtUtc in [startDate, endDate) for the business, IsActive = 1
    - _Requirements: 5.1_

  - [ ]* 6.3 Write property test for new leads count
    - **Property 7: New Leads Count Correctness**
    - Test: For any set of leads and date range, count equals number of active leads with CreatedAtUtc in range for the business
    - **Validates: Requirements 5.1**

  - [~] 6.4 Implement GetResponseSlaPercentageAsync
    - For leads created within date range with at least one response: compute elapsed hours from CreatedAtUtc to earliest SentAtUtc
    - Compare against ResponseTimeInHours (matched by ProductId template, or 24h default)
    - Return (leads within threshold / leads with response) × 100, or null if no responses
    - _Requirements: 6.1, 6.3_

  - [ ]* 6.5 Write property test for Response SLA computation
    - **Property 8: Response SLA Computation Correctness**
    - Test: Verify SLA percentage formula and null case when no responses exist
    - **Validates: Requirements 6.1, 6.3**

  - [~] 6.6 Implement GetConversionRatesAsync
    - Demo Conversion: leads with stage_changed to "Meeting Scheduled" in range / leads at New/Contacted during range
    - Proposal Conversion: leads with stage_changed to "Proposal Sent" in range / leads at non-terminal during range
    - Win Rate: leads with ClosedAtUtc in range at Won / (Won + Lost), excluding Inactive
    - Return null for each metric if denominator is zero
    - _Requirements: 7.1, 7.2, 7.3, 7.5_

  - [ ]* 6.7 Write property test for Win Rate computation
    - **Property 9: Win Rate Computation Correctness**
    - Test: Win Rate = Won/(Won+Lost)×100, excludes Inactive, returns null if no terminal leads
    - **Validates: Requirements 7.3, 7.5**

  - [~] 6.8 Implement GetRevenueByProductAsync and GetRevenueBySourceAsync
    - Revenue by Product: sum Invoice.TotalAmount grouped by SalesProduct.Name (or "General Enquiry" for null ProductId) for Won leads with ClosedAtUtc in range
    - Revenue by Source: same pattern grouped by LeadSourceType.Name
    - Compute percentage of total for each row
    - Exclude leads without linked invoices
    - _Requirements: 8.1, 8.2, 8.5, 8.6_

  - [ ]* 6.9 Write property test for revenue grouping
    - **Property 10: Revenue Grouping Correctness**
    - Test: Grouped sums equal invoice totals; percentages sum to 100%; null ProductId grouped as "General Enquiry"; leads without invoices excluded
    - **Validates: Requirements 8.1, 8.2, 8.5, 8.6**

  - [~] 6.10 Implement GetAverageSalesCycleDaysAsync
    - Mean of DATEDIFF(DAY, CreatedAtUtc, ClosedAtUtc) for Won/Lost leads with ClosedAtUtc in range
    - Exclude Inactive leads
    - Return null if no qualifying leads
    - _Requirements: 9.1, 9.3, 9.4_

  - [ ]* 6.11 Write property test for average sales cycle duration
    - **Property 11: Average Sales Cycle Duration Correctness**
    - Test: Mean of (ClosedAtUtc - CreatedAtUtc).TotalDays for Won/Lost; excludes Inactive; null when no qualifying leads
    - **Validates: Requirements 9.1, 9.3, 9.4**

  - [~] 6.12 Implement GetMetricsAsync (orchestrator)
    - Call all individual metric methods and assemble InsightsMetricsDto
    - Validate date range (startDate < endDate), return error if invalid
    - _Requirements: 5.1, 6.1, 7.1, 8.1, 9.1_

- [ ] 7. Service layer — TimelineService (new service)
  - [~] 7.1 Create ITimelineService interface and TimelineService class
    - Define interface with `GetTimelineAsync(int leadRequestId, int page, int pageSize)`
    - Register in DI container
    - Inject LeadResponseRepository, MeetingRepository, ActivityFeedRepository, TeamMemberRepository, ICurrentTenantService
    - _Requirements: 11.1, 11.4_

  - [~] 7.2 Implement timeline event aggregation logic
    - Source responses from LeadResponse entity table (EventType "response", Colour "#129867")
    - Source meetings from Meeting entity table (EventType "meeting", Colour "#C8912E")
    - Source all other events from ActivityFeed: stage_changed (#0D5EA6), assigned/unassigned (#0D5EA6), proposal_linked (#57B8E8), invoice_linked (#57B8E8), marked_as_won (#129867), task_created (#8a9bab)
    - Add synthetic "creation" event from LeadRequest.CreatedAtUtc (Colour "#8a9bab")
    - Resolve actor names via TeamMember lookup (fallback to "System")
    - No duplication: responses/meetings ONLY from entity tables, all others ONLY from ActivityFeed
    - _Requirements: 11.1, 11.3, 12.6_

  - [~] 7.3 Implement timeline ordering and pagination
    - Sort all merged events by Timestamp descending
    - Apply pagination: page size 20, return PagedResult with hasMore flag
    - hasMore = true when (page × pageSize) < totalCount
    - _Requirements: 11.2, 12.3_

  - [ ]* 7.4 Write property test for timeline aggregation correctness
    - **Property 12: Timeline Aggregation Correctness**
    - Test: Timeline contains exactly one event per response, one per non-cancelled meeting, one per activity feed entry (non-duplicate types), and one creation event; all events have valid EventType, Timestamp, Title, ActorName, Colour
    - **Validates: Requirements 11.1, 11.3, 11.4, 12.6**

  - [ ]* 7.5 Write property test for timeline ordering invariant
    - **Property 13: Timeline Ordering Invariant**
    - Test: For any timeline result, events[i].Timestamp >= events[i+1].Timestamp for all adjacent pairs
    - **Validates: Requirements 11.2**

  - [ ]* 7.6 Write property test for timeline pagination bounds
    - **Property 14: Timeline Pagination Bounds**
    - Test: Page P returns events at indices [(P-1)*20, min(P*20, N)); hasMore = true iff P*20 < N
    - **Validates: Requirements 12.3**

- [~] 8. Checkpoint — Ensure all service layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Controller layer — SalesController extensions
  - [~] 9.1 Add Insights page action to SalesController
    - Implement `Insights()` action at route /Sales/Insights
    - Check `IPlanCheckService.IsModuleInPlanAsync("sales")` — redirect if not in plan
    - Return the Insights view
    - _Requirements: 10.1_

  - [~] 9.2 Implement AxPostSetLeadPriority and AxPostClearLeadPriority
    - `AxPostSetLeadPriority(int leadRequestId, int leadPriorityTypeId)` — call LeadRequestService.SetPriorityAsync, return JSON {success, message}
    - `AxPostClearLeadPriority(int leadRequestId)` — call LeadRequestService.ClearPriorityAsync, return JSON {success, message}
    - Both: catch `Exception ex`, return {success: false, message: "Something went wrong. Please try again."}
    - _Requirements: 1.6, 1.7, 2.4_

  - [~] 9.3 Implement AxGetInsightsMetrics
    - `AxGetInsightsMetrics(DateTime startDate, DateTime endDate)` — call InsightsService.GetMetricsAsync, return JSON {success, data}
    - Validate date range, catch `Exception ex`
    - _Requirements: 5.1, 5.2, 6.1, 7.1, 8.1, 9.1, 10.5_

  - [~] 9.4 Implement AxGetLeadTimeline
    - `AxGetLeadTimeline(int leadRequestId, int page = 1)` — call TimelineService.GetTimelineAsync with pageSize=20, return JSON {success, data, hasMore}
    - Catch `Exception ex`
    - _Requirements: 12.1, 12.3_

  - [~] 9.5 Implement AxGetLeadPriorityTypes
    - `AxGetLeadPriorityTypes()` — call LeadRequestService.GetPriorityTypesAsync, return JSON {success, data}
    - Catch `Exception ex`
    - _Requirements: 2.4_

- [ ] 10. Views — Pipeline card enhancements
  - [~] 10.1 Update pipeline card partial view with priority badge
    - Add coloured priority badge to each lead card when LeadPriorityTypeId is assigned
    - Display priority name text in the corresponding PriorityColour
    - Show no badge when LeadPriorityTypeId is null
    - _Requirements: 2.1, 2.2_

  - [~] 10.2 Update pipeline card partial view with DaysSinceLastActivity
    - Display "{N}d ago" text on each card; display "Today" when value is 0
    - Apply warning colour (#C8912E) when value > 7
    - Apply danger colour (#C24A4A) when value > 14
    - _Requirements: 3.3, 3.4, 3.5, 3.6_

  - [ ]* 10.3 Write property test for days-since display formatting
    - **Property 4: Days Since Activity Display Formatting**
    - Test: N=0 → "Today"; N>0 → "{N}d ago"
    - **Validates: Requirements 3.3, 3.4**

- [ ] 11. Views — Insights page
  - [~] 11.1 Create Insights.cshtml Razor view
    - Add date range filter at top with presets: This Week, This Month (default), Last Month, This Quarter, Last 6 Months, This Year, Custom
    - Create responsive metric card grid (3 cols desktop, 2 tablet, 1 mobile)
    - Add metric cards: New Leads, Response SLA (colour-coded: green >80%, amber 50-80%, red <50%), Demo Conversion, Proposal Conversion, Win Rate (green >30%, amber 15-30%, red <15%), Avg. Sales Cycle
    - Add Revenue by Product and Revenue by Source ranked list sections
    - Display "No data" for null metrics
    - Follow `.glass.card-pad` layout with filter card `margin-bottom:22px`
    - _Requirements: 5.2, 5.3, 6.2, 6.3, 7.4, 7.5, 8.3, 8.4, 9.2, 9.3, 10.3, 10.4_

  - [~] 11.2 Add Insights navigation item to Sales sidebar
    - Add "Insights" sub-navigation item positioned after "Pipeline" and before "Contacts"
    - _Requirements: 10.2_

- [ ] 12. Views — Lead Detail page enhancements
  - [~] 12.1 Add priority dropdown to Lead Detail page
    - Add dropdown listing all active LeadPriorityType values plus "Clear Priority" option
    - Wire to AxPostSetLeadPriority / AxPostClearLeadPriority endpoints
    - _Requirements: 2.4_

  - [~] 12.2 Add Timeline section to Lead Detail page
    - Render vertical chronological timeline with coloured dots per event type
    - Show event title, actor name, relative timestamp ("3 days ago"), expandable description
    - Retain existing Meetings section as secondary panel for full CRUD
    - _Requirements: 12.2, 12.4_

  - [~] 12.3 Implement timeline pagination (Load More)
    - Show first 20 events; display "Load More" button when hasMore = true
    - Fetch next page via AxGetLeadTimeline on click
    - _Requirements: 12.3_

- [ ] 13. Views — Template editor placeholder guide
  - [~] 13.1 Update template editor to display new placeholders
    - Add the 9 new placeholders ({{AssignedSalesperson}}, {{MeetingDate}}, {{MeetingLink}}, {{ProposalLink}}, {{Company}}, {{Phone}}, {{BusinessWebsite}}, {{NextStage}}, {{SupportEmail}}) to the placeholder guide alongside existing ones
    - _Requirements: 4.12_

- [ ] 14. Client-side JavaScript
  - [~] 14.1 Implement Insights page JS (date filter + AJAX metrics loading)
    - On date range change: BlockUI.show → fetch AxGetInsightsMetrics → BlockUI.hide → render metric cards
    - Default to current month on page load
    - Use vanilla fetch API, antiforgery token for POST if needed
    - Handle errors with Swal.fire error dialog
    - _Requirements: 10.5, 5.3_

  - [~] 14.2 Implement priority assignment JS on Lead Detail page
    - On dropdown change: BlockUI.show → fetch AxPostSetLeadPriority or AxPostClearLeadPriority → BlockUI.hide → Swal.fire success/error → update badge display
    - _Requirements: 2.4_

  - [~] 14.3 Implement timeline JS on Lead Detail page
    - On page load: fetch AxGetLeadTimeline(page=1) → render timeline
    - On "Load More" click: fetch next page → append events
    - On new activity (response sent, meeting scheduled, stage changed): prepend event optimistically after successful AJAX call
    - _Requirements: 12.2, 12.3, 12.5_

- [~] 15. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit)
- Unit tests validate specific examples and edge cases
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex)` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Existing RenderTemplate uses double-brace {{}} placeholders
- Bottom-up ordering: DB → Entities → Repos → Services → Controller → Views → JS

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "1.6", "1.7"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7"] },
    { "id": 3, "tasks": ["4.1", "4.3", "4.5", "5.1", "5.2"] },
    { "id": 4, "tasks": ["4.2", "4.4", "4.6", "5.3", "5.4", "6.1"] },
    { "id": 5, "tasks": ["6.2", "6.4", "6.6", "6.8", "6.10", "7.1"] },
    { "id": 6, "tasks": ["6.3", "6.5", "6.7", "6.9", "6.11", "6.12", "7.2"] },
    { "id": 7, "tasks": ["7.3", "7.4", "7.5", "7.6"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3", "11.1", "11.2", "12.1", "12.2", "12.3", "13.1"] },
    { "id": 10, "tasks": ["14.1", "14.2", "14.3"] }
  ]
}
```
