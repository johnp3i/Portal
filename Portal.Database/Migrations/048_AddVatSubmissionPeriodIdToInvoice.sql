/*
    Migration: 048_AddVatSubmissionPeriodIdToInvoice
    Description: Adds a nullable VatSubmissionPeriodId column to [invoice].[Invoice],
                 allowing invoices to be explicitly assigned to a VAT submission period
                 independently of their InvoiceDate. Mirrors the existing pattern on
                 [purchase].[Purchase] (migration 041).

                 Includes:
                 - Nullable INT column with FK to [vat].[VatSubmissionPeriod].[Id]
                 - Filtered non-clustered index (WHERE VatSubmissionPeriodId IS NOT NULL)
                 - Idempotent backfill: assigns existing invoices to the matching period
                   by date-range (earliest PeriodStartDate wins), only where
                   VatSubmissionPeriodId IS NULL and IsDeleted = 0

    Requirements: 1.1, 1.4, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Add nullable VatSubmissionPeriodId column
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'Invoice'
      AND COLUMN_NAME = 'VatSubmissionPeriodId'
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
    ADD [VatSubmissionPeriodId] INT NULL;
END
GO

-- Step 2: Add foreign key constraint FK_Invoice_VatSubmissionPeriod
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_Invoice_VatSubmissionPeriod'
      AND [parent_object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    ALTER TABLE [invoice].[Invoice]
    ADD CONSTRAINT [FK_Invoice_VatSubmissionPeriod]
        FOREIGN KEY ([VatSubmissionPeriodId])
        REFERENCES [vat].[VatSubmissionPeriod]([Id]);
END
GO

-- Step 3: Create filtered non-clustered index IX_Invoice_VatSubmissionPeriodId
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Invoice_VatSubmissionPeriodId'
      AND [object_id] = OBJECT_ID('[invoice].[Invoice]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoice_VatSubmissionPeriodId]
        ON [invoice].[Invoice] ([VatSubmissionPeriodId])
        WHERE [VatSubmissionPeriodId] IS NOT NULL;
END
GO

-- Step 4: Backfill existing invoices with matching VatSubmissionPeriod by date-range
-- Only updates invoices where VatSubmissionPeriodId IS NULL and IsDeleted = 0.
-- If multiple periods match an invoice's date, the period with the earliest PeriodStartDate wins.
-- Invoices with no matching period remain NULL (requirement 6.5).
IF EXISTS (
    SELECT 1
    FROM [invoice].[Invoice]
    WHERE [VatSubmissionPeriodId] IS NULL
      AND [IsDeleted] = 0
)
BEGIN
    UPDATE [invoice].[Invoice]
    SET [invoice].[Invoice].[VatSubmissionPeriodId] = MatchedPeriod.[PeriodId]
    FROM [invoice].[Invoice]
    INNER JOIN (
        SELECT
            [invoice].[Invoice].[Id] AS [InvoiceId],
            (
                SELECT TOP 1 [vat].[VatSubmissionPeriod].[Id]
                FROM [vat].[VatSubmissionPeriod]
                WHERE [vat].[VatSubmissionPeriod].[BusinessId] = [invoice].[Invoice].[BusinessId]
                  AND [vat].[VatSubmissionPeriod].[PeriodStartDate] <= [invoice].[Invoice].[InvoiceDate]
                  AND [vat].[VatSubmissionPeriod].[PeriodEndDate] >= [invoice].[Invoice].[InvoiceDate]
                ORDER BY [vat].[VatSubmissionPeriod].[PeriodStartDate] ASC
            ) AS [PeriodId]
        FROM [invoice].[Invoice]
        WHERE [invoice].[Invoice].[VatSubmissionPeriodId] IS NULL
          AND [invoice].[Invoice].[IsDeleted] = 0
    ) AS MatchedPeriod ON [invoice].[Invoice].[Id] = MatchedPeriod.[InvoiceId]
    WHERE MatchedPeriod.[PeriodId] IS NOT NULL;
END
GO
