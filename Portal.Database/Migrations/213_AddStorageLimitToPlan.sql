/*
    Migration: 213_AddStorageLimitToPlan
    Description: Adds [StorageLimitMb] to [dbo].[Plan] — the per-tier storage cap (in MB)
                 used by the Storage pages. NULL = unlimited (no cap).
                 Phase 2 is display-only ("X of Y" + amber/red bar); enforcement is Phase 3.
                 Editable per plan via Admin/Subscriptions (no redeploy needed to change caps).

    Seeds by slug: starter = 250 MB, professional = 5120 MB (5 GB), enterprise = 25600 MB (25 GB).

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Add StorageLimitMb column (NULL = unlimited)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Plan]') AND name = N'StorageLimitMb'
)
BEGIN
    ALTER TABLE [dbo].[Plan]
        ADD [StorageLimitMb] INT NULL;
    PRINT 'Added [dbo].[Plan].[StorageLimitMb].';
END
ELSE
    PRINT '[dbo].[Plan].[StorageLimitMb] already exists.';
GO

-- =============================================================================
-- 2. Seed default caps by slug (only where not already set, so admin edits stick)
-- =============================================================================

UPDATE [dbo].[Plan] SET [StorageLimitMb] = 250   WHERE [Slug] = 'starter'      AND [StorageLimitMb] IS NULL;
UPDATE [dbo].[Plan] SET [StorageLimitMb] = 5120  WHERE [Slug] = 'professional' AND [StorageLimitMb] IS NULL;
UPDATE [dbo].[Plan] SET [StorageLimitMb] = 25600 WHERE [Slug] = 'enterprise'   AND [StorageLimitMb] IS NULL;
GO
