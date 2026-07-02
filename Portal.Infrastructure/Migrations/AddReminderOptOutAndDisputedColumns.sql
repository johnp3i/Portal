-- ============================================================
-- Add IsReminderOptedOut to Customer and IsDisputed to Invoice
-- ============================================================

USE [Portal]
GO

ALTER TABLE [customer].[Customer]
    ADD [IsReminderOptedOut] BIT NOT NULL DEFAULT 0
GO

ALTER TABLE [invoice].[Invoice]
    ADD [IsDisputed] BIT NOT NULL DEFAULT 0
GO
