-- ============================================================
-- Migration 194: Create notification outbox, per-business settings, and opt-out tables
-- ============================================================
-- Purpose: Core spine tables:
--          - [notification].[OutboxMessage]         durable pending/sent/failed messages
--          - [notification].[BusinessAssistantSetting] per-business enable/hours/footer
--          - [notification].[AssistantOptOut]        per-service, per-recipient opt-out
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

-- ---- OutboxMessage --------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'OutboxMessage'
)
BEGIN
    CREATE TABLE [notification].[OutboxMessage]
    (
        [Id]                INT            IDENTITY(1,1) NOT NULL,
        [BusinessId]        INT            NOT NULL,
        [AssistantTypeId]   INT            NOT NULL,
        [RecipientEmail]    NVARCHAR(320)  NOT NULL,
        [RecipientName]     NVARCHAR(200)  NULL,
        [ReplyToEmail]      NVARCHAR(320)  NULL,
        [Subject]           NVARCHAR(300)  NOT NULL,
        [BodyHtml]          NVARCHAR(MAX)  NOT NULL,
        [Status]            NVARCHAR(20)   NOT NULL CONSTRAINT [DF_OutboxMessage_Status] DEFAULT ('Pending'),
        [RetryCount]        INT            NOT NULL CONSTRAINT [DF_OutboxMessage_RetryCount] DEFAULT (0),
        [MaxRetries]        INT            NOT NULL CONSTRAINT [DF_OutboxMessage_MaxRetries] DEFAULT (5),
        [ScheduledForUtc]   DATETIME       NOT NULL,
        [LastError]         NVARCHAR(MAX)  NULL,
        [SentAtUtc]         DATETIME       NULL,
        [FailedAtUtc]       DATETIME       NULL,
        [RelatedEntityType] NVARCHAR(50)   NULL,
        [RelatedEntityId]   INT            NULL,
        [CreatedAtUtc]      DATETIME       NOT NULL CONSTRAINT [DF_OutboxMessage_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_OutboxMessage] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_OutboxMessage_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_OutboxMessage_AssistantType] FOREIGN KEY ([AssistantTypeId]) REFERENCES [notification].[AssistantType]([Id]),
        CONSTRAINT [CK_OutboxMessage_Status] CHECK ([Status] IN ('Pending', 'Sent', 'Failed'))
    );

    CREATE INDEX [IX_OutboxMessage_Status_ScheduledForUtc]
        ON [notification].[OutboxMessage] ([Status], [ScheduledForUtc]);
    CREATE INDEX [IX_OutboxMessage_BusinessId_AssistantTypeId_CreatedAtUtc]
        ON [notification].[OutboxMessage] ([BusinessId], [AssistantTypeId], [CreatedAtUtc] DESC);
    CREATE INDEX [IX_OutboxMessage_Status_FailedAtUtc]
        ON [notification].[OutboxMessage] ([Status], [FailedAtUtc]);

    PRINT 'Created [notification].[OutboxMessage] table.';
END
ELSE
BEGIN
    PRINT '[notification].[OutboxMessage] already exists.';
END
GO

-- ---- BusinessAssistantSetting --------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'BusinessAssistantSetting'
)
BEGIN
    CREATE TABLE [notification].[BusinessAssistantSetting]
    (
        [Id]                      INT      IDENTITY(1,1) NOT NULL,
        [BusinessId]              INT      NOT NULL,
        [AssistantTypeId]         INT      NOT NULL,
        [IsEnabled]               BIT      NOT NULL CONSTRAINT [DF_BusinessAssistantSetting_IsEnabled] DEFAULT (1),
        [IsBrandingFooterEnabled] BIT      NOT NULL CONSTRAINT [DF_BusinessAssistantSetting_IsBrandingFooterEnabled] DEFAULT (1),
        [WorkingHoursStart]       TIME     NULL,
        [WorkingHoursEnd]         TIME     NULL,
        [CreatedAtUtc]            DATETIME NOT NULL CONSTRAINT [DF_BusinessAssistantSetting_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]            DATETIME NULL,

        CONSTRAINT [PK_BusinessAssistantSetting] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessAssistantSetting_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_BusinessAssistantSetting_AssistantType] FOREIGN KEY ([AssistantTypeId]) REFERENCES [notification].[AssistantType]([Id]),
        CONSTRAINT [UQ_BusinessAssistantSetting_Business_Assistant] UNIQUE ([BusinessId], [AssistantTypeId])
    );
    PRINT 'Created [notification].[BusinessAssistantSetting] table.';
END
ELSE
BEGIN
    PRINT '[notification].[BusinessAssistantSetting] already exists.';
END
GO

-- ---- AssistantOptOut ------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'AssistantOptOut'
)
BEGIN
    CREATE TABLE [notification].[AssistantOptOut]
    (
        [Id]              INT           IDENTITY(1,1) NOT NULL,
        [BusinessId]      INT           NOT NULL,
        [AssistantTypeId] INT           NOT NULL,
        [CustomerId]      INT           NULL,
        [RecipientEmail]  NVARCHAR(320) NOT NULL,
        [OptedOutAtUtc]   DATETIME      NOT NULL CONSTRAINT [DF_AssistantOptOut_OptedOutAtUtc] DEFAULT (GETUTCDATE()),
        [CreatedAtUtc]    DATETIME      NOT NULL CONSTRAINT [DF_AssistantOptOut_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_AssistantOptOut] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AssistantOptOut_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_AssistantOptOut_AssistantType] FOREIGN KEY ([AssistantTypeId]) REFERENCES [notification].[AssistantType]([Id]),
        CONSTRAINT [UQ_AssistantOptOut_Business_Assistant_Email] UNIQUE ([BusinessId], [AssistantTypeId], [RecipientEmail])
    );
    PRINT 'Created [notification].[AssistantOptOut] table.';
END
ELSE
BEGIN
    PRINT '[notification].[AssistantOptOut] already exists.';
END
GO
