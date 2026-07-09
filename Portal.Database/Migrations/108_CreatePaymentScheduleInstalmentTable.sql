-- ============================================================
-- Migration: 108_CreatePaymentScheduleInstalmentTable
-- Description: Creates the [revenue].PaymentScheduleInstalment table —
--              individual instalment records within a payment schedule.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'PaymentScheduleInstalment'
)
BEGIN
    CREATE TABLE [revenue].[PaymentScheduleInstalment]
    (
        [Id]                  INT            IDENTITY(1,1) NOT NULL,
        [PaymentScheduleId]   INT                          NOT NULL,
        [SequenceNumber]      INT                          NOT NULL,
        [Amount]              DECIMAL(18,2)                NOT NULL,
        [MatchedAmount]       DECIMAL(18,2)                NOT NULL CONSTRAINT [DF_PSInstalment_MatchedAmount] DEFAULT (0),
        [DueDate]             DATE                         NULL,
        [PaymentId]           INT                          NULL,
        [ParentInstalmentId]  INT                          NULL,
        [IsRemainder]         BIT                          NOT NULL CONSTRAINT [DF_PSInstalment_IsRemainder] DEFAULT (0),
        [CreatedAtUtc]        DATETIME2                    NOT NULL CONSTRAINT [DF_PSInstalment_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_PaymentScheduleInstalment] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_PSInstalment_PaymentSchedule] FOREIGN KEY ([PaymentScheduleId])
            REFERENCES [revenue].[PaymentSchedule] ([Id]),
        CONSTRAINT [FK_PSInstalment_Payment] FOREIGN KEY ([PaymentId])
            REFERENCES [revenue].[Payment] ([Id]),
        CONSTRAINT [FK_PSInstalment_ParentInstalment] FOREIGN KEY ([ParentInstalmentId])
            REFERENCES [revenue].[PaymentScheduleInstalment] ([Id])
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_PSInstalment_PaymentScheduleId'
      AND [object_id] = OBJECT_ID('[revenue].[PaymentScheduleInstalment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PSInstalment_PaymentScheduleId]
        ON [revenue].[PaymentScheduleInstalment] ([PaymentScheduleId])
        INCLUDE ([SequenceNumber], [Amount], [MatchedAmount], [DueDate]);
END
GO
