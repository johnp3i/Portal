# Implementation Plan: Invoicing Module

## Overview

This plan implements the Invoicing module — quotation-to-invoice conversion, standalone invoice creation, section-based presentation, lifecycle management, and audit logging. Tasks follow the existing ASP.NET Core MVC 8 + SQL Server + Database-First patterns using raw SQL repositories, matching the Controller → Service → Repository architecture.

## Tasks

- [x] 1. Database schema migrations
  - [x] 1.1 Create migration 036: Add IsGrandTotalShown to Invoice
    - Create `Portal.Database/Migrations/036_AddIsGrandTotalShownToInvoice.sql`
    - ALTER TABLE [invoice].[Invoice] ADD [IsGrandTotalShown] BIT NOT NULL DEFAULT (1)
    - Use idempotent IF NOT EXISTS pattern matching existing migrations
    - _Requirements: 12.3_

  - [x] 1.2 Create migration 037: Extend InvoiceLine with new columns
    - Create `Portal.Database/Migrations/037_ExtendInvoiceLine.sql`
    - Add columns: VatRate DECIMAL(18,2), Discount DECIMAL(18,2), DiscountType NVARCHAR(20), CostPrice DECIMAL(18,2) NULL, ReferenceUrl NVARCHAR(2048) NULL, Subtitle NVARCHAR(500) NULL, InvoiceSectionId INT NULL
    - Use idempotent IF NOT EXISTS pattern for each column
    - _Requirements: 12.2_

  - [x] 1.3 Create migration 038: Create InvoiceSection table and FK
    - Create `Portal.Database/Migrations/038_CreateInvoiceSectionTable.sql`
    - CREATE TABLE [invoice].[InvoiceSection] with Id, InvoiceId, Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, IsTotalsTableShown
    - Add PK, FK to [invoice].[Invoice] with CASCADE delete
    - Add FK from [invoice].[InvoiceLine].[InvoiceSectionId] to [invoice].[InvoiceSection].[Id]
    - Use idempotent IF NOT EXISTS pattern
    - _Requirements: 12.1, 12.2_

- [x] 2. Entity and model updates
  - [x] 2.1 Update Invoice entity with IsGrandTotalShown property
    - Add `public bool IsGrandTotalShown { get; set; } = true;` to `Portal.Infrastructure/Entities/Invoice.cs`
    - Add navigation property `public ICollection<InvoiceSection> InvoiceSections { get; set; } = new List<InvoiceSection>();`
    - _Requirements: 12.3_

  - [x] 2.2 Update InvoiceLine entity with extended properties
    - Add VatRate, Discount, DiscountType, CostPrice, ReferenceUrl, Subtitle, InvoiceSectionId properties to `Portal.Infrastructure/Entities/InvoiceLine.cs`
    - Add navigation property `public InvoiceSection? InvoiceSection { get; set; }`
    - _Requirements: 12.2_

  - [x] 2.3 Create InvoiceSection entity
    - Create `Portal.Infrastructure/Entities/InvoiceSection.cs` with Id, InvoiceId, Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, IsTotalsTableShown
    - Add navigation properties: Invoice, ICollection<InvoiceLine> InvoiceLines
    - _Requirements: 12.1_

  - [x] 2.4 Create DTOs
    - Create `Portal.Infrastructure/Models/InvoiceListDto.cs` with Id, InvoiceNumber, CustomerName, InvoiceDate, DueDate, TotalAmount, StatusName, FinancialStatusName, InvoiceStatusTypeId, InvoiceFinancialStatusTypeId
    - Create `Portal.Infrastructure/Models/CreateInvoiceLineDto.cs` with Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, ReferenceUrl, Subtitle, SectionIndex
    - Create `Portal.Infrastructure/Models/CreateInvoiceSectionDto.cs` with Name, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, IsTotalsTableShown
    - _Requirements: 7.3, 3.5, 3.6_

- [x] 3. Repository layer
  - [x] 3.1 Create InvoiceRepository
    - Create `Portal.Infrastructure/Repositories/InvoiceRepository.cs` extending GenericStoredProcedureRepository<Invoice>
    - Implement: GetAllByBusinessIdAsync(int businessId) — SELECT with JOIN to Customer, InvoiceStatusType, InvoiceFinancialStatusType, ordered by InvoiceDate DESC
    - Implement: GetByIdAndBusinessIdAsync(int id, int businessId) — single invoice lookup with tenant filter
    - Implement: InsertAsync(Invoice entity) — INSERT returning new Id
    - Implement: UpdateAsync(Invoice entity) — UPDATE status and timestamps
    - Implement: GetNextSequentialNumberAsync(int businessId) — SELECT MAX pattern for invoice number generation
    - Implement: GetByQuotationIdAsync(int quotationId) — check for existing conversion
    - Use full table names in SQL, parameterized queries, null-safe SqlParameter patterns
    - _Requirements: 4.1, 4.2, 6.1, 7.1_

  - [x] 3.2 Create InvoiceLineRepository
    - Create `Portal.Infrastructure/Repositories/InvoiceLineRepository.cs` extending GenericStoredProcedureRepository<InvoiceLine>
    - Implement: GetByInvoiceIdAsync(int invoiceId) — SELECT all lines for an invoice ordered by SortOrder
    - Implement: InsertAsync(InvoiceLine entity) — INSERT single line returning Id
    - Implement: BulkInsertAsync(List<InvoiceLine> lines) — INSERT multiple lines (for conversion)
    - Implement: UpdateAsync(InvoiceLine entity) — UPDATE line fields
    - Implement: DeleteAsync(int id) — DELETE single line
    - Implement: GetByIdAsync(int id) — single line lookup
    - Implement: UpdateSectionIdAsync(int lineId, int? sectionId) — update InvoiceSectionId
    - Implement: UpdateSortOrdersAsync(List<(int Id, int SortOrder)> updates) — bulk SortOrder update
    - _Requirements: 1.3, 3.5, 12.2, 12.8_

  - [x] 3.3 Create InvoiceSectionRepository
    - Create `Portal.Infrastructure/Repositories/InvoiceSectionRepository.cs` extending GenericStoredProcedureRepository<InvoiceSection>
    - Implement: GetByInvoiceIdAsync(int invoiceId) — SELECT all sections ordered by SortOrder
    - Implement: InsertAsync(InvoiceSection entity) — INSERT returning Id
    - Implement: BulkInsertAsync(List<InvoiceSection> sections) — INSERT multiple sections (for conversion)
    - Implement: UpdateAsync(InvoiceSection entity) — UPDATE section fields
    - Implement: DeleteAsync(int id) — DELETE single section
    - Implement: GetByIdAsync(int id) — single section lookup
    - Implement: UpdateSortOrdersAsync(List<(int Id, int SortOrder)> updates) — bulk SortOrder update
    - _Requirements: 12.1, 12.7_

- [x] 4. Checkpoint - Ensure schema and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer — InvoiceService
  - [x] 5.1 Create IInvoiceService interface
    - Create `Portal.Infrastructure/Services/IInvoiceService.cs`
    - Define: ConvertFromQuotationAsync, CreateInvoiceAsync, GetInvoicesAsync, GetInvoiceByIdAsync, GetInvoiceLinesAsync, TransitionStatusAsync, AddLineAsync, UpdateLineAsync, RemoveLineAsync
    - _Requirements: 1.1, 3.1, 5.1, 7.1, 10.1–10.5_

  - [x] 5.2 Implement InvoiceService — conversion logic
    - Create `Portal.Infrastructure/Services/InvoiceService.cs`
    - Inject: ICurrentTenantService, InvoiceRepository, InvoiceLineRepository, InvoiceSectionRepository, QuotationRepository, QuotationLineRepository, ProposalSectionRepository, CustomerRepository, AuditLogRepository, PortalDbContext (for transactions)
    - ConvertFromQuotationAsync: validate quotation status = 3 (Accepted), validate has lines, check no existing invoice for quotationId, begin transaction, generate invoice number, insert invoice, copy ProposalSections → InvoiceSections, copy QuotationLines → InvoiceLines with section mapping, update quotation status to 4 (Converted), compute totals, write audit logs, commit transaction
    - Throw InvalidOperationException for precondition failures (wrong status, no lines, duplicate conversion)
    - _Requirements: 1.1–1.12, 2.1–2.3_

  - [x] 5.3 Implement InvoiceService — standalone creation and queries
    - CreateInvoiceAsync: validate required fields (customerId, dates, lines), verify customer belongs to business, generate invoice number, insert invoice with sections and lines, compute totals, write audit log
    - GetInvoicesAsync: delegate to repository with optional filters, return InvoiceListDto list
    - GetInvoiceByIdAsync: delegate to repository with tenant filter
    - GetInvoiceLinesAsync: delegate to InvoiceLineRepository
    - _Requirements: 3.1–3.7, 6.1–6.3, 7.1–7.4_

  - [x] 5.4 Implement InvoiceService — lifecycle and line management
    - TransitionStatusAsync: validate transition against valid transitions map {(1→2), (1→3), (2→3)}, update status, update UpdatedAtUtc, write audit log
    - AddLineAsync: validate invoice is Draft, insert line, recompute totals
    - UpdateLineAsync: validate invoice is Draft, update line, recompute totals
    - RemoveLineAsync: validate invoice is Draft, delete line, recompute totals
    - _Requirements: 5.1–5.5, 10.5, 10.6_

  - [ ]* 5.5 Write property tests for invoice conversion
    - **Property 1: Conversion data fidelity** — All quotation line and section fields are identically copied to invoice
    - **Validates: Requirements 1.3, 1.6, 1.7, 1.8, 1.9**

  - [ ]* 5.6 Write property tests for conversion preconditions
    - **Property 2: Conversion transitions quotation to Converted** — Source quotation status becomes 4 after conversion
    - **Property 5: Conversion precondition enforcement** — Non-accepted quotations are rejected
    - **Property 6: Conversion idempotency** — Duplicate conversion attempts are rejected
    - **Validates: Requirements 1.2, 1.10, 2.2**

  - [ ]* 5.7 Write property tests for invoice totals
    - **Property 3: Invoice totals computation invariant** — Subtotal = sum of LineTotals, TaxAmount = sum of (LineTotal × VatRate / 100), TotalAmount = Subtotal + TaxAmount
    - **Validates: Requirements 1.4, 3.2**

  - [ ]* 5.8 Write property tests for invoice number generation
    - **Property 9: Invoice number sequential uniqueness** — Each invoice gets a unique, sequentially incrementing number within BusinessId
    - **Property 10: Invoice number format constraint** — Format matches INV-{BusinessId}-{SequentialNumber:D5}, max 50 chars
    - **Validates: Requirements 4.1, 4.2, 4.4**

  - [ ]* 5.9 Write property tests for status machine
    - **Property 11: Status machine correctness** — Transitions succeed only for valid pairs {(1→2), (1→3), (2→3)}, invalid transitions are rejected
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5**

  - [ ]* 5.10 Write property tests for initial state and standalone
    - **Property 4: New invoice initial state** — All new invoices start as Draft (1) and Unpaid (1)
    - **Property 7: Standalone invoice has null QuotationId** — Standalone invoices have QuotationId = NULL
    - **Property 8: Standalone creation validation** — Missing required fields are rejected
    - **Validates: Requirements 1.5, 3.1, 3.3, 3.4**

  - [ ]* 5.11 Write property tests for tenant isolation
    - **Property 12: Tenant isolation** — Queries with wrong BusinessId return no results; cross-tenant customer assignment is rejected
    - **Validates: Requirements 6.1, 6.2, 6.3**

- [x] 6. Service layer — InvoiceSectionService
  - [x] 6.1 Create IInvoiceSectionService interface
    - Create `Portal.Infrastructure/Services/IInvoiceSectionService.cs`
    - Define: GetByInvoiceIdAsync, AddSectionAsync, RemoveSectionAsync, ReorderSectionsAsync, MoveLineToSectionAsync, ReorderLinesAsync, UpdateSectionAsync
    - _Requirements: 12.7, 12.8_

  - [x] 6.2 Implement InvoiceSectionService
    - Create `Portal.Infrastructure/Services/InvoiceSectionService.cs`
    - Inject: InvoiceSectionRepository, InvoiceLineRepository
    - GetByInvoiceIdAsync: delegate to repository
    - AddSectionAsync: validate non-empty name, validate SectionType ∈ {LineItems, Narrative}, compute next SortOrder, insert via repository
    - RemoveSectionAsync: set InvoiceSectionId = NULL on all lines in section, then delete section
    - ReorderSectionsAsync: bulk update SortOrder based on ordered list of section IDs
    - MoveLineToSectionAsync: update InvoiceLine.InvoiceSectionId to target section (or NULL)
    - ReorderLinesAsync: bulk update SortOrder based on ordered list of line IDs
    - UpdateSectionAsync: update section fields
    - _Requirements: 12.7, 12.8_

  - [ ]* 6.3 Write property tests for InvoiceSectionService
    - **Property 19: Section CRUD round-trip** — Adding a section and retrieving returns identical field values
    - **Property 20: Line section movement** — Moving a line updates InvoiceSectionId correctly
    - **Property 21: Line grouping by section** — Lines group correctly by InvoiceSectionId
    - **Validates: Requirements 12.7, 12.8, 8.2**

- [x] 7. Checkpoint - Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Audit logging integration
  - [x] 8.1 Implement audit log entries in InvoiceService
    - Invoice creation (conversion or standalone): Action = "Created", TableName = "Invoice", RecordId = new Invoice Id
    - Status transition: Action = "StatusChanged", TableName = "Invoice", OldValues = previous status, NewValues = new status
    - Quotation conversion: Action = "Converted", TableName = "Quotation", RecordId = Quotation Id, NewValues = Invoice Id reference
    - Populate BusinessId and UserId from ICurrentTenantService context
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [ ]* 8.2 Write property tests for audit logging
    - **Property 16: Audit logging for invoice creation** — AuditLog entry exists with correct Action, TableName, RecordId
    - **Property 17: Audit logging for status transition** — AuditLog entry exists with correct OldValues/NewValues
    - **Property 18: Audit logging for conversion** — AuditLog entry exists for quotation conversion
    - **Validates: Requirements 11.1, 11.2, 11.3, 11.4**

- [x] 9. InvoiceController — core actions
  - [x] 9.1 Create InvoiceController with list and detail actions
    - Create `Portal.Web/Controllers/InvoiceController.cs` with [Authorize] and [ModuleAccess(PortalModules.Invoice)]
    - Inject: IInvoiceService, IInvoiceSectionService, ICurrentTenantService
    - GET /Invoice (Index) — list invoices with optional status, financial status, customer filters
    - GET /Invoice/Detail/{id} — invoice detail with lines grouped by section
    - _Requirements: 7.1–7.4, 8.1–8.7, 10.1, 10.2_

  - [x] 9.2 Add standalone creation and conversion actions
    - POST /Invoice/Create — standalone invoice creation with lines and sections
    - POST /Invoice/ConvertFromQuotation — quotation-to-invoice conversion, redirect to detail on success
    - Handle ArgumentException and InvalidOperationException with ModelState errors or TempData["Error"]
    - _Requirements: 3.1, 9.3, 10.3, 10.4_

  - [x] 9.3 Add status transition and line/section management actions
    - POST /Invoice/TransitionStatus — status lifecycle transition (Issue, Cancel)
    - POST /Invoice/AddLine, UpdateLine, RemoveLine — line CRUD
    - POST /Invoice/AddSection, UpdateSection, RemoveSection, ReorderSections — section CRUD
    - POST /Invoice/MoveLineToSection, ReorderLines — line movement
    - Return JSON responses for AJAX requests, redirect for form posts
    - _Requirements: 5.1–5.5, 10.5, 10.6, 12.7, 12.8_

  - [ ]* 9.4 Write property test for controller validation
    - **Property 22: Controller validation error responses** — Invalid input returns validation errors with no state change
    - **Validates: Requirements 10.6**

- [x] 10. Convert-to-Invoice button on QuotationController
  - [x] 10.1 Add ConvertToInvoice action to QuotationController
    - Add POST action that accepts quotationId, calls IInvoiceService.ConvertFromQuotationAsync
    - On success: redirect to /Invoice/Detail/{newInvoiceId}
    - On failure: set TempData["Error"] with exception message, redirect back to Quotation Detail
    - _Requirements: 9.3, 9.4_

  - [x] 10.2 Update Quotation Detail view with Convert to Invoice button
    - Show "Convert to Invoice" button only when QuotationStatusTypeId = 3 (Accepted)
    - Hide button for all other statuses
    - Wire button to POST /Quotation/ConvertToInvoice form
    - _Requirements: 9.1, 9.2_

- [x] 11. Invoice views
  - [x] 11.1 Create Invoice Index view
    - Create `Portal.Web/Views/Invoice/Index.cshtml`
    - Display filterable list: InvoiceNumber, CustomerName, InvoiceDate, DueDate, TotalAmount, Status, FinancialStatus
    - Filter controls for status, financial status, customer
    - Link each row to Detail view
    - _Requirements: 7.1–7.4_

  - [x] 11.2 Create Invoice Detail view
    - Create `Portal.Web/Views/Invoice/Detail.cshtml`
    - Display header: InvoiceNumber, Customer, InvoiceDate, DueDate, Status, FinancialStatus, Notes
    - Display lines grouped by InvoiceSection with Description, Subtitle, Quantity, UnitPrice, VatRate, Discount, DiscountType, LineTotal
    - Display computed Subtotal, TaxAmount, TotalAmount
    - Show link to source Quotation when QuotationId is not null
    - Show status transition buttons (Issue, Cancel) when in Draft
    - Render sections with ColumnConfiguration, emphasis, per-section totals when IsTotalsTableShown enabled
    - Show grand total summary card when IsGrandTotalShown enabled
    - _Requirements: 8.1–8.7_

  - [x] 11.3 Create Invoice Create view
    - Create `Portal.Web/Views/Invoice/Create.cshtml`
    - Form for standalone invoice: customer selection, dates, notes, IsGrandTotalShown toggle
    - Dynamic line items with Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, ReferenceUrl, Subtitle
    - Optional section creation
    - _Requirements: 3.1, 3.5, 3.6, 3.7_

- [x] 12. Dependency injection registration
  - [x] 12.1 Register new services and repositories in DI container
    - Register InvoiceRepository, InvoiceLineRepository, InvoiceSectionRepository
    - Register IInvoiceService → InvoiceService
    - Register IInvoiceSectionService → InvoiceSectionService
    - _Requirements: 1.1, 3.1, 10.1_

- [x] 13. Invoice list filtering and ordering
  - [ ]* 13.1 Write property tests for list filtering and ordering
    - **Property 13: Invoice list ordering** — Results ordered by InvoiceDate descending
    - **Property 14: Invoice list filtering** — Applied filters return only matching invoices
    - **Property 15: Per-section totals computation** — Section totals equal sum of line values
    - **Validates: Requirements 7.1, 7.4, 8.6, 12.5**

- [x] 14. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit as specified in the design document (minimum 100 iterations per property)
- All repositories follow the GenericStoredProcedureRepository pattern with raw SQL, full table names, and null-safe SqlParameter usage
- Conversion transaction wraps all steps in a single IDbContextTransaction — any failure rolls back completely
- The filtered unique index on Invoice.QuotationId provides database-level idempotency for conversion
