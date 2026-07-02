-- ============================================================
-- Create PaymentReminderLog table
-- ============================================================

USE [Portal]
GO

CREATE TABLE [reminder].[PaymentReminderLog] (
    [Id]                  INT IDENTITY(1,1) NOT NULL,
    [BusinessId]          INT NOT NULL,
    [InvoiceId]           INT NOT NULL,
    [CustomerId]          INT NOT NULL,
    [RecipientEmail]      NVARCHAR(200) NOT NULL,
    [EscalationTier]      VARCHAR(20) NOT NULL,
    [IsSentSuccessfully]  BIT NOT NULL,
    [ErrorMessage]        NVARCHAR(1000) NULL,
    [IsManualTrigger]     BIT NOT NULL DEFAULT 0,
    [SentAtUtc]           DATETIME NOT NULL DEFAULT GETUTCDATE(),
    [CreatedAtUtc]        DATETIME NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_PaymentReminderLog] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PaymentReminderLog_Business] FOREIGN KEY ([BusinessId])
        REFERENCES [portal].[Business]([Id]),
    CONSTRAINT [FK_PaymentReminderLog_Invoice] FOREIGN KEY ([InvoiceId])
        REFERENCES [invoice].[Invoice]([Id]),
    CONSTRAINT [FK_PaymentReminderLog_Customer] FOREIGN KEY ([CustomerId])
        REFERENCES [customer].[Customer]([Id]),
    CONSTRAINT [CK_PaymentReminderLog_EscalationTier]
        CHECK ([EscalationTier] IN ('Friendly', 'Firm', 'Formal'))
)
GO

CREATE INDEX [IX_PaymentReminderLog_BusinessId_InvoiceId]
    ON [reminder].[PaymentReminderLog]([BusinessId], [InvoiceId])
GO

CREATE INDEX [IX_PaymentReminderLog_BusinessId_SentAtUtc]
    ON [reminder].[PaymentReminderLog]([BusinessId], [SentAtUtc])
GO
