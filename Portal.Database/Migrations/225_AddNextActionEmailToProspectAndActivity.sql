/*
    Migration: 225_AddNextActionEmailToProspectAndActivity
    Description: Adds a NextActionEmail column to [sales].[ProspectActivity] and mirrors it onto
                 [sales].[Prospect], completing the structured next-action contact fields alongside
                 the existing NextActionContactName / NextActionPhone (migration 224). Lets a
                 follow-up capture the email to use, shown in the UI as a privacy-safe icon badge
                 (presence only). Nullable, so existing rows are unaffected.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'NextActionEmail'
)
BEGIN
    ALTER TABLE [sales].[ProspectActivity] ADD [NextActionEmail] NVARCHAR(320) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[Prospect]') AND name = N'NextActionEmail'
)
BEGIN
    ALTER TABLE [sales].[Prospect] ADD [NextActionEmail] NVARCHAR(320) NULL;
END
GO
