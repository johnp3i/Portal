-- ============================================================
-- Migration 187: Add per-business LeadNumber to LeadRequest
-- ============================================================
-- Purpose: Introduce a human-friendly, per-business sequential lead number
--          (1, 2, 3... within each BusinessId) so the UI can reference a lead
--          without exposing the global database primary key ([Id]).
--          Existing leads are backfilled per business, ordered by creation.
-- Schema: [sales]
-- Idempotent.
-- ============================================================

USE [Portal]
GO

-- 1. Add nullable column (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest' AND COLUMN_NAME = 'LeadNumber'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest]
        ADD [LeadNumber] INT NULL;

    PRINT 'Added [LeadNumber] column to [sales].[LeadRequest].';
END
ELSE
BEGIN
    PRINT '[sales].[LeadRequest].[LeadNumber] already exists.';
END
GO

-- 2. Backfill any rows without a LeadNumber, numbering per business from the
--    current per-business max, ordered by CreatedAtUtc then Id (stable).
--    This is safe to re-run: only NULL LeadNumbers are assigned, and new
--    numbers continue from the existing per-business maximum.
IF EXISTS (
    SELECT 1 FROM [sales].[LeadRequest] WHERE [LeadNumber] IS NULL
)
BEGIN
    ;WITH ExistingMax AS
    (
        SELECT [BusinessId], MAX([LeadNumber]) AS MaxNumber
        FROM [sales].[LeadRequest]
        WHERE [LeadNumber] IS NOT NULL
        GROUP BY [BusinessId]
    ),
    ToNumber AS
    (
        SELECT
            [Id],
            [BusinessId],
            [LeadNumber],
            ROW_NUMBER() OVER (
                PARTITION BY [BusinessId]
                ORDER BY [CreatedAtUtc] ASC, [Id] ASC
            ) AS RowSeq
        FROM [sales].[LeadRequest]
        WHERE [LeadNumber] IS NULL
    )
    UPDATE ToNumber
        SET [LeadNumber] = ISNULL(em.MaxNumber, 0) + ToNumber.RowSeq
    FROM ToNumber
    LEFT JOIN ExistingMax em ON em.[BusinessId] = ToNumber.[BusinessId];

    PRINT 'Backfilled [LeadNumber] for existing leads (per business).';
END
GO

-- 3. Enforce NOT NULL after backfill
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'LeadRequest'
      AND COLUMN_NAME = 'LeadNumber' AND IS_NULLABLE = 'YES'
)
BEGIN
    ALTER TABLE [sales].[LeadRequest] ALTER COLUMN [LeadNumber] INT NOT NULL;
    PRINT 'Set [LeadNumber] NOT NULL.';
END
GO

-- 4. Unique per business (a lead number is unique within its business)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_LeadRequest_Business_LeadNumber'
      AND object_id = OBJECT_ID('[sales].[LeadRequest]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_LeadRequest_Business_LeadNumber]
        ON [sales].[LeadRequest] ([BusinessId], [LeadNumber]);
    PRINT 'Created unique index UQ_LeadRequest_Business_LeadNumber.';
END
GO
