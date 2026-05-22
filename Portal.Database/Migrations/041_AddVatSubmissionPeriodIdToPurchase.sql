-- Migration 041: Add VatSubmissionPeriodId to Purchase table
-- Allows purchases to be explicitly assigned to a VAT submission period,
-- supporting late-recorded purchases that belong to a different period than their InvoiceDate suggests.

ALTER TABLE [purchase].[Purchase]
ADD [VatSubmissionPeriodId] INT NULL;
GO

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_VatSubmissionPeriod
    FOREIGN KEY ([VatSubmissionPeriodId])
    REFERENCES [vat].[VatSubmissionPeriod]([Id]);
GO

CREATE NONCLUSTERED INDEX IX_Purchase_VatSubmissionPeriodId
ON [purchase].[Purchase] ([VatSubmissionPeriodId])
WHERE [VatSubmissionPeriodId] IS NOT NULL;
GO
