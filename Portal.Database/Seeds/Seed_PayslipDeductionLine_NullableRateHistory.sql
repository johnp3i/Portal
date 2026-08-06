-- ============================================================
-- Phase D: Make DeductionRateHistoryId nullable on PayslipDeductionLine
-- PAYE lines use NULL since PAYE uses progressive bands, not rate history.
-- ============================================================

USE [Portal]
GO

-- Check if the column is currently NOT NULL before altering
IF EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[payroll].[PayslipDeductionLine]')
      AND name = 'DeductionRateHistoryId'
      AND is_nullable = 0
)
BEGIN
    -- Drop the existing FK constraint if it exists
    IF EXISTS (
        SELECT * FROM sys.foreign_keys
        WHERE name = 'FK_PayslipDeductionLine_DeductionRateHistory'
          AND parent_object_id = OBJECT_ID('[payroll].[PayslipDeductionLine]')
    )
    BEGIN
        ALTER TABLE [payroll].[PayslipDeductionLine]
            DROP CONSTRAINT [FK_PayslipDeductionLine_DeductionRateHistory];
    END

    ALTER TABLE [payroll].[PayslipDeductionLine]
        ALTER COLUMN [DeductionRateHistoryId] INT NULL;

    -- Re-add the FK constraint (now allowing NULLs)
    ALTER TABLE [payroll].[PayslipDeductionLine]
        ADD CONSTRAINT [FK_PayslipDeductionLine_DeductionRateHistory]
            FOREIGN KEY ([DeductionRateHistoryId])
            REFERENCES [payroll].[DeductionRateHistory]([Id]);
END
GO
