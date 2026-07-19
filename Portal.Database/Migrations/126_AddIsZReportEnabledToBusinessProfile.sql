/*
    Migration: 126_AddIsZReportEnabledToBusinessProfile
    Description: Adds IsZReportEnabled column to [portal].[BusinessProfile].
                 Defaults to 0 (disabled). Controls whether Z-Report / Revenue Ingestion
                 features are visible and active for the business.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[BusinessProfile]')
      AND name = N'IsZReportEnabled'
)
BEGIN
    ALTER TABLE [portal].[BusinessProfile]
        ADD [IsZReportEnabled] BIT NOT NULL CONSTRAINT [DF_BusinessProfile_IsZReportEnabled] DEFAULT (0);
END
GO
