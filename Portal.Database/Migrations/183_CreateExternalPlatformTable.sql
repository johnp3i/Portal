-- ============================================================
-- Migration 183: Create ExternalPlatform table
-- ============================================================
-- Purpose: Registers external systems (other billing platforms,
--          online stores) that a Business imports sales from.
--          Identified by an invoice PlatformCode (e.g. GRD) matching
--          the {PlatformCode}-INV-{yyyy}-{NNNN} invoice-number format.
--          Distinct from [revenue].RevenueSource (a POS device/register).
-- Schema: [revenue]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'ExternalPlatform'
)
BEGIN
    CREATE TABLE [revenue].[ExternalPlatform]
    (
        [Id]           INT           IDENTITY(1,1) NOT NULL,
        [BusinessId]   INT           NOT NULL,
        [Name]         NVARCHAR(200) NOT NULL,
        [PlatformCode] NVARCHAR(10)  NOT NULL,
        [Description]  NVARCHAR(500) NULL,
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_ExternalPlatform_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME      NOT NULL CONSTRAINT [DF_ExternalPlatform_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ExternalPlatform] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ExternalPlatform_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [UQ_ExternalPlatform_Business_Code] UNIQUE ([BusinessId], [PlatformCode])
    );

    PRINT 'Created [revenue].[ExternalPlatform] table.';
END
ELSE
BEGIN
    PRINT '[revenue].[ExternalPlatform] already exists.';
END
GO
