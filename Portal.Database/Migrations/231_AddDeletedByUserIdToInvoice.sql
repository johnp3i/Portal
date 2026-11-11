USE [Portal];
GO

/*
    Migration: 231_AddDeletedByUserIdToInvoice
    Description: Adds [DeletedByUserId] NVARCHAR(450) NULL to [invoice].[Invoice] to record WHO
                 soft-deleted a draft invoice (the deletion time is already captured by DeletedAtUtc,
                 added in migration 043). The invoice list shows the deleter's name (resolved from the
                 Membership database) and the deletion time in the business's local timezone.

                 Nullable with no default: pre-existing deleted rows have no recorded deleter and will
                 display as "Unknown". 450 matches the ASP.NET Identity user-key length used elsewhere
                 (e.g. CreatedByUserId).

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'Invoice'
      AND COLUMN_NAME = 'DeletedByUserId'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
        ADD [DeletedByUserId] NVARCHAR(450) NULL;
END
GO
