USE [Portal];
GO

/*
    Migration: 232_AddUniqueInvoiceNumberPerBusiness
    Description: Enforces invoice-number uniqueness within a business at the database level, so a
                 retired (soft-deleted) invoice number can NEVER be reused — the register stays a
                 complete, gap-free, non-duplicated sequence. Deleted invoices are retained (never
                 hard-deleted) and keep their number; this index spans ALL rows (deleted or not), so
                 reuse is impossible even via a bug.

                 Fails loudly (RAISERROR) if the existing data already contains a duplicate
                 (BusinessId, InvoiceNumber) pair, rather than silently skipping — a duplicate must be
                 investigated and resolved before uniqueness can be guaranteed.

    This script is idempotent — safe to run multiple times.

    NOTE: the duplicate guard and the index creation are intentionally in the SAME batch (no GO
    between them) so the RETURN on duplicate-detection actually prevents the CREATE INDEX from
    running — otherwise a later batch would still attempt the index and fail with a cryptic error.
*/

-- Guard: abort (without creating the index) if any duplicate (BusinessId, InvoiceNumber) exists.
IF EXISTS (
    SELECT 1
    FROM [invoice].[Invoice]
    GROUP BY [BusinessId], [InvoiceNumber]
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR(
        'Cannot create unique index UX_Invoice_BusinessId_InvoiceNumber: duplicate (BusinessId, InvoiceNumber) pairs exist. Resolve the duplicates before applying this migration.',
        16, 1);
    RETURN;
END

-- Create the unique index (covers all rows, including soft-deleted). Idempotent.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_Invoice_BusinessId_InvoiceNumber'
      AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Invoice_BusinessId_InvoiceNumber]
        ON [invoice].[Invoice] ([BusinessId], [InvoiceNumber]);
END
GO
