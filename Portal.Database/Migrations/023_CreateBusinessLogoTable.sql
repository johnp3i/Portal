/*
    Migration: 023_CreateBusinessLogoTable
    Description: Creates the [portal].[BusinessLogo] table for managing uploaded logo images
                 per business, used in proposal branding.

    Requirements: 10.1, 10.3, 10.5

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [portal].[BusinessLogo]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal'
      AND TABLE_NAME = 'BusinessLogo'
)
BEGIN
    CREATE TABLE [portal].[BusinessLogo]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [DisplayName]   NVARCHAR(200)                 NOT NULL,
        [FileName]      NVARCHAR(500)                 NOT NULL,
        [ContentType]   NVARCHAR(100)                 NOT NULL,
        [FileSizeBytes] BIGINT                        NOT NULL,
        [PublicUrl]     NVARCHAR(1000)                NOT NULL,
        [CreatedAtUtc]  DATETIME2                     NOT NULL  CONSTRAINT [DF_BusinessLogo_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_BusinessLogo] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessLogo_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- =============================================================================
-- 2. Create index on BusinessId
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_BusinessLogo_BusinessId'
      AND [object_id] = OBJECT_ID('[portal].[BusinessLogo]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BusinessLogo_BusinessId]
        ON [portal].[BusinessLogo] ([BusinessId]);
END
GO
