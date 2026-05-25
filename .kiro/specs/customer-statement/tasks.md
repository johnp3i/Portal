# Implementation Plan: Customer Statement of Account

## Overview

This plan implements the Customer Statement of Account module following the established Controller → Service → Repository architecture. Tasks are ordered to build foundational data models first, then repository queries, service computation logic, controller endpoints, UI rendering, PDF export, email functionality, customer registry pagination, and finally audit logging and tests. Each task builds incrementally on the previous steps.

## Tasks

- [x] 1. Create data models and DTOs
  - [x] 1.1 Create StatementLineType enum and statement DTOs
    - Create `Portal.Infrastructure/Models/StatementLineType.cs` with enum values: Opening, Invoice, Payment, Closing
    - Create `Portal.Infrastructure/Models/StatementLineDto.cs` with fields: Date (DateOnly), Type (StatementLineType), Reference (string), Description (string), Debit (decimal), Credit (decimal), RunningBalance (decimal)
    - Create `Portal.Infrastructure/Models/StatementResultDto.cs` with fields: OpeningBalance, ClosingBalance, TotalInvoiced, TotalPaid, InvoiceCount, PaymentCount, Lines (List<StatementLineDto>)
    - Create `Portal.Infrastructure/Models/StatementInvoiceDto.cs` with fields: Id, InvoiceDate (DateOnly), InvoiceNumber, Notes, TotalAmount
    - Create `Portal.Infrastructure/Models/StatementPaymentDto.cs` with fields: Id, PaymentDateUtc (DateTime), Amount, Reference, Notes, PaymentMethodName
    - _Requirements: 2.1_

  - [x] 1.2 Create StatementPdfModel and PagedResult models
    - Create `Portal.Infrastructure/Models/StatementPdfModel.cs` with fields: CustomerName, CustomerAddress, CustomerEmail, CustomerPhone, BusinessName, BusinessLogoUrl, CurrencySymbol, FromDate (DateOnly), ToDate (DateOnly), Statement (StatementResultDto)
    - Create `Portal.Infrastructure/Models/PagedResult.cs` as generic class with fields: Items (List<T>), CurrentPage, TotalPages, TotalCount, PageSize, HasPreviousPage (computed), HasNextPage (computed)
    - _Requirements: 5.1, 9.1_

- [x] 2. Implement StatementRepository with SQL queries
  - [x] 2.1 Create StatementRepository with opening balance queries
    - Create `Portal.Infrastructure/Repositories/StatementRepository.cs` extending `GenericStoredProcedureRepository<Invoice>`
    - Implement `GetInvoicedTotalBeforeDateAsync(int customerId, int businessId, DateOnly beforeDate)` using `ExecuteSqlRawAsync` to sum Invoice.TotalAmount where InvoiceStatusTypeId = 2, IsDeleted = 0, InvoiceDate < beforeDate, scoped to BusinessId
    - Implement `GetPaidTotalBeforeDateAsync(int customerId, int businessId, DateOnly beforeDate)` joining Payment to Invoice, summing Payment.Amount where IsVoided = 0, PaymentDateUtc < beforeDate, scoped to BusinessId
    - Use full table names in SQL (no aliases), parameterized queries with SqlParameter, null-safe parameters
    - _Requirements: 1.1, 10.1, 10.2_

  - [x] 2.2 Create StatementRepository in-period query methods
    - Implement `GetInvoicesInPeriodAsync(int customerId, int businessId, DateOnly fromDate, DateOnly toDate)` returning `List<StatementInvoiceDto>` — select Id, InvoiceDate, InvoiceNumber, Notes, TotalAmount where InvoiceStatusTypeId = 2, IsDeleted = 0, InvoiceDate within range, ordered by InvoiceDate
    - Implement `GetPaymentsInPeriodAsync(int customerId, int businessId, DateOnly fromDate, DateOnly toDate)` returning `List<StatementPaymentDto>` — join Payment → Invoice → PaymentMethodType, select Id, PaymentDateUtc, Amount, Reference, Notes, PaymentMethodType.Name where IsVoided = 0, PaymentDateUtc within range, ordered by PaymentDateUtc
    - Register StatementRepository in DI container
    - _Requirements: 1.2, 1.3, 10.1, 10.2_

- [x] 3. Implement StatementService with computation logic
  - [x] 3.1 Create IStatementService interface and StatementService class
    - Create `Portal.Infrastructure/Services/IStatementService.cs` with methods: GenerateStatementAsync, LogPdfDownloadAsync, LogEmailSentAsync
    - Create `Portal.Infrastructure/Services/StatementService.cs` implementing IStatementService
    - Inject StatementRepository, AuditLogRepository, ICurrentTenantService
    - Implement `GenerateStatementAsync`: compute opening balance (invoiced before - paid before), fetch in-period invoices and payments, build StatementLineDto for each invoice (Debit = TotalAmount, Credit = 0) and payment (Debit = 0, Credit = Amount), handle payment Reference null/empty formatting (PaymentMethodName only vs PaymentMethodName + " · Ref: " + Reference), handle null Notes as empty string
    - Sort lines chronologically by Date with invoices before payments on same date
    - Prepend Opening line (Date = fromDate, Type = Opening, Reference = "Balance brought forward", RunningBalance = openingBalance)
    - Compute running balance sequentially: RunningBalance[i] = RunningBalance[i-1] + Debit[i] - Credit[i]
    - Append Closing line (Date = toDate, Type = Closing, Reference = "Balance carried forward", RunningBalance = final balance)
    - Compute TotalInvoiced, TotalPaid, InvoiceCount, PaymentCount
    - Handle empty period case: return statement with only Opening/Closing lines, zero totals
    - Register StatementService in DI container
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 10.5_

  - [ ]* 3.2 Write property test: Opening Balance Computation (Property 1)
    - **Property 1: Opening Balance Computation**
    - Create test class in test project using FsCheck.Xunit
    - Generate arbitrary lists of invoices (with TotalAmount > 0) and payments (with Amount > 0) dated before a given start date
    - Assert OpeningBalance = sum(invoice.TotalAmount) - sum(payment.Amount)
    - Minimum 100 iterations
    - **Validates: Requirements 1.1**

  - [ ]* 3.3 Write property test: Chronological Ordering with Same-Date Tiebreaker (Property 3)
    - **Property 3: Chronological Ordering with Same-Date Tiebreaker**
    - Generate arbitrary interleaved invoices and payments with overlapping dates
    - Assert all lines between Opening and Closing are sorted by Date non-decreasing
    - Assert for same-date lines, all Invoice types appear before Payment types
    - Minimum 100 iterations
    - **Validates: Requirements 1.4, 1.8, 2.8**

  - [ ]* 3.4 Write property test: Running Balance Invariant (Property 4)
    - **Property 4: Running Balance Invariant**
    - Generate arbitrary statement with n transaction lines
    - Assert for each line at index i > 0: RunningBalance[i] = RunningBalance[i-1] + Debit[i] - Credit[i]
    - Assert RunningBalance[0] (Opening line) = computed opening balance
    - Minimum 100 iterations
    - **Validates: Requirements 1.5**

  - [ ]* 3.5 Write property test: Closing Balance Equals Aggregate Formula (Property 5)
    - **Property 5: Closing Balance Equals Aggregate Formula**
    - Generate arbitrary invoices and payments within a period
    - Assert ClosingBalance = OpeningBalance + TotalInvoiced - TotalPaid
    - Minimum 100 iterations
    - **Validates: Requirements 1.6, 1.7**

  - [ ]* 3.6 Write property test: Statement Line Field Mapping (Property 6)
    - **Property 6: Statement Line Field Mapping**
    - Generate arbitrary invoices with various InvoiceNumber, Notes, TotalAmount values
    - Generate arbitrary payments with various PaymentMethodName, Reference (including null/empty), Notes (including null/empty), Amount values
    - Assert invoice lines: Date = InvoiceDate, Reference = InvoiceNumber, Description = Notes ?? "", Debit = TotalAmount, Credit = 0
    - Assert payment lines: Reference = PaymentMethodName + " · Ref: " + Reference (or PaymentMethodName only if Reference is null/empty), Description = Notes ?? "", Debit = 0, Credit = Amount
    - Minimum 100 iterations
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5**

  - [ ]* 3.7 Write property test: Boundary Lines Structure (Property 7)
    - **Property 7: Boundary Lines Structure**
    - Generate arbitrary statement for any customer and period
    - Assert first line: Type = Opening, Date = fromDate, Reference = "Balance brought forward", Debit = 0, Credit = 0, RunningBalance = OpeningBalance
    - Assert last line: Type = Closing, Date = toDate, Reference = "Balance carried forward", Debit = 0, Credit = 0, RunningBalance = ClosingBalance
    - Minimum 100 iterations
    - **Validates: Requirements 2.6, 2.7**

- [x] 4. Checkpoint - Ensure core computation logic compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement StatementController with all actions
  - [x] 5.1 Create StatementController with Index and Generate actions
    - Create `Portal.Web/Controllers/StatementController.cs` with [Authorize] and [ModuleAccess(PortalModules.Revenue)] attributes
    - Inject IStatementService, ICustomerService, ICurrentTenantService
    - Implement `Index(int? customerId)`: populate customer dropdown (all active customers for business, ordered alphabetically), pre-select customer if customerId provided, return View
    - Implement `Generate(int? customerId, string? fromDate, string? toDate)` as POST AJAX endpoint:
      - Validate: customerId required (return error JSON if missing)
      - Validate: both dates required (return error JSON if missing)
      - Parse dates, validate fromDate <= toDate (return error JSON if invalid)
      - Verify customer belongs to current business tenant (return error JSON if not found)
      - Call StatementService.GenerateStatementAsync
      - Return JSON with success flag, opening balance, closing balance, total invoiced, total paid, and transaction lines
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 10.3_

  - [x] 5.2 Add DownloadPdf and EmailStatement actions to StatementController
    - Implement `DownloadPdf(int customerId, string fromDate, string toDate)` as POST:
      - Validate customer belongs to tenant
      - Call StatementService.GenerateStatementAsync
      - Build StatementPdfModel with customer details, business info, currency symbol
      - Call StatementRenderer.RenderAsync to get HTML
      - Convert HTML to PDF (landscape orientation) with 30-second timeout
      - Generate filename: Statement_{SanitizedCustomerName}_{yyyyMMdd}_{yyyyMMdd}.pdf (spaces → underscores, invalid filename chars removed)
      - Return FileResult with content type application/pdf
      - Log PDF download via StatementService.LogPdfDownloadAsync
      - Handle timeout/failure: return error JSON, log failure
    - Implement `EmailStatement(int customerId, string fromDate, string toDate)` as POST:
      - Validate customer belongs to tenant
      - Check customer has email address (return error if not)
      - Generate PDF (same as DownloadPdf flow)
      - Call PortalEmailService.SendStatementEmailAsync with PDF bytes as attachment
      - Log email sent via StatementService.LogEmailSentAsync
      - Return success JSON on success, error JSON on failure
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 7.1, 7.2, 7.3, 7.4_

  - [ ]* 5.3 Write unit tests for StatementController validation
    - Test missing CustomerId returns error JSON with correct message
    - Test missing from-date or to-date returns error JSON
    - Test from-date after to-date returns error JSON
    - Test customer not belonging to tenant returns "Customer not found" error
    - Test customer with no email returns appropriate error on EmailStatement
    - _Requirements: 3.4, 3.5, 3.6, 3.8, 7.2_

- [x] 6. Implement Statement UI (Razor view)
  - [x] 6.1 Create Statement Index Razor view with filter panel and KPI cards
    - Create `Portal.Web/Views/Statement/Index.cshtml`
    - Render topbar with eyebrow label, heading (42px), and muted description
    - Render filter panel in `.glass.card-pad` (margin-bottom:22px) with:
      - Customer dropdown (populated from ViewBag, pre-selected if customerId provided)
      - From-date input (date picker)
      - To-date input (date picker)
      - Generate button (btn-primary)
    - Use flexbox layout with gap:14px, min-width:180px per field, buttons aligned bottom with padding-bottom:2px
    - Render statement results section (initially hidden, shown after Generate):
      - Header section: customer name, address, contact details, statement period dates
      - Four KPI cards: Opening Balance, Total Invoiced (with invoice count), Total Paid (with payment count), Closing Balance
      - Closing Balance styled red if > 0 (outstanding), green if = 0 (settled)
    - _Requirements: 3.1, 3.2, 4.1, 4.2, 4.6, 4.7_

  - [x] 6.2 Create transaction history table and action buttons in Statement view
    - Render transaction history table in `.glass.card-pad` with columns: Date, Type, Reference, Debit (Invoiced), Credit (Paid), Running Balance
    - Apply colour-coded type pills: gold for Invoice, green for Payment, blue for Opening, red for Closing
    - Render period totals footer row with sum of debits, sum of credits, and Closing Balance
    - Add action buttons: Download PDF, Email Statement
    - Implement AJAX Generate call using fetch with BlockUI.show/hide and SweetAlert2 for errors
    - Implement AJAX DownloadPdf call with BlockUI and file download handling
    - Implement AJAX EmailStatement call with BlockUI and SweetAlert2 success/error confirmation
    - Include antiforgery token in all POST requests
    - _Requirements: 4.3, 4.4, 4.5, 4.6, 4.7, 5.1, 7.3, 7.4_

  - [ ]* 6.3 Write property test: PDF Filename Sanitization (Property 8)
    - **Property 8: PDF Filename Sanitization**
    - Generate arbitrary customer names with spaces, special characters, unicode
    - Generate arbitrary valid date ranges
    - Assert filename matches pattern: Statement_{SanitizedName}_{yyyyMMdd}_{yyyyMMdd}.pdf
    - Assert spaces replaced with underscores
    - Assert invalid filename characters removed
    - Minimum 100 iterations
    - **Validates: Requirements 5.2**

- [x] 7. Implement PDF rendering
  - [x] 7.1 Create StatementRenderer for HTML-to-PDF conversion
    - Create `Portal.Web/Services/IStatementRenderer.cs` interface with `Task<string> RenderAsync(StatementPdfModel model)`
    - Create `Portal.Web/Services/StatementRenderer.cs` implementing IStatementRenderer
    - Inject ViewRenderService (existing pattern from InvoiceRenderer)
    - Render the statement Razor partial view (`_StatementPdf.cshtml`) to HTML string
    - Create `Portal.Web/Views/Statement/_StatementPdf.cshtml` partial view with landscape-oriented layout containing: business header/logo, customer details, period dates, KPI summary, full transaction table with opening/closing rows and period totals footer
    - Register IStatementRenderer in DI container
    - _Requirements: 5.1, 5.3_

- [x] 8. Implement Email Statement functionality
  - [x] 8.1 Add SendStatementEmailAsync to PortalEmailService
    - Add method `SendStatementEmailAsync(string recipientEmail, string customerName, byte[] pdfBytes, string filename)` to IPortalEmailService and PortalEmailService
    - Compose email with PDF attachment, appropriate subject line and body
    - Handle SMTP failures gracefully, rethrow for controller to catch and log
    - _Requirements: 7.1, 7.4_

- [x] 9. Checkpoint - Ensure statement generation, PDF, and email compile and work
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Implement Customer Registry pagination
  - [x] 10.1 Add paginated query to CustomerRepository
    - Add method to CustomerRepository: `GetCustomersPagedAsync(string? searchTerm, bool? isActive, int page, int pageSize, int businessId)` returning `PagedResult<Customer>`
    - Implement SQL with OFFSET/FETCH NEXT pagination, search term filtering (Name, ContactPerson, Email LIKE pattern, case-insensitive), IsActive filter, ordered by Name
    - Implement count query for total records matching filters
    - Compute TotalPages = ceil(TotalCount / PageSize)
    - Handle page exceeding total pages: return last available page (or empty if no results)
    - Use full table names, parameterized queries, null-safe parameters
    - _Requirements: 9.1, 9.4, 9.5, 9.8, 10.4_

  - [x] 10.2 Add GetCustomersPagedAsync to CustomerService and update CustomerController
    - Add `GetCustomersPagedAsync` to ICustomerService and CustomerService, delegating to repository
    - Update `CustomerController.Index` to accept `searchTerm`, `isActive`, `page` parameters (page defaults to 1)
    - Call CustomerService.GetCustomersPagedAsync with page size 15
    - Pass PagedResult to view via ViewBag or model
    - Reset to page 1 when filter criteria change
    - _Requirements: 9.1, 9.6, 9.7_

  - [x] 10.3 Update Customer Registry view with pagination controls
    - Add pagination info display: "Showing X-Y of Z" (X = (page-1)*15+1, Y = min(page*15, Z), Z = total count)
    - Add page navigation: numbered page buttons, Previous (disabled on page 1), Next (disabled on last page)
    - Maintain search term and status filter when navigating pages
    - Style pagination per layout standards: margin-top:18px, font-size:14px, page buttons with border-radius:8px
    - _Requirements: 9.2, 9.3, 9.6_

  - [ ]* 10.4 Write property test: Pagination Page Size Invariant (Property 9)
    - **Property 9: Pagination Page Size Invariant**
    - Generate arbitrary lists of customers (varying sizes) and page numbers
    - Assert each page returns at most 15 items
    - Assert non-last pages return exactly 15 items
    - Minimum 100 iterations
    - **Validates: Requirements 9.1**

  - [ ]* 10.5 Write property test: Pagination Info Correctness (Property 10)
    - **Property 10: Pagination Info Correctness**
    - Generate arbitrary total counts Z > 0 and valid page numbers p
    - Assert X = (p-1)*15 + 1, Y = min(p*15, Z)
    - Assert Previous disabled when p = 1
    - Assert Next disabled when p = ceil(Z/15)
    - Minimum 100 iterations
    - **Validates: Requirements 9.2, 9.3**

  - [ ]* 10.6 Write property test: Filter and Pagination Composition (Property 11)
    - **Property 11: Filter and Pagination Composition**
    - Generate arbitrary customer lists with varying Name, ContactPerson, Email, IsActive values
    - Apply arbitrary search term and status filter
    - Assert total count reflects only customers matching all active filters
    - Assert pagination applied to filtered result set
    - Minimum 100 iterations
    - **Validates: Requirements 9.4, 9.5**

- [x] 11. Implement Statement access points and navigation
  - [x] 11.1 Add Statement link to Customer Registry and Revenue Control navigation
    - Add "Statement" link in the Actions column of the Customer Registry table for each customer row, linking to `/Statement?customerId={id}`
    - Add navigation path to Statement page from Revenue Control dashboard (sidebar or dashboard card)
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 12. Implement Audit Logging
  - [x] 12.1 Implement audit log entries in StatementService
    - In `GenerateStatementAsync`: after successful generation, create audit log entry with BusinessId, UserId, CustomerId, Statement_Period (fromDate, toDate), timestamp, action = "StatementGenerated"
    - In `LogPdfDownloadAsync`: create audit log entry with BusinessId, UserId, CustomerId, Statement_Period, action = "StatementPdfDownloaded"
    - In `LogEmailSentAsync`: create audit log entry with BusinessId, UserId, CustomerId, recipientEmail, Statement_Period, action = "StatementEmailed"
    - Use existing AuditLogRepository pattern for inserting audit records
    - _Requirements: 8.1, 8.2, 8.3_

  - [ ]* 12.2 Write integration tests for audit logging
    - Test that generating a statement creates an audit log entry with correct fields
    - Test that downloading PDF creates an audit log entry with download event
    - Test that emailing statement creates an audit log entry with email event and recipient
    - _Requirements: 8.1, 8.2, 8.3_

- [ ] 13. Implement Period Filtering property test
  - [ ]* 13.1 Write property test: Period Filtering Correctness (Property 2)
    - **Property 2: Period Filtering Correctness**
    - Generate arbitrary invoices and payments with dates spanning before, within, and after the period
    - Assert only invoices with InvoiceDate within [fromDate, toDate], InvoiceStatusTypeId = 2, IsDeleted = 0 are included
    - Assert only payments with PaymentDateUtc within [fromDate, toDate], IsVoided = 0 are included
    - Assert all results scoped to the specified BusinessId
    - Minimum 100 iterations
    - **Validates: Requirements 1.2, 1.3, 10.1, 10.2**

- [x] 14. Final checkpoint - Ensure all tests pass (core module)
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Implement Email History
  - [x] 15.1 Create database migration for [customer].[StatementEmailHistory] table
    - Create `Portal.Database/Migrations/057_CreateStatementEmailHistoryTable.sql`
    - Define table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK → [dbo].[Business]), CustomerId (INT NOT NULL FK → [customer].[Customer]), FromDate (DATE NOT NULL), ToDate (DATE NOT NULL), RecipientEmail (NVARCHAR(256) NOT NULL), SentByUserId (NVARCHAR(450) NOT NULL), SentAtUtc (DATETIME NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Add clustered primary key [PK_StatementEmailHistory] on [Id]
    - Add foreign key [FK_StatementEmailHistory_BusinessId] referencing [dbo].[Business]([Id])
    - Add foreign key [FK_StatementEmailHistory_CustomerId] referencing [customer].[Customer]([Id])
    - Add nonclustered index [IX_StatementEmailHistory_CustomerId_BusinessId] on ([CustomerId], [BusinessId]) INCLUDE ([SentAtUtc])
    - _Requirements: 11.6_

  - [x] 15.2 Create StatementEmailHistory entity class
    - Create `Portal.Infrastructure/Entities/StatementEmailHistory.cs`
    - Define properties: Id (int), BusinessId (int), CustomerId (int), FromDate (DateOnly), ToDate (DateOnly), RecipientEmail (string), SentByUserId (string), SentAtUtc (DateTime), CreatedAtUtc (DateTime)
    - _Requirements: 11.6_

  - [x] 15.3 Create StatementEmailHistoryDto model
    - Create `Portal.Infrastructure/Models/StatementEmailHistoryDto.cs`
    - Define properties: SentAtUtc (DateTime), FromDate (DateOnly), ToDate (DateOnly), RecipientEmail (string), SentByDisplayName (string)
    - _Requirements: 11.2_

  - [x] 15.4 Add InsertEmailHistoryAsync and GetEmailHistoryByCustomerAsync to StatementRepository
    - Implement `InsertEmailHistoryAsync(StatementEmailHistory entity)` using ExecuteSqlRawAsync with INSERT INTO [customer].[StatementEmailHistory] ([BusinessId], [CustomerId], [FromDate], [ToDate], [RecipientEmail], [SentByUserId], [SentAtUtc]) VALUES (...)
    - Implement `GetEmailHistoryByCustomerAsync(int customerId, int businessId)` returning `List<StatementEmailHistoryDto>` — join [customer].[StatementEmailHistory] with [dbo].[AspNetUsers] on SentByUserId = Id, select SentAtUtc, FromDate, ToDate, RecipientEmail, AspNetUsers.FullName as SentByDisplayName, ordered by SentAtUtc DESC
    - Use full table names, parameterized queries, null-safe parameters, try/catch with rethrow
    - _Requirements: 11.3, 11.5, 11.6_

  - [x] 15.5 Add GetEmailHistoryAsync to IStatementService and StatementService
    - Add `Task<List<StatementEmailHistoryDto>> GetEmailHistoryAsync(int customerId, int businessId)` to IStatementService
    - Implement in StatementService: validate businessId > 0 (return empty list if unresolvable), delegate to StatementRepository.GetEmailHistoryByCustomerAsync
    - _Requirements: 11.4, 11.5_

  - [x] 15.6 Update LogEmailSentAsync in StatementService to persist to StatementEmailHistory
    - In `LogEmailSentAsync`: after creating the audit log entry, also call `StatementRepository.InsertEmailHistoryAsync` with a new StatementEmailHistory entity populated with BusinessId, CustomerId, FromDate, ToDate, RecipientEmail, SentByUserId, SentAtUtc = DateTime.UtcNow
    - _Requirements: 11.6_

  - [x] 15.7 Add GetEmailHistory AJAX endpoint to StatementController
    - Implement `GetEmailHistory(int customerId)` as GET AJAX endpoint
    - Validate customerId is provided (return error JSON if missing)
    - Verify customer belongs to current business tenant (return error JSON "Customer not found." if not)
    - Call StatementService.GetEmailHistoryAsync with customerId and businessId
    - Return JSON with success flag and list of StatementEmailHistoryDto records
    - _Requirements: 11.1, 11.4, 11.5_

  - [x] 15.8 Add Email History table UI section to Statement Razor view
    - Add a new `.glass.card-pad` section below the transaction table (margin-top:24px)
    - Render heading "Email History" within the section
    - Render table with columns: Date Sent, Statement Period (FromDate – ToDate), Recipient Email, Sent By
    - Display records ordered by Date Sent descending (most recent first)
    - Show empty state message "No statements have been emailed for this customer." when no records exist
    - Load email history via AJAX call to `/Statement/GetEmailHistory?customerId={id}` when a customer is selected
    - Refresh email history table after a successful EmailStatement action
    - Use BlockUI.show/hide and SweetAlert2 for error handling
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.7_

  - [ ]* 15.9 Write property test: Email History Display Completeness (Property 12)
    - **Property 12: Email History Display Completeness**
    - Generate arbitrary email history records with varying SentAtUtc, FromDate, ToDate, RecipientEmail, and SentByDisplayName values
    - Assert each record in the returned list contains all required fields: SentAtUtc, FromDate, ToDate, RecipientEmail, SentByDisplayName (non-null, non-empty)
    - Minimum 100 iterations
    - **Validates: Requirements 11.2**

  - [ ]* 15.10 Write property test: Email History Ordering (Property 13)
    - **Property 13: Email History Ordering**
    - Generate arbitrary lists of email history records with varying SentAtUtc timestamps
    - Assert all returned records are sorted in non-increasing order by SentAtUtc (most recent first)
    - Minimum 100 iterations
    - **Validates: Requirements 11.3**

  - [ ]* 15.11 Write property test: Email History Scoping (Property 14)
    - **Property 14: Email History Scoping**
    - Generate arbitrary email history records belonging to multiple customers and multiple business tenants
    - Query for a specific CustomerId and BusinessId
    - Assert all returned records have matching CustomerId AND matching BusinessId — no cross-tenant or cross-customer leakage
    - Minimum 100 iterations
    - **Validates: Requirements 11.4, 11.5**

  - [ ]* 15.12 Write property test: Email History Persistence Round-Trip (Property 15)
    - **Property 15: Email History Persistence Round-Trip**
    - Generate arbitrary valid email history input (BusinessId, CustomerId, FromDate, ToDate, RecipientEmail, UserId)
    - Insert via InsertEmailHistoryAsync, then query via GetEmailHistoryByCustomerAsync
    - Assert the returned list contains a record with all matching field values and SentAtUtc >= operation start time
    - Minimum 100 iterations
    - **Validates: Requirements 11.6**

- [x] 16. Final checkpoint - Ensure all tests pass including Email History
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- All SQL queries use full table names (no aliases) per repository standards
- All repositories follow try/catch with rethrow pattern
- All AJAX calls use BlockUI.show/hide and SweetAlert2 for user feedback
- UI follows MyChair Design System with `.glass.card-pad` cards and layout standards
- Pagination uses page size of 15 as specified in requirements

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "15.1"] },
    { "id": 2, "tasks": ["3.1", "15.2", "15.3"] },
    { "id": 3, "tasks": ["3.2", "3.3", "3.4", "3.5", "3.6", "3.7", "15.4"] },
    { "id": 4, "tasks": ["5.1", "10.1", "15.5"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.1", "10.2", "15.6"] },
    { "id": 6, "tasks": ["6.2", "6.3", "7.1", "10.3", "15.7"] },
    { "id": 7, "tasks": ["8.1", "10.4", "10.5", "10.6", "15.8"] },
    { "id": 8, "tasks": ["11.1", "12.1", "15.9", "15.10", "15.11"] },
    { "id": 9, "tasks": ["12.2", "13.1", "15.12"] }
  ]
}
```
