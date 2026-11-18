/*
    Migration: 234_AddCustomerPurchasesIndexes
    Description: Indexes for the Customer Purchases Dashboard read path.
                 [invoice].[Invoice] had no (BusinessId, CustomerId, InvoiceDate) index, and
                 [invoice].[InvoiceLine] and [revenue].[Payment] had no InvoiceId index.
    This script is idempotent - safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Invoice_BusinessId_CustomerId_InvoiceDate'
                 AND object_id = OBJECT_ID(N'[invoice].[Invoice]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoice_BusinessId_CustomerId_InvoiceDate]
        ON [invoice].[Invoice] ([BusinessId], [CustomerId], [InvoiceDate])
        INCLUDE ([DueDate], [CurrencyCode], [TotalAmount], [InvoiceStatusTypeId], [IsDeleted], [InvoiceNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_InvoiceLine_InvoiceId'
                 AND object_id = OBJECT_ID(N'[invoice].[InvoiceLine]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceLine_InvoiceId]
        ON [invoice].[InvoiceLine] ([InvoiceId])
        INCLUDE ([ProductCode], [Description], [Quantity], [LineTotal], [CostPrice], [IsAdjustmentLine]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Payment_InvoiceId'
                 AND object_id = OBJECT_ID(N'[revenue].[Payment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_InvoiceId]
        ON [revenue].[Payment] ([InvoiceId])
        INCLUDE ([BusinessId], [Amount], [PaymentDateUtc], [IsVoided]);
END
GO
