/*
    Reset Script: Clear All Invoices, Quotations, and Shared Links
    Description: Removes all data from invoice, quotation, and sharing tables
                 while preserving reference/lookup data and customer records.
                 Also reseeds identity columns to 1.

    Tables cleared (in FK-safe order):
      1. [invoice].[InvoiceShare]
      2. [invoice].[InvoiceLine]         (also cleared by CASCADE on Invoice delete)
      3. [invoice].[InvoiceSection]      (also cleared by CASCADE on Invoice delete)
      4. [invoice].[Invoice]
      5. [quotation].[ProposalShareLogo] (also cleared by CASCADE on ProposalShare delete)
      6. [quotation].[ProposalShare]
      7. [quotation].[QuotationLine]     (also cleared by CASCADE on Quotation delete)
      8. [quotation].[ProposalSection]   (also cleared by CASCADE on Quotation delete)
      9. [quotation].[Quotation]
     10. [customer].[Customer]

    Tables NOT cleared (preserved):
      - [invoice].[InvoiceStatusType]
      - [invoice].[InvoiceFinancialStatusType]
      - [quotation].[QuotationStatusType]
      - [quotation].[QuotationContact]
      - [portal].[Business]
      - [portal].[BusinessLogo]

    WARNING: This is a destructive operation. All invoice and quotation data will be lost.
*/

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================
    -- 1. INVOICE SHARING
    -- ==========================================================================
    DELETE FROM [invoice].[InvoiceShare];

    -- ==========================================================================
    -- 2. INVOICE LINES
    -- ==========================================================================
    DELETE FROM [invoice].[InvoiceLine];

    -- ==========================================================================
    -- 3. INVOICE SECTIONS
    -- ==========================================================================
    DELETE FROM [invoice].[InvoiceSection];

    -- ==========================================================================
    -- 4. INVOICES
    -- ==========================================================================
    DELETE FROM [invoice].[Invoice];

    -- ==========================================================================
    -- 5. PROPOSAL SHARE LOGOS (child of ProposalShare)
    -- ==========================================================================
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalShareLogo')
        DELETE FROM [quotation].[ProposalShareLogo];

    -- ==========================================================================
    -- 6. PROPOSAL SHARES
    -- ==========================================================================
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalShare')
        DELETE FROM [quotation].[ProposalShare];

    -- ==========================================================================
    -- 7. QUOTATION LINES
    -- ==========================================================================
    DELETE FROM [quotation].[QuotationLine];

    -- ==========================================================================
    -- 8. PROPOSAL SECTIONS
    -- ==========================================================================
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalSection')
        DELETE FROM [quotation].[ProposalSection];

    -- ==========================================================================
    -- 9. QUOTATIONS
    -- ==========================================================================
    DELETE FROM [quotation].[Quotation];

    -- ==========================================================================
    -- 10. CUSTOMERS
    -- ==========================================================================
    DELETE FROM [customer].[Customer];

    -- ==========================================================================
    -- 11. RESEED IDENTITY COLUMNS
    -- ==========================================================================
    DBCC CHECKIDENT ('[invoice].[InvoiceShare]', RESEED, 0);
    DBCC CHECKIDENT ('[invoice].[InvoiceLine]', RESEED, 0);
    DBCC CHECKIDENT ('[invoice].[InvoiceSection]', RESEED, 0);
    DBCC CHECKIDENT ('[invoice].[Invoice]', RESEED, 0);
    DBCC CHECKIDENT ('[quotation].[QuotationLine]', RESEED, 0);
    DBCC CHECKIDENT ('[quotation].[Quotation]', RESEED, 0);
    DBCC CHECKIDENT ('[customer].[Customer]', RESEED, 0);

    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalShareLogo')
        DBCC CHECKIDENT ('[quotation].[ProposalShareLogo]', RESEED, 0);

    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalShare')
        DBCC CHECKIDENT ('[quotation].[ProposalShare]', RESEED, 0);

    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'quotation' AND TABLE_NAME = 'ProposalSection')
        DBCC CHECKIDENT ('[quotation].[ProposalSection]', RESEED, 0);

    COMMIT TRANSACTION;

    PRINT 'Reset complete. All invoices, quotations, and shared links have been removed.';
    PRINT 'Identity columns reseeded to 0 (next insert will be Id = 1).';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: Reset failed. Transaction rolled back.';
    PRINT ERROR_MESSAGE();
END CATCH
GO
