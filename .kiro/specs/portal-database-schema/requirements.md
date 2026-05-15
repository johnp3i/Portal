# Requirements Document

## Introduction

This document defines the database schema requirements for the 3 Inventors Portal — a multi-tenant back-office operational platform built on ASP.NET Core MVC 8 with SQL Server. The schema uses separate SQL Server schemas per module with tenant isolation via a shared BusinessId column on all data tables. EF Core global query filters enforce tenant boundaries at the application layer.

## Glossary

- **Portal_Database**: The SQL Server database hosting all module schemas for the 3 Inventors Portal
- **Business**: The tenant entity representing a subscribing company within the platform
- **BusinessProfile**: Configuration record holding company registration, VAT details, and contact information for a Business
- **Customer**: A client entity registered under a specific Business tenant
- **Quotation**: A commercial proposal document containing priced line items sent to a Customer
- **QuotationLine**: An individual priced item within a Quotation
- **QuotationStatusType**: A reference table defining the lifecycle states of a Quotation (Draft, Sent, Accepted, Converted, Archived)
- **Invoice**: A financial document generated from a Quotation or created independently, representing an obligation to pay
- **InvoiceLine**: An individual priced item within an Invoice
- **InvoiceStatusType**: A reference table defining the document lifecycle states of an Invoice (Draft, Issued, Cancelled)
- **InvoiceFinancialStatusType**: A reference table defining the financial states of an Invoice (Unpaid, PartiallyPaid, Paid, Overdue, WrittenOff)
- **Payment**: A monetary transaction recorded against an Invoice
- **PaymentMethodType**: A reference table defining accepted payment methods (Cash, BankTransfer, Card, Cheque, Other)
- **Purchase**: An expense entry representing money spent by the Business, with VAT tracking
- **Supplier**: A vendor entity from whom Purchases are made
- **ExpenseCategory**: A classification for Purchase entries
- **VatSubmission**: A VAT return submission record for a specific period
- **VatSubmissionPeriod**: A calculated time range representing a single VAT reporting period
- **AuditLog**: A record tracking data changes across the platform
- **Tenant_Isolation**: The mechanism ensuring each Business can only access its own data, enforced via EF Core global query filters on BusinessId
- **EU_Reverse_Charge**: A VAT mechanism where cross-border EU purchases carry 0% local VAT and the buyer self-accounts

## Requirements

### Requirement 1: Portal Schema — Business Tenant Table

**User Story:** As a platform operator, I want each subscribing company stored as a Business tenant, so that all module data can be isolated per tenant.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [portal].Business table with columns: Id (PK, int identity), Name (nvarchar, required), IsActive (bit), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
2. THE Portal_Database SHALL enforce uniqueness on [portal].Business.Name
3. THE Portal_Database SHALL use the [portal] schema for all tenant-level tables

### Requirement 2: Portal Schema — BusinessProfile Configuration

**User Story:** As a business administrator, I want my company's registration details, VAT configuration, and contact information stored in a profile, so that the platform can use them for invoicing, VAT calculations, and communications.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [portal].BusinessProfile table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), CompanyRegistrationNumber (nvarchar), VatRegistrationNumber (nvarchar), VatRegistrationDate (date), VatPeriodLengthInMonths (int), AddressLine1 (nvarchar), AddressLine2 (nvarchar, nullable), City (nvarchar), PostalCode (nvarchar), Country (nvarchar), TelephoneNumber (nvarchar, nullable), MobileNumber (nvarchar, nullable), Email (nvarchar)
2. THE Portal_Database SHALL enforce a one-to-one relationship between [portal].BusinessProfile and [portal].Business via a unique constraint on BusinessId
3. WHEN VatPeriodLengthInMonths is stored, THE Portal_Database SHALL accept values of 1, 2, 3, 4, 6, or 12 only via a CHECK constraint

### Requirement 3: Customer Schema — Customer Registry

**User Story:** As a business user, I want to maintain a registry of my customers, so that I can associate quotations and invoices with them.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [customer].Customer table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), Name (nvarchar, required), Email (nvarchar, nullable), TelephoneNumber (nvarchar, nullable), AddressLine1 (nvarchar, nullable), AddressLine2 (nvarchar, nullable), City (nvarchar, nullable), PostalCode (nvarchar, nullable), Country (nvarchar, nullable), IsActive (bit, default 1), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
2. THE Portal_Database SHALL enforce that [customer].Customer.BusinessId references [portal].Business.Id via a foreign key constraint

### Requirement 4: Quotation Schema — Quotation and Line Items

**User Story:** As a business user, I want to create quotations with multiple line items, so that I can send commercial proposals to customers.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [quotation].Quotation table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), CustomerId (FK to [customer].Customer), QuotationStatusTypeId (FK to [quotation].QuotationStatusType), Reference (nvarchar, required), ValidUntil (date, nullable), Subtotal (decimal(18,2)), TaxAmount (decimal(18,2)), TotalAmount (decimal(18,2)), Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
2. THE Portal_Database SHALL contain a [quotation].QuotationLine table with columns: Id (PK, int identity), QuotationId (FK to [quotation].Quotation), Description (nvarchar, required), Quantity (decimal(18,4)), UnitPrice (decimal(18,2)), LineTotal (decimal(18,2)), SortOrder (int)
3. THE Portal_Database SHALL contain a [quotation].QuotationStatusType reference table with columns: Id (PK, int), Name (nvarchar, required) seeded with values: Draft (1), Sent (2), Accepted (3), Converted (4), Archived (5)
4. THE Portal_Database SHALL enforce cascading delete from [quotation].Quotation to [quotation].QuotationLine

### Requirement 5: Invoice Schema — Invoice and Line Items

**User Story:** As a business user, I want invoices generated from quotations or created independently, so that I can bill customers for goods and services.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain an [invoice].Invoice table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), CustomerId (FK to [customer].Customer), QuotationId (FK to [quotation].Quotation, nullable), InvoiceStatusTypeId (FK to [invoice].InvoiceStatusType), InvoiceFinancialStatusTypeId (FK to [invoice].InvoiceFinancialStatusType), InvoiceNumber (nvarchar, required), InvoiceDate (date), DueDate (date), Subtotal (decimal(18,2)), TaxAmount (decimal(18,2)), TotalAmount (decimal(18,2)), CurrencyCode (nvarchar(3), default 'EUR'), Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
2. THE Portal_Database SHALL contain an [invoice].InvoiceLine table with columns: Id (PK, int identity), InvoiceId (FK to [invoice].Invoice), Description (nvarchar, required), Quantity (decimal(18,4)), UnitPrice (decimal(18,2)), LineTotal (decimal(18,2)), SortOrder (int)
3. THE Portal_Database SHALL contain an [invoice].InvoiceStatusType reference table with columns: Id (PK, int), Name (nvarchar, required) seeded with values: Draft (1), Issued (2), Cancelled (3)
4. THE Portal_Database SHALL contain an [invoice].InvoiceFinancialStatusType reference table with columns: Id (PK, int), Name (nvarchar, required) seeded with values: Unpaid (1), PartiallyPaid (2), Paid (3), Overdue (4), WrittenOff (5)
5. WHEN a Quotation is converted, THE Portal_Database SHALL enforce a unique constraint on [invoice].Invoice.QuotationId to prevent duplicate conversions (filtered index excluding NULL)
6. THE Portal_Database SHALL enforce cascading delete from [invoice].Invoice to [invoice].InvoiceLine

### Requirement 6: Revenue Schema — Payments

**User Story:** As a business user, I want to record payments against invoices as separate entities, so that I can track partial payments, payment history, and compute outstanding balances deterministically.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [revenue].Payment table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), InvoiceId (FK to [invoice].Invoice), PaymentMethodTypeId (FK to [revenue].PaymentMethodType), PaymentDateUtc (datetime2), Amount (decimal(18,2)), Reference (nvarchar, nullable), Notes (nvarchar(max), nullable), IsVoided (bit, default 0), CreatedAtUtc (datetime2), CreatedByUserId (nvarchar, nullable)
2. THE Portal_Database SHALL contain a [revenue].PaymentMethodType reference table with columns: Id (PK, int), Name (nvarchar, required), IsActive (bit, default 1) seeded with values: Cash (1), BankTransfer (2), Card (3), Cheque (4), Other (5)
3. THE Portal_Database SHALL NOT allow deletion of Payment records; voiding is achieved by setting IsVoided to 1

### Requirement 7: Purchase Schema — Expenses, Suppliers, and Categories

**User Story:** As a business user, I want to record purchases and expenses with VAT tracking, categorised by supplier and expense type, so that I can track outgoings and prepare VAT submissions.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [purchase].Purchase table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), SupplierId (FK to [purchase].Supplier), ExpenseCategoryId (FK to [purchase].ExpenseCategory), InvoiceNumber (nvarchar, nullable), InvoiceDate (date), Description (nvarchar, required), AmountExcludingVat (decimal(18,2)), VatAmount (decimal(18,2)), TotalAmount (decimal(18,2)), IsEuReverseCharge (bit, default 0), Country (nvarchar, nullable), Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
2. THE Portal_Database SHALL contain a [purchase].Supplier table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), Name (nvarchar, required), IsActive (bit, default 1), CreatedAtUtc (datetime2)
3. THE Portal_Database SHALL contain a [purchase].ExpenseCategory table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), Name (nvarchar, required), IsActive (bit, default 1)
4. WHEN IsEuReverseCharge is set to 1, THE Portal_Database SHALL allow VatAmount to be 0 (no CHECK constraint preventing zero VAT on reverse charge entries)

### Requirement 8: VAT Schema — Submissions and Period Calculation

**User Story:** As a business user, I want VAT submission periods calculated automatically from my VAT registration date and period length, so that I can track which invoices and purchases fall into each submission period.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a [vat].VatSubmission table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), VatSubmissionPeriodId (FK to [vat].VatSubmissionPeriod), TotalOutputVat (decimal(18,2)), TotalInputVat (decimal(18,2)), NetVatPayable (decimal(18,2)), IsSubmitted (bit, default 0), SubmittedAtUtc (datetime2, nullable), Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2)
2. THE Portal_Database SHALL contain a [vat].VatSubmissionPeriod table with columns: Id (PK, int identity), BusinessId (FK to [portal].Business), PeriodStartDate (date), PeriodEndDate (date), PeriodLabel (nvarchar), CreatedAtUtc (datetime2)
3. WHEN a VatSubmissionPeriod is generated, THE Portal_Database SHALL derive PeriodStartDate and PeriodEndDate from the BusinessProfile.VatRegistrationDate and BusinessProfile.VatPeriodLengthInMonths configuration
4. THE Portal_Database SHALL enforce a unique constraint on [vat].VatSubmissionPeriod (BusinessId, PeriodStartDate) to prevent duplicate periods
5. THE Portal_Database SHALL enforce a unique constraint on [vat].VatSubmission (BusinessId, VatSubmissionPeriodId) to prevent duplicate submissions per period

### Requirement 9: Audit Schema — Change Tracking

**User Story:** As a platform operator, I want all significant data changes logged, so that I can trace who changed what and when for compliance and debugging purposes.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain an [audit].AuditLog table with columns: Id (PK, bigint identity), BusinessId (FK to [portal].Business, nullable for system-level events), UserId (nvarchar, nullable), Action (nvarchar, required), TableName (nvarchar, required), RecordId (nvarchar, required), OldValues (nvarchar(max), nullable), NewValues (nvarchar(max), nullable), Timestamp (datetime2, required)
2. THE Portal_Database SHALL use bigint for [audit].AuditLog.Id to accommodate high-volume audit entries
3. THE [audit].AuditLog table SHALL be append-only; no UPDATE or DELETE operations are permitted on audit records

### Requirement 10: Multi-Tenant Isolation

**User Story:** As a platform operator, I want all data tables to include a BusinessId foreign key with EF Core global query filters, so that tenants can never access each other's data.

#### Acceptance Criteria

1. THE Portal_Database SHALL include a BusinessId column (FK to [portal].Business) on every data table except reference/lookup tables (QuotationStatusType, InvoiceStatusType, InvoiceFinancialStatusType, PaymentMethodType)
2. WHEN EF Core DbContext is configured, THE Portal_Database SHALL apply global query filters on BusinessId for all tenant-scoped entities
3. THE Portal_Database SHALL create a non-clustered index on BusinessId for every tenant-scoped table to optimise filtered queries

### Requirement 11: Schema Separation

**User Story:** As a platform architect, I want each module's tables in a dedicated SQL Server schema, so that the database remains organised, permissions can be managed per module, and naming collisions are avoided.

#### Acceptance Criteria

1. THE Portal_Database SHALL create the following SQL Server schemas: portal, customer, quotation, invoice, revenue, purchase, vat, audit
2. THE Portal_Database SHALL place each table in its designated schema as defined in the module mapping
3. THE Portal_Database SHALL allow cross-schema foreign key references (e.g., [invoice].Invoice.CustomerId referencing [customer].Customer.Id)

### Requirement 12: Naming Conventions Enforcement

**User Story:** As a developer, I want consistent naming conventions across all tables and columns, so that the schema is predictable and self-documenting.

#### Acceptance Criteria

1. THE Portal_Database SHALL name all primary key columns as Id
2. THE Portal_Database SHALL name all foreign key columns as <ReferencedTableName>Id (e.g., BusinessId, CustomerId, QuotationId)
3. THE Portal_Database SHALL prefix all BIT columns with Is or Has (e.g., IsActive, IsVoided, IsSubmitted, IsEuReverseCharge, HasActiveSubscription)
4. THE Portal_Database SHALL use PascalCase for all table and column names
