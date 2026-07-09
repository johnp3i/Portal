-- ============================================================
-- Migration: 110_AddSchedulePaymentsToPlanFeature
-- Description: Adds the schedule_payments module permission to
--              Professional and Enterprise plans, enabling the
--              Payment Schedules feature for these tiers.
-- ============================================================

USE [Portal]
GO

-- Professional plan (PlanId = 3) — full access
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[PlanFeature]
    WHERE [PlanId] = 3 AND [ModuleName] = 'schedule_payments'
)
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (3, 'schedule_payments', 1, 'full');
END
GO

-- Enterprise plan (PlanId = 4) — full access
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[PlanFeature]
    WHERE [PlanId] = 4 AND [ModuleName] = 'schedule_payments'
)
BEGIN
    INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
    VALUES (4, 'schedule_payments', 1, 'full');
END
GO
