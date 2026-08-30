-- ============================================================
-- Add IsReminderSystemEnabled master toggle to Business table
-- Default TRUE — existing businesses keep reminders active
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'Business' AND COLUMN_NAME = 'IsReminderSystemEnabled')
BEGIN
    ALTER TABLE [portal].[Business]
        ADD [IsReminderSystemEnabled] BIT NOT NULL CONSTRAINT [DF_Business_IsReminderSystemEnabled] DEFAULT (1);
END
GO
