-- ============================================================
-- Period 3: September 2024 - November 2024
-- VatSubmissionPeriodId = 3
-- ============================================================

INSERT INTO [purchase].[Purchase] (BusinessId, SupplierId, ExpenseCategoryId, PurchaseOriginTypeId, InvoiceNumber, InvoiceDate, Description, AmountExcludingVat, VatAmount, TotalAmount, Country, Notes, IsCancelled, CancelledAtUtc, VatSubmissionPeriodId, CreatedAtUtc, UpdatedAtUtc)
VALUES
-- Staff Gym
(1, 1, 1, 1, NULL, '2024-09-11', 'Staff Gym', 140.91, 7.09, 148.00, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),

-- Software Subscriptions (EU - Ireland)
(1, 17, 9, 2, NULL, '2024-09-30', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2024-10-31', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 17, 9, 2, NULL, '2024-11-30', 'Google Workspace Subscription', 6.90, 0.00, 6.90, 'Ireland', NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),

-- Labels (EU - Italy)
(1, 32, 16, 2, NULL, '2024-10-16', 'Labels', 27.40, 0.00, 27.40, 'Italy', NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 32, 16, 2, NULL, '2024-11-13', 'Labels', 212.50, 0.00, 212.50, 'Italy', NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),

-- Hardware
(1, 21, 10, 1, NULL, '2024-11-24', 'Hardware', 47.35, 9.00, 56.35, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 34, 10, 1, NULL, '2024-11-06', 'Hardware (PC)', 1302.52, 247.48, 1550.00, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),

-- SMS Services
(1, 33, 9, 1, NULL, '2024-09-16', 'SMS Package (Services)', 50.00, 9.50, 59.50, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),

-- Gifts
(1, 35, 13, 1, NULL, '2024-11-29', 'Gifts', 41.12, 7.81, 48.93, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 35, 13, 1, NULL, '2024-11-30', 'Gifts', 10.06, 1.91, 11.97, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 35, 13, 1, NULL, '2024-11-30', 'Gifts', 13.84, 2.63, 16.47, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE()),
(1, 6, 13, 1, NULL, '2024-11-30', 'Gifts', 21.85, 4.15, 26.00, NULL, NULL, 0, NULL, 3, GETUTCDATE(), GETUTCDATE());
