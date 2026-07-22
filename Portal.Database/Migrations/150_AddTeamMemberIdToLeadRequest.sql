-- ============================================================
-- Migration: 150_AddTeamMemberIdToLeadRequest
-- Description: Adds TeamMemberId FK to [sales].[LeadRequest]
--              for proper team member assignment.
-- ============================================================

USE [Portal]
GO

-- Add TeamMemberId column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales'
      AND TABLE_NAME = 'LeadRequest'
      AND COLUMN_NAME = 'TeamMemberId'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
    ADD [TeamMemberId] INT NULL;
END
GO

-- Add FK constraint
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = 'FK_LeadRequest_TeamMember'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
    ADD CONSTRAINT [FK_LeadRequest_TeamMember]
    FOREIGN KEY ([TeamMemberId]) REFERENCES [sales].[TeamMember] ([Id]);
END
GO

-- Index for filtering by team member
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_LeadRequest_TeamMemberId'
      AND [object_id] = OBJECT_ID('[sales].[LeadRequest]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeadRequest_TeamMemberId]
        ON [sales].[LeadRequest] ([TeamMemberId])
        WHERE [TeamMemberId] IS NOT NULL;
END
GO
