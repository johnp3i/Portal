-- ============================================================
-- Migration 137: Create Meeting Type Table
-- ============================================================
-- Purpose: Creates the [sales].[MeetingType] lookup table —
--          defines the format of a scheduled meeting
--          (Online, On-Site, Phone Call, Video Call).
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingType'
)
BEGIN
    CREATE TABLE [sales].[MeetingType]
    (
        [Id]    INT             NOT NULL,
        [Name]  NVARCHAR(50)    NOT NULL,

        CONSTRAINT [PK_MeetingType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[MeetingType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[MeetingType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[MeetingType] WHERE [Id] = 1)
        INSERT INTO [sales].[MeetingType] ([Id], [Name]) VALUES (1, 'Online');

    IF NOT EXISTS (SELECT 1 FROM [sales].[MeetingType] WHERE [Id] = 2)
        INSERT INTO [sales].[MeetingType] ([Id], [Name]) VALUES (2, 'On-Site');

    IF NOT EXISTS (SELECT 1 FROM [sales].[MeetingType] WHERE [Id] = 3)
        INSERT INTO [sales].[MeetingType] ([Id], [Name]) VALUES (3, 'Phone Call');

    IF NOT EXISTS (SELECT 1 FROM [sales].[MeetingType] WHERE [Id] = 4)
        INSERT INTO [sales].[MeetingType] ([Id], [Name]) VALUES (4, 'Video Call');

    PRINT 'Seeded [sales].[MeetingType] data.';
END
GO
