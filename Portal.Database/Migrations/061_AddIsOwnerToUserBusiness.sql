/*
    Migration: 061_AddIsOwnerToUserBusiness
    Description: Adds IsOwner BIT column to [membership].[UserBusiness] to identify
                 the business owner who can invite other users.
    
    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[membership].[UserBusiness]')
      AND [name] = 'IsOwner'
)
BEGIN
    ALTER TABLE [membership].[UserBusiness]
        ADD [IsOwner] BIT NOT NULL CONSTRAINT [DF_UserBusiness_IsOwner] DEFAULT 0;
END
GO
