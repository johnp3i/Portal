# Implementation Plan: Document Soft Delete

## Overview

Implement the Document Soft Delete feature for invoices and quotations in Draft status. This adds `IsDeleted` and `DeletedAtUtc` columns via migration scripts, a new `IDocumentSoftDeleteService` for validation and execution, controller endpoints, two-step SweetAlert2 confirmation UI, and `IsDeleted = 0` filtering on listing queries. The implementation uses C# (ASP.NET Core MVC 8) with EF Core, SQL Server migrations, and FsCheck + xUnit for property-based testing.

## Tasks

- [x] 1. Create database migration scripts
  - [x] 1.1 Create migration `043_AddIsDeletedToInvoice.sql`
    - Add `IsDeleted` BIT NOT NULL column with named default constraint `[DF_Invoice_IsDeleted]` defaulting to 0
    - Add `DeletedAtUtc` DATETIME2 NULL column
    - Create non-clustered index `[IX_Invoice_BusinessId_IsDeleted]` on columns (BusinessId, IsDeleted)
    - Script must be idempotent — check column/index existence before creating
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Create migration `044_AddIsDeletedToQuotation.sql`
    - Add `IsDeleted` BIT NOT NULL column with named default constraint `[DF_Quotation_IsDeleted]` defaulting to 0
    - Add `DeletedAtUtc` DATETIME2 NULL column
    - Script must be idempotent — check column existence before creating
    - Existing rows receive IsDeleted = 0 via the DEFAULT constraint
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Update EF Core entity models
  - [x] 2.1 Add `IsDeleted` and `DeletedAtUtc` properties to `Invoice.cs`
    - Add `public bool IsDeleted { get; set; }` property
    - Add `public DateTime? DeletedAtUtc { get; set; }` property
    - _Requirements: 1.1, 7.1_

  - [x] 2.2 Add `IsDeleted` and `DeletedAtUtc` properties to `Quotation.cs`
    - Add `public bool IsDeleted { get; set; }` property
    - Add `public DateTime? DeletedAtUtc { get; set; }` property
    - _Requirements: 2.1, 8.1_

- [x] 3. Checkpoint - Ensure migrations and entity changes compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Create the service interface, result model, and implementation
  - [x] 4.1 Create `ServiceResult` model in `Portal.Infrastructure/Models/`
    - Define `Success` bool property and `Message` string? property
    - Add static factory methods `Ok()` and `Fail(string message)`
    - _Requirements: 7.1, 8.1_

  - [x] 4.2 Create `IDocumentSoftDeleteService` interface in `Portal.Infrastructure/Services/`
    - Define `SoftDeleteInvoiceAsync(int invoiceId)` returning `Task<ServiceResult>`
    - Define `SoftDeleteQuotationAsync(int quotationId)` returning `Task<ServiceResult>`
    - _Requirements: 3.4, 4.4_

  - [x] 4.3 Create `DocumentSoftDeleteService` class in `Portal.Infrastructure/Services/`
    - Inject `ICurrentTenantService`, `InvoiceRepository`, `QuotationRepository`
    - Implement `SoftDeleteInvoiceAsync`: get businessId from tenant service, fetch invoice by id and businessId, validate exists → belongs to business → not already deleted → is Draft (StatusTypeId = 1), call `_invoiceRepository.SoftDeleteAsync(invoiceId, businessId)`, return `ServiceResult.Ok()`
    - Implement `SoftDeleteQuotationAsync`: get businessId from tenant service, fetch quotation by id and businessId, validate exists → belongs to business → not already deleted → is Draft (StatusTypeId = 1), call `_quotationRepository.SoftDeleteAsync(quotationId, businessId)`, return `ServiceResult.Ok()`
    - Follow try/catch with rethrow pattern
    - _Requirements: 3.4, 3.5, 4.4, 4.5, 4.6, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2_

  - [x] 4.4 Register `IDocumentSoftDeleteService` in DI container (`Program.cs`)
    - Add `builder.Services.AddScoped<IDocumentSoftDeleteService, DocumentSoftDeleteService>();`
    - _Requirements: 3.4, 4.4_

- [x] 5. Add repository soft-delete methods
  - [x] 5.1 Add `SoftDeleteAsync` method to `InvoiceRepository`
    - Execute atomic UPDATE setting `IsDeleted = 1`, `DeletedAtUtc = GETUTCDATE()`, `UpdatedAtUtc = GETUTCDATE()` WHERE `Id = @Id AND BusinessId = @BusinessId AND IsDeleted = 0`
    - Use full table names `[invoice].[Invoice].[ColumnName]` — no aliases
    - Use `ExecuteSqlRawAsync` with `SqlParameter` for parameterized query
    - Follow try/catch with rethrow pattern
    - _Requirements: 7.1_

  - [x] 5.2 Add `SoftDeleteAsync` method to `QuotationRepository`
    - Execute atomic UPDATE setting `IsDeleted = 1`, `DeletedAtUtc = GETUTCDATE()`, `UpdatedAtUtc = GETUTCDATE()` WHERE `Id = @Id AND BusinessId = @BusinessId AND IsDeleted = 0`
    - Use full table names `[quotation].[Quotation].[ColumnName]` — no aliases
    - Use `ExecuteSqlRawAsync` with `SqlParameter` for parameterized query
    - Follow try/catch with rethrow pattern
    - _Requirements: 8.1_

- [x] 6. Add controller endpoints
  - [x] 6.1 Add `SoftDelete` POST endpoint to `InvoiceController`
    - Inject `IDocumentSoftDeleteService` into the controller constructor
    - Add `[HttpPost][ValidateAntiForgeryToken][ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]` action `SoftDelete(int id)`
    - Call `SoftDeleteInvoiceAsync(id)`, return `Json(new { success = true, message = "Invoice deleted successfully." })` on success
    - Return `Json(new { success = false, message = result.Message })` on failure
    - Wrap in try/catch returning generic error message on exception
    - _Requirements: 5.5, 7.1, 7.5, 10.1_

  - [x] 6.2 Add `SoftDelete` POST endpoint to `QuotationController`
    - Inject `IDocumentSoftDeleteService` into the controller constructor
    - Add `[HttpPost][ValidateAntiForgeryToken][ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]` action `SoftDelete(int id)`
    - Call `SoftDeleteQuotationAsync(id)`, return `Json(new { success = true, message = "Quotation deleted successfully." })` on success
    - Return `Json(new { success = false, message = result.Message })` on failure
    - Wrap in try/catch returning generic error message on exception
    - _Requirements: 6.5, 6.6, 8.1, 8.3, 10.3_

- [x] 7. Checkpoint - Ensure service, repository, and controller compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Add listing query filters
  - [x] 8.1 Add `IsDeleted = 0` filter to invoice listing queries in `InvoiceRepository`
    - Modify `GetAllByBusinessIdAsync` (and any filtered listing methods) to include `AND [invoice].[Invoice].[IsDeleted] = 0` in the WHERE clause
    - Ensure all status, financial status, and customer filters are applied only to non-deleted invoices
    - _Requirements: 9.1, 9.3, 9.5, 9.7_

  - [x] 8.2 Add `IsDeleted = 0` filter to quotation listing queries in `QuotationRepository`
    - Modify `GetAllByBusinessIdAsync` (and any filtered listing methods) to include `AND [quotation].[Quotation].[IsDeleted] = 0` in the WHERE clause
    - Ensure all status, customer, and date range filters are applied only to non-deleted quotations
    - _Requirements: 9.2, 9.4, 9.6_

- [x] 9. Add UI delete button and two-step confirmation flow
  - [x] 9.1 Add "Delete" button and JavaScript to Invoice Detail page
    - Conditionally render the "Delete" button only when `Model.InvoiceStatusTypeId == 1` (Draft)
    - Implement `deleteInvoice(invoiceId)` function with two-step SweetAlert2 confirmation:
      - First dialog: title "Are you sure?", text "This invoice will be deleted.", icon "warning", confirmButtonColor "#C24A4A"
      - Second dialog: title "Final Warning", text "This action cannot be easily undone. Are you sure you want to proceed?", icon "warning", confirmButtonColor "#C24A4A"
    - On second confirm: `BlockUI.show('Deleting...')`, POST to `/Invoice/SoftDelete` with id and antiforgery token
    - On success: `BlockUI.hide()`, show success SweetAlert2 ("Deleted!", "The invoice has been deleted.", icon "success", confirmButtonColor "#0D5EA6"), then redirect to `/Invoice`
    - On error: `BlockUI.hide()`, show error SweetAlert2 with `data.message`
    - Cancel at either dialog takes no action
    - _Requirements: 3.1, 3.2, 3.3, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 10.1, 10.2_

  - [x] 9.2 Add "Delete" button and JavaScript to Quotation Detail page
    - Conditionally render the "Delete" button only when `Model.QuotationStatusTypeId == 1` (Draft)
    - Implement `deleteQuotation(quotationId, reference)` function with two-step SweetAlert2 confirmation:
      - First dialog: title "Are you sure?", text includes quotation reference "Quotation {reference} will be deleted.", icon "warning", confirmButtonColor "#C24A4A"
      - Second dialog: title "Final Warning", text "This action cannot be easily undone. Are you sure you want to proceed?", icon "warning", confirmButtonColor "#C24A4A"
    - On second confirm: `BlockUI.show('Deleting...')`, POST to `/Quotation/SoftDelete` with id and antiforgery token
    - On success: `BlockUI.hide()`, show success SweetAlert2 ("Deleted!", "The quotation has been deleted.", icon "success", confirmButtonColor "#0D5EA6"), then redirect to `/Quotation`
    - On error: `BlockUI.hide()`, show error SweetAlert2 with `data.message`
    - Cancel at either dialog takes no action
    - _Requirements: 4.1, 4.2, 4.3, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 10.3, 10.4_

- [x] 10. Checkpoint - Ensure full feature compiles and UI is wired
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Write property-based tests
  - [ ]* 11.1 Write property test for draft invoice soft-delete atomicity
    - **Property 1: Draft invoice soft-delete atomicity**
    - Generate random Draft invoices (InvoiceStatusTypeId = 1, IsDeleted = 0) belonging to the current business
    - Verify soft-delete sets IsDeleted = 1, DeletedAtUtc is set to current UTC, UpdatedAtUtc is updated, and operation returns success
    - **Validates: Requirements 3.4, 7.1**

  - [ ]* 11.2 Write property test for non-Draft invoice soft-delete rejection
    - **Property 2: Non-Draft invoice soft-delete rejection**
    - Generate invoices with random non-Draft statuses (InvoiceStatusTypeId ≠ 1)
    - Verify soft-delete returns failure result with error message, and invoice remains unchanged (IsDeleted = 0, DeletedAtUtc = NULL, UpdatedAtUtc unchanged)
    - **Validates: Requirements 3.5**

  - [ ]* 11.3 Write property test for draft quotation soft-delete atomicity
    - **Property 3: Draft quotation soft-delete atomicity**
    - Generate random Draft quotations (QuotationStatusTypeId = 1, IsDeleted = 0) belonging to the current business
    - Verify soft-delete sets IsDeleted = 1, DeletedAtUtc is set to current UTC, UpdatedAtUtc is updated, and operation returns success
    - **Validates: Requirements 4.4, 8.1**

  - [ ]* 11.4 Write property test for non-Draft quotation soft-delete rejection
    - **Property 4: Non-Draft quotation soft-delete rejection**
    - Generate quotations with random non-Draft statuses (QuotationStatusTypeId ≠ 1)
    - Verify soft-delete returns failure result with error message, and quotation remains unchanged (IsDeleted = 0, DeletedAtUtc = NULL, UpdatedAtUtc unchanged)
    - **Validates: Requirements 4.5**

  - [ ]* 11.5 Write property test for invoice listing excludes soft-deleted records
    - **Property 5: Invoice listing excludes soft-deleted records**
    - Generate random invoice sets with mixed IsDeleted values (0 and 1), apply random status/financial status/customer filters
    - Verify listing query returns only invoices where IsDeleted = 0 — no deleted invoice ever appears
    - **Validates: Requirements 9.1, 9.3, 9.5**

  - [ ]* 11.6 Write property test for quotation listing excludes soft-deleted records
    - **Property 6: Quotation listing excludes soft-deleted records**
    - Generate random quotation sets with mixed IsDeleted values (0 and 1), apply random status/customer/date range filters
    - Verify listing query returns only quotations where IsDeleted = 0 — no deleted quotation ever appears
    - **Validates: Requirements 9.2, 9.4, 9.6**

- [ ] 12. Write unit tests for service validation and error handling
  - [ ]* 12.1 Write unit tests for `DocumentSoftDeleteService` validation paths
    - Test: invoice not found → returns `ServiceResult.Fail("Invoice not found.")`
    - Test: invoice belongs to different business → returns `ServiceResult.Fail("Invoice does not belong to this business.")`
    - Test: invoice already deleted (IsDeleted = 1) → returns `ServiceResult.Fail("Invoice has already been deleted.")`
    - Test: invoice not Draft (StatusTypeId ≠ 1) → returns `ServiceResult.Fail("Only draft invoices can be deleted.")`
    - Test: quotation not found → returns `ServiceResult.Fail("Quotation not found.")`
    - Test: quotation belongs to different business → returns `ServiceResult.Fail("Quotation does not belong to this business.")`
    - Test: quotation already deleted (IsDeleted = 1) → returns `ServiceResult.Fail("Quotation has already been deleted.")`
    - Test: quotation not Draft (StatusTypeId ≠ 1) → returns `ServiceResult.Fail("Only draft quotations can be deleted.")`
    - _Requirements: 3.5, 4.5, 4.6, 7.2, 7.3, 7.4, 8.2_

  - [ ]* 12.2 Write unit tests for controller JSON responses
    - Test: successful invoice deletion returns `{ success: true, message: "Invoice deleted successfully." }`
    - Test: failed invoice deletion returns `{ success: false, message: "..." }`
    - Test: exception during invoice deletion returns `{ success: false, message: "An unexpected error occurred..." }`
    - Test: successful quotation deletion returns `{ success: true, message: "Quotation deleted successfully." }`
    - Test: failed quotation deletion returns `{ success: false, message: "..." }`
    - _Requirements: 5.5, 5.6, 6.5, 6.7, 7.5, 8.3, 10.5_

- [x] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases
- Migration scripts are idempotent — safe to run multiple times
- The `ServiceResult` model may already exist from other features; if so, reuse the existing one rather than creating a duplicate
- The service follows the same dedicated-service pattern as `IDocumentDuplicationService`
- SQL queries use full table names with no aliases per project conventions
- Delete button visibility is enforced at both UI (conditional rendering) and service (validation) layers

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1", "2.2"] },
    { "id": 1, "tasks": ["4.1", "4.2"] },
    { "id": 2, "tasks": ["4.3", "5.1", "5.2"] },
    { "id": 3, "tasks": ["4.4", "6.1", "6.2"] },
    { "id": 4, "tasks": ["8.1", "8.2"] },
    { "id": 5, "tasks": ["9.1", "9.2"] },
    { "id": 6, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5", "11.6", "12.1", "12.2"] }
  ]
}
```
