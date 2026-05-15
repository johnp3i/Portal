-- Migration: Add ContactPerson and MobileNumber columns to Customer table
-- These columns exist in the entity but were missing from the original migration

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[customer].[Customer]')
      AND name = N'ContactPerson'
)
BEGIN
    ALTER TABLE [customer].[Customer]
        ADD [ContactPerson] NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[customer].[Customer]')
      AND name = N'MobileNumber'
)
BEGIN
    ALTER TABLE [customer].[Customer]
        ADD [MobileNumber] NVARCHAR(30) NULL;
END
GO
