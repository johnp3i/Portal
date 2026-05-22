-- ============================================================
-- Period 5: March 2025 - May 2025
-- VatSubmissionPeriodId = 5
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Software Subscriptions (EU - Ireland)
(1, 17, 9, 2, '515765660', '2025-03-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, '5234629668', '2025-04-30', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, '5265229646', '2025-05-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Developer Tools (Non-EU)
(1, 42, 9, 3, 'RCD41500309', '2025-03-18', 'Themes', 49.29, 0.00, 49.29, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Domains & Server (Non-EU - Database Mart)
(1, 41, 11, 3, '663688', '2025-04-10', 'Domain - mysunbed.us', 15.41, 0.00, 15.41, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '205720', '2025-04-15', 'Server Subscription - web server', 212.46, 0.00, 212.46, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '207666', '2025-04-13', 'Server Subscription - email server', 72.34, 0.00, 72.34, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '667166', '2025-05-21', 'Rapid SSL - 3inventors.com', 35.48, 0.00, 35.48, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '228491', '2025-05-13', 'Server Subscription - email server', 73.44, 0.00, 73.44, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '226451', '2025-05-10', 'Server Subscription - web server', 221.43, 0.00, 221.43, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 11, 3, '662001', '2025-03-21', 'Domain - ineedaharley.com', 15.41, 0.00, 15.41, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '189994', '2025-03-17', 'Server Subscription - web server', 228.28, 0.00, 228.28, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 41, 9, 3, '189995', '2025-03-17', 'Server Subscription - email server', 75.45, 0.00, 75.45, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Domain (Non-EU - GoDaddy)
(1, 23, 11, 3, '3653183797', '2025-04-10', 'Domain - mysunbed.eu', 10.99, 2.09, 13.08, 'USA', NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Domains (University of Cyprus)
(1, 43, 11, 1, '108698', '2025-04-19', 'Domain - mysunbed, sunbed', 100.84, 19.16, 120.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 43, 11, 1, '108099', '2025-03-21', 'Domain - ihm.com.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 43, 11, 1, '109054', '2025-05-05', 'Domain - elecessentials.com.cy', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Hardware
(1, 21, 10, 1, 'INV/2025/00936', '2025-05-30', 'Hardware - OCC Limassol', 588.00, 111.72, 699.72, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Conference
(1, 40, 19, 1, '1250038', '2025-04-10', 'Technology in Action 2025', 1000.00, 190.00, 1190.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Office Supplies / Banners
(1, 37, 3, 1, '25/132-CON', '2025-03-02', '4 Penguin Stand Banners Frames - Technology in Action 2025', 240.00, 50.36, 290.36, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Software Subscription
(1, 44, 9, 1, '35410', '2025-03-20', 'Grammarly Subscription', 144.00, 0.00, 144.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Banners/Printing
(1, 45, 21, 1, '1811', '2025-04-02', 'Banners - Technology in Action 2025', 170.00, 32.30, 202.30, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Utilities (Electricity)
(1, 46, 17, 1, '2.32855E+13', '2025-04-08', 'Electricity 0225-0425', 76.60, 6.76, 83.36, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Telephone Bills
(1, 39, 18, 1, '2001721212-3-2025', '2025-03-31', 'Telephone', 67.74, 9.68, 77.42, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, '2001721212-4-2025', '2025-04-30', 'Telephone', 51.59, 6.61, 58.20, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 39, 18, 1, '2001721212-5-2025', '2025-05-31', 'Telephone', 54.91, 7.24, 62.15, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Internet Bills
(1, 47, 18, 1, '20275491', '2025-03-01', 'Internet', 21.00, 3.99, 24.99, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-04-01', 'Internet', 21.00, 3.99, 24.99, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 47, 18, 1, NULL, '2025-05-01', 'Internet', 21.00, 3.99, 24.99, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Car Rental
(1, 48, 27, 1, 'INV-R-05456', '2025-03-06', 'Truck Rental', 88.24, 16.76, 105.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Printing
(1, 49, 21, 1, '2500523', '2025-03-24', 'Printing - Technology in Action 2025', 93.95, 17.85, 111.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Office Equipment
(1, 50, 3, 1, '114ΒΤΔΑΡ11', '2025-04-17', 'Office Equipment', 15.71, 2.99, 18.70, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Vehicle Service
(1, 51, 22, 1, '19403', '2025-05-27', 'Motorbike Service', 58.82, 11.18, 70.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Uniform
(1, 52, 8, 1, '2320', '2025-05-19', 'T-shirts', 36.00, 6.84, 42.84, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Office Renovation
(1, 16, 20, 1, 'P202000342216', '2025-04-19', 'Office Renovation - Wood', 36.44, 6.93, 43.37, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 16, 3, 1, 'P204000336555', '2025-04-13', 'Office Equipment', 25.61, 4.87, 30.48, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 53, 20, 1, '30600', '2025-03-24', 'Office Renovation - Wood', 63.78, 12.12, 75.90, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 53, 20, 1, '37652', '2025-03-24', 'Office Renovation', 11.97, 2.29, 14.26, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 53, 20, 1, '31433', '2025-04-19', 'Office Renovation', 20.48, 3.89, 24.37, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Fuels (EKO)
(1, 5, 6, 1, '56452', '2025-03-14', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '42184', '2025-03-24', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '57090', '2025-03-16', 'Fuels', 8.34, 1.58, 9.92, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '63050', '2025-03-16', 'Fuels', 8.24, 1.56, 9.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '39887', '2025-03-20', 'Fuels', 9.35, 1.78, 11.13, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '55669', '2025-03-11', 'Fuels', 9.87, 1.88, 11.75, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '57861', '2025-03-09', 'Fuels', 12.61, 2.40, 15.01, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '33961', '2025-03-08', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '45318', '2025-03-30', 'Fuels', 7.95, 1.51, 9.46, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '77606', '2025-03-31', 'Fuels', 12.61, 2.39, 15.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '57693', '2025-04-26', 'Fuels', 8.22, 1.56, 9.78, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '55660', '2025-04-21', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '96964', '2025-04-19', 'Fuels', 9.02, 1.71, 10.73, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '73972', '2025-04-01', 'Fuels', 25.21, 4.79, 30.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '50606', '2025-04-10', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
-- Fixed date: 08/01/1900 -> January in Mar-May 2025 period context = not valid in this period, but listed under period 5
(1, 5, 6, 1, '49670', '2025-01-08', 'Fuels', 20.50, 3.90, 24.40, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '55104', '2025-04-19', 'Fuels', 25.51, 4.49, 30.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '86032', '2025-04-18', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '72147', '2025-05-25', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '71740', '2025-05-24', 'Fuels', 9.00, 1.71, 10.71, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '69449', '2025-05-20', 'Fuels', 25.78, 4.82, 30.60, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '67711', '2025-05-16', 'Fuels', 8.39, 1.60, 9.99, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '66926', '2025-05-15', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '15846', '2025-05-30', 'Fuels', 25.51, 4.49, 30.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '99468', '2025-05-07', 'Fuels', 8.29, 1.57, 9.86, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '36189', '2025-05-06', 'Fuels', 25.49, 4.84, 30.33, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '61528', '2025-05-03', 'Fuels', 10.11, 1.92, 12.03, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '64048', '2025-05-09', 'Fuels', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, '65711', '2025-05-12', 'Fuels', 8.17, 1.55, 9.72, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE());


-- Continuation of Period 5 purchases

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Supermarket (Lidl)
(1, 54, 14, 1, NULL, '2025-05-29', 'Supermarket', 7.13, 0.24, 7.37, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-23', 'Supermarket', 14.24, 0.81, 15.05, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-28', 'Supermarket', 19.07, 0.86, 19.93, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-25', 'Supermarket', 10.15, 0.51, 10.66, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-15', 'Supermarket', 5.24, 0.26, 5.50, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-24', 'Supermarket', 19.40, 1.85, 21.25, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-28', 'Supermarket', 14.12, 1.03, 15.15, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-23', 'Supermarket', 18.52, 1.14, 19.66, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-10', 'Supermarket', 10.30, 0.42, 10.72, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-17', 'Supermarket', 15.82, 1.09, 16.91, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-05', 'Supermarket', 16.16, 0.63, 16.79, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-03-19', 'Supermarket', 24.33, 1.22, 25.55, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-03', 'Supermarket', 14.26, 0.62, 14.88, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-21', 'Supermarket', 8.72, 0.87, 9.59, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-09', 'Supermarket', 13.30, 0.66, 13.96, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-13', 'Supermarket', 10.88, 0.54, 11.42, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-10', 'Supermarket', 23.69, 1.18, 24.87, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-16', 'Supermarket', 9.71, 0.39, 10.10, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-23', 'Supermarket', 15.40, 1.49, 16.89, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-11', 'Supermarket', 9.88, 0.37, 10.25, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-02', 'Supermarket', 14.68, 1.94, 16.62, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-04', 'Supermarket', 24.93, 1.19, 26.12, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-17', 'Supermarket', 21.29, 2.67, 23.96, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-12', 'Supermarket', 13.12, 1.08, 14.20, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-02', 'Supermarket', 17.57, 1.16, 18.73, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-07', 'Supermarket', 21.91, 1.53, 23.44, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-04-15', 'Supermarket', 16.31, 0.82, 17.13, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 54, 14, 1, NULL, '2025-05-20', 'Supermarket', 12.30, 0.62, 12.92, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Shipping
(1, 55, 15, 1, NULL, '2025-03-24', 'Shipping', 11.74, 2.75, 14.49, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Pop Life (Supermarket)
(1, 56, 14, 1, NULL, '2025-05-30', 'Supermarket', 9.90, 1.89, 11.79, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Metro (Supermarket) - Fixed date: 16/01/1900 -> January 2025
(1, 57, 14, 1, NULL, '2025-01-16', 'Supermarket', 18.77, 0.93, 19.70, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-05-11', 'Supermarket', 10.26, 0.51, 10.77, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-03-11', 'Supermarket', 7.94, 0.40, 8.34, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 57, 14, 1, NULL, '2025-04-17', 'Supermarket', 11.68, 0.58, 12.26, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Sklavenitis (Supermarket) - Fixed date: 28/01/1900 -> January 2025
(1, 58, 14, 1, NULL, '2025-05-24', 'Supermarket', 16.37, 2.43, 18.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-01-28', 'Supermarket', 7.68, 1.46, 9.14, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-03-19', 'Supermarket', 13.87, 0.69, 14.56, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-03-11', 'Supermarket', 11.91, 0.88, 12.79, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 58, 14, 1, NULL, '2025-05-20', 'Supermarket', 9.31, 1.18, 10.49, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Best Values
(1, 59, 14, 1, NULL, '2025-03-26', 'Supermarket', 10.66, 0.28, 10.94, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Waddah Flahaha
(1, 60, 14, 1, NULL, '2025-05-27', 'Supermarket', 4.63, 0.12, 4.75, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 60, 14, 1, NULL, '2025-05-21', 'Supermarket', 12.09, 0.55, 12.64, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Periptero Armenias
(1, 61, 14, 1, NULL, '2025-05-29', 'Supermarket', 5.12, 0.78, 5.90, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- A Xorpas
(1, 62, 14, 1, NULL, '2025-03-24', 'Supermarket', 14.62, 0.73, 15.35, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Gifts (Intersport)
(1, 63, 13, 1, N'ΛΝ-ML02_00517150', '2025-04-08', 'Customer Gifts', 58.97, 11.21, 70.18, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Uniform
(1, 64, 8, 1, '724428', '2025-03-25', 'Uniform', 58.91, 11.19, 70.10, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 7, 8, 1, '7290417747', '2025-03-25', 'Uniform', 23.27, 4.42, 27.69, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 7, 8, 1, '7290418575', '2025-04-22', 'Uniform', 33.61, 6.38, 39.99, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- S.T Karpos Zois
(1, 65, 14, 1, NULL, '2025-05-24', 'Supermarket', 6.92, 0.38, 7.30, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Gifts (Afelandra)
(1, 66, 13, 1, NULL, '2025-03-07', 'Flower', 11.76, 2.24, 14.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 66, 13, 1, NULL, '2025-04-08', 'Flower', 6.72, 1.28, 8.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Vassos Eliades (Coffee/Supermarket)
(1, 67, 14, 1, NULL, '2025-03-11', 'Coffee', 24.29, 1.21, 25.50, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 67, 14, 1, NULL, '2025-03-25', 'Coffee', 15.81, 0.79, 16.60, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 67, 14, 1, NULL, '2025-04-08', 'Coffee', 32.86, 1.64, 34.50, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Stationery (Philippides)
(1, 68, 3, 1, NULL, '2025-05-19', 'Stationery', 2.26, 0.43, 2.69, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 68, 3, 1, NULL, '2025-05-19', 'Stationery', 1.88, 0.21, 2.09, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Restaurants & Bar
(1, 69, 7, 1, NULL, '2025-03-07', 'Meeting', 188.07, 16.93, 205.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2025-04-04', 'Meeting', 28.25, 2.55, 30.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 70, 7, 1, NULL, '2025-05-30', 'Meeting', 22.01, 1.99, 24.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 71, 7, 1, NULL, '2025-03-24', 'Meeting', 58.53, 5.27, 63.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 72, 7, 1, NULL, '2025-04-26', 'Meeting', 43.12, 3.88, 47.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 71, 7, 1, NULL, '2025-03-21', 'Meeting', 40.18, 3.62, 43.80, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-03-20', 'Meeting', 28.44, 2.56, 31.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2025-05-06', 'Meeting', 50.00, 4.50, 54.50, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 15, 7, 1, NULL, '2025-04-24', 'Meeting', 198.16, 17.84, 216.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 73, 7, 1, NULL, '2025-03-30', 'Leisure', 17.43, 1.57, 19.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Fuels (R.A.M. OIL)
(1, 74, 6, 1, NULL, '2025-03-13', 'Fuels', 43.70, 8.30, 52.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-03-26', 'Fuels', 81.92, 15.57, 97.49, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-03-22', 'Fuels', 42.02, 7.98, 50.00, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-03-19', 'Fuels', 33.63, 6.39, 40.02, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- Fuels (ESSO)
(1, 75, 6, 1, NULL, '2025-03-08', 'Fuels', 6.83, 1.30, 8.13, NULL, NULL, 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),

-- ============================================================
-- OUTDATED SECTION: Late purchases reported in Period 5
-- InvoiceDate is from previous periods, VatSubmissionPeriodId = 5
-- ============================================================

-- R.A.M. OIL - Outdated (from Period 4: Dec 2024 - Feb 2025)
-- Note: CSV shows Amount without VAT as empty, VAT and Total provided. Calculating AmountExcludingVat = TotalAmount - VatAmount
(1, 74, 6, 1, NULL, '2025-01-23', 'Fuels', 42.87, 8.15, 51.02, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-01-16', 'Fuels', 42.02, 7.98, 50.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-01-08', 'Fuels', 40.34, 7.66, 48.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2024-12-02', 'Fuels', 42.45, 8.06, 50.51, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2024-12-21', 'Fuels', 42.87, 8.14, 51.01, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2024-12-30', 'Fuels', 33.62, 6.39, 40.01, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2024-12-12', 'Fuels', 43.28, 8.22, 51.50, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-02-10', 'Fuels', 46.22, 8.78, 55.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-02-18', 'Fuels', 45.38, 8.62, 54.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-02-26', 'Fuels', 42.02, 7.98, 50.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE()),
(1, 74, 6, 1, NULL, '2025-01-31', 'Fuels', 45.38, 8.62, 54.00, NULL, 'Outdated - reported in Period 5', 0, NULL, 5, GETUTCDATE(), GETUTCDATE());
