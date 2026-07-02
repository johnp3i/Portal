/*
    Migration: 099_AddOpenTrackingAndTestSendToPaymentReminderLog
    Description: Extends [reminder].[PaymentReminderLog] with open tracking columns
                 (TrackingToken, IsOpened, OpenedAtUtc, OpenCount, LastOpenedAtUtc)
                 and a test send flag (IsTestSend). Creates a unique filtered index
                 on TrackingToken for fast pixel-endpoint lookups, and a filtered
                 composite index excluding test sends for evaluation queries.

    Requirements: 6.1, 6.2, 6.3

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Add TrackingToken column (URL-safe Base64, max 64 chars for 32 bytes)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'TrackingToken'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [TrackingToken] NVARCHAR(64) NULL;
END
GO

-- =============================================================================
-- 2. Add IsOpened column (BIT, NOT NULL, default 0)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'IsOpened'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [IsOpened] BIT NOT NULL
            CONSTRAINT [DF_PaymentReminderLog_IsOpened] DEFAULT (0);
END
GO

-- =============================================================================
-- 3. Add OpenedAtUtc column (DATETIME, nullable)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'OpenedAtUtc'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [OpenedAtUtc] DATETIME NULL;
END
GO

-- =============================================================================
-- 4. Add OpenCount column (INT, NOT NULL, default 0)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'OpenCount'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [OpenCount] INT NOT NULL
            CONSTRAINT [DF_PaymentReminderLog_OpenCount] DEFAULT (0);
END
GO

-- =============================================================================
-- 5. Add LastOpenedAtUtc column (DATETIME, nullable)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'LastOpenedAtUtc'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [LastOpenedAtUtc] DATETIME NULL;
END
GO

-- =============================================================================
-- 6. Add IsTestSend column (BIT, NOT NULL, default 0)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[reminder].[PaymentReminderLog]')
      AND name = N'IsTestSend'
)
BEGIN
    ALTER TABLE [reminder].[PaymentReminderLog]
        ADD [IsTestSend] BIT NOT NULL
            CONSTRAINT [DF_PaymentReminderLog_IsTestSend] DEFAULT (0);
END
GO

-- =============================================================================
-- 7. Create unique filtered index on TrackingToken (non-null tokens only)
--    Enables O(1) lookup for the tracking pixel endpoint.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'UX_PaymentReminderLog_TrackingToken'
      AND [object_id] = OBJECT_ID('[reminder].[PaymentReminderLog]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentReminderLog_TrackingToken]
        ON [reminder].[PaymentReminderLog]([TrackingToken])
        WHERE [TrackingToken] IS NOT NULL;
END
GO

-- =============================================================================
-- 8. Create filtered index on (BusinessId, InvoiceId, EscalationTier)
--    excluding test sends — supports efficient evaluation queries.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_PaymentReminderLog_BusinessId_IsTestSend'
      AND [object_id] = OBJECT_ID('[reminder].[PaymentReminderLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReminderLog_BusinessId_IsTestSend]
        ON [reminder].[PaymentReminderLog]([BusinessId], [InvoiceId], [EscalationTier])
        WHERE [IsTestSend] = 0;
END
GO
