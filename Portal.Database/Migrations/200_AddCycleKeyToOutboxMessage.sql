-- ============================================================
-- Migration 200: Add CycleKey to OutboxMessage for scheduled-digest dedup
-- ============================================================
-- Purpose: Cycle-based producers (weekly digests, and later Recurring
--          Invoices / Payroll) need an EXACT per-cycle idempotency key so a
--          scheduler re-run, overlap, or restart never enqueues the same
--          business twice for the same period. A dedicated NVARCHAR column is
--          used (NOT a hash into the int RelatedEntityId, which is not
--          collision-safe at the per-business/per-assistant scope).
--
--          Event-driven producers (e.g. Thank-You) leave CycleKey NULL and
--          keep using the existing RelatedEntityType/RelatedEntityId dedup.
--
--          Additive + nullable -> existing rows and Thank-You are unaffected.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF COL_LENGTH('notification.OutboxMessage', 'CycleKey') IS NULL
BEGIN
    ALTER TABLE [notification].[OutboxMessage]
        ADD [CycleKey] NVARCHAR(80) NULL;
    PRINT 'Added [CycleKey] to [notification].[OutboxMessage].';
END
ELSE
    PRINT '[CycleKey] already exists.';
GO

-- Filtered index supporting the exact-match cycle dedup lookup
-- (BusinessId, AssistantTypeId, CycleKey). None of the Phase 1 indexes cover it.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_OutboxMessage_Cycle'
      AND object_id = OBJECT_ID('notification.OutboxMessage')
)
BEGIN
    CREATE INDEX [IX_OutboxMessage_Cycle]
        ON [notification].[OutboxMessage] ([BusinessId], [AssistantTypeId], [CycleKey])
        WHERE [CycleKey] IS NOT NULL;
    PRINT 'Created index [IX_OutboxMessage_Cycle].';
END
ELSE
    PRINT 'Index [IX_OutboxMessage_Cycle] already exists.';
GO
