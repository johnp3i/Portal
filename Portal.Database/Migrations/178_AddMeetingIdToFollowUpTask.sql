-- ============================================================
-- Migration 178: Add MeetingId to FollowUpTask
-- ============================================================
-- Purpose: Links follow-up tasks to the meeting that spawned
--          them via a nullable FK. Enables meeting-scoped task
--          creation, viewing, and count badges.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

-- Add nullable MeetingId column with FK to Meeting
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTask' AND COLUMN_NAME = 'MeetingId'
)
BEGIN
    ALTER TABLE [sales].[FollowUpTask]
        ADD [MeetingId] INT NULL;

    ALTER TABLE [sales].[FollowUpTask]
        ADD CONSTRAINT [FK_FollowUpTask_Meeting]
            FOREIGN KEY ([MeetingId])
            REFERENCES [sales].[Meeting] ([Id]);

    PRINT 'Added [MeetingId] column and FK constraint to [sales].[FollowUpTask].';
END
ELSE
BEGIN
    PRINT '[MeetingId] column already exists on [sales].[FollowUpTask].';
END
GO

-- Filtered index for meeting-scoped lookups (only rows with a meeting link)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_FollowUpTask_MeetingId' AND object_id = OBJECT_ID('[sales].[FollowUpTask]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FollowUpTask_MeetingId]
        ON [sales].[FollowUpTask] ([MeetingId])
        WHERE [MeetingId] IS NOT NULL;

    PRINT 'Created filtered index [IX_FollowUpTask_MeetingId].';
END
ELSE
BEGIN
    PRINT 'Index [IX_FollowUpTask_MeetingId] already exists.';
END
GO
