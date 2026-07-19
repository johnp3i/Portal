-- ============================================================
-- Seed: Adds the 'zreport_import' module to Professional and
--        Enterprise plan features.
-- ============================================================
-- Purpose: Gates the Z-Report Bulk Import feature to Professional
--          tier and above. Foundation tier users can manually enter
--          Z-Reports but cannot bulk-import from CSV/Excel.
-- ============================================================

USE [Portal]
GO

-- Resolve Plan IDs
DECLARE @ProfessionalPlanId INT;
DECLARE @EnterprisePlanId INT;

SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';
SELECT @EnterprisePlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

-- Insert for Professional (if not already present)
IF @ProfessionalPlanId IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[PlanFeature]
       WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'zreport_import'
   )
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@ProfessionalPlanId, N'zreport_import', 1, N'full');
    PRINT 'Added zreport_import to Professional plan.';
END
ELSE
BEGIN
    PRINT 'zreport_import already exists for Professional plan (or plan not found).';
END
GO

-- Re-declare for second batch
DECLARE @EnterprisePlanId2 INT;
SELECT @EnterprisePlanId2 = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

-- Insert for Enterprise (if not already present)
IF @EnterprisePlanId2 IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[PlanFeature]
       WHERE [PlanId] = @EnterprisePlanId2 AND [ModuleName] = N'zreport_import'
   )
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@EnterprisePlanId2, N'zreport_import', 1, N'full');
    PRINT 'Added zreport_import to Enterprise plan.';
END
ELSE
BEGIN
    PRINT 'zreport_import already exists for Enterprise plan (or plan not found).';
END
GO
