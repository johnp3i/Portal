-- ============================================================
-- Migration: 156_CreateStripeConnectedAccountTable
-- Description: Creates the [stripe].[ConnectedAccount] table
--              for storing Stripe Connect linked accounts per business.
-- ============================================================

USE [Guardian]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'stripe'
      AND TABLE_NAME = 'ConnectedAccount'
)
BEGIN
    CREATE TABLE [stripe].[ConnectedAccount]
    (
        [Id]                    INT IDENTITY(1,1) NOT NULL,
        [BusinessId]            INT NOT NULL,
        [StripeAccountId]       NVARCHAR(255) NOT NULL,
        [IsActive]              BIT NOT NULL DEFAULT 1,
        [ConnectedAtUtc]        DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [DisconnectedAtUtc]     DATETIME NULL,
        [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_ConnectedAccount] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ConnectedAccount_Business] FOREIGN KEY ([BusinessId])
            REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [UQ_ConnectedAccount_Business] UNIQUE ([BusinessId])
    );
END
GO
