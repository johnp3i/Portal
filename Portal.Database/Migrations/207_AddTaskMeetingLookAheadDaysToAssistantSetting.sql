-- ============================================================
-- Migration 207: Add TaskMeetingLookAheadDays to BusinessAssistantSetting
-- ============================================================
-- Purpose: Per-business look-ahead window (days) for the Task & Meeting Reminder
--          assistant — how far ahead upcoming tasks/meetings are pulled into the
--          daily agenda. NULL means "fall back to the global
--          NotificationOptions.TaskMeetingDefaultLookAheadDays" (default 2).
--          Additive + nullable -> existing rows unaffected.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF COL_LENGTH('notification.BusinessAssistantSetting', 'TaskMeetingLookAheadDays') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [TaskMeetingLookAheadDays] INT NULL;
    PRINT 'Added [TaskMeetingLookAheadDays] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[TaskMeetingLookAheadDays] already exists.';
GO
