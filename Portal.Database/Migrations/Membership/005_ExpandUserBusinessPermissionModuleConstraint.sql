-- ============================================================
-- Migration: Expand UserBusinessPermission Module CHECK constraint
-- Database: Portal.Membership
-- ============================================================
-- Purpose: Adds all new modules to the CHECK constraint on
--          [membership].[UserBusinessPermission] so user permissions
--          can include newer features (sales, pnl, cashflow, etc.)
-- ============================================================

USE [Portal.Membership]
GO

-- Drop the existing constraint
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_UserBusinessPermission_Module'
      AND parent_object_id = OBJECT_ID('[membership].[UserBusinessPermission]')
)
BEGIN
    ALTER TABLE [membership].[UserBusinessPermission]
    DROP CONSTRAINT [CK_UserBusinessPermission_Module];
    PRINT 'Dropped existing CK_UserBusinessPermission_Module constraint.';
END
GO

-- Re-create with the full module list
ALTER TABLE [membership].[UserBusinessPermission]
ADD CONSTRAINT [CK_UserBusinessPermission_Module] CHECK (
    [Module] IN (
        'customer',
        'quotation',
        'invoice',
        'revenue',
        'purchase',
        'vat',
        'credit',
        'audit',
        'products',
        'payment_link_manual',
        'payment_reminder_manual',
        'payment_link_auto',
        'payment_reminder_auto',
        'cashflow',
        'pnl',
        'expense_insights',
        'attachments',
        'client_portal',
        'activity_timeline',
        'audit_log',
        'api',
        'webhooks',
        'multi_currency',
        'schedule_payments',
        'recurring_expense_validation',
        'purchase_import',
        'zreport_import',
        'sales'
    )
);
PRINT 'Created expanded CK_UserBusinessPermission_Module constraint.';
GO
