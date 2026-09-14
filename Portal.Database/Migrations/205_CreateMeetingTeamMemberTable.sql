-- ============================================================
-- Migration 205: Create Meeting Team Member (attendee) mapping table
-- ============================================================
-- Purpose: Creates the [sales].[MeetingTeamMember] table — a many-to-many
--          mapping of a meeting to the team member(s) assigned to attend it.
--          Powers the Task & Meeting Reminder assistant (each attendee gets
--          their own agenda). A meeting is 1-to-many over team members;
--          a follow-up task is 1-to-1 (via FollowUpTask.TeamMemberId).
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingTeamMember'
)
BEGIN
    CREATE TABLE [sales].[MeetingTeamMember]
    (
        [Id]            INT         IDENTITY(1,1) NOT NULL,
        [MeetingId]     INT         NOT NULL,
        [TeamMemberId]  INT         NOT NULL,
        [CreatedAtUtc]  DATETIME    NOT NULL CONSTRAINT [DF_MeetingTeamMember_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_MeetingTeamMember] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_MeetingTeamMember_Meeting] FOREIGN KEY ([MeetingId]) REFERENCES [sales].[Meeting]([Id]),
        CONSTRAINT [FK_MeetingTeamMember_TeamMember] FOREIGN KEY ([TeamMemberId]) REFERENCES [sales].[TeamMember]([Id])
    );

    PRINT 'Created [sales].[MeetingTeamMember] table.';
END
ELSE
BEGIN
    PRINT '[sales].[MeetingTeamMember] already exists.';
END
GO

-- One row per (meeting, team member) — prevents duplicate attendee assignments.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_MeetingTeamMember_Meeting_TeamMember'
      AND object_id = OBJECT_ID('sales.MeetingTeamMember')
)
BEGIN
    CREATE UNIQUE INDEX [UX_MeetingTeamMember_Meeting_TeamMember]
        ON [sales].[MeetingTeamMember] ([MeetingId], [TeamMemberId]);
    PRINT 'Created UNIQUE index [UX_MeetingTeamMember_Meeting_TeamMember].';
END
ELSE
    PRINT 'Index [UX_MeetingTeamMember_Meeting_TeamMember] already exists.';
GO
