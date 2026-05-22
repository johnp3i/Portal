/*
    Seed: All Invoices (1-88, with 64-A and 64-B)
    Description: Consolidated seed script for all 89 invoice records.
                 Creates customers, invoices, sections, and line items.
    
    Prerequisites:
      - A Business record must exist (BusinessId = 1 assumed)
      - InvoiceStatusType seed data must exist (1=Draft, 2=Issued, 3=Cancelled)
      - InvoiceFinancialStatusType seed data must exist (1=Unpaid)
      - All migrations up to 043 must have been applied

    This script is idempotent â€” checks before inserting.

    Invoice Status Mapping:
      - All invoices: InvoiceStatusTypeId = 2 (Issued)
      - All invoices: InvoiceFinancialStatusTypeId = 3 (Paid)

    Duplicate Invoice Number:
      - Invoice 64-A: MOTOYARD LTD Equipment
      - Invoice 64-B: PEO Maintenance Subscription
*/

-- =============================================================================
-- SECTION 1: ENSURE ALL CUSTOMERS EXIST
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'Pancyprian Federation of Labor (PEO)' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'Pancyprian Federation of Labor (PEO)', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'Lilan Cafenis Ltd' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [City], [Country], [IsActive]) VALUES (1, N'Lilan Cafenis Ltd', N'3A Kantaras', N'Pera Chorio', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'Orestis Craft Center Cyprus Ltd' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [City], [PostalCode], [Country], [IsActive]) VALUES (1, N'Orestis Craft Center Cyprus Ltd', N'10 Kennedy Avenue', N'Nicosia', N'1087', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'Hatlo Trading Ltd' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [ContactPerson], [AddressLine1], [AddressLine2], [City], [PostalCode], [Country], [IsActive]) VALUES (1, N'Hatlo Trading Ltd', N'Route 66 Aglantzias', N'7 Ayias Paraskevis', N'Strovolos', N'Nicosia', N'2002', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO AM/STOU-LARNACAS LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'SYNTECHNIAKO FARMAKEIO AM/STOU-LARNACAS LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'FARMAKEIO SYNTECHNION AMMOCHOSTOU LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'FARMAKEIO SYNTECHNION AMMOCHOSTOU LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO PAFOU LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'SYNTECHNIAKO FARMAKEIO PAFOU LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'MA AHHA (CHRISTOFOROU) FOOD LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'MA AHHA (CHRISTOFOROU) FOOD LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'G.M. Andreou Marmi LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [ContactPerson], [AddressLine1], [City], [Country], [IsActive]) VALUES (1, N'G.M. Andreou Marmi LTD', N'Cafe Route Livadia', N'Andrea Zakou 5', N'Livadia Larnaka', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'SYNTECHNIAKO FARMAKEIO LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'FARMAKEIO TO LAIKO LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'FARMAKEIO TO LAIKO LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'L.P (Lefkara) Souvenir Shops' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'L.P (Lefkara) Souvenir Shops', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'MRS. CONSTANTINA PAPAIOANNOU & MRS. KALLIA DEMITRIOU' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'MRS. CONSTANTINA PAPAIOANNOU & MRS. KALLIA DEMITRIOU', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'G. PH. Ioannides LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'G. PH. Ioannides LTD', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'C.L.A. Familia Esperanza Ltd' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'C.L.A. Familia Esperanza Ltd', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'A.S ELECESSENTIALS LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [City], [PostalCode], [Country], [Email], [IsActive]) VALUES (1, N'A.S ELECESSENTIALS LTD', N'Lykavitou 79', N'Anayia, Nicosia', N'2640', N'Cyprus', N'info@elecessentials.com.cy', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'Chartopak Ltd' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'Chartopak Ltd', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'MOTOYARD LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [City], [PostalCode], [Country], [IsActive]) VALUES (1, N'MOTOYARD LTD', N'20 Ionos Street, 5th Floor', N'Nicosia', N'2406', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'OVIS ART LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [PostalCode], [Country], [IsActive]) VALUES (1, N'OVIS ART LTD', N'Viotechniki Periochi Dimou Aglantzias No.12', N'2103', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'INOUS MANAGEMENT LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [AddressLine1], [City], [PostalCode], [Country], [IsActive]) VALUES (1, N'INOUS MANAGEMENT LTD', N'Pentadaktylou 29', N'Geri, Nicosia', N'2200', N'Cyprus', 1);
GO
IF NOT EXISTS (SELECT 1 FROM [customer].[Customer] WHERE [Name] = N'ENOMENA FARMAKIA PEO LTD' AND [BusinessId] = 1)
    INSERT INTO [customer].[Customer] ([BusinessId], [Name], [IsActive]) VALUES (1, N'ENOMENA FARMAKIA PEO LTD', 1);
GO

-- =============================================================================
-- SECTION 2: INSERT ALL INVOICES (Header records only)
-- Uses a single batch with variables for customer lookups
-- =============================================================================

DECLARE @PEO INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'Pancyprian Federation of Labor (PEO)' AND [BusinessId] = 1);
DECLARE @Cafenis INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'Lilan Cafenis Ltd' AND [BusinessId] = 1);
DECLARE @OCC INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'Orestis Craft Center Cyprus Ltd' AND [BusinessId] = 1);
DECLARE @Hatlo INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'Hatlo Trading Ltd' AND [BusinessId] = 1);
DECLARE @PSLarnaca INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO AM/STOU-LARNACAS LTD' AND [BusinessId] = 1);
DECLARE @PSAmmohostos INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'FARMAKEIO SYNTECHNION AMMOCHOSTOU LTD' AND [BusinessId] = 1);
DECLARE @PSPafos INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO PAFOU LTD' AND [BusinessId] = 1);
DECLARE @SevenStars INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'MA AHHA (CHRISTOFOROU) FOOD LTD' AND [BusinessId] = 1);
DECLARE @Route66 INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'G.M. Andreou Marmi LTD' AND [BusinessId] = 1);
DECLARE @PSNicosia INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'SYNTECHNIAKO FARMAKEIO LTD' AND [BusinessId] = 1);
DECLARE @PSLemesos INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'FARMAKEIO TO LAIKO LTD' AND [BusinessId] = 1);
DECLARE @Lefkara INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'L.P (Lefkara) Souvenir Shops' AND [BusinessId] = 1);
DECLARE @MrsPap INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'MRS. CONSTANTINA PAPAIOANNOU & MRS. KALLIA DEMITRIOU' AND [BusinessId] = 1);
DECLARE @GPH INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'G. PH. Ioannides LTD' AND [BusinessId] = 1);
DECLARE @CLA INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'C.L.A. Familia Esperanza Ltd' AND [BusinessId] = 1);
DECLARE @Elec INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'A.S ELECESSENTIALS LTD' AND [BusinessId] = 1);
DECLARE @Chartopak INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'Chartopak Ltd' AND [BusinessId] = 1);
DECLARE @Motoyard INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'MOTOYARD LTD' AND [BusinessId] = 1);
DECLARE @OVIS INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'OVIS ART LTD' AND [BusinessId] = 1);
DECLARE @Inous INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'INOUS MANAGEMENT LTD' AND [BusinessId] = 1);
DECLARE @Enomena INT = (SELECT [Id] FROM [customer].[Customer] WHERE [Name] = N'ENOMENA FARMAKIA PEO LTD' AND [BusinessId] = 1);

-- INV 01: PEO Server Hosting Subscription (05/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00001' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PEO, NULL, 2, 3, N'INV-1-00001', '2023-05-05', '2023-06-04', 1700.00, 323.00, 2023.00, N'EUR', NULL, 1, 0, 0);

-- INV 02: Cafenis MyChair POS (15/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00002' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Cafenis, NULL, 2, 3, N'INV-1-00002', '2023-05-15', '2023-06-14', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 03: Cafenis Equipment (15/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00003' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Cafenis, NULL, 2, 3, N'INV-1-00003', '2023-05-15', '2023-06-14', 225.00, 42.75, 267.75, N'EUR', NULL, 1, 0, 0);

-- INV 04: OCC MyChair POS (15/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00004' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00004', '2023-05-15', '2023-06-14', 14100.00, 2679.00, 16779.00, N'EUR', NULL, 1, 1, 0);

-- INV 05: OCC Equipment (18/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00005' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00005', '2023-05-18', '2023-06-17', 1370.00, 260.30, 1630.30, N'EUR', NULL, 1, 1, 0);

-- INV 06: OCC Customer Display (26/05/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00006' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00006', '2023-05-26', '2023-06-25', 130.00, 24.70, 154.70, N'EUR', NULL, 1, 1, 0);

-- INV 07: Hatlo MyChair POS PDG (06/06/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00007' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00007', '2023-06-06', '2023-07-06', 400.00, 76.00, 476.00, N'EUR', NULL, 1, 0, 0);

-- INV 08: PharmaSyn Larnaca (11/07/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00008' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLarnaca, NULL, 2, 3, N'INV-1-00008', '2023-07-11', '2023-08-10', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 09: PharmaSyn Ammohostos (11/07/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00009' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSAmmohostos, NULL, 2, 3, N'INV-1-00009', '2023-07-11', '2023-08-10', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 10: PharmaSyn Pafos (11/07/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00010' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSPafos, NULL, 2, 3, N'INV-1-00010', '2023-07-11', '2023-08-10', 2000.00, 380.00, 2380.00, N'EUR', NULL, 1, 1, 0);

-- INV 11: 7 Stars Pizza (12/09/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00011' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @SevenStars, NULL, 2, 3, N'INV-1-00011', '2023-09-12', '2023-10-12', 1750.00, 332.50, 2082.50, N'EUR', NULL, 1, 1, 0);

-- INV 12: Route 66 Larnaca Equipment (12/09/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00012' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Route66, NULL, 2, 3, N'INV-1-00012', '2023-09-12', '2023-10-12', 70.00, 13.30, 83.30, N'EUR', NULL, 1, 0, 0);

-- INV 13: Route 66 Larnaca Equipment Corrected (09/11/2023)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00013' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Route66, NULL, 2, 3, N'INV-1-00013', '2023-11-09', '2023-12-09', 116.80, 22.20, 139.00, N'EUR', NULL, 1, 0, 0);

-- INV 14: OCC Data Migration Service (24/01/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00014' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00014', '2024-01-24', '2024-02-23', 420.17, 79.83, 500.00, N'EUR', NULL, 1, 0, 0);

-- INV 15: OCC ERP Vouchers Feature (21/02/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00015' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00015', '2024-02-21', '2024-03-22', 2400.00, 456.00, 2856.00, N'EUR', NULL, 1, 0, 0);

-- INV 16: OCC ERP Shopify Synchronizing Feature (21/02/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00016' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00016', '2024-02-21', '2024-03-22', 2400.00, 456.00, 2856.00, N'EUR', NULL, 1, 0, 0);

-- INV 17: PharmaSyn Syntehniakio Farmakio (20/03/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00017' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSNicosia, NULL, 2, 3, N'INV-1-00017', '2024-03-20', '2024-04-19', 750.00, 142.50, 892.50, N'EUR', NULL, 1, 1, 0);

-- INV 18: PharmaSyn To Laiko (20/03/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00018' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLemesos, NULL, 2, 3, N'INV-1-00018', '2024-03-20', '2024-04-19', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 19: OCC Stock Service Shopify Sync Feature (25/03/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00019' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00019', '2024-03-25', '2024-04-24', 1800.00, 342.00, 2142.00, N'EUR', NULL, 1, 0, 0);

-- INV 20: OCC PC Bundle Equipment (25/03/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00020' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00020', '2024-03-25', '2024-04-24', 266.00, 50.54, 316.54, N'EUR', NULL, 1, 0, 0);

-- INV 21: PharmaSyn Larnaca (10/04/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00021' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLarnaca, NULL, 2, 3, N'INV-1-00021', '2024-04-10', '2024-05-10', 2000.00, 380.00, 2380.00, N'EUR', NULL, 1, 1, 0);

-- INV 22: PharmaSyn Pafos (10/04/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00022' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSPafos, NULL, 2, 3, N'INV-1-00022', '2024-04-10', '2024-05-10', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 23: PharmaSyn Ammohostos (10/04/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00023' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSAmmohostos, NULL, 2, 3, N'INV-1-00023', '2024-04-10', '2024-05-10', 500.00, 95.00, 595.00, N'EUR', NULL, 1, 1, 0);

-- INV 24: PharmaSyn Syntehniakio Farmakio (10/04/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00024' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSNicosia, NULL, 2, 3, N'INV-1-00024', '2024-04-10', '2024-05-10', 750.00, 142.50, 892.50, N'EUR', NULL, 1, 1, 0);

-- INV 25: L.P (Lefkara) Souvenir Shops (10/04/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00025' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Lefkara, NULL, 2, 3, N'INV-1-00025', '2024-04-10', '2024-05-10', 699.50, 132.91, 832.41, N'EUR', NULL, 1, 1, 0);

-- INV 26: PharmaSyn Lemesos (27/06/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00026' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLemesos, NULL, 2, 3, N'INV-1-00026', '2024-06-27', '2024-07-27', 2000.00, 380.00, 2380.00, N'EUR', NULL, 1, 1, 0);

-- INV 27: PharmaSyn Larnaca Maintenance (27/06/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00027' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLarnaca, NULL, 2, 3, N'INV-1-00027', '2024-06-27', '2024-07-27', 350.00, 66.50, 416.50, N'EUR', NULL, 1, 1, 0);

-- INV 28: PharmaSyn Pafos Maintenance (27/06/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00028' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSPafos, NULL, 2, 3, N'INV-1-00028', '2024-06-27', '2024-07-27', 1050.00, 199.50, 1249.50, N'EUR', NULL, 1, 1, 0);

-- INV 29: PharmaSyn Ammohostos (23/08/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00029' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSAmmohostos, NULL, 2, 3, N'INV-1-00029', '2024-08-23', '2024-09-22', 560.00, 106.40, 666.40, N'EUR', NULL, 1, 1, 0);

-- INV 30: PharmaSyn Syntehniakio Farmakio (23/09/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00030' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSNicosia, NULL, 2, 3, N'INV-1-00030', '2024-09-23', '2024-10-23', 1060.00, 201.40, 1261.40, N'EUR', NULL, 1, 1, 0);

-- INV 31: PharmaSyn Lemesos Maintenance (23/09/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00031' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSLemesos, NULL, 2, 3, N'INV-1-00031', '2024-09-23', '2024-10-23', 700.00, 133.00, 833.00, N'EUR', NULL, 1, 1, 0);

-- INV 32: PharmaSyn Ammohostos Maintenance (07/10/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00032' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSAmmohostos, NULL, 2, 3, N'INV-1-00032', '2024-10-07', '2024-11-06', 350.00, 66.50, 416.50, N'EUR', NULL, 1, 1, 0);

-- INV 33: Mrs Constantina & Mrs Kallia (18/10/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00033' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @MrsPap, NULL, 2, 3, N'INV-1-00033', '2024-10-18', '2024-11-17', 1300.00, 247.00, 1547.00, N'EUR', NULL, 1, 1, 0);

-- INV 34: PharmaSyn Syntehniakio Farmakio Maintenance (21/11/2024)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00034' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PSNicosia, NULL, 2, 3, N'INV-1-00034', '2024-11-21', '2024-12-21', 350.00, 66.50, 416.50, N'EUR', NULL, 1, 1, 0);

-- INV 35: G.PH. Ioannides LTD Order Processing System (24/01/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00035' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @GPH, NULL, 2, 3, N'INV-1-00035', '2025-01-24', '2025-02-23', 8000.00, 1520.00, 9520.00, N'EUR', NULL, 1, 1, 0);

-- INV 36: CLA Labels (11/03/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00036' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00036', '2025-03-11', '2025-04-10', 29.60, 5.62, 35.22, N'EUR', NULL, 1, 0, 0);

-- INV 37: CLA Labels (07/04/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00037' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00037', '2025-04-07', '2025-05-07', 29.60, 5.62, 35.22, N'EUR', NULL, 1, 0, 0);

-- INV 38: CLA Online Service (05/05/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00038' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00038', '2025-05-05', '2025-06-04', 132.34, 25.14, 157.48, N'EUR', NULL, 1, 0, 0);

-- INV 39: CLA IHM 04/2025 (22/05/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00039' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00039', '2025-05-22', '2025-06-21', 44.00, 8.36, 52.36, N'EUR', NULL, 1, 0, 0);

-- INV 40: CLA IHM 05/2025 (22/05/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00040' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00040', '2025-05-22', '2025-06-21', 44.00, 8.36, 52.36, N'EUR', NULL, 1, 0, 0);

-- INV 41: CLA Labels (27/05/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00041' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00041', '2025-05-27', '2025-06-26', 29.60, 5.62, 35.22, N'EUR', NULL, 1, 0, 0);

-- INV 42: OCC MyChairPos Limassol Equipment (30/05/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00042' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00042', '2025-05-30', '2025-06-29', 1315.00, 249.85, 1564.85, N'EUR', NULL, 1, 1, 0);

-- INV 43: G.PH. Ioannides LTD Order Processing System Phase 2 (01/06/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00043' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @GPH, NULL, 2, 3, N'INV-1-00043', '2025-06-01', '2025-07-01', 18832.00, 3578.08, 22410.08, N'EUR', NULL, 1, 1, 0);

-- INV 44: CLA Online Service (05/06/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00044' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00044', '2025-06-05', '2025-07-05', 146.27, 27.79, 174.06, N'EUR', NULL, 1, 0, 0);

-- INV 45: OCC MyChairPos Limassol Equipment 2 (15/06/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00045' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00045', '2025-06-15', '2025-07-15', 101.00, 19.19, 120.19, N'EUR', NULL, 1, 1, 0);

-- INV 46: OCC MyChairPos Limassol Software (15/06/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00046' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00046', '2025-06-15', '2025-07-15', 6600.00, 1254.00, 7854.00, N'EUR', NULL, 1, 1, 0);

-- INV 47: OCC MyChairPos Limassol Support 10h (15/06/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00047' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00047', '2025-06-15', '2025-07-15', 500.00, 95.00, 595.00, N'EUR', NULL, 1, 1, 0);

-- INV 48: CLA IHM 06/2025 (01/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00048' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00048', '2025-07-01', '2025-07-31', 44.00, 8.36, 52.36, N'EUR', NULL, 1, 0, 0);

-- INV 49: CLA Online Service (01/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00049' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00049', '2025-07-01', '2025-07-31', 129.15, 24.54, 153.69, N'EUR', NULL, 1, 0, 0);

-- INV 50: ElecEssentials Domain (09/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00050' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Elec, NULL, 2, 3, N'INV-1-00050', '2025-07-09', '2025-08-08', 90.00, 17.10, 107.10, N'EUR', NULL, 1, 0, 0);

-- INV 51: OCC ERP Upgrade DN/CN (10/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00051' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00051', '2025-07-10', '2025-08-09', 1700.00, 323.00, 2023.00, N'EUR', NULL, 1, 0, 0);

-- INV 52: CLA Labels (13/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00052' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00052', '2025-07-13', '2025-08-12', 14.80, 2.81, 17.61, N'EUR', NULL, 1, 0, 0);

-- INV 53: OCC MyChairPos Limassol Equipment 3 (19/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00053' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00053', '2025-07-19', '2025-08-18', 500.00, 95.00, 595.00, N'EUR', NULL, 1, 1, 0);

-- INV 54: G.PH. Ioannides LTD Equipment (29/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00054' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @GPH, NULL, 2, 3, N'INV-1-00054', '2025-07-29', '2025-08-28', 450.00, 85.50, 535.50, N'EUR', NULL, 1, 1, 0);

-- INV 55: CLA Labels (29/07/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00055' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00055', '2025-07-29', '2025-08-28', 22.20, 4.22, 26.42, N'EUR', NULL, 1, 0, 0);

-- INV 56: CLA Online Service (06/08/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00056' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00056', '2025-08-06', '2025-09-05', 107.98, 20.52, 128.50, N'EUR', NULL, 1, 0, 0);

-- INV 57: G.PH. Ioannides LTD Equipment Printers (06/08/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00057' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @GPH, NULL, 2, 3, N'INV-1-00057', '2025-08-06', '2025-09-05', 460.00, 87.40, 547.40, N'EUR', NULL, 1, 1, 0);

-- INV 58: CLA Online Service (01/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00058' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00058', '2025-09-01', '2025-10-01', 82.56, 15.69, 98.25, N'EUR', NULL, 1, 0, 0);

-- INV 59: Hatlo POS Equipment (05/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00059' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00059', '2025-09-05', '2025-10-05', 425.00, 80.75, 505.75, N'EUR', NULL, 1, 0, 0);

-- INV 60: OCC Tablet Go 10 (05/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00060' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00060', '2025-09-05', '2025-10-05', 540.00, 102.60, 642.60, N'EUR', NULL, 1, 0, 0);

-- INV 61: Chartopak Equipment Printers (12/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00061' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Chartopak, NULL, 2, 3, N'INV-1-00061', '2025-09-12', '2025-10-12', 240.00, 45.60, 285.60, N'EUR', NULL, 1, 0, 0);

-- INV 62: Chartopak Labels (12/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00062' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Chartopak, NULL, 2, 3, N'INV-1-00062', '2025-09-12', '2025-10-12', 14.80, 2.81, 17.61, N'EUR', NULL, 1, 0, 0);

-- INV 63: Motoyard MyChair POS (16/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00063' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Motoyard, NULL, 2, 3, N'INV-1-00063', '2025-09-16', '2025-10-16', 1700.00, 323.00, 2023.00, N'EUR', NULL, 1, 1, 0);

-- INV 64-A: Motoyard Equipment (16/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00064-A' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Motoyard, NULL, 2, 3, N'INV-1-00064-A', '2025-09-16', '2025-10-16', 745.00, 141.55, 886.55, N'EUR', NULL, 1, 1, 0);

-- INV 64-B: PEO Maintenance Subscription (22/09/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00064-B' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PEO, NULL, 2, 3, N'INV-1-00064-B', '2025-09-22', '2025-10-22', 1500.00, 285.00, 1785.00, N'EUR', NULL, 1, 1, 0);

-- INV 65: OVIS Equipment (02/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00065' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OVIS, NULL, 2, 3, N'INV-1-00065', '2025-10-02', '2025-11-01', 605.00, 114.95, 719.95, N'EUR', NULL, 1, 0, 0);

-- INV 66: CLA Online Service (02/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00066' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00066', '2025-10-02', '2025-11-01', 94.58, 17.97, 112.55, N'EUR', NULL, 1, 0, 0);

-- INV 67: Hatlo Kitchen Printer (03/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00067' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00067', '2025-10-03', '2025-11-02', 165.00, 31.35, 196.35, N'EUR', NULL, 1, 0, 0);

-- INV 68: Inous Management MyChair POS (10/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00068' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Inous, NULL, 2, 3, N'INV-1-00068', '2025-10-10', '2025-11-09', 1300.00, 247.00, 1547.00, N'EUR', NULL, 1, 1, 0);

-- INV 69: Inous Management Equipment (10/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00069' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Inous, NULL, 2, 3, N'INV-1-00069', '2025-10-10', '2025-11-09', 723.00, 137.37, 860.37, N'EUR', NULL, 1, 1, 0);

-- INV 70: OCC JDS Equipment (13/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00070' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00070', '2025-10-13', '2025-11-12', 510.00, 96.90, 606.90, N'EUR', NULL, 1, 0, 0);

-- INV 71: Hatlo POS Equipment (13/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00071' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00071', '2025-10-13', '2025-11-12', 395.00, 75.05, 470.05, N'EUR', NULL, 1, 0, 0);

-- INV 72: Hatlo Labels (17/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00072' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00072', '2025-10-17', '2025-11-16', 56.00, 10.64, 66.64, N'EUR', NULL, 1, 0, 0);

-- INV 73: Hatlo Label Printer (22/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00073' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00073', '2025-10-22', '2025-11-21', 220.00, 41.80, 261.80, N'EUR', NULL, 1, 0, 0);

-- INV 74: PharmaSyn Maintenance 2nd Year (31/10/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00074' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Enomena, NULL, 2, 3, N'INV-1-00074', '2025-10-31', '2025-11-30', 1800.00, 342.00, 2142.00, N'EUR', NULL, 1, 0, 0);

-- INV 75: CLA Online Service (02/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00075' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @CLA, NULL, 2, 3, N'INV-1-00075', '2025-11-02', '2025-12-02', 176.87, 33.60, 210.47, N'EUR', NULL, 1, 0, 0);

-- INV 76: Hatlo Labels (02/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00076' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00076', '2025-11-02', '2025-12-02', 56.00, 10.64, 66.64, N'EUR', NULL, 1, 0, 0);

-- INV 77: Hatlo Bartender License (07/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00077' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00077', '2025-11-07', '2025-12-07', 264.00, 50.16, 314.16, N'EUR', NULL, 1, 0, 0);

-- INV 78: OCC Bartender License (07/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00078' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00078', '2025-11-07', '2025-12-07', 1044.00, 198.36, 1242.36, N'EUR', NULL, 1, 0, 0);

-- INV 79: OCC SMS Service (07/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00079' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00079', '2025-11-07', '2025-12-07', 78.00, 14.82, 92.82, N'EUR', NULL, 1, 0, 0);

-- INV 80: OCC JDS (07/11/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00080' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00080', '2025-11-07', '2025-12-07', 8400.00, 1596.00, 9996.00, N'EUR', NULL, 1, 0, 0);

-- INV 81: Hatlo Labels (01/12/2025)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00081' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00081', '2025-12-01', '2025-12-31', 56.00, 10.64, 66.64, N'EUR', NULL, 1, 0, 0);

-- INV 82: Hatlo Labels (22/01/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00082' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00082', '2026-01-22', '2026-02-21', 81.00, 15.39, 96.39, N'EUR', NULL, 1, 0, 0);

-- INV 83: Hatlo Labels (06/02/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00083' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00083', '2026-02-06', '2026-03-08', 60.00, 11.40, 71.40, N'EUR', NULL, 1, 0, 0);

-- INV 84: G.PH. Ioannides LTD Logitech ConferenceCam (13/02/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00084' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @GPH, NULL, 2, 3, N'INV-1-00084', '2026-02-13', '2026-03-15', 180.00, 34.20, 214.20, N'EUR', NULL, 1, 0, 0);

-- INV 85: PEO Domain (17/02/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00085' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @PEO, NULL, 2, 3, N'INV-1-00085', '2026-02-17', '2026-03-19', 45.00, 8.55, 53.55, N'EUR', NULL, 1, 0, 0);

-- INV 86: OCC JDS Feature & Support (03/03/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00086' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @OCC, NULL, 2, 3, N'INV-1-00086', '2026-03-03', '2026-04-02', 1200.00, 228.00, 1428.00, N'EUR', NULL, 1, 0, 0);

-- INV 87: Hatlo Labels (19/03/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00087' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00087', '2026-03-19', '2026-04-18', 60.00, 11.40, 71.40, N'EUR', NULL, 1, 0, 0);

-- INV 88: Hatlo Labels & Device (20/04/2026)
IF NOT EXISTS (SELECT 1 FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00088' AND [BusinessId] = 1)
    INSERT INTO [invoice].[Invoice] ([BusinessId],[CustomerId],[QuotationId],[InvoiceStatusTypeId],[InvoiceFinancialStatusTypeId],[InvoiceNumber],[InvoiceDate],[DueDate],[Subtotal],[TaxAmount],[TotalAmount],[CurrencyCode],[Notes],[IsGrandTotalShown],[IsQuotationReferenceShown],[IsDeleted])
    VALUES (1, @Hatlo, NULL, 2, 3, N'INV-1-00088', '2026-04-20', '2026-05-20', 105.00, 19.95, 124.95, N'EUR', NULL, 1, 0, 0);
GO

-- =============================================================================
-- SECTION 3: INSERT INVOICE SECTIONS (one "General" section per invoice)
-- =============================================================================

DECLARE @InvId INT;

-- Helper: Insert a "General" section for each invoice that doesn't already have one
DECLARE @InvNumbers TABLE (InvoiceNumber NVARCHAR(50));
INSERT INTO @InvNumbers VALUES
    (N'INV-1-00001'),(N'INV-1-00002'),(N'INV-1-00003'),(N'INV-1-00004'),(N'INV-1-00005'),
    (N'INV-1-00006'),(N'INV-1-00007'),(N'INV-1-00008'),(N'INV-1-00009'),(N'INV-1-00010'),
    (N'INV-1-00011'),(N'INV-1-00012'),(N'INV-1-00013'),(N'INV-1-00014'),(N'INV-1-00015'),
    (N'INV-1-00016'),(N'INV-1-00017'),(N'INV-1-00018'),(N'INV-1-00019'),(N'INV-1-00020'),
    (N'INV-1-00021'),(N'INV-1-00022'),(N'INV-1-00023'),(N'INV-1-00024'),(N'INV-1-00025'),
    (N'INV-1-00026'),(N'INV-1-00027'),(N'INV-1-00028'),(N'INV-1-00029'),(N'INV-1-00030'),
    (N'INV-1-00031'),(N'INV-1-00032'),(N'INV-1-00033'),(N'INV-1-00034'),(N'INV-1-00035'),
    (N'INV-1-00036'),(N'INV-1-00037'),(N'INV-1-00038'),(N'INV-1-00039'),(N'INV-1-00040'),
    (N'INV-1-00041'),(N'INV-1-00042'),(N'INV-1-00043'),(N'INV-1-00044'),(N'INV-1-00045'),
    (N'INV-1-00046'),(N'INV-1-00047'),(N'INV-1-00048'),(N'INV-1-00049'),(N'INV-1-00050'),
    (N'INV-1-00051'),(N'INV-1-00052'),(N'INV-1-00053'),(N'INV-1-00054'),(N'INV-1-00055'),
    (N'INV-1-00056'),(N'INV-1-00057'),(N'INV-1-00058'),(N'INV-1-00059'),(N'INV-1-00060'),
    (N'INV-1-00061'),(N'INV-1-00062'),(N'INV-1-00063'),(N'INV-1-00064-A'),(N'INV-1-00064-B'),
    (N'INV-1-00065'),(N'INV-1-00066'),(N'INV-1-00067'),(N'INV-1-00068'),(N'INV-1-00069'),
    (N'INV-1-00070'),(N'INV-1-00071'),(N'INV-1-00072'),(N'INV-1-00073'),(N'INV-1-00074'),
    (N'INV-1-00075'),(N'INV-1-00076'),(N'INV-1-00077'),(N'INV-1-00078'),(N'INV-1-00079'),
    (N'INV-1-00080'),(N'INV-1-00081'),(N'INV-1-00082'),(N'INV-1-00083'),(N'INV-1-00084'),
    (N'INV-1-00085'),(N'INV-1-00086'),(N'INV-1-00087'),(N'INV-1-00088');

INSERT INTO [invoice].[InvoiceSection] ([InvoiceId], [Name], [SortOrder], [ColumnConfiguration], [SectionType], [IsTotalsTableShown])
SELECT Invoice.Id, N'General', 1, N'OneTime', N'LineItems', 0
FROM [invoice].[Invoice] Invoice
INNER JOIN @InvNumbers InvNum ON Invoice.InvoiceNumber = InvNum.InvoiceNumber
WHERE Invoice.BusinessId = 1
  AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = Invoice.Id);
GO

-- =============================================================================
-- SECTION 4: INSERT INVOICE LINES
-- Each block inserts lines for one invoice if none exist yet.
-- All discounts are type 'Fixed' (absolute amount).
-- VAT rate is 19% for all lines.
-- =============================================================================

-- INV 01 Lines
DECLARE @Inv01 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00001' AND [BusinessId] = 1);
DECLARE @Sec01 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv01 AND [Name] = N'General');
IF @Inv01 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv01)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv01, N'Basic Hosting: Private dedicated server, hosting. Providing a secure safe, and redundant environment with 5GB of data. 12 months subscription', 1.0000, 1600.00, 800.00, 1, 19.00, 800.00, N'Fixed', @Sec01),
    (@Inv01, N'Maintenance, Backups & Support: Monitoring website health and performance providing a secure environment, disaster recovery plan and perform up to date updates. 12 months subscription', 1.0000, 1800.00, 900.00, 2, 19.00, 900.00, N'Fixed', @Sec01);
END
GO

-- INV 02 Lines
DECLARE @Inv02 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00002' AND [BusinessId] = 1);
DECLARE @Sec02 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv02 AND [Name] = N'General');
IF @Inv02 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv02)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv02, N'POS & POS Admin Software', 1.0000, 1600.00, 1300.00, 1, 19.00, 300.00, N'Fixed', @Sec02),
    (@Inv02, N'POS Trust Discount', 1.0000, 0.00, -400.00, 2, 19.00, 400.00, N'Fixed', @Sec02),
    (@Inv02, N'Data Migration', 1.0000, 250.00, 200.00, 3, 19.00, 50.00, N'Fixed', @Sec02),
    (@Inv02, N'POS Software PDG Edition (POS Device Group)', 1.0000, 600.00, 400.00, 4, 19.00, 200.00, N'Fixed', @Sec02),
    (@Inv02, N'MyChair POS Manager online application (monitoring sales) yearly subscription', 2.0000, 220.00, 0.00, 5, 19.00, 440.00, N'Fixed', @Sec02);
END
GO

-- INV 03 Lines
DECLARE @Inv03 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00003' AND [BusinessId] = 1);
DECLARE @Sec03 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv03 AND [Name] = N'General');
IF @Inv03 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv03)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv03, N'XPOS THERMAL PRINTER 80mm WITH CUTTER USB/ETHERNET NIB - 2 Years Warranty', 1.0000, 85.00, 85.00, 1, 19.00, 0.00, N'Fixed', @Sec03),
    (@Inv03, N'HP PC DESKTOP TINY EliteDesk 800 G3 Mini I5-6500T 8GB-DDR4 (8GB, 120GB SSD) Certified Refurbished - 2 Years Warranty', 1.0000, 140.00, 140.00, 2, 19.00, 0.00, N'Fixed', @Sec03);
END
GO

-- INV 04 Lines
DECLARE @Inv04 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00004' AND [BusinessId] = 1);
DECLARE @Sec04 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv04 AND [Name] = N'General');
IF @Inv04 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv04)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv04, N'POS & POS Admin Software (with 1-year support license)', 1.0000, 1600.00, 1300.00, 1, 19.00, 300.00, N'Fixed', @Sec04),
    (@Inv04, N'POS Trust Discount', 1.0000, 0.00, -300.00, 2, 19.00, 300.00, N'Fixed', @Sec04),
    (@Inv04, N'Data Migration', 1.0000, 450.00, 350.00, 3, 19.00, 100.00, N'Fixed', @Sec04),
    (@Inv04, N'Label Printing Server License', 1.0000, 290.00, 290.00, 4, 19.00, 0.00, N'Fixed', @Sec04),
    (@Inv04, N'POS & POS Admin Updates', 1.0000, 5400.00, 4860.00, 5, 19.00, 540.00, N'Fixed', @Sec04),
    (@Inv04, N'Shopify Integration', 1.0000, 2400.00, 2400.00, 6, 19.00, 0.00, N'Fixed', @Sec04),
    (@Inv04, N'Courier Integration', 1.0000, 2400.00, 2400.00, 7, 19.00, 0.00, N'Fixed', @Sec04),
    (@Inv04, N'JCC Checkout Integration', 1.0000, 800.00, 500.00, 8, 19.00, 300.00, N'Fixed', @Sec04),
    (@Inv04, N'POS Device Software', 3.0000, 500.00, 1200.00, 9, 19.00, 300.00, N'Fixed', @Sec04),
    (@Inv04, N'SMS Service Facility', 1.0000, 500.00, 400.00, 10, 19.00, 100.00, N'Fixed', @Sec04),
    (@Inv04, N'Orders Management System (OMS)', 1.0000, 900.00, 700.00, 11, 19.00, 200.00, N'Fixed', @Sec04);
END
GO

-- INV 05 Lines
DECLARE @Inv05 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00005' AND [BusinessId] = 1);
DECLARE @Sec05 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv05 AND [Name] = N'General');
IF @Inv05 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv05)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv05, N'Computer Devices: Dell Optiplex Tiny (Refurbished Grade A+) 9020m, i5-4570T, 8GB Ram, 120GB SSD (2 years warranty)', 3.0000, 155.00, 465.00, 1, 19.00, 0.00, N'Fixed', @Sec05),
    (@Inv05, N'Touch Screen Monitors: XPOS 17 inches VGA Screen (2 years warranty)', 3.0000, 210.00, 630.00, 2, 19.00, 0.00, N'Fixed', @Sec05),
    (@Inv05, N'POS Device 3: Toshiba WT310-10U + Industrial Case (Refurbished)', 1.0000, 280.00, 0.00, 3, 19.00, 280.00, N'Fixed', @Sec05),
    (@Inv05, N'POS Device 3 Heavy Duty Metallic Custom Base', 1.0000, 85.00, 85.00, 4, 19.00, 0.00, N'Fixed', @Sec05),
    (@Inv05, N'Label Printer: Xprinter XP-420B support 10cmx10cm labels', 1.0000, 190.00, 190.00, 5, 19.00, 0.00, N'Fixed', @Sec05);
END
GO

-- INV 06 Lines
DECLARE @Inv06 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00006' AND [BusinessId] = 1);
DECLARE @Sec06 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv06 AND [Name] = N'General');
IF @Inv06 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv06)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv06, N'Customer Display: XPOS 2 Line, USB VFD Display (1 year warranty)', 2.0000, 65.00, 130.00, 1, 19.00, 0.00, N'Fixed', @Sec06);
END
GO

-- INV 07 Lines
DECLARE @Inv07 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00007' AND [BusinessId] = 1);
DECLARE @Sec07 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv07 AND [Name] = N'General');
IF @Inv07 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv07)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv07, N'POS Software PDG Edition (POS Device Group)', 1.0000, 600.00, 400.00, 1, 19.00, 200.00, N'Fixed', @Sec07);
END
GO

-- INV 08 Lines
DECLARE @Inv08 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00008' AND [BusinessId] = 1);
DECLARE @Sec08 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv08 AND [Name] = N'General');
IF @Inv08 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv08)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv08, N'Loyalty Application Platform -- Implementation Service', 1.0000, 1500.00, 1500.00, 1, 19.00, 0.00, N'Fixed', @Sec08);
END
GO

-- INV 09 Lines
DECLARE @Inv09 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00009' AND [BusinessId] = 1);
DECLARE @Sec09 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv09 AND [Name] = N'General');
IF @Inv09 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv09)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv09, N'Loyalty Application Platform -- Implementation Service', 1.0000, 1500.00, 1500.00, 1, 19.00, 0.00, N'Fixed', @Sec09);
END
GO

-- INV 10 Lines
DECLARE @Inv10 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00010' AND [BusinessId] = 1);
DECLARE @Sec10 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv10 AND [Name] = N'General');
IF @Inv10 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv10)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv10, N'Loyalty Application Platform -- Implementation Service', 1.0000, 2000.00, 2000.00, 1, 19.00, 0.00, N'Fixed', @Sec10);
END
GO

-- INV 11 Lines (Cancelled)
DECLARE @Inv11 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00011' AND [BusinessId] = 1);
DECLARE @Sec11 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv11 AND [Name] = N'General');
IF @Inv11 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv11)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv11, N'Master POS (Server) & POS Admin Software', 1.0000, 1600.00, 1300.00, 1, 19.00, 300.00, N'Fixed', @Sec11),
    (@Inv11, N'Second POS Software PDG Edition (POS Device Group)', 1.0000, 850.00, 850.00, 2, 19.00, 0.00, N'Fixed', @Sec11),
    (@Inv11, N'Data Migration', 1.0000, 250.00, 0.00, 3, 19.00, 250.00, N'Fixed', @Sec11),
    (@Inv11, N'Computer device preparation and installation', 2.0000, 100.00, 0.00, 4, 19.00, 200.00, N'Fixed', @Sec11),
    (@Inv11, N'MyChair POS Manager online application (Yearly subscription/device)', 2.0000, 220.00, 0.00, 5, 19.00, 440.00, N'Fixed', @Sec11),
    (@Inv11, N'POS Trust Discount', 1.0000, 0.00, -400.00, 6, 19.00, 400.00, N'Fixed', @Sec11);
END
GO

-- INV 12 Lines
DECLARE @Inv12 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00012' AND [BusinessId] = 1);
DECLARE @Sec12 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv12 AND [Name] = N'General');
IF @Inv12 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv12)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv12, N'XPOS Metallic Cash Drawer - Colour: Black - 1 Year Warranty', 1.0000, 70.00, 70.00, 1, 19.00, 0.00, N'Fixed', @Sec12);
END
GO

-- INV 13 Lines
DECLARE @Inv13 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00013' AND [BusinessId] = 1);
DECLARE @Sec13 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv13 AND [Name] = N'General');
IF @Inv13 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv13)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv13, N'X-Printer POS Thermal Printer XP-Q200II USB', 1.0000, 130.00, 116.80, 1, 19.00, 13.20, N'Fixed', @Sec13);
END
GO

-- INV 14 Lines
DECLARE @Inv14 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00014' AND [BusinessId] = 1);
DECLARE @Sec14 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv14 AND [Name] = N'General');
IF @Inv14 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv14)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv14, N'Data Migration Services', 9.0000, 50.00, 420.17, 1, 19.00, 29.83, N'Fixed', @Sec14);
END
GO

-- INV 15 Lines
DECLARE @Inv15 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00015' AND [BusinessId] = 1);
DECLARE @Sec15 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv15 AND [Name] = N'General');
IF @Inv15 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv15)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv15, N'MCP ERP -- Primewell Vouchers Feature: Synchronizing Shopify new products added', 40.0000, 60.00, 2400.00, 1, 19.00, 0.00, N'Fixed', @Sec15);
END
GO

-- INV 16 Lines
DECLARE @Inv16 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00016' AND [BusinessId] = 1);
DECLARE @Sec16 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv16 AND [Name] = N'General');
IF @Inv16 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv16)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv16, N'MCP ERP -- Shopify Synchronizing Feature', 40.0000, 60.00, 2400.00, 1, 19.00, 0.00, N'Fixed', @Sec16);
END
GO

-- INV 17 Lines
DECLARE @Inv17 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00017' AND [BusinessId] = 1);
DECLARE @Sec17 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv17 AND [Name] = N'General');
IF @Inv17 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv17)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv17, N'Loyalty Application Platform -- Implementation Service', 1.0000, 750.00, 750.00, 1, 19.00, 0.00, N'Fixed', @Sec17);
END
GO

-- INV 18 Lines
DECLARE @Inv18 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00018' AND [BusinessId] = 1);
DECLARE @Sec18 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv18 AND [Name] = N'General');
IF @Inv18 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv18)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv18, N'Loyalty Application Platform -- Implementation Service', 1.0000, 1500.00, 1500.00, 1, 19.00, 0.00, N'Fixed', @Sec18);
END
GO

-- INV 19 Lines
DECLARE @Inv19 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00019' AND [BusinessId] = 1);
DECLARE @Sec19 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv19 AND [Name] = N'General');
IF @Inv19 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv19)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv19, N'Stock Management Service: Shopify products, individual stock calculation based on supplies stock', 30.0000, 60.00, 1800.00, 1, 19.00, 0.00, N'Fixed', @Sec19);
END
GO

-- INV 20 Lines
DECLARE @Inv20 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00020' AND [BusinessId] = 1);
DECLARE @Sec20 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv20 AND [Name] = N'General');
IF @Inv20 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv20)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv20, N'Monitor: Dell Monitor 23" P2319 (Certified Refurbished) - 2 years warranty', 1.0000, 78.00, 78.00, 1, 19.00, 0.00, N'Fixed', @Sec20),
    (@Inv20, N'Computer Device: HP PC DESKTOP TINY 800 G3 Mini i5-6500T 4/32GB RAM GA+ 2YW (Certified Refurbished)', 1.0000, 158.00, 158.00, 2, 19.00, 0.00, N'Fixed', @Sec20),
    (@Inv20, N'Windows 10 Professional Installation', 1.0000, 30.00, 30.00, 3, 19.00, 0.00, N'Fixed', @Sec20);
END
GO

-- INV 21-34: Single-line PharmaSyn invoices and others
DECLARE @Inv21 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00021' AND [BusinessId] = 1);
DECLARE @Sec21 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv21 AND [Name] = N'General');
IF @Inv21 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv21)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv21, N'Loyalty Application Platform -- Implementation Service', 1.0000, 2000.00, 2000.00, 1, 19.00, 0.00, N'Fixed', @Sec21);
GO

DECLARE @Inv22 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00022' AND [BusinessId] = 1);
DECLARE @Sec22 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv22 AND [Name] = N'General');
IF @Inv22 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv22)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv22, N'Loyalty Application Platform -- Implementation Service', 1.0000, 1500.00, 1500.00, 1, 19.00, 0.00, N'Fixed', @Sec22);
GO

DECLARE @Inv23 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00023' AND [BusinessId] = 1);
DECLARE @Sec23 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv23 AND [Name] = N'General');
IF @Inv23 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv23)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv23, N'Loyalty Application Platform -- Implementation Service', 1.0000, 500.00, 500.00, 1, 19.00, 0.00, N'Fixed', @Sec23);
GO

DECLARE @Inv24 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00024' AND [BusinessId] = 1);
DECLARE @Sec24 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv24 AND [Name] = N'General');
IF @Inv24 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv24)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv24, N'Loyalty Application Platform -- Implementation Service', 1.0000, 750.00, 750.00, 1, 19.00, 0.00, N'Fixed', @Sec24);
GO

-- INV 25 Lines (Lefkara - 6 items)
DECLARE @Inv25 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00025' AND [BusinessId] = 1);
DECLARE @Sec25 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv25 AND [Name] = N'General');
IF @Inv25 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv25)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv25, N'HP -- Tiny 800 G4, i5-8500T, SSD: 240GB, RAM: 8GB, Windows 10 (Refurbished - 2 years warranty)', 1.0000, 220.00, 220.00, 1, 19.00, 0.00, N'Fixed', @Sec25),
    (@Inv25, N'XPOS -- Touchscreen USB Monitor 17" (2 years warranty)', 1.0000, 240.00, 240.00, 2, 19.00, 0.00, N'Fixed', @Sec25),
    (@Inv25, N'XPOS -- Thermal Receipt Printer USB/Ethernet (2 years warranty)', 1.0000, 100.00, 100.00, 3, 19.00, 0.00, N'Fixed', @Sec25),
    (@Inv25, N'XPOS -- Cash Drawer (2 years warranty)', 1.0000, 58.00, 58.00, 4, 19.00, 0.00, N'Fixed', @Sec25),
    (@Inv25, N'XPOS -- Barcode Scanner 2D Wireless USB (2 years warranty)', 1.0000, 74.00, 74.00, 5, 19.00, 0.00, N'Fixed', @Sec25),
    (@Inv25, N'XPOS -- WIFI Dongle Adapter USB (2 years warranty)', 1.0000, 7.50, 7.50, 6, 19.00, 0.00, N'Fixed', @Sec25);
END
GO

DECLARE @Inv26 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00026' AND [BusinessId] = 1);
DECLARE @Sec26 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv26 AND [Name] = N'General');
IF @Inv26 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv26)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv26, N'Loyalty Application Platform -- Implementation Service', 1.0000, 2000.00, 2000.00, 1, 19.00, 0.00, N'Fixed', @Sec26);
GO

DECLARE @Inv27 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00027' AND [BusinessId] = 1);
DECLARE @Sec27 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv27 AND [Name] = N'General');
IF @Inv27 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv27)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv27, N'Loyalty Application Platform -- Maintenance Service', 1.0000, 350.00, 350.00, 1, 19.00, 0.00, N'Fixed', @Sec27);
GO

DECLARE @Inv28 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00028' AND [BusinessId] = 1);
DECLARE @Sec28 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv28 AND [Name] = N'General');
IF @Inv28 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv28)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv28, N'Loyalty Application Platform -- Maintenance Service', 1.0000, 1050.00, 1050.00, 1, 19.00, 0.00, N'Fixed', @Sec28);
GO

DECLARE @Inv29 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00029' AND [BusinessId] = 1);
DECLARE @Sec29 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv29 AND [Name] = N'General');
IF @Inv29 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv29)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv29, N'Loyalty Application Platform -- Implementation Service', 1.0000, 560.00, 560.00, 1, 19.00, 0.00, N'Fixed', @Sec29);
GO

DECLARE @Inv30 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00030' AND [BusinessId] = 1);
DECLARE @Sec30 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv30 AND [Name] = N'General');
IF @Inv30 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv30)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv30, N'Loyalty Application Platform -- Implementation Service', 1.0000, 1060.00, 1060.00, 1, 19.00, 0.00, N'Fixed', @Sec30);
GO

DECLARE @Inv31 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00031' AND [BusinessId] = 1);
DECLARE @Sec31 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv31 AND [Name] = N'General');
IF @Inv31 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv31)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv31, N'Loyalty Application Platform -- Maintenance Service', 1.0000, 700.00, 700.00, 1, 19.00, 0.00, N'Fixed', @Sec31);
GO

DECLARE @Inv32 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00032' AND [BusinessId] = 1);
DECLARE @Sec32 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv32 AND [Name] = N'General');
IF @Inv32 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv32)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv32, N'Loyalty Application Platform -- Maintenance Service', 1.0000, 350.00, 350.00, 1, 19.00, 0.00, N'Fixed', @Sec32);
GO

-- INV 33 Lines (Mrs Constantina)
DECLARE @Inv33 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00033' AND [BusinessId] = 1);
DECLARE @Sec33 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv33 AND [Name] = N'General');
IF @Inv33 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv33)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv33, N'Master POS (Server) & POS Admin Software', 1.0000, 1600.00, 1300.00, 1, 19.00, 300.00, N'Fixed', @Sec33),
    (@Inv33, N'MyChair POS Manager online application (Yearly subscription/device)', 2.0000, 220.00, 0.00, 2, 19.00, 440.00, N'Fixed', @Sec33);
END
GO

DECLARE @Inv34 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00034' AND [BusinessId] = 1);
DECLARE @Sec34 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv34 AND [Name] = N'General');
IF @Inv34 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv34)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv34, N'Loyalty Application Platform -- Maintenance Service', 1.0000, 350.00, 350.00, 1, 19.00, 0.00, N'Fixed', @Sec34);
GO

-- INV 35 Lines
DECLARE @Inv35 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00035' AND [BusinessId] = 1);
DECLARE @Sec35 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv35 AND [Name] = N'General');
IF @Inv35 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv35)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv35, N'Order Processing System -- Implementation Service', 1.0000, 8000.00, 8000.00, 1, 19.00, 0.00, N'Fixed', @Sec35);
GO

-- INV 36-37, 41, 52, 55 Lines (CLA Labels - same pattern)
DECLARE @Inv36 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00036' AND [BusinessId] = 1);
DECLARE @Sec36 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv36 AND [Name] = N'General');
IF @Inv36 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv36)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv36, N'Thermal label roll, glossy x1000', 4.0000, 7.40, 29.60, 1, 19.00, 0.00, N'Fixed', @Sec36);
GO

DECLARE @Inv37 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00037' AND [BusinessId] = 1);
DECLARE @Sec37 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv37 AND [Name] = N'General');
IF @Inv37 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv37)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv37, N'Thermal label roll, glossy x1000', 4.0000, 7.40, 29.60, 1, 19.00, 0.00, N'Fixed', @Sec37);
GO

-- INV 38 Lines (CLA Online Service)
DECLARE @Inv38 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00038' AND [BusinessId] = 1);
DECLARE @Sec38 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv38 AND [Name] = N'General');
IF @Inv38 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv38)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv38, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/04/2025 - 30/04/2025', 1.0000, 132.34, 132.34, 1, 19.00, 0.00, N'Fixed', @Sec38);
GO

-- INV 39 Lines (CLA IHM)
DECLARE @Inv39 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00039' AND [BusinessId] = 1);
DECLARE @Sec39 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv39 AND [Name] = N'General');
IF @Inv39 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv39)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv39, N'IHM Monthly Subscription Service Period: 01/04/2025 - 30/04/2025', 1.0000, 44.00, 44.00, 1, 19.00, 0.00, N'Fixed', @Sec39);
GO

-- INV 40 Lines (CLA IHM)
DECLARE @Inv40 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00040' AND [BusinessId] = 1);
DECLARE @Sec40 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv40 AND [Name] = N'General');
IF @Inv40 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv40)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv40, N'IHM Monthly Subscription Service Period: 01/05/2025 - 31/05/2025', 1.0000, 44.00, 44.00, 1, 19.00, 0.00, N'Fixed', @Sec40);
GO

DECLARE @Inv41 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00041' AND [BusinessId] = 1);
DECLARE @Sec41 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv41 AND [Name] = N'General');
IF @Inv41 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv41)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv41, N'Thermal label roll, glossy x1000', 4.0000, 7.40, 29.60, 1, 19.00, 0.00, N'Fixed', @Sec41);
GO

-- INV 42 Lines (OCC Limassol Equipment)
DECLARE @Inv42 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00042' AND [BusinessId] = 1);
DECLARE @Sec42 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv42 AND [Name] = N'General');
IF @Inv42 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv42)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv42, N'HP PC DESKTOP TINY 800 G6 Mini i5-10500T, RAM: 16GB, SSD: 480GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 410.00, 410.00, 1, 19.00, 0.00, N'Fixed', @Sec42),
    (@Inv42, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 240GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 2.0000, 180.00, 360.00, 2, 19.00, 0.00, N'Fixed', @Sec42),
    (@Inv42, N'Customer Display: XPOS 2 Line, USB VFD Display (1 year warranty)', 1.0000, 65.00, 65.00, 3, 19.00, 0.00, N'Fixed', @Sec42),
    (@Inv42, N'Cash Drawer -- Metallic Black (1 year warranty)', 1.0000, 85.00, 85.00, 4, 19.00, 0.00, N'Fixed', @Sec42),
    (@Inv42, N'Thermal Printer: Xprinter XP-T80Q Receipt/Bill Printer (2-year warranty)', 1.0000, 155.00, 155.00, 5, 19.00, 0.00, N'Fixed', @Sec42);
END
GO

-- INV 43 Lines
DECLARE @Inv43 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00043' AND [BusinessId] = 1);
DECLARE @Sec43 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv43 AND [Name] = N'General');
IF @Inv43 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv43)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv43, N'Order Processing System -- Implementation Service', 1.0000, 18832.00, 18832.00, 1, 19.00, 0.00, N'Fixed', @Sec43);
GO

-- INV 44 Lines
DECLARE @Inv44 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00044' AND [BusinessId] = 1);
DECLARE @Sec44 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv44 AND [Name] = N'General');
IF @Inv44 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv44)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv44, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/05/2025 - 31/05/2025', 1.0000, 146.27, 146.27, 1, 19.00, 0.00, N'Fixed', @Sec44);
GO

-- INV 45 Lines
DECLARE @Inv45 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00045' AND [BusinessId] = 1);
DECLARE @Sec45 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv45 AND [Name] = N'General');
IF @Inv45 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv45)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv45, N'Wireless Keyboard & Mouse', 3.0000, 18.00, 54.00, 1, 19.00, 0.00, N'Fixed', @Sec45),
    (@Inv45, N'TP-Link 8-port Gigabit Switch, Network Cables, Power Cables & Adapters', 1.0000, 47.00, 47.00, 2, 19.00, 0.00, N'Fixed', @Sec45);
END
GO

-- INV 46 Lines (OCC Limassol Software)
DECLARE @Inv46 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00046' AND [BusinessId] = 1);
DECLARE @Sec46 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv46 AND [Name] = N'General');
IF @Inv46 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv46)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv46, N'MyChair POS Software', 1.0000, 1700.00, 1400.00, 1, 19.00, 300.00, N'Fixed', @Sec46),
    (@Inv46, N'MyChair ERP Software', 1.0000, 4200.00, 3400.00, 2, 19.00, 800.00, N'Fixed', @Sec46),
    (@Inv46, N'POS Device Software', 1.0000, 1100.00, 900.00, 3, 19.00, 200.00, N'Fixed', @Sec46),
    (@Inv46, N'Data Migration (Optional)', 1.0000, 300.00, 300.00, 4, 19.00, 0.00, N'Fixed', @Sec46),
    (@Inv46, N'JCC Checkout', 2.0000, 300.00, 600.00, 5, 19.00, 0.00, N'Fixed', @Sec46),
    (@Inv46, N'Setup & Installation', 1.0000, 200.00, 0.00, 6, 19.00, 200.00, N'Fixed', @Sec46);
END
GO

-- INV 47 Lines
DECLARE @Inv47 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00047' AND [BusinessId] = 1);
DECLARE @Sec47 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv47 AND [Name] = N'General');
IF @Inv47 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv47)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv47, N'10 Hours Remote & Telephone Support (End-Date: 01/07/2026)', 10.0000, 50.00, 500.00, 1, 19.00, 0.00, N'Fixed', @Sec47);
GO

-- INV 48 Lines
DECLARE @Inv48 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00048' AND [BusinessId] = 1);
DECLARE @Sec48 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv48 AND [Name] = N'General');
IF @Inv48 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv48)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv48, N'IHM Monthly Subscription Service Period: 01/06/2025 - 30/06/2025', 1.0000, 44.00, 44.00, 1, 19.00, 0.00, N'Fixed', @Sec48);
GO

-- INV 49 Lines
DECLARE @Inv49 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00049' AND [BusinessId] = 1);
DECLARE @Sec49 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv49 AND [Name] = N'General');
IF @Inv49 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv49)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv49, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/06/2025 - 30/06/2025', 1.0000, 129.15, 129.15, 1, 19.00, 0.00, N'Fixed', @Sec49);
GO

-- INV 50 Lines
DECLARE @Inv50 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00050' AND [BusinessId] = 1);
DECLARE @Sec50 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv50 AND [Name] = N'General');
IF @Inv50 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv50)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv50, N'Domain purchase elecessentials.com.cy (Expired on 05/05/2030)', 5.0000, 12.00, 60.00, 1, 19.00, 0.00, N'Fixed', @Sec50),
    (@Inv50, N'Domain DNS Hosting - Yearly Charged (Expired on 15/05/2026)', 1.0000, 30.00, 30.00, 2, 19.00, 0.00, N'Fixed', @Sec50);
END
GO

-- INV 51 Lines
DECLARE @Inv51 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00051' AND [BusinessId] = 1);
DECLARE @Sec51 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv51 AND [Name] = N'General');
IF @Inv51 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv51)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv51, N'Development and Implementation of Delivery Notes Feature for ERP System', 1.0000, 1300.00, 1300.00, 1, 19.00, 0.00, N'Fixed', @Sec51),
    (@Inv51, N'Development and Integration of Credit Notes Feature for ERP System', 1.0000, 400.00, 400.00, 2, 19.00, 0.00, N'Fixed', @Sec51);
END
GO

-- INV 52 Lines
DECLARE @Inv52 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00052' AND [BusinessId] = 1);
DECLARE @Sec52 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv52 AND [Name] = N'General');
IF @Inv52 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv52)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv52, N'Thermal label roll, glossy x1000', 2.0000, 7.40, 14.80, 1, 19.00, 0.00, N'Fixed', @Sec52);
GO

-- INV 53 Lines
DECLARE @Inv53 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00053' AND [BusinessId] = 1);
DECLARE @Sec53 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv53 AND [Name] = N'General');
IF @Inv53 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv53)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv53, N'Touch Screen Monitors: XPOS 17" VGA Screen (2-year warranty)', 2.0000, 250.00, 500.00, 1, 19.00, 0.00, N'Fixed', @Sec53);
GO

-- INV 54 Lines
DECLARE @Inv54 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00054' AND [BusinessId] = 1);
DECLARE @Sec54 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv54 AND [Name] = N'General');
IF @Inv54 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv54)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv54, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 120GB, Windows 10 Professional (Certified Refurbished, 2 years warranty)', 1.0000, 190.00, 190.00, 1, 19.00, 0.00, N'Fixed', @Sec54),
    (@Inv54, N'Touch Screen Monitors: XPOS 17" VGA & HDMI Screen (2 years warranty)', 1.0000, 260.00, 260.00, 2, 19.00, 0.00, N'Fixed', @Sec54);
END
GO

-- INV 55 Lines
DECLARE @Inv55 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00055' AND [BusinessId] = 1);
DECLARE @Sec55 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv55 AND [Name] = N'General');
IF @Inv55 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv55)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv55, N'Thermal label roll, glossy x1000', 3.0000, 7.40, 22.20, 1, 19.00, 0.00, N'Fixed', @Sec55);
GO

-- INV 56 Lines
DECLARE @Inv56 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00056' AND [BusinessId] = 1);
DECLARE @Sec56 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv56 AND [Name] = N'General');
IF @Inv56 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv56)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv56, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/07/2025 - 31/07/2025', 1.0000, 107.98, 107.98, 1, 19.00, 0.00, N'Fixed', @Sec56);
GO

-- INV 57 Lines
DECLARE @Inv57 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00057' AND [BusinessId] = 1);
DECLARE @Sec57 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv57 AND [Name] = N'General');
IF @Inv57 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv57)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv57, N'XP-410B LAN/USB Direct Thermal Label Printer (Max print width: 104mm, 203 dpi, 32-bit, 8MB SDRAM)', 2.0000, 240.00, 460.00, 1, 19.00, 20.00, N'Fixed', @Sec57);
GO

-- INV 58 Lines
DECLARE @Inv58 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00058' AND [BusinessId] = 1);
DECLARE @Sec58 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv58 AND [Name] = N'General');
IF @Inv58 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv58)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv58, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/08/2025 - 31/08/2025', 1.0000, 82.56, 82.56, 1, 19.00, 0.00, N'Fixed', @Sec58);
GO

-- INV 59 Lines
DECLARE @Inv59 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00059' AND [BusinessId] = 1);
DECLARE @Sec59 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv59 AND [Name] = N'General');
IF @Inv59 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv59)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv59, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 120GB, Windows 10 Professional, Wireless Antenna (Certified Refurbished, 2-year warranty)', 1.0000, 205.00, 205.00, 1, 19.00, 0.00, N'Fixed', @Sec59),
    (@Inv59, N'Touch Screen Monitor - Nixdorf BA91W 10.1" (Certified Refurbished, 2-year warranty) DVI-to-HDMI Cable', 1.0000, 155.00, 155.00, 2, 19.00, 0.00, N'Fixed', @Sec59),
    (@Inv59, N'Presitgio Node A8 - Android Tablet 8" RAM: 1GB, Memory: 32GB, WI-FI & 3G Support', 1.0000, 65.00, 65.00, 3, 19.00, 0.00, N'Fixed', @Sec59);
END
GO

-- INV 60 Lines
DECLARE @Inv60 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00060' AND [BusinessId] = 1);
DECLARE @Sec60 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv60 AND [Name] = N'General');
IF @Inv60 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv60)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv60, N'HP Engage Go 10 Touch Tablet Mobile System, 10" WUXGA, Core i5, RAM: 8GB, SSD: 128GB', 1.0000, 540.00, 540.00, 1, 19.00, 0.00, N'Fixed', @Sec60);
GO

-- INV 61 Lines
DECLARE @Inv61 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00061' AND [BusinessId] = 1);
DECLARE @Sec61 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv61 AND [Name] = N'General');
IF @Inv61 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv61)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv61, N'XP-350B USB Direct Thermal Label Printer (Max print width: 76mm, 203 dpi, 4MB DRAM)', 1.0000, 210.00, 190.00, 1, 19.00, 20.00, N'Fixed', @Sec61),
    (@Inv61, N'Labels Bartender Lite Software Setup', 1.0000, 50.00, 50.00, 2, 19.00, 0.00, N'Fixed', @Sec61);
END
GO

-- INV 62 Lines
DECLARE @Inv62 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00062' AND [BusinessId] = 1);
DECLARE @Sec62 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv62 AND [Name] = N'General');
IF @Inv62 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv62)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv62, N'Thermal label roll, glossy x1000', 2.0000, 7.40, 14.80, 1, 19.00, 0.00, N'Fixed', @Sec62);
GO

-- INV 63 Lines
DECLARE @Inv63 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00063' AND [BusinessId] = 1);
DECLARE @Sec63 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv63 AND [Name] = N'General');
IF @Inv63 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv63)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv63, N'Master POS (Server) & POS Admin Software', 1.0000, 1600.00, 1400.00, 1, 19.00, 200.00, N'Fixed', @Sec63),
    (@Inv63, N'JCC checkout', 1.0000, 300.00, 300.00, 2, 19.00, 0.00, N'Fixed', @Sec63),
    (@Inv63, N'Computer device preparation and installation', 1.0000, 100.00, 0.00, 3, 19.00, 100.00, N'Fixed', @Sec63),
    (@Inv63, N'MyChair POS Manager online application (Yearly subscription/device)', 1.0000, 220.00, 0.00, 4, 19.00, 220.00, N'Fixed', @Sec63);
END
GO

-- INV 64-A Lines (Motoyard Equipment)
DECLARE @Inv64A INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00064-A' AND [BusinessId] = 1);
DECLARE @Sec64A INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv64A AND [Name] = N'General');
IF @Inv64A IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv64A)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv64A, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 240GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 190.00, 190.00, 1, 19.00, 0.00, N'Fixed', @Sec64A),
    (@Inv64A, N'Touch Screen Monitors: 15-18" VGA/DVI Screen (2-year warranty)', 1.0000, 250.00, 250.00, 2, 19.00, 0.00, N'Fixed', @Sec64A),
    (@Inv64A, N'Customer Display: XPOS 2 Line, USB VFD Display (1 year warranty)', 1.0000, 65.00, 65.00, 3, 19.00, 0.00, N'Fixed', @Sec64A),
    (@Inv64A, N'Cash Drawer -- Metallic Black (1-year warranty)', 1.0000, 85.00, 85.00, 4, 19.00, 0.00, N'Fixed', @Sec64A),
    (@Inv64A, N'Thermal Printer: XPrinter XP-T80Q Receipt/Bill Printer (2-year warranty)', 1.0000, 155.00, 155.00, 5, 19.00, 0.00, N'Fixed', @Sec64A);
END
GO

-- INV 64-B Lines (PEO Maintenance)
DECLARE @Inv64B INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00064-B' AND [BusinessId] = 1);
DECLARE @Sec64B INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv64B AND [Name] = N'General');
IF @Inv64B IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv64B)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv64B, N'Maintenance, Backups & Support: Monitoring website health and performance, providing a secure environment and disaster recovery plan. 6 months subscription -- Ending on 03/02/2026', 1.0000, 1800.00, 1500.00, 1, 19.00, 300.00, N'Fixed', @Sec64B),
    (@Inv64B, N'DigiCert certificate', 1.0000, 40.00, 0.00, 2, 19.00, 40.00, N'Fixed', @Sec64B);
END
GO

-- INV 65 Lines (OVIS Equipment)
DECLARE @Inv65 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00065' AND [BusinessId] = 1);
DECLARE @Sec65 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv65 AND [Name] = N'General');
IF @Inv65 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv65)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv65, N'HP PC DESKTOP TINY 800 G6 Mini i5-10500T, RAM: 16GB, SSD: 480GB, Windows 11 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 418.00, 418.00, 1, 19.00, 0.00, N'Fixed', @Sec65),
    (@Inv65, N'LENOVO MONITOR 24" T24v (Certified Refurbished, 2-year warranty) Webcam & Frameless', 1.0000, 112.00, 112.00, 2, 19.00, 0.00, N'Fixed', @Sec65),
    (@Inv65, N'Office 2021 Standard Edition LTSC', 1.0000, 50.00, 50.00, 3, 19.00, 0.00, N'Fixed', @Sec65),
    (@Inv65, N'Yenkee YKM 2006 Wireless Mouse & Keyboard', 1.0000, 25.00, 25.00, 4, 19.00, 0.00, N'Fixed', @Sec65);
END
GO

-- INV 66 Lines
DECLARE @Inv66 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00066' AND [BusinessId] = 1);
DECLARE @Sec66 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv66 AND [Name] = N'General');
IF @Inv66 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv66)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv66, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/09/2025 - 30/09/2025', 1.0000, 94.58, 94.58, 1, 19.00, 0.00, N'Fixed', @Sec66);
GO

-- INV 67 Lines
DECLARE @Inv67 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00067' AND [BusinessId] = 1);
DECLARE @Sec67 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv67 AND [Name] = N'General');
IF @Inv67 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv67)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv67, N'XPrinter C260M Kitchen Printer (2-year warranty) 80mm, 230MM/S, Direct Thermal, LAN/USB, Auto-cut with Light and Sound Alarm', 1.0000, 165.00, 165.00, 1, 19.00, 0.00, N'Fixed', @Sec67);
GO

-- INV 68 Lines
DECLARE @Inv68 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00068' AND [BusinessId] = 1);
DECLARE @Sec68 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv68 AND [Name] = N'General');
IF @Inv68 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv68)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv68, N'Master POS (Server) & POS Admin Software', 1.0000, 1600.00, 1300.00, 1, 19.00, 300.00, N'Fixed', @Sec68),
    (@Inv68, N'Computer device preparation and installation', 1.0000, 100.00, 0.00, 2, 19.00, 100.00, N'Fixed', @Sec68),
    (@Inv68, N'MyChair POS Manager online application (Yearly subscription/device)', 1.0000, 220.00, 0.00, 3, 19.00, 220.00, N'Fixed', @Sec68);
END
GO

-- INV 69 Lines
DECLARE @Inv69 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00069' AND [BusinessId] = 1);
DECLARE @Sec69 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv69 AND [Name] = N'General');
IF @Inv69 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv69)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv69, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 240GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 210.00, 210.00, 1, 19.00, 0.00, N'Fixed', @Sec69),
    (@Inv69, N'Touch Screen Monitor -- Siemens Nixdorf BA93W 15.6" (Certified Refurbished, 2-year warranty) DVI-to-HDMI Cable', 1.0000, 310.00, 250.00, 2, 19.00, 60.00, N'Fixed', @Sec69),
    (@Inv69, N'Cash Drawer -- Metallic Black (1-year warranty)', 1.0000, 85.00, 85.00, 3, 19.00, 0.00, N'Fixed', @Sec69),
    (@Inv69, N'Thermal Printer: XPrinter XP-T80Q Receipt/Bill Printer (2-year warranty)', 1.0000, 155.00, 155.00, 4, 19.00, 0.00, N'Fixed', @Sec69),
    (@Inv69, N'Wireless Keyboard & Mouse', 1.0000, 23.00, 23.00, 5, 19.00, 0.00, N'Fixed', @Sec69);
END
GO

-- INV 70 Lines
DECLARE @Inv70 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00070' AND [BusinessId] = 1);
DECLARE @Sec70 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv70 AND [Name] = N'General');
IF @Inv70 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv70)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv70, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 120GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 200.00, 200.00, 1, 19.00, 0.00, N'Fixed', @Sec70),
    (@Inv70, N'Touch Screen Monitor - Siemens Nixdorf L185W 18.5" (Certified Refurbished, 2-year warranty) DVI-to-HDMI Cable + Wall Mount', 1.0000, 340.00, 290.00, 2, 19.00, 50.00, N'Fixed', @Sec70),
    (@Inv70, N'Wireless Keyboard & Mouse', 1.0000, 20.00, 20.00, 3, 19.00, 0.00, N'Fixed', @Sec70);
END
GO

-- INV 71 Lines
DECLARE @Inv71 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00071' AND [BusinessId] = 1);
DECLARE @Sec71 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv71 AND [Name] = N'General');
IF @Inv71 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv71)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv71, N'Dell PC TINY 7050 Micro i5-6500T, RAM: 8GB, SSD: 120GB, Windows 10 Professional (Certified Refurbished, 2-year warranty)', 1.0000, 200.00, 200.00, 1, 19.00, 0.00, N'Fixed', @Sec71),
    (@Inv71, N'Touch Screen Monitor -- Siemens Nixdorf BA93W 15.6" (Certified Refurbished, 2-year warranty) DVI-to-HDMI Cable + Wall Mount', 1.0000, 195.00, 195.00, 2, 19.00, 0.00, N'Fixed', @Sec71);
END
GO

-- INV 72 Lines
DECLARE @Inv72 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00072' AND [BusinessId] = 1);
DECLARE @Sec72 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv72 AND [Name] = N'General');
IF @Inv72 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv72)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv72, N'Thermal label roll, glossy x1000', 8.0000, 7.00, 56.00, 1, 19.00, 0.00, N'Fixed', @Sec72);
GO

-- INV 73 Lines
DECLARE @Inv73 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00073' AND [BusinessId] = 1);
DECLARE @Sec73 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv73 AND [Name] = N'General');
IF @Inv73 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv73)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv73, N'XP-410B LAN/USB Direct Thermal Label Printer (Max print width: 104mm, 203 dpi, 32-bit, 8MB SDRAM)', 1.0000, 240.00, 220.00, 1, 19.00, 20.00, N'Fixed', @Sec73);
GO

-- INV 74 Lines
DECLARE @Inv74 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00074' AND [BusinessId] = 1);
DECLARE @Sec74 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv74 AND [Name] = N'General');
IF @Inv74 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv74)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv74, N'Maintenance, Backups & Support. Monitoring website health and performance, providing a secure environment, and performing up-to-date updates. Yearly fees -- Ending at 13-05-2026', 1.0000, 1800.00, 1800.00, 1, 19.00, 0.00, N'Fixed', @Sec74);
GO

-- INV 75 Lines
DECLARE @Inv75 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00075' AND [BusinessId] = 1);
DECLARE @Sec75 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv75 AND [Name] = N'General');
IF @Inv75 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv75)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv75, N'Online Service Fees (www.thekennedyscafe.com) Period: 01/10/2025 - 31/10/2025', 1.0000, 96.87, 96.87, 1, 19.00, 0.00, N'Fixed', @Sec75),
    (@Inv75, N'Online Service Termination Fees (Domain, SSL Certificate, ID Protection)', 1.0000, 80.00, 80.00, 2, 19.00, 0.00, N'Fixed', @Sec75);
END
GO

-- INV 76 Lines
DECLARE @Inv76 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00076' AND [BusinessId] = 1);
DECLARE @Sec76 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv76 AND [Name] = N'General');
IF @Inv76 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv76)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv76, N'Thermal label roll, glossy x1000', 8.0000, 7.00, 56.00, 1, 19.00, 0.00, N'Fixed', @Sec76);
GO

-- INV 77 Lines
DECLARE @Inv77 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00077' AND [BusinessId] = 1);
DECLARE @Sec77 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv77 AND [Name] = N'General');
IF @Inv77 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv77)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv77, N'Label Print Server License: 3INV-1-SUB-1YR (1 Year License -- 1 Printer)', 12.0000, 22.00, 264.00, 1, 19.00, 0.00, N'Fixed', @Sec77);
GO

-- INV 78 Lines
DECLARE @Inv78 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00078' AND [BusinessId] = 1);
DECLARE @Sec78 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv78 AND [Name] = N'General');
IF @Inv78 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv78)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv78, N'Label Print Server License: 3INV-2-SUB-3YR (3-Year License -- 2 Printers)', 36.0000, 29.00, 1044.00, 1, 19.00, 0.00, N'Fixed', @Sec78);
GO

-- INV 79 Lines
DECLARE @Inv79 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00079' AND [BusinessId] = 1);
DECLARE @Sec79 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv79 AND [Name] = N'General');
IF @Inv79 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv79)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv79, N'SMS Service for 01/10/24 -- 01/11/25 (Credit: 2600 SMS)', 2600.0000, 0.03, 78.00, 1, 19.00, 0.00, N'Fixed', @Sec79);
GO

-- INV 80 Lines
DECLARE @Inv80 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00080' AND [BusinessId] = 1);
DECLARE @Sec80 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv80 AND [Name] = N'General');
IF @Inv80 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv80)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv80, N'Jobs Distribution System', 1.0000, 8400.00, 8400.00, 1, 19.00, 0.00, N'Fixed', @Sec80);
GO

-- INV 81 Lines
DECLARE @Inv81 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00081' AND [BusinessId] = 1);
DECLARE @Sec81 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv81 AND [Name] = N'General');
IF @Inv81 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv81)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv81, N'Thermal label roll, glossy x1000', 8.0000, 7.00, 56.00, 1, 19.00, 0.00, N'Fixed', @Sec81);
GO

-- INV 82 Lines
DECLARE @Inv82 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00082' AND [BusinessId] = 1);
DECLARE @Sec82 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv82 AND [Name] = N'General');
IF @Inv82 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv82)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv82, N'Thermal label roll, glossy x1000 (50x30mm)', 3.0000, 7.00, 21.00, 1, 19.00, 0.00, N'Fixed', @Sec82),
    (@Inv82, N'Thermal label roll, glossy x1000 (56x35mm)', 8.0000, 7.50, 60.00, 2, 19.00, 0.00, N'Fixed', @Sec82);
END
GO

-- INV 83 Lines
DECLARE @Inv83 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00083' AND [BusinessId] = 1);
DECLARE @Sec83 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv83 AND [Name] = N'General');
IF @Inv83 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv83)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv83, N'Thermal label roll, glossy x1000 (56x35mm)', 8.0000, 7.50, 60.00, 1, 19.00, 0.00, N'Fixed', @Sec83);
GO

-- INV 84 Lines
DECLARE @Inv84 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00084' AND [BusinessId] = 1);
DECLARE @Sec84 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv84 AND [Name] = N'General');
IF @Inv84 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv84)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv84, N'Logitech ConferenceCam Connect - Video conferencing kit / silver | 960-001034 (Open Box -- No warranty)', 1.0000, 180.00, 180.00, 1, 19.00, 0.00, N'Fixed', @Sec84);
GO

-- INV 85 Lines
DECLARE @Inv85 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00085' AND [BusinessId] = 1);
DECLARE @Sec85 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv85 AND [Name] = N'General');
IF @Inv85 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv85)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv85, N'Peoportal.org domain - 12 months subscription -- Ending on 10/02/2027', 1.0000, 45.00, 45.00, 1, 19.00, 0.00, N'Fixed', @Sec85);
GO

-- INV 86 Lines
DECLARE @Inv86 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00086' AND [BusinessId] = 1);
DECLARE @Sec86 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv86 AND [Name] = N'General');
IF @Inv86 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv86)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv86, N'Jobs Distribution System -- Manual Job Creation Feature', 1.0000, 700.00, 700.00, 1, 19.00, 0.00, N'Fixed', @Sec86),
    (@Inv86, N'Support Hours 07/25 -- 02/26', 10.0000, 50.00, 500.00, 2, 19.00, 0.00, N'Fixed', @Sec86);
END
GO

-- INV 87 Lines
DECLARE @Inv87 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00087' AND [BusinessId] = 1);
DECLARE @Sec87 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv87 AND [Name] = N'General');
IF @Inv87 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv87)
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv87, N'Thermal label roll, glossy x1000 (56x35mm)', 8.0000, 7.50, 60.00, 1, 19.00, 0.00, N'Fixed', @Sec87);
GO

-- INV 88 Lines
DECLARE @Inv88 INT = (SELECT [Id] FROM [invoice].[Invoice] WHERE [InvoiceNumber] = N'INV-1-00088' AND [BusinessId] = 1);
DECLARE @Sec88 INT = (SELECT [Id] FROM [invoice].[InvoiceSection] WHERE [InvoiceId] = @Inv88 AND [Name] = N'General');
IF @Inv88 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [invoice].[InvoiceLine] WHERE [InvoiceId] = @Inv88)
BEGIN
    INSERT INTO [invoice].[InvoiceLine] ([InvoiceId],[Description],[Quantity],[UnitPrice],[LineTotal],[SortOrder],[VatRate],[Discount],[DiscountType],[InvoiceSectionId]) VALUES
    (@Inv88, N'Thermal label roll, glossy x1000 (56x35mm)', 8.0000, 7.50, 60.00, 1, 19.00, 0.00, N'Fixed', @Sec88),
    (@Inv88, N'Lenovo Tab 10"', 1.0000, 145.00, 45.00, 2, 19.00, 100.00, N'Fixed', @Sec88);
END
GO

-- =============================================================================
-- END OF SEED SCRIPT
-- =============================================================================
PRINT 'All invoices seed completed successfully.';
GO
