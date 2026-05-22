/*
    Migration: 046_MakeDescriptionOptionalOnPurchase
    Description: Makes the [Description] column on [purchase].[Purchase] nullable.
                 Description is now optional for purchase entries.

    This script is idempotent — safe to run multiple times.
*/

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'purchase'
      AND TABLE_NAME = 'Purchase'
      AND COLUMN_NAME = 'Description'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    ALTER TABLE [purchase].[Purchase]
        ALTER COLUMN [Description] NVARCHAR(500) NULL;
END
GO
