-- ============================================================
-- Phase D: Add IsPayeApplicable column to Employee table
-- Flags whether PAYE income tax applies to this employee.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[payroll].[Employee]') AND name = 'IsPayeApplicable'
)
BEGIN
    ALTER TABLE [payroll].[Employee]
        ADD [IsPayeApplicable] BIT NOT NULL DEFAULT 0;
END
GO
