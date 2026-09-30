/*
    Migration: 221_SeedOrphanedFileDeletionEnabled
    Description: Seeds the PlatformConfig setting that governs DESTRUCTIVE orphaned-file deletion
                 (Phase 4b-2): OrphanedFileDeletionEnabled (default 'false' — ships DISABLED).

                 This is deliberately SEPARATE from OrphanedFileCleanupEnabled (which controls the
                 non-destructive detection scan). Detection can run for weeks populating the report
                 before deletion is ever switched on, so a full cycle can be watched first. Both must
                 be enabled by a SuperAdmin from Admin > Storage > Cleanup for files to be removed.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[PlatformConfig] WHERE [Key] = 'OrphanedFileDeletionEnabled')
BEGIN
    INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
    VALUES (
        'OrphanedFileDeletionEnabled',
        'false',
        'Switch for PERMANENT deletion of orphaned files past their scheduled date (Phase 4b-2). Separate from OrphanedFileCleanupEnabled (detection). Ships disabled; a SuperAdmin enables it from Admin > Storage > Cleanup. Files are re-verified as still-orphaned immediately before removal.'
    );

    PRINT 'Seeded PlatformConfig key OrphanedFileDeletionEnabled = false.';
END
ELSE
    PRINT 'PlatformConfig key OrphanedFileDeletionEnabled already exists.';
GO
