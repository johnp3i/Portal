/*
    Migration: 223_AddSalesBriefLookAheadDaysToBusinessProfile
    Description: Adds SalesBriefLookAheadDays column to [portal].[BusinessProfile].
                 Per-business look-ahead window (in days) for the dashboard "Today's Brief" and the
                 pipeline "Today's Actions" panel — controls how far ahead upcoming sales tasks and
                 prospect next-actions are surfaced. NULL means "use the platform default" (7 days),
                 so existing businesses keep the current behaviour until they set a value.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessProfile]')
      AND name = N'SalesBriefLookAheadDays'
)
BEGIN
    ALTER TABLE [portal].[BusinessProfile]
        ADD [SalesBriefLookAheadDays] INT NULL;
END
GO
