/*
    Migration: 030_CreateLineItemCatalogTable
    Description: Creates [quotation].[LineItemCatalog] — a per-business library of reusable
                 line item templates, automatically populated when quotations transition to
                 "Sent" or "Accepted" status. Supports autocomplete search and catalog management.

    Requirements: 1.3, 1.5, 8.2

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [quotation].[LineItemCatalog]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'LineItemCatalog'
)
BEGIN
    CREATE TABLE [quotation].[LineItemCatalog]
    (
        [Id]            INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]    INT                             NOT NULL,
        [Description]   NVARCHAR(500)                   NOT NULL,
        [UnitPrice]     DECIMAL(18,2)                   NOT NULL,
        [VatRate]       DECIMAL(5,2)                    NOT NULL,
        [ReferenceUrl]  NVARCHAR(2048)                  NULL,
        [Discount]      DECIMAL(18,2)                   NOT NULL    CONSTRAINT [DF_LineItemCatalog_Discount] DEFAULT (0),
        [DiscountType]  NVARCHAR(20)                    NOT NULL    CONSTRAINT [DF_LineItemCatalog_DiscountType] DEFAULT ('Percentage'),
        [UpdatedAtUtc]  DATETIME2                       NOT NULL    CONSTRAINT [DF_LineItemCatalog_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT [PK_LineItemCatalog] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_LineItemCatalog_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [UQ_LineItemCatalog_Business_Description] UNIQUE ([BusinessId], [Description])
    );
END
GO

-- =============================================================================
-- 2. Create index on BusinessId
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_LineItemCatalog_BusinessId'
      AND [object_id] = OBJECT_ID('[quotation].[LineItemCatalog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LineItemCatalog_BusinessId]
        ON [quotation].[LineItemCatalog] ([BusinessId]);
END
GO

-- =============================================================================
-- 3. Create composite index on BusinessId + Description (supports search queries)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_LineItemCatalog_BusinessId_Description'
      AND [object_id] = OBJECT_ID('[quotation].[LineItemCatalog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_LineItemCatalog_BusinessId_Description]
        ON [quotation].[LineItemCatalog] ([BusinessId], [Description]);
END
GO
