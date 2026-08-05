-- ============================================================
-- Payroll Phase B — Seed PayslipStatusType + Create PayslipAuditActionType
-- ============================================================

USE [Portal]
GO

-- ============================================================
-- 1. Extend PayslipStatusType with new statuses (Unlocked, Re-finalised)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [payroll].[PayslipStatusType] WHERE [Id] = 4)
BEGIN
    INSERT INTO [payroll].[PayslipStatusType] ([Id], [Name]) VALUES (4, 'Unlocked')
END
GO

IF NOT EXISTS (SELECT 1 FROM [payroll].[PayslipStatusType] WHERE [Id] = 5)
BEGIN
    INSERT INTO [payroll].[PayslipStatusType] ([Id], [Name]) VALUES (5, 'Re-finalised')
END
GO

-- ============================================================
-- 2. Create PayslipAuditActionType lookup table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipAuditActionType' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[PayslipAuditActionType] (
        [Id]    TINYINT NOT NULL,
        [Name]  NVARCHAR(20) NOT NULL,
        CONSTRAINT [PK_PayslipAuditActionType] PRIMARY KEY CLUSTERED ([Id])
    )
END
GO

-- ============================================================
-- 3. Seed PayslipAuditActionType values
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [payroll].[PayslipAuditActionType] WHERE [Id] = 1)
BEGIN
    INSERT INTO [payroll].[PayslipAuditActionType] ([Id], [Name]) VALUES (1, 'Unlocked')
END
GO

IF NOT EXISTS (SELECT 1 FROM [payroll].[PayslipAuditActionType] WHERE [Id] = 2)
BEGIN
    INSERT INTO [payroll].[PayslipAuditActionType] ([Id], [Name]) VALUES (2, 'Edited')
END
GO

IF NOT EXISTS (SELECT 1 FROM [payroll].[PayslipAuditActionType] WHERE [Id] = 3)
BEGIN
    INSERT INTO [payroll].[PayslipAuditActionType] ([Id], [Name]) VALUES (3, 'Re-finalised')
END
GO
