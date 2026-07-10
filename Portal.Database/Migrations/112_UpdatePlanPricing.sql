-- ============================================================
-- Migration: 112_UpdatePlanPricing
-- Description: Updates Plan pricing to the new model:
--              Foundation €39/mo (€390/year), Professional €89/mo (€890/year),
--              Enterprise €169/mo (€1,690/year).
--              Annual pricing = 10 months (2 months free).
-- ============================================================

USE [Portal]
GO

-- Foundation (Id=2): €39/mo, €390/year
UPDATE [dbo].[Plan]
SET [MonthlyPriceEur] = 39.00,
    [AnnualPriceEur] = 390.00
WHERE [Id] = 2
GO

-- Professional (Id=3): €89/mo, €890/year
UPDATE [dbo].[Plan]
SET [MonthlyPriceEur] = 89.00,
    [AnnualPriceEur] = 890.00
WHERE [Id] = 3
GO

-- Enterprise (Id=4): €169/mo, €1690/year
UPDATE [dbo].[Plan]
SET [MonthlyPriceEur] = 169.00,
    [AnnualPriceEur] = 1690.00
WHERE [Id] = 4
GO
