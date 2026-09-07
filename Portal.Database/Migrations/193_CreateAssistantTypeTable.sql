-- ============================================================
-- Migration 193: Create [notification].[AssistantType] reference table + seed
-- ============================================================
-- Purpose: Static registry of Digital Assistants. Seeded reference
--          data (exempt from CreatedAtUtc per the SQL schema convention).
--          Phase 1 seeds the Thank-You Assistant.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'AssistantType'
)
BEGIN
    CREATE TABLE [notification].[AssistantType]
    (
        [Id]               INT           NOT NULL,
        [Key]              NVARCHAR(50)  NOT NULL,
        [Name]             NVARCHAR(100) NOT NULL,
        [Description]      NVARCHAR(300) NOT NULL,
        [RecipientKind]    NVARCHAR(20)  NOT NULL,   -- 'Customer' | 'PortalUser'
        [IsCustomerFacing] BIT           NOT NULL,

        CONSTRAINT [PK_AssistantType] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_AssistantType_Key] UNIQUE ([Key]),
        CONSTRAINT [CK_AssistantType_RecipientKind] CHECK ([RecipientKind] IN ('Customer', 'PortalUser'))
    );
    PRINT 'Created [notification].[AssistantType] table.';
END
ELSE
BEGIN
    PRINT '[notification].[AssistantType] already exists.';
END
GO

-- Seed the Thank-You Assistant (idempotent, explicit Id)
MERGE [notification].[AssistantType] AS target
USING (VALUES
    (1, 'thank_you', 'Thank-You Assistant',
        'Sends a courteous payment confirmation the moment a customer pays an invoice.',
        'Customer', 1)
) AS source ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
    VALUES (source.[Id], source.[Key], source.[Name], source.[Description], source.[RecipientKind], source.[IsCustomerFacing]);
GO

PRINT 'Seeded [notification].[AssistantType] (thank_you).';
GO
