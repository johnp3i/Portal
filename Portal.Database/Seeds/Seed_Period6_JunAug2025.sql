-- ============================================================
-- Period 6: June 2025 - August 2025
-- VatSubmissionPeriodId = 6
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Software Subscriptions (EU - Ireland)
(1, 17, 9, 2, '5291872594', '2025-06-01', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, '5322717253', '2025-07-01', 'Google Workspace Subscription x12', 82.26, 0.00, 82.26, 'Ireland', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- License (Non-EU - USA)
(1, 107, 9, 3, '4177065', '2025-07-21', 'MS Office 2024', 26.50, 0.00, 26.50, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Developer Tools (Non-EU - Australia)
(1, 19, 9, 3, '04557-26418979-1', '2025-06-24', 'Canva Design', 92.43, 17.56, 109.99, 'Australia', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Developer Tools (Non-EU - USA)
(1, 106, 9, 3, 'A22041757580', '2025-06-20', 'AnyDesk Remote Connection', 238.80, 0.00, 238.80, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Marketing (EU - Ireland)
(1, 105, 26, 2, '781117873368', '2025-07-01', 'LinkedIn', 50.00, 0.00, 50.00, 'Ireland', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Domains
(1, 43, 11, 1, '110014', '2025-06-17', 'Chaplin domains', 50.42, 9.58, 60.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- License (Seagull Software)
(1, 92, 9, 1, 'Q-229867', '2025-06-17', 'GFI License', 365.20, 0.00, 365.20, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 92, 9, 1, 'Q-283155', '2025-08-27', 'GFI + 2nd printer License', 188.30, 0.00, 188.30, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Server Subscriptions (Non-EU - Database Mart)
(1, 41, 9, 3, '250579', '2025-06-13', 'Server Subscription - Email Server', 70.11, 0.00, 70.11, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '248401', '2025-06-10', 'Server Subscription - Web Server', 215.18, 0.00, 215.18, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '249376', '2025-06-11', 'Rapid SSL - thekennedyscafe.com', 35.92, 0.00, 35.92, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '2700508', '2025-06-12', 'Server Subscription - Web Server', 212.21, 0.00, 212.21, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '272706', '2025-06-13', 'Server Subscription - Email Server', 70.38, 0.00, 70.38, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 11, 3, '301506', '2025-07-20', 'Domain - mychairpos.com', 15.11, 0.00, 15.11, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 11, 3, '299867', '2025-08-18', 'Domain - eomfa.com', 15.11, 0.00, 15.11, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 11, 3, '297755', '2025-06-15', 'Domain - 3inventors.com', 15.09, 0.00, 15.09, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '296019', '2025-07-13', 'Server Subscription - Email Server', 70.41, 0.00, 70.41, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '293599', '2025-08-15', 'Server Subscription - Web Server', 212.58, 0.00, 212.58, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '292601', '2025-08-08', 'Rapid SSL - app.chaplin.cy', 34.55, 0.00, 34.55, 'USA', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Conference/Training
(1, 40, 19, 1, '1250067', '2025-06-05', 'Sales Seminar', 500.00, 0.00, 500.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Telephone Bills
(1, 39, 18, 1, '2001721212-6-2025', '2025-06-30', 'Telephone', 49.40, 6.19, 55.59, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, '2001721212-7-2025', '2025-07-31', 'Telephone', 53.75, 7.02, 60.77, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, '2001721212-8-2025', '2025-08-31', 'Telephone', 49.92, 6.29, 56.21, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Internet Bills
(1, 47, 18, 1, '20275491', '2025-06-30', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-07-31', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-08-31', 'Internet', 21.01, 3.99, 25.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Government
(1, 108, 24, 1, '11946832', '2025-08-29', 'Passport', 70.00, 0.00, 70.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 109, 24, 1, '4123489/1', '2025-07-05', 'Documents', 120.00, 0.00, 120.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 110, 24, 1, NULL, '2025-07-16', 'PostOffice', 3.60, 0.00, 3.60, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Utilities (Water)
(1, 111, 17, 1, NULL, '2025-08-07', 'Water Bill', 23.40, 0.95, 24.35, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Utilities (Electricity)
(1, 46, 17, 1, '23285469112', '2025-08-07', 'Electricity 0625-0825', 167.34, 14.77, 182.11, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Fuels (EKO)
(1, 5, 6, 1, NULL, '2025-06-11', 'Fuels', 16.92, 3.22, 20.14, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-11', 'Fuels', 8.50, 1.62, 10.12, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-09', 'Fuels', 7.61, 1.44, 9.05, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-07', 'Fuels', 18.57, 3.28, 21.85, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-06', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-05', 'Fuels', 9.45, 1.80, 11.25, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-01', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-29', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-27', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-22', 'Fuels', 6.61, 1.25, 7.86, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-20', 'Fuels', 10.05, 1.91, 11.96, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-17', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-17', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-17', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-08', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-12', 'Fuels', 8.90, 1.69, 10.59, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-12', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-15', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-18', 'Fuels', 8.36, 1.59, 9.95, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-21', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-23', 'Fuels', 33.82, 6.43, 40.25, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-25', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-26', 'Fuels', 8.71, 1.66, 10.37, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-07-28', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-02', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-02', 'Fuels', 17.11, 3.25, 20.36, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-04', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-05', 'Fuels', 22.05, 4.19, 26.24, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-08', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-09', 'Fuels', 16.57, 3.15, 19.72, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-22', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-23', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-27', 'Fuels', 18.49, 3.51, 22.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-08-31', 'Fuels', 9.50, 1.80, 11.30, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2025-06-26', 'Fuels', 26.89, 5.11, 32.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE());


-- Fuels (R.A.M. OIL)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 74, 6, 1, NULL, '2025-06-18', 'Fuels', 43.71, 8.31, 52.02, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-13', 'Fuels', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-07', 'Fuels', 44.12, 8.38, 52.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-29', 'Fuels', 40.34, 7.66, 48.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-21', 'Fuels', 8.66, 1.64, 10.30, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-30', 'Fuels', 42.86, 8.14, 51.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-23', 'Fuels', 42.69, 8.11, 50.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-15', 'Fuels', 44.75, 8.50, 53.25, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-08', 'Fuels', 43.71, 8.30, 52.01, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-21', 'Fuels', 41.18, 7.83, 49.01, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-04', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-08-04', 'Fuels', 7.48, 1.42, 8.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-11', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-28', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-07-15', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-06-06', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE());


-- Supermarket (Lidl)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 54, 14, 1, NULL, '2025-08-31', 'Supermarket', 9.94, 1.31, 11.25, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-28', 'Supermarket', 12.56, 1.72, 14.28, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-24', 'Supermarket', 25.62, 2.28, 27.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-22', 'Supermarket', 29.28, 1.93, 31.21, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-12', 'Supermarket', 8.24, 0.35, 8.59, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-04', 'Supermarket', 21.38, 2.79, 24.17, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-22', 'Supermarket', 9.44, 0.49, 9.93, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-17', 'Supermarket', 6.29, 0.31, 6.60, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-13', 'Supermarket', 12.57, 1.78, 14.35, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-04', 'Supermarket', 8.19, 0.13, 8.32, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-10', 'Supermarket', 12.07, 1.94, 14.01, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-11', 'Supermarket', 2.65, 0.00, 2.65, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-07-30', 'Supermarket', 28.57, 3.26, 31.83, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-03', 'Supermarket', 30.28, 1.52, 31.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-08-07', 'Supermarket', 17.77, 1.87, 19.64, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-16', 'Supermarket', 6.56, 0.17, 6.73, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-23', 'Supermarket', 5.54, 0.19, 5.73, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-21', 'Supermarket', 12.82, 1.09, 13.91, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-08', 'Supermarket', 18.36, 1.57, 19.93, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-29', 'Supermarket', 9.69, 0.33, 10.02, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-15', 'Supermarket', 15.51, 0.52, 16.03, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-06-13', 'Supermarket', 9.24, 1.75, 10.99, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Alphamega
(1, 112, 14, 1, NULL, '2025-06-17', 'Supermarket', 3.18, 0.00, 3.18, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Vienna Bakeries
(1, 78, 14, 1, NULL, '2025-06-16', 'Bakery', 9.22, 0.46, 9.68, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Metro
(1, 57, 14, 1, NULL, '2025-06-05', 'Supermarket', 11.74, 0.59, 12.33, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-06-14', 'Supermarket', 19.12, 0.96, 20.08, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-06-21', 'Supermarket', 13.42, 0.85, 14.27, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-06-25', 'Supermarket', 8.71, 0.94, 9.65, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-08-13', 'Supermarket', 6.43, 0.32, 6.75, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-17', 'Supermarket', 7.46, 0.11, 7.57, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-15', 'Supermarket', 9.54, 0.28, 9.82, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-20', 'Supermarket', 9.99, 0.50, 10.49, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-26', 'Supermarket', 5.64, 0.00, 5.64, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-08-06', 'Supermarket', 18.40, 0.54, 18.94, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-20', 'Supermarket', 10.44, 0.37, 10.81, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-07', 'Supermarket', 11.97, 0.45, 12.42, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-09', 'Supermarket', 10.97, 0.43, 11.40, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-08-02', 'Supermarket', 5.62, 0.28, 5.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-07-23', 'Supermarket', 12.24, 0.61, 12.85, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE());


-- Sklavenitis
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 58, 14, 1, NULL, '2025-07-08', 'Supermarket', 7.94, 0.40, 8.34, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-07-07', 'Supermarket', 7.40, 0.60, 8.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-06-13', 'Supermarket', 12.26, 2.23, 14.49, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-06-03', 'Supermarket', 8.88, 1.25, 10.13, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-08-08', 'Supermarket', 4.30, 0.38, 4.68, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-07-23', 'Supermarket', 15.22, 1.11, 16.33, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Melis (Meat market)
(1, 113, 14, 1, NULL, '2025-07-08', 'Meat market', 5.05, 0.25, 5.30, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- G.Giagkou (Meat market)
(1, 114, 14, 1, NULL, '2025-08-30', 'Meat market', 57.14, 2.86, 60.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Waddah Flahaha
(1, 60, 14, 1, NULL, '2025-06-20', 'Supermarket', 4.58, 0.08, 4.66, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Vehicle Service
(1, 51, 22, 1, NULL, '2025-08-06', 'Vehicle Service', 64.80, 15.20, 80.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Hardware
(1, 115, 10, 1, NULL, '2025-06-13', 'Cables', 10.88, 2.07, 12.95, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 116, 10, 2, NULL, '2025-07-11', 'eBAY- Portege x40 Laptop', 256.40, 58.08, 314.48, 'United Kingdom', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 95, 10, 2, NULL, '2025-06-13', 'eBAY- HP Engage Go 10 Touch Tablet', 193.28, 0.00, 193.28, 'Germany', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 117, 10, 1, NULL, '2025-07-10', 'HP Engage Go 10 Tablet CASE', 102.19, 0.00, 102.19, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, 'INV/2025/01393', '2025-08-26', 'Route 66 + adapter', 83.00, 15.77, 98.77, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, 'INV/2025/01298', '2025-07-29', 'GFI Warehouse', 248.00, 47.12, 295.12, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, 'INV/2025/01201', '2025-07-14', 'OCC Limassol', 352.51, 66.98, 419.49, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, NULL, '2025-08-11', 'Camera Memory Card', 3.19, 0.61, 3.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 20, 10, 1, NULL, '2025-06-12', 'Cables', 67.46, 12.82, 80.28, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Electrical Equipment
(1, 118, 10, 1, NULL, '2025-06-13', 'Tools - OCC Limassol', 7.56, 1.44, 9.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- AliExpress (Non-EU - China)
(1, 98, 23, 3, NULL, '2025-07-20', 'Alarm System', 48.73, 9.26, 57.99, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 3, 3, NULL, '2025-07-20', '3 Inventors Bag', 39.49, 7.50, 46.99, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 3, 3, NULL, '2025-07-20', 'Business Cards Holder', 2.38, 0.45, 2.83, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-21', 'Cables', 2.13, 0.41, 2.54, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-22', 'Cables', 1.95, 0.38, 2.33, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-26', 'Dynabook Adapter', 12.34, 2.34, 14.68, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-26', 'Hift', 26.03, 0.78, 26.81, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 23, 3, NULL, '2025-07-20', 'Camera Surveillance', 31.60, 6.00, 37.60, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-20', 'Cables', 4.85, 0.92, 5.77, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-20', 'Adapter', 3.98, 0.76, 4.74, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 98, 10, 3, NULL, '2025-07-20', 'Timer', 3.71, 0.70, 4.41, 'China', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Car Rental (Greece)
(1, 119, 27, 2, NULL, '2025-08-13', 'Business Travelling', 372.55, 89.42, 461.97, 'Greece', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Travel (Greece)
(1, 120, 12, 2, NULL, '2025-06-24', 'Air Ticket - Business Travelling', 245.97, 0.00, 245.97, 'Greece', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Outfit
(1, 35, 8, 1, NULL, '2025-07-09', 'Outfit', 20.92, 3.98, 24.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 35, 8, 1, NULL, '2025-06-24', 'Outfit', 34.00, 6.46, 40.46, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Hardware (Germany)
(1, 121, 10, 2, NULL, '2025-08-11', 'Nixdorf Monitors', 36.73, 15.00, 51.73, 'Germany', NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Health & Safety
(1, 104, 25, 1, '987976', '2025-07-31', 'Pharmacy', 47.52, 2.38, 49.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE());


-- Vassos Eliades (Coffee)
INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
(1, 67, 14, 1, '102RS25025337', '2025-08-03', 'Nespresso pop up', 15.81, 0.79, 16.60, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 67, 14, 1, '102RS25028567', '2025-08-31', 'Nespresso pop up', 22.95, 1.15, 24.10, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 67, 14, 1, '102RS25022775', '2025-07-12', 'Nespresso pop up', 17.10, 0.85, 17.95, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Stationery
(1, 68, 3, 1, 'CA50052971', '2025-08-29', 'Stationery', 4.46, 0.85, 5.31, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 4, 3, 1, '922652325', '2025-06-20', 'Stationery', 15.03, 0.45, 15.48, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 4, 3, 1, '922660126', '2025-08-28', 'Stationery', 51.66, 1.55, 53.21, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Restaurants & Bar
(1, 122, 7, 1, '141873', '2025-06-11', 'Meeting', 42.11, 3.79, 45.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 123, 7, 1, '135188', '2025-06-18', 'Meeting', 11.24, 0.56, 11.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 15, 7, 1, NULL, '2025-06-09', 'Meeting', 27.34, 2.46, 29.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 124, 7, 1, NULL, '2025-06-24', 'Meeting', 60.09, 5.41, 65.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 125, 7, 1, NULL, '2025-06-09', 'Meeting', 70.18, 6.32, 76.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 126, 7, 1, NULL, '2025-06-08', 'Meeting', 15.60, 1.40, 17.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 77, 7, 1, NULL, '2025-08-26', 'Meeting', 30.28, 2.72, 33.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 28, 7, 1, NULL, '2025-08-02', 'Meeting', 7.25, 0.65, 7.90, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 71, 7, 1, NULL, '2025-07-06', 'Meeting', 58.99, 5.31, 64.30, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-08-23', 'Meeting', 43.63, 3.73, 47.36, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-08-28', 'Meeting', 43.31, 3.81, 47.12, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2025-07-12', 'Meeting', 87.71, 7.89, 95.60, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2025-07-21', 'Meeting', 113.57, 10.23, 123.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 122, 7, 1, NULL, '2025-07-05', 'Meeting', 48.62, 4.38, 53.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 127, 7, 1, NULL, '2025-07-11', 'Meeting', 42.27, 2.23, 44.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 128, 7, 1, NULL, '2025-07-26', 'Meeting', 64.04, 5.76, 69.80, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Gifts
(1, 77, 13, 1, NULL, '2025-07-03', 'Customer Gifts', 9.17, 0.83, 10.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 36, 13, 1, NULL, '2025-08-26', 'Customer Gifts', 9.24, 1.76, 11.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 88, 13, 1, NULL, '2025-07-15', 'Customer Gifts', 126.05, 23.95, 150.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 129, 13, 1, '4022', '2025-08-12', 'Customer Gifts', 51.28, 8.72, 60.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 130, 13, 1, '5090100010834', '2025-07-31', 'Customer Gifts', 39.83, 7.57, 47.40, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 63, 13, 1, NULL, '2025-08-09', 'Customer Gifts', 40.50, 9.50, 50.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 85, 13, 1, NULL, '2025-08-09', 'Customer Gifts', 16.39, 3.11, 19.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 7, 13, 1, '7320450581', '2025-08-12', 'Customer Gifts', 20.29, 3.86, 24.15, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 130, 13, 1, '5090100011062', '2025-08-12', 'Customer Gifts', 33.19, 6.31, 39.50, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 130, 13, 1, '5090100011016', '2025-08-09', 'Customer Gifts', 91.60, 17.40, 109.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 93, 13, 1, '2427', '2025-07-10', 'Customer Gifts', 84.00, 15.96, 99.96, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 131, 13, 1, 'CAI25/2904', '2025-06-25', 'Customer Gifts', 29.75, 5.65, 35.40, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Maintenance
(1, 132, 29, 1, '39561', '2025-08-07', 'Maintenance - breakdown', 40.50, 9.50, 50.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 133, 29, 1, '2938', '2025-08-25', 'Cleaning', 11.40, 0.60, 12.00, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Kitchen/Bath Equipment (Leroy Merlin)
(1, 90, 28, 1, '9809', '2025-07-17', 'Kitchen Equipment', 110.49, 21.00, 131.49, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 90, 28, 1, '9810', '2025-07-17', 'Bath Equipment', 22.91, 4.35, 27.26, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- PC Equipment (Super Home Center)
(1, 16, 10, 1, 'P207000274370', '2025-08-31', 'PC Equipment', 17.10, 3.25, 20.35, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 16, 10, 1, 'P207000273392', '2025-08-26', 'PC Equipment', 13.51, 2.57, 16.08, NULL, NULL, 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- ============================================================
-- OUTDATED SECTION: Late purchases reported in Period 6
-- InvoiceDate is from Period 5 (Apr-May 2025), VatSubmissionPeriodId = 6
-- ============================================================
(1, 9, 7, 1, NULL, '2025-04-30', 'Meeting', 72.48, 6.52, 79.00, NULL, 'Outdated - reported in Period 6', 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 151, 7, 1, NULL, '2025-04-09', 'Meeting', 31.35, 6.05, 42.40, NULL, 'Outdated - reported in Period 6', 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 76, 7, 1, NULL, '2025-05-10', 'Meeting', 21.13, 1.77, 22.90, NULL, 'Outdated - reported in Period 6', 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),
(1, 152, 7, 1, NULL, '2025-05-10', 'Meeting', 25.71, 1.29, 27.00, NULL, 'Outdated - reported in Period 6', 0, NULL, 6, GETUTCDATE(), GETUTCDATE()),

-- Outdated: Jamie Oliver from May (Period 5)
(1, 15, 7, 1, NULL, '2025-05-11', 'Meeting', 308.72, 27.78, 336.50, NULL, 'Outdated - reported in Period 6', 0, NULL, 6, GETUTCDATE(), GETUTCDATE());
