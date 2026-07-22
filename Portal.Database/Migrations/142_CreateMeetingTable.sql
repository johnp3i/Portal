-- ============================================================
-- Migration 142: Create Meeting Table
-- ============================================================
-- Purpose: Creates the [sales].[Meeting] table — represents a
--          scheduled meeting between the business and a contact.
--          Optionally linked to a lead request for pipeline tracking.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'Meeting'
)
BEGIN
    CREATE TABLE [sales].[Meeting]
    (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]                INT             NOT NULL,
        [LeadRequestId]             INT             NULL,
        [ContactId]                 INT             NOT NULL,
        [MeetingTypeId]             INT             NOT NULL,
        [Subject]                   NVARCHAR(300)   NOT NULL,
        [ScheduledAtUtc]            DATETIME        NOT NULL,
        [DurationMinutes]           INT             NOT NULL CONSTRAINT [DF_Meeting_DurationMinutes] DEFAULT (60),
        [Location]                  NVARCHAR(300)   NULL,
        [Notes]                     NVARCHAR(MAX)   NULL,
        [Outcome]                   NVARCHAR(MAX)   NULL,
        [IsCancelled]               BIT             NOT NULL CONSTRAINT [DF_Meeting_IsCancelled] DEFAULT (0),
        [CancellationTimestamp]     DATETIME        NULL,
        [CancellationDescription]   NVARCHAR(500)   NULL,
        [IsActive]                  BIT             NOT NULL CONSTRAINT [DF_Meeting_IsActive] DEFAULT (1),
        [CreatedByUserId]           NVARCHAR(450)   NOT NULL,
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_Meeting_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Meeting] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Meeting_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_Meeting_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]),
        CONSTRAINT [FK_Meeting_Contact] FOREIGN KEY ([ContactId]) REFERENCES [sales].[Contact]([Id]),
        CONSTRAINT [FK_Meeting_MeetingType] FOREIGN KEY ([MeetingTypeId]) REFERENCES [sales].[MeetingType]([Id])
    );

    PRINT 'Created [sales].[Meeting] table.';
END
ELSE
BEGIN
    PRINT '[sales].[Meeting] already exists.';
END
GO
