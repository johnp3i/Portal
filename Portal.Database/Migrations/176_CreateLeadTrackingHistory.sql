-- ============================================================
-- Migration 176: Create Lead Tracking History Tables
-- ============================================================
-- Purpose: Creates [sales].[LeadTrackingActionType] lookup table
--          and [sales].[LeadTrackingHistory] audit table to record
--          every stage transition with the action type that caused it.
--          Enables regression algorithm for lead stage re-evaluation
--          when meetings are cancelled or reactivated.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadTrackingActionType'
)
BEGIN
    CREATE TABLE [sales].[LeadTrackingActionType]
    (
        [Id]    INT             NOT NULL,
        [Name]  NVARCHAR(50)    NOT NULL,

        CONSTRAINT [PK_LeadTrackingActionType] PRIMARY KEY CLUSTERED ([Id])
    );

    PRINT 'Created [sales].[LeadTrackingActionType] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadTrackingActionType] already exists.';
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadTrackingActionType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 1)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (1, 'MeetingScheduled');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 2)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (2, 'MeetingCancelled');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 3)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (3, 'MeetingReactivated');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 4)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (4, 'ResponseSent');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 5)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (5, 'ProposalLinked');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 6)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (6, 'ManualStageChange');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 7)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (7, 'MarkedAsWon');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 8)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (8, 'LeadCancelled');

    IF NOT EXISTS (SELECT 1 FROM [sales].[LeadTrackingActionType] WHERE [Id] = 9)
        INSERT INTO [sales].[LeadTrackingActionType] ([Id], [Name]) VALUES (9, 'LeadReactivated');

    PRINT 'Seeded [sales].[LeadTrackingActionType] data.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadTrackingHistory'
)
BEGIN
    CREATE TABLE [sales].[LeadTrackingHistory]
    (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [LeadRequestId]             INT             NOT NULL,
        [BusinessId]                INT             NOT NULL,
        [LeadTrackingActionTypeId]  INT             NOT NULL,
        [FromLeadStatusTypeId]      INT             NULL,
        [ToLeadStatusTypeId]        INT             NOT NULL,
        [RelatedEntityId]           INT             NULL,
        [CreatedByUserId]           NVARCHAR(450)   NULL,
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_LeadTrackingHistory_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_LeadTrackingHistory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_LeadTrackingHistory_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]),
        CONSTRAINT [FK_LeadTrackingHistory_ActionType] FOREIGN KEY ([LeadTrackingActionTypeId]) REFERENCES [sales].[LeadTrackingActionType]([Id])
    );

    PRINT 'Created [sales].[LeadTrackingHistory] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadTrackingHistory] already exists.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_LeadTrackingHistory_LeadRequestId_BusinessId'
      AND [object_id] = OBJECT_ID('[sales].[LeadTrackingHistory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LeadTrackingHistory_LeadRequestId_BusinessId]
    ON [sales].[LeadTrackingHistory] ([LeadRequestId], [BusinessId])
    INCLUDE ([LeadTrackingActionTypeId], [ToLeadStatusTypeId], [RelatedEntityId]);

    PRINT 'Created index [IX_LeadTrackingHistory_LeadRequestId_BusinessId].';
END
ELSE
BEGIN
    PRINT 'Index [IX_LeadTrackingHistory_LeadRequestId_BusinessId] already exists.';
END
GO
