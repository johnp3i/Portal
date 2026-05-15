/*
    Migration: 011_CreateInvoiceLineTable
    Description: Creates the [invoice].InvoiceLine table — an individual priced item
                 within an Invoice. Lines cascade-delete when their parent Invoice is removed.

    Requirements: 5.2 - THE Portal_Database SHALL contain an [invoice].InvoiceLine table with columns:
                         Id (PK, int identity), InvoiceId (FK to [invoice].Invoice),
                         Description (nvarchar, required), Quantity (decimal(18,4)),
                         UnitPrice (decimal(18,2)), LineTotal (decimal(18,2)), SortOrder (int)
                 5.6 - THE Portal_Database SHALL enforce cascading delete from
                         [invoice].Invoice to [invoice].InvoiceLine

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
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
