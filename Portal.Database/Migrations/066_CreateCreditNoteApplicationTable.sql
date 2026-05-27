USE [Portal];
GO

/*
    Migration: 066_CreateCreditNoteApplicationTable
    Description: Creates the [credit].[CreditNoteApplication] table — tracks when and how
                 a credit note amount was applied against the source invoice's outstanding
                 balance. Each record links a credit note to an invoice with the applied
                 amount, timestamp, and applying user.

    Requirements: 4.1 - WHEN the user applies a credit note, THE Credit_Note_Service SHALL
                          create a Credit_Note_Application record linking the credit note to
                          the source invoice with the applied amount, application date, and
                          applying user.
                 5.5 - WHEN a previously Applied credit note is voided, THE Credit_Note_Service
                          SHALL mark the associated Credit_Note_Application record as voided
                          (IsVoided = true).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNoteApplication'
)
BEGIN
    CREATE TABLE [credit].[CreditNoteApplication]
    (
        [Id]              INT             IDENTITY(1,1)  NOT NULL,
        [CreditNoteId]    INT                            NOT NULL,
        [InvoiceId]       INT                            NOT NULL,
        [AmountApplied]   DECIMAL(18,2)                  NOT NULL,
        [AppliedAtUtc]    DATETIME                       NOT NULL  CONSTRAINT [DF_CreditNoteApplication_AppliedAtUtc] DEFAULT (GETUTCDATE()),
        [AppliedByUserId] NVARCHAR(450)                  NULL,
        [IsVoided]        BIT                            NOT NULL  CONSTRAINT [DF_CreditNoteApplication_IsVoided] DEFAULT (0),
        [CreatedAtUtc]    DATETIME                       NOT NULL  CONSTRAINT [DF_CreditNoteApplication_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_CreditNoteApplication] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CreditNoteApplication_CreditNote] FOREIGN KEY ([CreditNoteId]) REFERENCES [credit].[CreditNote] ([Id]),
        CONSTRAINT [FK_CreditNoteApplication_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id])
    );
END
GO

-- Non-clustered index on CreditNoteId for credit-note-based lookups
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNoteApplication_CreditNoteId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNoteApplication]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNoteApplication_CreditNoteId]
        ON [credit].[CreditNoteApplication] ([CreditNoteId]);
END
GO

-- Non-clustered index on InvoiceId for invoice-based lookups
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNoteApplication_InvoiceId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNoteApplication]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNoteApplication_InvoiceId]
        ON [credit].[CreditNoteApplication] ([InvoiceId]);
END
GO
