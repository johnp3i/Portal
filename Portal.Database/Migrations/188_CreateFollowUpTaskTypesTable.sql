-- ============================================================
-- Migration 188: Create + seed [sales].[FollowUpTaskTypes] lookup
-- ============================================================
-- Purpose: Replace the free-text [sales].[FollowUpTask].[TaskType] column
--          with a proper reference table, mirroring [sales].[MeetingType].
--          The allowed task types (Call, Email, Follow-up, Meeting Prep, Other)
--          become seed rows. Id is TINYINT (only 5 values). Non-identity —
--          ids are assigned explicitly, matching MeetingType.
-- Schema: [sales]
-- Idempotent. Lookup table with static seed data — exempt from CreatedAtUtc.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'FollowUpTaskTypes'
)
BEGIN
    CREATE TABLE [sales].[FollowUpTaskTypes]
    (
        [Id]   TINYINT      NOT NULL,
        [Name] NVARCHAR(50) NOT NULL,
        CONSTRAINT [PK_FollowUpTaskTypes] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created table [sales].[FollowUpTaskTypes].';
END
ELSE
BEGIN
    PRINT '[sales].[FollowUpTaskTypes] already exists.';
END
GO

-- Idempotent seed, in UI order (Call, Email, Follow-up, Meeting Prep, Other).
-- Follow-up (Id 3) remains the create-form default.
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 1)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id], [Name]) VALUES (1, 'Call');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 2)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id], [Name]) VALUES (2, 'Email');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 3)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id], [Name]) VALUES (3, 'Follow-up');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 4)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id], [Name]) VALUES (4, 'Meeting Prep');
IF NOT EXISTS (SELECT 1 FROM [sales].[FollowUpTaskTypes] WHERE [Id] = 5)
    INSERT INTO [sales].[FollowUpTaskTypes] ([Id], [Name]) VALUES (5, 'Other');
GO

PRINT 'Seeded [sales].[FollowUpTaskTypes] (Call, Email, Follow-up, Meeting Prep, Other).';
GO
