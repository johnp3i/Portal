/*
    Migration: 006_CreateQuotationTable
    Description: Creates the [quotation].Quotation table — a commercial proposal document
                 containing priced line items sent to a Customer, scoped to a Business tenant.

    Requirements: 4.1 - THE Portal_Database SHALL contain a [quotation].Quotation table with columns:
                         Id (PK, int identity), BusinessId (FK to [portal].Business),
                         CustomerId (FK to [customer].Customer),
                         QuotationStatusTypeId (FK to [quotation].QuotationStatusType),
                         Reference (nvarchar, required), ValidUntil (date, nullable),
                         Subtotal (decimal(18,2)), TaxAmount (decimal(18,2)),
                         TotalAmount (decimal(18,2)), Notes (nvarchar(max), nullable),
                         CreatedAtUtc (datetime2), UpdatedAtUtc (datetime2)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'Quotation'
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

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Quotation_BusinessId'
      AND [object_id] = OBJECT_ID('[quotation].[Quotation]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Quotation_BusinessId]
        ON [quotation].[Quotation] ([BusinessId]);
END
GO
