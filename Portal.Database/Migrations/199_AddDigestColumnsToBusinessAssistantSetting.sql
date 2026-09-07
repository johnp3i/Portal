-- ============================================================
-- Migration 199: Add scheduled-digest columns to BusinessAssistantSetting
-- ============================================================
-- Purpose: Scheduled digests (Group 3) need a per-business send schedule, a
--          configurable recipient, and (snapshot) a figure selection. These
--          columns are additive and nullable/defaulted, so existing rows and
--          the customer-facing assistants (e.g. Thank-You) are unaffected.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

-- SendDayOfWeek: 0 = Sunday .. 6 = Saturday. NULL -> default (Monday) at read time.
IF COL_LENGTH('notification.BusinessAssistantSetting', 'SendDayOfWeek') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [SendDayOfWeek] TINYINT NULL;
    PRINT 'Added [SendDayOfWeek] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[SendDayOfWeek] already exists.';
GO

-- SendTimeLocal: send time in business-local time. NULL -> default 08:00 at read time.
IF COL_LENGTH('notification.BusinessAssistantSetting', 'SendTimeLocal') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [SendTimeLocal] TIME NULL;
    PRINT 'Added [SendTimeLocal] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[SendTimeLocal] already exists.';
GO

-- RecipientOverride: delimited email list. NULL -> send to the business owner.
IF COL_LENGTH('notification.BusinessAssistantSetting', 'RecipientOverride') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [RecipientOverride] NVARCHAR(1000) NULL;
    PRINT 'Added [RecipientOverride] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[RecipientOverride] already exists.';
GO

-- IsRecipientOwnerIncluded: when an override is set, also CC the owner?
IF COL_LENGTH('notification.BusinessAssistantSetting', 'IsRecipientOwnerIncluded') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [IsRecipientOwnerIncluded] BIT NOT NULL
            CONSTRAINT [DF_BusinessAssistantSetting_IsRecipientOwnerIncluded] DEFAULT (1);
    PRINT 'Added [IsRecipientOwnerIncluded] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[IsRecipientOwnerIncluded] already exists.';
GO

-- IncludedFiguresCsv: snapshot figure keys (CSV). NULL -> default figure set at read time.
IF COL_LENGTH('notification.BusinessAssistantSetting', 'IncludedFiguresCsv') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [IncludedFiguresCsv] NVARCHAR(400) NULL;
    PRINT 'Added [IncludedFiguresCsv] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[IncludedFiguresCsv] already exists.';
GO
