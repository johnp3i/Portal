-- ============================================================
-- Adds Global Payment Allocation columns to [revenue].[Payment].
-- Enables parent-child payment model for customer-level payments
-- that distribute across multiple invoices.
-- ============================================================

USE [Portal]
GO

-- Add ParentPaymentId (self-referencing FK)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[revenue].[Payment]') AND name = 'ParentPaymentId')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD [ParentPaymentId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Payment_ParentPayment')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD CONSTRAINT [FK_Payment_ParentPayment]
    FOREIGN KEY ([ParentPaymentId]) REFERENCES [revenue].[Payment]([Id]);
END
GO

-- Add IsAutoAllocated
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[revenue].[Payment]') AND name = 'IsAutoAllocated')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD [IsAutoAllocated] BIT NOT NULL CONSTRAINT [DF_Payment_IsAutoAllocated] DEFAULT (0);
END
GO

-- Add CustomerId (FK to customer)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[revenue].[Payment]') AND name = 'CustomerId')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD [CustomerId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Payment_Customer')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD CONSTRAINT [FK_Payment_Customer]
    FOREIGN KEY ([CustomerId]) REFERENCES [customer].[Customer]([Id]);
END
GO

-- Add CreditAmount
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[revenue].[Payment]') AND name = 'CreditAmount')
BEGIN
    ALTER TABLE [revenue].[Payment]
    ADD [CreditAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Payment_CreditAmount] DEFAULT (0);
END
GO

-- Make InvoiceId nullable (for parent payments that are customer-level)
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'revenue' AND TABLE_NAME = 'Payment'
      AND COLUMN_NAME = 'InvoiceId' AND IS_NULLABLE = 'NO'
)
BEGIN
    -- Drop existing FK constraint first
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Payment_Invoice')
    BEGIN
        ALTER TABLE [revenue].[Payment] DROP CONSTRAINT [FK_Payment_Invoice];
    END

    ALTER TABLE [revenue].[Payment]
    ALTER COLUMN [InvoiceId] INT NULL;

    -- Re-add FK constraint
    ALTER TABLE [revenue].[Payment]
    ADD CONSTRAINT [FK_Payment_Invoice]
    FOREIGN KEY ([InvoiceId]) REFERENCES [invoice].[Invoice]([Id]);
END
GO

-- Index for parent-child queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payment_ParentPaymentId' AND object_id = OBJECT_ID(N'[revenue].[Payment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_ParentPaymentId]
    ON [revenue].[Payment] ([ParentPaymentId])
    WHERE [ParentPaymentId] IS NOT NULL;
END
GO

-- Index for customer-level payment queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payment_CustomerId' AND object_id = OBJECT_ID(N'[revenue].[Payment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_CustomerId]
    ON [revenue].[Payment] ([CustomerId])
    WHERE [CustomerId] IS NOT NULL;
END
GO
