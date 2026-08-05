-- ============================================================
-- Seed: What's New Announcement — Payroll Reports & PDF Export
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'Payroll Reports & PDF Export')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'Payroll Reports & PDF Export',
         N'Generate branded payslip PDFs, view employee history and annual summaries, analyse earnings breakdowns, and email payslips directly to employees.',
         N'<p>Complete reporting and export capabilities for your payroll:</p><ul><li><strong>Download Payslip PDFs</strong> — branded A4 documents ready to print or share</li><li><strong>Employee History & Annual Summary</strong> — year-by-year payroll overview for tax preparation</li><li><strong>Earnings Breakdown</strong> — analyse overtime, bonuses, and holidays with Excel export</li><li><strong>Period Summary</strong> — consolidated cost view per department with PDF and Excel export</li><li><strong>Email Payslips</strong> — send individual or batch payslip PDFs to employees with tracking</li><li><strong>Employee Statements</strong> — generate date-range PDF statements for audits and loan applications</li></ul>',
         N'payroll',
         N'Open Payroll Reports',
         N'/PayrollReport/EarningsBreakdown',
         NULL,
         1,
         GETUTCDATE(),
         NULL);
    PRINT 'Inserted announcement: Payroll Reports & PDF Export';
END
GO
