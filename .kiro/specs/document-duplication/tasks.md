# Implementation Plan: Document Duplication

## Overview

Implement the Document Duplication feature following the established `ConvertFromQuotationAsync` pattern. A new `IDocumentDuplicationService` will encapsulate all duplication logic for invoices and quotations, with controller endpoints and UI triggers on both detail pages. The implementation uses C# (ASP.NET Core MVC 8) with EF Core, FsCheck + xUnit for property-based testing.

## Tasks

- [x] 1. Create the service interface and implementation
  - [x] 1.1 Create `IDocumentDuplicationService` interface in `Portal.Infrastructure/Services/`
    - Define `DuplicateInvoiceAsync(int sourceInvoiceId, string userId)` returning `Task<Invoice>`
    - Define `DuplicateQuotationAsync(int sourceQuotationId, string userId)` returning `Task<Quotation>`
    - _Requirements: 3.1, 4.1_

  - [x] 1.2 Create `DocumentDuplicationService` class in `Portal.Infrastructure/Services/`
    - Inject `ICurrentTenantService`, `InvoiceRepository`, `InvoiceLineRepository`, `InvoiceSectionRepository`, `QuotationRepository`, `QuotationLineRepository`, `ProposalSectionRepository`, `AuditLogRepository`, and `PortalDbContext`
    - Implement `DuplicateInvoiceAsync`: validate source exists and belongs to current business, begin transaction, generate next sequential invoice number, create new Invoice entity with Draft/Unpaid status, InvoiceDate = today, DueDate = today + duration gap, copy CustomerId/Notes/IsGrandTotalShown/IsQuotationReferenceShown/CurrencyCode, set QuotationId = null, copy sections with ID mapping, copy lines with section mapping, recalculate financials, write audit log, commit transaction
    - Implement `DuplicateQuotationAsync`: validate source exists and belongs to current business, begin transaction, generate next sequential reference, create new Quotation entity with Draft status, calculate ValidUntil from validity gap (or null), copy CustomerId/Notes/IsGrandTotalShown, set QuotationContactId = null, copy sections with ID mapping, copy lines with section mapping, recalculate financials, write audit log, commit transaction
    - Follow the transaction pattern from `ConvertFromQuotationAsync` (begin → try/commit → catch/rollback/throw)
    - Financial calculation: for each line compute discountedPrice based on DiscountType, lineTotal = Quantity * discountedPrice, Subtotal = sum of lineTotals, TaxAmount = sum of ROUND(lineTotal * VatRate / 100, 2), TotalAmount = Subtotal + TaxAmount
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 10.1, 10.2, 10.3_

  - [x] 1.3 Register `IDocumentDuplicationService` in DI container (`Program.cs`)
    - Add `builder.Services.AddScoped<IDocumentDuplicationService, DocumentDuplicationService>();`
    - _Requirements: 3.1, 4.1_

- [x] 2. Add controller endpoints
  - [x] 2.1 Add `Duplicate` POST endpoint to `InvoiceController`
    - Inject `IDocumentDuplicationService` into the controller constructor
    - Add `[HttpPost][ValidateAntiForgeryToken] Duplicate(int id)` action
    - Extract userId from claims, call `DuplicateInvoiceAsync`, return `Json(new { success = true, redirectUrl })` on success
    - Catch `InvalidOperationException` and return `Json(new { success = false, message = ex.Message })`
    - _Requirements: 1.3, 9.1, 10.3_

  - [x] 2.2 Add `Duplicate` POST endpoint to `QuotationController`
    - Inject `IDocumentDuplicationService` into the controller constructor
    - Add `[HttpPost][ValidateAntiForgeryToken] Duplicate(int id)` action
    - Extract userId from claims, call `DuplicateQuotationAsync`, return `Json(new { success = true, redirectUrl })` on success
    - Catch `InvalidOperationException` and return `Json(new { success = false, message = ex.Message })`
    - _Requirements: 2.3, 9.1, 10.3_

- [x] 3. Checkpoint - Ensure service and controllers compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add UI triggers on detail pages
  - [x] 4.1 Add "Duplicate" button and JavaScript to Invoice Detail page (`Views/Invoice/Detail.cshtml`)
    - Add a "Duplicate" button in the action area of the Invoice Detail page
    - Implement `duplicateInvoice(invoiceId)` function following the standard SweetAlert2 + BlockUI + fetch AJAX pattern
    - Confirmation dialog: title "Duplicate Invoice", text "Are you sure you want to duplicate this invoice?", icon "question", confirmButtonColor "#0D5EA6"
    - On confirm: BlockUI.show('Duplicating...'), POST to `/Invoice/Duplicate` with id and antiforgery token, on success redirect to `data.redirectUrl`, on failure show SweetAlert2 error
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 9.1, 9.2_

  - [x] 4.2 Add "Duplicate" button and JavaScript to Quotation Detail page (`Views/Quotation/Detail.cshtml`)
    - Add a "Duplicate" button in the action area of the Quotation Detail page
    - Implement `duplicateQuotation(quotationId)` function following the standard SweetAlert2 + BlockUI + fetch AJAX pattern
    - Confirmation dialog: title "Duplicate Quotation", text "Are you sure you want to duplicate this quotation?", icon "question", confirmButtonColor "#0D5EA6"
    - On confirm: BlockUI.show('Duplicating...'), POST to `/Quotation/Duplicate` with id and antiforgery token, on success redirect to `data.redirectUrl`, on failure show SweetAlert2 error
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 9.1, 9.2_

- [x] 5. Checkpoint - Ensure full feature compiles and UI is wired
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Create test project and property-based tests
  - [x] 6.1 Create `Portal.Tests` xUnit test project with FsCheck
    - Create `Portal.Tests/Portal.Tests.csproj` referencing xUnit, FsCheck, FsCheck.Xunit, and `Portal.Infrastructure`
    - Add the test project to `Portal.sln`
    - Create `Portal.Tests/DocumentDuplication/` directory for test files
    - _Requirements: 3.1, 4.1_

  - [ ]* 6.2 Write property test for invoice header duplication correctness
    - **Property 1: Invoice header duplication correctness**
    - Generate random Invoice entities with varied statuses, dates, CustomerId, Notes, IsGrandTotalShown, IsQuotationReferenceShown, CurrencyCode, and QuotationId values
    - Verify duplicate has InvoiceStatusTypeId = 1, InvoiceFinancialStatusTypeId = 1, QuotationId = null, InvoiceDate = today, and same CustomerId, Notes, IsGrandTotalShown, IsQuotationReferenceShown, CurrencyCode
    - **Validates: Requirements 3.1, 3.3, 3.5, 3.6, 3.7**

  - [ ]* 6.3 Write property test for invoice duration gap preservation
    - **Property 2: Invoice duration gap preservation**
    - Generate random InvoiceDate/DueDate pairs
    - Verify duplicate DueDate = today + (source DueDate - source InvoiceDate).Days
    - **Validates: Requirements 3.4**

  - [ ]* 6.4 Write property test for quotation header duplication correctness
    - **Property 3: Quotation header duplication correctness**
    - Generate random Quotation entities with varied statuses, contacts, CustomerId, Notes, IsGrandTotalShown
    - Verify duplicate has QuotationStatusTypeId = 1, QuotationContactId = null, and same CustomerId, Notes, IsGrandTotalShown
    - **Validates: Requirements 4.1, 4.4, 4.5, 4.6**

  - [ ]* 6.5 Write property test for quotation validity period preservation
    - **Property 4: Quotation validity period preservation**
    - Generate random CreatedAtUtc/ValidUntil pairs (including null ValidUntil)
    - Verify duplicate ValidUntil = today + (source ValidUntil - source CreatedAtUtc).Days, or null if source was null
    - **Validates: Requirements 4.3**

  - [ ]* 6.6 Write property test for line item field preservation
    - **Property 5: Line item field preservation**
    - Generate random lists of line items with varied Description, Quantity, UnitPrice, VatRate, Discount, DiscountType, CostPrice, SortOrder, ReferenceUrl, Subtitle
    - Verify duplicate contains same count and all specified fields are identical per line
    - **Validates: Requirements 5.1, 5.2**

  - [ ]* 6.7 Write property test for section-to-line mapping preservation
    - **Property 6: Section-to-line mapping preservation**
    - Generate random documents with sections and mixed line-to-section assignments (some null)
    - Verify lines assigned to sections map to corresponding duplicate sections, and null assignments remain null
    - **Validates: Requirements 5.3, 5.4**

  - [ ]* 6.8 Write property test for section field preservation
    - **Property 7: Section field preservation**
    - Generate random lists of sections with varied Name, SortOrder, ColumnConfiguration, SectionType, Description, Notes, IsEmphasized, AccentColor, Label, IsTotalsTableShown
    - Verify duplicate contains same count and all specified fields are identical per section
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 6.9 Write property test for financial calculation correctness
    - **Property 8: Financial calculation correctness**
    - Generate random line items with valid Quantity, UnitPrice, Discount, DiscountType, VatRate
    - Verify Subtotal = sum of lineTotals, TaxAmount = sum of ROUND(lineTotal * VatRate / 100, 2), TotalAmount = Subtotal + TaxAmount
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

  - [ ]* 6.10 Write property test for document independence — new identifiers
    - **Property 9: Document independence — new identifiers**
    - Generate random source documents with sections and lines
    - Verify all duplicate IDs (document, sections, lines) differ from source IDs
    - **Validates: Requirements 8.2**

- [ ] 7. Write unit tests for error handling and edge cases
  - [ ]* 7.1 Write unit tests for `DocumentDuplicationService` error paths
    - Test: source invoice not found → throws `InvalidOperationException`
    - Test: source invoice belongs to different business → throws `InvalidOperationException`
    - Test: source quotation not found → throws `InvalidOperationException`
    - Test: source quotation belongs to different business → throws `InvalidOperationException`
    - Test: source invoice with QuotationId set → duplicate has QuotationId = null
    - Test: source quotation with ValidUntil = null → duplicate has ValidUntil = null
    - Test: source document with zero line items → duplicate has zero line items
    - _Requirements: 10.2, 10.3, 3.6, 4.3, 4.6_

  - [ ]* 7.2 Write unit tests for controller JSON responses
    - Test: successful duplication returns `{ success: true, redirectUrl: "/Invoice/Details/{id}" }`
    - Test: failed duplication returns `{ success: false, message: "..." }`
    - _Requirements: 9.1, 1.4, 2.4_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases
- The service follows the exact transaction pattern from `ConvertFromQuotationAsync` in `InvoiceService.cs`
- No database schema changes are required — all entities already exist
- No test project currently exists in the solution; task 6.1 creates it

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1", "2.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "6.1"] },
    { "id": 4, "tasks": ["6.2", "6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9", "6.10", "7.1", "7.2"] }
  ]
}
```
