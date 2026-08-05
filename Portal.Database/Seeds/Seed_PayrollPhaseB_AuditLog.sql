-- ============================================================
-- Payroll Phase B — Create PayslipAuditLog table
-- ============================================================

USE [Portal]
GO

-- ============================================================
-- 1. Create PayslipAuditLog table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipAuditLog' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[PayslipAuditLog] (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [PayslipId]                 INT NOT NULL,
        [UserId]                    NVARCHAR(450) NOT NULL,
        [PayslipAuditActionTypeId]  TINYINT NOT NULL,
        [FieldName]                 NVARCHAR(100) NULL,
        [OldValue]                  NVARCHAR(500) NULL,
        [NewValue]                  NVARCHAR(500) NULL,
        [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_PayslipAuditLog] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PayslipAuditLog_Payslip] FOREIGN KEY ([PayslipId])
            REFERENCES [payroll].[Payslip]([Id]),
        CONSTRAINT [FK_PayslipAuditLog_ActionType] FOREIGN KEY ([PayslipAuditActionTypeId])
            REFERENCES [payroll].[PayslipAuditActionType]([Id])
    )
END
GO

-- ============================================================
-- 2. Performance index for audit history queries (per payslip)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PayslipAuditLog_PayslipId' AND object_id = OBJECT_ID('[payroll].[PayslipAuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PayslipAuditLog_PayslipId]
        ON [payroll].[PayslipAuditLog] ([PayslipId]) INCLUDE ([CreatedAtUtc], [PayslipAuditActionTypeId])
END
GO

-- ============================================================
-- 3. Index for period-level audit summary (sorted by time)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PayslipAuditLog_CreatedAtUtc' AND object_id = OBJECT_ID('[payroll].[PayslipAuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PayslipAuditLog_CreatedAtUtc]
        ON [payroll].[PayslipAuditLog] ([CreatedAtUtc] DESC) INCLUDE ([PayslipId], [UserId])
END
GO
