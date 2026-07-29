USE [Portal]
GO

/*
    Migration: 159_SeedStripeConnectPlanFeature
    Description: Seeds the 'stripe_connect' module into PlanFeature for Professional
                 and Enterprise plans at AccessLevel = 'full'.
                 Foundation/Starter plans do not include card payments.

    This script is idempotent — safe to run multiple times without producing duplicate rows.
*/

DECLARE @ProfessionalPlanId INT;
DECLARE @EnterprisePlanId INT;

SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';
SELECT @EnterprisePlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'stripe_connect')
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@ProfessionalPlanId, N'stripe_connect', 1, N'full');

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @EnterprisePlanId AND [ModuleName] = N'stripe_connect')
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@EnterprisePlanId, N'stripe_connect', 1, N'full');

GO
