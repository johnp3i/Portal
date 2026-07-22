-- ============================================================
-- Migration: 149_CreateActivityFeedTable
-- Description: Creates the [sales].[ActivityFeed] table for
--              recording all actions on leads as an immutable timeline.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales'
      AND TABLE_NAME = 'ActivityFeed'
)
BEGIN
    CREATE TABLE [sales].[ActivityFeed]
    (
        [Id]                        INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                INT                           NOT NULL,
        [LeadRequestId]             INT                           NOT NULL,
        [Action]                    NVARCHAR(50)                  NOT NULL,
        [Description]               NVARCHAR(500)                 NOT NULL,
        [PerformedByUserId]         NVARCHAR(450)                 NULL,
        [PerformedByTeamMemberId]   INT                           NULL,
        [Metadata]                  NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]              DATETIME                      NOT NULL CONSTRAINT [DF_ActivityFeed_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ActivityFeed] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ActivityFeed_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_ActivityFeed_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest] ([Id]),
        CONSTRAINT [FK_ActivityFeed_TeamMember] FOREIGN KEY ([PerformedByTeamMemberId]) REFERENCES [sales].[TeamMember] ([Id])
    );
END
GO

-- Index for timeline queries (newest first per lead)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_ActivityFeed_LeadRequestId_CreatedAtUtc'
      AND [object_id] = OBJECT_ID('[sales].[ActivityFeed]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ActivityFeed_LeadRequestId_CreatedAtUtc]
        ON [sales].[ActivityFeed] ([LeadRequestId], [CreatedAtUtc] DESC);
END
GO

-- Index for business-scoped queries
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_ActivityFeed_BusinessId'
      AND [object_id] = OBJECT_ID('[sales].[ActivityFeed]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ActivityFeed_BusinessId]
        ON [sales].[ActivityFeed] ([BusinessId]);
END
GO
