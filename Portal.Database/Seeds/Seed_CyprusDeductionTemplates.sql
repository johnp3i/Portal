-- ============================================================
-- Phase D: Seed Cyprus Country Deduction Templates
-- DefaultRate stored as decimal (0.0880 = 8.80%).
-- DeductionCategoryTypeId: 1 = Employee Deduction, 2 = Employer Contribution.
-- IsPayeDeductible: 1 = reduces PAYE taxable base.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [payroll].[CountryDeductionTemplate] WHERE [CountryCode] = 'CY')
BEGIN
    INSERT INTO [payroll].[CountryDeductionTemplate]
        ([CountryCode], [DeductionName], [Code], [IsPercentage], [DeductionCategoryTypeId], [DefaultRate], [IsPayeDeductible], [SortOrder], [IsActive])
    VALUES
        ('CY', 'Social Insurance (Employee)',   'SI_Deduction',         1, 1, 0.0880, 1, 1, 1),
        ('CY', 'GESY / GHS (Employee)',         'GESY_Deduction',       1, 1, 0.0265, 1, 2, 1),
        ('CY', 'Social Insurance (Employer)',   'SI_Contribution',      1, 2, 0.0880, 0, 3, 1),
        ('CY', 'Redundancy Fund',               'Redundancy',           1, 2, 0.0120, 0, 4, 1),
        ('CY', 'Industrial Training',           'Industrial_Training',  1, 2, 0.0050, 0, 5, 1),
        ('CY', 'Social Cohesion Fund',          'Social_Cohesion',      1, 2, 0.0200, 0, 6, 1),
        ('CY', 'GESY / GHS (Employer)',         'GESY_Contribution',    1, 2, 0.0290, 0, 7, 1);
END
GO
