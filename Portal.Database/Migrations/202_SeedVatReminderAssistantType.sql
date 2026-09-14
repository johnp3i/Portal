-- ============================================================
-- Migration 202: Seed the VAT Period Due Reminder AssistantType row
-- ============================================================
-- Purpose: Register the VAT Period Due Reminder (scheduled, daily cadence)
--          assistant. Owner-facing (RecipientKind = 'PortalUser', not
--          customer-facing). Idempotent explicit-Id MERGE, matching 201.
--          Ids: 1=thank_you, 2=weekly_outstanding_digest, 3=weekly_financial_snapshot,
--          4=new_payment_received, 5=daily_brief, 6=vat_period_due_reminder.
--
--          NOTE: the MERGE ... WHEN NOT MATCHED silently no-ops if an Id is already
--          taken. The verification block below PRINTs a count so a no-op (assistant
--          not seeded) is visible when the script is run.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

MERGE [notification].[AssistantType] AS target
USING (VALUES
    (6, 'vat_period_due_reminder', 'VAT Period Due Reminder',
        'Reminds you before each VAT filing deadline, with an estimate of what you''ll owe.',
        'PortalUser', 0)
) AS source ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
    VALUES (source.[Id], source.[Key], source.[Name], source.[Description], source.[RecipientKind], source.[IsCustomerFacing]);
GO

-- Verify the seed landed (guards against a silent no-op on a taken Id).
DECLARE @seeded INT = (
    SELECT COUNT(*) FROM [notification].[AssistantType]
    WHERE [Key] = 'vat_period_due_reminder'
);
IF @seeded = 1
    PRINT 'Seeded [notification].[AssistantType] (vat_period_due_reminder) — verified (1/1).';
ELSE
    PRINT 'WARNING: expected 1 vat_period_due_reminder AssistantType row but found ' + CAST(@seeded AS NVARCHAR(10))
        + '. Check for an Id collision (MERGE no-ops on a taken Id) and reassign Id 6 if needed.';
GO
