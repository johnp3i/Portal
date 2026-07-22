-- ============================================================
-- Migration 141: Create Lead Response Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadResponse] table — records each
--          response sent to a lead request. Tracks who responded,
--          which template was used (if any), and the channel used.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadResponse'
)
BEGIN
    CREATE TABLE [sales].[LeadResponse]
    (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [LeadRequestId]             INT             NOT NULL,
        [LeadResponseTypeId]        INT             NOT NULL,
        [LeadResponseTemplateId]    INT             NULL,
        [RespondedByUserId]         NVARCHAR(450)   NULL,
        [ResponseText]              NVARCHAR(MAX)   NULL,
        [IsAutomated]               BIT             NOT NULL CONSTRAINT [DF_LeadResponse_IsAutomated] DEFAULT (0),
        [SentAtUtc]                 DATETIME        NOT NULL,
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_LeadResponse_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_LeadResponse] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_LeadResponse_LeadRequest] FOREIGN KEY ([LeadRequestId]) REFERENCES [sales].[LeadRequest]([Id]),
        CONSTRAINT [FK_LeadResponse_LeadResponseType] FOREIGN KEY ([LeadResponseTypeId]) REFERENCES [sales].[LeadResponseType]([Id]),
        CONSTRAINT [FK_LeadResponse_LeadResponseTemplate] FOREIGN KEY ([LeadResponseTemplateId]) REFERENCES [sales].[LeadResponseTemplate]([Id])
    );

    PRINT 'Created [sales].[LeadResponse] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadResponse] already exists.';
END
GO
