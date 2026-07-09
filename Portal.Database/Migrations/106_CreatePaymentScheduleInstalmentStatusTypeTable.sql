/*
    Migration: 106_CreatePaymentScheduleInstalmentStatusTypeTable
    Description: Creates the [revenue].PaymentScheduleInstalmentStatusType reference table — a system-wide
                 lookup table defining the lifecycle states of a Payment Schedule Instalment.
                 This is a shared reference table with no BusinessId column.

    Requirements: 10.3 - THE Portal_Database SHALL include a PaymentScheduleInstalmentStatusType
                          reference table seeded with: Pending (1), Due (2), Overdue (3),
                          Paid (4), PartiallyPaid (5)

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentScheduleInstalmentStatusType'
)
BEGIN
    CREATE TABLE [revenue].[PaymentScheduleInstalmentStatusType]
    (
        [Id]    INT            NOT NULL,
        [Name]  NVARCHAR(50)   NOT NULL,

        CONSTRAINT [PK_PaymentScheduleInstalmentStatusType] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Seed data: idempotent inserts
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentScheduleInstalmentStatusType'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalmentStatusType] WHERE [Id] = 1)
        INSERT INTO [revenue].[PaymentScheduleInstalmentStatusType] ([Id], [Name]) VALUES (1, 'Pending');

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalmentStatusType] WHERE [Id] = 2)
        INSERT INTO [revenue].[PaymentScheduleInstalmentStatusType] ([Id], [Name]) VALUES (2, 'Due');

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalmentStatusType] WHERE [Id] = 3)
        INSERT INTO [revenue].[PaymentScheduleInstalmentStatusType] ([Id], [Name]) VALUES (3, 'Overdue');

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalmentStatusType] WHERE [Id] = 4)
        INSERT INTO [revenue].[PaymentScheduleInstalmentStatusType] ([Id], [Name]) VALUES (4, 'Paid');

    IF NOT EXISTS (SELECT 1 FROM [revenue].[PaymentScheduleInstalmentStatusType] WHERE [Id] = 5)
        INSERT INTO [revenue].[PaymentScheduleInstalmentStatusType] ([Id], [Name]) VALUES (5, 'PartiallyPaid');
END
GO
