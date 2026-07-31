-- ============================================================
-- Migration 149: Move SupplierRecurringRule to [purchase] schema
-- ============================================================
-- Purpose: Corrects the schema assignment for the SupplierRecurringRule
--          table. It logically belongs with other purchase-related tables
--          (Supplier, ExpenseCategory, Purchase) in the [purchase] schema,
--          not in [billing].
-- ============================================================

USE [Portal]
GO

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing' AND TABLE_NAME = 'SupplierRecurringRule'
)
BEGIN
    ALTER SCHEMA [purchase] TRANSFER [billing].[SupplierRecurringRule];
    PRINT 'Moved [billing].[SupplierRecurringRule] to [purchase] schema.';
END
ELSE IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'purchase' AND TABLE_NAME = 'SupplierRecurringRule'
)
BEGIN
    PRINT '[purchase].[SupplierRecurringRule] already exists — no action needed.';
END
GO
