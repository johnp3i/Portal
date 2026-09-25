-- ============================================================
-- Migration 211: Add Sentiment + IsFollowUp to ProspectActivity
-- ============================================================
-- Purpose: Enriches the prospect activity timeline so entries can carry an
--          interaction signal (Sentiment) and be marked as a follow-up.
--            [Sentiment]  TINYINT NULL  - 1 Positive, 2 Neutral, 3 Negative
--            [IsFollowUp] BIT NOT NULL  - flags an entry that needs follow-up
--          Sentiment feeds the scoring-validation loop (did high-scored
--          prospects actually produce positive interactions / conversions?).
-- Schema: [sales].[ProspectActivity]
-- Idempotent.
-- ============================================================

USE [Portal]
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'ProspectActivity')
BEGIN
    IF COL_LENGTH('[sales].[ProspectActivity]', 'Sentiment') IS NULL
    BEGIN
        ALTER TABLE [sales].[ProspectActivity]
            ADD [Sentiment] TINYINT NULL; -- 1 Positive, 2 Neutral, 3 Negative
        PRINT 'Added [sales].[ProspectActivity].[Sentiment].';
    END
    ELSE
        PRINT '[sales].[ProspectActivity].[Sentiment] already exists.';

    IF COL_LENGTH('[sales].[ProspectActivity]', 'IsFollowUp') IS NULL
    BEGIN
        ALTER TABLE [sales].[ProspectActivity]
            ADD [IsFollowUp] BIT NOT NULL CONSTRAINT [DF_ProspectActivity_IsFollowUp] DEFAULT (0);
        PRINT 'Added [sales].[ProspectActivity].[IsFollowUp].';
    END
    ELSE
        PRINT '[sales].[ProspectActivity].[IsFollowUp] already exists.';
END
ELSE
BEGIN
    PRINT '[sales].[ProspectActivity] does not exist — run migration 210 first.';
END
GO
