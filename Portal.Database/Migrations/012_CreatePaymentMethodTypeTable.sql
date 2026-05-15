/*
    Migration: 012_CreatePaymentMethodTypeTable
    Description: Creates the [revenue].PaymentMethodType reference table — a system-wide
                 lookup table defining accepted payment methods.
                 This is a shared reference table with no BusinessId column.
                 Includes an IsActive column to allow soft-disabling of payment methods.

    Requirements: 6.2 - THE Portal_Database SHALL contain a [revenue].PaymentMethodType
                         reference table with columns: Id (PK, int), Name (nvarchar, required),
                         IsActive (bit, default 1) seeded with values: Cash (1),
                         BankTransfer (2), Card (3), Cheque (4), Other (5)

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentMethodType'
)
BEGIN
    CREATE TABLE [revenue].[PaymentMethodType]
    (
        [Id]        INT            NOT NULL,
        [Name]      NVARCHAR(50)   NOT NULL,
        [IsActive]  BIT            NOT NULL  CONSTRAINT [DF_PaymentMethodType_IsActive] DEFAULT 1,

        CONSTRAINT [PK_PaymentMethodType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentMethodType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 1)
        INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (1, 'Cash', 1);

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 2)
        INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (2, 'BankTransfer', 1);

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 3)
        INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (3, 'Card', 1);

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 4)
        INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (4, 'Cheque', 1);

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentMethodType] WHERE [Id] = 5)
        INSERT INTO [revenue].[PaymentMethodType] ([Id], [Name], [IsActive]) VALUES (5, 'Other', 1);
END
GO
