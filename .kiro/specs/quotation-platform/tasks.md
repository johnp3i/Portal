# Implementation Plan: Quotation Platform

## Overview

Implement tenant-scoped quotation management with line items, lifecycle state transitions, pricing calculations, and audit logging within the Portal platform. This adds a database migration (VatRate on QuotationLine), QuotationRepository, QuotationLineRepository, AuditLogRepository, IQuotationService/QuotationService, QuotationController, view models, and Razor views following the exact patterns established in Module 1 (Customer Registry).

## Tasks

- [x] 1. Database migration and entity update
  - [x] 1.1 Create migration 020_AddVatRateToQuotationLine.sql
    - Create `Portal.Database/Migrations/020_AddVatRateToQuotationLine.sql`
    - Add `ALTER TABLE [quotation].[QuotationLine] ADD [VatRate] DECIMAL(5,2) NOT NULL CONSTRAINT [DF_QuotationLine_VatRate] DEFAULT 0`
    - Make the script idempotent (check IF NOT EXISTS before altering)
    - _Requirements: 6.2_

  - [x] 1.2 Add VatRate property to QuotationLine entity
    - Add `public decimal VatRate { get; set; }` to `Portal.Infrastructure/Entities/QuotationLine.cs`
    - _Requirements: 6.2_

  - [x] 1.3 Configure VatRate in PortalDbContext
    - In `Portal.Infrastructure/Data/PortalDbContext.cs`, add `entity.Property(e => e.VatRate).HasPrecision(5, 2);` to the `ConfigureQuotationLine` method
    - _Requirements: 6.2_

- [x] 2. Create repositories
  - [x] 2.1 Create QuotationRepository
    - Create `Portal.Infrastructure/Repositories/QuotationRepository.cs`
    - Extend `GenericStoredProcedureRepository<Quotation>` with constructor accepting `DbContext`
    - Implement `GetAllByBusinessIdAsync(int businessId)` — SELECT all columns FROM `[quotation].[Quotation]` WHERE BusinessId = @BusinessId, JOIN `[customer].[Customer]` for CustomerName and `[quotation].[QuotationStatusType]` for StatusName
    - Implement `GetByIdAndBusinessIdAsync(int id, int businessId)` — SELECT single record WHERE Id = @Id AND BusinessId = @BusinessId
    - Implement `InsertAsync(Quotation entity)` — INSERT INTO `[quotation].[Quotation]` with BusinessId, CustomerId, QuotationStatusTypeId, Reference, ValidUntil, Subtotal, TaxAmount, TotalAmount, Notes, CreatedAtUtc, UpdatedAtUtc
    - Implement `UpdateAsync(Quotation entity)` — UPDATE CustomerId, Reference, ValidUntil, Subtotal, TaxAmount, TotalAmount, Notes, QuotationStatusTypeId, UpdatedAtUtc WHERE Id = @Id
    - Implement `GetNextSequentialNumberAsync(int businessId)` — SELECT ISNULL(MAX([Id]), 0) + 1 FROM `[quotation].[Quotation]` WHERE BusinessId = @BusinessId
    - Use full table names in SQL queries (no aliases), wrap all methods in try/catch with `throw;`
    - Use null-safe SQL parameters using `?? (object)DBNull.Value` for nullable fields (ValidUntil, Notes)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8_

  - [x] 2.2 Create QuotationLineRepository
    - Create `Portal.Infrastructure/Repositories/QuotationLineRepository.cs`
    - Extend `GenericStoredProcedureRepository<QuotationLine>` with constructor accepting `DbContext`
    - Implement `GetByQuotationIdAsync(int quotationId)` — SELECT all columns FROM `[quotation].[QuotationLine]` WHERE QuotationId = @QuotationId ORDER BY SortOrder
    - Implement `GetByIdAsync(int id)` — SELECT single record WHERE Id = @Id
    - Implement `InsertAsync(QuotationLine entity)` — INSERT INTO `[quotation].[QuotationLine]` with QuotationId, Description, Quantity, UnitPrice, VatRate, LineTotal, SortOrder
    - Implement `UpdateAsync(QuotationLine entity)` — UPDATE Description, Quantity, UnitPrice, VatRate, LineTotal, SortOrder WHERE Id = @Id
    - Implement `DeleteAsync(int id)` — DELETE FROM `[quotation].[QuotationLine]` WHERE Id = @Id
    - Implement `DeleteAllByQuotationIdAsync(int quotationId)` — DELETE FROM `[quotation].[QuotationLine]` WHERE QuotationId = @QuotationId
    - Use full table names in SQL queries (no aliases), wrap all methods in try/catch with `throw;`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9_

  - [x] 2.3 Create AuditLogRepository
    - Create `Portal.Infrastructure/Repositories/AuditLogRepository.cs`
    - Extend `GenericStoredProcedureRepository<AuditLog>` with constructor accepting `DbContext`
    - Implement `InsertAsync(AuditLog entity)` — INSERT INTO `[audit].[AuditLog]` with BusinessId, UserId, Action, TableName, RecordId, OldValues, NewValues, Timestamp
    - Use null-safe SQL parameters using `?? (object)DBNull.Value` for nullable fields (BusinessId, UserId, OldValues, NewValues)
    - Wrap in try/catch with `throw;`
    - _Requirements: 9.1, 9.2, 9.4_

  - [ ]* 2.4 Write property tests for repositories
    - **Property 1: Tenant isolation on retrieval**
    - **Property 12: Line item ordering invariant**
    - **Validates: Requirements 1.2, 1.3, 2.2, 7.2, 7.3**

- [x] 3. Create IQuotationService and QuotationService
  - [x] 3.1 Create IQuotationService interface
    - Create `Portal.Infrastructure/Services/IQuotationService.cs`
    - Define methods: `GetQuotationsAsync(int? statusFilter, int? customerFilter, DateTime? dateFrom, DateTime? dateTo)`, `GetQuotationByIdAsync(int id)`, `GetQuotationLinesAsync(int quotationId)`, `CreateQuotationAsync(int customerId, DateOnly? validUntil, string? notes)`, `UpdateQuotationAsync(int quotationId, int customerId, DateOnly? validUntil, string? notes)`, `TransitionStatusAsync(int quotationId, int newStatusId, string userId)`, `AddLineAsync(int quotationId, string description, decimal quantity, decimal unitPrice, decimal vatRate)`, `UpdateLineAsync(int lineId, string description, decimal quantity, decimal unitPrice, decimal vatRate)`, `RemoveLineAsync(int lineId)`, `IsExpired(Quotation quotation)`, `GetValidTransitions()`
    - _Requirements: 3.1_

  - [x] 3.2 Create QuotationService implementation
    - Create `Portal.Infrastructure/Services/QuotationService.cs` implementing `IQuotationService`
    - Inject `QuotationRepository`, `QuotationLineRepository`, `AuditLogRepository`, `CustomerRepository`, and `ICurrentTenantService`
    - Define static `ValidTransitions` dictionary: `{1: [2,5], 2: [3,5], 3: [4,5]}`
    - `GetQuotationsAsync` — retrieve all quotations for current tenant's BusinessId, apply status/customer/date filters in-memory, map to `QuotationListDto` with CustomerName, StatusName, IsExpired
    - `GetQuotationByIdAsync` — retrieve by Id and current tenant's BusinessId, return null if not found
    - `GetQuotationLinesAsync` — retrieve all lines for a quotation ordered by SortOrder
    - `CreateQuotationAsync` — validate CustomerId belongs to current tenant (throw ArgumentException if not), set BusinessId from ICurrentTenantService, set QuotationStatusTypeId = 1 (Draft), generate Reference as `QUO-{BusinessId}-{sequential:D5}`, set Subtotal/TaxAmount/TotalAmount = 0, set CreatedAtUtc and UpdatedAtUtc to DateTime.UtcNow, call repository InsertAsync
    - `UpdateQuotationAsync` — validate quotation exists and is in Draft status (throw InvalidOperationException if not), validate CustomerId belongs to current tenant, set UpdatedAtUtc, call repository UpdateAsync
    - `TransitionStatusAsync` — validate quotation exists, validate transition is in ValidTransitions (throw InvalidOperationException if not), validate at least one line when transitioning to Sent (status 2), update QuotationStatusTypeId and UpdatedAtUtc, insert AuditLog record with BusinessId, UserId, Action="StatusTransition", TableName="quotation.Quotation", RecordId=quotation Id, OldValues=previous status name, NewValues=new status name, Timestamp=UTC now
    - `AddLineAsync` — validate quotation is in Draft status, validate Description (non-whitespace), Quantity > 0, UnitPrice >= 0, VatRate in [0,100], compute LineTotal = Math.Round(Quantity * UnitPrice, 2), assign next SortOrder, insert line, recalculate quotation totals (Subtotal, TaxAmount, TotalAmount), update quotation
    - `UpdateLineAsync` — validate quotation is in Draft status, validate inputs, recompute LineTotal, update line, recalculate quotation totals, update quotation
    - `RemoveLineAsync` — validate quotation is in Draft status, delete line, recalculate quotation totals, update quotation
    - `IsExpired` — return true if ValidUntil is not null and ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow)
    - `GetValidTransitions` — return the ValidTransitions dictionary
    - Pricing recalculation: Subtotal = SUM(LineTotal), TaxAmount = Math.Round(SUM(LineTotal * VatRate / 100), 2), TotalAmount = Subtotal + TaxAmount
    - Throw `ArgumentException` for validation failures, `InvalidOperationException` for lifecycle violations
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.2, 7.3, 8.1, 8.2, 8.4, 9.1, 9.2, 9.3, 9.4, 14.1, 14.2, 14.3, 14.4, 14.5_

  - [ ]* 3.3 Write property tests for QuotationService
    - **Property 2: Quotation creation invariants**
    - **Property 3: Quotation update round-trip**
    - **Property 4: Lifecycle transition correctness**
    - **Property 5: Draft-only editing**
    - **Property 6: Pricing computation invariant**
    - **Property 7: Line item and customer validation**
    - **Property 8: Expiry computation**
    - **Property 9: Audit log correctness for status transitions**
    - **Property 10: Filter correctness**
    - **Validates: Requirements 3.4, 3.5, 3.6, 3.7, 3.8, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.1, 5.2, 5.3, 5.5, 5.6, 5.7, 6.1, 6.2, 6.3, 6.4, 8.2, 8.4, 9.1, 9.2, 9.4, 14.1, 14.2, 14.3, 14.4, 14.5**

- [x] 4. Register services in DI container
  - [x] 4.1 Add QuotationRepository, QuotationLineRepository, AuditLogRepository, and IQuotationService registrations to Program.cs
    - Add `builder.Services.AddScoped<QuotationRepository>();`
    - Add `builder.Services.AddScoped<QuotationLineRepository>();`
    - Add `builder.Services.AddScoped<AuditLogRepository>();`
    - Add `builder.Services.AddScoped<IQuotationService, QuotationService>();`
    - Add required `using` statements for Portal.Infrastructure.Repositories and Portal.Infrastructure.Services
    - _Requirements: 3.2_

- [x] 5. Checkpoint - Verify compilation
  - Ensure the solution compiles with `dotnet build`. Ask the user if questions arise.

- [x] 6. Create view models
  - [x] 6.1 Create QuotationListDto
    - Create `Portal.Web/Models/QuotationListDto.cs`
    - Properties: Id (int), Reference (string), CustomerName (string), StatusName (string), QuotationStatusTypeId (int), TotalAmount (decimal), ValidUntil (DateOnly?), CreatedAtUtc (DateTime), IsExpired (bool)
    - _Requirements: 11.2_

  - [x] 6.2 Create QuotationListViewModel
    - Create `Portal.Web/Models/QuotationListViewModel.cs`
    - Properties: `List<QuotationListDto> Quotations`, `int? StatusFilter`, `int? CustomerFilter`, `DateTime? DateFrom`, `DateTime? DateTo`, `List<Customer> Customers`, `List<QuotationStatusType> Statuses`
    - _Requirements: 11.3, 14.1, 14.2, 14.3_

  - [x] 6.3 Create QuotationCreateViewModel
    - Create `Portal.Web/Models/QuotationCreateViewModel.cs`
    - Properties: CustomerId (Required), ValidUntil (DateOnly?), Notes (MaxLength 4000), `List<Customer> Customers`
    - Use data annotation attributes for validation
    - _Requirements: 12.1, 12.5_

  - [x] 6.4 Create QuotationEditViewModel
    - Create `Portal.Web/Models/QuotationEditViewModel.cs`
    - Properties: Id (int), Reference (string), CustomerId (Required), ValidUntil (DateOnly?), Notes (MaxLength 4000), `List<QuotationLine> Lines`, Subtotal (decimal), TaxAmount (decimal), TotalAmount (decimal), `List<Customer> Customers`
    - _Requirements: 12.1, 12.2, 12.4, 12.7_

  - [x] 6.5 Create QuotationDetailViewModel
    - Create `Portal.Web/Models/QuotationDetailViewModel.cs`
    - Properties: Quotation (Quotation), `List<QuotationLine> Lines`, CustomerName (string), StatusName (string), IsExpired (bool), `List<int> AvailableTransitions`
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

  - [x] 6.6 Create QuotationLineFormViewModel
    - Create `Portal.Web/Models/QuotationLineFormViewModel.cs`
    - Properties: Description (Required, MaxLength 500), Quantity (Required, Range 0.0001 to double.MaxValue), UnitPrice (Required, Range 0 to double.MaxValue), VatRate (Required, Range 0 to 100)
    - Use data annotation attributes for validation
    - _Requirements: 12.3_

- [x] 7. Create QuotationController
  - [x] 7.1 Create QuotationController with all actions
    - Create `Portal.Web/Controllers/QuotationController.cs`
    - Apply `[Authorize]` attribute on the controller
    - Inject `IQuotationService` and `ICustomerService` via constructor
    - `[HttpGet] Index(int? status, int? customer, DateTime? dateFrom, DateTime? dateTo)` — call `GetQuotationsAsync` with filters, populate `QuotationListViewModel` with quotations, customers list, and statuses list, return View
    - `[HttpGet] Create()` — populate `QuotationCreateViewModel` with customers list, return View
    - `[HttpPost][ValidateAntiForgeryToken] Create(QuotationCreateViewModel model)` — check ModelState, call `CreateQuotationAsync`, catch ArgumentException and add to ModelState, redirect to Edit on success
    - `[HttpGet] Edit(int id)` — call `GetQuotationByIdAsync`, return NotFound if null, verify Draft status (redirect to Detail if not Draft), populate `QuotationEditViewModel` with lines and customers, return View
    - `[HttpPost][ValidateAntiForgeryToken] Edit(int id, QuotationEditViewModel model)` — check ModelState, call `UpdateQuotationAsync`, catch ArgumentException/InvalidOperationException, redirect to Detail on success
    - `[HttpGet] Detail(int id)` — call `GetQuotationByIdAsync`, return NotFound if null, populate `QuotationDetailViewModel` with lines, customer name, status name, available transitions, return View
    - `[HttpPost][ValidateAntiForgeryToken] TransitionStatus(int id, int newStatusId)` — get UserId from User.Claims, call `TransitionStatusAsync`, catch InvalidOperationException, redirect to Detail
    - `[HttpPost][ValidateAntiForgeryToken] AddLine(int quotationId, QuotationLineFormViewModel model)` — check ModelState, call `AddLineAsync`, catch ArgumentException/InvalidOperationException, redirect to Edit
    - `[HttpPost][ValidateAntiForgeryToken] UpdateLine(int quotationId, int lineId, QuotationLineFormViewModel model)` — check ModelState, call `UpdateLineAsync`, catch exceptions, redirect to Edit
    - `[HttpPost][ValidateAntiForgeryToken] RemoveLine(int quotationId, int lineId)` — call `RemoveLineAsync`, catch InvalidOperationException, redirect to Edit
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.10, 7.4_

  - [ ]* 7.2 Write property tests for QuotationController
    - **Property 11: Validation failure preserves form state**
    - **Property 1: Tenant isolation on retrieval**
    - **Validates: Requirements 10.7, 10.8, 7.4**

- [x] 8. Checkpoint - Verify controller compilation
  - Ensure the solution compiles with `dotnet build`. Ask the user if questions arise.

- [x] 9. Create Quotation views
  - [x] 9.1 Create Quotation Index view
    - Create `Portal.Web/Views/Quotation/Index.cshtml`
    - Model: `QuotationListViewModel`
    - Display filter controls for status (dropdown), customer (dropdown), and date range (date inputs) above the quotation list
    - Display quotations in a table layout following MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body, cards with 20-30px border radius)
    - Show Reference, Customer name, Status (colour-coded badge), TotalAmount, ValidUntil, and CreatedAtUtc for each quotation
    - Visually indicate expired quotations (where ValidUntil < today) with muted/warning styling
    - Provide Detail and Edit action links per quotation
    - Provide "Create New Quotation" button
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9_

  - [x] 9.2 Create Quotation Create view
    - Create `Portal.Web/Views/Quotation/Create.cshtml`
    - Model: `QuotationCreateViewModel`
    - Display input fields for: Customer (dropdown, required), ValidUntil (date picker), Notes (textarea)
    - Mark Customer field as required with visual indicator (asterisk)
    - Display validation error messages adjacent to relevant fields using `asp-validation-for`
    - Include anti-forgery token via `asp-antiforgery`
    - Follow MyChair Design System styling (input fields, buttons, spacing, typography)
    - _Requirements: 12.1, 12.5, 12.6, 12.9_

  - [x] 9.3 Create Quotation Edit view
    - Create `Portal.Web/Views/Quotation/Edit.cshtml`
    - Model: `QuotationEditViewModel`
    - Pre-populate all fields with existing quotation values (Customer dropdown, ValidUntil, Notes)
    - Display dynamic line items section with table showing Description, Quantity, UnitPrice, VatRate, LineTotal per line
    - Provide Add Line form with fields for Description, Quantity, UnitPrice, VatRate
    - Provide Update and Remove actions per existing line item
    - Display computed Subtotal, TaxAmount, and TotalAmount
    - Display validation error messages for line item operations
    - Include anti-forgery tokens on all forms
    - Follow MyChair Design System styling
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.6, 12.7, 12.8, 12.9_

  - [x] 9.4 Create Quotation Detail view
    - Create `Portal.Web/Views/Quotation/Detail.cshtml`
    - Model: `QuotationDetailViewModel`
    - Display quotation Reference, Customer name, Status (badge), ValidUntil, Notes, and CreatedAtUtc
    - Display all line items in a table with Description, Quantity, UnitPrice, VatRate, and LineTotal columns
    - Display computed Subtotal, TaxAmount, and TotalAmount
    - Display action buttons for valid status transitions based on current status (Send, Accept, Convert, Archive)
    - Show Edit button when quotation is in Draft status
    - Visually indicate if the quotation has expired
    - Include anti-forgery tokens on transition forms
    - Follow MyChair Design System styling
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7, 13.8, 13.9, 13.10_

- [x] 10. Final checkpoint - Ensure full compilation
  - Ensure the solution compiles with `dotnet build` and all views render without errors. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design uses C# / ASP.NET Core MVC 8 — all code examples use this stack
- Repository pattern follows the established GenericStoredProcedureRepository convention with try/catch rethrow
- All SQL uses full table names with no aliases
- Line item management uses server-side form posts (no JavaScript frameworks)
- Only Draft quotations can be edited — all other statuses are read-only
- Pricing is always computed (never manually set): LineTotal, Subtotal, TaxAmount, TotalAmount
