-- ============================================================
-- Migration 010: Add LastLoginUtc to AspNetUsers
-- ============================================================
-- Purpose: Adds a nullable LastLoginUtc column to the AspNetUsers
--          table to track when a user last successfully signed in.
-- Database: [Portal.Membership]
-- ============================================================

USE [Portal.Membership]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'LastLoginUtc'
)
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
        ADD [LastLoginUtc] DATETIME2 NULL;

    PRINT 'Added [LastLoginUtc] column to [dbo].[AspNetUsers].';
END
ELSE
BEGIN
    PRINT '[dbo].[AspNetUsers].[LastLoginUtc] already exists.';
END
GO
