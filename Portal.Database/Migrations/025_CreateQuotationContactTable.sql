/*
    Migration: 025_CreateQuotationContactTable
    Description: Creates [quotation].[QuotationContact] — a reusable directory of people
                 who prepare quotations for a business. Adds QuotationContactId FK to Quotation.

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [quotation].[QuotationContact]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationContact'
)
BEGIN
    CREATE TABLE [quotation].[QuotationContact]
    (
        [Id]              INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]      INT                           NOT NULL,
        [UserId]          NVARCHAR(450)                 NULL,
        [Name]            NVARCHAR(200)                 NOT NULL,
        [Email]           NVARCHAR(200)                 NULL,
        [TelephoneNumber] NVARCHAR(30)                  NULL,
        [IsActive]        BIT                           NOT NULL  CONSTRAINT [DF_QuotationContact_IsActive] DEFAULT (1),
        [CreatedAtUtc]    DATETIME2                     NOT NULL  CONSTRAINT [DF_QuotationContact_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_QuotationContact] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_QuotationContact_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_QuotationContact_BusinessId'
      AND [object_id] = OBJECT_ID('[quotation].[QuotationContact]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_QuotationContact_BusinessId]
        ON [quotation].[QuotationContact] ([BusinessId]);
END
GO

-- =============================================================================
-- 2. Add QuotationContactId to [quotation].[Quotation]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[Quotation]')
      AND name = N'QuotationContactId'
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD [QuotationContactId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_Quotation_QuotationContact'
      AND [parent_object_id] = OBJECT_ID('[quotation].[Quotation]')
)
BEGIN
    ALTER TABLE [quotation].[Quotation]
        ADD CONSTRAINT [FK_Quotation_QuotationContact]
        FOREIGN KEY ([QuotationContactId]) REFERENCES [quotation].[QuotationContact] ([Id])
        ON DELETE NO ACTION;
END
GO
