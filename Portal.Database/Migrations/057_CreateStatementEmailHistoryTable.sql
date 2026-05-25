/*
    Migration: 057_CreateStatementEmailHistoryTable
    Description: Creates the [customer].[StatementEmailHistory] table — stores a record
                 of each statement of account emailed to a customer, including the period
                 covered, recipient, and sender details.

    Requirements: 11.6 - WHEN a statement is successfully emailed, THE Statement_Service SHALL
                         persist an email history record containing: BusinessId, CustomerId,
                         statement period from-date, statement period to-date, recipient email
                         address, the UserId of the sender, and the timestamp of sending

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'customer'
      AND TABLE_NAME = 'StatementEmailHistory'
)
BEGIN
    CREATE TABLE [customer].[StatementEmailHistory]
    (
        [Id]              INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]      INT                           NOT NULL,
        [CustomerId]      INT                           NOT NULL,
        [FromDate]        DATE                          NOT NULL,
        [ToDate]          DATE                          NOT NULL,
        [RecipientEmail]  NVARCHAR(256)                 NOT NULL,
        [SentByUserId]    NVARCHAR(450)                 NOT NULL,
        [SentAtUtc]       DATETIME                      NOT NULL,
        [CreatedAtUtc]    DATETIME                      NOT NULL  CONSTRAINT [DF_StatementEmailHistory_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_StatementEmailHistory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_StatementEmailHistory_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_StatementEmailHistory_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer] ([Id])
    );
END
GO

-- Nonclustered index on (CustomerId, BusinessId) with SentAtUtc included for email history queries
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_StatementEmailHistory_CustomerId_BusinessId'
      AND [object_id] = OBJECT_ID('[customer].[StatementEmailHistory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_StatementEmailHistory_CustomerId_BusinessId]
        ON [customer].[StatementEmailHistory] ([CustomerId], [BusinessId])
        INCLUDE ([SentAtUtc]);
END
GO
