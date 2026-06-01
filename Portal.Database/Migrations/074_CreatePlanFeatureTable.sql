USE [Portal];
GO

/*
    Migration: 074_CreatePlanFeatureTable
    Description: Creates [dbo].[PlanFeature] — a mapping table associating platform modules
                 with subscription plans. Each record defines whether a specific module is
                 included in a given plan. Includes a foreign key to [dbo].[Plan], a unique
                 constraint on (PlanId, ModuleName) to prevent duplicate assignments, and a
                 nonclustered index on PlanId for query performance.

    Requirements: 2.1  - Id column (INT IDENTITY PK)
                 2.2  - PlanId column (INT, NOT NULL, FK to Plan)
                 2.3  - ModuleName column (NVARCHAR(50), NOT NULL)
                 2.4  - IsIncluded column (BIT, NOT NULL, DEFAULT 1)
                 2.5  - Unique constraint on (PlanId, ModuleName)
                 2.6  - CreatedAtUtc column (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
                 2.7  - Resides in [dbo] schema
                 2.8  - NO ACTION referential integrity on Plan deletion
                 2.9  - Duplicate PlanId+ModuleName rejected by unique constraint
                 7.1  - Sequential three-digit numbering
                 7.2  - IF NOT EXISTS guards for idempotency
                 7.3  - [dbo] schema
                 7.4  - Explicit FK constraint name FK_PlanFeature_Plan
                 7.5  - Nonclustered index IX_PlanFeature_PlanId on FK column
                 7.6  - Header comment block
                 7.7  - GO batch terminators
                 7.8  - Explicit PK constraint name PK_PlanFeature

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[PlanFeature]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'PlanFeature'
)
BEGIN
    CREATE TABLE [dbo].[PlanFeature]
    (
        [Id]            INT           IDENTITY(1,1)   NOT NULL,
        [PlanId]        INT                           NOT NULL,
        [ModuleName]    NVARCHAR(50)                  NOT NULL,
        [IsIncluded]    BIT                           NOT NULL  CONSTRAINT [DF_PlanFeature_IsIncluded] DEFAULT (1),
        [CreatedAtUtc]  DATETIME                      NOT NULL  CONSTRAINT [DF_PlanFeature_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PlanFeature] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PlanFeature_Plan] FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plan] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [UX_PlanFeature_PlanId_ModuleName] UNIQUE ([PlanId], [ModuleName])
    );
END
GO

-- =============================================================================
-- 2. Nonclustered index on PlanId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PlanFeature_PlanId'
      AND [object_id] = OBJECT_ID('[dbo].[PlanFeature]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PlanFeature_PlanId]
        ON [dbo].[PlanFeature] ([PlanId]);
END
GO
