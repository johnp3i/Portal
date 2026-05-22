/*
    Migration: 042_CreateInvoiceShareTable
    Description: Creates the InvoiceShare table in the [invoice] schema for sharing invoices
                 via secure, time-limited public links with HTML snapshots.

    Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [invoice].[InvoiceShare]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceShare'
)
BEGIN
    CREATE TABLE [invoice].[InvoiceShare]
    (
        [Id]              INT                IDENTITY(1,1)  NOT NULL,
        [InvoiceId]       INT                               NOT NULL,
        [BusinessId]      INT                               NOT NULL,
        [ShareToken]      NVARCHAR(128)                     NOT NULL,
        [SnapshotHtml]    NVARCHAR(MAX)                     NOT NULL,
        [CustomerEmail]   NVARCHAR(200)                     NOT NULL,
        [ExpiresAtUtc]    DATETIMEOFFSET                    NOT NULL,
        [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL  CONSTRAINT [DF_InvoiceShare_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedByUserId] NVARCHAR(450)                     NOT NULL,
        [IsActive]        BIT                               NOT NULL  CONSTRAINT [DF_InvoiceShare_IsActive] DEFAULT (1),

        CONSTRAINT [PK_InvoiceShare] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_InvoiceShare_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]),
        CONSTRAINT [FK_InvoiceShare_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [UX_InvoiceShare_ShareToken] UNIQUE NONCLUSTERED ([ShareToken])
    );
END
GO

-- =============================================================================
-- 2. Create nonclustered index on InvoiceId
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_InvoiceShare_InvoiceId'
      AND [object_id] = OBJECT_ID('[invoice].[InvoiceShare]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceShare_InvoiceId]
        ON [invoice].[InvoiceShare] ([InvoiceId]);
END
GO

-- =============================================================================
-- 3. Create nonclustered index on BusinessId
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_InvoiceShare_BusinessId'
      AND [object_id] = OBJECT_ID('[invoice].[InvoiceShare]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceShare_BusinessId]
        ON [invoice].[InvoiceShare] ([BusinessId]);
END
GO
