-- ============================================================
-- Reset VAT Schema Tables (Truncate with FK handling)
-- Also clears VatSubmissionPeriodId references from Purchase
-- ============================================================

-- Step 1: Clear the FK reference from Purchase to VatSubmissionPeriod
UPDATE [purchase].[Purchase] SET [VatSubmissionPeriodId] = NULL;
GO

-- Step 2: Drop FK constraints that prevent truncation
ALTER TABLE [vat].[VatSubmission] DROP CONSTRAINT FK_VatSubmission_VatSubmissionPeriod;
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT FK_Purchase_VatSubmissionPeriod;
GO

-- Step 3: Truncate tables (resets identity to 1)
TRUNCATE TABLE [vat].[VatSubmission];
TRUNCATE TABLE [vat].[VatSubmissionPeriod];
GO

-- Step 4: Re-add FK constraints
ALTER TABLE [vat].[VatSubmission]
ADD CONSTRAINT FK_VatSubmission_VatSubmissionPeriod
    FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod]([Id]);

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_VatSubmissionPeriod
    FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod]([Id]);
GO
