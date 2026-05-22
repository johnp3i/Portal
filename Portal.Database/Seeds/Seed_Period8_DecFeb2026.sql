-- ============================================================
-- Period 8: December 2025 - February 2026
-- VatSubmissionPeriodId = 8
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- META (EU - Ireland) - VAT is empty, set to 0
(1, 134, 26, 2, '25750873094597716-25848312524853778', '2026-02-25', 'Meta Platforms Ireland Limited', 18.00, 0.00, 18.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '26137159322635761-25846154681736229', '2026-02-25', 'Meta Platforms Ireland Limited', 18.00, 0.00, 18.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25758131983871836-25720700594281633', '2026-02-22', 'Meta Platforms Ireland Limited', 18.00, 0.00, 18.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25727043780313990-25809968188688214', '2026-02-19', 'Meta Platforms Ireland Limited', 18.00, 0.00, 18.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25717830374568658-26048660261485668', '2026-02-16', 'Meta Platforms Ireland Limited', 16.00, 0.00, 16.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25746457635039268-25575314378820257', '2026-02-15', 'Meta Platforms Ireland Limited', 14.00, 0.00, 14.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25664618523223178-25678459978505704', '2026-02-14', 'Meta Platforms Ireland Limited', 12.00, 0.00, 12.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25633376229680737-25691309457220750', '2026-02-13', 'Meta Platforms Ireland Limited', 11.00, 0.00, 11.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25681621161522913-26012554058429622', '2026-02-12', 'Meta Platforms Ireland Limited', 10.00, 0.00, 10.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25652562817762087-25755780550773644', '2026-02-11', 'Meta Platforms Ireland Limited', 9.00, 0.00, 9.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25747485568269809-25630385269979837', '2026-02-10', 'Meta Platforms Ireland Limited', 8.00, 0.00, 8.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25991501657201529-25700325816319117', '2026-02-10', 'Meta Platforms Ireland Limited', 7.00, 0.00, 7.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25626118373739865-25646680121683684', '2026-02-09', 'Meta Platforms Ireland Limited', 6.00, 0.00, 6.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25516605078024522-25614671648217871', '2026-02-08', 'Meta Platforms Ireland Limited', 5.00, 0.00, 5.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25668164486201917-25668164532868579', '2026-02-07', 'Meta Platforms Ireland Limited', 4.00, 0.00, 4.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25702996139385419-25620342044317492', '2026-02-06', 'Meta Platforms Ireland Limited', 3.00, 0.00, 3.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 134, 26, 2, '25942340055451023-25492475023770861', '2026-02-05', 'Meta Platforms Ireland Limited', 2.00, 0.00, 2.00, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Equipment
(1, 135, 10, 1, NULL, '2026-01-31', 'Equipment', 4.71, 0.89, 5.60, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, NULL, '2025-12-31', 'Desk Equipment', 29.19, 5.55, 34.74, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Stationery
(1, 4, 3, 1, NULL, '2025-12-31', 'Stationery', 26.87, 0.81, 27.68, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 140, 3, 1, NULL, '2025-12-31', 'Stationery', 18.44, 0.55, 18.99, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Fuels (R.A.M. OIL)
(1, 74, 6, 1, NULL, '2026-01-05', 'Fuels', 43.29, 8.22, 51.51, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-12-17', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-12-23', 'Fuels', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-12-12', 'Fuels', 41.18, 7.82, 49.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Fuels (Petrolina)
(1, 136, 6, 1, NULL, '2026-02-27', 'Fuels', 50.42, 9.58, 60.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2026-02-23', 'Fuels', 46.22, 8.78, 55.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2026-02-16', 'Fuels', 50.43, 9.58, 60.01, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2025-12-12', 'Fuels', 38.66, 7.35, 46.01, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2025-12-05', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
-- Fixed date: 27/12/2026 -> 27/12/2025
(1, 136, 6, 1, NULL, '2025-12-27', 'Fuels', 12.61, 2.39, 15.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2026-01-09', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE());


-- Supermarket (Lidl)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 54, 14, 1, NULL, '2026-02-16', 'Supermarket', 21.19, 1.24, 22.43, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-03', 'Supermarket', 29.64, 4.87, 34.51, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-22', 'Supermarket', 10.17, 0.80, 10.97, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-11', 'Supermarket', 6.45, 0.32, 6.77, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-23', 'Supermarket', 4.82, 0.61, 5.43, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-06', 'Supermarket', 17.28, 1.00, 18.28, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-27', 'Supermarket', 9.92, 0.80, 10.72, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-22', 'Supermarket', 15.43, 0.65, 16.08, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-08', 'Supermarket', 24.85, 1.32, 26.17, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-10', 'Supermarket', 42.81, 3.87, 46.68, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-29', 'Supermarket', 29.78, 3.82, 33.60, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-01', 'Supermarket', 11.39, 0.57, 11.96, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-24', 'Supermarket', 27.61, 2.19, 29.80, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-12-28', 'Supermarket', 11.23, 1.90, 13.13, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-04', 'Supermarket', 23.30, 1.74, 25.04, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-25', 'Supermarket', 15.00, 1.38, 16.38, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-02-07', 'Supermarket', 2.42, 0.12, 2.54, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-12-21', 'Supermarket', 25.71, 1.71, 27.42, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-12-19', 'Supermarket', 42.05, 2.38, 44.43, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2026-01-21', 'Supermarket', 11.23, 1.23, 12.46, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Sklavenitis
(1, 58, 14, 1, NULL, '2026-02-27', 'Supermarket', 9.05, 0.67, 9.72, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-02-09', 'Supermarket', 22.06, 1.74, 23.80, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-02-19', 'Supermarket', 18.62, 1.34, 19.96, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-01-22', 'Supermarket', 10.63, 1.20, 11.83, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-02-13', 'Supermarket', 34.37, 4.02, 38.39, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-12-29', 'Supermarket', 5.43, 0.27, 5.70, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-02-03', 'Supermarket', 9.43, 1.01, 10.44, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2026-01-20', 'Supermarket', 6.47, 0.28, 6.75, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
-- Fixed date: 15/12/2026 -> 15/12/2025
(1, 58, 14, 1, NULL, '2025-12-15', 'Supermarket', 17.72, 2.01, 19.73, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Metro
(1, 57, 14, 1, NULL, '2025-12-28', 'Supermarket', 11.52, 1.13, 12.65, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-12-23', 'Supermarket', 19.00, 0.95, 19.95, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-12-21', 'Supermarket', 13.06, 0.53, 13.59, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-12-14', 'Supermarket', 15.72, 1.82, 17.54, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-12-17', 'Supermarket', 8.32, 0.42, 8.74, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Zorbas Bakeries
(1, 79, 14, 1, NULL, '2025-12-01', 'Bakery', 30.64, 1.56, 32.20, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 79, 14, 1, NULL, '2026-02-15', 'Bakery', 14.71, 0.74, 15.45, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE());


-- Restaurants & Bar
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 139, 7, 1, NULL, '2026-02-20', 'Meeting', 39.45, 3.55, 43.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2026-02-05', 'Meeting', 20.18, 1.82, 22.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 137, 7, 1, NULL, '2026-01-14', 'Meeting', 6.19, 0.31, 6.50, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-12-17', 'Meeting', 57.43, 4.97, 62.40, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2026-01-22', 'Meeting', 25.23, 2.27, 27.50, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Food (Oriental)
(1, 138, 14, 1, NULL, '2025-12-25', 'Bakery', 43.41, 3.59, 47.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 138, 14, 1, NULL, '2025-12-26', 'Bakery', 25.24, 1.26, 26.50, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Outfit (Camel Active)
(1, 130, 8, 1, NULL, '2026-01-24', 'Clothes', 59.14, 0.11, 59.25, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Outfit (A.M. EshopForFitness)
(1, 36, 8, 1, NULL, '2026-02-16', 'Clothes', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Stationery (Pantelis Katelaris)
(1, 84, 3, 1, NULL, '2025-12-17', 'Stationery', 3.28, 0.62, 3.90, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 84, 3, 1, NULL, '2025-12-14', 'Stationery', 5.72, 1.09, 6.81, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Food (Athienites)
(1, 153, 14, 1, NULL, '2025-12-06', 'Bakery', 20.00, 1.00, 21.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Stationery (Parga)
(1, 4, 3, 1, NULL, '2025-12-27', 'Stationery', 22.72, 0.68, 23.40, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Stationery (Rivergate Bibliopolis)
(1, 141, 3, 1, NULL, '2026-01-10', 'Stationery', 10.63, 0.32, 10.95, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Equipment (Super Home Center)
(1, 16, 10, 1, 'P202000412466', '2026-01-25', 'Office Equipment', 26.22, 4.80, 31.02, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, NULL, '2026-02-01', 'Office Equipment', 2.17, 0.41, 2.58, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, NULL, '2026-01-30', 'Office Equipment', 1.74, 0.33, 2.07, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, 'p205000418403', '2025-12-21', 'Office Equipment', 45.36, 8.62, 53.98, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, NULL, '2025-12-21', 'Equipment', 45.36, 8.62, 53.98, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 16, 31, 1, NULL, '2026-01-25', 'Building Repair', 45.03, 8.56, 53.59, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Health & Safety (Pharmacy)
(1, 104, 25, 1, NULL, '2025-12-29', 'Pharmacy', 58.45, 2.93, 61.38, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 104, 25, 1, NULL, '2026-01-05', 'Pharmacy', 52.88, 2.65, 55.53, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 104, 25, 1, NULL, '2026-01-05', 'Pharmacy', 35.86, 1.92, 37.78, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- PC Equipment (Bazaraki)
(1, 2, 10, 1, 'G0786270', '2025-12-04', 'PC Equipment', 140.00, 0.00, 140.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Database Mart (Non-EU - USA) - VAT empty = 0
(1, 41, 9, 3, NULL, '2026-01-14', 'Server Subscription', 70.48, 0.00, 70.48, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 11, 3, NULL, '2026-01-14', 'Domain', 15.05, 0.00, 15.05, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2026-01-14', 'Server Subscription', 212.50, 0.00, 212.50, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2025-12-23', 'SSL', 34.32, 0.00, 34.32, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
-- Fixed date: 15/12/2026 -> 15/12/2025
(1, 41, 9, 3, NULL, '2025-12-15', 'Server Subscription', 69.96, 0.00, 69.96, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2025-12-14', 'Server Subscription', 210.94, 0.00, 210.94, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2025-12-15', 'Server Subscription', 34.28, 0.00, 34.28, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2026-02-15', 'Server Subscription', 69.22, 0.00, 69.22, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2026-02-12', 'SSL', 27.85, 0.00, 27.85, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, NULL, '2026-02-14', 'Server Subscription', 208.70, 0.00, 208.70, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE());


-- Eshop Cyprus (Hardware)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 20, 10, 1, NULL, '2025-12-24', 'Equipment', 2.94, 0.56, 3.50, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, NULL, '2025-12-24', 'Equipment', 11.68, 2.22, 13.90, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, NULL, '2026-02-14', 'Equipment', 100.00, 19.00, 119.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, NULL, '2025-12-15', 'PC Equipment', 5.80, 1.10, 6.90, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- AI Software (KIRO) - VAT empty = 0
(1, 142, 30, 3, NULL, '2026-01-17', 'AI Software', 10.27, 0.00, 10.27, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 142, 30, 3, NULL, '2026-02-01', 'AI Software', 21.90, 0.00, 21.90, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Building Repair (S&N) - VAT empty = 0
(1, 143, 31, 1, '1718', '2026-01-05', 'Building Repair', 206.00, 0.00, 206.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Equipment (Leroy Merlin)
(1, 90, 10, 1, NULL, '2026-01-29', 'Equipment', 26.97, 5.14, 32.11, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 90, 10, 1, NULL, '2026-01-30', 'Equipment', 7.71, 1.46, 9.17, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 90, 10, 1, NULL, '2026-02-19', 'Plumbing', 26.99, 5.12, 32.11, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- OpenAI (EU - Ireland) - VAT empty = 0
(1, 99, 30, 2, NULL, '2026-01-23', 'AI Software', 19.33, 0.00, 19.33, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 99, 30, 2, NULL, '2025-12-23', 'AI Software', 19.33, 0.00, 19.33, 'Ireland', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Domains (GoDaddy)
(1, 23, 11, 1, NULL, '2026-01-29', 'Domain', 12.54, 2.58, 15.12, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Domains (University of Cyprus)
(1, 43, 11, 1, NULL, '2026-01-19', 'Domain', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 43, 11, 1, NULL, '2026-01-16', 'Domain', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Advertising
(1, 144, 26, 1, NULL, '2026-01-26', 'Advertising Agency', 4500.00, 855.00, 5355.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Food (A.S. Xinaris)
(1, 131, 14, 1, NULL, '2025-12-31', 'Supermarket', 100.16, 19.03, 119.19, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Domain (Easy by Europlanet) - VAT empty = 0
(1, 145, 11, 1, NULL, '2026-02-13', 'Domain', 19.90, 0.00, 19.90, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Car Rental (Enterprise) - VAT empty = 0
(1, 146, 27, 1, NULL, '2025-12-11', 'Car Rental', 96.38, 0.00, 96.38, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Software (Monzilla Foundation) - VAT empty = 0
(1, 147, 9, 1, NULL, '2025-12-04', 'Software', 10.90, 0.00, 10.90, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- SMS Service (Microsat)
(1, 156, 9, 1, NULL, '2025-12-18', 'SMS Service', 50.00, 9.50, 59.50, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Labels (LabelShop) - VAT empty = 0
(1, 155, 16, 2, NULL, '2025-12-18', 'Labels', 212.00, 0.00, 212.00, 'Italy', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Utilities (AHK)
(1, 46, 17, 1, NULL, '2025-12-31', 'Utilities', 83.16, 7.35, 90.51, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 46, 17, 1, NULL, '2026-02-28', 'Utilities', 229.96, 20.29, 250.25, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Ebay (PC Equipment) - VAT empty = 0
(1, 95, 10, 3, NULL, '2026-02-12', 'PC Equipment', 179.12, 0.00, 179.12, 'USA', NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Telephone Bills (Cyta)
(1, 39, 18, 1, NULL, '2026-01-01', 'Telephone Bill', 33.40, 6.35, 39.75, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, NULL, '2026-02-01', 'Telephone Bill', 38.90, 7.40, 46.30, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, NULL, '2025-12-01', 'Telephone Bill', 26.00, 4.94, 30.94, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Internet (Cablenet)
(1, 47, 18, 1, NULL, '2026-02-02', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Utilities (EOA - Water)
(1, 111, 17, 1, NULL, '2026-01-05', 'Utilities', 21.85, 0.90, 22.75, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Government/Tax - VAT empty = 0
(1, 148, 24, 1, NULL, '2026-01-07', 'Tax Payment', 2303.57, 0.00, 2303.57, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 149, 24, 1, NULL, '2026-01-16', 'Municipality - Business Operation', 250.00, 0.00, 250.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE());


-- Fuels (EKO)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 5, 6, 1, NULL, '2026-02-14', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-12-23', 'Fuels', 25.22, 4.79, 30.01, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-12-22', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-04', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-12-19', 'Fuels', 10.44, 1.98, 12.42, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-09', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-04', 'Fuels', 16.13, 3.07, 19.20, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-12-27', 'Fuels', 17.01, 3.23, 20.24, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-12-28', 'Fuels', 17.37, 3.30, 20.67, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-21', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-13', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-14', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-02-06', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-02-16', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-02-18', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-01-30', 'Fuels', 16.92, 3.21, 20.13, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2026-02-23', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- EKO Larnakos
(1, 150, 6, 1, NULL, '2025-12-27', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- ============================================================
-- OUTDATED SECTION: Late purchases reported in Period 8
-- InvoiceDate is from Period 7 (November 2025), VatSubmissionPeriodId = 8
-- ============================================================
(1, 70, 7, 1, '1000214906', '2025-11-20', 'Meeting', 23.67, 2.13, 25.80, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 69, 7, 1, NULL, '2025-11-05', 'Meeting', 44.95, 4.05, 49.00, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Outdated Fuels (November EKO)
(1, 5, 6, 1, NULL, '2025-11-27', 'Fuels', 20.33, 3.86, 24.19, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),

-- Outdated Fuels (Petrolina) - Fixed dates: 2026 -> context is Dec 2025-Feb 2026 period
-- 03/10/2026, 09/10/2026, 28/11/2026 are likely October/November 2025 late entries
(1, 136, 6, 1, NULL, '2025-10-03', 'Fuels', 25.21, 4.79, 30.00, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2025-10-09', 'Fuels', 21.01, 3.99, 25.00, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE()),
(1, 136, 6, 1, NULL, '2025-11-28', 'Fuels', 25.21, 4.79, 30.00, NULL, 'Outdated - reported in Period 8', 0, NULL, 8, GETUTCDATE(), GETUTCDATE());
