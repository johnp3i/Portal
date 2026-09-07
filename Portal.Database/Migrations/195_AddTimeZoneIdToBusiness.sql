-- ============================================================
-- Migration 195: Add TimeZoneId to [portal].[Business]
-- ============================================================
-- Purpose: Gives each business an optional time zone (FK to
--          [notification].[TimeZone]) for working-hours scheduling.
--          NULL falls back to the platform default at runtime.
-- Schema: [portal]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'TimeZoneId'
      AND Object_ID = Object_ID(N'[portal].[Business]')
)
BEGIN
    ALTER TABLE [portal].[Business]
    ADD [TimeZoneId] INT NULL;
    PRINT 'Added [TimeZoneId] column to [portal].[Business].';
END
ELSE
BEGIN
    PRINT '[portal].[Business].[TimeZoneId] already exists.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Business_TimeZone'
      AND parent_object_id = OBJECT_ID('[portal].[Business]')
)
BEGIN
    ALTER TABLE [portal].[Business]
    ADD CONSTRAINT [FK_Business_TimeZone] FOREIGN KEY ([TimeZoneId])
        REFERENCES [notification].[TimeZone]([Id]);
    PRINT 'Added FK_Business_TimeZone constraint.';
END
ELSE
BEGIN
    PRINT 'FK_Business_TimeZone already exists.';
END
GO
