/*
    Migration: 072_AddIsReverseChargeToLines
    Description: Adds IsReverseCharge (BIT NOT NULL DEFAULT 0) to both
                 [quotation].[QuotationLine] and [invoice].[InvoiceLine].
                 Adds ProductTypeId (INT NULL) to [invoice].[InvoiceLine] with
                 a FK constraint referencing [product].[ProductType].

                 IsReverseCharge indicates the reverse charge VAT mechanism applies,
                 forcing VatRate to 0% on that line. ProductTypeId on InvoiceLine is
                 an immutable snapshot captured during quotation-to-invoice conversion.

    Requirements: 5.1 - IsReverseCharge column on QuotationLine (BIT NOT NULL DEFAULT 0)
                  6.1 - IsReverseCharge column on InvoiceLine (BIT NOT NULL DEFAULT 0)
                  8.4 - Migration is idempotent (safe to run multiple times)

    This script is idempotent — safe to run multiple times.
*/

-- Step 1: Add IsReverseCharge to [quotation].[QuotationLine]
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'QuotationLine'
      AND COLUMN_NAME = 'IsReverseCharge'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [IsReverseCharge] BIT NOT NULL
            CONSTRAINT [DF_QuotationLine_IsReverseCharge] DEFAULT (0);
END
GO

-- Step 2: Add IsReverseCharge to [invoice].[InvoiceLine]
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
      AND COLUMN_NAME = 'IsReverseCharge'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [IsReverseCharge] BIT NOT NULL
            CONSTRAINT [DF_InvoiceLine_IsReverseCharge] DEFAULT (0);
END
GO

-- Step 3: Add ProductTypeId to [invoice].[InvoiceLine] (snapshot from conversion)
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'invoice'
      AND TABLE_NAME = 'InvoiceLine'
      AND COLUMN_NAME = 'ProductTypeId'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [ProductTypeId] INT NULL;
END
GO

-- Step 4: Add FK constraint on InvoiceLine.ProductTypeId referencing [product].[ProductType]
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_InvoiceLine_ProductType'
      AND [parent_object_id] = OBJECT_ID('[invoice].[InvoiceLine]')
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD CONSTRAINT [FK_InvoiceLine_ProductType]
            FOREIGN KEY ([ProductTypeId])
            REFERENCES [product].[ProductType] ([Id]);
END
GO
