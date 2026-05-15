# Implementation Plan: Portal Database Schema

## Overview

Implement the multi-tenant SQL Server database schema for the 3 Inventors Portal. The schema spans 8 SQL Server schemas with tenant isolation via BusinessId. Implementation follows Database-First approach with EF Core scaffolding, using raw SQL migration scripts for schema/table creation and C# for EF Core configuration and tests.

## Tasks

- [x] 1. Create SQL Server schemas and portal tables
  - [x] 1.1 Create schema creation script with all 8 schemas (portal, customer, quotation, invoice, revenue, purchase, vat, audit)
    - Write a SQL migration script with CREATE SCHEMA statements for all 8 schemas
    - _Requirements: 11.1_

  - [x] 1.2 Create [portal].Business table
    - Implement DDL with Id (PK, int identity), Name (nvarchar(200), NOT NULL, UNIQUE), IsActive (bit, DEFAULT 1), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.3 Create [portal].BusinessProfile table
    - Implement DDL with all columns per design, including FK to Business, UNIQUE on BusinessId, CHECK constraint on VatPeriodLengthInMonths IN (1,2,3,4,6,12)
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Create customer and quotation schema tables
  - [x] 2.1 Create [customer].Customer table
    - Implement DDL with all columns per design, FK to [portal].Business, non-clustered index on BusinessId
    - _Requirements: 3.1, 3.2_

  - [x] 2.2 Create [quotation].QuotationStatusType reference table with seed data
    - Implement DDL (Id int PK, Name nvarchar(50) NOT NULL) and INSERT seed values: Draft(1), Sent(2), Accepted(3), Converted(4), Archived(5)
    - _Requirements: 4.3_

  - [x] 2.3 Create [quotation].Quotation table
    - Implement DDL with all columns per design, FKs to Business, Customer, QuotationStatusType, non-clustered index on BusinessId
    - _Requirements: 4.1_

  - [x] 2.4 Create [quotation].QuotationLine table
    - Implement DDL with FK to Quotation ON DELETE CASCADE
    - _Requirements: 4.2, 4.4_

- [x] 3. Create invoice schema tables
  - [x] 3.1 Create [invoice].InvoiceStatusType reference table with seed data
    - Implement DDL and INSERT seed values: Draft(1), Issued(2), Cancelled(3)
    - _Requirements: 5.3_

  - [x] 3.2 Create [invoice].InvoiceFinancialStatusType reference table with seed data
    - Implement DDL and INSERT seed values: Unpaid(1), PartiallyPaid(2), Paid(3), Overdue(4), WrittenOff(5)
    - _Requirements: 5.4_

  - [x] 3.3 Create [invoice].Invoice table
    - Implement DDL with all columns per design, FKs to Business, Customer, Quotation (nullable), InvoiceStatusType, InvoiceFinancialStatusType
    - Include non-clustered index on BusinessId and filtered unique index on QuotationId WHERE QuotationId IS NOT NULL
    - _Requirements: 5.1, 5.5_

  - [x] 3.4 Create [invoice].InvoiceLine table
    - Implement DDL with FK to Invoice ON DELETE CASCADE
    - _Requirements: 5.2, 5.6_

- [x] 4. Create revenue and purchase schema tables
  - [x] 4.1 Create [revenue].PaymentMethodType reference table with seed data
    - Implement DDL (Id, Name, IsActive) and INSERT seed values: Cash(1), BankTransfer(2), Card(3), Cheque(4), Other(5)
    - _Requirements: 6.2_

  - [x] 4.2 Create [revenue].Payment table
    - Implement DDL with all columns per design, FKs to Business, Invoice, PaymentMethodType, non-clustered index on BusinessId
    - _Requirements: 6.1, 6.3_

  - [x] 4.3 Create [purchase].Supplier table
    - Implement DDL with FK to Business, non-clustered index on BusinessId
    - _Requirements: 7.2_

  - [x] 4.4 Create [purchase].ExpenseCategory table
    - Implement DDL with FK to Business, non-clustered index on BusinessId
    - _Requirements: 7.3_

  - [x] 4.5 Create [purchase].Purchase table
    - Implement DDL with all columns per design, FKs to Business, Supplier, ExpenseCategory, non-clustered index on BusinessId
    - _Requirements: 7.1, 7.4_

- [x] 5. Create VAT and audit schema tables
  - [x] 5.1 Create [vat].VatSubmissionPeriod table
    - Implement DDL with FK to Business, non-clustered index on BusinessId, unique constraint on (BusinessId, PeriodStartDate)
    - _Requirements: 8.2, 8.4_

  - [x] 5.2 Create [vat].VatSubmission table
    - Implement DDL with FKs to Business and VatSubmissionPeriod, non-clustered index on BusinessId, unique constraint on (BusinessId, VatSubmissionPeriodId)
    - _Requirements: 8.1, 8.5_

  - [x] 5.3 Create [audit].AuditLog table
    - Implement DDL with bigint identity PK, nullable FK to Business, non-clustered index on BusinessId
    - _Requirements: 9.1, 9.2_

- [x] 6. Checkpoint - Verify all SQL scripts
  - Ensure all SQL migration scripts compile and execute without errors against a clean database. Ask the user if questions arise.

- [x] 7. EF Core entity configuration and DbContext
  - [x] 7.1 Create entity classes for all tables
    - Create C# entity classes matching each table's columns and types (Business, BusinessProfile, Customer, Quotation, QuotationLine, QuotationStatusType, Invoice, InvoiceLine, InvoiceStatusType, InvoiceFinancialStatusType, Payment, PaymentMethodType, Purchase, Supplier, ExpenseCategory, VatSubmission, VatSubmissionPeriod, AuditLog)
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 4.2, 5.1, 5.2, 6.1, 7.1, 7.2, 7.3, 8.1, 8.2, 9.1_

  - [x] 7.2 Create ICurrentTenantService interface and implementation
    - Define interface with CurrentBusinessId property, implement resolution from authentication claims
    - _Requirements: 10.2_

  - [x] 7.3 Create PortalDbContext with schema mappings and relationships
    - Configure all DbSets, map entities to schema-qualified table names using ToTable("TableName", "schema")
    - Configure all FK relationships, cascade delete rules, indexes, unique constraints, CHECK constraints, and default values via Fluent API
    - _Requirements: 11.2, 11.3, 12.1, 12.2, 12.3, 12.4_

  - [x] 7.4 Apply global query filters for tenant isolation
    - Add HasQueryFilter on BusinessId for all tenant-scoped entities using ICurrentTenantService.CurrentBusinessId
    - Exclude reference tables (QuotationStatusType, InvoiceStatusType, InvoiceFinancialStatusType, PaymentMethodType) from query filters
    - _Requirements: 10.1, 10.2_

  - [x] 7.5 Configure seed data for reference tables in OnModelCreating
    - Use HasData() to seed QuotationStatusType, InvoiceStatusType, InvoiceFinancialStatusType, PaymentMethodType with defined values
    - _Requirements: 4.3, 5.3, 5.4, 6.2_

- [x] 8. Checkpoint - Verify EF Core configuration compiles
  - Ensure all entity classes and DbContext configuration compile without errors. Ask the user if questions arise.

- [ ]* 9. Property-based tests for correctness properties
  - [ ]* 9.1 Write property test for unique Business name enforcement
    - **Property 1: Unique Business Name Enforcement**
    - **Validates: Requirements 1.2**

  - [ ]* 9.2 Write property test for VatPeriodLengthInMonths CHECK constraint
    - **Property 2: VatPeriodLengthInMonths CHECK Constraint**
    - **Validates: Requirements 2.3**

  - [ ]* 9.3 Write property test for FK referential integrity on BusinessId
    - **Property 3: Foreign Key Referential Integrity**
    - **Validates: Requirements 3.2, 10.1**

  - [ ]* 9.4 Write property test for cascade delete on line items
    - **Property 4: Cascade Delete on Line Items**
    - **Validates: Requirements 4.4, 5.6**

  - [ ]* 9.5 Write property test for filtered unique index on Invoice.QuotationId
    - **Property 5: Filtered Unique Index on Invoice.QuotationId**
    - **Validates: Requirements 5.5**

  - [ ]* 9.6 Write property test for VAT period generation correctness
    - **Property 6: VAT Period Generation Correctness**
    - **Validates: Requirements 8.3**

  - [ ]* 9.7 Write property test for VAT uniqueness constraints
    - **Property 7: VAT Uniqueness Constraints**
    - **Validates: Requirements 8.4, 8.5**

  - [ ]* 9.8 Write property test for tenant isolation via BusinessId and index
    - **Property 8: Tenant Isolation via BusinessId and Index**
    - **Validates: Requirements 10.1, 10.3**

  - [ ]* 9.9 Write property test for EF Core global query filter tenant isolation
    - **Property 9: EF Core Global Query Filter Tenant Isolation**
    - **Validates: Requirements 10.2**

  - [ ]* 9.10 Write property test for naming convention compliance
    - **Property 10: Naming Convention Compliance**
    - **Validates: Requirements 12.1, 12.2, 12.3, 12.4**

  - [ ]* 9.11 Write property test for one-to-one BusinessProfile enforcement
    - **Property 11: One-to-One BusinessProfile Enforcement**
    - **Validates: Requirements 2.2**

- [ ]* 10. Unit tests for structural validation
  - [ ]* 10.1 Write xUnit tests verifying all 8 schemas exist
    - Query sys.schemas to confirm portal, customer, quotation, invoice, revenue, purchase, vat, audit schemas exist
    - _Requirements: 11.1_

  - [ ]* 10.2 Write xUnit tests verifying table existence and column definitions
    - Query INFORMATION_SCHEMA.COLUMNS to verify each table has correct columns with correct types and nullability
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 4.2, 5.1, 5.2, 6.1, 7.1, 7.2, 7.3, 8.1, 8.2, 9.1_

  - [ ]* 10.3 Write xUnit tests verifying seed data in reference tables
    - Query each reference table and assert expected rows: QuotationStatusType(5), InvoiceStatusType(3), InvoiceFinancialStatusType(5), PaymentMethodType(5)
    - _Requirements: 4.3, 5.3, 5.4, 6.2_

  - [ ]* 10.4 Write xUnit tests verifying cross-schema FK relationships
    - Verify FK constraints exist between schemas (e.g., [invoice].Invoice.CustomerId → [customer].Customer.Id)
    - _Requirements: 11.3_

  - [ ]* 10.5 Write xUnit tests verifying non-clustered indexes on BusinessId
    - Query sys.indexes to confirm IX_*_BusinessId indexes exist on all tenant-scoped tables
    - _Requirements: 10.3_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- SQL migration scripts should be idempotent where possible (use IF NOT EXISTS checks)
- The Database-First approach means EF Core entities mirror the SQL schema exactly
- Property tests use FsCheck.Xunit with minimum 100 iterations per property
- Unit tests use xUnit and query database metadata (sys.schemas, INFORMATION_SCHEMA, sys.indexes)
- All tests run against a dedicated SQL Server LocalDB or Docker container instance
