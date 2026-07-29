-- ============================================================
-- Seed compliance categories and Cyprus application type templates
-- ============================================================

USE [Portal]
GO

-- Seed categories
INSERT INTO [compliance].[ApplicationCategory] ([Name], [Description]) VALUES
    ('Tax', 'Income tax, corporate tax, and related filings'),
    ('Employee', 'Social insurance, employer declarations, and payroll-related filings'),
    ('Regulatory', 'Annual levies, registrations, and regulatory compliance filings'),
    ('Business Registration', 'Company formation, renewals, and registration filings');

-- Seed Cyprus templates
INSERT INTO [compliance].[ApplicationType]
    ([Name], [Description], [Country], [ApplicationCategoryId], [Frequency], [DefaultDueMonth], [DefaultDueDay])
VALUES
    ('IR7 Annual Tax Return', 'Annual corporate/personal income tax return', 'Cyprus', 1, 'Annual', 3, 31),
    ('Social Insurance Monthly', 'Monthly social insurance contribution declaration', 'Cyprus', 2, 'Monthly', NULL, 15),
    ('VAT Return', 'Quarterly Value Added Tax return', 'Cyprus', 1, 'Quarterly', NULL, 10),
    ('Annual Levy', 'Annual company levy to the Registrar of Companies', 'Cyprus', 3, 'Annual', 6, 30),
    ('Employer''s Declaration', 'Annual employer declaration of employee earnings', 'Cyprus', 2, 'Annual', 4, 30);
