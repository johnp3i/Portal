-- ============================================================
-- Migration 203: Add VatNoticeLeadDays to BusinessAssistantSetting
-- ============================================================
-- Purpose: Per-business notice lead time (days before the derived VAT filing
--          deadline) for the VAT Period Due Reminder assistant. NULL means
--          "fall back to the global NotificationOptions.VatDeadlineNoticeDays"
--          (default 21). Additive + nullable -> existing rows unaffected.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF COL_LENGTH('notification.BusinessAssistantSetting', 'VatNoticeLeadDays') IS NULL
BEGIN
    ALTER TABLE [notification].[BusinessAssistantSetting]
        ADD [VatNoticeLeadDays] INT NULL;
    PRINT 'Added [VatNoticeLeadDays] to [notification].[BusinessAssistantSetting].';
END
ELSE
    PRINT '[VatNoticeLeadDays] already exists.';
GO
