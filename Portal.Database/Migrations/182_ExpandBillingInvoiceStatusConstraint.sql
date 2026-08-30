-- ============================================================
-- Migration 182: Expand [billing].[Invoice] Status CHECK constraint
-- ============================================================
-- Purpose: Adds 'partially_paid' to the Status CHECK constraint
--          to support instalment payments where the total paid is
--          less than the invoice amount.
-- Schema: [billing]
-- ============================================================

USE [Portal]
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_BillingInvoice_Status'
      AND parent_object_id = OBJECT_ID('[billing].[Invoice]')
)
BEGIN
    ALTER TABLE [billing].[Invoice]
    DROP CONSTRAINT [CK_BillingInvoice_Status];
    PRINT 'Dropped existing CK_BillingInvoice_Status constraint.';
END
GO

ALTER TABLE [billing].[Invoice]
ADD CONSTRAINT [CK_BillingInvoice_Status] CHECK (
    [Status] IN ('draft', 'open', 'paid', 'void', 'uncollectible', 'partially_paid')
);
PRINT 'Created expanded CK_BillingInvoice_Status constraint (includes partially_paid).';
GO
