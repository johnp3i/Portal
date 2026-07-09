/*
    Migration: 104_CreateCashFlowSettingsTable
    Description: Creates [cashflow] schema and CashFlowSettings table.
                 Stores per-business starting balance and alert threshold
                 for the Cash Flow Forecasting module.
    Requirements: 1.1
*/

USE [Portal]
GO

-- Create schema if not exists
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'cashflow')
BEGIN
    EXEC('CREATE SCHEMA [cashflow]')
END
GO

-- Create CashFlowSettings table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'cashflow' AND TABLE_NAME = 'CashFlowSettings'
)
BEGIN
    CREATE TABLE [cashflow].[CashFlowSettings]
    (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]        INT             NOT NULL,
        [StartingBalance]   DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_CashFlowSettings_StartingBalance] DEFAULT (0),
        [AlertThreshold]    DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_CashFlowSettings_AlertThreshold] DEFAULT (0),
        [CreatedAtUtc]      DATETIME        NOT NULL CONSTRAINT [DF_CashFlowSettings_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]      DATETIME        NOT NULL CONSTRAINT [DF_CashFlowSettings_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_CashFlowSettings] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CashFlowSettings_Business] FOREIGN KEY ([BusinessId])
            REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [UQ_CashFlowSettings_BusinessId] UNIQUE ([BusinessId]),
        CONSTRAINT [CK_CashFlowSettings_StartingBalance] CHECK ([StartingBalance] >= 0),
        CONSTRAINT [CK_CashFlowSettings_AlertThreshold] CHECK ([AlertThreshold] >= 0)
    )
END
GO

-- Index for tenant isolation lookups
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_CashFlowSettings_BusinessId'
      AND [object_id] = OBJECT_ID('[cashflow].[CashFlowSettings]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CashFlowSettings_BusinessId]
        ON [cashflow].[CashFlowSettings]([BusinessId])
END
GO
