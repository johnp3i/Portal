/*
    Script:  Seed_Vasilis_WorkforcePi_Operations_Payroll_Quotation
    Purpose: Seeds the "Vasilis — WorkforcePi Operations & 3 Inventors Payroll" proposal as a Draft
             quotation, matching .kiro/docs/QuotationPlatform/quotations/
             Vasilis_WorkforcePi_Operations_Payroll_Proposal_Specification.md and the structure in
             proposal_quotation_structure.md.

    Target customer:
      - DEV / LOCAL run: @CustomerId = 1009   (set below)
      - PRODUCTION run:  @CustomerId = 1032   (change the one line marked below)

    Pricing (all VAT 19%; subscription lines are MONTHLY unit prices annualised x12 by the app):
      Section (Subscription) WorkforcePi Operations:
        - 40 x EUR 10.50/mo, 10% line discount -> monthly LineTotal 378.00 -> x12 = 4,536.00/yr
      Section (Subscription) 3 Inventors Payroll:
        - 1  x EUR 89.00/mo, Fixed 14.83/mo discount -> monthly LineTotal 74.17 -> x12 = 890.04/yr
          (NOTE: exactly 890.00/yr is not reachable on an 89/mo line because 890/12 is not a clean
           2-dp figure; 890.04 is the closest achievable while preserving the 89 list price.)
      Section (OneTime) Banking:
        - Bank of Cyprus setup  : 1 x EUR 350.00
        - Bank of Cyprus annual : 1 x EUR 590.00   (flat annual charge, OneTime so it is NOT x12'd)
      Section (OneTime) Implementation:
        - 1 x EUR 850.00   (confirmed fee; spec's recommended 1,250 was overridden to 850)

    Header totals (computed exactly as the app's RecalculateQuotationTotalsAsync would):
        Subtotal   = 4,536.00 + 890.04 + 350.00 + 590.00 + 850.00 = 7,216.04
        TaxAmount  = round(per-line VAT sum)                        = 1,371.05
        TotalAmount= 7,216.04 + 0 (no bulk discount) + 1,371.05     = 8,587.09

    Idempotency: wrapped in a transaction. Re-running creates a NEW quotation (new Reference).
    Safe: inserts only; does not modify existing data.
*/

USE [Portal]
GO

-- Required for inserts into tables that carry filtered indexes (quotation tables do).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- ============================================================================
-- Parameters
-- ============================================================================
DECLARE @CustomerId   INT = 1009;   -- <<< DEV / LOCAL value. For PRODUCTION set to 1032.
DECLARE @BusinessId   INT;
DECLARE @NowUtc       DATETIME = GETUTCDATE();
DECLARE @ValidUntil   DATE = CAST(DATEADD(DAY, 30, GETUTCDATE()) AS DATE);
DECLARE @Reference    NVARCHAR(100);
DECLARE @QuotationContactId INT;

-- Resolve the business tenant from the customer so the quotation belongs to the right business.
SELECT @BusinessId = [BusinessId]
FROM [customer].[Customer]
WHERE [Id] = @CustomerId;

IF @BusinessId IS NULL
BEGIN
    RAISERROR('Customer %d not found; cannot resolve BusinessId. Aborting.', 16, 1, @CustomerId);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Resolve a "Prepared By" contact for this business (most recent active one), if any. Optional.
SELECT TOP (1) @QuotationContactId = [Id]
FROM [quotation].[QuotationContact]
WHERE [BusinessId] = @BusinessId AND [IsActive] = 1
ORDER BY [Id] DESC;

-- Build a reference following the QUO-YYYY-MM-##### convention (next per-business sequence).
DECLARE @Seq INT;
SELECT @Seq = ISNULL(MAX(TRY_CONVERT(INT, RIGHT([Reference], 5))), 0) + 1
FROM [quotation].[Quotation]
WHERE [BusinessId] = @BusinessId
  AND [Reference] LIKE 'QUO-%';

SET @Reference = CONCAT('QUO-', FORMAT(@NowUtc, 'yyyy'), '-', FORMAT(@NowUtc, 'MM'), '-', RIGHT('00000' + CAST(@Seq AS NVARCHAR(5)), 5));

-- ============================================================================
-- Quotation header (Draft). Totals set to final computed values below.
-- ============================================================================
DECLARE @QuotationId INT;

INSERT INTO [quotation].[Quotation]
    ([BusinessId], [CustomerId], [QuotationStatusTypeId], [Reference], [ValidUntil],
     [Subtotal], [TaxAmount], [TotalAmount], [Notes],
     [CreatedAtUtc], [UpdatedAtUtc], [IsDeleted], [QuotationContactId], [IsGrandTotalShown])
VALUES
    (@BusinessId, @CustomerId, 1, @Reference, @ValidUntil,
     7216.04, 1371.05, 8587.09,
     N'WorkforcePi Operations, Staff Portal, 3 Inventors Payroll and Bank of Cyprus payroll integration for approximately 40 active employees pooled across multiple Business Units. Annual commitment pricing. Implementation commencement within 40 days following signed agreement.',
     @NowUtc, @NowUtc, 0, @QuotationContactId, 1);

SET @QuotationId = SCOPE_IDENTITY();

-- ============================================================================
-- Sections (ordered). Narrative sections carry their prose in [Description].
-- Do NOT create a "General" section (that is the implicit null-section bucket).
-- ============================================================================
DECLARE @SecOverview    INT;
DECLARE @SecWorkflow    INT;
DECLARE @SecOperations  INT;  -- LineItems / Subscription
DECLARE @SecStaffPortal INT;  -- Narrative / OneTime
DECLARE @SecPayroll     INT;  -- LineItems / Subscription
DECLARE @SecBanking     INT;  -- LineItems / OneTime
DECLARE @SecImplement   INT;  -- LineItems / OneTime
DECLARE @SecHardware    INT;
DECLARE @SecDelivery    INT;
DECLARE @SecCommercial  INT;

-- 1. Multi-Business Workforce Operations (Narrative / OneTime, emphasized)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Multi-Business Workforce Operations', 1, N'OneTime',
     N'WorkforcePi will provide a unified workforce operations environment supporting approximately 40 active employees across multiple Business Units.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Individual businesses/locations can be represented as Business Units, with Departments defined within each BU. Employees and attendance devices operate within the appropriate BU while administration remains visible at Organisation level.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'The 40 Active Employee Seats are pooled across the entire Organisation rather than purchased separately per Business Unit or physical location.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'The solution combines WorkforcePi Operations, Staff Portal, 3 Inventors Payroll and banking capabilities to connect workforce planning and employee communication with attendance verification, payroll preparation and salary payment.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Commercial principle: pay for people, not premises.',
     NULL, N'Narrative', 1, NULL, N'SOLUTION OVERVIEW', 0, 0);
SET @SecOverview = SCOPE_IDENTITY();

-- 2. Automated Workforce Verification & Processing (Narrative / OneTime, emphasized)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Automated Workforce Verification & Processing', 2, N'OneTime',
     N'Rota -> Actual Attendance -> Verification: WorkforcePi compares planned rota information with actual employee attendance records and identifies discrepancies for manager review, including missing records, late/early attendance and deviations from planned working time. Managers remain in control of verification and corrections before records are approved.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Approved Worksheets -> Payroll -> Salary Payments: Verified workforce records flow into employee worksheets and then into 3 Inventors Payroll, reducing repeated manual entry. Following payroll verification and approval, salary payments proceed through the configured banking method. For Bank of Cyprus, payroll payment batches can be submitted through the supported B2B interface and subsequently authorised by the organisation''s authorised signatory/signatories. Eurobank-compatible payroll payment files can alternatively be generated for the bank-supported upload workflow.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Workforce Holidays -> Planning & Communication: Holiday and leave management is connected to workforce planning and the relevant communication mechanism. Approved absences can be reflected across workforce operations, reducing discrepancies between leave, rota planning and attendance expectations. WorkforcePi Operations and the Staff Portal provide the employee-facing foundation for this wider communication process.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Control principle: automated verification does not mean uncontrolled automatic correction. Manager approval remains explicit where records require confirmation or adjustment.',
     NULL, N'Narrative', 1, NULL, N'OPERATIONAL WORKFLOW', 0, 0);
SET @SecWorkflow = SCOPE_IDENTITY();

-- 3. WorkforcePi Operations (LineItems / Subscription, emphasized, totals shown)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'WorkforcePi Operations', 3, N'Subscription',
     N'Organisation-wide active employee capacity including rota, attendance, reconciliation, Staff Portal, workforce communication and multi-Business-Unit operations. Active seats belong to the Organisation and may be allocated across Business Units and Departments. Commercial principle: pay for people, not premises.',
     NULL, N'LineItems', 1, NULL, N'WORKFORCEPI OPERATIONS', 1, 0);
SET @SecOperations = SCOPE_IDENTITY();

-- 4. Staff Portal & Workforce Communication (Narrative / OneTime)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Staff Portal & Workforce Communication', 4, N'OneTime',
     N'Staff Portal capabilities are included with WorkforcePi Operations and do not carry a separate employee-seat charge. The Staff Portal provides the employee-facing foundation for workforce information and communication. Relevant Operations capabilities include employee profiles, workforce information, checklists/objectives, policies/reporting and holiday-related processes according to implemented scope.',
     NULL, N'Narrative', 0, NULL, N'STAFF PORTAL', 0, 0);
SET @SecStaffPortal = SCOPE_IDENTITY();

-- 5. 3 Inventors Payroll (LineItems / Subscription, totals shown)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'3 Inventors Payroll', 5, N'Subscription',
     N'Payroll processing following approved workforce records, including payroll records, salary/hourly inputs, country-template statutory calculations, configurable funds/deductions/contributions, payslips, payroll history, employer contributions/reporting and supported payment/export workflows.',
     NULL, N'LineItems', 0, NULL, N'PAYROLL SUBSCRIPTION', 1, 0);
SET @SecPayroll = SCOPE_IDENTITY();

-- 6. Banking & Payroll Payment Services (LineItems / OneTime)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Banking & Payroll Payment Services', 6, N'OneTime',
     N'Bank of Cyprus B2B payroll integration (initial setup plus annual live connector service). Eurobank-compatible payroll payment-file generation is included with 3 Inventors Payroll at no additional integration fee (a file-based capability, not a separately charged live API integration). Final bank authorisation/execution remains under the customer''s authorised banking users/signatories.',
     NULL, N'LineItems', 0, NULL, N'BANKING', 1, 0);
SET @SecBanking = SCOPE_IDENTITY();

-- 7. Implementation, Configuration & Go-Live (LineItems / OneTime, emphasized, totals shown)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Implementation, Configuration & Go-Live', 7, N'OneTime',
     N'Organisation, Business Unit and Department configuration; initial workforce setup/import and employee allocation to BUs/Departments; manager and operational permissions; attendance-device and multi-BU attendance configuration; WorkforcePi Operations and Staff Portal configuration; rota/attendance/reconciliation and holiday/workforce-communication workflow configuration; 3 Inventors Payroll and Cyprus payroll/company configuration; Bank of Cyprus workflow configuration and Eurobank payroll-file workflow validation; operational testing; administrator/manager onboarding; deployment preparation; and initial go-live support.',
     NULL, N'LineItems', 1, NULL, N'IMPLEMENTATION', 1, 0);
SET @SecImplement = SCOPE_IDENTITY();

-- 8. Attendance Devices (Narrative / OneTime)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Attendance Devices', 8, N'OneTime',
     N'Attendance hardware is not included in the WorkforcePi subscription. The organisation may use compatible existing hardware, subject to technical verification by 3 Inventors, or purchase suitable devices separately. Where new hardware is required, indicative cost is approximately EUR 200-500 per device, depending on specifications and requirements. 3 Inventors can recommend and configure suitable devices. Hardware is not priced in this quotation until device quantity/specification is confirmed.',
     NULL, N'Narrative', 0, NULL, N'HARDWARE', 0, 0);
SET @SecHardware = SCOPE_IDENTITY();

-- 9. Implementation Schedule (Narrative / OneTime)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Implementation Schedule', 9, N'OneTime',
     N'Following acceptance and signing of the agreement, implementation is scheduled to commence within 40 days. The detailed rollout schedule, configuration activities, device deployment and go-live plan will be coordinated with the organisation.',
     NULL, N'Narrative', 0, NULL, N'DELIVERY', 0, 0);
SET @SecDelivery = SCOPE_IDENTITY();

-- 10. Commercial Terms (Narrative / OneTime)
INSERT INTO [quotation].[ProposalSection]
    ([QuotationId], [Name], [SortOrder], [ColumnConfiguration], [Description], [Notes],
     [SectionType], [IsEmphasized], [AccentColor], [Label], [IsTotalsTableShown], [IsHalfWidth])
VALUES
    (@QuotationId, N'Commercial Terms', 10, N'OneTime',
     N'Active Employee Seats: the subscription is based on purchased Active Employee Seat capacity. Additional registered employee records may be maintained while simultaneously active employees are limited by purchased capacity; a registered employee does not by itself consume an active seat.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Organisation-wide capacity: the 40 Active Employee Seats belong to the Organisation and may be allocated across Business Units and Departments according to operational requirements. WorkforcePi does not charge separately per BU, Department or physical location.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Additional seats: may be purchased when required. Under annual commitment, additional seats may be activated immediately and charged for the remaining subscription period according to applicable terms.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Capacity reductions: for annual subscriptions, reductions take effect at renewal. Active employee count must be within the new capacity before reduction becomes effective.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Annual commitment: the agreed annual discount applies to committed subscription capacity and represents the commercial exchange for annual commitment.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Staff Portal: access is included within WorkforcePi Operations and is not a separate per-employee subscription.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     N'Hardware: attendance devices are excluded unless explicitly listed as priced quotation items.',
     NULL, N'Narrative', 0, NULL, N'COMMERCIAL TERMS', 0, 0);
SET @SecCommercial = SCOPE_IDENTITY();

-- ============================================================================
-- Lines. LineTotal is the per-line NET (after line discount), excluding VAT, in MONTHLY
-- terms for Subscription-section lines (the app annualises x12 at calculation time).
-- ============================================================================

-- WorkforcePi Operations: 40 x 10.50/mo, 10% percentage discount.
--   gross monthly = 40 * 10.50 = 420.00; net monthly = round(420.00 * 0.90, 2) = 378.00
INSERT INTO [quotation].[QuotationLine]
    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType],
     [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle],
     [ProductCode], [IsReverseCharge], [IsAdjustmentLine], [ProductPriceTierId], [PriceTierName])
VALUES
    (@QuotationId, N'WorkforcePi Operations - Active Employee Seat', 40, 10.50, 19.00, 10.00, N'Percentage',
     NULL, 378.00, 1, NULL, @SecOperations,
     N'Organisation-wide active employee capacity including rota, attendance, reconciliation, Staff Portal, workforce communication and multi-Business-Unit operations.',
     NULL, 0, 0, NULL, NULL);

-- 3 Inventors Payroll: 1 x 89.00/mo, Fixed 14.83/mo discount.
--   gross monthly = 89.00; net monthly = round(89.00 - 14.83, 2) = 74.17 -> x12 = 890.04/yr
INSERT INTO [quotation].[QuotationLine]
    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType],
     [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle],
     [ProductCode], [IsReverseCharge], [IsAdjustmentLine], [ProductPriceTierId], [PriceTierName])
VALUES
    (@QuotationId, N'3 Inventors Payroll - 26-100 Employees', 1, 89.00, 19.00, 14.83, N'Fixed',
     NULL, 74.17, 1, NULL, @SecPayroll,
     N'Annual commitment pricing. List price EUR 89.00/month; annual commitment value EUR 890.04.',
     NULL, 0, 0, NULL, NULL);

-- Banking (OneTime): Bank of Cyprus setup 350.00, then annual service 590.00 (flat annual, not x12).
INSERT INTO [quotation].[QuotationLine]
    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType],
     [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle],
     [ProductCode], [IsReverseCharge], [IsAdjustmentLine], [ProductPriceTierId], [PriceTierName])
VALUES
    (@QuotationId, N'Bank of Cyprus B2B Payroll Integration - Initial Setup & Activation', 1, 350.00, 19.00, 0.00, N'Percentage',
     NULL, 350.00, 1, NULL, @SecBanking,
     N'Initial onboarding, configuration, banking environment/credential setup, connection validation, testing and first successful payroll submission support.',
     NULL, 0, 0, NULL, NULL);

INSERT INTO [quotation].[QuotationLine]
    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType],
     [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle],
     [ProductCode], [IsReverseCharge], [IsAdjustmentLine], [ProductPriceTierId], [PriceTierName])
VALUES
    (@QuotationId, N'Bank of Cyprus B2B Payroll Integration Service - Annual', 1, 590.00, 19.00, 0.00, N'Percentage',
     NULL, 590.00, 2, NULL, @SecBanking,
     N'Live connector service including payroll batch submission, supported payment-status retrieval, connector maintenance, compatibility updates and integration support.',
     NULL, 0, 0, NULL, NULL);

-- Implementation (OneTime): 1 x 850.00 (confirmed fee)
INSERT INTO [quotation].[QuotationLine]
    ([QuotationId], [Description], [Quantity], [UnitPrice], [VatRate], [Discount], [DiscountType],
     [CostPrice], [LineTotal], [SortOrder], [ReferenceUrl], [ProposalSectionId], [Subtitle],
     [ProductCode], [IsReverseCharge], [IsAdjustmentLine], [ProductPriceTierId], [PriceTierName])
VALUES
    (@QuotationId, N'WorkforcePi Operations & Payroll - Implementation, Configuration & Go-Live', 1, 850.00, 19.00, 0.00, N'Percentage',
     NULL, 850.00, 1, NULL, @SecImplement,
     N'One-time implementation, configuration and go-live. Covers general WorkforcePi Operations/Payroll setup (the Bank of Cyprus activation line separately covers banking-connector onboarding).',
     NULL, 0, 0, NULL, NULL);

-- ============================================================================
-- Verification: recompute totals the same way the app does and compare to the header.
-- ============================================================================
DECLARE @CalcSubtotal DECIMAL(18,2);
DECLARE @CalcTax      DECIMAL(18,2);
DECLARE @CalcTotal    DECIMAL(18,2);

;WITH L AS (
    SELECT
        ql.[LineTotal],
        ql.[VatRate],
        CASE WHEN ps.[ColumnConfiguration] = N'Subscription' THEN 12.0 ELSE 1.0 END AS Mult
    FROM [quotation].[QuotationLine] ql
    LEFT JOIN [quotation].[ProposalSection] ps ON ps.[Id] = ql.[ProposalSectionId]
    WHERE ql.[QuotationId] = @QuotationId
      AND ql.[IsAdjustmentLine] = 0
)
SELECT
    @CalcSubtotal = ROUND(SUM(L.[LineTotal] * L.Mult), 2),
    @CalcTax      = ROUND(SUM(L.[LineTotal] * L.Mult * L.[VatRate] / 100.0), 2)
FROM L;

SET @CalcTotal = @CalcSubtotal + @CalcTax;

PRINT CONCAT('Quotation Id       : ', @QuotationId);
PRINT CONCAT('Reference          : ', @Reference);
PRINT CONCAT('CustomerId         : ', @CustomerId, '  BusinessId: ', @BusinessId);
PRINT CONCAT('Computed Subtotal  : ', @CalcSubtotal, '  (header 7216.04)');
PRINT CONCAT('Computed Tax       : ', @CalcTax,      '  (header 1371.05)');
PRINT CONCAT('Computed Total     : ', @CalcTotal,    '  (header 8587.09)');

IF (@CalcSubtotal <> 7216.04 OR @CalcTax <> 1371.05 OR @CalcTotal <> 8587.09)
BEGIN
    RAISERROR('Computed totals do not match expected header totals. Rolling back so nothing inconsistent is persisted.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Sync the header to the independently computed values (defensive; they should already match).
UPDATE [quotation].[Quotation]
SET [Subtotal] = @CalcSubtotal,
    [TaxAmount] = @CalcTax,
    [TotalAmount] = @CalcTotal,
    [UpdatedAtUtc] = GETUTCDATE()
WHERE [Id] = @QuotationId;

COMMIT TRANSACTION;

PRINT 'Seed committed successfully.';
GO
