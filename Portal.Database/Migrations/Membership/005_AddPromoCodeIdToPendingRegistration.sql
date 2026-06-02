/*
    Migration: 005_AddPromoCodeIdToPendingRegistration
    Description: Adds a nullable [PromoCodeId] INT column to the [membership].[PendingRegistration] table.
                 This is a logical cross-database reference to [dbo].[PromoCode].Id in the Portal DB.
                 No physical FK constraint is added since the tables reside in different databases.

                 When a user registers with a valid promo code, the PromoCodeId is stored here
                 so the provisioning service can reference it during email confirmation.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[membership].[PendingRegistration]')
      AND name = N'PromoCodeId'
)
BEGIN
    ALTER TABLE [membership].[PendingRegistration]
        ADD [PromoCodeId] INT NULL;
END
GO
