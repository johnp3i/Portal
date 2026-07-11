-- ============================================================
-- Backfill: Assign unassigned purchases to their matching VAT period
-- 
-- This script retroactively assigns VatSubmissionPeriodId to all
-- purchases that currently have NULL, by matching their InvoiceDate
-- to the period's date range.
--
-- Safe to run multiple times (only updates NULL assignments).
-- ============================================================

USE [Portal]
GO

UPDATE [purchase].[Purchase]
SET [purchase].[Purchase].[VatSubmissionPeriodId] = [vat].[VatSubmissionPeriod].[Id]
FROM [purchase].[Purchase]
INNER JOIN [vat].[VatSubmissionPeriod]
    ON [vat].[VatSubmissionPeriod].[BusinessId] = [purchase].[Purchase].[BusinessId]
    AND [purchase].[Purchase].[InvoiceDate] >= [vat].[VatSubmissionPeriod].[PeriodStartDate]
    AND [purchase].[Purchase].[InvoiceDate] <= [vat].[VatSubmissionPeriod].[PeriodEndDate]
WHERE [purchase].[Purchase].[VatSubmissionPeriodId] IS NULL
  AND [purchase].[Purchase].[IsCancelled] = 0
GO

PRINT 'Backfill complete. All unassigned purchases have been matched to their date-range period.'
GO
