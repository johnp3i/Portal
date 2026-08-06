-- ============================================================
-- Phase D: Create PayslipPeriodComplianceFiling table
-- Cross-reference between payslip periods and compliance filings.
-- Each finalisation creates a new record (preserves history).
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayslipPeriodComplianceFiling' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[PayslipPeriodComplianceFiling]
    (
        [Id]                    INT IDENTITY(1,1) NOT NULL,
        [PayslipPeriodId]       INT NOT NULL,
        [ComplianceFilingId]    INT NOT NULL,
        [ContributionTotal]     DECIMAL(18,2) NOT NULL,
        [UpdatedAtUtc]          DATETIME NOT NULL,
        [UpdatedByUserId]       NVARCHAR(450) NOT NULL,
        [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_PayslipPeriodComplianceFiling] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PayslipPeriodCF_Period] FOREIGN KEY ([PayslipPeriodId])
            REFERENCES [payroll].[PayslipPeriod]([Id]),
        CONSTRAINT [FK_PayslipPeriodCF_Filing] FOREIGN KEY ([ComplianceFilingId])
            REFERENCES [compliance].[BusinessApplication]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_PayslipPeriodCF_Period]
        ON [payroll].[PayslipPeriodComplianceFiling] ([PayslipPeriodId])
        INCLUDE ([ComplianceFilingId], [ContributionTotal], [UpdatedAtUtc]);
END
GO
