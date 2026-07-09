-- ============================================================
-- Migration: 005_AddSchedulePaymentsToModuleConstraint
-- Description: Updates the CK_UserBusinessPermission_Module CHECK constraint
--              to include all current module values (schedule_payments and others
--              added since the original migration).
-- ============================================================

USE [Portal.Membership]
GO

-- Drop the existing constraint
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE [name] = 'CK_UserBusinessPermission_Module'
      AND parent_object_id = OBJECT_ID('[membership].[UserBusinessPermission]')
)
BEGIN
    ALTER TABLE [membership].[UserBusinessPermission]
        DROP CONSTRAINT [CK_UserBusinessPermission_Module];
END
GO

-- Recreate with all current modules
ALTER TABLE [membership].[UserBusinessPermission]
    ADD CONSTRAINT [CK_UserBusinessPermission_Module] CHECK (
        [Module] IN (
            'customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat',
            'audit', 'credit', 'products', 'payment_reminder_manual', 'payment_reminder_auto',
            'cashflow', 'pnl', 'expense_insights', 'audit_log', 'schedule_payments'
        )
    );
GO
