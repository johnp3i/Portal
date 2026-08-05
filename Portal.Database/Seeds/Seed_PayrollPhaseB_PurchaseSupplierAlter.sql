-- ============================================================
-- Payroll Phase B — ALTER Purchase and Supplier tables for P&L integration
-- ============================================================

USE [Portal]
GO

-- ============================================================
-- 1. Add PayslipPeriodId to Purchase (links payroll-generated expenses to payroll periods)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[purchase].[Purchase]') AND name = 'PayslipPeriodId')
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [PayslipPeriodId] INT NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Purchase_PayslipPeriod')
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD CONSTRAINT [FK_Purchase_PayslipPeriod] FOREIGN KEY ([PayslipPeriodId])
            REFERENCES [payroll].[PayslipPeriod]([Id])
END
GO

-- Filtered index for finding payroll-generated purchases by period
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Purchase_PayslipPeriodId' AND object_id = OBJECT_ID('[purchase].[Purchase]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Purchase_PayslipPeriodId]
        ON [purchase].[Purchase] ([PayslipPeriodId]) WHERE [PayslipPeriodId] IS NOT NULL
END
GO

-- ============================================================
-- 2. Add CancelledByUserId to Purchase (tracks who cancelled for P&L audit trail)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[purchase].[Purchase]') AND name = 'CancelledByUserId')
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [CancelledByUserId] NVARCHAR(450) NULL
END
GO

-- ============================================================
-- 3. Add IsSystemGenerated to Supplier (protects payroll internal supplier)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[purchase].[Supplier]') AND name = 'IsSystemGenerated')
BEGIN
    ALTER TABLE [purchase].[Supplier]
        ADD [IsSystemGenerated] BIT NOT NULL DEFAULT 0
END
GO
