-- ============================================================
-- Migration 185: Seed external_platform_import PlanFeature
-- ============================================================
-- Purpose: Grants the External Platform Sales Import module to the
--          Professional and Enterprise tiers (excluded on Foundation).
--          Distinct from zreport_import (POS Sales Invoice Import).
-- Idempotent — safe to run multiple times.
-- ============================================================

USE [Portal]
GO

INSERT INTO [dbo].[PlanFeature] ([PlanId], [ModuleName], [IsIncluded], [AccessLevel])
SELECT [Plan].[Id], 'external_platform_import', 1, 'full'
FROM [dbo].[Plan]
WHERE [Plan].[Name] IN ('Professional', 'Enterprise')
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[PlanFeature]
        WHERE [PlanFeature].[PlanId] = [Plan].[Id]
          AND [PlanFeature].[ModuleName] = 'external_platform_import'
  );
GO
