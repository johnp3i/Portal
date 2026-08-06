-- ============================================================
-- Phase D: Seed Cyprus PAYE Tax Bands (2024)
-- Progressive income tax bands per Cyprus Tax Department.
-- Rates stored as decimals (0.20 = 20%).
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [payroll].[PayeTaxBand] WHERE [CountryCode] = 'CY' AND [EffectiveFromYear] = 2024)
BEGIN
    INSERT INTO [payroll].[PayeTaxBand] ([CountryCode], [LowerBound], [UpperBound], [Rate], [EffectiveFromYear], [EffectiveToYear])
    VALUES
        ('CY', 0.00, 19500.00, 0.0000, 2024, NULL),
        ('CY', 19500.01, 28000.00, 0.2000, 2024, NULL),
        ('CY', 28000.01, 36300.00, 0.2500, 2024, NULL),
        ('CY', 36300.01, 60000.00, 0.3000, 2024, NULL),
        ('CY', 60000.01, NULL, 0.3500, 2024, NULL);
END
GO
