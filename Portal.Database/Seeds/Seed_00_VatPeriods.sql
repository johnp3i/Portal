-- ============================================================
-- VAT Submission Periods Seed Data
-- ============================================================

SET IDENTITY_INSERT [vat].[VatSubmissionPeriod] ON;

INSERT INTO [vat].[VatSubmissionPeriod] (Id, BusinessId, PeriodStartDate, PeriodEndDate, PeriodLabel, CreatedAtUtc)
VALUES
(1, 1, '2024-03-01', '2024-05-31', '01 Mar 2024 – 31 May 2024', GETUTCDATE()),
(2, 1, '2024-06-01', '2024-08-31', '01 Jun 2024 – 31 Aug 2024', GETUTCDATE()),
(3, 1, '2024-09-01', '2024-11-30', '01 Sep 2024 – 30 Nov 2024', GETUTCDATE()),
(4, 1, '2024-12-01', '2025-02-28', '01 Dec 2024 – 28 Feb 2025', GETUTCDATE()),
(5, 1, '2025-03-01', '2025-05-31', '01 Mar 2025 – 31 May 2025', GETUTCDATE()),
(6, 1, '2025-06-01', '2025-08-31', '01 Jun 2025 – 31 Aug 2025', GETUTCDATE()),
(7, 1, '2025-09-01', '2025-11-30', '01 Sep 2025 – 30 Nov 2025', GETUTCDATE()),
(8, 1, '2025-12-01', '2026-02-28', '01 Dec 2025 – 28 Feb 2026', GETUTCDATE());

SET IDENTITY_INSERT [vat].[VatSubmissionPeriod] OFF;
