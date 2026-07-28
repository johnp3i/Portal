-- ============================================================
-- Migration: 158_SeedCardPaymentMethodType
-- Description: Adds "Card" to the PaymentMethodType reference table
--              for Stripe Connect card payments.
-- ============================================================

USE [Guardian]
GO

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Name] = 'Card')
BEGIN
    INSERT INTO [revenue].[PaymentMethodType] ([Name], [IsActive])
    VALUES ('Card', 1);
END
GO
