/*
    Migration: 010_CreateInvoiceTable
    Description: Creates the [invoice].Invoice table — a financial document generated from a
                 Quotation or created independently, representing an obligation to pay.
                 Scoped to a Business tenant with foreign keys to Customer, Quotation (nullable),
                 InvoiceStatusType, and InvoiceFinancialStatusType.

    Requirements: 5.1 - THE Portal_Database SHALL contain an [invoice].Invoice table with columns:
                         Id (PK, int identity), BusinessId (FK to [portal].Business),
                         CustomerId (FK to [customer].Customer),
                         QuotationId (FK to [quotation].Quotation, nullable),
                         InvoiceStatusTypeId (FK to [invoice].InvoiceStatusType),
                         InvoiceFinancialStatusTypeId (FK to [invoice].InvoiceFinancialStatusType),
                         InvoiceNumber (nvarchar, required), InvoiceDate (date),
                         DueDate (date), Subtotal (decimal(18,2)), TaxAmount (decimal(18,2)),
                         TotalAmount (decimal(18,2)), CurrencyCode (nvarchar(3), default 'EUR'),
                         Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)
                 5.5 - WHEN a Quotation is converted, THE Portal_Database SHALL enforce a unique
                         constraint on [invoice].Invoice.QuotationId to prevent duplicate conversions
                         (filtered index excluding NULL)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'Invoice'
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

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Invoice_BusinessId'
      AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoice_BusinessId]
        ON [invoice].[Invoice] ([BusinessId]);
END
GO

-- Filtered unique index on QuotationId to prevent duplicate quotation-to-invoice conversions
-- while allowing multiple NULL values (invoices created independently without a quotation)
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_Invoice_QuotationId'
      AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Invoice_QuotationId]
        ON [invoice].[Invoice] ([QuotationId])
        WHERE [QuotationId] IS NOT NULL;
END
GO
