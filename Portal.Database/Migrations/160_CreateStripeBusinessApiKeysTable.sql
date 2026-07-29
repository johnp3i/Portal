-- ============================================================
-- Migration: 160_CreateStripeBusinessApiKeysTable
-- Description: Creates the [stripe].[BusinessApiKeys] table
--              for storing encrypted Stripe API keys per business.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'stripe'
      AND TABLE_NAME = 'BusinessApiKeys'
)
BEGIN
    CREATE TABLE [stripe].[BusinessApiKeys]
    (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [BusinessId]        INT NOT NULL,
        [KeyType]           NVARCHAR(50) NOT NULL,
        [EncryptedValue]    NVARCHAR(MAX) NOT NULL,
        [CreatedAtUtc]      DATETIME NOT NULL CONSTRAINT [DF_BusinessApiKeys_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]      DATETIME NULL,

        CONSTRAINT [PK_BusinessApiKeys] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessApiKeys_Business] FOREIGN KEY ([BusinessId])
            REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [UQ_BusinessApiKeys_BusinessId_KeyType] UNIQUE ([BusinessId], [KeyType])
    );
END
GO
