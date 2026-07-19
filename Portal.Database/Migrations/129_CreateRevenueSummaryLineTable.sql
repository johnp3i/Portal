/*
    Migration: 129_CreateRevenueSummaryLineTable
    Description: Creates the [revenue].RevenueSummaryLine table — individual VAT-rate
                 breakdown lines within a Revenue Summary. Each line represents a
                 distinct VAT rate bucket with net, VAT, and total amounts.

    Requirements: 5.1 - THE system SHALL store Revenue Summary Lines per summary
                  5.2 - THE system SHALL capture VatRate, NetAmount, VatAmount, TotalAmount, DiscountAmount, Description
                  5.3 - THE system SHALL enforce FK to RevenueSummary

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'RevenueSummaryLine'
)
BEGIN
    CREATE TABLE [revenue].[RevenueSummaryLine]
    (
        [Id]                INT            IDENTITY(1,1)  NOT NULL,
        [RevenueSummaryId]  INT                           NOT NULL,
        [VatRate]           DECIMAL(5,2)                  NOT NULL,
        [NetAmount]         DECIMAL(18,2)                 NOT NULL,
        [VatAmount]         DECIMAL(18,2)                 NOT NULL,
        [TotalAmount]       DECIMAL(18,2)                 NOT NULL,
        [DiscountAmount]    DECIMAL(18,2)                 NULL,
        [Description]       NVARCHAR(200)                 NULL,
        [CreatedAtUtc]      DATETIME2                     NOT NULL  CONSTRAINT [DF_RevenueSummaryLine_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_RevenueSummaryLine] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RevenueSummaryLine_Summary] FOREIGN KEY ([RevenueSummaryId]) REFERENCES [revenue].[RevenueSummary] ([Id])
    );
END
GO

-- Non-clustered index on RevenueSummaryId for parent-child lookups
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_RevenueSummaryLine_RevenueSummaryId'
      AND [object_id] = OBJECT_ID('[revenue].[RevenueSummaryLine]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RevenueSummaryLine_RevenueSummaryId]
        ON [revenue].[RevenueSummaryLine] ([RevenueSummaryId]);
END
GO
