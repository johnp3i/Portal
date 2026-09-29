/*
    Migration: 219_CreateOrphanedFileCandidate
    Description: One row per physical file detected as orphaned (no live DB record references it).
                 The nightly scan upserts candidates by [RelativePath]; a candidate is deleted only
                 after [ScheduledDeletionAtUtc] passes (Phase 4b-2) and only while Pending.

                 RelativePath is the storage-root-relative, forward-slash path (matches the on-disk
                 layout). BusinessId is nullable — the scan infers it from the first path segment
                 where possible, but a stray file at the root may have no resolvable business.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[Storage].[OrphanedFileCandidate]') AND type = N'U'
)
BEGIN
    CREATE TABLE [Storage].[OrphanedFileCandidate] (
        [Id]                      INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RelativePath]            NVARCHAR(1024) NOT NULL,
        [BusinessId]              INT            NULL,
        [FileSizeBytes]           BIGINT         NOT NULL DEFAULT 0,
        [DetectedAtUtc]           DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [ScheduledDeletionAtUtc]  DATETIME       NOT NULL,
        [OrphanedFileStatusTypeId] INT           NOT NULL
            CONSTRAINT [FK_OrphanedFileCandidate_StatusType]
            REFERENCES [Storage].[OrphanedFileStatusType]([Id]),
        [CreatedAtUtc]            DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAtUtc]            DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );

    -- One candidate row per physical path (the scan upserts on this).
    CREATE UNIQUE INDEX [UX_OrphanedFileCandidate_RelativePath]
        ON [Storage].[OrphanedFileCandidate]([RelativePath]);

    -- Common filters: by status, and by due date for the (future) delete pass.
    CREATE INDEX [IX_OrphanedFileCandidate_Status]
        ON [Storage].[OrphanedFileCandidate]([OrphanedFileStatusTypeId]);
    CREATE INDEX [IX_OrphanedFileCandidate_ScheduledDeletion]
        ON [Storage].[OrphanedFileCandidate]([ScheduledDeletionAtUtc]);

    PRINT 'Created [Storage].[OrphanedFileCandidate].';
END
ELSE
    PRINT '[Storage].[OrphanedFileCandidate] already exists.';
GO
