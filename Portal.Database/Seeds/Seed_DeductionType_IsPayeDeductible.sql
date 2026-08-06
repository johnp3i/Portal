-- ============================================================
-- Phase D: Add IsPayeDeductible column to DeductionType table
-- Marks deductions that reduce the PAYE taxable base.
-- Replaces hard-coded Code matching for PAYE base calculation.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[payroll].[DeductionType]') AND name = 'IsPayeDeductible'
)
BEGIN
    ALTER TABLE [payroll].[DeductionType]
        ADD [IsPayeDeductible] BIT NOT NULL DEFAULT 0;
END
GO

-- Flag existing SI and GESY employee deductions as PAYE-deductible
UPDATE [payroll].[DeductionType]
SET [IsPayeDeductible] = 1
WHERE [Code] IN ('SI_Deduction', 'GESY_Deduction')
  AND [DeductionCategoryTypeId] = 1;
GO
