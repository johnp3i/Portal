USE [Portal];
GO

/*
    Migration: 097_SeedPlanFeatureModules
    Description: Seeds three subscription plan tiers (Starter, Professional, Enterprise)
                 and their corresponding PlanFeature records. Each plan tier includes a
                 specific set of modules, all at AccessLevel = 'full'.

                 - Starter (10 modules): quotation, invoice, revenue, customer, purchase,
                   vat, credit, products, payment_link_manual, payment_reminder_manual
                 - Professional (16 modules): All Starter + payment_link_auto,
                   payment_reminder_auto, cashflow, pnl, expense_insights, attachments
                 - Enterprise (22 modules): All Professional + client_portal,
                   activity_timeline, audit_log, api, webhooks, multi_currency

    Requirements: 2.1  - Starter plan modules
                  2.2  - Professional plan modules
                  2.3  - Enterprise plan modules

    This script is idempotent — safe to run multiple times without producing duplicate rows.
*/

-- =============================================================================
-- 1. Seed Plan records for Starter, Professional, Enterprise
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[Plan] WHERE [Slug] = 'starter')
BEGIN
    INSERT INTO [dbo].[Plan]
        ([Name], [Slug], [MonthlyPriceEur], [AnnualPriceEur], [MaxUsers], [IsActive], [DisplayOrder], [Description])
    VALUES
        (N'Starter', N'starter', 39.00, 390.00, 2, 1, 1, N'Foundation — complete business management');
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Plan] WHERE [Slug] = 'professional')
BEGIN
    INSERT INTO [dbo].[Plan]
        ([Name], [Slug], [MonthlyPriceEur], [AnnualPriceEur], [MaxUsers], [IsActive], [DisplayOrder], [Description])
    VALUES
        (N'Professional', N'professional', 79.00, 790.00, 5, 1, 3, N'Automation — the platform works for you');
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Plan] WHERE [Slug] = 'enterprise')
BEGIN
    INSERT INTO [dbo].[Plan]
        ([Name], [Slug], [MonthlyPriceEur], [AnnualPriceEur], [MaxUsers], [IsActive], [DisplayOrder], [Description])
    VALUES
        (N'Enterprise', N'enterprise', 149.00, 1490.00, 9999, 1, 4, N'Scale — teams, integrations, self-service');
END
GO

-- =============================================================================
-- 2. Delete existing PlanFeature records for these plans (clean slate)
--    This ensures the complete module set is always in sync with this migration.
-- =============================================================================

DECLARE @StarterPlanId INT;
DECLARE @ProfessionalPlanId INT;
DECLARE @EnterprisePlanId INT;

SELECT @StarterPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'starter';
SELECT @ProfessionalPlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'professional';
SELECT @EnterprisePlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'enterprise';

-- Remove existing PlanFeature records for all three plans to replace with complete set
DELETE FROM [dbo].[PlanFeature] WHERE [PlanId] = @StarterPlanId;
DELETE FROM [dbo].[PlanFeature] WHERE [PlanId] = @ProfessionalPlanId;
DELETE FROM [dbo].[PlanFeature] WHERE [PlanId] = @EnterprisePlanId;

-- =============================================================================
-- 3. Insert PlanFeature records for Starter plan (10 modules)
-- =============================================================================

INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
VALUES
    (@StarterPlanId, N'quotation', 1, N'full'),
    (@StarterPlanId, N'invoice', 1, N'full'),
    (@StarterPlanId, N'revenue', 1, N'full'),
    (@StarterPlanId, N'customer', 1, N'full'),
    (@StarterPlanId, N'purchase', 1, N'full'),
    (@StarterPlanId, N'vat', 1, N'full'),
    (@StarterPlanId, N'credit', 1, N'full'),
    (@StarterPlanId, N'products', 1, N'full'),
    (@StarterPlanId, N'payment_link_manual', 1, N'full'),
    (@StarterPlanId, N'payment_reminder_manual', 1, N'full');

-- =============================================================================
-- 4. Insert PlanFeature records for Professional plan (16 modules)
-- =============================================================================

INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
VALUES
    (@ProfessionalPlanId, N'quotation', 1, N'full'),
    (@ProfessionalPlanId, N'invoice', 1, N'full'),
    (@ProfessionalPlanId, N'revenue', 1, N'full'),
    (@ProfessionalPlanId, N'customer', 1, N'full'),
    (@ProfessionalPlanId, N'purchase', 1, N'full'),
    (@ProfessionalPlanId, N'vat', 1, N'full'),
    (@ProfessionalPlanId, N'credit', 1, N'full'),
    (@ProfessionalPlanId, N'products', 1, N'full'),
    (@ProfessionalPlanId, N'payment_link_manual', 1, N'full'),
    (@ProfessionalPlanId, N'payment_reminder_manual', 1, N'full'),
    (@ProfessionalPlanId, N'payment_link_auto', 1, N'full'),
    (@ProfessionalPlanId, N'payment_reminder_auto', 1, N'full'),
    (@ProfessionalPlanId, N'cashflow', 1, N'full'),
    (@ProfessionalPlanId, N'pnl', 1, N'full'),
    (@ProfessionalPlanId, N'expense_insights', 1, N'full'),
    (@ProfessionalPlanId, N'attachments', 1, N'full');

-- =============================================================================
-- 5. Insert PlanFeature records for Enterprise plan (22 modules)
-- =============================================================================

INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
VALUES
    (@EnterprisePlanId, N'quotation', 1, N'full'),
    (@EnterprisePlanId, N'invoice', 1, N'full'),
    (@EnterprisePlanId, N'revenue', 1, N'full'),
    (@EnterprisePlanId, N'customer', 1, N'full'),
    (@EnterprisePlanId, N'purchase', 1, N'full'),
    (@EnterprisePlanId, N'vat', 1, N'full'),
    (@EnterprisePlanId, N'credit', 1, N'full'),
    (@EnterprisePlanId, N'products', 1, N'full'),
    (@EnterprisePlanId, N'payment_link_manual', 1, N'full'),
    (@EnterprisePlanId, N'payment_reminder_manual', 1, N'full'),
    (@EnterprisePlanId, N'payment_link_auto', 1, N'full'),
    (@EnterprisePlanId, N'payment_reminder_auto', 1, N'full'),
    (@EnterprisePlanId, N'cashflow', 1, N'full'),
    (@EnterprisePlanId, N'pnl', 1, N'full'),
    (@EnterprisePlanId, N'expense_insights', 1, N'full'),
    (@EnterprisePlanId, N'attachments', 1, N'full'),
    (@EnterprisePlanId, N'client_portal', 1, N'full'),
    (@EnterprisePlanId, N'activity_timeline', 1, N'full'),
    (@EnterprisePlanId, N'audit_log', 1, N'full'),
    (@EnterprisePlanId, N'api', 1, N'full'),
    (@EnterprisePlanId, N'webhooks', 1, N'full'),
    (@EnterprisePlanId, N'multi_currency', 1, N'full');

GO
