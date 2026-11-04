USE [Portal]
GO

/*
    Migration: 230_SeedCreditAccountLevelPlanFeature
    Description: Seeds the 'credit_account_level' capability into PlanFeature for every tier that
                 already includes the base 'credit' module (Starter, Professional, Enterprise), at
                 AccessLevel = 'full'.

                 'credit_account_level' gates account-level (invoice-less) credit notes — issuing
                 credit directly to a customer account with no originating invoice. It is a capability
                 WITHIN the credit module, so it is offered on the same tiers that offer 'credit'.
                 The capability is checked in code via IPlanCheckService.IsModuleInPlanAsync; when a
                 business's plan lacks this row, account mode stays hidden and is rejected server-side.

    This script is idempotent — safe to run multiple times without producing duplicate rows.
*/

DECLARE @StarterPlanId INT;
DECLARE @ProfessionalPlanId INT;
DECLARE @EnterprisePlanId INT;

SELECT @StarterPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'starter';
SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';
SELECT @EnterprisePlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

IF @StarterPlanId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @StarterPlanId AND [ModuleName] = N'credit_account_level')
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@StarterPlanId, N'credit_account_level', 1, N'full');

IF @ProfessionalPlanId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @ProfessionalPlanId AND [ModuleName] = N'credit_account_level')
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@ProfessionalPlanId, N'credit_account_level', 1, N'full');

IF @EnterprisePlanId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @EnterprisePlanId AND [ModuleName] = N'credit_account_level')
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (@EnterprisePlanId, N'credit_account_level', 1, N'full');

GO
