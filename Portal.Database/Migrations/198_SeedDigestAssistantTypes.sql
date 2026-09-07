-- ============================================================
-- Migration 198: Seed the two scheduled-digest AssistantType rows
-- ============================================================
-- Purpose: Register the Weekly Outstanding Balance Digest and Weekly
--          Financial Snapshot assistants (Group 3 — scheduled owner digests).
--          Both are owner-facing (RecipientKind = 'PortalUser', not customer-
--          facing). Idempotent explicit-Id MERGE, matching migration 193.
--          Ids: 1 = thank_you (existing), 2 = weekly_outstanding_digest,
--          3 = weekly_financial_snapshot. No id is pre-reserved for
--          Quotation Follow-Up (it takes the next free id when built).
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

MERGE [notification].[AssistantType] AS target
USING (VALUES
    (2, 'weekly_outstanding_digest', 'Weekly Outstanding Balance Digest',
        'Emails you a weekly summary of what customers owe you and which supplier payments are coming due.',
        'PortalUser', 0),
    (3, 'weekly_financial_snapshot', 'Weekly Financial Snapshot',
        'Emails you a weekly at-a-glance summary of your business''s financial position.',
        'PortalUser', 0)
) AS source ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Key], [Name], [Description], [RecipientKind], [IsCustomerFacing])
    VALUES (source.[Id], source.[Key], source.[Name], source.[Description], source.[RecipientKind], source.[IsCustomerFacing]);
GO

PRINT 'Seeded [notification].[AssistantType] (weekly_outstanding_digest, weekly_financial_snapshot).';
GO
