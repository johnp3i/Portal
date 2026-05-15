/*
    ============================================================================
    Portal Database — Full Schema Deployment Script
    ============================================================================
    Description: Creates the complete multi-tenant database schema for the
                 3 Inventors Portal. Includes all 8 schemas, 18 tables,
                 reference data, indexes, and constraints.

    Target: SQL Server (clean database or idempotent re-run)
    Order: Database → Schemas → Portal tables → Customer → Quotation →
           Invoice → Revenue → Purchase → VAT → Audit

    This script is idempotent — safe to run multiple times.
    ============================================================================
*/

-- ============================================================================
-- 0. CREATE DATABASE
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'Portal')
BEGIN
    CREATE DATABASE [Portal];
END
GO

USE [Portal];
GO

-- ============================================================================
-- 1. CREATE SCHEMAS
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'portal')
BEGIN
    EXEC('CREATE SCHEMA [portal]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'customer')
BEGIN
    EXEC('CREATE SCHEMA [customer]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'quotation')
BEGIN
    EXEC('CREATE SCHEMA [quotation]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'invoice')
BEGIN
    EXEC('CREATE SCHEMA [invoice]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'revenue')
BEGIN
    EXEC('CREATE SCHEMA [revenue]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'purchase')
BEGIN
    EXEC('CREATE SCHEMA [purchase]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'vat')
BEGIN
    EXEC('CREATE SCHEMA [vat]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA [audit]');
END
GO

-- ============================================================================
-- 2. [portal].Business
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'Business'
)
BEGIN
    CREATE TABLE [portal].[Business]
    (
        [Id]           INT            IDENTITY(1,1)  NOT NULL,
        [Name]         NVARCHAR(200)                 NOT NULL,
        [IsActive]     BIT                           NOT NULL  CONSTRAINT [DF_Business_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2                     NOT NULL  CONSTRAINT [DF_Business_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] DATETIME2                     NOT NULL  CONSTRAINT [DF_Business_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Business] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_Business_Name] UNIQUE ([Name])
    );
END
GO

-- ============================================================================
-- 3. [portal].BusinessProfile
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'BusinessProfile'
)
BEGIN
    CREATE TABLE [portal].[BusinessProfile]
    (
        [Id]                        INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                INT                           NOT NULL,
        [CompanyRegistrationNumber] NVARCHAR(50)                  NOT NULL,
        [VatRegistrationNumber]     NVARCHAR(50)                  NOT NULL,
        [VatRegistrationDate]       DATE                          NOT NULL,
        [VatPeriodLengthInMonths]   INT                           NOT NULL,
        [AddressLine1]              NVARCHAR(200)                 NOT NULL,
        [AddressLine2]              NVARCHAR(200)                 NULL,
        [City]                      NVARCHAR(100)                 NOT NULL,
        [PostalCode]                NVARCHAR(20)                  NOT NULL,
        [Country]                   NVARCHAR(100)                 NOT NULL,
        [TelephoneNumber]           NVARCHAR(30)                  NULL,
        [MobileNumber]              NVARCHAR(30)                  NULL,
        [Email]                     NVARCHAR(200)                 NOT NULL,

        CONSTRAINT [PK_BusinessProfile] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessProfile_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [UQ_BusinessProfile_BusinessId] UNIQUE ([BusinessId]),
        CONSTRAINT [CK_BusinessProfile_VatPeriodLengthInMonths] CHECK ([VatPeriodLengthInMonths] IN (1, 2, 3, 4, 6, 12))
    );
END
GO

-- ============================================================================
-- 4. [customer].Customer
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'customer' AND TABLE_NAME = 'Customer'
)
BEGIN
    CREATE TABLE [customer].[Customer]
    (
        [Id]               INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]       INT                           NOT NULL,
        [Name]             NVARCHAR(200)                 NOT NULL,
        [Email]            NVARCHAR(200)                 NULL,
        [TelephoneNumber]  NVARCHAR(30)                  NULL,
        [AddressLine1]     NVARCHAR(200)                 NULL,
        [AddressLine2]     NVARCHAR(200)                 NULL,
        [City]             NVARCHAR(100)                 NULL,
        [PostalCode]       NVARCHAR(20)                  NULL,
        [Country]          NVARCHAR(100)                 NULL,
        [IsActive]         BIT                           NOT NULL  CONSTRAINT [DF_Customer_IsActive] DEFAULT (1),
        [CreatedAtUtc]     DATETIME2                     NOT NULL  CONSTRAINT [DF_Customer_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]     DATETIME2                     NOT NULL  CONSTRAINT [DF_Customer_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Customer] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Customer_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Customer_BusinessId' AND [object_id] = OBJECT_ID('[customer].[Customer]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Customer_BusinessId]
        ON [customer].[Customer] ([BusinessId]);
END
GO

-- ============================================================================
-- 5. [quotation].QuotationStatusType (Reference Table + Seed Data)
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'QuotationStatusType'
)
BEGIN
    CREATE TABLE [quotation].[QuotationStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_QuotationStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 1)
    INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (1, 'Draft');
IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 2)
    INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (2, 'Sent');
IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 3)
    INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (3, 'Accepted');
IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 4)
    INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (4, 'Converted');
IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationStatusType] WHERE [Id] = 5)
    INSERT INTO [quotation].[QuotationStatusType] ([Id], [Name]) VALUES (5, 'Archived');
GO

-- ============================================================================
-- 6. [quotation].Quotation
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'Quotation'
)
BEGIN
    CREATE TABLE [quotation].[Quotation]
    (
        [Id]                      INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]              INT                           NOT NULL,
        [CustomerId]              INT                           NOT NULL,
        [QuotationStatusTypeId]   INT                           NOT NULL,
        [Reference]               NVARCHAR(100)                 NOT NULL,
        [ValidUntil]              DATE                          NULL,
        [Subtotal]                DECIMAL(18,2)                 NOT NULL,
        [TaxAmount]               DECIMAL(18,2)                 NOT NULL,
        [TotalAmount]             DECIMAL(18,2)                 NOT NULL,
        [Notes]                   NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]            DATETIME2                     NOT NULL  CONSTRAINT [DF_Quotation_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]            DATETIME2                     NOT NULL  CONSTRAINT [DF_Quotation_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Quotation] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Quotation_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Quotation_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer] ([Id]),
        CONSTRAINT [FK_Quotation_QuotationStatusType] FOREIGN KEY ([QuotationStatusTypeId]) REFERENCES [quotation].[QuotationStatusType] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Quotation_BusinessId' AND [object_id] = OBJECT_ID('[quotation].[Quotation]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Quotation_BusinessId]
        ON [quotation].[Quotation] ([BusinessId]);
END
GO

-- ============================================================================
-- 7. [quotation].QuotationLine
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'QuotationLine'
)
BEGIN
    CREATE TABLE [quotation].[QuotationLine]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [QuotationId]   INT                           NOT NULL,
        [Description]   NVARCHAR(500)                 NOT NULL,
        [Quantity]      DECIMAL(18,4)                 NOT NULL,
        [UnitPrice]     DECIMAL(18,2)                 NOT NULL,
        [LineTotal]     DECIMAL(18,2)                 NOT NULL,
        [SortOrder]     INT                           NOT NULL,

        CONSTRAINT [PK_QuotationLine] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_QuotationLine_Quotation] FOREIGN KEY ([QuotationId]) REFERENCES [quotation].[Quotation] ([Id]) ON DELETE CASCADE
    );
END
GO

-- ============================================================================
-- 8. [invoice].InvoiceStatusType (Reference Table + Seed Data)
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'InvoiceStatusType'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_InvoiceStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 1)
    INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (1, 'Draft');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 2)
    INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (2, 'Issued');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceStatusType] WHERE [Id] = 3)
    INSERT INTO [invoice].[InvoiceStatusType] ([Id], [Name]) VALUES (3, 'Cancelled');
GO

-- ============================================================================
-- 9. [invoice].InvoiceFinancialStatusType (Reference Table + Seed Data)
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'InvoiceFinancialStatusType'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceFinancialStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_InvoiceFinancialStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 1)
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (1, 'Unpaid');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 2)
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (2, 'PartiallyPaid');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 3)
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (3, 'Paid');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 4)
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (4, 'Overdue');
IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceFinancialStatusType] WHERE [Id] = 5)
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name]) VALUES (5, 'WrittenOff');
GO

-- ============================================================================
-- 10. [invoice].Invoice
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'Invoice'
)
BEGIN
    CREATE TABLE [invoice].[Invoice]
    (
        [Id]                            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                    INT                           NOT NULL,
        [CustomerId]                    INT                           NOT NULL,
        [QuotationId]                   INT                           NULL,
        [InvoiceStatusTypeId]           INT                           NOT NULL,
        [InvoiceFinancialStatusTypeId]  INT                           NOT NULL,
        [InvoiceNumber]                 NVARCHAR(50)                  NOT NULL,
        [InvoiceDate]                   DATE                          NOT NULL,
        [DueDate]                       DATE                          NOT NULL,
        [Subtotal]                      DECIMAL(18,2)                 NOT NULL,
        [TaxAmount]                     DECIMAL(18,2)                 NOT NULL,
        [TotalAmount]                   DECIMAL(18,2)                 NOT NULL,
        [CurrencyCode]                  NVARCHAR(3)                   NOT NULL  CONSTRAINT [DF_Invoice_CurrencyCode] DEFAULT ('EUR'),
        [Notes]                         NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]                  DATETIME2                     NOT NULL  CONSTRAINT [DF_Invoice_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]                  DATETIME2                     NOT NULL  CONSTRAINT [DF_Invoice_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Invoice] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Invoice_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Invoice_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer] ([Id]),
        CONSTRAINT [FK_Invoice_Quotation] FOREIGN KEY ([QuotationId]) REFERENCES [quotation].[Quotation] ([Id]),
        CONSTRAINT [FK_Invoice_InvoiceStatusType] FOREIGN KEY ([InvoiceStatusTypeId]) REFERENCES [invoice].[InvoiceStatusType] ([Id]),
        CONSTRAINT [FK_Invoice_InvoiceFinancialStatusType] FOREIGN KEY ([InvoiceFinancialStatusTypeId]) REFERENCES [invoice].[InvoiceFinancialStatusType] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Invoice_BusinessId' AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoice_BusinessId]
        ON [invoice].[Invoice] ([BusinessId]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'UX_Invoice_QuotationId' AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Invoice_QuotationId]
        ON [invoice].[Invoice] ([QuotationId])
        WHERE [QuotationId] IS NOT NULL;
END
GO

-- ============================================================================
-- 11. [invoice].InvoiceLine
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice' AND TABLE_NAME = 'InvoiceLine'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceLine]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [InvoiceId]     INT                           NOT NULL,
        [Description]   NVARCHAR(500)                 NOT NULL,
        [Quantity]      DECIMAL(18,4)                 NOT NULL,
        [UnitPrice]     DECIMAL(18,2)                 NOT NULL,
        [LineTotal]     DECIMAL(18,2)                 NOT NULL,
        [SortOrder]     INT                           NOT NULL,

        CONSTRAINT [PK_InvoiceLine] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_InvoiceLine_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]) ON DELETE CASCADE
    );
END
GO

-- ============================================================================
-- 12. [revenue].PaymentMethodType (Reference Table + Seed Data)
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'PaymentMethodType'
)
BEGIN
    CREATE TABLE [revenue].[PaymentMethodType]
    (
        [Id]        INT            NOT NULL,
        [Name]      NVARCHAR(50)   NOT NULL,
        [IsActive]  BIT            NOT NULL  CONSTRAINT [DF_PaymentMethodType_IsActive] DEFAULT (1),

        CONSTRAINT [PK_PaymentMethodType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 1)
    INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (1, 'Cash', 1);
IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 2)
    INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (2, 'BankTransfer', 1);
IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 3)
    INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (3, 'Card', 1);
IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 4)
    INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (4, 'Cheque', 1);
IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 5)
    INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (5, 'Other', 1);
GO

-- ============================================================================
-- 13. [revenue].Payment
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'Payment'
)
BEGIN
    CREATE TABLE [revenue].[Payment]
    (
        [Id]                   INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]           INT                           NOT NULL,
        [InvoiceId]            INT                           NOT NULL,
        [PaymentMethodTypeId]  INT                           NOT NULL,
        [PaymentDateUtc]       DATETIME2                     NOT NULL,
        [Amount]               DECIMAL(18,2)                 NOT NULL,
        [Reference]            NVARCHAR(200)                 NULL,
        [Notes]                NVARCHAR(MAX)                 NULL,
        [IsVoided]             BIT                           NOT NULL  CONSTRAINT [DF_Payment_IsVoided] DEFAULT (0),
        [CreatedAtUtc]         DATETIME2                     NOT NULL  CONSTRAINT [DF_Payment_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [CreatedByUserId]      NVARCHAR(450)                 NULL,

        CONSTRAINT [PK_Payment] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Payment_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Payment_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]),
        CONSTRAINT [FK_Payment_PaymentMethodType] FOREIGN KEY ([PaymentMethodTypeId]) REFERENCES [revenue].[PaymentMethodType] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Payment_BusinessId' AND [object_id] = OBJECT_ID('[revenue].[Payment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_BusinessId]
        ON [revenue].[Payment] ([BusinessId]);
END
GO

-- ============================================================================
-- 14. [purchase].Supplier
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'Supplier'
)
BEGIN
    CREATE TABLE [purchase].[Supplier]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [Name]          NVARCHAR(200)                 NOT NULL,
        [IsActive]      BIT                           NOT NULL  CONSTRAINT [DF_Supplier_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME2                     NOT NULL  CONSTRAINT [DF_Supplier_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Supplier_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Supplier_BusinessId' AND [object_id] = OBJECT_ID('[purchase].[Supplier]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Supplier_BusinessId]
        ON [purchase].[Supplier] ([BusinessId]);
END
GO

-- ============================================================================
-- 15. [purchase].ExpenseCategory
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'ExpenseCategory'
)
BEGIN
    CREATE TABLE [purchase].[ExpenseCategory]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [Name]          NVARCHAR(100)                 NOT NULL,
        [IsActive]      BIT                           NOT NULL  CONSTRAINT [DF_ExpenseCategory_IsActive] DEFAULT (1),

        CONSTRAINT [PK_ExpenseCategory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ExpenseCategory_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_ExpenseCategory_BusinessId' AND [object_id] = OBJECT_ID('[purchase].[ExpenseCategory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExpenseCategory_BusinessId]
        ON [purchase].[ExpenseCategory] ([BusinessId]);
END
GO

-- ============================================================================
-- 16. [purchase].Purchase
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'Purchase'
)
BEGIN
    CREATE TABLE [purchase].[Purchase]
    (
        [Id]                    INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]            INT                           NOT NULL,
        [SupplierId]            INT                           NOT NULL,
        [ExpenseCategoryId]     INT                           NOT NULL,
        [InvoiceNumber]         NVARCHAR(100)                 NULL,
        [InvoiceDate]           DATE                          NOT NULL,
        [Description]           NVARCHAR(500)                 NOT NULL,
        [AmountExcludingVat]    DECIMAL(18,2)                 NOT NULL,
        [VatAmount]             DECIMAL(18,2)                 NOT NULL,
        [TotalAmount]           DECIMAL(18,2)                 NOT NULL,
        [IsEuReverseCharge]     BIT                           NOT NULL  CONSTRAINT [DF_Purchase_IsEuReverseCharge] DEFAULT (0),
        [Country]               NVARCHAR(100)                 NULL,
        [Notes]                 NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]          DATETIME2                     NOT NULL  CONSTRAINT [DF_Purchase_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]          DATETIME2                     NOT NULL  CONSTRAINT [DF_Purchase_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Purchase] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Purchase_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_Purchase_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]),
        CONSTRAINT [FK_Purchase_ExpenseCategory] FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_Purchase_BusinessId' AND [object_id] = OBJECT_ID('[purchase].[Purchase]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Purchase_BusinessId]
        ON [purchase].[Purchase] ([BusinessId]);
END
GO

-- ============================================================================
-- 17. [vat].VatSubmissionPeriod
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'vat' AND TABLE_NAME = 'VatSubmissionPeriod'
)
BEGIN
    CREATE TABLE [vat].[VatSubmissionPeriod]
    (
        [Id]                INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]        INT                           NOT NULL,
        [PeriodStartDate]   DATE                          NOT NULL,
        [PeriodEndDate]     DATE                          NOT NULL,
        [PeriodLabel]       NVARCHAR(100)                 NOT NULL,
        [CreatedAtUtc]      DATETIME2                     NOT NULL  CONSTRAINT [DF_VatSubmissionPeriod_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_VatSubmissionPeriod] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_VatSubmissionPeriod_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_VatSubmissionPeriod_BusinessId' AND [object_id] = OBJECT_ID('[vat].[VatSubmissionPeriod]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VatSubmissionPeriod_BusinessId]
        ON [vat].[VatSubmissionPeriod] ([BusinessId]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'UX_VatSubmissionPeriod_BusinessId_PeriodStartDate' AND [object_id] = OBJECT_ID('[vat].[VatSubmissionPeriod]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_VatSubmissionPeriod_BusinessId_PeriodStartDate]
        ON [vat].[VatSubmissionPeriod] ([BusinessId], [PeriodStartDate]);
END
GO

-- ============================================================================
-- 18. [vat].VatSubmission
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'vat' AND TABLE_NAME = 'VatSubmission'
)
BEGIN
    CREATE TABLE [vat].[VatSubmission]
    (
        [Id]                        INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                INT                           NOT NULL,
        [VatSubmissionPeriodId]     INT                           NOT NULL,
        [TotalOutputVat]            DECIMAL(18,2)                 NOT NULL,
        [TotalInputVat]             DECIMAL(18,2)                 NOT NULL,
        [NetVatPayable]             DECIMAL(18,2)                 NOT NULL,
        [IsSubmitted]               BIT                           NOT NULL  CONSTRAINT [DF_VatSubmission_IsSubmitted] DEFAULT (0),
        [SubmittedAtUtc]            DATETIME2                     NULL,
        [Notes]                     NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]              DATETIME2                     NOT NULL  CONSTRAINT [DF_VatSubmission_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_VatSubmission] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_VatSubmission_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_VatSubmission_VatSubmissionPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_VatSubmission_BusinessId' AND [object_id] = OBJECT_ID('[vat].[VatSubmission]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VatSubmission_BusinessId]
        ON [vat].[VatSubmission] ([BusinessId]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'UX_VatSubmission_BusinessId_VatSubmissionPeriodId' AND [object_id] = OBJECT_ID('[vat].[VatSubmission]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_VatSubmission_BusinessId_VatSubmissionPeriodId]
        ON [vat].[VatSubmission] ([BusinessId], [VatSubmissionPeriodId]);
END
GO

-- ============================================================================
-- 19. [audit].AuditLog
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'audit' AND TABLE_NAME = 'AuditLog'
)
BEGIN
    CREATE TABLE [audit].[AuditLog]
    (
        [Id]            BIGINT          IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                            NULL,
        [UserId]        NVARCHAR(450)                  NULL,
        [Action]        NVARCHAR(50)                   NOT NULL,
        [TableName]     NVARCHAR(200)                  NOT NULL,
        [RecordId]      NVARCHAR(50)                   NOT NULL,
        [OldValues]     NVARCHAR(MAX)                  NULL,
        [NewValues]     NVARCHAR(MAX)                  NULL,
        [Timestamp]     DATETIME2                      NOT NULL  CONSTRAINT [DF_AuditLog_Timestamp] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AuditLog_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId' AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId]
        ON [audit].[AuditLog] ([BusinessId]);
END
GO

-- ============================================================================
-- DEPLOYMENT COMPLETE
-- ============================================================================
PRINT 'Portal database schema deployment completed successfully.';
GO
