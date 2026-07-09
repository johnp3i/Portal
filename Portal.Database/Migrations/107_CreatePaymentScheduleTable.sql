/*
    Migration: 107_CreatePaymentScheduleTable
    Description: Creates the [revenue].PaymentSchedule table — a structured instalment plan
                 attached to an invoice. Only one active schedule may exist per invoice at any
                 time, enforced by a unique filtered index on InvoiceId WHERE IsActive = 1.
                 Scoped to a Business tenant with foreign keys to [portal].Business and
                 [invoice].Invoice.

    Requirements: 10.1 - THE Portal_Database SHALL store Payment_Schedule data in the [revenue] schema
                  10.2 - THE Portal_Database SHALL enforce a unique constraint ensuring only one
                         active Payment_Schedule exists per invoice at any time
                  10.6 - THE Portal_Database SHALL include CreatedAtUtc columns with GETUTCDATE()
                         defaults on all new tables
                  10.7 - THE Portal_Database SHALL include a foreign key from PaymentSchedule to
                         [invoice].Invoice with cascade restrictions preventing orphaned schedules

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentSchedule'
)
BEGIN
    CREATE TABLE [revenue].[PaymentSchedule]
    (
        [Id]              INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]      INT                           NOT NULL,
        [InvoiceId]       INT                           NOT NULL,
        [IsActive]        BIT                           NOT NULL  CONSTRAINT [DF_PaymentSchedule_IsActive] DEFAULT (1),
        [CreatedAtUtc]    DATETIME2                     NOT NULL  CONSTRAINT [DF_PaymentSchedule_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [CreatedByUserId] NVARCHAR(450)                 NULL,

        CONSTRAINT [PK_PaymentSchedule] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PaymentSchedule_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_PaymentSchedule_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id])
    );
END
GO

-- Unique filtered index: only one active schedule per invoice
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_PaymentSchedule_InvoiceId_Active'
      AND [object_id] = OBJECT_ID('[revenue].[PaymentSchedule]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentSchedule_InvoiceId_Active]
        ON [revenue].[PaymentSchedule] ([InvoiceId])
        WHERE [IsActive] = 1;
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PaymentSchedule_BusinessId'
      AND [object_id] = OBJECT_ID('[revenue].[PaymentSchedule]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentSchedule_BusinessId]
        ON [revenue].[PaymentSchedule] ([BusinessId]);
END
GO
