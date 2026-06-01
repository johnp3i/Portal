USE [Portal];
GO

/*
    Migration: 077_CreateBillingInvoicePaymentTables
    Description: Creates [billing].[Invoice] and [billing].[Payment] tables for tracking
                 subscription billing invoices and their associated payments. The Invoice
                 table records each billing cycle charge with status tracking, while the
                 Payment table records individual payment transactions linked to invoices.
                 Includes CHECK constraints for amount validation and status enumeration,
                 foreign keys to [portal].[Business] and [billing].[Invoice], and
                 nonclustered indexes on FK columns for query performance.

    Requirements: 6.2  - billing.Invoice table with Id, BusinessId, AmountEur, PeriodStart,
                         PeriodEnd, Status, PaidAtUtc, CreatedAtUtc
                 6.3  - billing.Payment table with Id, InvoiceId, AmountEur, Method,
                         PaidAtUtc, StripePaymentIntentId, CreatedAtUtc
                 6.8  - billing.Invoice FK from BusinessId to [portal].[Business].Id
                 6.9  - billing.Payment FK from InvoiceId to billing.Invoice.Id
                 6.12 - billing.Invoice Status CHECK constraint
                 7.1  - Sequential three-digit numbering
                 7.2  - IF NOT EXISTS guards for idempotency
                 7.6  - Header comment block
                 7.7  - GO batch terminators
                 7.8  - Explicit PK constraint names

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [billing].[Invoice]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing'
      AND TABLE_NAME = 'Invoice'
)
BEGIN
    CREATE TABLE [billing].[Invoice]
    (
        [Id]              INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]      INT                             NOT NULL,
        [StripeInvoiceId] NVARCHAR(100)                   NULL,
        [AmountEur]       DECIMAL(10,2)                   NOT NULL,
        [PeriodStart]     DATETIME                        NOT NULL,
        [PeriodEnd]       DATETIME                        NOT NULL,
        [Status]          NVARCHAR(20)                    NOT NULL,
        [PaidAtUtc]       DATETIME                        NULL,
        [CreatedAtUtc]    DATETIME                        NOT NULL  CONSTRAINT [DF_BillingInvoice_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_BillingInvoice] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BillingInvoice_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_BillingInvoice_AmountEur] CHECK ([AmountEur] >= 0.00),
        CONSTRAINT [CK_BillingInvoice_Status] CHECK ([Status] IN ('draft','open','paid','void','uncollectible'))
    );
END
GO

-- =============================================================================
-- 2. Nonclustered index on Invoice.BusinessId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_BillingInvoice_BusinessId'
      AND [object_id] = OBJECT_ID('[billing].[Invoice]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BillingInvoice_BusinessId]
        ON [billing].[Invoice] ([BusinessId]);
END
GO

-- =============================================================================
-- 3. Create [billing].[Payment]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'billing'
      AND TABLE_NAME = 'Payment'
)
BEGIN
    CREATE TABLE [billing].[Payment]
    (
        [Id]                     INT             IDENTITY(1,1)   NOT NULL,
        [InvoiceId]              INT                             NOT NULL,
        [AmountEur]              DECIMAL(10,2)                   NOT NULL,
        [Method]                 NVARCHAR(50)                    NOT NULL,
        [PaidAtUtc]              DATETIME                        NOT NULL,
        [StripePaymentIntentId]  NVARCHAR(100)                   NULL,
        [CreatedAtUtc]           DATETIME                        NOT NULL  CONSTRAINT [DF_BillingPayment_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_BillingPayment] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BillingPayment_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [billing].[Invoice] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_BillingPayment_AmountEur] CHECK ([AmountEur] >= 0.00)
    );
END
GO

-- =============================================================================
-- 4. Nonclustered index on Payment.InvoiceId (FK column)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_BillingPayment_InvoiceId'
      AND [object_id] = OBJECT_ID('[billing].[Payment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BillingPayment_InvoiceId]
        ON [billing].[Payment] ([InvoiceId]);
END
GO
