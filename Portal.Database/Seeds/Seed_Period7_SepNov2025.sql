-- ============================================================
-- Period 7: September 2025 - November 2025
-- VatSubmissionPeriodId = 7
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Supermarket (Lidl) - September
(1, 54, 14, 1, NULL, '2025-09-28', 'Supermarket', 11.55, 1.57, 13.12, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-24', 'Supermarket', 16.28, 0.70, 16.98, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-22', 'Supermarket', 16.64, 0.81, 17.45, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-20', 'Supermarket', 12.89, 0.69, 13.58, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-16', 'Supermarket', 23.77, 1.34, 25.11, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-14', 'Supermarket', 52.15, 7.43, 59.58, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-13', 'Supermarket', 9.24, 1.75, 10.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-09-09', 'Supermarket', 11.20, 0.28, 11.48, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Vienna Bakeries
(1, 78, 14, 1, NULL, '2025-09-07', 'Bakery', 6.90, 0.37, 7.27, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Zorbas Bakeries
(1, 79, 14, 1, NULL, '2025-09-27', 'Bakery', 4.58, 0.23, 4.81, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 79, 14, 1, NULL, '2025-09-03', 'Bakery', 12.37, 0.63, 13.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Metro
(1, 57, 14, 1, NULL, '2025-09-28', 'Supermarket', 6.04, 0.30, 6.34, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-09-08', 'Supermarket', 16.78, 0.59, 17.37, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Sklavenitis
(1, 58, 14, 1, NULL, '2025-09-04', 'Supermarket', 7.82, 0.39, 8.21, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-09-16', 'Supermarket', 8.19, 0.78, 8.97, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Waddah Flahaha
(1, 60, 14, 1, NULL, '2025-09-26', 'Supermarket', 3.76, 0.19, 3.95, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 60, 14, 1, NULL, '2025-09-24', 'Supermarket', 4.11, 0.16, 4.27, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Stationery
(1, 68, 3, 1, 'CA30065454', '2025-09-05', 'Stationery', 1.92, 0.36, 2.28, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 4, 3, 1, '922666901', '2025-09-23', 'Stationery', 16.90, 1.20, 18.10, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 4, 3, 1, '922665086', '2025-09-13', 'Stationery', 22.64, 0.68, 23.32, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Restaurants & Bar - September
(1, 27, 7, 1, NULL, '2025-09-04', 'Meeting', 9.50, 0.85, 10.35, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-09-28', 'Meeting', 38.97, 3.35, 42.32, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2025-09-15', 'Meeting', 25.50, 2.30, 27.80, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 15, 7, 1, NULL, '2025-09-05', 'Meeting', 43.67, 3.93, 47.60, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-09-03', 'Meeting', 53.30, 4.80, 58.10, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Bath Equipment (Super Home Center)
(1, 16, 28, 1, 'P204000371030', '2025-09-13', 'Bath Equipment', 11.76, 2.23, 13.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Office Equipment (HM House Market)
(1, 80, 3, 1, '6000837697', '2025-09-22', 'Office Equipment', 21.25, 4.04, 25.29, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- October - Restaurants
(1, 9, 7, 1, NULL, '2025-10-02', 'Meeting', 33.94, 3.06, 37.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- October/November - Lidl
(1, 54, 14, 1, NULL, '2025-11-27', 'Supermarket', 3.26, 0.62, 3.88, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-13', 'Supermarket', 10.71, 2.04, 12.75, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-12', 'Supermarket', 11.18, 0.44, 11.62, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-10', 'Supermarket', 15.60, 0.73, 16.33, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-06', 'Supermarket', 10.58, 0.39, 10.97, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-03', 'Supermarket', 16.60, 0.81, 17.41, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-01', 'Supermarket', 4.61, 0.87, 5.48, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-20', 'Supermarket', 11.62, 0.80, 12.42, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-19', 'Supermarket', 11.60, 1.73, 13.33, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-25', 'Supermarket', 3.41, 0.17, 3.58, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-05', 'Supermarket', 31.24, 5.05, 36.29, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-01', 'Supermarket', 17.03, 1.02, 18.05, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-17', 'Supermarket', 8.16, 0.31, 8.47, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-24', 'Supermarket', 32.30, 1.85, 34.15, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-16', 'Supermarket', 20.20, 1.87, 22.07, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-30', 'Supermarket', 9.81, 0.92, 10.73, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-11-28', 'Supermarket', 22.39, 1.03, 23.42, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-27', 'Supermarket', 15.36, 0.65, 16.01, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-10-16', 'Supermarket', 15.11, 1.48, 16.59, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE());


-- Metro (October/November)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 57, 14, 1, NULL, '2025-10-08', 'Supermarket', 11.92, 0.51, 12.43, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-10-21', 'Supermarket', 12.54, 0.47, 13.01, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-10-24', 'Supermarket', 3.64, 0.18, 3.82, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-11-02', 'Supermarket', 12.07, 0.60, 12.67, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-11-06', 'Supermarket', 8.31, 0.42, 8.73, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-11-11', 'Supermarket', 6.41, 0.32, 6.73, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-11-17', 'Supermarket', 2.81, 0.14, 2.95, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-11-21', 'Supermarket', 14.40, 0.73, 15.13, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-10-27', 'Supermarket', 10.47, 0.52, 10.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Sklavenitis (October/November)
(1, 58, 14, 1, NULL, '2025-10-11', 'Supermarket', 11.49, 0.57, 12.06, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-10-13', 'Supermarket', 13.84, 1.21, 15.05, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-10-20', 'Supermarket', 2.24, 0.11, 2.35, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-11-08', 'Supermarket', 17.13, 0.80, 17.93, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-11-10', 'Supermarket', 12.28, 1.14, 13.42, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-10-16', 'Supermarket', 36.62, 5.96, 42.58, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Waddah Flahaha (October)
(1, 60, 14, 1, NULL, '2025-10-20', 'Supermarket', 4.05, 0.20, 4.25, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Foodhaus
(1, 102, 14, 1, NULL, '2025-10-22', 'Supermarket', 5.24, 0.26, 5.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Zorbas (November)
(1, 79, 14, 1, NULL, '2025-11-01', 'Bakery', 1.81, 0.09, 1.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Vienna Bakeries (October)
(1, 78, 14, 1, NULL, '2025-10-16', 'Bakery', 5.24, 0.26, 5.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Kitchen/Bath Equipment (Super Home Center)
(1, 16, 28, 1, NULL, '2025-11-02', 'Kitchen Equipment', 20.39, 3.88, 24.27, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 16, 28, 1, NULL, '2025-11-29', 'WC Equipment', 17.80, 3.38, 21.18, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 16, 28, 1, NULL, '2025-11-22', 'Kitchen Equipment', 4.56, 0.23, 4.79, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 16, 28, 1, NULL, '2025-11-29', 'Kitchen Equipment', 8.97, 1.70, 10.67, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Restaurants & Bar (October/November)
(1, 81, 7, 1, NULL, '2025-10-09', 'Meeting', 5.71, 0.29, 6.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 69, 7, 1, NULL, '2025-10-08', 'Meeting', 24.31, 2.19, 26.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 77, 7, 1, NULL, '2025-10-18', 'Meeting', 55.06, 4.94, 60.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-10-18', 'Meeting', 24.31, 2.19, 26.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 77, 7, 1, NULL, '2025-11-07', 'Meeting', 39.44, 3.56, 43.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 67, 14, 1, NULL, '2025-11-08', 'Consumables', 15.05, 0.75, 15.80, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-11-10', 'Meeting', 33.03, 2.97, 36.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-11-09', 'Meeting', 55.96, 4.84, 60.80, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 82, 7, 1, NULL, '2025-11-14', 'Meeting', 39.91, 3.59, 43.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 69, 7, 1, NULL, '2025-11-12', 'Meeting', 69.72, 6.28, 76.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-11-21', 'Entertainment', 34.86, 3.14, 38.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 101, 7, 1, NULL, '2025-11-22', 'Meeting', 56.33, 5.07, 61.40, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE());


-- Fuels (EKO)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 5, 6, 1, '19189', '2025-09-01', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '19812', '2025-09-02', 'Fuels', 7.06, 1.34, 8.40, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '90779', '2025-09-08', 'Fuels', 8.66, 1.65, 10.31, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '24879', '2025-09-12', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '94350', '2025-09-12', 'Fuels', 7.97, 1.52, 9.49, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '25044', '2025-09-12', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '26307', '2025-09-15', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '29496', '2025-09-21', 'Fuels', 10.86, 2.06, 12.92, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '30372', '2025-09-23', 'Fuels', 10.30, 1.96, 12.26, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '31295', '2025-09-25', 'Fuels', 40.91, 7.77, 48.68, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '6064', '2025-09-27', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '10726', '2025-10-03', 'Fuels', 19.57, 3.33, 22.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '58303', '2025-11-19', 'Fuels', 10.95, 2.08, 13.03, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '44871', '2025-11-19', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '41569', '2025-11-14', 'Fuels', 26.97, 4.88, 31.85, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '34269', '2025-11-04', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '44763', '2025-10-21', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '44104', '2025-10-20', 'Fuels', 13.08, 2.49, 15.57, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '43877', '2025-10-20', 'Fuels', 10.16, 1.93, 12.09, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '40855', '2025-10-14', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '14878', '2025-10-09', 'Fuels', 17.09, 3.25, 20.34, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '37088', '2025-10-06', 'Fuels', 8.54, 1.62, 10.16, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '61533', '2025-11-26', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '50486', '2025-11-27', 'Fuels', 10.18, 1.93, 12.11, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '52870', '2025-11-30', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '55730', '2025-11-13', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- ENI
(1, 83, 6, 1, NULL, '2025-11-06', 'Fuels', 9.25, 1.76, 11.01, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- ESSO
(1, 75, 6, 1, NULL, '2025-11-15', 'Fuels', 8.40, 1.60, 10.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- R.A.M. OIL
(1, 74, 6, 1, NULL, '2025-11-11', 'Fuels', 42.62, 8.10, 50.72, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-09-08', 'Fuels', 40.34, 7.66, 48.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-09-29', 'Fuels', 44.87, 8.53, 53.40, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-08', 'Fuels', 42.03, 7.98, 50.01, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-31', 'Fuels', 41.18, 7.82, 49.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-23', 'Fuels', 43.29, 8.22, 51.51, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-15', 'Fuels', 44.13, 8.38, 52.51, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-11-26', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-11-19', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-11-13', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-14', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-09-25', 'Fuels', 40.91, 7.77, 48.68, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-10-03', 'Fuels', 19.57, 3.33, 22.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE());


-- Stationery (Pantelis Katelaris)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 84, 3, 1, NULL, '2025-10-25', 'Stationery', 12.13, 2.30, 14.43, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 84, 3, 1, NULL, '2025-10-25', 'Stationery', 2.31, 0.44, 2.75, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 4, 3, 1, NULL, '2025-11-26', 'Stationery', 0.59, 0.11, 0.70, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 68, 3, 1, NULL, '2025-11-15', 'Stationery', 3.96, 0.40, 4.36, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Gifts
(1, 85, 13, 1, NULL, '2025-11-29', 'Gifts', 23.95, 4.55, 28.50, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 86, 13, 1, NULL, '2025-10-08', 'Gifts', 10.08, 1.91, 11.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 87, 13, 1, NULL, '2025-10-19', 'Gifts', 84.04, 15.96, 100.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 63, 13, 1, NULL, '2025-11-08', 'Gifts', 37.81, 7.18, 44.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 63, 13, 1, NULL, '2025-11-22', 'Gifts', 82.34, 15.65, 97.99, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 88, 13, 1, NULL, '2025-11-25', 'Gifts', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 35, 13, 1, NULL, '2025-10-01', 'Gifts', 20.28, 3.85, 24.13, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 93, 13, 1, NULL, '2025-10-15', 'Gifts', 72.00, 13.68, 85.68, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Vehicle Service/Transportation
(1, 51, 22, 1, NULL, '2025-09-29', 'Antifreeze - engine coolant', 80.00, 0.00, 80.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Equipment (WT.Miami)
(1, 154, 10, 1, NULL, '2025-09-29', 'Cafe Equipment', 70.00, 0.00, 70.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Health & Safety
(1, 104, 25, 1, NULL, '2025-12-01', 'Pharmacy', 15.90, 0.80, 16.70, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (MCIT)
(1, 21, 10, 1, '1704', '2025-10-13', 'PC Equipment', 141.50, 26.89, 168.39, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, '1510', '2025-09-16', 'PC Equipment', 49.00, 9.31, 58.31, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, '1507', '2025-09-15', 'PC Equipment', 338.00, 64.22, 402.22, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, '1572', '2025-09-23', 'PC Equipment', 445.00, 84.55, 529.55, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Software (CJS CD Keys - Non-EU USA)
(1, 103, 9, 3, '4261856', '2025-01-01', 'Microsoft Office Standard 2024', 12.99, 0.00, 12.99, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 103, 9, 3, '4270315', '2025-01-01', 'Microsoft Office Standard 2021', 9.99, 0.00, 9.99, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 103, 9, 3, '4270831', '2025-01-01', 'Microsoft Office Standard 2021', 9.99, 0.00, 9.99, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Server Subscriptions (Database Mart - Non-EU USA)
(1, 41, 9, 3, '367169', '2025-11-14', 'Server Subscription - Web Server', 213.51, 0.00, 213.51, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '370758', '2025-11-18', 'Server Subscription - Email Server', 70.79, 0.00, 70.79, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '344948', '2025-10-18', 'Server Subscription - Email Server', 70.68, 0.00, 70.68, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '342567', '2025-10-15', 'Server Subscription - Web Server', 214.21, 0.00, 214.21, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '319809', '2025-09-18', 'Server Subscription - Email Server', 70.07, 0.00, 70.07, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '317470', '2025-09-15', 'Server Subscription - Web Server', 211.28, 0.00, 211.28, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '313774', '2025-09-10', 'SSL', 34.33, 0.00, 34.33, 'USA', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Furniture (GEVOREST)
(1, 89, 2, 1, 'SAL0122393', '2025-10-09', 'Furniture', 1054.62, 200.38, 1255.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 89, 2, 1, '124996', '2025-09-30', 'Furniture', 105.04, 19.96, 125.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Furniture (Leroy Merlin)
(1, 90, 2, 1, '4039', '2025-10-25', 'Furniture', 14.00, 2.66, 16.66, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 90, 2, 1, '17191', '2025-11-25', 'Furniture', 43.52, 8.27, 51.79, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 90, 2, 1, '11840', '2025-10-16', 'Furniture', 21.48, 4.08, 25.56, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Equipment (SPANIAS)
(1, 91, 10, 1, NULL, '2025-12-02', 'Outdoor cleaning equipment', 97.39, 18.50, 115.89, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (Eshop Cyprus)
(1, 20, 10, 1, '31813', '2025-10-14', 'PC-communication-hardware', 13.36, 2.54, 15.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, '31998', '2025-11-27', 'PC-communication-hardware', 5.80, 1.10, 6.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, '31860', '2025-10-27', 'PC-communication-hardware', 20.25, 3.85, 24.10, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, '32014', '2025-12-01', 'PC-communication-hardware', 11.68, 2.22, 13.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE());


-- Seagull Software (Non-EU - USA) - Outdated from June (Period 6)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 92, 10, 3, NULL, '2025-06-11', 'Software/Hardware', 611.00, 0.00, 611.00, 'USA', 'Outdated - reported in Period 7', 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 92, 10, 3, NULL, '2025-06-11', 'Software/Hardware', 143.55, 0.00, 143.55, 'USA', 'Outdated - reported in Period 7', 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Cleaning Equipment
(1, 94, 10, 1, NULL, '2025-11-22', 'Cleaning Equipment', 75.55, 14.35, 89.90, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Government/Infringement
(1, 3, 24, 1, '1104529', '2025-11-13', 'Infringement', 100.00, 0.00, 100.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (Ebay)
(1, 95, 10, 1, NULL, '2025-10-25', 'PC Equipment', 9.42, 2.21, 11.63, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 95, 10, 1, NULL, '2025-10-08', 'PC Equipment', 120.00, 25.00, 145.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 95, 10, 1, NULL, '2025-09-17', 'Lenovo ThinkHub', 320.76, 75.24, 396.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (Darya - Germany)
(1, 96, 10, 2, NULL, '2025-09-19', 'PC Equipment', 117.59, 47.05, 164.64, 'Germany', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (Web Supplies - Non-EU China)
(1, 97, 10, 3, NULL, '2025-09-19', 'PC Equipment', 12.02, 0.00, 12.02, 'China', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Hardware (AliExpress - Non-EU China)
(1, 98, 10, 3, NULL, '2025-09-14', 'PC Equipment', 152.84, 29.03, 181.87, 'China', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-11-17', 'PC Equipment', 69.36, 13.18, 82.54, 'China', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- OpenAI (EU - Ireland)
(1, 99, 30, 2, 'OTYQNWJT-0001', '2025-10-23', 'AI Software', 19.33, 0.00, 19.33, 'Ireland', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 99, 30, 2, 'OTYQNWJT-0002', '2025-11-23', 'AI Software', 19.33, 0.00, 19.33, 'Ireland', NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Stationery (W.P.E. Print)
(1, 49, 3, 1, NULL, '2025-09-23', 'Cards RV', 12.60, 2.40, 15.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Shipping (DHL)
(1, 100, 15, 1, NULL, '2025-09-19', 'Mini receipt printer - sending items', 40.31, 2.28, 42.59, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Utilities (AHK - Electricity)
(1, 46, 17, 1, NULL, '2025-10-07', 'Electricity 0825-1025', 134.58, 11.89, 146.47, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Telephone Bills
(1, 39, 18, 1, NULL, '2025-09-30', 'Telephone', 50.55, 6.41, 56.96, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, NULL, '2025-10-31', 'Telephone', 48.51, 6.04, 54.55, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, NULL, '2025-11-30', 'Telephone', 39.75, 7.55, 47.30, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),

-- Internet Bills
(1, 47, 18, 1, NULL, '2025-09-01', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-10-01', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-11-01', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 7, GETUTCDATE(), GETUTCDATE());
