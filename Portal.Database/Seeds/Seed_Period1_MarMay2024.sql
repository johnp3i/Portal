-- ============================================================
-- Period 1: March 2024 - May 2024
-- VatSubmissionPeriodId = 1
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Staff Gym
(1, 1, 1, 1, NULL, '2024-03-05', 'Staff Gym', 60.57, 3.03, 63.60, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 1, 1, 1, NULL, '2024-04-08', 'Staff Gym', 60.57, 3.03, 63.60, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 1, 1, 1, NULL, '2024-05-07', 'Staff Gym', 60.76, 3.04, 63.80, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Office Furniture (Bazaraki)
(1, 2, 2, 1, NULL, '2024-03-25', 'Titan Chair', 50.00, 0.00, 50.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 2, 3, 1, NULL, '2024-03-28', 'Whiteboard', 50.00, 0.00, 50.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Parking
(1, 3, 4, 1, NULL, '2024-05-27', 'Parking', 2.52, 0.48, 3.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Books
(1, 4, 5, 1, NULL, '2024-05-21', 'Books', 20.62, 0.62, 21.24, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Fuels
(1, 5, 6, 1, NULL, '2024-02-14', 'Fuels', 37.76, 7.17, 44.93, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2024-03-28', 'Fuels', 42.45, 8.07, 50.52, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2024-03-14', 'Fuels', 41.18, 7.82, 49.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 5, 6, 1, NULL, '2024-02-20', 'Fuels', 31.88, 6.06, 37.94, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Business Meetings
(1, 6, 7, 1, NULL, '2024-04-24', 'Business Meeting', 58.71, 5.19, 63.90, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 8, 7, 1, NULL, '2024-03-10', 'Business Meeting', 13.12, 1.18, 14.30, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 9, 7, 1, NULL, '2024-03-01', 'Business Meeting', 29.82, 2.68, 32.50, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 11, 7, 1, NULL, '2024-05-19', 'Business Meeting', 15.51, 1.39, 16.90, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 12, 7, 1, NULL, '2024-05-18', 'Business Meeting', 35.32, 3.18, 38.50, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 13, 7, 1, NULL, '2024-05-17', 'Business Meeting', 40.55, 3.65, 44.20, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 14, 7, 1, NULL, '2024-05-18', 'Business Meeting', 14.50, 1.30, 15.80, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 15, 7, 1, NULL, '2024-05-31', 'Business Meeting', 88.17, 7.93, 96.10, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Staff Outfit
(1, 7, 8, 1, NULL, '2024-03-23', 'Staff Outfit', 11.34, 2.16, 13.50, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 10, 8, 1, NULL, '2024-03-05', 'Staff Outfit', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Suitcase/Travel
(1, 16, 12, 1, NULL, '2024-05-21', 'Suitcase', 38.33, 7.23, 45.56, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (EU Reverse Charge - Ireland)
(1, 17, 9, 2, NULL, '2024-05-09', 'Google Workspace Subscription', 5.11, 0.00, 5.11, 'Ireland', NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (Non-EU)
(1, 18, 9, 3, NULL, '2024-04-17', 'Website Theme', 35.79, 0.00, 35.79, 'USA', NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (Non-EU - Australia)
(1, 19, 9, 3, NULL, '2024-04-28', 'Canva Design Subscription - Yearly', 92.43, 17.56, 109.99, 'Australia', NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Hardware
(1, 20, 10, 1, NULL, '2024-04-15', 'Hardware', 31.76, 6.04, 37.80, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, NULL, '2024-05-08', 'Hardware', 515.51, 97.95, 613.46, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, NULL, '2024-04-02', 'Hardware', 90.50, 17.20, 107.70, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 21, 10, 1, NULL, '2024-03-12', 'Hardware', 172.00, 32.68, 204.68, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),

-- Domains
(1, 22, 11, 1, NULL, '2024-04-16', 'Domain - mysunbed.com.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 22, 11, 1, NULL, '2024-04-17', 'Domain - mysunbed.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 22, 11, 1, NULL, '2024-04-18', 'Domain - sunbed.com.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 22, 11, 1, NULL, '2024-04-19', 'Domain - sunbed.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 22, 11, 1, NULL, '2024-04-23', 'Domain - myparking.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 22, 11, 1, NULL, '2024-03-01', 'Domain - whitelabel.com.cy', 16.81, 3.19, 20.00, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 23, 11, 1, NULL, '2024-04-15', 'Domain - mysunbed.eu', 3.99, 0.76, 4.75, NULL, NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE()),
(1, 24, 11, 2, NULL, '2024-04-15', 'Domain - mysunbed.gr', 19.00, 4.56, 23.56, 'Greece', NULL, 0, NULL, 1, GETUTCDATE(), GETUTCDATE());
