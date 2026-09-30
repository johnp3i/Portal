/*
    Migration: 222_FixOrphanedFileCandidateRelativePathLength
    Description: Fixes the oversized UNIQUE index key on [Storage].[OrphanedFileCandidate].
                 [RelativePath] was originally NVARCHAR(1024); as the key of a nonclustered UNIQUE
                 index that is 2048 bytes, over SQL Server's 1700-byte limit — which raises a warning
                 at create time and causes insert/update to FAIL for a sufficiently long path.

                 Shrinks the column to NVARCHAR(500), matching the source columns it is derived from
                 ([document].[DocumentAttachment].[StoragePath], [portal].[Signature].[FilePath],
                 [compliance].[ApplicationAttachment].[FilePath] — all NVARCHAR(500)), so a real path
                 can never be truncated, and rebuilds the UNIQUE index within the key-size limit.

                 Only needed on databases where 219 ran with the old NVARCHAR(1024) definition; fresh
                 installs get NVARCHAR(500) directly from the (corrected) 219. Idempotent — safe to
                 run multiple times.
*/

USE [Portal]
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[Storage].[OrphanedFileCandidate]') AND type = N'U')
   AND EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Storage].[OrphanedFileCandidate]')
          AND name = N'RelativePath'
          AND max_length <> 1000  -- NVARCHAR(500) => 1000 bytes; anything else needs fixing
   )
BEGIN
    -- Drop the oversized unique index before altering the column it keys on.
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_OrphanedFileCandidate_RelativePath'
          AND object_id = OBJECT_ID(N'[Storage].[OrphanedFileCandidate]')
    )
        DROP INDEX [UX_OrphanedFileCandidate_RelativePath] ON [Storage].[OrphanedFileCandidate];

    ALTER TABLE [Storage].[OrphanedFileCandidate]
        ALTER COLUMN [RelativePath] NVARCHAR(500) NOT NULL;

    CREATE UNIQUE INDEX [UX_OrphanedFileCandidate_RelativePath]
        ON [Storage].[OrphanedFileCandidate]([RelativePath]);

    PRINT 'Shrank [Storage].[OrphanedFileCandidate].[RelativePath] to NVARCHAR(500) and rebuilt the UNIQUE index.';
END
ELSE
    PRINT 'No change needed: [Storage].[OrphanedFileCandidate].[RelativePath] is already NVARCHAR(500) (or the table does not exist).';
GO
