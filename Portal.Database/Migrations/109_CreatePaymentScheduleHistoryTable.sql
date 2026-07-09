/*
    Migration: 109_CreatePaymentScheduleHistoryTable
    Description: Creates the [revenue].PaymentScheduleHistory table — an audit trail of all
                 schedule modifications. Each row captures a single field change with old/new
                 values, the identity of the user who made the change, and the UTC timestamp.

    Requirements: 10.4 - THE Portal_Database SHALL include a PaymentScheduleHistory table
                         capturing: field changed, old value, new value, changed by user
                         identity, and changed at timestamp
                  10.6 - THE Portal_Database SHALL include CreatedAtUtc columns with GETUTCDATE()
                         defaults on all new tables

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue'
      AND TABLE_NAME = 'PaymentScheduleHistory'
)
BEGIN
    CREATE TABLE [revenue].[PaymentScheduleHistory]
    (
        [Id]                  INT            IDENTITY(1,1)  NOT NULL,
        [PaymentScheduleId]   INT                           NOT NULL,
        [FieldChanged]        NVARCHAR(100)                 NOT NULL,
        [OldValue]            NVARCHAR(500)                 NULL,
        [NewValue]            NVARCHAR(500)                 NULL,
        [ChangedByUserId]     NVARCHAR(450)                 NOT NULL,
        [ChangedAtUtc]        DATETIME2                     NOT NULL  CONSTRAINT [DF_PSHistory_ChangedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PaymentScheduleHistory] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PSHistory_PaymentSchedule] FOREIGN KEY ([PaymentScheduleId])
            REFERENCES [revenue].[PaymentSchedule] ([Id])
    );
END
GO

-- Non-clustered index on PaymentScheduleId for efficient history lookups by schedule
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_PSHistory_PaymentScheduleId'
      AND [object_id] = OBJECT_ID('[revenue].[PaymentScheduleHistory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PSHistory_PaymentScheduleId]
        ON [revenue].[PaymentScheduleHistory] ([PaymentScheduleId])
        INCLUDE ([ChangedAtUtc]);
END
GO
