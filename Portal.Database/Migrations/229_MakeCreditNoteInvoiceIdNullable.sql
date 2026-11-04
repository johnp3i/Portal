USE [Portal];
GO

/*
    Migration: 229_MakeCreditNoteInvoiceIdNullable
    Description: Supports account-level (invoice-less) credit notes. A credit note may now be issued
                 directly to a customer with no originating invoice: the credit sits on the customer
                 account and is drawn down against invoices later via CreditNoteApplication rows.

                 Changes:
                   1. [credit].[CreditNote].[InvoiceId] becomes NULLABLE (the FK to Invoice already
                      allows NULL semantics — an unmatched NULL simply has no referenced row).
                   2. Adds a composite index on (BusinessId, CustomerId) to find a customer's
                      account-level credits quickly.

                 [credit].[CreditNoteApplication].[InvoiceId] is intentionally left NOT NULL — an
                 application always targets a concrete invoice; only the note's origin can be null.

                 Existing invoice-linked credit notes keep their InvoiceId and are unaffected.

    This script is idempotent — safe to run multiple times.
*/

-- 1. Drop the FK so the column can be altered, make InvoiceId nullable, then recreate the FK.
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'credit'
      AND TABLE_NAME = 'CreditNote'
      AND COLUMN_NAME = 'InvoiceId'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE [name] = 'FK_CreditNote_Invoice'
          AND [parent_object_id] = OBJECT_ID('[credit].[CreditNote]')
    )
    BEGIN
        ALTER TABLE [credit].[CreditNote] DROP CONSTRAINT [FK_CreditNote_Invoice];
    END

    ALTER TABLE [credit].[CreditNote] ALTER COLUMN [InvoiceId] INT NULL;

    ALTER TABLE [credit].[CreditNote]
        ADD CONSTRAINT [FK_CreditNote_Invoice]
        FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id]);
END
GO

-- 2. Composite index on (BusinessId, CustomerId) for account-credit lookups.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CreditNote_BusinessId_CustomerId'
      AND [object_id] = OBJECT_ID('[credit].[CreditNote]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNote_BusinessId_CustomerId]
        ON [credit].[CreditNote] ([BusinessId], [CustomerId]);
END
GO
