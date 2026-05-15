/*
    Migration: 007_CreateQuotationLineTable
    Description: Creates the [quotation].QuotationLine table — an individual priced item
                 within a Quotation. Lines cascade-delete when their parent Quotation is removed.

    Requirements: 4.2 - THE Portal_Database SHALL contain a [quotation].QuotationLine table with columns:
                         Id (PK, int identity), QuotationId (FK to [quotation].Quotation),
                         Description (nvarchar, required), Quantity (decimal(18,4)),
                         UnitPrice (decimal(18,2)), LineTotal (decimal(18,2)), SortOrder (int)
                 4.4 - THE Portal_Database SHALL enforce cascading delete from
                         [quotation].Quotation to [quotation].QuotationLine

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationLine'
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
