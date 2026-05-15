/*
    Migration: 002_CreateBusinessTable
    Description: Creates the [portal].Business table — the tenant entity representing
                 a subscribing company within the platform.

    Requirements: 1.1 - THE Portal_Database SHALL contain a [portal].Business table
                  1.2 - THE Portal_Database SHALL enforce uniqueness on [portal].Business.Name
                  1.3 - THE Portal_Database SHALL use the [portal] schema for all tenant-level tables

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal'
      AND TABLE_NAME = 'Business'
)
BEGIN
    CREATE TABLE [portal].[Business]
    (
        [Id]           INT            IDENTITY(1,1)  NOT NULL,
        [Name]         NVARCHAR(200)                 NOT NULL,
        [IsActive]     BIT                           NOT NULL  CONSTRAINT [DF_Business_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2                     NOT NULL  CONSTRAINT [DF_Business_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc] DATETIME2                     NOT NULL  CONSTRAINT [DF_Business_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Business] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_Business_Name] UNIQUE ([Name])
    );
END
GO
