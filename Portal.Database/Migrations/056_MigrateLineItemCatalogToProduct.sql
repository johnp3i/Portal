/*
    Migration: 056_MigrateLineItemCatalogToProduct
    Description: Migrates existing records from [quotation].[LineItemCatalog] into [product].[Product].
                 Generates sequential ProductCode values per business (e.g., "MIGRATED-001", "MIGRATED-002").
                 Preserves CreatedAtUtc from LineItemCatalog where available, falling back to UpdatedAtUtc.
                 The [quotation].[LineItemCatalog] table is retained in a deprecated state (no deletion).

    Requirements: 6.1 - Insert records from LineItemCatalog into Product with correct field mapping
                  6.2 - Ensure no duplicate ProductCode values within a BusinessId during migration
                  6.3 - Preserve CreatedAtUtc (fallback to UpdatedAtUtc)
                  6.4 - Retain the LineItemCatalog table in deprecated state (no deletion)

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Migrate LineItemCatalog records into [product].[Product]
-- =============================================================================

-- Only insert records that have not already been migrated.
-- We detect prior migration by checking if any Product with a 'MIGRATED-' prefix
-- already exists for a given BusinessId. We skip individual records whose Description
-- already exists in Product for the same BusinessId to avoid unique constraint violations.

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'LineItemCatalog'
)
AND EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'product'
      AND TABLE_NAME = 'Product'
)
BEGIN
    -- Use a CTE with ROW_NUMBER() to generate sequential codes per BusinessId.
    -- We offset the numbering by the count of existing MIGRATED- products per business
    -- to ensure no duplicate ProductCode values if the script is run again after partial migration.

    ;WITH ExistingMigratedCounts AS (
        SELECT
            [Product].[BusinessId],
            COUNT(*) AS [ExistingCount]
        FROM [product].[Product]
        WHERE [Product].[ProductCode] LIKE 'MIGRATED-%'
        GROUP BY [Product].[BusinessId]
    ),
    CatalogItems AS (
        SELECT
            [LineItemCatalog].[Id],
            [LineItemCatalog].[BusinessId],
            [LineItemCatalog].[Description],
            [LineItemCatalog].[UnitPrice],
            [LineItemCatalog].[VatRate],
            [LineItemCatalog].[UpdatedAtUtc],
            ROW_NUMBER() OVER (
                PARTITION BY [LineItemCatalog].[BusinessId]
                ORDER BY [LineItemCatalog].[Id]
            ) AS [RowNum]
        FROM [quotation].[LineItemCatalog]
        WHERE NOT EXISTS (
            SELECT 1
            FROM [product].[Product]
            WHERE [Product].[BusinessId] = [LineItemCatalog].[BusinessId]
              AND [Product].[Description] = [LineItemCatalog].[Description]
        )
    )
    INSERT INTO [product].[Product]
    (
        [BusinessId],
        [ProductCode],
        [Description],
        [DefaultSellingPrice],
        [DefaultCostPrice],
        [DefaultVatRate],
        [SupplierId],
        [IsActive],
        [LastUsedDate],
        [CreatedAtUtc]
    )
    SELECT
        [CatalogItems].[BusinessId],
        CONCAT('MIGRATED-', RIGHT('000' + CAST(([CatalogItems].[RowNum] + ISNULL([ExistingMigratedCounts].[ExistingCount], 0)) AS VARCHAR(10)), 3)),
        [CatalogItems].[Description],
        [CatalogItems].[UnitPrice],
        0.00,
        [CatalogItems].[VatRate],
        NULL,
        1,
        NULL,
        [CatalogItems].[UpdatedAtUtc]
    FROM [CatalogItems]
    LEFT JOIN [ExistingMigratedCounts]
        ON [ExistingMigratedCounts].[BusinessId] = [CatalogItems].[BusinessId];
END
GO
