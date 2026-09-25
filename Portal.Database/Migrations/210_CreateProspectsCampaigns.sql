-- ============================================================
-- Migration 210: Create Prospects & Campaigns tables
-- ============================================================
-- Purpose: Adds the prospecting layer that sits BEFORE the Lead pipeline
--          (a complementary sub-area under Sales/Opportunities that replaces
--          the prospecting spreadsheet). Three tables:
--            [sales].[ProspectCampaign]  - a named prospecting push per product
--            [sales].[Prospect]          - a researched target inside a campaign
--                                          (NOT a Sales Contact; never touches the
--                                          pipeline until Convert-to-Lead)
--            [sales].[ProspectActivity]  - a prospect's own activity timeline
--                                          (the [sales].[ActivityFeed] is lead-keyed
--                                          and cannot represent a pre-conversion prospect)
--          Prospect keeps a ConvertedLeadRequestId back-reference so predicted
--          score/priority can later be measured against actual conversion.
-- Schema: [sales]
-- Idempotent.
-- ============================================================

USE [Portal]
GO

-- 1. ProspectCampaign -----------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'ProspectCampaign'
)
BEGIN
    CREATE TABLE [sales].[ProspectCampaign]
    (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]        INT             NOT NULL,
        [Name]              NVARCHAR(200)   NOT NULL,
        [SalesProductId]    INT             NULL,
        [Description]       NVARCHAR(1000)  NULL,
        [Market]            NVARCHAR(200)   NULL,
        [StartDate]         DATE            NULL,
        [EndDate]           DATE            NULL,
        [WeeklyCallTarget]  INT             NOT NULL CONSTRAINT [DF_ProspectCampaign_WeeklyCallTarget] DEFAULT (0),
        [OwnerUserId]       NVARCHAR(450)   NULL,
        [Status]            TINYINT         NOT NULL CONSTRAINT [DF_ProspectCampaign_Status] DEFAULT (1), -- 1 Draft, 2 Active, 3 Closed
        [Notes]             NVARCHAR(MAX)   NULL,
        [CreatedAtUtc]      DATETIME        NOT NULL CONSTRAINT [DF_ProspectCampaign_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ProspectCampaign] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProspectCampaign_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_ProspectCampaign_Product] FOREIGN KEY ([SalesProductId]) REFERENCES [sales].[Product]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_ProspectCampaign_Business]
        ON [sales].[ProspectCampaign] ([BusinessId]);

    PRINT 'Created [sales].[ProspectCampaign] table.';
END
ELSE
BEGIN
    PRINT '[sales].[ProspectCampaign] already exists.';
END
GO

-- 2. Prospect -------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'Prospect'
)
BEGIN
    CREATE TABLE [sales].[Prospect]
    (
        [Id]                        INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]                INT             NOT NULL,
        [ProspectCampaignId]        INT             NOT NULL,
        [Name]                      NVARCHAR(200)   NOT NULL,
        [Segment]                   NVARCHAR(100)   NULL,
        [Location]                  NVARCHAR(150)   NULL,
        [BusinessType]              NVARCHAR(150)   NULL,
        [PublicContactRole]         NVARCHAR(200)   NULL,
        [Phone]                     NVARCHAR(60)    NULL,   -- research field, NOT a Sales Contact
        [Email]                     NVARCHAR(320)   NULL,   -- research field, NOT a Sales Contact
        [Website]                   NVARCHAR(300)   NULL,
        [PublicEvidence]            NVARCHAR(MAX)   NULL,
        [WhyFit]                    NVARCHAR(MAX)   NULL,
        [ResearchSourceUrl]         NVARCHAR(500)   NULL,
        [RecommendedFirstContact]   NVARCHAR(300)   NULL,
        [IcpFitScore]               TINYINT         NOT NULL CONSTRAINT [DF_Prospect_IcpFitScore] DEFAULT (0),          -- 0-5
        [PainProbabilityScore]      TINYINT         NOT NULL CONSTRAINT [DF_Prospect_PainProbabilityScore] DEFAULT (0), -- 0-5
        [AccessibilityScore]        TINYINT         NOT NULL CONSTRAINT [DF_Prospect_AccessibilityScore] DEFAULT (0),   -- 0-5
        [LearningValueScore]        TINYINT         NOT NULL CONSTRAINT [DF_Prospect_LearningValueScore] DEFAULT (0),   -- 0-5
        -- Total (0-20) and Priority are COMPUTED in the service/DTO, not stored.
        [Status]                    TINYINT         NOT NULL CONSTRAINT [DF_Prospect_Status] DEFAULT (1), -- 1 Research,2 Ready,3 Contacting,4 Engaged,5 Converted,6 Disqualified
        [AssignedToUserId]          NVARCHAR(450)   NULL,
        [NextAction]                NVARCHAR(300)   NULL,
        [NextActionDate]            DATE            NULL,
        [ConvertedLeadRequestId]    INT             NULL,   -- measurement link: prospect -> created lead
        [ConvertedAtUtc]            DATETIME        NULL,
        [ImportRowId]               NVARCHAR(40)    NULL,   -- workbook 'ID' (e.g. A01) for idempotent re-import; NOT the PK
        [CreatedAtUtc]              DATETIME        NOT NULL CONSTRAINT [DF_Prospect_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Prospect] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Prospect_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_Prospect_ProspectCampaign] FOREIGN KEY ([ProspectCampaignId]) REFERENCES [sales].[ProspectCampaign]([Id]),
        CONSTRAINT [FK_Prospect_LeadRequest] FOREIGN KEY ([ConvertedLeadRequestId]) REFERENCES [sales].[LeadRequest]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Prospect_Campaign]
        ON [sales].[Prospect] ([ProspectCampaignId]);
    CREATE NONCLUSTERED INDEX [IX_Prospect_Business_Status]
        ON [sales].[Prospect] ([BusinessId], [Status]);

    PRINT 'Created [sales].[Prospect] table.';
END
ELSE
BEGIN
    PRINT '[sales].[Prospect] already exists.';
END
GO

-- 3. ProspectActivity -----------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'ProspectActivity'
)
BEGIN
    CREATE TABLE [sales].[ProspectActivity]
    (
        [Id]                    INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]            INT             NOT NULL,
        [ProspectId]            INT             NOT NULL,
        [ActivityType]          TINYINT         NOT NULL, -- 1 Research,2 Call,3 Email,4 Meeting,5 Demo,6 Note,7 Social,8 Other
        [OccurredAtUtc]         DATETIME        NOT NULL CONSTRAINT [DF_ProspectActivity_OccurredAtUtc] DEFAULT (GETUTCDATE()),
        [PerformedByUserId]     NVARCHAR(450)   NULL,
        [Outcome]               NVARCHAR(200)   NULL,
        [Notes]                 NVARCHAR(MAX)   NULL,
        [NextAction]            NVARCHAR(300)   NULL,
        [NextActionDate]        DATE            NULL,
        [CreatedAtUtc]          DATETIME        NOT NULL CONSTRAINT [DF_ProspectActivity_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ProspectActivity] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProspectActivity_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_ProspectActivity_Prospect] FOREIGN KEY ([ProspectId]) REFERENCES [sales].[Prospect]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_ProspectActivity_Prospect]
        ON [sales].[ProspectActivity] ([ProspectId], [OccurredAtUtc] DESC);

    PRINT 'Created [sales].[ProspectActivity] table.';
END
ELSE
BEGIN
    PRINT '[sales].[ProspectActivity] already exists.';
END
GO
