-- ============================================================
-- SUBSCRIPTION SEED: Le Paris Roasting (BusinessId = 1000)
-- ============================================================
-- Run this against the Portal database to create an active
-- subscription for the demo business.
--
-- This is idempotent — safe to run multiple times.
-- ============================================================

USE [Portal];
GO

IF NOT EXISTS (
    SELECT 1 FROM [billing].[Subscription] WHERE [BusinessId] = 1000
)
BEGIN
    DECLARE @PlanId INT;
    SELECT @PlanId = [Id] FROM [dbo].[Plan] WHERE [Slug] = 'business';

    IF @PlanId IS NOT NULL
    BEGIN
        INSERT INTO [billing].[Subscription]
            ([BusinessId], [PlanId], [Status], [StripeSubscriptionId], [CurrentPeriodStart], [CurrentPeriodEnd], [CancelledAtUtc], [CreatedAtUtc])
        VALUES
            (1000, @PlanId, N'active', N'demo_sub_leparis', GETUTCDATE(), DATEADD(YEAR, 10, GETUTCDATE()), NULL, GETUTCDATE());

        PRINT 'Subscription created for Le Paris Roasting (BusinessId=1000), Status=active, Plan=Business';
    END
    ELSE
    BEGIN
        PRINT 'ERROR: Plan with Slug "business" not found. Run 076_SeedBusinessPlan.sql first.';
    END
END
ELSE
BEGIN
    PRINT 'Subscription already exists for BusinessId=1000. No action taken.';
END
GO
