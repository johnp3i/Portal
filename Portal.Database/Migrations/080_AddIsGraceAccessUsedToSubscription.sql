/*
    Migration: 080_AddIsGraceAccessUsedToSubscription
    Description: Adds IsGraceAccessUsed BIT column to [billing].[Subscription] to track
                 whether the one-time grace access has been consumed for an expired subscription.

    Requirements: 5.1 - IsGraceAccessUsed (BIT, NOT NULL, DEFAULT 0)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[billing].[Subscription]')
      AND [name] = 'IsGraceAccessUsed'
)
BEGIN
    ALTER TABLE [billing].[Subscription]
        ADD [IsGraceAccessUsed] BIT NOT NULL CONSTRAINT [DF_Subscription_IsGraceAccessUsed] DEFAULT (0);
END
GO
