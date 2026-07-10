-- ============================================================
-- Migration: 111_RenamePlanTiersFoundationProfessional
-- Description: Renames Starter to Foundation, deactivates Business
--              and Enterprise plans, updates descriptions for the
--              two-tier public launch (Foundation + Professional).
-- ============================================================

USE [Portal]
GO

-- Rename Starter (Id=2) to Foundation
UPDATE [dbo].[Plan]
SET [Name] = 'Foundation',
    [Slug] = 'foundation',
    [Description] = 'Complete business management'
WHERE [Id] = 2
  AND [Name] = 'Starter'
GO

-- Deactivate the old Business plan (Id=1, €29/mo)
UPDATE [dbo].[Plan]
SET [IsActive] = 0
WHERE [Id] = 1
GO

-- Update Professional description (Id=3)
UPDATE [dbo].[Plan]
SET [Description] = 'Automation — the platform works for you'
WHERE [Id] = 3
GO

-- Deactivate Enterprise (Id=4) — not ready for public launch
UPDATE [dbo].[Plan]
SET [IsActive] = 0
WHERE [Id] = 4
GO
