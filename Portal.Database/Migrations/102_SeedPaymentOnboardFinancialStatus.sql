/*
    Migration: 102_SeedPaymentOnboardFinancialStatus
    Description: Inserts PaymentOnboard (Id=6) into [invoice].[InvoiceFinancialStatusType].
                 This status indicates a customer has declared payment via bank transfer,
                 pending business verification.
    Requirements: 5.1
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM [invoice].[InvoiceFinancialStatusType]
    WHERE [Id] = 6
)
BEGIN
    INSERT INTO [invoice].[InvoiceFinancialStatusType] ([Id], [Name])
    VALUES (6, 'PaymentOnboard');
END
GO
