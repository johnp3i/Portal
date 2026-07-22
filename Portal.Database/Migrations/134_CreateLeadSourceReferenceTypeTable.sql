-- ============================================================
-- Migration 134: Create Lead Source Reference Type Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadSourceReferenceType] lookup table —
--          defines the specific channel or campaign within a lead source
--          (Facebook, Instagram, LinkedIn, etc.).
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadSourceReferenceType'
)
BEGIN
    CREATE TABLE [sales].[LeadSourceReferenceType]
    (
        [Id]        INT             NOT NULL,
        [Name]      NVARCHAR(100)   NOT NULL,
        [IsActive]  BIT             NOT NULL CONSTRAINT [DF_LeadSourceReferenceType_IsActive] DEFAULT (1),

        CONSTRAINT [PK_LeadSourceReferenceType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadSourceReferenceType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadSourceReferenceType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadSourceReferenceType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 1)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (1, 'Facebook', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 2)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (2, 'Instagram', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 3)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (3, 'LinkedIn', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 4)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (4, 'Google Ads', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 5)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (5, 'Twitter/X', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 6)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (6, 'Email Campaign', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 7)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (7, 'Direct', 1);

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadSourceReferenceType] WHERE [Id] = 8)
        INSERT INTO [sales].[LeadSourceReferenceType] ([Id], [Name], [IsActive]) VALUES (8, 'Other', 1);

    PRINT 'Seeded [sales].[LeadSourceReferenceType] data.';
END
GO
