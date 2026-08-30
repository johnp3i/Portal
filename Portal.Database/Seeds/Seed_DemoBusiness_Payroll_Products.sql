-- ============================================================
-- DEMO BUSINESS SEED: Le Paris Roasting — Payroll & Products
-- ============================================================
-- Purpose: Seeds payroll and product catalog data for the demo
--          business (BusinessId=1000).
--
-- Approach: Uses natural auto-generated IDs (no IDENTITY_INSERT)
--           to avoid identity sequence gaps. Child rows reference
--           parent IDs via variables captured with SCOPE_IDENTITY()
--           or OUTPUT INSERTED.Id.
--
-- Idempotency: Checks for existing data before inserting.
--              Guard: if ANY demo employee already exists for
--              BusinessId=1000, the entire script is skipped.
--
-- Prerequisites:
--   - Seed_DemoBusiness_LeParisRoasting.sql already run
--   - All migrations up to 180 applied
--   - Cyprus deduction templates seeded (migration 166)
--
-- Date: 27 August 2026
-- ============================================================

USE [Portal];
GO

-- Guard: skip everything if payroll data already exists for demo business
IF EXISTS (SELECT 1 FROM [payroll].[Employee] WHERE [BusinessId] = 1000)
BEGIN
    PRINT 'Payroll seed data already exists for BusinessId=1000. Skipping.';
    RETURN;
END
GO

-- Guard: skip products if they already exist
IF NOT EXISTS (SELECT 1 FROM [product].[Product] WHERE [BusinessId] = 1000)
BEGIN
    -- ============================================================
    -- SECTION 1: PRODUCTS (12 items)
    -- ============================================================
    INSERT INTO [product].[Product]
        ([BusinessId], [ProductCode], [Description], [DefaultSellingPrice], [DefaultCostPrice], [DefaultVatRate], [SupplierId], [IsActive], [ProductTypeId], [CreatedAtUtc])
    VALUES
        (1000, N'BEAN-ETH-250',   N'Ethiopian Yirgacheffe — 250g bag',                   8.50,    3.20,  19.00, 1000, 1, 2, '2026-01-15T08:00:00'),
        (1000, N'BEAN-COL-250',   N'Colombian Supremo — 250g bag',                       7.90,    2.80,  19.00, 1000, 1, 2, '2026-01-15T08:00:00'),
        (1000, N'BEAN-BRA-1KG',   N'Brazilian Santos — 1kg bag',                        22.00,    9.50,  19.00, 1000, 1, 2, '2026-01-15T08:00:00'),
        (1000, N'BEAN-HOUSE-1KG', N'Le Paris House Blend — 1kg bag',                    18.50,    7.00,  19.00, NULL, 1, 2, '2026-01-15T08:00:00'),
        (1000, N'BEAN-DECAF-250', N'Swiss Water Decaf — 250g bag',                       9.20,    3.80,  19.00, 1001, 1, 2, '2026-02-01T08:00:00'),
        (1000, N'EQUIP-GRINDER',  N'Commercial Burr Grinder — Mazzer Mini',            680.00,  420.00,  19.00, 1002, 1, 2, '2026-02-01T08:00:00'),
        (1000, N'EQUIP-TAMPER',   N'Calibrated Tamper 58mm',                            45.00,   18.00,  19.00, 1002, 1, 2, '2026-02-01T08:00:00'),
        (1000, N'EQUIP-SCALE',    N'Precision Brewing Scale 0.1g',                      38.00,   15.00,  19.00, 1002, 1, 2, '2026-02-01T08:00:00'),
        (1000, N'SVC-TRAINING',   N'Barista Training Session (half day)',               250.00,    0.00,  19.00, NULL, 1, 1, '2026-03-01T08:00:00'),
        (1000, N'SVC-MAINT-Q',    N'Quarterly Equipment Maintenance Contract',          180.00,   60.00,  19.00, NULL, 1, 1, '2026-03-01T08:00:00'),
        (1000, N'SVC-CUPPING',    N'Coffee Cupping & Tasting Event (group of 10)',      350.00,   80.00,  19.00, NULL, 1, 1, '2026-03-15T08:00:00'),
        (1000, N'PACK-STARTER',   N'Café Starter Pack (grinder + 3kg beans + training)', 950.00, 480.00, 19.00, NULL, 1, 2, '2026-04-01T08:00:00');

    PRINT 'Inserted 12 products for BusinessId=1000.';
END
ELSE
BEGIN
    PRINT 'Products already exist for BusinessId=1000. Skipping.';
END
GO

-- ============================================================
-- SECTION 2: DEPARTMENTS (3)
-- ============================================================

DECLARE @DeptRoasting INT, @DeptRetail INT, @DeptAdmin INT;

INSERT INTO [payroll].[Department] ([BusinessId], [Name], [IsActive], [CreatedAtUtc])
VALUES (1000, N'Roasting & Production', 1, '2026-01-01T08:00:00');
SET @DeptRoasting = SCOPE_IDENTITY();

INSERT INTO [payroll].[Department] ([BusinessId], [Name], [IsActive], [CreatedAtUtc])
VALUES (1000, N'Retail & Café', 1, '2026-01-01T08:00:00');
SET @DeptRetail = SCOPE_IDENTITY();

INSERT INTO [payroll].[Department] ([BusinessId], [Name], [IsActive], [CreatedAtUtc])
VALUES (1000, N'Sales & Admin', 1, '2026-01-01T08:00:00');
SET @DeptAdmin = SCOPE_IDENTITY();

PRINT 'Inserted 3 departments.';

-- ============================================================
-- SECTION 3: EMPLOYEES (6)
-- ============================================================

DECLARE @EmpGiorgos INT, @EmpAnna INT, @EmpMaria INT, @EmpStavros INT, @EmpElena INT, @EmpMarie INT;

-- Giorgos — Head Roaster (Full-time, PAYE applicable)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptRoasting, N'Giorgos Pavlou', N'Head Roaster',
     N'SI-LP-001', N'ID-LP-001', N'+357 91 700001', N'giorgos@leparisroasting.com',
     '2024-03-01', NULL, 1, 2200.00, NULL,
     N'CY12 0020 0128 0000 0012 3456 7890', 1, 1, '2026-01-01T08:00:00');
SET @EmpGiorgos = SCOPE_IDENTITY();

-- Anna — Roasting Assistant (Full-time)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptRoasting, N'Anna Christofi', N'Roasting Assistant',
     N'SI-LP-002', N'ID-LP-002', N'+357 91 700002', N'anna@leparisroasting.com',
     '2025-01-15', NULL, 1, 1600.00, NULL,
     N'CY34 0020 0128 0000 0098 7654 3210', 1, 0, '2026-01-01T08:00:00');
SET @EmpAnna = SCOPE_IDENTITY();

-- Maria — Café Manager (Full-time, PAYE applicable)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptRetail, N'Maria Kyriakidou', N'Café Manager',
     N'SI-LP-003', N'ID-LP-003', N'+357 91 700003', N'maria@leparisroasting.com',
     '2024-06-01', NULL, 1, 1900.00, NULL,
     N'CY56 0020 0128 0000 0055 5555 5555', 1, 1, '2026-01-01T08:00:00');
SET @EmpMaria = SCOPE_IDENTITY();

-- Stavros — Barista (Part-time)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptRetail, N'Stavros Demetriou', N'Barista',
     N'SI-LP-004', N'ID-LP-004', N'+357 91 700004', N'stavros@leparisroasting.com',
     '2025-06-01', NULL, 2, 950.00, NULL,
     N'CY78 0020 0128 0000 0066 6666 6666', 1, 0, '2026-01-01T08:00:00');
SET @EmpStavros = SCOPE_IDENTITY();

-- Elena — Weekend Barista (Hourly)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptRetail, N'Elena Vasiliou', N'Barista (Weekend)',
     N'SI-LP-005', N'ID-LP-005', N'+357 91 700005', N'elena.v@leparisroasting.com',
     '2026-02-01', NULL, 3, 0.00, 12.50,
     N'CY90 0020 0128 0000 0077 7777 7777', 1, 0, '2026-02-01T08:00:00');
SET @EmpElena = SCOPE_IDENTITY();

-- Marie — Managing Director (Full-time, PAYE applicable)
INSERT INTO [payroll].[Employee]
    ([BusinessId], [DepartmentId], [Name], [Position], [SocialInsuranceNumber], [IdNumber],
     [Phone], [Email], [StartDate], [EndDate], [SalaryTypeId], [BaseSalary], [HourlyRate],
     [BankAccount], [IsActive], [IsPayeApplicable], [CreatedAtUtc])
VALUES
    (1000, @DeptAdmin, N'Marie Dupont', N'Managing Director',
     N'SI-LP-006', N'ID-LP-006', N'+357 91 700006', N'demo@leparis.com',
     '2024-01-01', NULL, 1, 3500.00, NULL,
     N'CY11 0020 0128 0000 0088 8888 8888', 1, 1, '2026-01-01T08:00:00');
SET @EmpMarie = SCOPE_IDENTITY();

PRINT 'Inserted 6 employees.';

-- ============================================================
-- SECTION 4: EMPLOYEE DEFAULT EARNINGS
-- ============================================================

INSERT INTO [payroll].[EmployeeDefaultEarnings]
    ([EmployeeId], [EarningTypeId], [Description], [Amount], [OvertimeMultiplier], [OvertimeHours], [CreatedAtUtc])
VALUES
    (@EmpGiorgos, 1, N'Monthly Basic Salary',        2200.00, NULL, NULL, '2026-01-01T08:00:00'),
    (@EmpGiorgos, 2, N'Weekend roasting overtime',    NULL,    1.50, 8.00, '2026-01-01T08:00:00'),
    (@EmpAnna,    1, N'Monthly Basic Salary',         1600.00, NULL, NULL, '2026-01-01T08:00:00'),
    (@EmpMaria,   1, N'Monthly Basic Salary',         1900.00, NULL, NULL, '2026-01-01T08:00:00'),
    (@EmpStavros, 5, N'Part-time shift (mornings)',    950.00, NULL, NULL, '2026-01-01T08:00:00'),
    (@EmpMarie,   1, N'Monthly Basic Salary',         3500.00, NULL, NULL, '2026-01-01T08:00:00');

PRINT 'Inserted employee default earnings.';

-- ============================================================
-- SECTION 5: BUSINESS-SPECIFIC DEDUCTION TYPES
-- ============================================================

DECLARE @DT_SI_Ded INT, @DT_GESY_Ded INT;
DECLARE @DT_SI_Con INT, @DT_Redundancy INT, @DT_IndTraining INT, @DT_SocCohesion INT, @DT_GESY_Con INT;

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'Social Insurance', N'SI_Deduction', 1, 1, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_SI_Ded = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'GESY', N'GESY_Deduction', 1, 1, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_GESY_Ded = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'Social Insurance', N'SI_Contribution', 1, 2, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_SI_Con = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'Redundancy Fund', N'Redundancy', 1, 2, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_Redundancy = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'Industrial Training Fund', N'IndustrialTraining', 1, 2, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_IndTraining = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'Social Cohesion Fund', N'SocialCohesion', 1, 2, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_SocCohesion = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionType] ([Name], [Code], [IsPercentage], [DeductionCategoryTypeId], [IsActive], [BusinessId], [Country], [IsTemplate], [IsPayeDeductible], [CreatedAtUtc])
VALUES (N'GESY', N'GESY_Contribution', 1, 2, 1, 1000, N'CY', 0, 0, '2026-01-01T08:00:00');
SET @DT_GESY_Con = SCOPE_IDENTITY();

PRINT 'Inserted 7 business deduction types.';

-- ============================================================
-- SECTION 6: DEDUCTION RATE HISTORY
-- ============================================================

DECLARE @RH_SI_Ded INT, @RH_GESY_Ded INT;
DECLARE @RH_SI_Con INT, @RH_Redundancy INT, @RH_IndTraining INT, @RH_SocCohesion INT, @RH_GESY_Con INT;

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_SI_Ded, 8.80, '2024-01-01', NULL);
SET @RH_SI_Ded = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_GESY_Ded, 2.65, '2024-01-01', NULL);
SET @RH_GESY_Ded = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_SI_Con, 8.80, '2024-01-01', NULL);
SET @RH_SI_Con = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_Redundancy, 1.20, '2024-01-01', NULL);
SET @RH_Redundancy = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_IndTraining, 0.50, '2024-01-01', NULL);
SET @RH_IndTraining = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_SocCohesion, 2.00, '2024-01-01', NULL);
SET @RH_SocCohesion = SCOPE_IDENTITY();

INSERT INTO [payroll].[DeductionRateHistory] ([DeductionTypeId], [Rate], [EffectiveFromUtc], [EffectiveToUtc]) VALUES (@DT_GESY_Con, 2.90, '2024-01-01', NULL);
SET @RH_GESY_Con = SCOPE_IDENTITY();

PRINT 'Inserted deduction rate history.';

-- ============================================================
-- SECTION 7: PAYSLIP PERIODS (3 months)
-- ============================================================

DECLARE @PeriodMay INT, @PeriodJun INT, @PeriodJul INT;

INSERT INTO [payroll].[PayslipPeriod] ([BusinessId], [Year], [Month], [PayslipStatusTypeId], [ProcessedAtUtc], [CreatedAtUtc])
VALUES (1000, 2026, 5, 3, '2026-06-01T10:00:00', '2026-05-28T08:00:00');
SET @PeriodMay = SCOPE_IDENTITY();

INSERT INTO [payroll].[PayslipPeriod] ([BusinessId], [Year], [Month], [PayslipStatusTypeId], [ProcessedAtUtc], [CreatedAtUtc])
VALUES (1000, 2026, 6, 3, '2026-07-01T10:00:00', '2026-06-28T08:00:00');
SET @PeriodJun = SCOPE_IDENTITY();

INSERT INTO [payroll].[PayslipPeriod] ([BusinessId], [Year], [Month], [PayslipStatusTypeId], [ProcessedAtUtc], [CreatedAtUtc])
VALUES (1000, 2026, 7, 1, NULL, '2026-07-28T08:00:00');
SET @PeriodJul = SCOPE_IDENTITY();

PRINT 'Inserted 3 payslip periods (May finalised, Jun finalised, Jul draft).';

-- ============================================================
-- SECTION 8: PAYSLIPS (6 employees × 3 months = 18)
-- ============================================================
-- Using variables + OUTPUT to capture IDs for earning/deduction lines.
-- SI Employee=8.80%, GESY Employee=2.65% → total employee deductions=11.45%
-- Employer: SI 8.80 + Redundancy 1.20 + IndTraining 0.50 + SocCohesion 2.00 + GESY 2.90 = 15.40%

-- We only insert earning/deduction detail lines for May (as sample).
-- Jun/Jul payslips have summary totals only.

DECLARE @PS_Giorgos_May INT, @PS_Anna_May INT, @PS_Maria_May INT;
DECLARE @PS_Stavros_May INT, @PS_Elena_May INT, @PS_Marie_May INT;

-- May 2026 — Finalised
INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpGiorgos, @PeriodMay, 2365.00, 270.79, 2094.21, 364.21, NULL, 3, '2026-05-28T08:00:00');
SET @PS_Giorgos_May = SCOPE_IDENTITY();

INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpAnna, @PeriodMay, 1600.00, 183.20, 1416.80, 246.40, NULL, 3, '2026-05-28T08:00:00');
SET @PS_Anna_May = SCOPE_IDENTITY();

INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpMaria, @PeriodMay, 1900.00, 217.55, 1682.45, 292.60, NULL, 3, '2026-05-28T08:00:00');
SET @PS_Maria_May = SCOPE_IDENTITY();

INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpStavros, @PeriodMay, 950.00, 108.78, 841.22, 146.30, NULL, 3, '2026-05-28T08:00:00');
SET @PS_Stavros_May = SCOPE_IDENTITY();

INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpElena, @PeriodMay, 500.00, 57.25, 442.75, 77.00, N'40 hours @ €12.50', 3, '2026-05-28T08:00:00');
SET @PS_Elena_May = SCOPE_IDENTITY();

INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES (@EmpMarie, @PeriodMay, 3500.00, 400.75, 3099.25, 539.00, NULL, 3, '2026-05-28T08:00:00');
SET @PS_Marie_May = SCOPE_IDENTITY();

-- June 2026 — Finalised (summary only, no detail lines)
INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES
    (@EmpGiorgos, @PeriodJun, 2365.00, 270.79, 2094.21, 364.21, NULL, 3, '2026-06-28T08:00:00'),
    (@EmpAnna,    @PeriodJun, 1600.00, 183.20, 1416.80, 246.40, NULL, 3, '2026-06-28T08:00:00'),
    (@EmpMaria,   @PeriodJun, 1900.00, 217.55, 1682.45, 292.60, NULL, 3, '2026-06-28T08:00:00'),
    (@EmpStavros, @PeriodJun,  950.00, 108.78,  841.22, 146.30, NULL, 3, '2026-06-28T08:00:00'),
    (@EmpElena,   @PeriodJun,  625.00,  71.56,  553.44,  96.25, N'50 hours @ €12.50', 3, '2026-06-28T08:00:00'),
    (@EmpMarie,   @PeriodJun, 3500.00, 400.75, 3099.25, 539.00, NULL, 3, '2026-06-28T08:00:00');

-- July 2026 — Draft (summary only)
INSERT INTO [payroll].[Payslip] ([EmployeeId], [PayslipPeriodId], [TotalEarnings], [TotalEmployeeDeductions], [NetSalary], [TotalEmployerContributions], [ManagerNotes], [PayslipStatusTypeId], [CreatedAtUtc])
VALUES
    (@EmpGiorgos, @PeriodJul, 2365.00, 270.79, 2094.21, 364.21, NULL, 1, '2026-07-28T08:00:00'),
    (@EmpAnna,    @PeriodJul, 1600.00, 183.20, 1416.80, 246.40, NULL, 1, '2026-07-28T08:00:00'),
    (@EmpMaria,   @PeriodJul, 1900.00, 217.55, 1682.45, 292.60, NULL, 1, '2026-07-28T08:00:00'),
    (@EmpStavros, @PeriodJul,  950.00, 108.78,  841.22, 146.30, NULL, 1, '2026-07-28T08:00:00'),
    (@EmpElena,   @PeriodJul,  500.00,  57.25,  442.75,  77.00, N'40 hours @ €12.50', 1, '2026-07-28T08:00:00'),
    (@EmpMarie,   @PeriodJul, 3500.00, 400.75, 3099.25, 539.00, NULL, 1, '2026-07-28T08:00:00');

PRINT 'Inserted 18 payslips (6 employees × 3 months).';

-- ============================================================
-- SECTION 9: MAY EARNING LINES (detail for finalised month)
-- ============================================================

INSERT INTO [payroll].[PayslipEarningLine] ([PayslipId], [EarningTypeId], [Description], [Amount], [OvertimeMultiplier], [OvertimeHours], [CreatedAtUtc])
VALUES
    (@PS_Giorgos_May, 1, N'Monthly Basic Salary',        2200.00, NULL, NULL,  '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, 2, N'Weekend roasting overtime',     165.00, 1.50, 8.00, '2026-05-28T08:00:00'),
    (@PS_Anna_May,    1, N'Monthly Basic Salary',          1600.00, NULL, NULL, '2026-05-28T08:00:00'),
    (@PS_Maria_May,   1, N'Monthly Basic Salary',          1900.00, NULL, NULL, '2026-05-28T08:00:00'),
    (@PS_Stavros_May, 5, N'Part-time shift (mornings)',     950.00, NULL, NULL, '2026-05-28T08:00:00'),
    (@PS_Elena_May,   1, N'40 hours @ €12.50/hr',           500.00, NULL, NULL, '2026-05-28T08:00:00'),
    (@PS_Marie_May,   1, N'Monthly Basic Salary',          3500.00, NULL, NULL, '2026-05-28T08:00:00');

PRINT 'Inserted May earning lines.';

-- ============================================================
-- SECTION 10: MAY DEDUCTION LINES (detail for finalised month)
-- ============================================================
-- Employee deductions (SI 8.80%, GESY 2.65%) + Employer contributions
-- Sample: Giorgos (earnings=2365), Anna (1600), Marie (3500)

INSERT INTO [payroll].[PayslipDeductionLine]
    ([PayslipId], [DeductionTypeId], [BaseAmount], [Rate], [CalculatedAmount], [DeductionCategoryTypeId], [DeductionRateHistoryId], [CreatedAtUtc])
VALUES
    -- Giorgos (2365.00)
    (@PS_Giorgos_May, @DT_SI_Ded,      2365.00, 8.80, 208.12, 1, @RH_SI_Ded,      '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_GESY_Ded,    2365.00, 2.65,  62.67, 1, @RH_GESY_Ded,    '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_SI_Con,      2365.00, 8.80, 208.12, 2, @RH_SI_Con,      '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_Redundancy,  2365.00, 1.20,  28.38, 2, @RH_Redundancy,  '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_IndTraining, 2365.00, 0.50,  11.83, 2, @RH_IndTraining, '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_SocCohesion, 2365.00, 2.00,  47.30, 2, @RH_SocCohesion, '2026-05-28T08:00:00'),
    (@PS_Giorgos_May, @DT_GESY_Con,    2365.00, 2.90,  68.59, 2, @RH_GESY_Con,    '2026-05-28T08:00:00'),

    -- Anna (1600.00)
    (@PS_Anna_May, @DT_SI_Ded,      1600.00, 8.80, 140.80, 1, @RH_SI_Ded,      '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_GESY_Ded,    1600.00, 2.65,  42.40, 1, @RH_GESY_Ded,    '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_SI_Con,      1600.00, 8.80, 140.80, 2, @RH_SI_Con,      '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_Redundancy,  1600.00, 1.20,  19.20, 2, @RH_Redundancy,  '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_IndTraining, 1600.00, 0.50,   8.00, 2, @RH_IndTraining, '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_SocCohesion, 1600.00, 2.00,  32.00, 2, @RH_SocCohesion, '2026-05-28T08:00:00'),
    (@PS_Anna_May, @DT_GESY_Con,    1600.00, 2.90,  46.40, 2, @RH_GESY_Con,    '2026-05-28T08:00:00'),

    -- Marie (3500.00)
    (@PS_Marie_May, @DT_SI_Ded,      3500.00, 8.80, 308.00, 1, @RH_SI_Ded,      '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_GESY_Ded,    3500.00, 2.65,  92.75, 1, @RH_GESY_Ded,    '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_SI_Con,      3500.00, 8.80, 308.00, 2, @RH_SI_Con,      '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_Redundancy,  3500.00, 1.20,  42.00, 2, @RH_Redundancy,  '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_IndTraining, 3500.00, 0.50,  17.50, 2, @RH_IndTraining, '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_SocCohesion, 3500.00, 2.00,  70.00, 2, @RH_SocCohesion, '2026-05-28T08:00:00'),
    (@PS_Marie_May, @DT_GESY_Con,    3500.00, 2.90, 101.50, 2, @RH_GESY_Con,    '2026-05-28T08:00:00');

PRINT 'Inserted May deduction lines for Giorgos, Anna, and Marie.';

PRINT '======================================================';
PRINT 'Demo seed complete for BusinessId=1000:';
PRINT '  - 12 products (coffee beans, equipment, services)';
PRINT '  - 3 departments';
PRINT '  - 6 employees (full-time, part-time, hourly)';
PRINT '  - 7 business deduction types with rate history';
PRINT '  - 3 payslip periods (May finalised, Jun finalised, Jul draft)';
PRINT '  - 18 payslips with earning + deduction detail for May';
PRINT '======================================================';
GO
