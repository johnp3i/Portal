-- ============================================================
-- Migration 201: Seed the two Phase 4a owner-alert AssistantType rows
-- ============================================================
-- Purpose: Register the New Payment Received (event) and Daily Brief (scheduled,
--          daily) assistants. Both owner-facing (RecipientKind = 'PortalUser',
--          not customer-facing). Idempotent explicit-Id MERGE, matching 193/198.
--          Ids: 1=thank_you, 2=weekly_outstanding_digest, 3=weekly_financial_snapshot,
--          4=new_payment_received, 5=daily_brief.
--
--          NOTE: the MERGE ... WHEN NOT MATCHED silently no-ops if an Id is already
--          taken. The verification block below PRINTs a count of the two keys so a
--          no-op (assistant not seeded) is visible when the script is run.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

MERGE [notification].[AssistantType] AS target
USING (VALUES
    (4, 'new_payment_received', 'New Payment Received',
        'Emails you the moment a payment is recorded, so you know money has landed.',
        'PortalUser', 0),
    (5, 'daily_brief', 'Daily Brief',
        'Emails you a short daily summary of what needs your attention — and nothing on quiet days.',
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
    WHERE [Key] IN ('new_payment_received', 'daily_brief')
);
IF @seeded = 2
    PRINT 'Seeded [notification].[AssistantType] (new_payment_received, daily_brief) — verified (2/2).';
ELSE
    PRINT 'WARNING: expected 2 owner-alert AssistantType rows but found ' + CAST(@seeded AS NVARCHAR(10))
        + '. Check for an Id collision (MERGE no-ops on a taken Id) and reassign Ids 4/5 if needed.';
GO
