-- ============================================================
-- Create [reminder] schema and PaymentReminderSchedule table
-- ============================================================

USE [Portal]
GO

CREATE SCHEMA [reminder]
GO

CREATE TABLE [reminder].[PaymentReminderSchedule] (
    [Id]                            INT IDENTITY(1,1) NOT NULL,
    [BusinessId]                    INT NOT NULL,
    [EscalationTier]                VARCHAR(20) NOT NULL,
    [DaysOffset]                    INT NOT NULL,
    [MaxRemindersPerTier]           INT NOT NULL DEFAULT 1,
    [MinIntervalDays]               INT NOT NULL DEFAULT 3,
    [PartialPaymentSuppressionDays] INT NOT NULL DEFAULT 7,
    [IsEnabled]                     BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]                  DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAtUtc]                  DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_PaymentReminderSchedule] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PaymentReminderSchedule_Business] FOREIGN KEY ([BusinessId])
        REFERENCES [portal].[Business]([Id]),
    CONSTRAINT [CK_PaymentReminderSchedule_EscalationTier]
        CHECK ([EscalationTier] IN ('Friendly', 'Firm', 'Formal'))
)
GO

CREATE INDEX [IX_PaymentReminderSchedule_BusinessId]
    ON [reminder].[PaymentReminderSchedule]([BusinessId])
GO
