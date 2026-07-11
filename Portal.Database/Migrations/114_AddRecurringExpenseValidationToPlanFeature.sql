-- ============================================================
-- Migration: 114_AddRecurringExpenseValidationToPlanFeature
-- Description: Adds the recurring_expense_validation module
--              to Professional and Enterprise plan features.
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
       WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'recurring_expense_validation'
   )
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@ProfessionalPlanId, N'recurring_expense_validation', 1, N'full');
END
GO

-- Re-declare for second batch
DECLARE @EnterprisePlanId2 INT;
SELECT @EnterprisePlanId2 = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

-- Insert for Enterprise (if not already present)
IF @EnterprisePlanId2 IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM [dbo].[PlanFeature]
       WHERE [PlanId] = @EnterprisePlanId2 AND [ModuleName] = N'recurring_expense_validation'
   )
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@EnterprisePlanId2, N'recurring_expense_validation', 1, N'full');
END
GO
