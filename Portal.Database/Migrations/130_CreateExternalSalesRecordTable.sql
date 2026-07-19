-- ============================================================
-- Migration 130: Create ExternalSalesRecord table
-- ============================================================
-- Purpose: Stores transaction-level POS sales data imported from
--          external systems. Each record represents one individual
--          sale/transaction (unlike RevenueSummary which is aggregated).
--          Supports optional Customer FK for behaviour analytics.
-- Schema: [revenue]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'ExternalSalesRecord'
)
BEGIN
    CREATE TABLE [revenue].[ExternalSalesRecord]
    (
        [Id]                    INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]            INT             NOT NULL,
        [RevenueSourceId]       INT             NULL,
        [TransactionDate]       DATE            NOT NULL,
        [InvoiceNumber]         NVARCHAR(100)   NULL,
        [CustomerId]            INT             NULL,
        [NetAmount]             DECIMAL(18,2)   NOT NULL,
        [VatAmount]             DECIMAL(18,2)   NOT NULL,
        [TotalAmount]           DECIMAL(18,2)   NOT NULL,
        [Description]           NVARCHAR(500)   NULL,
        [PaymentMethod]         NVARCHAR(50)    NULL,
        [ImportSessionId]       INT             NULL,
        [VatSubmissionPeriodId] INT             NULL,
        [IsActive]              BIT             NOT NULL CONSTRAINT [DF_ExternalSalesRecord_IsActive] DEFAULT (1),
        [CreatedAtUtc]          DATETIME        NOT NULL CONSTRAINT [DF_ExternalSalesRecord_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ExternalSalesRecord] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ExternalSalesRecord_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]),
        CONSTRAINT [FK_ExternalSalesRecord_RevenueSource] FOREIGN KEY ([RevenueSourceId]) REFERENCES [revenue].[RevenueSource]([Id]),
        CONSTRAINT [FK_ExternalSalesRecord_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer]([Id]),
        CONSTRAINT [FK_ExternalSalesRecord_VatPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod]([Id])
    );

    PRINT 'Created [revenue].[ExternalSalesRecord] table.';
END
ELSE
BEGIN
    PRINT '[revenue].[ExternalSalesRecord] already exists.';
END
GO

-- Indexes for common queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ExternalSalesRecord_BusinessId_TransactionDate' AND [object_id] = OBJECT_ID('[revenue].[ExternalSalesRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExternalSalesRecord_BusinessId_TransactionDate]
        ON [revenue].[ExternalSalesRecord] ([BusinessId], [TransactionDate] DESC)
        INCLUDE ([RevenueSourceId], [TotalAmount], [VatAmount], [IsActive]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_ExternalSalesRecord_VatPeriod' AND [object_id] = OBJECT_ID('[revenue].[ExternalSalesRecord]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ExternalSalesRecord_VatPeriod]
        ON [revenue].[ExternalSalesRecord] ([BusinessId], [VatSubmissionPeriodId])
        INCLUDE ([VatAmount], [IsActive]);
END
GO
