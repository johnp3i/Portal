-- ============================================================
-- Migration 133: Create Lead Source Type Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadSourceType] lookup table —
--          defines where a lead originated (Website, Referral, etc.).
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadSourceType'
)
BEGIN
    CREATE TABLE [sales].[LeadSourceType]
    (
        [Id]        INT             NOT NULL,
        [Name]      NVARCHAR(100)   NOT NULL,
        [IsActive]  BIT             NOT NULL CONSTRAINT [DF_LeadSourceType_IsActive] DEFAULT (1),

        CONSTRAINT [PK_LeadSourceType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadSourceType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadSourceType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadSourceType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 1)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (1, 'Website', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 2)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (2, 'Referral', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 3)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (3, 'Event', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 4)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (4, 'Cold Call', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 5)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (5, 'Partner', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 6)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (6, 'Social Media', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceType] WHERE [Id] = 7)
        INSERT INTO [sales].[LeadSourceType] ([Id], [Name], [IsActive]) VALUES (7, 'Other', 1);

    PRINT 'Seeded [sales].[LeadSourceType] data.';
END
GO
