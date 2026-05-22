-- ============================================================
-- Mark VAT Periods 1-7 as Submitted
-- Period 8 (Dec 2025 - Feb 2026) remains pending/current
-- ============================================================

-- First ensure submissions exist for each period (create if missing)
INSERT INTO [vat].[VatSubmission] ([BusinessId], [VatSubmissionPeriodId], [TotalOutputVat], [TotalInputVat], [NetVatPayable], [IsSubmitted], [SubmittedAtUtc], [Notes], [CreatedAtUtc])
SELECT 1, Id, 0, 0, 0, 1, GETUTCDATE(), NULL, GETUTCDATE()
FROM [vat].[VatSubmissionPeriod]
WHERE Id BETWEEN 1 AND 7
  AND BusinessId = 1
  AND Id NOT IN (SELECT VatSubmissionPeriodId FROM [vat].[VatSubmission] WHERE BusinessId = 1);
GO

-- Mark any existing unsubmitted submissions for periods 1-7 as submitted
UPDATE [vat].[VatSubmission]
SET [IsSubmitted] = 1,
    [SubmittedAtUtc] = GETUTCDATE()
WHERE BusinessId = 1
  AND VatSubmissionPeriodId BETWEEN 1 AND 7
  AND IsSubmitted = 0;
GO
