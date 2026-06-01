USE [Portal];
GO

/*
    Migration: 073_CreatePlanTable
    Description: Creates [dbo].[Plan] — a subscription tier record defining pricing,
                 billing cycle, user limits, and metadata for a subscription offering.
                 Includes CHECK constraints for price and user limit validation,
                 and a unique constraint on the Slug column for URL-safe identification.

    Requirements: 1.1  - Unique integer identity primary key
                 1.2  - Name column (NVARCHAR(100), NOT NULL)
                 1.3  - Slug column (NVARCHAR(50), NOT NULL, UNIQUE)
                 1.4  - MonthlyPriceEur (DECIMAL(10,2), NOT NULL, CHECK >= 0.00)
                 1.5  - AnnualPriceEur (DECIMAL(10,2), NULL, CHECK >= 0.00 when not NULL)
                 1.6  - MaxUsers (INT, NOT NULL, -1 or >= 1)
                 1.7  - IsActive (BIT, NOT NULL, DEFAULT 1)
                 1.8  - DisplayOrder (INT, NOT NULL)
                 1.9  - Description (NVARCHAR(500), NULL)
                 1.10 - CreatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
                 1.11 - Resides in [dbo] schema
                 1.12 - UpdatedAtUtc (DATETIME, NOT NULL, DEFAULT GETUTCDATE())
                 7.1  - Sequential three-digit numbering
                 7.2  - IF NOT EXISTS guards for idempotency
                 7.3  - [dbo] schema
                 7.6  - Header comment block
                 7.7  - GO batch terminators
                 7.8  - Explicit PK constraint name

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [dbo].[Plan]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Plan'
)
BEGIN
    CREATE TABLE [dbo].[Plan]
    (
        [Id]              INT             IDENTITY(1,1)   NOT NULL,
        [Name]            NVARCHAR(100)                   NOT NULL,
        [Slug]            NVARCHAR(50)                    NOT NULL,
        [MonthlyPriceEur] DECIMAL(10,2)                  NOT NULL,
        [AnnualPriceEur]  DECIMAL(10,2)                  NULL,
        [MaxUsers]        INT                             NOT NULL,
        [IsActive]        BIT                             NOT NULL  CONSTRAINT [DF_Plan_IsActive] DEFAULT (1),
        [DisplayOrder]    INT                             NOT NULL,
        [Description]     NVARCHAR(500)                   NULL,
        [CreatedAtUtc]    DATETIME                        NOT NULL  CONSTRAINT [DF_Plan_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]    DATETIME                        NOT NULL  CONSTRAINT [DF_Plan_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Plan] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UX_Plan_Slug] UNIQUE ([Slug]),
        CONSTRAINT [CK_Plan_MonthlyPriceEur] CHECK ([MonthlyPriceEur] >= 0.00),
        CONSTRAINT [CK_Plan_AnnualPriceEur] CHECK ([AnnualPriceEur] IS NULL OR [AnnualPriceEur] >= 0.00),
        CONSTRAINT [CK_Plan_MaxUsers] CHECK ([MaxUsers] = -1 OR [MaxUsers] >= 1)
    );
END
GO
