-- ============================================================
-- Migration 196: Seed digital_assistants PlanFeature + expand demo constraint
-- ============================================================
-- Purpose: Grants the Digital Assistants module to Professional and
--          Enterprise tiers (excluded on Foundation), and adds
--          'digital_assistants' to the DemoInvitationPermission Module
--          CHECK constraint so it stays in sync with PortalModules.All.
-- Idempotent — safe to run multiple times.
-- ============================================================

USE [Portal]
GO

-- ---- Plan feature grant ---------------------------------------------------
INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
SELECT [Plan].[Id], 'digital_assistants', 1, 'full'
FROM [dbo].[Plan]
WHERE [Plan].[Name] IN ('Professional', 'Enterprise')
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[PlanFeature]
        WHERE [PlanFeature].[PlanId] = [Plan].[Id]
          AND [PlanFeature].[ModuleName] = 'digital_assistants'
  );
GO

PRINT 'Seeded digital_assistants PlanFeature for Professional and Enterprise.';
GO

-- ---- Expand DemoInvitationPermission Module CHECK constraint --------------
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
        'payroll',
        'digital_assistants'
    )
);
PRINT 'Created expanded CK_DemoInvitationPermission_Module constraint (includes digital_assistants).';
GO
