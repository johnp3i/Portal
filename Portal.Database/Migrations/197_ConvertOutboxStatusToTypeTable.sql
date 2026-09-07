-- ============================================================
-- Migration 197: Convert OutboxMessage.Status (string) to a reference table
-- ============================================================
-- Purpose: Replaces the free-text [Status] column on
--          [notification].[OutboxMessage] with an FK to a new
--          [notification].[OutboxMessageStatusType] reference table
--          (1=Pending, 2=Sent, 3=Failed), per the SQL naming convention
--          (a <Something>Id column implies a <Something> table).
-- Idempotent — safe to run multiple times.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

-- 1) Create the reference table + seed --------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'OutboxMessageStatusType'
)
BEGIN
    CREATE TABLE [notification].[OutboxMessageStatusType]
    (
        [Id]   INT          NOT NULL,
        [Name] NVARCHAR(20) NOT NULL,

        CONSTRAINT [PK_OutboxMessageStatusType] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_OutboxMessageStatusType_Name] UNIQUE ([Name])
    );
    PRINT 'Created [notification].[OutboxMessageStatusType] table.';
END
ELSE
BEGIN
    PRINT '[notification].[OutboxMessageStatusType] already exists.';
END
GO

MERGE [notification].[OutboxMessageStatusType] AS target
USING (VALUES
    (1, 'Pending'),
    (2, 'Sent'),
    (3, 'Failed')
) AS source ([Id], [Name])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name]) VALUES (source.[Id], source.[Name]);
GO

-- 2) Add the new FK column (nullable for now so we can backfill) -------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutboxMessageStatusTypeId'
      AND Object_ID = Object_ID(N'[notification].[OutboxMessage]')
)
BEGIN
    ALTER TABLE [notification].[OutboxMessage]
    ADD [OutboxMessageStatusTypeId] INT NULL;
    PRINT 'Added [OutboxMessageStatusTypeId] column.';
END
ELSE
BEGIN
    PRINT '[OutboxMessageStatusTypeId] already exists.';
END
GO

-- 3) Backfill from the existing [Status] string column (only if it still exists)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'Status'
      AND Object_ID = Object_ID(N'[notification].[OutboxMessage]')
)
BEGIN
    UPDATE [notification].[OutboxMessage]
    SET [OutboxMessageStatusTypeId] =
        CASE [Status]
            WHEN 'Pending' THEN 1
            WHEN 'Sent'    THEN 2
            WHEN 'Failed'  THEN 3
            ELSE 1
        END
    WHERE [OutboxMessageStatusTypeId] IS NULL;
    PRINT 'Backfilled OutboxMessageStatusTypeId from Status.';
END
GO

-- Any rows still NULL (e.g. fresh table with no Status column) default to Pending
UPDATE [notification].[OutboxMessage]
SET [OutboxMessageStatusTypeId] = 1
WHERE [OutboxMessageStatusTypeId] IS NULL;
GO

-- 4) Make the column NOT NULL + add FK ---------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'OutboxMessageStatusTypeId'
      AND Object_ID = Object_ID(N'[notification].[OutboxMessage]')
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE [notification].[OutboxMessage]
    ALTER COLUMN [OutboxMessageStatusTypeId] INT NOT NULL;
    PRINT 'Set [OutboxMessageStatusTypeId] to NOT NULL.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_OutboxMessage_StatusType'
      AND parent_object_id = OBJECT_ID('[notification].[OutboxMessage]')
)
BEGIN
    ALTER TABLE [notification].[OutboxMessage]
    ADD CONSTRAINT [FK_OutboxMessage_StatusType] FOREIGN KEY ([OutboxMessageStatusTypeId])
        REFERENCES [notification].[OutboxMessageStatusType]([Id]);
    PRINT 'Added FK_OutboxMessage_StatusType.';
END
GO

-- 5) Replace the old index and drop the old [Status] column + its default/check
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessage_Status_ScheduledForUtc'
           AND object_id = OBJECT_ID('[notification].[OutboxMessage]'))
BEGIN
    DROP INDEX [IX_OutboxMessage_Status_ScheduledForUtc] ON [notification].[OutboxMessage];
END
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessage_Status_FailedAtUtc'
           AND object_id = OBJECT_ID('[notification].[OutboxMessage]'))
BEGIN
    DROP INDEX [IX_OutboxMessage_Status_FailedAtUtc] ON [notification].[OutboxMessage];
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'Status'
      AND Object_ID = Object_ID(N'[notification].[OutboxMessage]')
)
BEGIN
    -- Drop the CHECK constraint on Status if present
    DECLARE @ck NVARCHAR(200);
    SELECT @ck = cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('[notification].[OutboxMessage]')
      AND cc.name = 'CK_OutboxMessage_Status';
    IF @ck IS NOT NULL
        EXEC('ALTER TABLE [notification].[OutboxMessage] DROP CONSTRAINT [CK_OutboxMessage_Status]');

    -- Drop the DEFAULT constraint on Status if present
    DECLARE @df NVARCHAR(200);
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    WHERE dc.parent_object_id = OBJECT_ID('[notification].[OutboxMessage]')
      AND dc.name = 'DF_OutboxMessage_Status';
    IF @df IS NOT NULL
        EXEC('ALTER TABLE [notification].[OutboxMessage] DROP CONSTRAINT [DF_OutboxMessage_Status]');

    ALTER TABLE [notification].[OutboxMessage] DROP COLUMN [Status];
    PRINT 'Dropped legacy [Status] column and its constraints.';
END
GO

-- 6) Recreate the hot-path indexes on the new column -------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessage_StatusTypeId_ScheduledForUtc'
               AND object_id = OBJECT_ID('[notification].[OutboxMessage]'))
BEGIN
    CREATE INDEX [IX_OutboxMessage_StatusTypeId_ScheduledForUtc]
        ON [notification].[OutboxMessage] ([OutboxMessageStatusTypeId], [ScheduledForUtc]);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OutboxMessage_StatusTypeId_FailedAtUtc'
               AND object_id = OBJECT_ID('[notification].[OutboxMessage]'))
BEGIN
    CREATE INDEX [IX_OutboxMessage_StatusTypeId_FailedAtUtc]
        ON [notification].[OutboxMessage] ([OutboxMessageStatusTypeId], [FailedAtUtc]);
END
GO

PRINT 'Migration 197 complete: OutboxMessage.Status converted to OutboxMessageStatusTypeId.';
GO
