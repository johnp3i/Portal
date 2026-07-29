# Implementation Plan: Business Applications Tracker (Compliance Filings)

## Overview

This plan implements the Compliance Filings module end-to-end: database schema and seed data, EF Core entities, DTOs, repository, service layer with business logic, two controllers (business-facing + admin), four Razor views, a dashboard ViewComponent, DI registration, plan permission wiring, and build verification. Tasks are ordered so each step builds on the previous — no orphaned code.

## Tasks

- [x] 1. Database migration and schema setup
  - [x] 1.1 Create SQL migration script for `[compliance]` schema and all 4 tables
    - Create file `SQL/Migrations/Compliance_CreateSchema.sql`
    - Create `[compliance]` schema
    - Create `ApplicationCategory` table with PK, unique constraint on Name, CreatedAtUtc default
    - Create `ApplicationType` table with PK, FK to ApplicationCategory, composite unique on (Name, Country), CHECK constraints on Frequency/DueMonth/DueDay, CreatedAtUtc default
    - Create `BusinessApplication` table with PK, FK to ApplicationType, CHECK constraint on Status, indexes on (BusinessId, DueDate) and (BusinessId, Status), CreatedAtUtc default
    - Create `ApplicationAttachment` table with PK, FK to BusinessApplication (NO ACTION on delete), CreatedAtUtc default
    - _Requirements: 1.1, 1.3, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2_

  - [x] 1.2 Create SQL seed data script for categories and Cyprus templates
    - Insert 4 ApplicationCategory records: Tax, Employee, Regulatory, Business Registration
    - Insert 5 Cyprus ApplicationType templates with correct Frequency, DefaultDueMonth, DefaultDueDay values
    - _Requirements: 1.2, 2.6_

  - [x] 1.3 Create PlanFeature seed migration for Professional and Enterprise plans
    - Add `compliance` module key to PlanFeature table for Professional and Enterprise subscription tiers
    - _Requirements: 14.1_

- [x] 2. Entity classes and EF Core configuration
  - [x] 2.1 Create entity classes for compliance tables
    - Create `Entities/ApplicationCategory.cs` with all properties matching the SQL schema
    - Create `Entities/ApplicationType.cs` with all properties
    - Create `Entities/BusinessApplication.cs` with all properties
    - Create `Entities/ApplicationAttachment.cs` with all properties
    - _Requirements: 1.1, 2.1, 3.1, 4.1_

  - [x] 2.2 Add EF Core DbContext configuration for compliance entities
    - Add `DbSet<>` properties for all 4 compliance entities to the Portal DbContext
    - Configure table mapping to `[compliance]` schema in `OnModelCreating`
    - Configure `CreatedAtUtc` default value (`GETUTCDATE()`) for all entities
    - Configure relationships, constraints, and indexes to match SQL migration
    - _Requirements: 1.1, 2.1, 3.1, 4.1_

- [x] 3. DTO and request model classes
  - [x] 3.1 Create all DTO classes for compliance module
    - Create `Models/Compliance/BusinessApplicationDto.cs` (list view DTO with DueStatus, DaysUntilDue)
    - Create `Models/Compliance/BusinessApplicationDetailDto.cs` (detail view with AllowedTransitions, Attachments)
    - Create `Models/Compliance/ApplicationAttachmentDto.cs`
    - Create `Models/Compliance/ApplicationTypeDto.cs` (admin/import DTO)
    - Create `Models/Compliance/ApplicationCategoryDto.cs`
    - Create `Models/Compliance/UpcomingFilingDto.cs` (dashboard widget)
    - Create `Models/Compliance/CalendarFilingDto.cs` (calendar view)
    - _Requirements: 7.1, 8.1, 11.2, 12.2_

  - [x] 3.2 Create request model classes for compliance operations
    - Create `Models/Compliance/ImportTemplatesRequest.cs` (TemplateIds, Year, OneOffDueDate)
    - Create `Models/Compliance/CreateApplicationTypeRequest.cs`
    - Create `Models/Compliance/UpdateApplicationTypeRequest.cs`
    - Create `Models/Compliance/CreateCategoryRequest.cs`
    - Create `Models/Compliance/UpdateCategoryRequest.cs`
    - _Requirements: 5.2, 5.6, 6.2_

- [x] 4. Constants and permission wiring
  - [x] 4.1 Add `Compliance` module constant and controller mappings
    - Add `public const string Compliance = "compliance";` to `PortalModules.cs`
    - Add `Compliance` to the `All` array in PortalModules
    - Add `[PortalModules.Compliance] = new[] { "Compliance", "AdminCompliance" }` to `ModuleControllerMap`
    - Add description to `PlanPermissionFilter.GetModuleDescription` for the compliance module
    - _Requirements: 14.1, 14.2, 14.3_

- [x] 5. Checkpoint - Ensure project compiles
  - Ensure all entity classes, DTOs, and constants are compilable. Ask the user if questions arise.

- [x] 6. Repository layer
  - [x] 6.1 Create ComplianceRepository with category and type data access methods
    - Create `Repositories/ComplianceRepository.cs` extending `GenericStoredProcedureRepository<BusinessApplication>`
    - Implement `GetAllCategoriesAsync()`, `InsertCategoryAsync()`, `UpdateCategoryAsync()`
    - Implement `GetAllTypesAsync()` (with JOIN to ApplicationCategory for CategoryName), `InsertTypeAsync()`, `UpdateTypeAsync()`, `DeactivateTypeAsync()`, `TypeExistsAsync()`
    - Use full table names in SQL, parameterized queries with `SqlParameter`, null-safety with `?? (object)DBNull.Value`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 6.2 Add business application data access methods to ComplianceRepository
    - Implement `GetPagedAsync(businessId, category, status, dateFrom, dateTo, page, pageSize)` with JOIN to ApplicationType and ApplicationCategory
    - Implement `GetByIdAsync(id, businessId)` with tenant isolation WHERE clause
    - Implement `InsertBatchAsync(List<BusinessApplication>)` for template import
    - Implement `UpdateStatusAsync(id, status, submittedAt?, approvedAt?)`
    - Implement `UpdateDetailsAsync(id, referenceNumber, notes)`
    - Implement `ExistsForTypeAndPeriodAsync(businessId, typeId, year)` for duplicate detection
    - All queries include mandatory `BusinessId` filter
    - _Requirements: 3.4, 3.5, 6.7, 7.1, 7.3, 7.4, 8.5, 15.1, 15.2_

  - [x] 6.3 Add dashboard, calendar, and attachment data access methods to ComplianceRepository
    - Implement `GetUpcomingAsync(businessId, days, maxItems)` for widget — filters Pending/InProgress within date range
    - Implement `GetCalendarAsync(businessId, year)` — all filings for selected year
    - Implement `InsertAttachmentAsync(entity)`, `GetAttachmentByIdAsync(id, businessId)`, `DeleteAttachmentAsync(id)`, `GetAttachmentCountAsync(applicationId)`, `GetAttachmentsForApplicationAsync(applicationId)`
    - _Requirements: 4.2, 4.3, 10.5, 11.1, 12.1_

- [x] 7. Service layer
  - [x] 7.1 Create IComplianceService interface and ComplianceService class with category/type management
    - Create `Services/IComplianceService.cs` with full interface definition
    - Create `Services/ComplianceService.cs` with constructor injection of ComplianceRepository and IFileStorageService
    - Implement `GetCategoriesAsync()`, `CreateCategoryAsync()`, `UpdateCategoryAsync()`
    - Implement `GetAllTypesAsync()`, `CreateTypeAsync()` (with duplicate validation), `UpdateTypeAsync()`, `DeactivateTypeAsync()`
    - Return `ServiceResult` for all mutating operations
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 7.2 Add import logic and due date calculation to ComplianceService
    - Implement `GetAvailableTemplatesAsync(country)` — filtered active templates
    - Implement `HasDuplicatesAsync(businessId, typeIds, year)` — duplicate detection
    - Implement private `CalculateDueDates(frequency, year, defaultDueMonth, defaultDueDay)` — Monthly returns 12 dates, Quarterly returns 4, Annual returns 1, One-off returns empty
    - Implement `ImportTemplatesAsync(businessId, request)` — iterates templates, calculates due dates, calls batch insert
    - Clamp day to month end for months with fewer days (e.g., Feb 30 → Feb 28/29)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8_

  - [x] 7.3 Add status workflow and detail operations to ComplianceService
    - Implement `ValidTransitions` dictionary defining all permitted transitions
    - Implement `UpdateStatusAsync(id, newStatus, businessId)` — validates transition, sets SubmittedAtUtc/ApprovedAtUtc timestamps as appropriate
    - Implement `GetApplicationsAsync(businessId, filters, page, pageSize)` — adds DueStatus calculation (normal/warning/urgent/overdue) and DaysUntilDue to each DTO
    - Implement `GetApplicationDetailAsync(id, businessId)` — includes AllowedTransitions array based on current status
    - Implement `UpdateDetailsAsync(id, referenceNumber, notes, businessId)`
    - _Requirements: 7.5, 7.6, 7.7, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 9.1, 9.2, 9.3, 9.4, 13.1, 13.2, 13.3, 13.4, 13.5_

  - [x] 7.4 Add attachment operations and dashboard/calendar methods to ComplianceService
    - Implement `UploadAttachmentAsync(applicationId, businessId, userId, file)` — validates PDF type, 5 MB limit, max 3 count, calls IFileStorageService
    - Implement `DeleteAttachmentAsync(attachmentId, businessId)` — removes record and physical file
    - Implement `DownloadAttachmentAsync(attachmentId, businessId)` — retrieves file with tenant check
    - Implement `GetUpcomingFilingsAsync(businessId, days, maxItems)` — with DueStatus calculation
    - Implement `GetCalendarDataAsync(businessId, year)` — with DueStatus per filing
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 11.1, 11.2, 11.4, 11.5, 12.1, 12.2_

- [x] 8. Checkpoint - Ensure service layer compiles and logic is correct
  - Ensure all service methods, repository calls, and DTOs wire up correctly. Ask the user if questions arise.

- [x] 9. Controllers
  - [x] 9.1 Create ComplianceController with page actions and AJAX endpoints
    - Create `Controllers/ComplianceController.cs` with `[Authorize]` and `[ModuleAccess(PortalModules.Compliance)]`
    - Inject `IComplianceService`, `ICurrentTenantService`, `IFileStorageService`
    - Implement page actions: `Index` (with filter params), `Import`, `Detail(int id)`, `Calendar(int? year)`
    - Implement AJAX endpoints: `AxPostImportTemplates`, `AxPostUpdateStatus`, `AxPostUpdateDetails`, `AxPostUploadAttachment`, `AxPostDeleteAttachment`, `AxGetAvailableTemplates`, `AxGetCalendarData`
    - All endpoints resolve BusinessId from `ICurrentTenantService`, return `Json(new { success, message })`
    - All try/catch blocks capture `Exception ex`
    - _Requirements: 6.1, 6.8, 7.1, 8.1, 8.2, 8.5, 9.2, 10.1, 10.5, 10.6, 12.1, 15.1, 15.2, 15.3, 15.4, 16.1, 16.2, 16.4_

  - [x] 9.2 Create AdminComplianceController with template management endpoints
    - Create `Controllers/AdminComplianceController.cs` with `[Authorize(Roles = "SuperAdmin")]`
    - Inject `IComplianceService`
    - Implement page actions: `Index` (template list), `Categories` (category management)
    - Implement AJAX endpoints: `AxPostCreateType`, `AxPostUpdateType`, `AxPostDeactivateType`, `AxPostCreateCategory`, `AxPostUpdateCategory`
    - All endpoints return `Json(new { success, message })`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [x] 10. Razor views
  - [x] 10.1 Create compliance Index view (list view with filters and pagination)
    - Create `Views/Compliance/Index.cshtml`
    - Topbar with eyebrow label, heading, description
    - Filter section (`.glass.card-pad`, margin-bottom:22px): Category dropdown, Status dropdown, Date range pickers, Filter/Clear buttons
    - Data table section (`.glass.card-pad`): columns for Application Name, Category, Due Date, Status badge, Reference, Attachment indicator
    - Overdue/warning/urgent badges per requirement 7.5-7.7 and 13.1-13.4
    - Pagination (15 per page) with info text and controls
    - JavaScript: BlockUI + fetch + SweetAlert2 for status quick-actions
    - Mobile responsive: hide ReferenceNumber column below 576px
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 13.1, 13.2, 13.3, 16.1, 17.1_

  - [x] 10.2 Create compliance Import view (template selection and import)
    - Create `Views/Compliance/Import.cshtml`
    - Display available templates grouped by category with checkboxes
    - Year selector (default: current year)
    - One-off date picker (shown conditionally when one-off template selected)
    - Duplicate warning SweetAlert2 confirmation dialog
    - Import button: BlockUI → fetch AxPostImportTemplates → unblock → SweetAlert2 success with count
    - _Requirements: 6.1, 6.2, 6.6, 6.7, 16.4_

  - [x] 10.3 Create compliance Detail view (single application detail/edit)
    - Create `Views/Compliance/Detail.cshtml`
    - Display: Application Name, Category, Frequency, DueDate, Status badge with overdue indicators, ReferenceNumber (editable), Notes (editable textarea), timestamps
    - Status transition buttons (rendered based on AllowedTransitions): BlockUI → fetch AxPostUpdateStatus → unblock → SweetAlert2
    - Save details button: BlockUI → fetch AxPostUpdateDetails → unblock → SweetAlert2
    - Attachment section: list current attachments (max 3), upload button (PDF only, ≤5 MB), delete with SweetAlert2 confirmation
    - Mobile responsive: stack fields vertically below 576px, 44px touch targets
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 10.1, 10.2, 10.3, 10.4, 10.6, 13.1, 13.2, 13.3, 13.4, 16.1, 16.2, 16.3, 17.5_

  - [x] 10.4 Create compliance Calendar view (year overview)
    - Create `Views/Compliance/Calendar.cshtml`
    - 12-month grid layout with year navigation (prev/next)
    - Filing deadlines as coloured dot markers: Pending=grey, InProgress=blue, Submitted=amber, Approved=green, Rejected=red
    - Overdue filings with distinct marker style (pulse animation or border)
    - Today's date highlighted
    - Click on day shows popover with filing names and statuses
    - Load data via AxGetCalendarData fetch call
    - Mobile responsive: single-month scrollable view below 768px
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 17.2_

- [x] 11. Admin views
  - [x] 11.1 Create AdminCompliance Index view (template catalog management)
    - Create `Views/AdminCompliance/Index.cshtml`
    - Table listing all ApplicationType records: Name, Country, Category, Frequency, IsActive, actions (Edit/Deactivate)
    - Create new template form/modal: Name, Description, Country, Category dropdown, Frequency dropdown, DefaultDueMonth, DefaultDueDay
    - Edit template modal
    - Deactivate with SweetAlert2 confirmation dialog
    - All AJAX: BlockUI → fetch → unblock → SweetAlert2
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 16.1_

  - [x] 11.2 Create AdminCompliance Categories view (category management)
    - Create `Views/AdminCompliance/Categories.cshtml`
    - Table listing categories: Name, Description, IsActive
    - Create/edit forms with SweetAlert2 feedback
    - All AJAX follows BlockUI pattern
    - _Requirements: 5.6_

- [x] 12. Dashboard integration
  - [x] 12.1 Create UpcomingFilingsViewComponent and its partial view
    - Create `ViewComponents/UpcomingFilingsViewComponent.cs` with IComplianceService, ICurrentTenantService, IPlanCheckService injection
    - Check plan access — return empty if no compliance module access
    - Fetch upcoming filings (30 days, max 5 items)
    - Create `Views/Shared/Components/UpcomingFilings/Default.cshtml` — card with filing list, due date badges, warning/danger colours, "View All" link
    - Handle empty state: "No filings due in the next 30 days"
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 14.4, 17.3_

  - [x] 12.2 Integrate UpcomingFilings widget into Home/Index.cshtml
    - Add `@await Component.InvokeAsync("UpcomingFilings")` to the dashboard page in the appropriate widget section
    - _Requirements: 11.1_

- [x] 13. Dependency injection registration
  - [x] 13.1 Register ComplianceService and ComplianceRepository in Program.cs
    - Add `builder.Services.AddScoped<IComplianceService, ComplianceService>();`
    - Add `builder.Services.AddScoped<ComplianceRepository>();`
    - _Requirements: All (enables DI for the module)_

- [x] 14. Checkpoint - Full build verification
  - Ensure the entire project compiles cleanly with `dotnet build`. Ask the user if questions arise.

- [ ] 15. Unit tests
  - [ ]* 15.1 Write unit tests for status transition logic
    - Test all valid transitions succeed (Pending→InProgress, Pending→Submitted, InProgress→Submitted, Submitted→Approved, Submitted→Rejected, Rejected→InProgress)
    - Test invalid transitions return failure (Approved→anything, Pending→Rejected, InProgress→Approved, etc.)
    - Test SubmittedAtUtc set on transition to Submitted
    - Test ApprovedAtUtc set on transition to Approved
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 8.3, 8.4_

  - [ ]* 15.2 Write unit tests for due date calculation logic
    - Test Monthly frequency returns 12 dates with correct day
    - Test Monthly clamps to month end (day 31 in Feb → Feb 28/29)
    - Test Quarterly returns 4 dates (months 1, 4, 7, 10)
    - Test Annual returns 1 date with correct month/day
    - Test One-off returns empty list
    - _Requirements: 6.3, 6.4, 6.5, 6.6_

  - [ ]* 15.3 Write unit tests for attachment validation logic
    - Test valid PDF upload succeeds
    - Test non-PDF file rejected
    - Test file over 5 MB rejected
    - Test 4th attachment rejected (max 3 enforcement)
    - _Requirements: 10.1, 10.2, 10.3, 4.3_

  - [ ]* 15.4 Write unit tests for overdue/warning status calculation
    - Test filing 7 days away with Pending status returns "warning"
    - Test filing 3 days away with InProgress status returns "urgent"
    - Test filing past due with Pending status returns "overdue" with days count
    - Test filed (Submitted/Approved/Rejected) past due returns "normal" (suppressed)
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

  - [ ]* 15.5 Write unit tests for import logic
    - Test Monthly template creates 12 BusinessApplication records
    - Test import scoped to session BusinessId (ignores request body BusinessId)
    - Test duplicate detection returns warning when existing records found
    - _Requirements: 6.2, 6.3, 6.7, 6.8_

- [x] 16. Final checkpoint - Full build and test verification
  - Run `dotnet build` and `dotnet test` (if test project configured). Ensure all tests pass. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- The design does not include Correctness Properties — unit tests are used instead of property-based tests
- Checkpoints at tasks 5, 8, 14, and 16 ensure incremental validation
- All AJAX patterns use BlockUI + fetch + SweetAlert2 per project standards
- All SQL uses full table names (no aliases) and parameterized queries
- Repository follows `GenericStoredProcedureRepository<T>` pattern
- Controllers use `AxPost`/`AxGet` prefix convention for AJAX endpoints

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "4.1"] },
    { "id": 2, "tasks": ["2.2", "3.1", "3.2"] },
    { "id": 3, "tasks": ["6.1"] },
    { "id": 4, "tasks": ["6.2", "6.3"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3"] },
    { "id": 7, "tasks": ["7.4"] },
    { "id": 8, "tasks": ["9.1", "9.2"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 10, "tasks": ["11.1", "11.2", "12.1"] },
    { "id": 11, "tasks": ["12.2", "13.1"] },
    { "id": 12, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5"] }
  ]
}
```
