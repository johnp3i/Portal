-- ============================================================
-- Migration 140: Create Lead Response Template Table
-- ============================================================
-- Purpose: Creates the [sales].[LeadResponseTemplate] table — stores
--          reusable response templates that businesses can use to
--          reply to leads via different channels. Templates define
--          subject, body, and expected response time.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadResponseTemplate'
)
BEGIN
    CREATE TABLE [sales].[LeadResponseTemplate]
    (
        [Id]                    INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]            INT             NOT NULL,
        [ProductId]             INT             NULL,
        [LeadResponseTypeId]    INT             NOT NULL,
        [Name]                  NVARCHAR(200)   NOT NULL,
        [Subject]               NVARCHAR(300)   NULL,
        [BodyTemplate]          NVARCHAR(MAX)   NOT NULL,
        [ResponseTimeInHours]   INT             NOT NULL,
        [IsActive]              BIT             NOT NULL CONSTRAINT [DF_LeadResponseTemplate_IsActive] DEFAULT (1),
        [CreatedAtUtc]          DATETIME        NOT NULL CONSTRAINT [DF_LeadResponseTemplate_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_LeadResponseTemplate] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_LeadResponseTemplate_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_LeadResponseTemplate_Product] FOREIGN KEY ([ProductId]) REFERENCES [sales].[Product]([Id]),
        CONSTRAINT [FK_LeadResponseTemplate_LeadResponseType] FOREIGN KEY ([LeadResponseTypeId]) REFERENCES [sales].[LeadResponseType]([Id])
    );

    PRINT 'Created [sales].[LeadResponseTemplate] table.';
END
ELSE
BEGIN
    PRINT '[sales].[LeadResponseTemplate] already exists.';
END
GO
