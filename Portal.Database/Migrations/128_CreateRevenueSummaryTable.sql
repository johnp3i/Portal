/*
    Migration: 128_CreateRevenueSummaryTable
    Description: Creates the [revenue].RevenueSummary table — the header record for
                 a Z-Report or external revenue batch entry. Contains aggregated totals
                 and metadata. Scoped to a Business tenant with foreign keys to
                 [portal].Business, [revenue].RevenueSource, and [vat].VatSubmissionPeriod.

    Requirements: 4.1 - THE system SHALL store Revenue Summaries per business
                  4.2 - THE system SHALL capture all header fields (dates, totals, metadata)
                  4.3 - THE system SHALL link to RevenueSource and optionally to VatSubmissionPeriod
                  4.4 - THE system SHALL support soft-delete via IsActive flag

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'RevenueSummary'
)
BEGIN
    CREATE TABLE [revenue].[RevenueSummary]
    (
        [Id]                    INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]            INT                           NOT NULL,
        [RevenueSourceId]       INT                           NOT NULL,
        [SummaryDate]           DATE                          NOT NULL,
        [PeriodEndDate]         DATE                          NULL,
        [ZReportNumber]         NVARCHAR(50)                  NULL,
        [TotalNet]              DECIMAL(18,2)                 NOT NULL,
        [TotalVat]              DECIMAL(18,2)                 NOT NULL,
        [TotalGross]            DECIMAL(18,2)                 NOT NULL,
        [TotalDiscount]         DECIMAL(18,2)                 NULL,
        [TransactionCount]      INT                           NULL,
        [Reference]             NVARCHAR(200)                 NULL,
        [Notes]                 NVARCHAR(MAX)                 NULL,
        [ExportedAtUtc]         DATETIME2                     NULL,
        [VatSubmissionPeriodId] INT                           NULL,
        [ImportSessionId]       INT                           NULL,
        [IsActive]              BIT                           NOT NULL  CONSTRAINT [DF_RevenueSummary_IsActive] DEFAULT (1),
        [CreatedAtUtc]          DATETIME2                     NOT NULL  CONSTRAINT [DF_RevenueSummary_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_RevenueSummary] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RevenueSummary_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_RevenueSummary_RevenueSource] FOREIGN KEY ([RevenueSourceId]) REFERENCES [revenue].[RevenueSource] ([Id]),
        CONSTRAINT [FK_RevenueSummary_VatPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_RevenueSummary_BusinessId'
      AND [object_id] = OBJECT_ID('[revenue].[RevenueSummary]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RevenueSummary_BusinessId]
        ON [revenue].[RevenueSummary] ([BusinessId]);
END
GO

-- Non-clustered index on RevenueSourceId for source-filtered queries
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_RevenueSummary_RevenueSourceId'
      AND [object_id] = OBJECT_ID('[revenue].[RevenueSummary]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RevenueSummary_RevenueSourceId]
        ON [revenue].[RevenueSummary] ([RevenueSourceId]);
END
GO

-- Non-clustered index on VatSubmissionPeriodId for VAT period queries
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_RevenueSummary_VatSubmissionPeriodId'
      AND [object_id] = OBJECT_ID('[revenue].[RevenueSummary]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_RevenueSummary_VatSubmissionPeriodId]
        ON [revenue].[RevenueSummary] ([VatSubmissionPeriodId]);
END
GO
