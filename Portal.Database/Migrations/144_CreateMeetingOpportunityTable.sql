-- ============================================================
-- Migration 144: Create Meeting Opportunity Table
-- ============================================================
-- Purpose: Creates the [sales].[MeetingOpportunity] table —
--          captures business opportunities identified during a meeting.
--          Tracks title, description, and estimated monetary value.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'MeetingOpportunity'
)
BEGIN
    CREATE TABLE [sales].[MeetingOpportunity]
    (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [MeetingId]         INT             NOT NULL,
        [Title]             NVARCHAR(300)   NOT NULL,
        [Description]       NVARCHAR(MAX)   NULL,
        [EstimatedValue]    DECIMAL(18,2)   NULL,
        [IsActive]          BIT             NOT NULL CONSTRAINT [DF_MeetingOpportunity_IsActive] DEFAULT (1),
        [CreatedAtUtc]      DATETIME        NOT NULL CONSTRAINT [DF_MeetingOpportunity_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_MeetingOpportunity] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_MeetingOpportunity_Meeting] FOREIGN KEY ([MeetingId]) REFERENCES [sales].[Meeting]([Id])
    );

    PRINT 'Created [sales].[MeetingOpportunity] table.';
END
ELSE
BEGIN
    PRINT '[sales].[MeetingOpportunity] already exists.';
END
GO
