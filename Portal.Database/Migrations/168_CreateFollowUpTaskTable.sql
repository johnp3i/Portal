-- ============================================================
-- Create Follow-Up Task table for lightweight sales reminders
-- ============================================================

USE [Portal]
GO

CREATE TABLE [sales].[FollowUpTask]
(
    [Id]                INT             IDENTITY(1,1)   NOT NULL,
    [BusinessId]        INT                             NOT NULL,
    [LeadRequestId]     INT                             NULL,
    [ContactId]         INT                             NULL,
    [TeamMemberId]      INT                             NULL,
    [Title]             NVARCHAR(200)                   NOT NULL,
    [TaskType]          NVARCHAR(50)                    NOT NULL,
    [DueAtUtc]          DATETIME                        NOT NULL,
    [Notes]             NVARCHAR(500)                   NULL,
    [IsCompleted]       BIT                             NOT NULL  CONSTRAINT [DF_FollowUpTask_IsCompleted] DEFAULT (0),
    [CompletedAtUtc]    DATETIME                        NULL,
    [SnoozedCount]      INT                             NOT NULL  CONSTRAINT [DF_FollowUpTask_SnoozedCount] DEFAULT (0),
    [CreatedByUserId]   NVARCHAR(450)                   NOT NULL,
    [CreatedAtUtc]      DATETIME                        NOT NULL  CONSTRAINT [DF_FollowUpTask_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_FollowUpTask] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_FollowUpTask_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
    CONSTRAINT [FK_FollowUpTask_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest] ([Id]),
    CONSTRAINT [FK_FollowUpTask_Contact] FOREIGN KEY ([ContactId]) REFERENCES [sales].[Contact] ([Id]),
    CONSTRAINT [FK_FollowUpTask_TeamMember] FOREIGN KEY ([TeamMemberId]) REFERENCES [sales].[TeamMember] ([Id])
);
GO

-- Primary query: "What's due today?" — sorted by urgency
CREATE NONCLUSTERED INDEX [IX_FollowUpTask_BusinessId_DueAtUtc]
    ON [sales].[FollowUpTask] ([BusinessId], [DueAtUtc])
    INCLUDE ([TeamMemberId], [IsCompleted])
    WHERE [IsCompleted] = 0;
GO

-- Lead-scoped lookups
CREATE NONCLUSTERED INDEX [IX_FollowUpTask_LeadRequestId]
    ON [sales].[FollowUpTask] ([LeadRequestId])
    WHERE [IsCompleted] = 0;
GO
