/*
    Migration: 040_AddWebsiteToBusinessProfile
    Description: Adds a [Website] column to [portal].[BusinessProfile] for displaying
                 the business website URL on invoice previews.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessProfile]')
      AND name = N'Website'
)
BEGIN
    ALTER TABLE [portal].[BusinessProfile]
        ADD [Website] NVARCHAR(500) NULL;
END
GO
