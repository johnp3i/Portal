/*
    Migration: 218_CreateOrphanedFileStatusType
    Description: Reference (lookup) table for orphaned-file candidate statuses. Includes a
                 [Description] column whose text is surfaced on the admin "Upcoming for deletion"
                 page (the status legend is rendered from these rows, not hard-coded) and helps a
                 DB admin understand each status code directly in the database.

                 Static seed data — exempt from the CreatedAtUtc convention per the schema-design
                 steering (reference tables with fixed seed data).

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[Storage].[OrphanedFileStatusType]') AND type = N'U'
)
BEGIN
    CREATE TABLE [Storage].[OrphanedFileStatusType] (
        [Id]          INT           NOT NULL PRIMARY KEY,
        [Name]        NVARCHAR(50)  NOT NULL,
        [Description] NVARCHAR(400) NOT NULL
    );
    PRINT 'Created [Storage].[OrphanedFileStatusType].';
END
ELSE
    PRINT '[Storage].[OrphanedFileStatusType] already exists.';
GO

-- Seed the four statuses with explanatory descriptions (idempotent by Id).
MERGE [Storage].[OrphanedFileStatusType] AS Target
USING (VALUES
    (1, N'Pending',   N'Detected as orphaned (no database record references this file). It will be permanently deleted after the grace period passes, unless you cancel or pause it first.'),
    (2, N'Paused',    N'Deletion is temporarily on hold. The file stays in the list but will not be deleted until you resume it, which restarts the grace-period countdown.'),
    (3, N'Cancelled', N'Excluded from deletion. This file will be kept and will not be re-listed by future scans. You can un-cancel it to make it a deletion candidate again.'),
    (4, N'Deleted',   N'The file has been permanently removed from storage. Kept here as an audit record; see the deletion report for details.')
) AS Source ([Id], [Name], [Description])
ON Target.[Id] = Source.[Id]
WHEN MATCHED THEN
    UPDATE SET [Name] = Source.[Name], [Description] = Source.[Description]
WHEN NOT MATCHED THEN
    INSERT ([Id], [Name], [Description]) VALUES (Source.[Id], Source.[Name], Source.[Description]);
GO
