/*
    Migration: 216_AddFileSizeBytesToSignature
    Description: Adds [FileSizeBytes] to [portal].[Signature] so signatures count toward a business's
                 storage usage and are subject to the plan storage cap (Phase 4a). Previously
                 signatures were shown as "not counted yet" because the table had no size column.

                 Existing rows default to 0 (= "not yet measured"). Their real sizes are backfilled
                 at runtime from the files on disk by SignatureService.BackfillFileSizesAsync
                 (a DB migration cannot stat the filesystem). New uploads record the size directly.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[portal].[Signature]') AND name = N'FileSizeBytes'
)
BEGIN
    ALTER TABLE [portal].[Signature]
        ADD [FileSizeBytes] BIGINT NOT NULL CONSTRAINT [DF_Signature_FileSizeBytes] DEFAULT 0;
    PRINT 'Added [portal].[Signature].[FileSizeBytes].';
END
ELSE
    PRINT '[portal].[Signature].[FileSizeBytes] already exists.';
GO
