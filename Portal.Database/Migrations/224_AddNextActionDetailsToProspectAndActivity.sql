/*
    Migration: 224_AddNextActionDetailsToProspectAndActivity
    Description: Adds structured next-action detail columns to [sales].[ProspectActivity] and mirrors
                 them onto [sales].[Prospect]:
                   - NextActionTime        TIME         NULL  (e.g. 10:00 — the time to call)
                   - NextActionContactName NVARCHAR(200) NULL (e.g. the person who called back)
                   - NextActionPhone       NVARCHAR(30)  NULL (e.g. the mobile they called from)
                 These let a follow-up capture WHO to contact, on WHICH number, and at WHAT time —
                 instead of packing it all into the free-text NextAction. The Prospect copies mirror
                 the latest follow-up so the working list and dashboard brief can show them without
                 joining the activity timeline. All nullable, so existing rows are unaffected.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- ── [sales].[ProspectActivity] ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'NextActionTime'
)
BEGIN
    ALTER TABLE [sales].[ProspectActivity] ADD [NextActionTime] TIME NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'NextActionContactName'
)
BEGIN
    ALTER TABLE [sales].[ProspectActivity] ADD [NextActionContactName] NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'NextActionPhone'
)
BEGIN
    ALTER TABLE [sales].[ProspectActivity] ADD [NextActionPhone] NVARCHAR(30) NULL;
END
GO

-- ── [sales].[Prospect] (mirror of the latest follow-up) ─────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[Prospect]') AND name = N'NextActionTime'
)
BEGIN
    ALTER TABLE [sales].[Prospect] ADD [NextActionTime] TIME NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[Prospect]') AND name = N'NextActionContactName'
)
BEGIN
    ALTER TABLE [sales].[Prospect] ADD [NextActionContactName] NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[Prospect]') AND name = N'NextActionPhone'
)
BEGIN
    ALTER TABLE [sales].[Prospect] ADD [NextActionPhone] NVARCHAR(30) NULL;
END
GO
