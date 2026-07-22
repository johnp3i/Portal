-- ============================================================
-- Migration 138: Create Contact Table
-- ============================================================
-- Purpose: Creates the [sales].[Contact] table — a business-specific
--          directory of contacts (people or companies) that the business
--          interacts with in its sales pipeline. Contacts are linked to
--          leads, meetings, and responses.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'Contact'
)
BEGIN
    CREATE TABLE [sales].[Contact]
    (
        [Id]            INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]    INT             NOT NULL,
        [FirstName]     NVARCHAR(100)   NOT NULL,
        [LastName]      NVARCHAR(100)   NULL,
        [Email]         NVARCHAR(320)   NULL,
        [PhoneNumber]   NVARCHAR(30)    NULL,
        [CompanyName]   NVARCHAR(200)   NULL,
        [JobTitle]      NVARCHAR(100)   NULL,
        [Country]       NVARCHAR(100)   NULL,
        [Notes]         NVARCHAR(MAX)   NULL,
        [IsActive]      BIT             NOT NULL CONSTRAINT [DF_SalesContact_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME        NOT NULL CONSTRAINT [DF_SalesContact_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_SalesContact] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_SalesContact_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id])
    );

    PRINT 'Created [sales].[Contact] table.';
END
ELSE
BEGIN
    PRINT '[sales].[Contact] already exists.';
END
GO

-- Partial unique index: one email per business (where email is provided)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SalesContact_BusinessId_Email'
      AND object_id = OBJECT_ID('[sales].[Contact]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_SalesContact_BusinessId_Email]
        ON [sales].[Contact] ([BusinessId], [Email])
        WHERE [Email] IS NOT NULL;

    PRINT 'Created partial unique index UX_SalesContact_BusinessId_Email.';
END
GO

-- Partial unique index: one phone number per business (where phone is provided)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SalesContact_BusinessId_PhoneNumber'
      AND object_id = OBJECT_ID('[sales].[Contact]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_SalesContact_BusinessId_PhoneNumber]
        ON [sales].[Contact] ([BusinessId], [PhoneNumber])
        WHERE [PhoneNumber] IS NOT NULL;

    PRINT 'Created partial unique index UX_SalesContact_BusinessId_PhoneNumber.';
END
GO
