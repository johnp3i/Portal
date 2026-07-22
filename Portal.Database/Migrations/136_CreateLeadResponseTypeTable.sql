-- ============================================================
-- Migration 136: Create Lead Response Type Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadResponseType] lookup table —
--          defines the communication channel used when responding
--          to a lead (Email, Telephone, SMS, WhatsApp, In Person).
--          This is a shared reference table with no BusinessId column.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadResponseType'
)
BEGIN
    CREATE TABLE [sales].[LeadResponseType]
    (
        [Id]    INT             NOT NULL,
        [Name]  NVARCHAR(50)    NOT NULL,

        CONSTRAINT [PK_LeadResponseType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadResponseType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadResponseType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadResponseType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseType] WHERE [Id] = 1)
        INSERT INTO [sales].[LeadResponseType] ([Id], [Name]) VALUES (1, 'Email');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseType] WHERE [Id] = 2)
        INSERT INTO [sales].[LeadResponseType] ([Id], [Name]) VALUES (2, 'Telephone');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseType] WHERE [Id] = 3)
        INSERT INTO [sales].[LeadResponseType] ([Id], [Name]) VALUES (3, 'SMS');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseType] WHERE [Id] = 4)
        INSERT INTO [sales].[LeadResponseType] ([Id], [Name]) VALUES (4, 'WhatsApp');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseType] WHERE [Id] = 5)
        INSERT INTO [sales].[LeadResponseType] ([Id], [Name]) VALUES (5, 'In Person');

    PRINT 'Seeded [sales].[LeadResponseType] data.';
END
GO
