-- ============================================================
-- Period 2: June 2024 - August 2024
-- VatSubmissionPeriodId = 2
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Staff Gym
(1, 1, 1, 1, NULL, '2024-06-10', 'Staff Gym', 60.00, 3.00, 63.00, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 1, 1, 1, NULL, '2024-07-08', 'Staff Gym', 60.00, 3.00, 63.00, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 1, 1, 1, NULL, '2024-08-12', 'Staff Gym', 60.76, 3.04, 63.80, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (EU - Ireland)
(1, 17, 9, 2, NULL, '2024-06-30', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2024-07-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2024-08-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Business Meetings
(1, 25, 7, 1, NULL, '2024-08-19', 'Business Meeting', 121.74, 10.96, 132.70, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 27, 7, 1, NULL, '2024-07-28', 'Business Meeting', 18.17, 1.63, 19.80, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 28, 7, 1, NULL, '2024-08-01', 'Business Meeting', 9.91, 0.89, 10.80, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Food/Supermarket
(1, 26, 14, 1, NULL, '2024-07-27', 'Food', 9.05, 0.45, 9.50, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Books
(1, 4, 5, 1, NULL, '2024-07-17', 'Books', 10.25, 0.31, 10.56, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Gifts
(1, 29, 13, 1, NULL, '2024-07-27', 'Gift', 13.44, 2.55, 15.99, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),

-- Staff Outfit
(1, 30, 8, 1, NULL, '2024-08-03', 'Staff Outfit', 13.29, 3.71, 17.00, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE()),
(1, 31, 8, 1, NULL, '2024-08-16', 'Staff Outfit', 10.92, 2.07, 12.99, NULL, NULL, 0, NULL, 2, GETUTCDATE(), GETUTCDATE());
