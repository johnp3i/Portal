-- ============================================================
-- Migration 135: Create Lead Status Type Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadStatusType] lookup table —
--          defines pipeline stages that track lead progression.
--          Includes DisplayOrder for Kanban column ordering,
--          Colour for visual indicators, and IsTerminal flag
--          for end-state stages (Won, Lost, Inactive).
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadStatusType'
)
BEGIN
    CREATE TABLE [sales].[LeadStatusType]
    (
        [Id]            INT             NOT NULL,
        [Name]          NVARCHAR(50)    NOT NULL,
        [DisplayOrder]  INT             NOT NULL,
        [Colour]        NVARCHAR(7)     NULL,
        [IsTerminal]    BIT             NOT NULL CONSTRAINT [DF_LeadStatusType_IsTerminal] DEFAULT (0),

        CONSTRAINT [PK_LeadStatusType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadStatusType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadStatusType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 1)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (1, 'New', 1, '#57B8E8', 0);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 2)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (2, 'Contacted', 2, '#0D5EA6', 0);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 3)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (3, 'Follow-Up', 3, '#C8912E', 0);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 4)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (4, 'Meeting Scheduled', 4, '#6B5CE7', 0);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 5)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (5, 'Proposal Sent', 5, '#0D5EA6', 0);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 6)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (6, 'Won', 6, '#129867', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 7)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (7, 'Lost', 7, '#C24A4A', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadStatusType] WHERE [Id] = 8)
        INSERT INTO [sales].[LeadStatusType] ([Id], [Name], [DisplayOrder], [Colour], [IsTerminal]) VALUES (8, 'Inactive', 8, '#8a9bab', 1);

    PRINT 'Seeded [sales].[LeadStatusType] data.';
END
GO
