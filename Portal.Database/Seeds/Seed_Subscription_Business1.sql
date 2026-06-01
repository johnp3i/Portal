USE [Portal];
GO

/*
    Seed: Subscription for Business ID = 1 (3 Inventors)
    Description: Creates an active subscription for the 3 Inventors account (Business ID 1)
                 on the "Business" plan with a 4-year period. This is the platform owner's
                 account — no Stripe payment required.

    Prerequisites:
        - Business with Id = 1 must exist in [portal].[Business]
        - Plan with Slug = 'business' must exist in [dbo].[Plan]
        - [billing] schema and [billing].[Subscription] table must exist (migration 076)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Resolve PlanId for "business" plan
-- =============================================================================

DECLARE @PlanId INT;
SELECT @PlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'business';

IF @PlanId IS NULL
BEGIN
    RAISERROR('Plan with Slug "business" not found. Run the plan seed migration first.', 16, 1);
    RETURN;
END

-- =============================================================================
-- 2. Insert Subscription for Business ID = 1 (if not already exists)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM [billing].[Subscription]
    WHERE [BusinessId] = 1
)
BEGIN
    INSERT INTO [billing].[Subscription]
        ([BusinessId], [PlanId], [Status], [StripeSubscriptionId], [CurrentPeriodStart], [CurrentPeriodEnd], [CancelledAtUtc], [CreatedAtUtc])
    VALUES
        (1, @PlanId, 'active', NULL, GETUTCDATE(), DATEADD(YEAR, 4, GETUTCDATE()), NULL, GETUTCDATE());

    PRINT 'Subscription created for Business ID = 1 — 3 Inventors (Status: active, Plan: Business, Expires: 4 years)';
END
ELSE
BEGIN
    PRINT 'Subscription already exists for Business ID = 1. Skipped.';
END
GO
