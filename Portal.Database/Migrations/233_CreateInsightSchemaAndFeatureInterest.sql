/*
    Migration: 233_CreateInsightSchemaAndFeatureInterest
    Description: Creates the [insight] schema and [insight].[FeatureInterest], which records
                 "Notify me when available" clicks from the Customer Purchases Dashboard.
                 Feature-interest tracking only: nothing reads this table to send notifications.
    This script is idempotent - safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'insight')
BEGIN
    EXEC('CREATE SCHEMA [insight]');
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'insight' AND t.name = N'FeatureInterest'
)
BEGIN
    CREATE TABLE [insight].[FeatureInterest] (
        [Id]           INT IDENTITY(1,1) NOT NULL,
        [BusinessId]   INT NOT NULL,
        [UserId]       NVARCHAR(450) NOT NULL,   -- Membership user id
        [FeatureKey]   NVARCHAR(50) NOT NULL,
        [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_FeatureInterest_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_FeatureInterest] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_FeatureInterest_Business]
            FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_FeatureInterest_Business_User_Feature'
      AND object_id = OBJECT_ID(N'[insight].[FeatureInterest]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_FeatureInterest_Business_User_Feature]
        ON [insight].[FeatureInterest] ([BusinessId], [UserId], [FeatureKey]);
END
GO