/*
    Migration: 081_CreatePlatformConfigTable
    Description: Creates [dbo].[PlatformConfig] — a key-value store for platform-wide
                 configuration settings such as feature flags and display text. Supports
                 the promo code system and future centralized configuration needs.

    Requirements: 1.1 - THE [dbo].[PlatformConfig] table SHALL contain: Key (NVARCHAR(256),
                         NOT NULL, PRIMARY KEY), Value (NVARCHAR(MAX), NOT NULL),
                         Description (NVARCHAR(500), NULL), and LastModifiedAtUtc
                         (DATETIME, NOT NULL, default GETUTCDATE()).
                 8.1 - THE platform SHALL provide a PlatformConfigService that reads
                         configuration values from the [dbo].[PlatformConfig] table by key.

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[PlatformConfig]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'PlatformConfig'
)
BEGIN
    CREATE TABLE [dbo].[PlatformConfig]
    (
        [Key]               NVARCHAR(256)   NOT NULL,
        [Value]             NVARCHAR(MAX)   NOT NULL,
        [Description]       NVARCHAR(500)   NULL,
        [LastModifiedAtUtc]  DATETIME        NOT NULL    CONSTRAINT [DF_PlatformConfig_LastModifiedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PlatformConfig] PRIMARY KEY CLUSTERED ([Key])
    );
END
GO
