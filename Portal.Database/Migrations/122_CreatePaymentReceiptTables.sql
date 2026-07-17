-- ============================================================
-- Create Payment Receipt tables for formal receipt generation
-- ============================================================

USE [Portal]
GO

-- PaymentReceipt: header record for each receipt document
CREATE TABLE [revenue].[PaymentReceipt]
(
    [Id]                        INT             IDENTITY(1,1)   NOT NULL,
    [BusinessId]                INT                             NOT NULL,
    [ReceiptNumber]             NVARCHAR(50)                    NOT NULL,
    [CustomerId]                INT                             NOT NULL,
    [PaymentId]                 INT                             NOT NULL,
    [ReceiptDate]               DATETIME                        NOT NULL,
    [TotalAmountReceived]       DECIMAL(18,2)                   NOT NULL,
    [OutstandingBalanceAfter]   DECIMAL(18,2)                   NOT NULL,
    [PaymentMethodTypeId]       INT                             NOT NULL,
    [PaymentReference]          NVARCHAR(200)                   NULL,
    [Notes]                     NVARCHAR(500)                   NULL,
    [SignatureId]               INT                             NULL,
    [IsVoided]                  BIT                             NOT NULL    CONSTRAINT [DF_PaymentReceipt_IsVoided] DEFAULT (0),
    [CreatedByUserId]           NVARCHAR(450)                   NOT NULL,
    [CreatedAtUtc]              DATETIME                        NOT NULL    CONSTRAINT [DF_PaymentReceipt_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_PaymentReceipt] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PaymentReceipt_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
    CONSTRAINT [FK_PaymentReceipt_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer] ([Id]),
    CONSTRAINT [FK_PaymentReceipt_Payment] FOREIGN KEY ([PaymentId]) REFERENCES [revenue].[Payment] ([Id]),
    CONSTRAINT [FK_PaymentReceipt_PaymentMethodType] FOREIGN KEY ([PaymentMethodTypeId]) REFERENCES [revenue].[PaymentMethodType] ([Id]),
    CONSTRAINT [FK_PaymentReceipt_Signature] FOREIGN KEY ([SignatureId]) REFERENCES [portal].[Signature] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_PaymentReceipt_BusinessId_CustomerId]
    ON [revenue].[PaymentReceipt] ([BusinessId], [CustomerId]);
GO

CREATE NONCLUSTERED INDEX [IX_PaymentReceipt_PaymentId]
    ON [revenue].[PaymentReceipt] ([PaymentId]);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_PaymentReceipt_ReceiptNumber_BusinessId]
    ON [revenue].[PaymentReceipt] ([BusinessId], [ReceiptNumber]);
GO

-- PaymentReceiptLine: one row per invoice covered by the receipt
CREATE TABLE [revenue].[PaymentReceiptLine]
(
    [Id]                        INT             IDENTITY(1,1)   NOT NULL,
    [PaymentReceiptId]          INT                             NOT NULL,
    [PaymentId]                 INT                             NOT NULL,
    [InvoiceId]                 INT                             NOT NULL,
    [InvoiceNumber]             NVARCHAR(50)                    NOT NULL,
    [Amount]                    DECIMAL(18,2)                   NOT NULL,
    [InvoiceTotal]              DECIMAL(18,2)                   NOT NULL,
    [InvoiceOutstandingBefore]  DECIMAL(18,2)                   NOT NULL,
    [InvoiceOutstandingAfter]   DECIMAL(18,2)                   NOT NULL,

    CONSTRAINT [PK_PaymentReceiptLine] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PaymentReceiptLine_Receipt] FOREIGN KEY ([PaymentReceiptId]) REFERENCES [revenue].[PaymentReceipt] ([Id]),
    CONSTRAINT [FK_PaymentReceiptLine_Payment] FOREIGN KEY ([PaymentId]) REFERENCES [revenue].[Payment] ([Id]),
    CONSTRAINT [FK_PaymentReceiptLine_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_PaymentReceiptLine_PaymentReceiptId]
    ON [revenue].[PaymentReceiptLine] ([PaymentReceiptId]);
GO

-- PaymentReceiptShare: token-based sharing for receipts
CREATE TABLE [revenue].[PaymentReceiptShare]
(
    [Id]                    INT             IDENTITY(1,1)   NOT NULL,
    [PaymentReceiptId]      INT                             NOT NULL,
    [BusinessId]            INT                             NOT NULL,
    [ShareToken]            NVARCHAR(100)                   NOT NULL,
    [SnapshotHtml]          NVARCHAR(MAX)                   NOT NULL,
    [CustomerEmail]         NVARCHAR(200)                   NOT NULL,
    [ExpiresAtUtc]          DATETIMEOFFSET                  NOT NULL,
    [IsActive]              BIT                             NOT NULL    CONSTRAINT [DF_PaymentReceiptShare_IsActive] DEFAULT (1),
    [CreatedAtUtc]          DATETIMEOFFSET                  NOT NULL,
    [CreatedByUserId]       NVARCHAR(450)                   NOT NULL,

    CONSTRAINT [PK_PaymentReceiptShare] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_PaymentReceiptShare_Receipt] FOREIGN KEY ([PaymentReceiptId]) REFERENCES [revenue].[PaymentReceipt] ([Id]),
    CONSTRAINT [FK_PaymentReceiptShare_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_PaymentReceiptShare_ShareToken]
    ON [revenue].[PaymentReceiptShare] ([ShareToken]);
GO

CREATE NONCLUSTERED INDEX [IX_PaymentReceiptShare_PaymentReceiptId]
    ON [revenue].[PaymentReceiptShare] ([PaymentReceiptId])
    WHERE [IsActive] = 1;
GO
