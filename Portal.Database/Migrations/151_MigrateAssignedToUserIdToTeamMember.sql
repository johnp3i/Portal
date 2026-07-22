-- ============================================================
-- Migration: 151_MigrateAssignedToUserIdToTeamMember
-- Description: Migrates existing AssignedToUserId values to
--              TeamMember records and backfills TeamMemberId.
--              Idempotent — safe to run multiple times.
-- ============================================================

USE [Portal]
GO

-- Step 1: Create TeamMember records for each distinct AssignedToUserId
-- that doesn't already have a matching TeamMember
INSERT INTO [sales].[TeamMember] ([BusinessId], [FirstName], [UserId], [IsActive])
SELECT DISTINCT
    [sales].[LeadRequest].[BusinessId],
    'Team Member',
    [sales].[LeadRequest].[AssignedToUserId],
    1
FROM [sales].[LeadRequest]
WHERE [sales].[LeadRequest].[AssignedToUserId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [sales].[TeamMember]
      WHERE [sales].[TeamMember].[BusinessId] = [sales].[LeadRequest].[BusinessId]
        AND [sales].[TeamMember].[UserId] = [sales].[LeadRequest].[AssignedToUserId]
  )
GO

-- Step 2: Backfill TeamMemberId on LeadRequest from the matching TeamMember
UPDATE [sales].[LeadRequest]
SET [TeamMemberId] = [sales].[TeamMember].[Id]
FROM [sales].[LeadRequest]
INNER JOIN [sales].[TeamMember]
    ON [sales].[TeamMember].[BusinessId] = [sales].[LeadRequest].[BusinessId]
    AND [sales].[TeamMember].[UserId] = [sales].[LeadRequest].[AssignedToUserId]
WHERE [sales].[LeadRequest].[AssignedToUserId] IS NOT NULL
  AND [sales].[LeadRequest].[TeamMemberId] IS NULL
GO

PRINT 'Migration complete: AssignedToUserId values migrated to TeamMember records.'
GO
