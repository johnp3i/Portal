USE [Portal];
GO

/*
    Migration: 076_SeedBusinessPlan
    Description: Seeds the initial "Business" subscription plan with all nine platform
                 modules enabled. Inserts one Plan record and nine PlanFeature records
                 covering: customer, quotation, invoice, revenue, purchase, vat, credit,
                 audit, and products. The PlanId is resolved dynamically by querying the
                 Slug column rather than hardcoding an identity value.

    Requirements: 4.1  - Plan record: Name "Business", Slug "business", MonthlyPriceEur 29.00,
                         AnnualPriceEur 348.00, MaxUsers 5, IsActive 1, DisplayOrder 2, Description NULL
                 4.2  - Nine PlanFeature records with IsIncluded = 1, referencing PlanId via Slug
                 4.3  - Idempotent: check existing Plan by Slug, check existing PlanFeature by PlanId+ModuleName
                 4.4  - DisplayOrder 2 (leaving 1 and 3 for future Starter/Enterprise)
                 7.1  - Sequential three-digit numbering
                 7.6  - Header comment block
                 7.7  - GO batch terminators

    This script is idempotent — safe to run multiple times without producing duplicate rows.
*/

-- =============================================================================
-- 1. Insert Plan record for "Business" tier
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[Plan]
    WHERE [Slug] = 'business'
)
BEGIN
    INSERT INTO [dbo].[Plan]
        ([Name], [Slug], [MonthlyPriceEur], [AnnualPriceEur], [MaxUsers], [IsActive], [DisplayOrder], [Description])
    VALUES
        (N'Business', N'business', 29.00, 348.00, 5, 1, 2, NULL);
END
GO

-- =============================================================================
-- 2. Resolve PlanId for "business" plan
-- =============================================================================

DECLARE @PlanId INT;
SELECT @PlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'business';

-- =============================================================================
-- 3. Insert PlanFeature records (9 modules, all included)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'customer')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'customer', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'quotation')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'quotation', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'invoice')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'invoice', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'revenue')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'revenue', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'purchase')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'purchase', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'vat')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'vat', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'credit')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'credit', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'audit')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'audit', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlanFeature] WHERE [PlanId] = @PlanId AND [ModuleName] = N'products')
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded]) VALUES (@PlanId, N'products', 1);
END
GO
