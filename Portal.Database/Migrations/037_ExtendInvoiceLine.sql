/*
    Migration: 037_ExtendInvoiceLine
    Description: Extends [invoice].[InvoiceLine] with additional columns to support
                 VAT rate, discount, cost price, reference URL, subtitle, and section
                 assignment. These columns mirror the QuotationLine structure and enable
                 section-based invoice presentation.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'VatRate'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [VatRate] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_InvoiceLine_VatRate] DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'Discount'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [Discount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_InvoiceLine_Discount] DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'DiscountType'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [DiscountType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_InvoiceLine_DiscountType] DEFAULT ('Percentage');
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'CostPrice'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [CostPrice] DECIMAL(18,2) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'ReferenceUrl'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [ReferenceUrl] NVARCHAR(2048) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'Subtitle'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [Subtitle] NVARCHAR(500) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[invoice].[InvoiceLine]')
      AND name = N'InvoiceSectionId'
)
BEGIN
    ALTER TABLE [invoice].[InvoiceLine]
        ADD [InvoiceSectionId] INT NULL;
END
GO
