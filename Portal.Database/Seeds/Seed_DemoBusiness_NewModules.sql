-- ============================================================
-- DEMO BUSINESS SEED: Le Paris Roasting — New Modules
-- ============================================================
-- Purpose: Seeds additional module data for the existing Le Paris Roasting
--          demo business (BusinessId=1000). Covers:
--            1. Opportunities (Sales Pipeline)
--            2. Z-Reports (Revenue Summary)
--            3. Payment Receipts
--            4. Payment Reminders
--            5. Payment Schedules
--
-- Prerequisites:
--   - Run Seed_DemoBusiness_LeParisRoasting.sql first (Business, Customers,
--     Invoices, Payments must already exist)
--   - All migration scripts up to 152 must be applied
--
-- ID Range: 5000+ (avoids conflicts with main demo seed at 1000+)
-- Demo User: 0650dc64-6615-4d84-947d-da783ed45160
-- ============================================================

USE [Portal];
GO

-- ============================================================
-- SECTION 1: OPPORTUNITIES — Team Members (2)
-- ============================================================

SET IDENTITY_INSERT [sales].[TeamMember] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[TeamMember] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[TeamMember]
        ([Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [Role], [UserId], [IsActive], [CreatedAtUtc])
    VALUES
        (5000, 1000, N'Elena', N'Georgiou', N'elena@leparisroasting.com', N'+357 99 100200', N'Sales Manager', N'0650dc64-6615-4d84-947d-da783ed45160', 1, '2026-01-10T09:00:00'),
        (5001, 1000, N'Nikos', N'Andreou', N'nikos@leparisroasting.com', N'+357 99 100300', N'Account Executive', NULL, 1, '2026-01-10T09:00:00');
END

SET IDENTITY_INSERT [sales].[TeamMember] OFF;
GO

-- ============================================================
-- SECTION 2: OPPORTUNITIES — Sales Products (3)
-- ============================================================

SET IDENTITY_INSERT [sales].[Product] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[Product] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[Product]
        ([Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
    VALUES
        (5000, 1000, N'Premium Coffee Subscription', N'Monthly delivery of freshly roasted specialty coffee — customisable blends and quantities.', 1, '2026-01-15T10:00:00'),
        (5001, 1000, N'Barista Training', N'On-site or virtual barista training sessions for café staff — covers extraction, latte art, and equipment care.', 1, '2026-01-15T10:00:00'),
        (5002, 1000, N'Equipment Maintenance', N'Quarterly maintenance contracts for commercial grinders and espresso machines.', 1, '2026-01-15T10:00:00');
END

SET IDENTITY_INSERT [sales].[Product] OFF;
GO

-- ============================================================
-- SECTION 3: OPPORTUNITIES — Sales Contacts (4 prospects)
-- ============================================================

SET IDENTITY_INSERT [sales].[Contact] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[Contact] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[Contact]
        ([Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber], [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc])
    VALUES
        (5000, 1000, N'Andreas', N'Christodoulou', N'andreas@limassollounges.cy', N'+357 25 300100', N'Limassol Lounges', N'Operations Director', N'Cyprus', N'Met at Cyprus Hospitality Expo 2026', 1, '2026-05-20T08:30:00'),
        (5001, 1000, N'Sofia', N'Papadopoulou', N'sofia@morningbrew.cy', N'+357 22 300200', N'Morning Brew Café', N'Owner', N'Cyprus', N'Referred by Hotel Alexandros', 1, '2026-06-01T09:15:00'),
        (5002, 1000, N'Markos', N'Ioannou', N'markos@coastalhotels.cy', N'+357 24 300300', N'Coastal Hotels Group', N'F&B Manager', N'Cyprus', N'Interested in bulk supply for 3 hotel restaurants', 1, '2026-06-10T11:00:00'),
        (5003, 1000, N'Christina', N'Nicolaou', N'christina@artisancorner.cy', N'+357 22 300400', N'Artisan Corner Bakery', N'Head Barista', N'Cyprus', N'Looking for training and premium beans', 1, '2026-06-18T14:00:00');
END

SET IDENTITY_INSERT [sales].[Contact] OFF;
GO

-- ============================================================
-- SECTION 4: OPPORTUNITIES — Lead Requests (5)
-- ============================================================
-- LeadSourceTypeId: 1=Website, 2=Referral, 3=Event, 4=Cold Call, 6=Social Media
-- LeadStatusTypeId: 1=New, 2=Contacted, 3=Follow-Up, 5=Proposal Sent, 6=Won

SET IDENTITY_INSERT [sales].[LeadRequest] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[LeadRequest] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[LeadRequest]
        ([Id], [BusinessId], [ContactId], [ProductId], [LeadSourceTypeId], [LeadSourceReferenceTypeId], [LeadStatusTypeId],
         [SourceUrl], [RequestText], [AssignedToUserId], [TeamMemberId], [IsCancelled], [CancellationTimestamp], [CancellationDescription], [IsActive], [CreatedAtUtc])
    VALUES
        -- Lead 1: Andreas — Event source, Won
        (5000, 1000, 5000, 5000, 3, NULL, 6,
         NULL, N'Discussed at expo — wants 20kg/month subscription for 2 lounge locations.',
         N'0650dc64-6615-4d84-947d-da783ed45160', 5000, 0, NULL, NULL, 1, '2026-05-20T16:00:00'),

        -- Lead 2: Sofia — Referral, Proposal Sent
        (5001, 1000, 5001, 5000, 2, NULL, 5,
         NULL, N'Looking for a premium single-origin subscription, 10kg/month.',
         N'0650dc64-6615-4d84-947d-da783ed45160', 5000, 0, NULL, NULL, 1, '2026-06-01T10:00:00'),

        -- Lead 3: Markos — Website, Follow-Up
        (5002, 1000, 5002, 5000, 1, NULL, 3,
         N'https://leparisroasting.com/wholesale', N'Submitted enquiry via wholesale page — needs bulk pricing for 3 hotels.',
         N'0650dc64-6615-4d84-947d-da783ed45160', 5001, 0, NULL, NULL, 1, '2026-06-10T11:30:00'),

        -- Lead 4: Christina — Social Media, Contacted
        (5003, 1000, 5003, 5001, 6, NULL, 2,
         N'https://instagram.com/leparisroasting', N'DM on Instagram asking about barista training packages.',
         N'0650dc64-6615-4d84-947d-da783ed45160', 5001, 0, NULL, NULL, 1, '2026-06-18T14:30:00'),

        -- Lead 5: Andreas (2nd lead) — Event, New (equipment maintenance interest)
        (5004, 1000, 5000, 5002, 3, NULL, 1,
         NULL, N'Follow-up from expo — also interested in quarterly maintenance for their La Marzocca.',
         NULL, NULL, 0, NULL, NULL, 1, '2026-07-01T09:00:00');
END

SET IDENTITY_INSERT [sales].[LeadRequest] OFF;
GO

-- ============================================================
-- SECTION 5: OPPORTUNITIES — Lead Response Template (1)
-- ============================================================
-- LeadResponseTypeId: 1=Email

SET IDENTITY_INSERT [sales].[LeadResponseTemplate] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponseTemplate] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[LeadResponseTemplate]
        ([Id], [BusinessId], [ProductId], [LeadResponseTypeId], [Name], [Subject], [BodyTemplate], [ResponseTimeInHours], [IsActive], [CreatedAtUtc])
    VALUES
        (5000, 1000, 5000, 1, N'Coffee Subscription Introduction', N'Le Paris Roasting — Your Premium Coffee Subscription',
         N'Hi {{FirstName}},

Thank you for your interest in our Premium Coffee Subscription. We offer freshly roasted specialty beans delivered to your door on a schedule that suits you.

Our subscription plans include:
- Single Origin (10kg/month): from €185/month
- Custom Blend (20kg/month): from €340/month
- Enterprise (50kg+): custom pricing

I''d love to arrange a tasting session so you can experience our roasts firsthand. Would next week work for you?

Best regards,
{{SenderName}}
Le Paris Roasting',
         24, 1, '2026-01-20T10:00:00');
END

SET IDENTITY_INSERT [sales].[LeadResponseTemplate] OFF;
GO

-- ============================================================
-- SECTION 6: OPPORTUNITIES — Lead Responses (2)
-- ============================================================

SET IDENTITY_INSERT [sales].[LeadResponse] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[LeadResponse] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[LeadResponse]
        ([Id], [LeadRequestId], [LeadResponseTypeId], [LeadResponseTemplateId], [RespondedByUserId], [ResponseText], [IsAutomated], [SentAtUtc], [CreatedAtUtc])
    VALUES
        -- Response to Sofia's lead (used template)
        (5000, 5001, 1, 5000, N'0650dc64-6615-4d84-947d-da783ed45160',
         N'Hi Sofia, Thank you for your interest in our Premium Coffee Subscription. We offer freshly roasted specialty beans delivered on a schedule that suits you. I''d love to arrange a tasting session. Would Thursday work for you? Best regards, Elena — Le Paris Roasting',
         0, '2026-06-02T08:45:00', '2026-06-02T08:45:00'),

        -- Phone follow-up to Markos (no template)
        (5001, 5002, 2, NULL, N'0650dc64-6615-4d84-947d-da783ed45160',
         N'Called Markos to discuss bulk pricing. He needs pricing for 3 locations: 30kg, 20kg, and 15kg monthly. Will send formal proposal by end of week.',
         0, '2026-06-12T10:30:00', '2026-06-12T10:30:00');
END

SET IDENTITY_INSERT [sales].[LeadResponse] OFF;
GO

-- ============================================================
-- SECTION 7: OPPORTUNITIES — Meeting (1)
-- ============================================================
-- MeetingTypeId: 1=Online, 2=On-Site, 3=Phone Call, 4=Video Call

SET IDENTITY_INSERT [sales].[Meeting] ON;

IF NOT EXISTS (SELECT 1 FROM [sales].[Meeting] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [sales].[Meeting]
        ([Id], [BusinessId], [LeadRequestId], [ContactId], [MeetingTypeId], [Subject], [ScheduledAtUtc], [DurationMinutes],
         [Location], [Notes], [Outcome], [IsCancelled], [CancellationTimestamp], [CancellationDescription], [IsActive], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        (5000, 1000, 5001, 5001, 2, N'Coffee Tasting & Subscription Discussion', '2026-06-05T10:00:00', 60,
         N'Morning Brew Café, 18 Stasinou Ave, Nicosia', N'Bring sample pack: Ethiopian Yirgacheffe, Colombian, and house blend.',
         N'Sofia loved the Ethiopian. Agreed on 10kg/month single origin subscription starting July. Sending proposal.', 0, NULL, NULL, 1, N'0650dc64-6615-4d84-947d-da783ed45160', '2026-06-02T09:00:00');
END

SET IDENTITY_INSERT [sales].[Meeting] OFF;
GO

-- ============================================================
-- SECTION 8: Z-REPORTS — Revenue Sources (2)
-- ============================================================

SET IDENTITY_INSERT [revenue].[RevenueSource] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[RevenueSource] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[RevenueSource]
        ([Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
    VALUES
        (5000, 1000, N'POS Terminal - Main Shop', N'Primary point-of-sale at the Makarios Avenue retail shop.', 1, '2026-01-05T08:00:00'),
        (5001, 1000, N'POS Terminal - Warehouse Counter', N'Walk-in counter sales at the roasting warehouse in Strovolos.', 1, '2026-01-05T08:00:00');
END

SET IDENTITY_INSERT [revenue].[RevenueSource] OFF;
GO

-- ============================================================
-- SECTION 9: Z-REPORTS — Revenue Summaries (6)
-- ============================================================
-- VatSubmissionPeriodId = 1003 (Jun-Aug 2026)
-- Realistic daily totals for a coffee roastery retail shop

SET IDENTITY_INSERT [revenue].[RevenueSummary] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[RevenueSummary] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[RevenueSummary]
        ([Id], [BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate], [ZReportNumber],
         [TotalNet], [TotalVat], [TotalGross], [TotalDiscount], [TransactionCount],
         [Reference], [Notes], [ExportedAtUtc], [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc])
    VALUES
        -- Main Shop: 3 daily summaries in June 2026
        (5000, 1000, 5000, '2026-06-02', NULL, N'Z-2026-0601',
         487.39, 92.61, 580.00, 12.50, 74,
         NULL, N'Monday — strong morning rush', NULL, 1003, NULL, 1, '2026-06-02T22:00:00'),

        (5001, 1000, 5000, '2026-06-09', NULL, N'Z-2026-0602',
         630.25, 119.75, 750.00, 8.00, 92,
         NULL, N'Monday — promotional week launch', NULL, 1003, NULL, 1, '2026-06-09T22:00:00'),

        (5002, 1000, 5000, '2026-06-16', NULL, N'Z-2026-0603',
         352.10, 66.90, 419.00, 5.50, 58,
         NULL, N'Monday — quieter summer week', NULL, 1003, NULL, 1, '2026-06-16T22:00:00'),

        -- Main Shop: 1 daily summary in July 2026
        (5003, 1000, 5000, '2026-07-07', NULL, N'Z-2026-0701',
         571.43, 108.57, 680.00, 15.00, 85,
         NULL, N'Monday — tourist season picking up', NULL, 1003, NULL, 1, '2026-07-07T22:00:00'),

        -- Warehouse Counter: 2 daily summaries
        (5004, 1000, 5001, '2026-06-05', NULL, N'W-2026-0601',
         218.49, 41.51, 260.00, 0.00, 28,
         NULL, N'Thursday — wholesale pick-ups', NULL, 1003, NULL, 1, '2026-06-05T18:00:00'),

        (5005, 1000, 5001, '2026-07-03', NULL, N'W-2026-0701',
         310.08, 58.92, 369.00, 4.00, 35,
         NULL, N'Thursday — busy with hotel re-orders', NULL, 1003, NULL, 1, '2026-07-03T18:00:00');
END

SET IDENTITY_INSERT [revenue].[RevenueSummary] OFF;
GO

-- ============================================================
-- SECTION 10: PAYMENT RECEIPTS (2)
-- ============================================================
-- Payment 1000: Invoice 1000 (INV-2026-001), Customer 1004, Amount €1,142.40, BankTransfer (2), Date 2026-01-20
-- Payment 1002: Invoice 1003 (INV-2026-004), Customer 1001, Amount €1,000.00 (partial), BankTransfer (2), Date 2026-02-15

SET IDENTITY_INSERT [revenue].[PaymentReceipt] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentReceipt] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[PaymentReceipt]
        ([Id], [BusinessId], [ReceiptNumber], [CustomerId], [PaymentId], [ReceiptDate],
         [TotalAmountReceived], [OutstandingBalanceAfter], [PaymentMethodTypeId], [PaymentReference],
         [Notes], [SignatureId], [IsVoided], [CreatedByUserId], [CreatedAtUtc])
    VALUES
        -- Receipt for Payment 1000 (full payment of INV-2026-001)
        (5000, 1000, N'REC-001-1-200126', 1004, 1000, '2026-01-20',
         1142.40, 0.00, 2, N'TRF-2026-001',
         N'Full payment received — thank you.', NULL, 0, N'0650dc64-6615-4d84-947d-da783ed45160', '2026-01-20T11:00:00'),

        -- Receipt for Payment 1002 (partial payment of INV-2026-004)
        (5001, 1000, N'REC-004-1-150226', 1001, 1002, '2026-02-15',
         1000.00, 1481.15, 2, N'TRF-2026-004-P1',
         N'First instalment received.', NULL, 0, N'0650dc64-6615-4d84-947d-da783ed45160', '2026-02-15T09:30:00');
END

SET IDENTITY_INSERT [revenue].[PaymentReceipt] OFF;
GO

-- Payment Receipt Lines
SET IDENTITY_INSERT [revenue].[PaymentReceiptLine] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentReceiptLine] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[PaymentReceiptLine]
        ([Id], [PaymentReceiptId], [PaymentId], [InvoiceId], [InvoiceNumber], [Amount], [InvoiceTotal], [InvoiceOutstandingBefore], [InvoiceOutstandingAfter])
    VALUES
        -- Line for Receipt 5000 (full payment of INV-2026-001)
        (5000, 5000, 1000, 1000, N'INV-2026-001', 1142.40, 1142.40, 1142.40, 0.00),

        -- Line for Receipt 5001 (partial payment of INV-2026-004)
        (5001, 5001, 1002, 1003, N'INV-2026-004', 1000.00, 2481.15, 2481.15, 1481.15);
END

SET IDENTITY_INSERT [revenue].[PaymentReceiptLine] OFF;
GO

-- ============================================================
-- SECTION 11: PAYMENT REMINDERS — 3-Tier Schedule Config
-- ============================================================

SET IDENTITY_INSERT [reminder].[PaymentReminderSchedule] ON;

IF NOT EXISTS (SELECT 1 FROM [reminder].[PaymentReminderSchedule] WHERE [BusinessId] = 1000)
BEGIN
    INSERT INTO [reminder].[PaymentReminderSchedule]
        ([Id], [BusinessId], [EscalationTier], [DaysOffset], [MaxRemindersPerTier], [MinIntervalDays], [PartialPaymentSuppressionDays], [IsEnabled], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        -- Friendly: 3 days before due
        (5000, 1000, 'Friendly', -3, 1, 3, 7, 1, '2026-01-05T08:00:00', '2026-01-05T08:00:00'),
        -- Firm: 7 days after due
        (5001, 1000, 'Firm', 7, 2, 5, 7, 1, '2026-01-05T08:00:00', '2026-01-05T08:00:00'),
        -- Formal: 21 days after due
        (5002, 1000, 'Formal', 21, 1, 7, 7, 1, '2026-01-05T08:00:00', '2026-01-05T08:00:00');
END

SET IDENTITY_INSERT [reminder].[PaymentReminderSchedule] OFF;
GO

-- ============================================================
-- SECTION 12: PAYMENT SCHEDULE — Invoice 1004 (3 instalments)
-- ============================================================
-- Invoice 1004: INV-2026-005, TotalAmount=€1,877.23, DueDate=2026-04-01

SET IDENTITY_INSERT [revenue].[PaymentSchedule] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentSchedule] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[PaymentSchedule]
        ([Id], [BusinessId], [InvoiceId], [IsActive], [CreatedAtUtc], [CreatedByUserId])
    VALUES
        (5000, 1000, 1004, 1, '2026-03-01T10:00:00', N'0650dc64-6615-4d84-947d-da783ed45160');
END

SET IDENTITY_INSERT [revenue].[PaymentSchedule] OFF;
GO

-- Payment Schedule Instalments (3)
SET IDENTITY_INSERT [revenue].[PaymentScheduleInstalment] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalment] WHERE [Id] = 5000)
BEGIN
    INSERT INTO [revenue].[PaymentScheduleInstalment]
        ([Id], [PaymentScheduleId], [SequenceNumber], [Amount], [MatchedAmount], [DueDate], [PaymentId], [ParentInstalmentId], [IsRemainder], [CreatedAtUtc])
    VALUES
        -- Instalment 1: €625.74 on 01-Apr-2026 — Paid
        (5000, 5000, 1, 625.74, 625.74, '2026-04-01', NULL, NULL, 0, '2026-03-01T10:00:00'),
        -- Instalment 2: €625.74 on 01-May-2026 — Due (partially matched = 0)
        (5001, 5000, 2, 625.74, 0.00, '2026-05-01', NULL, NULL, 0, '2026-03-01T10:00:00'),
        -- Instalment 3: €625.75 on 01-Jun-2026 — Pending
        (5002, 5000, 3, 625.75, 0.00, '2026-06-01', NULL, NULL, 0, '2026-03-01T10:00:00');
END

SET IDENTITY_INSERT [revenue].[PaymentScheduleInstalment] OFF;
GO

-- ============================================================
-- END OF NEW MODULES SEED SCRIPT
-- ============================================================
-- Summary:
--   Team Members:            2 (Ids 5000-5001)
--   Sales Products:          3 (Ids 5000-5002)
--   Sales Contacts:          4 (Ids 5000-5003)
--   Lead Requests:           5 (Ids 5000-5004)
--   Lead Response Templates: 1 (Id 5000)
--   Lead Responses:          2 (Ids 5000-5001)
--   Meetings:                1 (Id 5000)
--   Revenue Sources:         2 (Ids 5000-5001)
--   Revenue Summaries:       6 (Ids 5000-5005)
--   Payment Receipts:        2 (Ids 5000-5001)
--   Payment Receipt Lines:   2 (Ids 5000-5001)
--   Reminder Schedules:      3 (Ids 5000-5002)
--   Payment Schedule:        1 (Id 5000)
--   Schedule Instalments:    3 (Ids 5000-5002)
-- ============================================================
