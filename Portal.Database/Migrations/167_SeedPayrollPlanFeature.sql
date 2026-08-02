-- ============================================================
-- Seed payroll module PlanFeature for Enterprise tier
-- ============================================================

USE [Portal]
GO

INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded])
SELECT [Id], 'payroll', 1
FROM [dbo].[Plan]
WHERE [Name] = 'Enterprise'
GO
