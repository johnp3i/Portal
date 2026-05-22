-- Migration 042: Add CreatedAtUtc to ExpenseCategory table
-- Tracks when an expense category was created for audit purposes.

ALTER TABLE [purchase].[ExpenseCategory]
ADD [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT DF_ExpenseCategory_CreatedAtUtc DEFAULT GETUTCDATE();
GO
