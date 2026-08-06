-- ============================================================
-- Seed: What's New Announcement — PAYE Tax & Compliance Integration
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[FeatureAnnouncements] WHERE [Title] = N'PAYE Tax & Compliance Integration')
BEGIN
    INSERT INTO [dbo].[FeatureAnnouncements]
        ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc])
    VALUES
        (N'PAYE Tax & Compliance Integration',
         N'Automatic PAYE income tax calculation with Cyprus 2024 progressive bands, compliance filing auto-population, and employer contribution reporting.',
         N'<p>The payroll module now handles PAYE income tax and integrates with compliance filings:</p><ul><li><strong>Automatic PAYE Calculation</strong> — progressive tax bands (Cyprus 2024) applied during payslip generation, with per-band breakdown visible on payslips</li><li><strong>Per-Employee PAYE Toggle</strong> — opt employees in or out of PAYE with a threshold warning for incomes below €19,500</li><li><strong>Compliance Auto-Population</strong> — Social Insurance filing amounts are automatically updated when a payroll period is finalised</li><li><strong>Employer Contribution Report</strong> — breakdown of SI, Redundancy, Industrial Training, Social Cohesion, and GESY contributions per employee with Excel export</li><li><strong>Cross-Reference Audit Trail</strong> — every finalisation creates a traceable link between payroll and compliance filings</li><li><strong>Country Deduction Templates</strong> — SuperAdmin can manage country-specific templates for multi-country expansion</li></ul>',
         N'payroll',
         N'Open Contribution Report',
         N'/PayrollCompliance/ContributionReport',
         NULL,
         1,
         GETUTCDATE(),
         NULL);
    PRINT 'Inserted announcement: PAYE Tax & Compliance Integration';
END
GO
