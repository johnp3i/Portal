/*
    Migration: 214_AddStorageUsageIndexes
    Description: Covering indexes to speed up the per-business storage-usage SUM(FileSizeBytes)
                 queries. Phase 3 enforcement runs these three sums on every file upload
                 (document attachments, compliance attachments, logos), so they should be cheap
                 even as the tables grow.

                 Each index keys on the column(s) the usage query filters by and INCLUDEs
                 FileSizeBytes so the aggregate is a covering index seek (no key lookups).

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- =============================================================================
-- 1. Document attachments — SUM(FileSizeBytes) WHERE BusinessId=@ AND IsDeleted=0
--    (also serves the by-EntityType breakdown on the Storage tab)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DocumentAttachment_BusinessId_IsDeleted_Size'
      AND object_id = OBJECT_ID(N'[document].[DocumentAttachment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DocumentAttachment_BusinessId_IsDeleted_Size]
        ON [document].[DocumentAttachment] ([BusinessId], [IsDeleted])
        INCLUDE ([FileSizeBytes], [EntityType]);
    PRINT 'Created IX_DocumentAttachment_BusinessId_IsDeleted_Size.';
END
ELSE
    PRINT 'IX_DocumentAttachment_BusinessId_IsDeleted_Size already exists.';
GO

-- =============================================================================
-- 2. Compliance attachments — SUM(FileSizeBytes) grouped/filtered by BusinessApplicationId
--    (joined to BusinessApplication to reach BusinessId)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ApplicationAttachment_BusinessApplicationId_Size'
      AND object_id = OBJECT_ID(N'[compliance].[ApplicationAttachment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ApplicationAttachment_BusinessApplicationId_Size]
        ON [compliance].[ApplicationAttachment] ([BusinessApplicationId])
        INCLUDE ([FileSizeBytes]);
    PRINT 'Created IX_ApplicationAttachment_BusinessApplicationId_Size.';
END
ELSE
    PRINT 'IX_ApplicationAttachment_BusinessApplicationId_Size already exists.';
GO

-- =============================================================================
-- 3. Business logos — SUM(FileSizeBytes) WHERE BusinessId=@
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BusinessLogo_BusinessId_Size'
      AND object_id = OBJECT_ID(N'[portal].[BusinessLogo]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BusinessLogo_BusinessId_Size]
        ON [portal].[BusinessLogo] ([BusinessId])
        INCLUDE ([FileSizeBytes]);
    PRINT 'Created IX_BusinessLogo_BusinessId_Size.';
END
ELSE
    PRINT 'IX_BusinessLogo_BusinessId_Size already exists.';
GO
