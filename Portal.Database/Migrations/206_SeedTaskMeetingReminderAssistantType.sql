-- ============================================================
-- Migration 206: Seed the Task & Meeting Reminder AssistantType row
-- ============================================================
-- Purpose: Register the Task & Meeting Reminder (scheduled, daily cadence)
--          assistant. Owner/internal-facing (RecipientKind = 'PortalUser',
--          not customer-facing) — but it fans out to each assigned team member.
--          Idempotent explicit-Id MERGE, matching 202.
--          Ids: 1=thank_you, 2=weekly_outstanding_digest, 3=weekly_financial_snapshot,
--          4=new_payment_received, 5=daily_brief, 6=vat_period_due_reminder,
--          7=task_meeting_reminder.
--
--          NOTE: the MERGE ... WHEN NOT MATCHED silently no-ops if an Id is already
--          taken. The verification block below PRINTs a count so a no-op is visible.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

MERGE [notification].[AssistantType] AS target
USING (VALUES
    (7, 'task_meeting_reminder', 'Task & Meeting Reminder',
        'Emails each team member a morning agenda of their upcoming tasks and meetings.',
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
    WHERE [Key] = 'task_meeting_reminder'
);
IF @seeded = 1
    PRINT 'Seeded [notification].[AssistantType] (task_meeting_reminder) — verified (1/1).';
ELSE
    PRINT 'WARNING: expected 1 task_meeting_reminder AssistantType row but found ' + CAST(@seeded AS NVARCHAR(10))
        + '. Check for an Id collision (MERGE no-ops on a taken Id) and reassign Id 7 if needed.';
GO
