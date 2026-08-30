-- ============================================================
-- Migration 180: Expand DemoInvitationPermission Module CHECK constraint (v2)
-- ============================================================
-- Purpose: Adds stripe_connect, compliance, and payroll to the
--          Module CHECK constraint on [portal].[DemoInvitationPermission].
--          These modules were added to PortalModules after migration 152
--          but were not included in the constraint.
-- Schema: [portal]
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

-- Re-create with the full module list (synced with PortalModules.All)
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
        'sales',
        'stripe_connect',
        'compliance',
        'payroll'
    )
);
PRINT 'Created expanded CK_DemoInvitationPermission_Module constraint (v2 — includes stripe_connect, compliance, payroll).';
GO
