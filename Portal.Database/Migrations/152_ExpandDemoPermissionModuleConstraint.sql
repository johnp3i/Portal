-- ============================================================
-- Migration 152: Expand DemoInvitationPermission Module CHECK constraint
-- ============================================================
-- Purpose: Adds all new modules to the CHECK constraint on [portal].[DemoInvitationPermission]
--          so demo invitations can include: sales, pnl, cashflow, schedule_payments,
--          payment_reminder_manual, payment_reminder_auto, audit_log, expense_insights,
--          purchase_import, attachments, zreport_import, recurring_expense_validation,
--          payment_link_manual, payment_link_auto
-- ============================================================

USE [Portal]
GO

-- Drop the existing constraint
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_DemoInvitationPermission_Module'
      AND parent_object_id = OBJECT_ID('[portal].[DemoInvitationPermission]')
)
BEGIN
    ALTER TABLE [portal].[DemoInvitationPermission]
    DROP CONSTRAINT [CK_DemoInvitationPermission_Module];
    PRINT 'Dropped existing CK_DemoInvitationPermission_Module constraint.';
END
GO

-- Re-create with the full module list
ALTER TABLE [portal].[DemoInvitationPermission]
ADD CONSTRAINT [CK_DemoInvitationPermission_Module] CHECK (
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
PRINT 'Created expanded CK_DemoInvitationPermission_Module constraint.';
GO
