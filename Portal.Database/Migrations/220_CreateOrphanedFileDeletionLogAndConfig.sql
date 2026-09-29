/*
    Migration: 220_CreateOrphanedFileDeletionLogAndConfig
    Description: (1) Creates [Storage].[OrphanedFileDeletionLog] — the permanent audit trail of
                    files actually removed by the cleanup (populated in Phase 4b-2; the admin
                    "deletion report" reads from it). (2) Seeds the two PlatformConfig settings that
                    govern the cleanup: OrphanedFileCleanupEnabled (default 'false' — ships DISABLED)
                    and OrphanedFileGraceDays (default '30', admin-editable).

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Deletion log (audit feed / report source)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[Storage].[OrphanedFileDeletionLog]') AND type = N'U'
)
BEGIN
    CREATE TABLE [Storage].[OrphanedFileDeletionLog] (
        [Id]            INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RelativePath]  NVARCHAR(1024) NOT NULL,
        [BusinessId]    INT            NULL,
        [FileSizeBytes] BIGINT         NOT NULL DEFAULT 0,
        [Reason]        NVARCHAR(200)  NOT NULL,
        [DeletedAtUtc]  DATETIME       NOT NULL DEFAULT GETUTCDATE(),
        [CreatedAtUtc]  DATETIME       NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX [IX_OrphanedFileDeletionLog_DeletedAt]
        ON [Storage].[OrphanedFileDeletionLog]([DeletedAtUtc]);

    PRINT 'Created [Storage].[OrphanedFileDeletionLog].';
END
ELSE
    PRINT '[Storage].[OrphanedFileDeletionLog] already exists.';
GO

-- =============================================================================
-- 2. Seed cleanup config (ships DISABLED; grace period editable by SuperAdmin)
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlatformConfig] WHERE [Key] = 'OrphanedFileCleanupEnabled')
BEGIN
    INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
    VALUES (
        'OrphanedFileCleanupEnabled',
        'false',
        'Master switch for the nightly orphaned-file cleanup scan/delete job. Ships disabled; a SuperAdmin enables it from Admin > Storage > Cleanup.'
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlatformConfig] WHERE [Key] = 'OrphanedFileGraceDays')
BEGIN
    INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
    VALUES (
        'OrphanedFileGraceDays',
        '30',
        'Days an orphaned file stays as a candidate before it becomes eligible for deletion. Editable from Admin > Storage > Cleanup.'
    );
END
GO
