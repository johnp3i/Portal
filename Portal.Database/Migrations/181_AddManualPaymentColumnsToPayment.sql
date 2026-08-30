-- ============================================================
-- Migration 181: Add manual payment metadata columns to [billing].[Payment]
-- ============================================================
-- Purpose: Adds Reference (payment ref number), Notes (free-text),
--          and RecordedByUserId (who recorded the payment) to support
--          manual/offline payment recording alongside Stripe payments.
--          All columns are nullable — existing Stripe rows are unaffected.
-- Schema: [billing]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[billing].[Payment]') AND name = 'Reference'
)
BEGIN
    ALTER TABLE [billing].[Payment]
        ADD [Reference] NVARCHAR(200) NULL;
    PRINT 'Added [Reference] column to [billing].[Payment].';
END
ELSE
BEGIN
    PRINT '[Reference] column already exists on [billing].[Payment].';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[billing].[Payment]') AND name = 'Notes'
)
BEGIN
    ALTER TABLE [billing].[Payment]
        ADD [Notes] NVARCHAR(500) NULL;
    PRINT 'Added [Notes] column to [billing].[Payment].';
END
ELSE
BEGIN
    PRINT '[Notes] column already exists on [billing].[Payment].';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[billing].[Payment]') AND name = 'RecordedByUserId'
)
BEGIN
    ALTER TABLE [billing].[Payment]
        ADD [RecordedByUserId] NVARCHAR(450) NULL;
    PRINT 'Added [RecordedByUserId] column to [billing].[Payment].';
END
ELSE
BEGIN
    PRINT '[RecordedByUserId] column already exists on [billing].[Payment].';
END
GO
