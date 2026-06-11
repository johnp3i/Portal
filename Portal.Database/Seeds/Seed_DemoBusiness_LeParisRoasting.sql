-- ============================================================
-- DEMO BUSINESS SEED: Le Paris Roasting
-- ============================================================
-- Purpose: Seeds a complete demo business for platform demonstrations.
--          Includes business, profile, customers, suppliers, expense categories,
--          VAT periods, products, quotations, invoices, payments, purchases,
--          and spending limits.
--
-- Target Database: Portal (not Membership)
-- Business ID: 1000 (high ID to avoid conflicts)
--
-- IMPORTANT - User Registration:
--   The user CANNOT be created via raw SQL INSERT because ASP.NET Identity
--   uses password hashing via UserManager. To create the demo user:
--
--   1. Register through the application (Register page) with:
--      - Email: demo@leparis.com
--      - Password: Demo_2026!
--      - First Name: Marie
--      - Last Name: Dupont
--
--   2. SuperAdmin must approve the registration
--
--   3. After approval, run SECTION 11 below (UserBusiness + Permissions)
--      replacing the placeholder @UserId with the actual GUID from AspNetUsers
--
-- Run Order:
--   1. Run this entire script (Sections 1-10) against Portal DB
--   2. Register user via application UI
--   3. SuperAdmin approves user
--   4. Run Section 11 against Membership DB with real UserId
--
-- This script uses SET IDENTITY_INSERT ON/OFF with high IDs (1000+)
-- to avoid conflicts with existing data.
-- ============================================================

USE [Portal];
GO

-- ============================================================
-- SECTION 1: BUSINESS
-- ============================================================

SET IDENTITY_INSERT [portal].[Business] ON;

IF NOT EXISTS (SELECT 1 FROM [portal].[Business] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [portal].[Business] ([Id], [Name], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES (1000, N'Le Paris Roasting', 1, GETUTCDATE(), GETUTCDATE());
END

SET IDENTITY_INSERT [portal].[Business] OFF;
GO

-- ============================================================
-- SECTION 2: BUSINESS PROFILE
-- ============================================================

SET IDENTITY_INSERT [portal].[BusinessProfile] ON;

IF NOT EXISTS (SELECT 1 FROM [portal].[BusinessProfile] WHERE [BusinessId] = 1000)
BEGIN
    INSERT INTO [portal].[BusinessProfile]
        ([Id], [BusinessId], [CompanyRegistrationNumber], [VatRegistrationNumber],
         [VatRegistrationDate], [VatPeriodLengthInMonths],
         [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
         [TelephoneNumber], [MobileNumber], [Email])
    VALUES
        (1000, 1000, N'HE 456789', N'CY12345678X',
         '2024-09-01', 3,
         N'42 Makarios Avenue', NULL, N'Nicosia', N'1065', N'Cyprus',
         N'+357 22 123456', NULL, N'info@leparisroasting.com');
END

SET IDENTITY_INSERT [portal].[BusinessProfile] OFF;
GO

-- ============================================================
-- SECTION 3: CUSTOMERS (8)
-- ============================================================

SET IDENTITY_INSERT [customer].[Customer] ON;

IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [customer].[Customer]
        ([Id], [BusinessId], [Name], [Email], [TelephoneNumber], [AddressLine1], [City], [PostalCode], [Country], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        (1000, 1000, N'Café Lumière',         N'orders@cafelumiere.cy',      N'+357 22 200100', N'15 Ledra Street',         N'Nicosia',  N'1011', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1001, 1000, N'Hotel Alexandros',     N'procurement@alexandros.cy',  N'+357 22 200200', N'8 Archbishop Makarios Ave', N'Nicosia', N'1065', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1002, 1000, N'Sunrise Bakery',       N'info@sunrisebakery.cy',      N'+357 22 200300', N'22 Stasinou Avenue',      N'Nicosia',  N'1060', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1003, 1000, N'Mediterranean Deli',   N'orders@meddeli.cy',          N'+357 22 200400', N'5 Onasagorou Street',     N'Nicosia',  N'1010', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1004, 1000, N'The Green Terrace',    N'manager@greenterrace.cy',    N'+357 22 200500', N'90 Grivas Digenis Ave',   N'Nicosia',  N'1080', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1005, 1000, N'Nicosia Coffee Club',  N'hello@nicosiacoffee.cy',     N'+357 22 200600', N'3 Themistokli Dervi',     N'Nicosia',  N'1066', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1006, 1000, N'Larnaca Beach Resort', N'fb@larnacabeach.cy',         N'+357 24 200700', N'Dhekelia Road',           N'Larnaca',  N'6305', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE()),
        (1007, 1000, N'Paphos Wine Bar',      N'contact@paphoswinebar.cy',   N'+357 26 200800', N'12 Apostolou Pavlou',     N'Paphos',   N'8046', N'Cyprus', 1, GETUTCDATE(), GETUTCDATE());
END

SET IDENTITY_INSERT [customer].[Customer] OFF;
GO

-- ============================================================
-- SECTION 4: SUPPLIERS (8)
-- ============================================================

SET IDENTITY_INSERT [purchase].[Supplier] ON;

IF NOT EXISTS (SELECT 1 FROM [purchase].[Supplier] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [purchase].[Supplier] ([Id], [BusinessId], [Name], [IsActive], [CreatedAtUtc])
    VALUES
        (1000, 1000, N'Green Bean Importers Ltd',  1, GETUTCDATE()),
        (1001, 1000, N'Dairy Fresh Cyprus',        1, GETUTCDATE()),
        (1002, 1000, N'Sweet Harvest Trading',     1, GETUTCDATE()),
        (1003, 1000, N'ProPack Solutions',         1, GETUTCDATE()),
        (1004, 1000, N'EuroCup Distributors',      1, GETUTCDATE()),
        (1005, 1000, N'CoolTech Appliances',       1, GETUTCDATE()),
        (1006, 1000, N'CleanPro Services',         1, GETUTCDATE()),
        (1007, 1000, N'TransMed Logistics',        1, GETUTCDATE());
END

SET IDENTITY_INSERT [purchase].[Supplier] OFF;
GO

-- ============================================================
-- SECTION 5: EXPENSE CATEGORIES (7)
-- ============================================================

SET IDENTITY_INSERT [purchase].[ExpenseCategory] ON;

IF NOT EXISTS (SELECT 1 FROM [purchase].[ExpenseCategory] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [purchase].[ExpenseCategory] ([Id], [BusinessId], [Name], [IsActive], [ExpenseTypeId], [CreatedAtUtc])
    VALUES
        (1000, 1000, N'Raw Materials (Coffee Beans)',  1, 2, GETUTCDATE()),  -- Goods
        (1001, 1000, N'Packaging & Supplies',          1, 2, GETUTCDATE()),  -- Goods
        (1002, 1000, N'Equipment & Maintenance',       1, 2, GETUTCDATE()),  -- Goods
        (1003, 1000, N'Utilities',                     1, 1, GETUTCDATE()),  -- Services
        (1004, 1000, N'Marketing & Events',            1, 1, GETUTCDATE()),  -- Services
        (1005, 1000, N'Transportation & Delivery',     1, 1, GETUTCDATE()),  -- Services
        (1006, 1000, N'Office & Admin',                1, 1, GETUTCDATE());  -- Services
END

SET IDENTITY_INSERT [purchase].[ExpenseCategory] OFF;
GO

-- ============================================================
-- SECTION 6: VAT SUBMISSION PERIODS (Sep 2025 - Aug 2026)
-- ============================================================

SET IDENTITY_INSERT [vat].[VatSubmissionPeriod] ON;

IF NOT EXISTS (SELECT 1 FROM [vat].[VatSubmissionPeriod] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [vat].[VatSubmissionPeriod] ([Id], [BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc])
    VALUES
        (1000, 1000, '2025-09-01', '2025-11-30', N'01 Sep 2025 – 30 Nov 2025', GETUTCDATE()),
        (1001, 1000, '2025-12-01', '2026-02-28', N'01 Dec 2025 – 28 Feb 2026', GETUTCDATE()),
        (1002, 1000, '2026-03-01', '2026-05-31', N'01 Mar 2026 – 31 May 2026', GETUTCDATE()),
        (1003, 1000, '2026-06-01', '2026-08-31', N'01 Jun 2026 – 31 Aug 2026', GETUTCDATE());
END

SET IDENTITY_INSERT [vat].[VatSubmissionPeriod] OFF;
GO

-- ============================================================
-- SECTION 7: LINE ITEM CATALOG / PRODUCTS (6)
-- ============================================================

SET IDENTITY_INSERT [quotation].[LineItemCatalog] ON;

IF NOT EXISTS (SELECT 1 FROM [quotation].[LineItemCatalog] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [quotation].[LineItemCatalog]
        ([Id], [BusinessId], [Description], [UnitPrice], [VatRate], [ReferenceUrl], [Discount], [DiscountType], [UpdatedAtUtc])
    VALUES
        (1000, 1000, N'Premium Arabica Blend (1kg)',       18.50, 19.00, NULL, 0, N'Percentage', GETUTCDATE()),
        (1001, 1000, N'House Espresso Roast (500g)',       12.00, 19.00, NULL, 0, N'Percentage', GETUTCDATE()),
        (1002, 1000, N'Cold Brew Concentrate (1L)',         8.50, 19.00, NULL, 0, N'Percentage', GETUTCDATE()),
        (1003, 1000, N'Barista Training Session (2hrs)',  120.00, 19.00, NULL, 0, N'Percentage', GETUTCDATE()),
        (1004, 1000, N'Equipment Calibration Service',     75.00, 19.00, NULL, 0, N'Percentage', GETUTCDATE()),
        (1005, 1000, N'Custom Roasting (per kg)',          22.00, 19.00, NULL, 0, N'Percentage', GETUTCDATE());
END

SET IDENTITY_INSERT [quotation].[LineItemCatalog] OFF;
GO

-- ============================================================
-- SECTION 8: QUOTATIONS (4) + QUOTATION LINES
-- ============================================================

SET IDENTITY_INSERT [quotation].[Quotation] ON;

IF NOT EXISTS (SELECT 1 FROM [quotation].[Quotation] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [quotation].[Quotation]
        ([Id], [BusinessId], [CustomerId], [QuotationStatusTypeId], [Reference], [ValidUntil], [Subtotal], [TaxAmount], [TotalAmount], [Notes], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        -- Q-2026-001: Café Lumière, Sent (2), lines: 370+180+170=720, VAT 19%=136.80, Total=856.80
        (1000, 1000, 1000, 2, N'Q-2026-001', '2026-02-28', 720.00, 136.80, 856.80, N'Monthly coffee supply proposal', GETUTCDATE(), GETUTCDATE()),
        -- Q-2026-002: Hotel Alexandros, Accepted (3), lines: 1110+480+255+240=2085, VAT=396.15, Total=2481.15
        (1001, 1000, 1001, 3, N'Q-2026-002', '2026-03-15', 2085.00, 396.15, 2481.15, N'Quarterly bulk order with training', GETUTCDATE(), GETUTCDATE()),
        -- Q-2026-003: Sunrise Bakery, Draft (1), lines: 370+154=524, VAT=99.56, Total=623.56
        (1002, 1000, 1002, 1, N'Q-2026-003', NULL, 524.00, 99.56, 623.56, NULL, GETUTCDATE(), GETUTCDATE()),
        -- Q-2026-004: The Green Terrace, Converted (4), lines: 555+255+150=960, VAT=182.40, Total=1142.40
        (1003, 1000, 1004, 4, N'Q-2026-004', '2026-02-15', 960.00, 182.40, 1142.40, N'Premium blend supply + calibration', GETUTCDATE(), GETUTCDATE());
END

SET IDENTITY_INSERT [quotation].[Quotation] OFF;
GO

-- Quotation Lines
SET IDENTITY_INSERT [quotation].[QuotationLine] ON;

IF NOT EXISTS (SELECT 1 FROM [quotation].[QuotationLine] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [quotation].[QuotationLine]
        ([Id], [QuotationId], [Description], [Quantity], [UnitPrice], [LineTotal], [SortOrder], [VatRate], [Discount], [DiscountType])
    VALUES
        -- Q-2026-001 lines (3 lines, total ~€850)
        (1000, 1000, N'Premium Arabica Blend (1kg)',    20.0000, 18.50, 370.00, 1, 19.00, 0, N'Percentage'),
        (1001, 1000, N'House Espresso Roast (500g)',    15.0000, 12.00, 180.00, 2, 19.00, 0, N'Percentage'),
        (1002, 1000, N'Cold Brew Concentrate (1L)',     20.0000, 8.50,  170.00, 3, 19.00, 0, N'Percentage'),

        -- Q-2026-002 lines (4 lines, total ~€2,400)
        (1003, 1001, N'Premium Arabica Blend (1kg)',    60.0000, 18.50, 1110.00, 1, 19.00, 0, N'Percentage'),
        (1004, 1001, N'House Espresso Roast (500g)',    40.0000, 12.00, 480.00,  2, 19.00, 0, N'Percentage'),
        (1005, 1001, N'Cold Brew Concentrate (1L)',     30.0000, 8.50,  255.00,  3, 19.00, 0, N'Percentage'),
        (1006, 1001, N'Barista Training Session (2hrs)', 2.0000, 120.00, 240.00, 4, 19.00, 0, N'Percentage'),

        -- Q-2026-003 lines (2 lines, total ~€520)
        (1007, 1002, N'Premium Arabica Blend (1kg)',    20.0000, 18.50, 370.00, 1, 19.00, 0, N'Percentage'),
        (1008, 1002, N'Custom Roasting (per kg)',        7.0000, 22.00, 154.00, 2, 19.00, 0, N'Percentage'),

        -- Q-2026-004 lines (3 lines, total ~€1,100)
        (1009, 1003, N'Premium Arabica Blend (1kg)',    30.0000, 18.50, 555.00, 1, 19.00, 0, N'Percentage'),
        (1010, 1003, N'Cold Brew Concentrate (1L)',     30.0000, 8.50,  255.00, 2, 19.00, 0, N'Percentage'),
        (1011, 1003, N'Equipment Calibration Service',   2.0000, 75.00, 150.00, 3, 19.00, 0, N'Percentage');
END

SET IDENTITY_INSERT [quotation].[QuotationLine] OFF;
GO

-- ============================================================
-- SECTION 9: INVOICES (6) + INVOICE LINES
-- ============================================================

SET IDENTITY_INSERT [invoice].[Invoice] ON;

IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [invoice].[Invoice]
        ([Id], [BusinessId], [CustomerId], [QuotationId], [InvoiceStatusTypeId], [InvoiceFinancialStatusTypeId],
         [InvoiceNumber], [InvoiceDate], [DueDate], [Subtotal], [TaxAmount], [TotalAmount],
         [CurrencyCode], [Notes], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc])
    VALUES
        -- INV-2026-001: from Q-2026-004 conversion, Issued (2), Paid (3), lines=960, VAT=182.40, Total=1142.40
        (1000, 1000, 1004, 1003, 2, 3,
         N'INV-2026-001', '2026-01-15', '2026-02-15', 960.00, 182.40, 1142.40,
         N'EUR', N'Converted from Q-2026-004', 1001, GETUTCDATE(), GETUTCDATE()),

        -- INV-2026-002: Mediterranean Deli, Issued (2), Unpaid (1), lines=550, VAT=104.50, Total=654.50
        (1001, 1000, 1003, NULL, 2, 1,
         N'INV-2026-002', '2026-01-20', '2026-02-20', 550.00, 104.50, 654.50,
         N'EUR', NULL, 1001, GETUTCDATE(), GETUTCDATE()),

        -- INV-2026-003: Nicosia Coffee Club, Issued (2), Paid (3), lines=367.50, VAT=69.83, Total=437.33
        (1002, 1000, 1005, NULL, 2, 3,
         N'INV-2026-003', '2026-02-01', '2026-03-01', 367.50, 69.83, 437.33,
         N'EUR', NULL, 1001, GETUTCDATE(), GETUTCDATE()),

        -- INV-2026-004: Hotel Alexandros, Issued (2), PartiallyPaid (2), lines=2085, VAT=396.15, Total=2481.15
        (1003, 1000, 1001, NULL, 2, 2,
         N'INV-2026-004', '2026-02-10', '2026-03-10', 2085.00, 396.15, 2481.15,
         N'EUR', N'Bulk quarterly order', 1001, GETUTCDATE(), GETUTCDATE()),

        -- INV-2026-005: Larnaca Beach Resort, Draft (1), Unpaid (1), lines=1577.50, VAT=299.73, Total=1877.23
        (1004, 1000, 1006, NULL, 1, 1,
         N'INV-2026-005', '2026-03-01', '2026-04-01', 1577.50, 299.73, 1877.23,
         N'EUR', N'Summer supply pre-order', 1002, GETUTCDATE(), GETUTCDATE()),

        -- INV-2026-006: Paphos Wine Bar, Issued (2), Overdue (4), lines=273, VAT=51.87, Total=324.87
        (1005, 1000, 1007, NULL, 2, 4,
         N'INV-2026-006', '2025-12-10', '2026-01-10', 273.00, 51.87, 324.87,
         N'EUR', NULL, 1001, GETUTCDATE(), GETUTCDATE());
END

SET IDENTITY_INSERT [invoice].[Invoice] OFF;
GO

-- Invoice Lines
SET IDENTITY_INSERT [invoice].[InvoiceLine] ON;

IF NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [invoice].[InvoiceLine]
        ([Id], [InvoiceId], [Description], [Quantity], [UnitPrice], [LineTotal], [SortOrder], [VatRate], [Discount], [DiscountType])
    VALUES
        -- INV-2026-001 lines (from Q-2026-004, 3 lines)
        (1000, 1000, N'Premium Arabica Blend (1kg)',    30.0000, 18.50, 555.00, 1, 19.00, 0, N'Percentage'),
        (1001, 1000, N'Cold Brew Concentrate (1L)',     30.0000, 8.50,  255.00, 2, 19.00, 0, N'Percentage'),
        (1002, 1000, N'Equipment Calibration Service',   2.0000, 75.00, 150.00, 3, 19.00, 0, N'Percentage'),

        -- INV-2026-002 lines (2 lines, €680)
        (1003, 1001, N'Premium Arabica Blend (1kg)',    20.0000, 18.50, 370.00, 1, 19.00, 0, N'Percentage'),
        (1004, 1001, N'House Espresso Roast (500g)',    15.0000, 12.00, 180.00, 2, 19.00, 0, N'Percentage'),

        -- INV-2026-003 lines (2 lines, €450)
        (1005, 1002, N'House Espresso Roast (500g)',    20.0000, 12.00, 240.00, 1, 19.00, 0, N'Percentage'),
        (1006, 1002, N'Cold Brew Concentrate (1L)',     15.0000, 8.50,  127.50, 2, 19.00, 0, N'Percentage'),

        -- INV-2026-004 lines (4 lines, €2,400)
        (1007, 1003, N'Premium Arabica Blend (1kg)',    60.0000, 18.50, 1110.00, 1, 19.00, 0, N'Percentage'),
        (1008, 1003, N'House Espresso Roast (500g)',    40.0000, 12.00, 480.00,  2, 19.00, 0, N'Percentage'),
        (1009, 1003, N'Cold Brew Concentrate (1L)',     30.0000, 8.50,  255.00,  3, 19.00, 0, N'Percentage'),
        (1010, 1003, N'Barista Training Session (2hrs)', 2.0000, 120.00, 240.00, 4, 19.00, 0, N'Percentage'),

        -- INV-2026-005 lines (3 lines, €1,850)
        (1011, 1004, N'Premium Arabica Blend (1kg)',    50.0000, 18.50, 925.00,  1, 19.00, 0, N'Percentage'),
        (1012, 1004, N'Custom Roasting (per kg)',       20.0000, 22.00, 440.00,  2, 19.00, 0, N'Percentage'),
        (1013, 1004, N'Cold Brew Concentrate (1L)',     25.0000, 8.50,  212.50,  3, 19.00, 0, N'Percentage'),

        -- INV-2026-006 lines (2 lines, €320)
        (1014, 1005, N'House Espresso Roast (500g)',    10.0000, 12.00, 120.00, 1, 19.00, 0, N'Percentage'),
        (1015, 1005, N'Cold Brew Concentrate (1L)',     18.0000, 8.50,  153.00, 2, 19.00, 0, N'Percentage');
END

SET IDENTITY_INSERT [invoice].[InvoiceLine] OFF;
GO

-- ============================================================
-- SECTION 10: PAYMENTS (3)
-- ============================================================

SET IDENTITY_INSERT [revenue].[Payment] ON;

IF NOT EXISTS (SELECT 1 FROM [revenue].[Payment] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [revenue].[Payment]
        ([Id], [BusinessId], [InvoiceId], [PaymentMethodTypeId], [PaymentDateUtc], [Amount], [Reference], [Notes], [IsVoided], [CreatedByUserId])
    VALUES
        -- Full payment for INV-2026-001 (BankTransfer) - €1,142.40
        (1000, 1000, 1000, 2, '2026-01-20 10:30:00', 1142.40, N'TRF-2026-001', N'Full payment received', 0, N'system-seed'),
        -- Full payment for INV-2026-003 (Card) - €437.33
        (1001, 1000, 1002, 3, '2026-02-05 14:15:00', 437.33, N'CARD-2026-003', N'Card payment at delivery', 0, N'system-seed'),
        -- Partial payment for INV-2026-004 (BankTransfer) - €1,000 of €2,481.15
        (1002, 1000, 1003, 2, '2026-02-15 09:00:00', 1000.00, N'TRF-2026-004-P1', N'Partial payment - first instalment', 0, N'system-seed');
END

SET IDENTITY_INSERT [revenue].[Payment] OFF;
GO

-- ============================================================
-- SECTION 11: PURCHASES (18) - Spread across 2025-2026
-- ============================================================
-- PurchaseOriginTypeId: 1=Domestic, 2=EuReverseCharge, 3=NonEu, 4=EuPaid
-- PurchaseTypeId: 1=Asset, 2=Stock, 3=Expense
-- ExpenseCategory IDs: 1000=Raw Materials, 1001=Packaging, 1002=Equipment,
--                      1003=Utilities, 1004=Marketing, 1005=Transport, 1006=Office

INSERT INTO [purchase].[Purchase]
    ([BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
     [InvoiceNumber], [InvoiceDate], [Description], [AmountExcludingVat], [VatAmount], [TotalAmount],
     [Country], [Notes], [IsCancelled], [CancelledAtUtc], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc])
VALUES
-- ---- Period 1: Sep-Nov 2025 (VatSubmissionPeriodId = 1000) ----

-- Raw Materials (Coffee Beans)
(1000, 1000, 1000, 1, 3, N'GBI-2025-0891', '2025-09-10', N'Colombian Arabica Green Beans 50kg', 420.17, 79.83, 500.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),
(1000, 1000, 1000, 1, 3, N'GBI-2025-0923', '2025-10-05', N'Ethiopian Yirgacheffe 30kg', 336.13, 63.87, 400.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),
(1000, 1000, 1000, 1, 3, N'GBI-2025-0964', '2025-11-12', N'Brazilian Santos 40kg', 378.15, 71.85, 450.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),

-- Packaging
(1000, 1003, 1001, 1, 3, N'PP-2025-4421', '2025-09-20', N'Kraft bags 1kg x500', 168.07, 31.93, 200.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),
(1000, 1004, 1001, 1, 3, N'EC-2025-1102', '2025-10-25', N'Takeaway cups 12oz x1000', 126.05, 23.95, 150.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),

-- Equipment (Asset)
(1000, 1005, 1002, 1, 1, N'CT-2025-0088', '2025-09-05', N'Commercial Grinder - Mazzer Major', 504.20, 95.80, 600.00, NULL, N'3-year warranty', 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),

-- Marketing
(1000, 1000, 1004, 1, 3, NULL, '2025-10-15', N'Coffee Festival Stand Fee', 168.07, 31.93, 200.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),

-- Transport
(1000, 1007, 1005, 1, 3, N'TML-2025-3320', '2025-11-20', N'Monthly delivery service Nov', 126.05, 23.95, 150.00, NULL, NULL, 0, NULL, 1000, GETUTCDATE(), GETUTCDATE()),

-- ---- Period 2: Dec 2025 - Feb 2026 (VatSubmissionPeriodId = 1001) ----

-- Raw Materials
(1000, 1000, 1000, 1, 3, N'GBI-2025-1045', '2025-12-08', N'Kenyan AA 25kg', 294.12, 55.88, 350.00, NULL, NULL, 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),
(1000, 1000, 1000, 2, 3, N'RC-EU-2026-011', '2026-01-15', N'Specialty Gesha Beans 10kg (EU Import)', 520.00, 0.00, 520.00, N'Netherlands', N'EU Reverse Charge - Dutch roaster', 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),
(1000, 1000, 1000, 1, 3, N'GBI-2026-0012', '2026-02-10', N'Guatemalan Antigua 35kg', 352.94, 67.06, 420.00, NULL, NULL, 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),

-- Packaging
(1000, 1003, 1001, 1, 3, N'PP-2026-4501', '2026-01-08', N'Valve bags 500g x800', 210.08, 39.92, 250.00, NULL, NULL, 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),

-- Utilities
(1000, 1006, 1003, 1, 3, NULL, '2025-12-20', N'Roasting facility electricity Dec', 184.87, 35.13, 220.00, NULL, NULL, 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),

-- Marketing
(1000, 1000, 1004, 3, 3, NULL, '2026-02-05', N'Instagram Ads Campaign (Meta USA)', 180.00, 0.00, 180.00, N'USA', N'Non-EU digital advertising', 0, NULL, 1001, GETUTCDATE(), GETUTCDATE()),

-- ---- Period 3: Mar-May 2026 (VatSubmissionPeriodId = 1002) ----

-- Raw Materials
(1000, 1000, 1000, 1, 3, N'GBI-2026-0078', '2026-03-12', N'Costa Rican Tarrazu 30kg', 336.13, 63.87, 400.00, NULL, NULL, 0, NULL, 1002, GETUTCDATE(), GETUTCDATE()),
(1000, 1000, 1000, 1, 3, N'GBI-2026-0115', '2026-04-20', N'Indian Monsooned Malabar 25kg', 294.12, 55.88, 350.00, NULL, NULL, 0, NULL, 1002, GETUTCDATE(), GETUTCDATE()),

-- Office
(1000, 1006, 1006, 1, 3, NULL, '2026-03-25', N'Office cleaning supplies Q2', 67.23, 12.77, 80.00, NULL, NULL, 0, NULL, 1002, GETUTCDATE(), GETUTCDATE()),

-- Equipment (maintenance)
(1000, 1005, 1002, 1, 3, N'CT-2026-0201', '2026-05-10', N'Grinder calibration + burr replacement', 168.07, 31.93, 200.00, NULL, NULL, 0, NULL, 1002, GETUTCDATE(), GETUTCDATE());
GO

-- ============================================================
-- SECTION 12: EXPENSE CATEGORY LIMITS (4)
-- ============================================================

SET IDENTITY_INSERT [purchase].[ExpenseCategoryLimit] ON;

IF NOT EXISTS (SELECT 1 FROM [purchase].[ExpenseCategoryLimit] WHERE [Id] = 1000)
BEGIN
    INSERT INTO [purchase].[ExpenseCategoryLimit]
        ([Id], [BusinessId], [ExpenseCategoryId], [AnnualLimitEur], [PeriodLimitEur], [CreatedAtUtc])
    VALUES
        -- Raw Materials: annual €6,000, period €2,000
        (1000, 1000, 1000, 6000.00, 2000.00, GETUTCDATE()),
        -- Packaging: annual €1,500, no period limit
        (1001, 1000, 1001, 1500.00, NULL, GETUTCDATE()),
        -- Equipment: annual €3,000, no period limit
        (1002, 1000, 1002, 3000.00, NULL, GETUTCDATE()),
        -- Marketing: annual €1,000, period €400
        (1003, 1000, 1004, 1000.00, 400.00, GETUTCDATE());
END

SET IDENTITY_INSERT [purchase].[ExpenseCategoryLimit] OFF;
GO

-- ============================================================
-- SECTION 13: USER BUSINESS + PERMISSIONS
-- ============================================================
-- ⚠️  THIS SECTION MUST BE RUN AGAINST THE MEMBERSHIP DATABASE
--     AFTER the user registers and is approved by SuperAdmin.
--
-- Steps:
--   1. Register user at: /Account/Register
--      - Email: demo@leparis.com
--      - Password: Demo_2026!
--      - First Name: Marie
--      - Last Name: Dupont
--
--   2. SuperAdmin approves the registration
--
--   3. Find the UserId GUID:
--      SELECT [Id] FROM [dbo].[AspNetUsers] WHERE [Email] = 'demo@leparis.com';
--
--   4. Replace '<USER_ID_GUID>' below with the actual value and run against Membership DB
--
-- ============================================================

/*
-- ============================================================
-- RUN THIS AGAINST THE MEMBERSHIP DATABASE AFTER USER REGISTRATION
-- ============================================================

DECLARE @UserId NVARCHAR(450) = '<USER_ID_GUID>';  -- Replace with actual GUID
DECLARE @BusinessId INT = 1000;

-- Create UserBusiness mapping
IF NOT EXISTS (
    SELECT 1 FROM [membership].[UserBusiness]
    WHERE [UserId] = @UserId AND [BusinessId] = @BusinessId
)
BEGIN
    INSERT INTO [membership].[UserBusiness] ([UserId], [BusinessId], [IsDefault], [IsActive], [IsOwner], [CreatedAtUtc])
    VALUES (@UserId, @BusinessId, 1, 1, 1, GETUTCDATE());
END

-- Get the UserBusinessId
DECLARE @UserBusinessId INT;
SELECT @UserBusinessId = [Id]
FROM [membership].[UserBusiness]
WHERE [UserId] = @UserId AND [BusinessId] = @BusinessId;

-- Grant full permissions on all modules
INSERT INTO [membership].[UserBusinessPermission] ([UserBusinessId], [Module], [AccessLevel], [IsActive], [CreatedAtUtc])
SELECT @UserBusinessId, Modules.[Module], N'full', 1, GETUTCDATE()
FROM (
    VALUES ('customer'), ('quotation'), ('invoice'), ('revenue'), ('purchase'), ('vat'), ('credit'), ('audit'), ('products')
) AS Modules([Module])
WHERE NOT EXISTS (
    SELECT 1
    FROM [membership].[UserBusinessPermission]
    WHERE [membership].[UserBusinessPermission].[UserBusinessId] = @UserBusinessId
      AND [membership].[UserBusinessPermission].[Module] = Modules.[Module]
);
*/

-- ============================================================
-- END OF SEED SCRIPT
-- ============================================================
-- Summary:
--   Business:           Le Paris Roasting (Id=1000)
--   Customers:          8 (Ids 1000-1007)
--   Suppliers:          8 (Ids 1000-1007)
--   Expense Categories: 7 (Ids 1000-1006)
--   VAT Periods:        4 (Ids 1000-1003, Sep 2025 - Aug 2026)
--   Products (Catalog): 6 (Ids 1000-1005)
--   Quotations:         4 (Ids 1000-1003)
--   Quotation Lines:    12 (Ids 1000-1011)
--   Invoices:           6 (Ids 1000-1005)
--   Invoice Lines:      16 (Ids 1000-1015)
--   Payments:           3 (Ids 1000-1002)
--   Purchases:          18
--   Spending Limits:    4 (Ids 1000-1003)
-- ============================================================
