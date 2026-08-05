-- ============================================================
-- Seed: What's New Announcement — Payroll Audit Trail & P&L Integration
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Payroll Audit Trail & P&L Integration')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Payroll Audit Trail & P&L Integration',
         N'Unlock finalised periods for corrections, track every change with a field-level audit trail, and let P&L expenses update automatically when you re-finalise.',
         N'<p>Full control over your payroll lifecycle with built-in accountability:</p><ul><li><strong>Unlock & Re-finalise Periods</strong> — reopen a finalised month, make corrections, and lock it down again in one click</li><li><strong>Field-Level Audit Trail</strong> — every edit is recorded with who changed what, old value, new value, and timestamp</li><li><strong>Automatic P&L Expense Entries</strong> — salary costs and employer contributions sync to your Purchases ledger on finalisation and adjust on re-finalisation</li><li><strong>Role-Restricted Access</strong> — only Owners and SuperAdmins can unlock or re-finalise periods, keeping your data safe</li></ul><p>Corrections without chaos. Full visibility, zero guesswork.</p>',
         N'payroll',
         N'Open Payroll',
         N'/Payroll',
         NULL,
         1,
         GETUTCDATE(),
         NULL);
    PRINT 'Inserted announcement: Payroll Audit Trail & P&L Integration';
END
GO
