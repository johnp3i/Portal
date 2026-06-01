USE [Portal];
GO

/*
    Migration: 075_CreateBusinessPlanTable
    Description: Creates [dbo].[BusinessPlan] — an association table linking each tenant
                 (Business) to their active subscription plan. Each record defines the
                 subscription lifecycle dates and active status. Includes foreign keys to
                 [portal].[Business] and [dbo].[Plan], a filtered unique index ensuring
                 at most one active plan per business, and nonclustered indexes on both
                 FK columns for query performance.

    Requirements: 3.1  - Id column (INT IDENTITY PK)
                 3.2  - BusinessId column (INT, NOT NULL, FK to Business)
                 3.3  - PlanId column (INT, NOT NULL, FK to Plan)
                 3.4  - StartDateUtc column (DATETIME, NOT NULL)
                 3.5  - EndDateUtc column (DATETIME, NULL)
                 3.6  - IsActive column (BIT, NOT NULL, DEFAULT 1)
                 3.7  - CreatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
                 3.8  - Resides in [dbo] schema
                 3.9  - Filtered unique index on (BusinessId, IsActive) WHERE IsActive = 1
                 3.10 - NO ACTION referential integrity on Business deletion
                 3.11 - NO ACTION referential integrity on Plan deletion
                 3.12 - Nonclustered indexes on BusinessId and PlanId
                 7.1  - Sequential three-digit numbering
                 7.2  - IF NOT EXISTS guards for idempotency
                 7.3  - [dbo] schema
                 7.4  - Explicit FK constraint names FK_BusinessPlan_Business, FK_BusinessPlan_Plan
                 7.5  - Nonclustered indexes IX_BusinessPlan_BusinessId, IX_BusinessPlan_PlanId
                 7.6  - Header comment block
                 7.7  - GO batch terminators
                 7.8  - Explicit PK constraint name PK_BusinessPlan

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[BusinessPlan]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'BusinessPlan'
)
BEGIN
    CREATE TABLE [dbo].[BusinessPlan]
    (
        [Id]            INT       IDENTITY(1,1)   NOT NULL,
        [BusinessId]    INT                       NOT NULL,
        [PlanId]        INT                       NOT NULL,
        [StartDateUtc]  DATETIME                  NOT NULL,
        [EndDateUtc]    DATETIME                  NULL,
        [IsActive]      BIT                       NOT NULL  CONSTRAINT [DF_BusinessPlan_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME                  NOT NULL  CONSTRAINT [DF_BusinessPlan_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_BusinessPlan] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessPlan_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessPlan_Plan] FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plan] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- =============================================================================
-- 2. Filtered unique index — one active plan per business
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_BusinessPlan_BusinessId_IsActive'
      AND [object_id] = OBJECT_ID('[dbo].[BusinessPlan]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_BusinessPlan_BusinessId_IsActive]
        ON [dbo].[BusinessPlan] ([BusinessId], [IsActive])
        WHERE [IsActive] = 1;
END
GO

-- =============================================================================
-- 3. Nonclustered index on BusinessId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_BusinessPlan_BusinessId'
      AND [object_id] = OBJECT_ID('[dbo].[BusinessPlan]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BusinessPlan_BusinessId]
        ON [dbo].[BusinessPlan] ([BusinessId]);
END
GO

-- =============================================================================
-- 4. Nonclustered index on PlanId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_BusinessPlan_PlanId'
      AND [object_id] = OBJECT_ID('[dbo].[BusinessPlan]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BusinessPlan_PlanId]
        ON [dbo].[BusinessPlan] ([PlanId]);
END
GO
