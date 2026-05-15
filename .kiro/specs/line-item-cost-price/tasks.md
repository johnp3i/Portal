# Implementation Plan: Line Item Cost Price

## Overview

This plan adds a nullable CostPrice column to the QuotationLine entity across all layers (database → entity → repository → service → form model → view), enabling internal profit/margin tracking per line item. The cost price is strictly internal and never exposed in customer-facing views. Tasks follow the existing ASP.NET Core MVC 8 + SQL Server + raw SQL repository patterns.

## Tasks

- [x] 1. Database schema migration
  - [x] 1.1 Create migration script to add CostPrice column to QuotationLine table
    - Create `Portal.Database/Migrations/033_AddCostPriceToQuotationLine.sql`
    - ALTER TABLE [quotation].[QuotationLine] ADD [CostPrice] DECIMAL(18,2) NULL
    - Use IF NOT EXISTS guard for idempotency
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Entity and DbContext updates
  - [x] 2.1 Add CostPrice property to QuotationLine entity
    - Add `public decimal? CostPrice { get; set; }` to `Portal.Infrastructure/Entities/QuotationLine.cs`
    - _Requirements: 2.1_

  - [x] 2.2 Update PortalDbContext with CostPrice column configuration
    - Add fluent configuration: `entity.Property(e => e.CostPrice).HasPrecision(18, 2)`
    - _Requirements: 2.1_

- [x] 3. Repository layer updates
  - [x] 3.1 Update QuotationLineRepository to include CostPrice in all SQL statements
    - Add `[CostPrice]` to SELECT queries
    - Add `@CostPrice` parameter to INSERT statement with `entity.CostPrice ?? (object)DBNull.Value` null-safety
    - Add `[CostPrice] = @CostPrice` to UPDATE statement with null-safe parameter
    - Use full table names in SQL, parameterized queries
    - _Requirements: 2.2, 2.3, 2.4_

- [x] 4. Checkpoint - Ensure schema and data layer compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Service layer updates
  - [x] 5.1 Update IQuotationService interface with CostPrice parameter
    - Add `decimal? costPrice = null` parameter to AddLineAsync method signature
    - Add `decimal? costPrice = null` parameter to UpdateLineAsync method signature
    - _Requirements: 3.1, 3.2_

  - [x] 5.2 Update QuotationService implementation with CostPrice validation and pass-through
    - Accept `decimal? costPrice` in AddLineAsync and UpdateLineAsync
    - Add validation: if `costPrice.HasValue && costPrice.Value < 0` throw `ArgumentException("Cost price must be zero or greater")`
    - Pass CostPrice value through to repository
    - _Requirements: 3.3, 3.4, 3.5_

  - [ ]* 5.3 Write property test for CostPrice validation
    - **Property 2: CostPrice validation rejects negative values**
    - Generate random negative decimals, assert ArgumentException thrown from AddLineAsync/UpdateLineAsync
    - Generate random non-negative decimals (including zero and null), assert no exception
    - **Validates: Requirements 3.4, 3.5**

- [x] 6. Form model and controller updates
  - [x] 6.1 Add CostPrice property to QuotationLineFormViewModel
    - Add `[Range(0, double.MaxValue, ErrorMessage = "Cost price must be zero or greater")] public decimal? CostPrice { get; set; }`
    - _Requirements: 4.1, 4.3, 4.4_

  - [x] 6.2 Update QuotationController to pass CostPrice from form to service
    - Pass `model.CostPrice` to service AddLineAsync and UpdateLineAsync calls
    - _Requirements: 4.2, 4.3, 4.4_

- [x] 7. Edit view — CostPrice input field and margin display
  - [x] 7.1 Add CostPrice input field to the quotation line item edit form
    - Add optional decimal input field for CostPrice in `_SectionCards.cshtml` (or equivalent line item form partial)
    - Render as an optional field with appropriate label (e.g., "Cost Price")
    - _Requirements: 4.2, 4.4_

  - [x] 7.2 Add margin display to the quotation edit view
    - Display computed unit margin (UnitPrice − CostPrice) when CostPrice is populated
    - Display computed line margin (LineTotal − CostPrice × Quantity) when CostPrice is populated
    - Show no margin value when CostPrice is null
    - Display margin values only in the internal edit view
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 8. Checkpoint - Ensure form and view compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Internal visibility constraint verification
  - [x] 9.1 Verify CostPrice is excluded from all customer-facing views
    - Confirm Proposal snapshot view does NOT render CostPrice
    - Confirm ProposalShare endpoint does NOT include CostPrice in response
    - Confirm Invoice views do NOT render CostPrice
    - Confirm margin values are excluded from Proposal and Invoice views
    - If any customer-facing view references QuotationLine properties in a way that could leak CostPrice, explicitly exclude it
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.5_

  - [ ]* 9.2 Write property test for CostPrice exclusion from customer-facing output
    - **Property 3: CostPrice excluded from customer-facing output**
    - Generate random QuotationLines with non-null CostPrice values, render proposal/invoice HTML, assert CostPrice value string not present in output
    - **Validates: Requirements 5.1, 5.2**

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests use FsCheck.Xunit as specified in the design document
- All repositories follow the GenericStoredProcedureRepository pattern with raw SQL, full table names, and null-safe SqlParameter usage
- Margin values are computed in the view/viewmodel — they are NOT persisted in the database
- CostPrice is strictly internal — never exposed in proposals, invoices, or shared links
