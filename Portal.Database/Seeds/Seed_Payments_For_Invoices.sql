/*
    Seed: Payments for All Invoices (1-89, except Invoice 86 which remains Unpaid)
    Description: Creates a full payment record for each invoice, matching the TotalAmount.
                 Payment method = 2 (BankTransfer).
                 PaymentDateUtc is set to the invoice's DueDate (converted to datetime2).
                 Also updates InvoiceFinancialStatusTypeId to 3 (Paid) for consistency,
                 and sets Invoice 86 to InvoiceFinancialStatusTypeId = 1 (Unpaid).

    Prerequisites:
      - All 89 invoices must exist (from All_Invoices_Seed.sql)
      - PaymentMethodType seed data must exist (2 = BankTransfer)
      - [revenue].[Payment] table must exist (migration 013)

    This script is idempotent — only inserts payments for invoices that don't already have one.
*/

-- =============================================================================
-- SECTION 1: INSERT PAYMENTS FOR INVOICES 1-89 (EXCEPT 86)
-- =============================================================================

-- Insert a full payment for each invoice that doesn't already have a payment record.
-- Uses the invoice's TotalAmount as the payment amount and DueDate as the payment date.
INSERT INTO [revenue].[Payment]
    ([BusinessId], [InvoiceId], [PaymentMethodTypeId], [PaymentDateUtc], [Amount], [Reference], [Notes], [IsVoided], [CreatedByUserId])
SELECT
    [invoice].[Invoice].[BusinessId],
    [invoice].[Invoice].[Id],
    2,  -- BankTransfer
    CAST([invoice].[Invoice].[DueDate] AS DATETIME2),
    [invoice].[Invoice].[TotalAmount],
    N'SEED-PAY-' + CAST([invoice].[Invoice].[Id] AS NVARCHAR(10)),
    N'Seeded payment — full amount via bank transfer',
    0,  -- IsVoided = false
    N'system-seed'
FROM [invoice].[Invoice]
WHERE [invoice].[Invoice].[Id] BETWEEN 1 AND 89
  AND [invoice].[Invoice].[Id] <> 86
  AND NOT EXISTS (
      SELECT 1
      FROM [revenue].[Payment]
      WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
        AND [revenue].[Payment].[IsVoided] = 0
  );
GO

-- =============================================================================
-- SECTION 2: ENSURE FINANCIAL STATUS CONSISTENCY
-- =============================================================================

-- Set invoices 1-89 (except 86) to Paid (3) — they now have full payment records
UPDATE [invoice].[Invoice]
SET [InvoiceFinancialStatusTypeId] = 3
WHERE [Id] BETWEEN 1 AND 89
  AND [Id] <> 86
  AND [InvoiceFinancialStatusTypeId] <> 3;
GO

-- Set invoice 86 to Unpaid (1) — no payment exists for this invoice
UPDATE [invoice].[Invoice]
SET [InvoiceFinancialStatusTypeId] = 1
WHERE [Id] = 86
  AND [InvoiceFinancialStatusTypeId] <> 1;
GO

-- =============================================================================
-- VERIFICATION
-- =============================================================================

-- Quick verification: count payments created
SELECT 
    COUNT(*) AS [PaymentsCreated],
    SUM([Amount]) AS [TotalAmountSeeded]
FROM [revenue].[Payment]
WHERE [Reference] LIKE N'SEED-PAY-%';
GO
