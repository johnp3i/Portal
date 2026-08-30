-- ============================================================
-- Create MeetingOutcomeClassification lookup table and add
-- MeetingOutcomeClassificationId FK to Meeting table
-- ============================================================

USE [Portal]
GO

-- Create lookup table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingOutcomeClassification')
BEGIN
    CREATE TABLE [sales].[MeetingOutcomeClassification] (
        [Id]            INT IDENTITY(1,1) NOT NULL,
        [Name]          NVARCHAR(50) NOT NULL,
        [CreatedAtUtc]  DATETIME NOT NULL CONSTRAINT [DF_MeetingOutcomeClassification_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_MeetingOutcomeClassification] PRIMARY KEY CLUSTERED ([Id])
    );

    -- Seed values
    SET IDENTITY_INSERT [sales].[MeetingOutcomeClassification] ON;
    INSERT INTO [sales].[MeetingOutcomeClassification] ([Id], [Name]) VALUES
        (1, 'Positive'),
        (2, 'Neutral'),
        (3, 'Negative'),
        (4, 'Rescheduled'),
        (5, 'No Show');
    SET IDENTITY_INSERT [sales].[MeetingOutcomeClassification] OFF;
END
GO

-- Add nullable FK column to Meeting
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'Meeting' AND COLUMN_NAME = 'MeetingOutcomeClassificationId')
BEGIN
    ALTER TABLE [sales].[Meeting]
        ADD [MeetingOutcomeClassificationId] INT NULL;

    ALTER TABLE [sales].[Meeting]
        ADD CONSTRAINT [FK_Meeting_MeetingOutcomeClassification]
        FOREIGN KEY ([MeetingOutcomeClassificationId])
        REFERENCES [sales].[MeetingOutcomeClassification] ([Id]);
END
GO
