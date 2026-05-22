-- ============================================================
-- Period 4: December 2024 - February 2025
-- VatSubmissionPeriodId = 4
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Staff Gym
(1, 1, 1, 1, NULL, '2024-12-23', 'Staff Gym', 140.95, 7.05, 148.00, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),
(1, 1, 1, 1, NULL, '2024-12-10', 'Staff Gym', 60.76, 3.04, 63.80, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (EU - Ireland)
(1, 17, 9, 2, NULL, '2024-12-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2025-01-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2025-02-28', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Business Meeting
(1, 9, 7, 1, NULL, '2024-10-16', 'Meeting', 21.56, 1.94, 23.50, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Gift
(1, 36, 13, 1, NULL, '2025-02-13', 'Gift', 8.40, 1.60, 10.00, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Outfit
(1, 35, 8, 1, NULL, '2025-02-22', 'Outfit', 21.82, 4.14, 25.96, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Office Supplies
(1, 37, 3, 1, NULL, '2025-02-17', '3 Penguin Brochure Stands', 100.00, 19.00, 119.00, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Outfit (EU - Germany)
(1, 38, 8, 2, NULL, '2024-12-02', 'Outfit', 66.93, 15.00, 81.93, 'Germany', NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Gifts
(1, 39, 13, 1, NULL, '2025-02-28', 'Gifts', 16.02, 3.04, 19.06, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE()),

-- Conference
(1, 40, 19, 1, NULL, '2024-12-02', 'Horeca In Action Attendance', 1000.00, 190.00, 1190.00, NULL, NULL, 0, NULL, 4, GETUTCDATE(), GETUTCDATE());
