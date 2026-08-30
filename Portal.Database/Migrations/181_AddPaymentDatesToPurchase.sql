-- ============================================================
-- Add SupplierDueDate and TargetPaymentDate to Purchase table
-- Both nullable — existing purchases keep no payment tracking
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'Purchase' AND COLUMN_NAME = 'SupplierDueDate')
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [SupplierDueDate] DATE NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'Purchase' AND COLUMN_NAME = 'TargetPaymentDate')
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ADD [TargetPaymentDate] DATE NULL;
END
GO
