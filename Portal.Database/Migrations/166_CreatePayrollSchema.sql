-- ============================================================
-- Migration 166: Payroll Phase A — Schema, Tables, Indexes, Seed Data
-- ============================================================

USE [Portal]
GO

-- Create the payroll schema
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'payroll')
    EXEC('CREATE SCHEMA [payroll]')
GO

-- ============================================================
-- 0a. PayslipStatusType (Lookup)
-- ============================================================
CREATE TABLE [payroll].[PayslipStatusType] (
    [Id]    TINYINT NOT NULL,
    [Name]  NVARCHAR(20) NOT NULL,
    CONSTRAINT [PK_PayslipStatusType] PRIMARY KEY CLUSTERED ([Id])
)
GO

INSERT INTO [payroll].[PayslipStatusType] ([Id], [Name]) VALUES
    (1, 'Draft'),
    (2, 'Preview'),
    (3, 'Finalised')
GO

-- ============================================================
-- 0b. DeductionCategoryType (Lookup)
-- ============================================================
CREATE TABLE [payroll].[DeductionCategoryType] (
    [Id]    TINYINT NOT NULL,
    [Name]  NVARCHAR(20) NOT NULL,
    CONSTRAINT [PK_DeductionCategoryType] PRIMARY KEY CLUSTERED ([Id])
)
GO

INSERT INTO [payroll].[DeductionCategoryType] ([Id], [Name]) VALUES
    (1, 'Deduction'),
    (2, 'Contribution')
GO

-- ============================================================
-- 0c. SalaryType (Lookup)
-- ============================================================
CREATE TABLE [payroll].[SalaryType] (
    [Id]    TINYINT NOT NULL,
    [Name]  NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_SalaryType] PRIMARY KEY CLUSTERED ([Id])
)
GO

INSERT INTO [payroll].[SalaryType] ([Id], [Name]) VALUES
    (1, 'Full-time'),
    (2, 'Part-time'),
    (3, 'Hourly')
GO

-- ============================================================
-- 1. Department
-- ============================================================
CREATE TABLE [payroll].[Department] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [BusinessId]    INT NOT NULL,
    [Name]          NVARCHAR(200) NOT NULL,
    [IsActive]      BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]  DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Department] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Department_BusinessId_Name] UNIQUE ([BusinessId], [Name])
)
GO

-- ============================================================
-- 2. Employee
-- ============================================================
CREATE TABLE [payroll].[Employee] (
    [Id]                      INT IDENTITY(1,1) NOT NULL,
    [BusinessId]              INT NOT NULL,
    [DepartmentId]            INT NULL,
    [Name]                    NVARCHAR(300) NOT NULL,
    [Position]                NVARCHAR(200) NULL,
    [SocialInsuranceNumber]   NVARCHAR(50) NOT NULL,
    [IdNumber]                NVARCHAR(50) NOT NULL,
    [Phone]                   NVARCHAR(50) NULL,
    [Email]                   NVARCHAR(200) NULL,
    [StartDate]               DATE NOT NULL,
    [EndDate]                 DATE NULL,
    [SalaryTypeId]            TINYINT NOT NULL,
    [BaseSalary]              DECIMAL(18,2) NOT NULL,
    [HourlyRate]              DECIMAL(18,2) NULL,
    [BankAccount]             NVARCHAR(100) NULL,
    [IsActive]                BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]            DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Employee] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Employee_Department] FOREIGN KEY ([DepartmentId])
        REFERENCES [payroll].[Department]([Id]),
    CONSTRAINT [FK_Employee_SalaryType] FOREIGN KEY ([SalaryTypeId])
        REFERENCES [payroll].[SalaryType]([Id]),
    CONSTRAINT [UQ_Employee_BusinessId_SIN] UNIQUE ([BusinessId], [SocialInsuranceNumber]),
    CONSTRAINT [UQ_Employee_BusinessId_IdNumber] UNIQUE ([BusinessId], [IdNumber])
)
GO

-- ============================================================
-- 3. EarningType
-- ============================================================
CREATE TABLE [payroll].[EarningType] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [Name]          NVARCHAR(100) NOT NULL,
    [Code]          NVARCHAR(50) NOT NULL,
    [IsActive]      BIT NOT NULL DEFAULT 1,
    [SortOrder]     INT NOT NULL DEFAULT 0,
    [CreatedAtUtc]  DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_EarningType] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_EarningType_Code] UNIQUE ([Code])
)
GO

-- ============================================================
-- 4. DeductionType
-- ============================================================
CREATE TABLE [payroll].[DeductionType] (
    [Id]                      INT IDENTITY(1,1) NOT NULL,
    [Name]                    NVARCHAR(200) NOT NULL,
    [Code]                    NVARCHAR(50) NOT NULL,
    [IsPercentage]            BIT NOT NULL DEFAULT 1,
    [DeductionCategoryTypeId] TINYINT NOT NULL,
    [IsActive]                BIT NOT NULL DEFAULT 1,
    [BusinessId]              INT NULL,
    [Country]                 NVARCHAR(50) NOT NULL DEFAULT 'CY',
    [IsTemplate]              BIT NOT NULL DEFAULT 0,
    [CreatedAtUtc]            DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_DeductionType] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_DeductionType_BusinessId_Code] UNIQUE ([BusinessId], [Code]),
    CONSTRAINT [FK_DeductionType_CategoryType] FOREIGN KEY ([DeductionCategoryTypeId])
        REFERENCES [payroll].[DeductionCategoryType]([Id])
)
GO

-- ============================================================
-- 5. DeductionRateHistory
-- ============================================================
CREATE TABLE [payroll].[DeductionRateHistory] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [DeductionTypeId]   INT NOT NULL,
    [Rate]              DECIMAL(6,2) NOT NULL,
    [EffectiveFromUtc]  DATETIME NOT NULL,
    [EffectiveToUtc]    DATETIME NULL,
    [CreatedAtUtc]      DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_DeductionRateHistory] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_DeductionRateHistory_DeductionType] FOREIGN KEY ([DeductionTypeId])
        REFERENCES [payroll].[DeductionType]([Id])
)
GO

-- ============================================================
-- 6. EmployeeDefaultEarnings
-- ============================================================
CREATE TABLE [payroll].[EmployeeDefaultEarnings] (
    [Id]                  INT IDENTITY(1,1) NOT NULL,
    [EmployeeId]          INT NOT NULL,
    [EarningTypeId]       INT NOT NULL,
    [Description]         NVARCHAR(300) NULL,
    [Amount]              DECIMAL(18,2) NULL,
    [OvertimeMultiplier]  DECIMAL(4,2) NULL,
    [OvertimeHours]       DECIMAL(6,2) NULL,
    [CreatedAtUtc]        DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_EmployeeDefaultEarnings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_EmployeeDefaultEarnings_Employee] FOREIGN KEY ([EmployeeId])
        REFERENCES [payroll].[Employee]([Id]),
    CONSTRAINT [FK_EmployeeDefaultEarnings_EarningType] FOREIGN KEY ([EarningTypeId])
        REFERENCES [payroll].[EarningType]([Id])
)
GO

-- ============================================================
-- 7. PayslipPeriod
-- ============================================================
CREATE TABLE [payroll].[PayslipPeriod] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [BusinessId]            INT NOT NULL,
    [Year]                  INT NOT NULL,
    [Month]                 INT NOT NULL,
    [PayslipStatusTypeId]   TINYINT NOT NULL DEFAULT 1,
    [ProcessedAtUtc]        DATETIME NULL,
    [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PayslipPeriod] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_PayslipPeriod_Business_YearMonth] UNIQUE ([BusinessId], [Year], [Month]),
    CONSTRAINT [FK_PayslipPeriod_StatusType] FOREIGN KEY ([PayslipStatusTypeId])
        REFERENCES [payroll].[PayslipStatusType]([Id])
)
GO

-- ============================================================
-- 8. Payslip
-- ============================================================
CREATE TABLE [payroll].[Payslip] (
    [Id]                          INT IDENTITY(1,1) NOT NULL,
    [EmployeeId]                  INT NOT NULL,
    [PayslipPeriodId]             INT NOT NULL,
    [TotalEarnings]               DECIMAL(18,2) NOT NULL DEFAULT 0,
    [TotalEmployeeDeductions]     DECIMAL(18,2) NOT NULL DEFAULT 0,
    [NetSalary]                   DECIMAL(18,2) NOT NULL DEFAULT 0,
    [TotalEmployerContributions]  DECIMAL(18,2) NOT NULL DEFAULT 0,
    [ManagerNotes]                NVARCHAR(2000) NULL,
    [PayslipStatusTypeId]         TINYINT NOT NULL DEFAULT 1,
    [CreatedAtUtc]                DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Payslip] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Payslip_Employee] FOREIGN KEY ([EmployeeId])
        REFERENCES [payroll].[Employee]([Id]),
    CONSTRAINT [FK_Payslip_PayslipPeriod] FOREIGN KEY ([PayslipPeriodId])
        REFERENCES [payroll].[PayslipPeriod]([Id]),
    CONSTRAINT [FK_Payslip_StatusType] FOREIGN KEY ([PayslipStatusTypeId])
        REFERENCES [payroll].[PayslipStatusType]([Id])
)
GO

-- ============================================================
-- 9. PayslipEarningLine
-- ============================================================
CREATE TABLE [payroll].[PayslipEarningLine] (
    [Id]                  INT IDENTITY(1,1) NOT NULL,
    [PayslipId]           INT NOT NULL,
    [EarningTypeId]       INT NOT NULL,
    [Description]         NVARCHAR(300) NULL,
    [Amount]              DECIMAL(18,2) NOT NULL,
    [OvertimeMultiplier]  DECIMAL(4,2) NULL,
    [OvertimeHours]       DECIMAL(6,2) NULL,
    [CreatedAtUtc]        DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PayslipEarningLine] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PayslipEarningLine_Payslip] FOREIGN KEY ([PayslipId])
        REFERENCES [payroll].[Payslip]([Id]),
    CONSTRAINT [FK_PayslipEarningLine_EarningType] FOREIGN KEY ([EarningTypeId])
        REFERENCES [payroll].[EarningType]([Id]),
    CONSTRAINT [CK_PayslipEarningLine_Multiplier] CHECK (
        [OvertimeMultiplier] IS NULL OR ([OvertimeMultiplier] >= 1.0 AND [OvertimeMultiplier] <= 4.0)
    )
)
GO

-- ============================================================
-- 10. PayslipDeductionLine
-- ============================================================
CREATE TABLE [payroll].[PayslipDeductionLine] (
    [Id]                      INT IDENTITY(1,1) NOT NULL,
    [PayslipId]               INT NOT NULL,
    [DeductionTypeId]         INT NOT NULL,
    [BaseAmount]              DECIMAL(18,2) NOT NULL,
    [Rate]                    DECIMAL(6,2) NOT NULL,
    [CalculatedAmount]        DECIMAL(18,2) NOT NULL,
    [DeductionCategoryTypeId] TINYINT NOT NULL,
    [DeductionRateHistoryId]  INT NOT NULL,
    [CreatedAtUtc]            DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PayslipDeductionLine] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PayslipDeductionLine_Payslip] FOREIGN KEY ([PayslipId])
        REFERENCES [payroll].[Payslip]([Id]),
    CONSTRAINT [FK_PayslipDeductionLine_DeductionType] FOREIGN KEY ([DeductionTypeId])
        REFERENCES [payroll].[DeductionType]([Id]),
    CONSTRAINT [FK_PayslipDeductionLine_RateHistory] FOREIGN KEY ([DeductionRateHistoryId])
        REFERENCES [payroll].[DeductionRateHistory]([Id]),
    CONSTRAINT [FK_PayslipDeductionLine_CategoryType] FOREIGN KEY ([DeductionCategoryTypeId])
        REFERENCES [payroll].[DeductionCategoryType]([Id])
)
GO

-- ============================================================
-- 11. PayslipEmailLog
-- ============================================================
CREATE TABLE [payroll].[PayslipEmailLog] (
    [Id]                  INT IDENTITY(1,1) NOT NULL,
    [PayslipId]           INT NOT NULL,
    [SentByUserId]        NVARCHAR(450) NOT NULL,
    [SentToEmail]         NVARCHAR(200) NOT NULL,
    [IsSignatureIncluded] BIT NOT NULL DEFAULT 0,
    [SentAtUtc]           DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [CreatedAtUtc]        DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PayslipEmailLog] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PayslipEmailLog_Payslip] FOREIGN KEY ([PayslipId])
        REFERENCES [payroll].[Payslip]([Id])
)
GO

-- ============================================================
-- Performance Indexes
-- ============================================================
CREATE NONCLUSTERED INDEX [IX_Employee_BusinessId_IsActive]
    ON [payroll].[Employee] ([BusinessId], [IsActive]) INCLUDE ([Name], [DepartmentId])
GO

CREATE NONCLUSTERED INDEX [IX_Payslip_PayslipPeriodId]
    ON [payroll].[Payslip] ([PayslipPeriodId]) INCLUDE ([EmployeeId], [NetSalary])
GO

CREATE NONCLUSTERED INDEX [IX_PayslipPeriod_BusinessId_Status]
    ON [payroll].[PayslipPeriod] ([BusinessId], [PayslipStatusTypeId]) INCLUDE ([Year], [Month])
GO

CREATE NONCLUSTERED INDEX [IX_DeductionRateHistory_Lookup]
    ON [payroll].[DeductionRateHistory] ([DeductionTypeId], [EffectiveFromUtc]) INCLUDE ([EffectiveToUtc], [Rate])
GO

CREATE NONCLUSTERED INDEX [IX_PayslipEarningLine_PayslipId]
    ON [payroll].[PayslipEarningLine] ([PayslipId])
GO

CREATE NONCLUSTERED INDEX [IX_PayslipDeductionLine_PayslipId]
    ON [payroll].[PayslipDeductionLine] ([PayslipId])
GO

CREATE NONCLUSTERED INDEX [IX_DeductionType_BusinessId]
    ON [payroll].[DeductionType] ([BusinessId], [IsActive]) INCLUDE ([Code], [Name])
GO

-- ============================================================
-- Seed Data: Earning Types
-- ============================================================
INSERT INTO [payroll].[EarningType] ([Name], [Code], [IsActive], [SortOrder])
VALUES
    ('Basic Salary', 'Basic', 1, 1),
    ('Overtime', 'Overtime', 1, 2),
    ('Bonus', 'Bonus', 1, 3),
    ('Paid Holidays', 'PaidHolidays', 1, 4),
    ('Part-time', 'PartTime', 1, 5)
GO

-- ============================================================
-- Seed Data: Cyprus Deduction Type Templates with Rate History
-- DeductionCategoryTypeId: 1 = Deduction (from employee), 2 = Contribution (by employer)
-- ============================================================
INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [Country], [BusinessId], [IsTemplate])
VALUES
    -- Employee Deductions (taken from salary)
    ('Social Insurance', 'SI_Deduction', 1, 1, 1, 'CY', NULL, 1),
    ('GESY', 'GESY_Deduction', 1, 1, 1, 'CY', NULL, 1),
    -- Employer Contributions (paid by business)
    ('Social Insurance', 'SI_Contribution', 1, 2, 1, 'CY', NULL, 1),
    ('Redundancy Fund', 'Redundancy', 1, 2, 1, 'CY', NULL, 1),
    ('Industrial Training Fund', 'IndustrialTraining', 1, 2, 1, 'CY', NULL, 1),
    ('Social Cohesion Fund', 'SocialCohesion', 1, 2, 1, 'CY', NULL, 1),
    ('GESY', 'GESY_Contribution', 1, 2, 1, 'CY', NULL, 1)
GO

-- Rate history (current rates — EffectiveToUtc = NULL, effective from 1 Jan 2024)
INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc])
SELECT [payroll].[DeductionType].[Id], 8.80, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'SI_Deduction'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 2.65, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'GESY_Deduction'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 8.80, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'SI_Contribution'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 1.20, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'Redundancy'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 0.50, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'IndustrialTraining'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 2.00, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'SocialCohesion'
UNION ALL
SELECT [payroll].[DeductionType].[Id], 2.90, '2024-01-01', NULL
FROM [payroll].[DeductionType] WHERE [payroll].[DeductionType].[Code] = 'GESY_Contribution'
GO
