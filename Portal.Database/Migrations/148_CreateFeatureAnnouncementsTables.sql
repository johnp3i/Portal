-- ============================================================
-- Migration 148: Create Feature Announcements Tables
-- ============================================================
-- Purpose: Creates the FeatureAnnouncements and UserAnnouncementDismissals
--          tables for the What's New Announcements feature.
-- Schema: [dbo]
-- ============================================================

USE [Portal]
GO

-- Table 1: Feature Announcements
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'FeatureAnnouncements'
)
BEGIN
    CREATE TABLE [dbo].[FeatureAnnouncements] (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [Title]             NVARCHAR(200)   NOT NULL,
        [Summary]           NVARCHAR(500)   NOT NULL,
        [DetailHtml]        NVARCHAR(MAX)   NOT NULL,
        [ModuleKey]         NVARCHAR(100)   NULL,
        [CtaLabel]          NVARCHAR(100)   NULL,
        [CtaUrl]            NVARCHAR(500)   NULL,
        [TargetPlanTier]    NVARCHAR(50)    NULL,
        [IsActive]          BIT             NOT NULL CONSTRAINT [DF_FeatureAnnouncements_IsActive] DEFAULT (1),
        [PublishedAtUtc]    DATETIME        NOT NULL,
        [ExpiresAtUtc]      DATETIME        NULL,
        [CreatedAtUtc]      DATETIME        NOT NULL CONSTRAINT [DF_FeatureAnnouncements_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_FeatureAnnouncements] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT 'Created [dbo].[FeatureAnnouncements] table.';
END
ELSE
BEGIN
    PRINT '[dbo].[FeatureAnnouncements] already exists.';
END
GO

-- Table 2: User Announcement Dismissals
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'UserAnnouncementDismissals'
)
BEGIN
    CREATE TABLE [dbo].[UserAnnouncementDismissals] (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [UserId]                    NVARCHAR(450)   NOT NULL,
        [FeatureAnnouncementId]     INT             NOT NULL,
        [DismissedAtUtc]            DATETIME        NOT NULL CONSTRAINT [DF_UserAnnouncementDismissals_DismissedAtUtc] DEFAULT (GETUTCDATE()),
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_UserAnnouncementDismissals_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_UserAnnouncementDismissals] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserAnnouncementDismissals_FeatureAnnouncements]
            FOREIGN KEY ([FeatureAnnouncementId]) REFERENCES [dbo].[FeatureAnnouncements]([Id]),
        CONSTRAINT [UQ_UserAnnouncementDismissals_UserAnnouncement]
            UNIQUE ([UserId], [FeatureAnnouncementId])
    );

    PRINT 'Created [dbo].[UserAnnouncementDismissals] table.';
END
ELSE
BEGIN
    PRINT '[dbo].[UserAnnouncementDismissals] already exists.';
END
GO

-- Index for fast lookup of dismissals by user
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_UserAnnouncementDismissals_UserId'
      AND [object_id] = OBJECT_ID('[dbo].[UserAnnouncementDismissals]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_UserAnnouncementDismissals_UserId]
        ON [dbo].[UserAnnouncementDismissals] ([UserId])
        INCLUDE ([FeatureAnnouncementId], [DismissedAtUtc]);

    PRINT 'Created index IX_UserAnnouncementDismissals_UserId.';
END
GO

-- Index for visible announcements query (filtering by active + dates)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_FeatureAnnouncements_Visibility'
      AND [object_id] = OBJECT_ID('[dbo].[FeatureAnnouncements]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FeatureAnnouncements_Visibility]
        ON [dbo].[FeatureAnnouncements] ([IsActive], [PublishedAtUtc], [ExpiresAtUtc])
        INCLUDE ([TargetPlanTier]);

    PRINT 'Created index IX_FeatureAnnouncements_Visibility.';
END
GO
