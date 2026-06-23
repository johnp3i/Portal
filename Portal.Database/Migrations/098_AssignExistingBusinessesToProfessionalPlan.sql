USE [Portal];
GO

/*
    Migration: 098_AssignExistingBusinessesToProfessionalPlan
    Description: Assigns all existing businesses to the Professional subscription plan.
                 For every Business that does not already have a BusinessPlan record,
                 this script inserts a new record linking the business to the Professional
                 plan (looked up by Slug = 'professional') with an active status.

                 This ensures no existing business loses functionality when the
                 subscription-permission-gating feature launches.

    Requirements: 2.4  - Assign all existing businesses to Professional plan
                  11.1 - Create BusinessSubscription for every existing Business
                  11.2 - Set Status to 'active' and StartDateUtc to current UTC time
                  11.3 - Set EndDateUtc to NULL indicating no expiry

    This script is idempotent — safe to run multiple times without producing duplicate rows.
*/

-- =============================================================================
-- 1. Resolve the Professional plan Id
-- =============================================================================

DECLARE @ProfessionalPlanId INT;

SELECT @ProfessionalPlanId = [Id]
FROM [dbo].[Plan]
WHERE [Slug] = 'professional';

-- Exit early if the Professional plan does not exist (seed migration 097 must run first)
IF @ProfessionalPlanId IS NULL
BEGIN
    RAISERROR('Professional plan not found. Ensure migration 097_SeedPlanFeatureModules has been executed.', 16, 1);
    RETURN;
END

-- =============================================================================
-- 2. Insert BusinessPlan records for businesses that don't already have one
-- =============================================================================

INSERT INTO [dbo].[BusinessPlan]
    ([BusinessId], [PlanId], [StartDateUtc], [EndDateUtc], [IsActive], [Status], [CreatedAtUtc])
SELECT
    Business.[Id],
    @ProfessionalPlanId,
    GETUTCDATE(),
    NULL,
    1,
    N'active',
    GETUTCDATE()
FROM [portal].[Business]
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[BusinessPlan]
    WHERE [dbo].[BusinessPlan].[BusinessId] = Business.[Id]
);

GO
