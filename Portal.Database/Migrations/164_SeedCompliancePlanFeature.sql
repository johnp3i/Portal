-- ============================================================
-- Add compliance module to Professional and Enterprise plans
-- ============================================================

USE [Portal]
GO

DECLARE @ProfessionalPlanId INT;
DECLARE @EnterprisePlanId INT;

SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';
SELECT @EnterprisePlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

-- Add compliance module to Professional plan (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[PlanFeature]
    WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'compliance'
)
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@ProfessionalPlanId, N'compliance', 1, N'full');
END

-- Add compliance module to Enterprise plan (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[PlanFeature]
    WHERE [PlanId] = @EnterprisePlanId AND [ModuleName] = N'compliance'
)
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@EnterprisePlanId, N'compliance', 1, N'full');
END

GO
