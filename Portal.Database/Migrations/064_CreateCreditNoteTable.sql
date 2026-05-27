USE [Portal];
GO

/*
    Migration: 064_CreateCreditNoteTable
    Description: Creates the [credit].CreditNote table — a financial document issued against
                 a source invoice that formally reduces the amount owed by the customer.
                 Scoped to a Business tenant with foreign keys to Business, Invoice, Customer,
                 CreditNoteStatusType, and VatSubmissionPeriod.

    Requirements: 1.11 - WHEN a credit note is saved, THE Credit_Note_Service SHALL assign it
                          an initial status of Draft (CreditNoteStatusTypeId = 1).
                 1.12 - THE Credit_Note_Service SHALL scope all credit note records to the
                          current user's BusinessId for tenant isolation.
                 2.4  - THE Credit_Note_Repository SHALL enforce uniqueness of credit note
                          numbers within a BusinessId using a unique filtered index.
                 6.1  - THE Credit_Note SHALL store a mandatory VatSubmissionPeriodId
                          (non-null foreign key to [vat].[VatSubmissionPeriod]).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNote'
)
BEGIN
    CREATE TABLE [credit].[CreditNote]
    (
        [Id]                       INT             IDENTITY(1,1)  NOT NULL,
        [BusinessId]               INT                            NOT NULL,
        [InvoiceId]                INT                            NOT NULL,
        [CustomerId]               INT                            NOT NULL,
        [CreditNoteStatusTypeId]   INT                            NOT NULL  CONSTRAINT [DF_CreditNote_CreditNoteStatusTypeId] DEFAULT (1),
        [VatSubmissionPeriodId]    INT                            NOT NULL,
        [CreditNoteNumber]         NVARCHAR(20)                   NOT NULL,
        [IssueDate]                DATE                           NOT NULL,
        [Reason]                   NVARCHAR(1000)                 NOT NULL,
        [Subtotal]                 DECIMAL(18,2)                  NOT NULL,
        [TaxAmount]                DECIMAL(18,2)                  NOT NULL,
        [TotalAmount]              DECIMAL(18,2)                  NOT NULL,
        [IssuedAtUtc]              DATETIME                       NULL,
        [VoidedAtUtc]              DATETIME                       NULL,
        [CreatedByUserId]          NVARCHAR(450)                  NULL,
        [CreatedAtUtc]             DATETIME                       NOT NULL  CONSTRAINT [DF_CreditNote_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_CreditNote] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CreditNote_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_CreditNote_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]),
        CONSTRAINT [FK_CreditNote_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer] ([Id]),
        CONSTRAINT [FK_CreditNote_StatusType] FOREIGN KEY ([CreditNoteStatusTypeId]) REFERENCES [credit].[CreditNoteStatusType] ([Id]),
        CONSTRAINT [FK_CreditNote_VatPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNote_BusinessId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNote]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNote_BusinessId]
        ON [credit].[CreditNote] ([BusinessId]);
END
GO

-- Non-clustered index on InvoiceId for invoice-based lookups
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNote_InvoiceId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNote]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNote_InvoiceId]
        ON [credit].[CreditNote] ([InvoiceId]);
END
GO

-- Filtered unique index on BusinessId + CreditNoteNumber to enforce uniqueness
-- within a tenant, excluding Voided credit notes (CreditNoteStatusTypeId = 4)
-- so that voided numbers can be reused
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_CreditNote_BusinessId_CreditNoteNumber'
      AND [object_id] = OBJECT_ID('[credit].[CreditNote]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_CreditNote_BusinessId_CreditNoteNumber]
        ON [credit].[CreditNote] ([BusinessId], [CreditNoteNumber])
        WHERE [CreditNoteStatusTypeId] <> 4;
END
GO
