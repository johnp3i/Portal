-- ============================================================
-- Migration: 157_CreateStripeCheckoutSessionTable
-- Description: Creates the [stripe].[CheckoutSession] table
--              for tracking Stripe Checkout Sessions and fees.
-- ============================================================

USE [Guardian]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'stripe'
      AND TABLE_NAME = 'CheckoutSession'
)
BEGIN
    CREATE TABLE [stripe].[CheckoutSession]
    (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [BusinessId]                INT NOT NULL,
        [InvoiceId]                 INT NOT NULL,
        [StripeSessionId]           NVARCHAR(255) NOT NULL,
        [Amount]                    DECIMAL(18,2) NOT NULL,
        [StripeFeeAmount]           DECIMAL(18,2) NULL,
        [NetAmount]                 DECIMAL(18,2) NULL,
        [Currency]                  NVARCHAR(3) NOT NULL DEFAULT 'EUR',
        [Status]                    NVARCHAR(50) NOT NULL DEFAULT 'pending',
        [StripePaymentIntentId]     NVARCHAR(255) NULL,
        [StripeChargeId]            NVARCHAR(255) NULL,
        [PaymentId]                 INT NULL,
        [CustomerName]              NVARCHAR(255) NULL,
        [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAtUtc]            DATETIME NULL,

        CONSTRAINT [PK_CheckoutSession] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_CheckoutSession_StripeSessionId] UNIQUE ([StripeSessionId])
    );
END
GO
