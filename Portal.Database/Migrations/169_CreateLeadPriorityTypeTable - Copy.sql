-- ============================================================
-- Migration 169: Create Lead Priority Type Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadPriorityType] lookup table —
--          defines priority levels (Hot, Warm, Cold) that can be
--          assigned to leads for visual prioritisation on the
--          pipeline Kanban board.
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE Portal
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadPriorityType'
)
BEGIN
    CREATE TABLE [sales].[LeadPriorityType]
    (
        [Id]            INT             IDENTITY(1,1) NOT NULL,
        [Name]          NVARCHAR(50)    NOT NULL,
        [DisplayOrder]  INT             NOT NULL,
        [Colour]        NVARCHAR(10)    NOT NULL,
        [CreatedAtUtc]  DATETIME        NOT NULL CONSTRAINT [DF_LeadPriorityType_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_LeadPriorityType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadPriorityType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadPriorityType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadPriorityType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadPriorityType] WHERE [Name] = 'Hot')
        INSERT INTO [sales].[LeadPriorityType] ([Name], [DisplayOrder], [Colour]) VALUES ('Hot', 1, '#E53E3E');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadPriorityType] WHERE [Name] = 'Warm')
        INSERT INTO [sales].[LeadPriorityType] ([Name], [DisplayOrder], [Colour]) VALUES ('Warm', 2, '#DD6B20');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadPriorityType] WHERE [Name] = 'Cold')
        INSERT INTO [sales].[LeadPriorityType] ([Name], [DisplayOrder], [Colour]) VALUES ('Cold', 3, '#3182CE');

    PRINT 'Seeded [sales].[LeadPriorityType] data.';
END
GO
