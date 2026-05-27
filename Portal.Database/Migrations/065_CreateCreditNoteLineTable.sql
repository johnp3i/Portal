USE [Portal];
GO

/*
    Migration: 065_CreateCreditNoteLineTable
    Description: Creates the [credit].CreditNoteLine table — an individual priced item
                 within a Credit Note. Lines cascade-delete when their parent CreditNote
                 is removed.

    Requirements: 1.6 - THE Credit_Note_Service SHALL compute each Credit_Note_Line line total
                         as Quantity multiplied by UnitPrice, where Quantity must be a positive
                         integer between 1 and 10,000 and UnitPrice must be a positive decimal
                         between 0.01 and 999,999.99 with up to two decimal places.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNoteLine'
)
BEGIN
    CREATE TABLE [credit].[CreditNoteLine]
    (
        [Id]            INT             IDENTITY(1,1)  NOT NULL,
        [CreditNoteId]  INT                            NOT NULL,
        [Description]   NVARCHAR(500)                  NOT NULL,
        [Quantity]      DECIMAL(18,4)                  NOT NULL,
        [UnitPrice]     DECIMAL(18,2)                  NOT NULL,
        [VatRate]       DECIMAL(5,2)                   NOT NULL,
        [LineTotal]     DECIMAL(18,2)                  NOT NULL,
        [SortOrder]     INT                            NOT NULL  CONSTRAINT [DF_CreditNoteLine_SortOrder] DEFAULT (0),

        CONSTRAINT [PK_CreditNoteLine] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CreditNoteLine_CreditNote] FOREIGN KEY ([CreditNoteId]) REFERENCES [credit].[CreditNote] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Non-clustered index on CreditNoteId for parent-based lookups
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNoteLine_CreditNoteId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNoteLine]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNoteLine_CreditNoteId]
        ON [credit].[CreditNoteLine] ([CreditNoteId]);
END
GO
