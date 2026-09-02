-- ============================================================
-- Migration 186: Add ClosedAtUtc and LeadPriorityTypeId to LeadRequest
-- ============================================================
-- Purpose: The LeadRequest EF entity and repository queries reference
--          [ClosedAtUtc] and [LeadPriorityTypeId], but these columns were
--          missing from [sales].[LeadRequest] in some environments (the
--          original ad-hoc migrations were never applied). This consolidates
--          both additions into the numbered migration sequence so the schema
--          matches the code. Idempotent.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

-- 1. ClosedAtUtc (nullable) — set when a lead is closed (Won/Lost)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest' AND COLUMN_NAME = 'ClosedAtUtc'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD [ClosedAtUtc] DATETIME NULL;

    PRINT 'Added [ClosedAtUtc] column to [sales].[LeadRequest].';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest].[ClosedAtUtc] already exists.';
END
GO

-- 2. LeadPriorityTypeId (nullable FK) — Hot/Warm/Cold priority
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest' AND COLUMN_NAME = 'LeadPriorityTypeId'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD [LeadPriorityTypeId] INT NULL;

    PRINT 'Added [LeadPriorityTypeId] column to [sales].[LeadRequest].';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest].[LeadPriorityTypeId] already exists.';
END
GO

-- 3. FK for LeadPriorityTypeId → [sales].[LeadPriorityType]([Id]) (only if the table exists and FK missing)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadPriorityType')
   AND NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_LeadRequest_LeadPriorityType'
          AND parent_object_id = OBJECT_ID('[sales].[LeadRequest]')
   )
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD CONSTRAINT [FK_LeadRequest_LeadPriorityType]
            FOREIGN KEY ([LeadPriorityTypeId]) REFERENCES [sales].[LeadPriorityType]([Id]);

    PRINT 'Added FK_LeadRequest_LeadPriorityType.';
END
GO
