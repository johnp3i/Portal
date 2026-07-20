# Implementation Plan: Sales Pipeline (Phase 1)

## Overview

Implement the Sales Pipeline module as an ASP.NET Core MVC feature under the `[sales]` schema, following the existing Portal platform patterns (GenericStoredProcedureRepository, ServiceResult, ICurrentTenantService, IPlanCheckService). The implementation proceeds bottom-up: database migrations → entities → repositories → services → controller → views → client-side JS.

## Tasks

- [ ] 1. Database migrations and schema setup
  - [-] 1.1 Create sales schema and lookup tables (migrations 120–126)
    - Create `[sales]` schema
    - Create `[sales].[Product]` table
    - Create and seed `[sales].[LeadSourceType]`, `[sales].[LeadSourceReferenceType]`, `[sales].[LeadStatusType]`, `[sales].[LeadResponseType]`, `[sales].[MeetingType]`
    - _Requirements: 1.1, 2.1, 3.2, 3.3, 3.4, 5.2, 7.2_

  - [~] 1.2 Create contact and lead request tables (migrations 127–128)
    - Create `[sales].[Contact]` with partial unique indexes on (BusinessId, Email) and (BusinessId, PhoneNumber)
    - Create `[sales].[LeadRequest]` with all FK relationships
    - _Requirements: 1.2, 1.3, 1.4, 3.1_

  - [~] 1.3 Create response and template tables (migrations 129–130)
    - Create `[sales].[LeadResponseTemplate]` table
    - Create `[sales].[LeadResponse]` table
    - _Requirements: 5.1, 6.1_

  - [~] 1.4 Create meeting tables (migrations 131–133)
    - Create `[sales].[Meeting]` table
    - Create `[sales].[MeetingProductRequest]` table
    - Create `[sales].[MeetingOpportunity]` table
    - _Requirements: 7.1, 8.1, 8.2_

  - [~] 1.5 Add FK columns to existing tables (migrations 134–136)
    - ALTER `[dbo].[Quotation]` ADD nullable LeadRequestId FK
    - ALTER `[dbo].[Invoice]` ADD nullable LeadRequestId FK
    - ALTER `[dbo].[Customer]` ADD nullable ContactId FK
    - _Requirements: 9.1, 9.2, 10.1_

- [ ] 2. Entity classes and DbContext configuration
  - [~] 2.1 Create core entity classes
    - Create `SalesContact`, `SalesProduct`, `LeadRequest`, `LeadResponse`, `LeadResponseTemplate`, `Meeting`, `MeetingProductRequest`, `MeetingOpportunity` in `Portal.Infrastructure/Entities/`
    - Create lookup entities: `LeadSourceType`, `LeadSourceReferenceType`, `LeadStatusType`, `LeadResponseType`, `MeetingType`
    - _Requirements: 1.2, 2.1, 3.1, 5.1, 6.1, 7.1, 8.1, 8.2_

  - [~] 2.2 Configure DbContext mappings for sales schema
    - Add `DbSet<T>` for all 13 sales entities
    - Configure schema mapping (`[sales]`) in `OnModelCreating`
    - Configure partial unique indexes, FK relationships, and default values
    - _Requirements: 1.3, 1.4, 2.1, 3.1_

- [ ] 3. DTOs and request/response models
  - [~] 3.1 Create all DTOs and view models
    - Create `ContactListDto`, `CreateContactRequest`, `UpdateContactRequest`
    - Create `LeadCardDto`, `LeadTableRowDto`, `LeadRequestDetailDto`, `LeadFilterDto`, `CreateLeadRequest`
    - Create `MeetingListDto`, `CreateMeetingRequest`, `UpdateMeetingRequest`
    - Create `PreparedResponseDto`, `SendResponseRequest`, `TemplatePlaceholderValues`
    - Create `TemplateListDto`, `CreateTemplateRequest`, `UpdateTemplateRequest`
    - Create `CreateSalesProductRequest`, `UpdateSalesProductRequest`
    - Create `CreateMeetingProductRequest`, `CreateMeetingOpportunity`
    - Create `PagedResult<T>` if not already available
    - _Requirements: 4.3, 4.7, 6.7, 7.9, 9.6, 12.1, 13.1_

- [ ] 4. Repositories
  - [~] 4.1 Implement ContactRepository
    - Insert, Update, Deactivate, GetPaged (with search), GetById, CheckDuplicateAsync
    - All queries scoped by BusinessId, full table names in SQL
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 1.6, 12.3_

  - [~] 4.2 Implement LeadRequestRepository
    - Insert, UpdateStage, UpdateAssignment, Cancel, Deactivate, GetPaged, GetById, GetGroupedByStage
    - _Requirements: 3.1, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10_

  - [~] 4.3 Implement lookup repositories (5 total)
    - LeadSourceTypeRepository, LeadSourceReferenceTypeRepository, LeadStatusTypeRepository, LeadResponseTypeRepository, MeetingTypeRepository
    - Each with GetAll method
    - _Requirements: 3.2, 3.3, 3.4, 5.2, 7.2_

  - [~] 4.4 Implement LeadResponseRepository and LeadResponseTemplateRepository
    - LeadResponseRepository: Insert, GetByLeadRequestId
    - LeadResponseTemplateRepository: Insert, Update, Deactivate, GetPaged, GetById, FindMatchingTemplate
    - _Requirements: 5.1, 6.1, 6.2, 6.3, 6.4_

  - [~] 4.5 Implement MeetingRepository, MeetingProductRequestRepository, MeetingOpportunityRepository
    - MeetingRepository: Insert, Update, Cancel, GetById, GetByLeadRequestId
    - MeetingProductRequestRepository: Insert, GetByMeetingId
    - MeetingOpportunityRepository: Insert, GetByMeetingId
    - _Requirements: 7.1, 7.3, 7.5, 7.6, 8.1, 8.2, 8.3, 8.4_

  - [~] 4.6 Implement SalesProductRepository
    - Insert, Update, Deactivate, GetPaged (with search), GetById, GetAllActive
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [ ] 5. Service layer — Contact and Product
  - [~] 5.1 Implement IContactService and ContactService
    - CreateContactAsync with dedup check (email + phone uniqueness), validation (email or phone required)
    - UpdateContactAsync, DeactivateContactAsync, GetByIdAsync, GetContactsPagedAsync
    - GetContactInterestHistoryAsync, ConvertToCustomerAsync
    - All methods scoped by BusinessId
    - _Requirements: 1.5, 1.6, 1.7, 1.8, 10.3, 10.4, 10.5, 12.3, 14.1_

  - [ ]* 5.2 Write property tests for Contact deduplication and validation
    - **Property 2: Contact Email Uniqueness Per Business**
    - **Property 3: Contact Phone Uniqueness Per Business**
    - **Property 4: Contact Requires Email or Phone**
    - **Validates: Requirements 1.3, 1.4, 1.5, 1.6, 1.7**

  - [~] 5.3 Implement ISalesProductService and SalesProductService
    - CreateProductAsync, UpdateProductAsync, DeactivateProductAsync
    - GetByIdAsync, GetProductsPagedAsync, GetActiveProductsAsync
    - All methods scoped by BusinessId
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 14.5_

  - [ ]* 5.4 Write property tests for entity creation defaults and deactivation
    - **Property 5: Entity Creation Defaults**
    - **Property 6: Deactivation Sets IsActive False**
    - **Validates: Requirements 2.2, 2.4, 3.5, 3.9, 6.2, 6.4, 7.3, 8.3, 8.4**

- [ ] 6. Service layer — Lead Request
  - [~] 6.1 Implement ILeadRequestService and LeadRequestService
    - CreateLeadRequestAsync (default status New, IsCancelled false, IsActive true)
    - ChangeStageAsync (manual transitions between any stages)
    - AssignLeadAsync, CancelLeadAsync, DeactivateLeadAsync
    - SuggestStageTransitionAsync (event-driven conditional updates)
    - MarkAsWonAsync (set Won + trigger Contact→Customer conversion)
    - LinkProposalAsync, LinkInvoiceAsync (set LeadRequestId on Quotation/Invoice)
    - GetLeadDetailAsync, GetPipelineDataAsync, GetLeadsPagedAsync
    - All queries scoped by BusinessId with IsActive filter
    - _Requirements: 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 3.11, 5.7, 7.4, 9.3, 9.4, 9.5, 10.2, 11.1, 11.2, 14.2_

  - [ ]* 6.2 Write property tests for pipeline stage transitions
    - **Property 8: Event-Driven Stage Suggestions**
    - **Property 9: Manual Stage Transition Unrestricted**
    - **Validates: Requirements 3.6, 3.11, 5.7, 7.4, 9.4, 13.8**

  - [ ]* 6.3 Write property tests for lead assignment and cancellation
    - **Property 7: Cancellation Atomicity**
    - **Property 15: Lead Assignment and Unassignment**
    - **Validates: Requirements 3.7, 3.8, 7.6, 11.1, 11.2**

  - [ ]* 6.4 Write property tests for document linking
    - **Property 16: Document Linking Sets LeadRequestId**
    - **Validates: Requirements 9.3, 9.5**

- [ ] 7. Service layer — Response and Templates
  - [~] 7.1 Implement IResponseService and ResponseService
    - PrepareResponseAsync (find matching template by ProductId or fallback, render placeholders)
    - SendResponseAsync (record LeadResponse, trigger stage suggestion if status is New)
    - RenderTemplate (replace all 4 placeholders with values or empty string)
    - CreateTemplateAsync, UpdateTemplateAsync, DeactivateTemplateAsync
    - GetTemplatesPagedAsync, GetTemplateByIdAsync, GetResponsesForLeadAsync
    - All queries scoped by BusinessId
    - _Requirements: 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 6.2, 6.3, 6.4, 6.5, 6.6, 6.8, 14.4_

  - [ ]* 7.2 Write property test for template rendering
    - **Property 10: Template Rendering Replaces All Placeholders**
    - **Validates: Requirements 5.4, 6.5, 6.6**

- [ ] 8. Service layer — Meeting
  - [~] 8.1 Implement IMeetingService and MeetingService
    - CreateMeetingAsync (with stage suggestion when linked to lead)
    - UpdateMeetingAsync, CancelMeetingAsync
    - GetByIdAsync, GetMeetingsForLeadAsync
    - GenerateIcsFileAsync (RFC 5545 string building, return byte[])
    - CreateProductRequestAsync, CreateOpportunityAsync
    - GetProductRequestsForMeetingAsync, GetOpportunitiesForMeetingAsync
    - All queries scoped by BusinessId
    - _Requirements: 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.10, 8.3, 8.4, 8.6, 14.3_

  - [ ]* 8.2 Write property test for ICS file generation
    - **Property 12: ICS File Contains Required VEVENT Fields**
    - **Validates: Requirements 7.7**

- [~] 9. Checkpoint — Core services complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. DI registration and module wiring
  - [~] 10.1 Register services and repositories in DI container
    - Register all 5 service interfaces and implementations
    - Register all 13 repositories
    - Register PortalModules.Sales module identifier
    - Wire IPlanCheckService check for "sales" module
    - _Requirements: 15.4, 15.5_

  - [ ]* 10.2 Write property test for tenant isolation
    - **Property 1: Tenant Isolation**
    - **Validates: Requirements 1.8, 2.5, 3.10, 6.8, 7.10, 8.6, 14.1–14.7**

- [ ] 11. SalesController — Page actions and AJAX endpoints
  - [~] 11.1 Implement SalesController page actions
    - Pipeline, Contacts, Products, Meetings, Templates, LeadDetail, ContactDetail
    - Each action resolves BusinessId via ICurrentTenantService
    - Each action checks IPlanCheckService (redirect to upgrade if not in plan)
    - _Requirements: 4.1, 12.1, 2.6, 7.9, 6.7, 13.1, 12.7, 15.1, 15.2, 15.3_

  - [~] 11.2 Implement SalesController AJAX endpoints — Contact and Product
    - AxPostCreateContact, AxPostUpdateContact, AxPostDeactivateContact
    - AxPostCreateProduct, AxPostUpdateProduct, AxPostDeactivateProduct
    - AxGetContactsSearch (paginated search)
    - All return Json(new { success, message, data? })
    - _Requirements: 1.5, 1.6, 1.7, 2.2, 2.3, 2.4, 12.2, 12.3, 12.4, 12.5, 12.6_

  - [~] 11.3 Implement SalesController AJAX endpoints — Lead management
    - AxPostCreateLeadRequest, AxPostChangeLeadStage, AxPostAssignLead, AxPostUnassignLead
    - AxPostCancelLead, AxPostDeactivateLead, AxPostMarkAsWon
    - AxGetPipelineData (grouped by stage for Kanban)
    - AxGetLeadDetail
    - _Requirements: 3.5, 3.6, 3.7, 3.8, 3.9, 4.1, 4.5, 4.6, 10.2, 11.1, 11.2, 13.5_

  - [~] 11.4 Implement SalesController AJAX endpoints — Meeting and Response
    - AxPostCreateMeeting, AxPostUpdateMeeting, AxPostCancelMeeting
    - AxGetDownloadIcs (return File with text/calendar content type)
    - AxPostCreateMeetingProductRequest, AxPostCreateMeetingOpportunity
    - AxGetPrepareResponse, AxPostSendResponse
    - AxPostCreateTemplate, AxPostUpdateTemplate, AxPostDeactivateTemplate
    - _Requirements: 5.3, 5.6, 7.3, 7.5, 7.6, 7.7, 7.8, 8.3, 8.4, 6.2, 6.3, 6.4, 13.6, 13.7_

  - [~] 11.5 Implement proposal and invoice linking endpoints
    - "Create Proposal" action navigating to quotation creation with LeadRequestId pre-populated
    - "Create Invoice" action navigating to invoice creation with LeadRequestId pre-populated
    - Display linked documents on lead detail view
    - _Requirements: 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

- [~] 12. Checkpoint — Controller complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Razor views — Pipeline and Contacts
  - [~] 13.1 Implement Pipeline view (Kanban board + table toggle)
    - Kanban board with columns per LeadStatusType (ordered by DisplayOrder)
    - Lead cards showing: Contact name, Product name, Assigned user, CreatedAtUtc (relative)
    - Stage count in column headers
    - Table view alternative with paginated list (Page_Size 15)
    - Filter controls: AssignedToUserId dropdown, ProductId dropdown
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 11.4, 11.5_

  - [~] 13.2 Implement Contacts view and ContactDetail view
    - Contacts: searchable, paginated list (Page_Size 15) with Name, Email, Phone, Company, Lead Count, IsActive, CreatedAtUtc
    - Create/Edit contact form with SweetAlert2 confirmation for deactivation
    - ContactDetail: contact info + interest history (all LeadRequests ordered by CreatedAtUtc desc)
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7_

- [ ] 14. Razor views — Lead Detail and Meetings
  - [~] 14.1 Implement LeadDetail view
    - Contact info, Product name, Lead Source, Current stage, Assigned user, RequestText, CreatedAtUtc
    - Response history section (ordered by SentAtUtc desc)
    - Meetings section (ordered by ScheduledAtUtc desc)
    - Linked documents section (Quotations + Invoices)
    - Pipeline stage change controls (dropdown/button group)
    - Action buttons: Respond, Schedule Meeting, Create Proposal, Create Invoice, Mark as Won
    - Terminal stage visual indicator with reopen capability
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7, 13.8, 10.7_

  - [~] 14.2 Implement Meetings view
    - List meetings with: Subject, Meeting Type, Scheduled Date, Duration, Outcome (truncated 100 chars), IsCancelled
    - Create/Edit meeting form, Cancel with SweetAlert2 confirmation
    - Meeting product requests and opportunities display
    - ICS download button
    - _Requirements: 7.3, 7.5, 7.6, 7.7, 7.8, 7.9, 8.5_

- [ ] 15. Razor views — Products and Templates
  - [~] 15.1 Implement Products view
    - Searchable, paginated product list (Page_Size 15) with Name, Description, IsActive, CreatedAtUtc
    - Create/Edit product form, Deactivate with SweetAlert2 confirmation
    - _Requirements: 2.6, 2.7_

  - [~] 15.2 Implement Templates view
    - Template list with: Name, Product (or "All Products"), Response Type, ResponseTimeInHours, IsActive
    - Create/Edit template form with placeholder guide
    - Template preview functionality
    - Deactivate with SweetAlert2 confirmation
    - _Requirements: 6.7, 6.2, 6.3, 6.4_

- [ ] 16. Client-side JavaScript
  - [~] 16.1 Implement Pipeline Kanban board interactions
    - Fetch pipeline data via AxGetPipelineData
    - Render Kanban columns dynamically
    - Lead card click navigates to LeadDetail
    - Filter application (assigned user, product)
    - Table/Kanban view toggle
    - BlockUI for all AJAX calls, SweetAlert2 for errors
    - _Requirements: 4.1, 4.2, 4.5, 4.6, 4.8_

  - [~] 16.2 Implement Contact and Product form interactions
    - Create/Edit contact with dedup error handling (display existing contact name)
    - Deactivate contact/product with SweetAlert2 confirmation dialog
    - Contact search with debounced input
    - Pagination controls
    - BlockUI + SweetAlert2 pattern for all AJAX
    - _Requirements: 1.5, 1.6, 12.3, 12.4, 12.5, 12.6_

  - [~] 16.3 Implement Lead Detail interactions
    - Stage change via dropdown/button group (AxPostChangeLeadStage)
    - Assign/Unassign lead (AxPostAssignLead, AxPostUnassignLead)
    - Respond action: load suggested response (AxGetPrepareResponse), review, send (AxPostSendResponse)
    - Schedule meeting: open form pre-populated with ContactId + LeadRequestId
    - Mark as Won with SweetAlert2 confirmation showing conversion details
    - Cancel lead with description input via SweetAlert2
    - BlockUI + SweetAlert2 pattern for all AJAX
    - _Requirements: 5.3, 5.6, 5.8, 10.7, 11.3, 13.5, 13.6, 13.7, 13.8_

  - [~] 16.4 Implement Meeting and Template form interactions
    - Create/Edit/Cancel meeting forms with SweetAlert2 confirmation
    - ICS download via AxGetDownloadIcs
    - Meeting product request and opportunity creation forms
    - Template create/edit/deactivate with preview
    - BlockUI + SweetAlert2 pattern for all AJAX
    - _Requirements: 7.3, 7.5, 7.6, 7.7, 8.3, 8.4, 6.2, 6.3, 6.4_

- [ ] 17. Sidebar navigation and module registration
  - [~] 17.1 Register Sales module in sidebar and configure navigation
    - Add "Sales" top-level sidebar item with appropriate icon
    - Add sub-navigation: Pipeline (default), Contacts, Products, Meetings, Templates
    - Configure subscription tier check (display upgrade prompt when plan does not include Sales)
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5_

- [~] 18. Checkpoint — Views and navigation complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 19. Integration and cross-cutting concerns
  - [~] 19.1 Wire Contact-to-Customer conversion end-to-end
    - Mark as Won → check existing Customer (email/name match) → create or link
    - SweetAlert2 confirmation dialog showing contact details before conversion
    - Handle "customer already exists" message display
    - _Requirements: 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [ ]* 19.2 Write property test for Contact-to-Customer conversion
    - **Property 14: Contact-to-Customer Conversion Correctness**
    - **Validates: Requirements 10.3, 10.4, 10.5**

  - [~] 19.3 Wire proposal and invoice linking end-to-end
    - "Create Proposal" from lead detail → quotation creation with LeadRequestId
    - "Create Invoice" from lead detail → invoice creation with LeadRequestId
    - Stage suggestion on proposal link (→ Proposal Sent)
    - Display linked documents on lead detail
    - _Requirements: 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

  - [ ]* 19.4 Write property tests for pipeline filtering and search
    - **Property 11: Pipeline Filter Correctness**
    - **Property 13: Contact Search Returns Partial Matches**
    - **Property 17: Pipeline Stage Count Accuracy**
    - **Validates: Requirements 4.4, 4.5, 4.6, 11.5, 12.3**

- [~] 20. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (17 properties total)
- Unit tests validate specific examples and edge cases
- All AJAX interactions must follow BlockUI → fetch → Unblock → SweetAlert2 pattern
- All confirmation dialogs use SweetAlert2 (never native alert/confirm)
- Repository SQL must use full table names (no aliases) per project standards
- All catch blocks must capture `Exception ex` per coding golden rules
- All AJAX controller methods must use `AxPost`/`AxGet` prefix naming convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4"] },
    { "id": 2, "tasks": ["1.5", "2.1"] },
    { "id": 3, "tasks": ["2.2", "3.1"] },
    { "id": 4, "tasks": ["4.1", "4.3", "4.6"] },
    { "id": 5, "tasks": ["4.2", "4.4", "4.5"] },
    { "id": 6, "tasks": ["5.1", "5.3"] },
    { "id": 7, "tasks": ["5.2", "5.4", "6.1"] },
    { "id": 8, "tasks": ["6.2", "6.3", "6.4", "7.1"] },
    { "id": 9, "tasks": ["7.2", "8.1"] },
    { "id": 10, "tasks": ["8.2", "10.1"] },
    { "id": 11, "tasks": ["10.2", "11.1"] },
    { "id": 12, "tasks": ["11.2", "11.3", "11.4", "11.5"] },
    { "id": 13, "tasks": ["13.1", "13.2"] },
    { "id": 14, "tasks": ["14.1", "14.2", "15.1", "15.2"] },
    { "id": 15, "tasks": ["16.1", "16.2", "16.3", "16.4"] },
    { "id": 16, "tasks": ["17.1"] },
    { "id": 17, "tasks": ["19.1", "19.3"] },
    { "id": 18, "tasks": ["19.2", "19.4"] }
  ]
}
```
