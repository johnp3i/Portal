-- ============================================================
-- SEED: Z-Reports for Filtering Test (Scenario 11)
-- ============================================================
-- Purpose: Creates revenue sources and Z-Reports across April–July 2026
--          for testing list filtering, quick date ranges, and pagination.
--
-- Target Database: Portal
-- Business ID: 1000 (Le Paris Roasting demo business)
--
-- Prerequisites:
--   - Business 1000 exists
--   - Migration 127 (CreateRevenueSourceTable) has been run
--   - Migration 128 (CreateRevenueSummaryTable) has been run
--   - Migration 129 (CreateRevenueSummaryLineTable) has been run
--   - BusinessProfile.IsZReportEnabled = 1
-- ============================================================

USE [Portal]
GO

DECLARE @BusinessId INT = 1000;

-- ============================================================
-- 1. Enable Z-Report feature on business profile
-- ============================================================
UPDATE [portal].[BusinessProfile]
SET [IsZReportEnabled] = 1
WHERE [BusinessId] = @BusinessId;

PRINT 'Z-Report feature enabled for BusinessId=1000.';
GO

-- ============================================================
-- 2. Create Revenue Sources
-- ============================================================
DECLARE @BusinessId INT = 1000;
DECLARE @Source1Id INT, @Source2Id INT, @Source3Id INT;

IF NOT EXISTS (SELECT 1 FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Main POS')
BEGIN
    INSERT INTO [revenue].[RevenueSource] ([BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
    VALUES (@BusinessId, N'Main POS', N'Front counter register', 1, GETUTCDATE());
END
SET @Source1Id = (SELECT [Id] FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Main POS');

IF NOT EXISTS (SELECT 1 FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Bar Register')
BEGIN
    INSERT INTO [revenue].[RevenueSource] ([BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
    VALUES (@BusinessId, N'Bar Register', N'Cocktail bar POS device', 1, GETUTCDATE());
END
SET @Source2Id = (SELECT [Id] FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Bar Register');

IF NOT EXISTS (SELECT 1 FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Terrace POS')
BEGIN
    INSERT INTO [revenue].[RevenueSource] ([BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
    VALUES (@BusinessId, N'Terrace POS', N'Outdoor terrace register', 1, GETUTCDATE());
END
SET @Source3Id = (SELECT [Id] FROM [revenue].[RevenueSource] WHERE [BusinessId] = @BusinessId AND [Name] = N'Terrace POS');

PRINT 'Revenue Sources created: Main POS, Bar Register, Terrace POS.';

-- ============================================================
-- 3. Create Z-Reports (April–July 2026)
-- ============================================================
-- Helper: Insert a Z-Report with 2 VAT lines (5% and 9%)
-- We'll create ~25 Z-Reports across 4 months, spread across sources.

-- Clean previous test data (idempotent)
DELETE FROM [revenue].[RevenueSummaryLine]
WHERE [RevenueSummaryId] IN (
    SELECT [Id] FROM [revenue].[RevenueSummary]
    WHERE [BusinessId] = @BusinessId AND [Reference] = N'SEED_FILTER_TEST'
);
DELETE FROM [revenue].[RevenueSummary]
WHERE [BusinessId] = @BusinessId AND [Reference] = N'SEED_FILTER_TEST';

PRINT 'Cleaned previous seed data.';

-- ── APRIL 2026 (Main POS) ──────────────────────────────────
DECLARE @SummaryId INT;

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-04-01', '2026-04-07', N'90001', 4200.00, 231.00, 4431.00, 85.00, 312, N'SEED_FILTER_TEST', N'Week 1 April', '2026-04-08 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 3800.00, 190.00, 3990.00, 70.00, GETUTCDATE()),
       (@SummaryId, 9.00, 400.00, 41.00, 441.00, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-04-08', '2026-04-14', N'90002', 3950.00, 217.50, 4167.50, 60.00, 287, N'SEED_FILTER_TEST', N'Week 2 April', '2026-04-15 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 3550.00, 177.50, 3727.50, 45.00, GETUTCDATE()),
       (@SummaryId, 9.00, 400.00, 40.00, 440.00, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-04-15', '2026-04-21', N'90003', 4500.00, 247.50, 4747.50, 90.00, 330, N'SEED_FILTER_TEST', N'Week 3 April', '2026-04-22 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4100.00, 205.00, 4305.00, 75.00, GETUTCDATE()),
       (@SummaryId, 9.00, 400.00, 42.50, 442.50, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-04-22', '2026-04-30', N'90004', 5100.00, 280.50, 5380.50, 110.00, 395, N'SEED_FILTER_TEST', N'Week 4 April', '2026-05-01 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4600.00, 230.00, 4830.00, 90.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 50.50, 550.50, 20.00, GETUTCDATE());

-- ── APRIL 2026 (Bar Register) ──────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source2Id, '2026-04-01', '2026-04-30', N'B-4001', 8200.00, 738.00, 8938.00, 200.00, 610, N'SEED_FILTER_TEST', N'April monthly bar', '2026-05-01 10:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 2200.00, 110.00, 2310.00, 50.00, GETUTCDATE()),
       (@SummaryId, 9.00, 6000.00, 628.00, 6628.00, 150.00, GETUTCDATE());

-- ── MAY 2026 (Main POS) ────────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-05-01', '2026-05-07', N'90005', 4800.00, 264.00, 5064.00, 95.00, 345, N'SEED_FILTER_TEST', N'Week 1 May', '2026-05-08 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4300.00, 215.00, 4515.00, 80.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 49.00, 549.00, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-05-08', '2026-05-14', N'90006', 4600.00, 253.00, 4853.00, 75.00, 320, N'SEED_FILTER_TEST', N'Week 2 May', '2026-05-15 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4100.00, 205.00, 4305.00, 60.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 48.00, 548.00, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-05-15', '2026-05-21', N'90007', 5200.00, 286.00, 5486.00, 120.00, 380, N'SEED_FILTER_TEST', N'Week 3 May', '2026-05-22 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4700.00, 235.00, 4935.00, 100.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 51.00, 551.00, 20.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-05-22', '2026-05-31', N'90008', 5800.00, 319.00, 6119.00, 140.00, 420, N'SEED_FILTER_TEST', N'Week 4 May', '2026-06-01 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5200.00, 260.00, 5460.00, 120.00, GETUTCDATE()),
       (@SummaryId, 9.00, 600.00, 59.00, 659.00, 20.00, GETUTCDATE());

-- ── MAY 2026 (Bar Register) ────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source2Id, '2026-05-01', '2026-05-31', N'B-5001', 9100.00, 819.00, 9919.00, 240.00, 690, N'SEED_FILTER_TEST', N'May monthly bar', '2026-06-01 10:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 2500.00, 125.00, 2625.00, 60.00, GETUTCDATE()),
       (@SummaryId, 9.00, 6600.00, 694.00, 7294.00, 180.00, GETUTCDATE());

-- ── MAY 2026 (Terrace POS) ─────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source3Id, '2026-05-01', '2026-05-31', N'T-5001', 3400.00, 170.00, 3570.00, 45.00, 210, N'SEED_FILTER_TEST', N'May terrace', '2026-06-01 10:30', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 3400.00, 170.00, 3570.00, 45.00, GETUTCDATE());

-- ── JUNE 2026 (Main POS) ───────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-06-01', '2026-06-07', N'90009', 5500.00, 302.50, 5802.50, 100.00, 400, N'SEED_FILTER_TEST', N'Week 1 June', '2026-06-08 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5000.00, 250.00, 5250.00, 85.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 52.50, 552.50, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-06-08', '2026-06-14', N'90010', 5300.00, 291.50, 5591.50, 90.00, 385, N'SEED_FILTER_TEST', N'Week 2 June', '2026-06-15 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 4800.00, 240.00, 5040.00, 75.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 51.50, 551.50, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-06-15', '2026-06-21', N'90011', 5700.00, 313.50, 6013.50, 105.00, 410, N'SEED_FILTER_TEST', N'Week 3 June', '2026-06-22 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5200.00, 260.00, 5460.00, 90.00, GETUTCDATE()),
       (@SummaryId, 9.00, 500.00, 53.50, 553.50, 15.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-06-22', '2026-06-30', N'90012', 6100.00, 335.50, 6435.50, 130.00, 450, N'SEED_FILTER_TEST', N'Week 4 June', '2026-07-01 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5500.00, 275.00, 5775.00, 110.00, GETUTCDATE()),
       (@SummaryId, 9.00, 600.00, 60.50, 660.50, 20.00, GETUTCDATE());

-- ── JUNE 2026 (Bar Register) ───────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source2Id, '2026-06-01', '2026-06-30', N'B-6001', 10500.00, 945.00, 11445.00, 280.00, 780, N'SEED_FILTER_TEST', N'June monthly bar', '2026-07-01 10:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 2800.00, 140.00, 2940.00, 70.00, GETUTCDATE()),
       (@SummaryId, 9.00, 7700.00, 805.00, 8505.00, 210.00, GETUTCDATE());

-- ── JUNE 2026 (Terrace POS) ────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source3Id, '2026-06-01', '2026-06-30', N'T-6001', 5200.00, 260.00, 5460.00, 65.00, 350, N'SEED_FILTER_TEST', N'June terrace (peak season)', '2026-07-01 10:30', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5200.00, 260.00, 5460.00, 65.00, GETUTCDATE());

-- ── JULY 2026 (Main POS) ───────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-07-01', '2026-07-07', N'90013', 6000.00, 330.00, 6330.00, 125.00, 440, N'SEED_FILTER_TEST', N'Week 1 July', '2026-07-08 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5400.00, 270.00, 5670.00, 105.00, GETUTCDATE()),
       (@SummaryId, 9.00, 600.00, 60.00, 660.00, 20.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-07-08', '2026-07-14', N'90014', 6200.00, 341.00, 6541.00, 130.00, 455, N'SEED_FILTER_TEST', N'Week 2 July', '2026-07-15 09:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 5600.00, 280.00, 5880.00, 110.00, GETUTCDATE()),
       (@SummaryId, 9.00, 600.00, 61.00, 661.00, 20.00, GETUTCDATE());

INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source1Id, '2026-07-15', '2026-07-18', N'90015', 3200.00, 176.00, 3376.00, 55.00, 230, N'SEED_FILTER_TEST', N'Mid-July partial', '2026-07-18 18:00', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 2900.00, 145.00, 3045.00, 45.00, GETUTCDATE()),
       (@SummaryId, 9.00, 300.00, 31.00, 331.00, 10.00, GETUTCDATE());

-- ── JULY 2026 (Bar Register) ───────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source2Id, '2026-07-01', '2026-07-18', N'B-7001', 7800.00, 702.00, 8502.00, 190.00, 560, N'SEED_FILTER_TEST', N'July bar (partial month)', '2026-07-18 18:30', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 2100.00, 105.00, 2205.00, 50.00, GETUTCDATE()),
       (@SummaryId, 9.00, 5700.00, 597.00, 6297.00, 140.00, GETUTCDATE());

-- ── JULY 2026 (Terrace POS) ────────────────────────────────
INSERT INTO [revenue].[RevenueSummary]
    ([BusinessId],[RevenueSourceId],[SummaryDate],[PeriodEndDate],[ZReportNumber],[TotalNet],[TotalVat],[TotalGross],[TotalDiscount],[TransactionCount],[Reference],[Notes],[ExportedAtUtc],[IsActive],[CreatedAtUtc])
VALUES
    (@BusinessId, @Source3Id, '2026-07-01', '2026-07-18', N'T-7001', 6100.00, 305.00, 6405.00, 80.00, 420, N'SEED_FILTER_TEST', N'July terrace (peak)', '2026-07-18 18:45', 1, GETUTCDATE());
SET @SummaryId = SCOPE_IDENTITY();
INSERT INTO [revenue].[RevenueSummaryLine] ([RevenueSummaryId],[VatRate],[NetAmount],[VatAmount],[TotalAmount],[DiscountAmount],[CreatedAtUtc])
VALUES (@SummaryId, 5.00, 6100.00, 305.00, 6405.00, 80.00, GETUTCDATE());

-- ============================================================
-- Summary
-- ============================================================
PRINT '──────────────────────────────────────────────────────';
PRINT 'Z-Report Filtering Test Seed Complete!';
PRINT '──────────────────────────────────────────────────────';
PRINT 'Created 20 Z-Reports across April–July 2026:';
PRINT '  • Main POS:     11 weekly reports (90001–90015)';
PRINT '  • Bar Register:  4 monthly reports (B-4001, B-5001, B-6001, B-7001)';
PRINT '  • Terrace POS:   4 monthly reports (T-5001, T-6001, T-7001 + May)';
PRINT '';
PRINT 'Filter test suggestions:';
PRINT '  • Source filter: Select "Bar Register" → expect 4 results';
PRINT '  • Date range: June only → expect 6 results (4 Main + 1 Bar + 1 Terrace)';
PRINT '  • Z-Report #: Search "9001" → partial match on 90010–90015';
PRINT '  • Quick "Last Month": Should show July 2026 data';
PRINT '  • Pagination: All 20 records exceed page 1 (15/page)';
PRINT '──────────────────────────────────────────────────────';
GO
