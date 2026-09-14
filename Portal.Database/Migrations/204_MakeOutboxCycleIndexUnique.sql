-- ============================================================
-- Migration 204: Make the OutboxMessage cycle index UNIQUE (race hardening)
-- ============================================================
-- Purpose: Close the check-then-insert race in DigestEnqueuer. Migration 200
--          created a NON-unique filtered index IX_OutboxMessage_Cycle on
--          (BusinessId, AssistantTypeId, CycleKey) WHERE CycleKey IS NOT NULL.
--          Because EnqueueAsync does ExistsForCycleAsync THEN InsertAsync as two
--          statements, two overlapping scheduler passes can both pass the check
--          and double-insert. Making the index UNIQUE lets the DB reject the
--          duplicate; EnqueueAsync catches the violation and treats it as
--          "already enqueued" (return false).
--
--          This is a SHARED hardening co-owned by the VAT Period Due Reminder and
--          the Task & Meeting Reminder specs. Idempotent: safe to run once.
--
--          A previously-Failed cycle may legitimately re-enqueue (a new row after
--          fixing delivery), so the UNIQUE constraint would wrongly block that if
--          the Failed row remained. The dedup below keeps only the EARLIEST row per
--          (BusinessId, AssistantTypeId, CycleKey) regardless of status; going
--          forward the unique index + EnqueueAsync's exists-check (which excludes
--          Failed) together allow at most one non-Failed row while still permitting
--          a retry after a Failed row is itself removed. NOTE: if operational data
--          needs Failed rows retained, revisit this — for now the outbox doubles as
--          the activity log and a single row per cycle is the intended invariant.
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

-- 1. Remove any pre-existing duplicate cycle rows (keep the earliest by Id) so the
--    UNIQUE index can be created. No-op on a clean database.
;WITH Ranked AS (
    SELECT [Id],
           ROW_NUMBER() OVER (
               PARTITION BY [BusinessId], [AssistantTypeId], [CycleKey]
               ORDER BY [Id] ASC
           ) AS rn
    FROM [notification].[OutboxMessage]
    WHERE [CycleKey] IS NOT NULL
)
DELETE FROM [notification].[OutboxMessage]
WHERE [Id] IN (SELECT [Id] FROM Ranked WHERE rn > 1);

PRINT 'Deduplicated [notification].[OutboxMessage] cycle rows (kept earliest per business/assistant/cycle).';
GO

-- 2. Drop the non-unique index (from migration 200) if present.
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_OutboxMessage_Cycle'
      AND object_id = OBJECT_ID('notification.OutboxMessage')
)
BEGIN
    DROP INDEX [IX_OutboxMessage_Cycle] ON [notification].[OutboxMessage];
    PRINT 'Dropped non-unique index [IX_OutboxMessage_Cycle].';
END
ELSE
    PRINT 'Index [IX_OutboxMessage_Cycle] not present (already replaced or never created).';
GO

-- 3. Create the UNIQUE filtered index (same key + filter as before, now unique).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_OutboxMessage_Cycle'
      AND object_id = OBJECT_ID('notification.OutboxMessage')
)
BEGIN
    CREATE UNIQUE INDEX [UX_OutboxMessage_Cycle]
        ON [notification].[OutboxMessage] ([BusinessId], [AssistantTypeId], [CycleKey])
        WHERE [CycleKey] IS NOT NULL;
    PRINT 'Created UNIQUE index [UX_OutboxMessage_Cycle].';
END
ELSE
    PRINT 'Index [UX_OutboxMessage_Cycle] already exists.';
GO
