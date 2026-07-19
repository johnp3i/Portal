/*
    Migration: 127_CreateRevenueSourceTable
    Description: Creates the [revenue].RevenueSource table — a configurable label
                 representing where external revenue comes from (e.g., POS system,
                 market stall, online store). Scoped to a Business tenant with a
                 foreign key to [portal].Business.

    Requirements: 2.1 - THE system SHALL store Revenue Sources per business
                  2.2 - THE system SHALL capture Name (required, ≤200 chars) and Description (optional, ≤500 chars)
                  2.3 - THE system SHALL support soft-disable via IsActive flag

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'RevenueSource'
)
BEGIN
    CREATE TABLE [revenue].[RevenueSource]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [Name]          NVARCHAR(200)                 NOT NULL,
        [Description]   NVARCHAR(500)                 NULL,
        [IsActive]      BIT                           NOT NULL  CONSTRAINT [DF_RevenueSource_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME2                     NOT NULL  CONSTRAINT [DF_RevenueSource_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_RevenueSource] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RevenueSource_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_RevenueSource_BusinessId'
      AND [object_id] = OBJECT_ID('[revenue].[RevenueSource]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RevenueSource_BusinessId]
        ON [revenue].[RevenueSource] ([BusinessId]);
END
GO
