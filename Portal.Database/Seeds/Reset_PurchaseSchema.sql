-- ============================================================
-- Reset Purchase Schema Tables (Truncate with FK handling)
-- Excludes: [purchase].[PurchaseOriginType] (lookup table)
-- Order: Purchase first (has FKs to Supplier & ExpenseCategory)
-- ============================================================

-- Step 1: Drop FK constraints on Purchase
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT IF EXISTS FK_Purchase_VatSubmissionPeriod;
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT IF EXISTS FK_Purchase_Supplier;
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT IF EXISTS FK_Purchase_ExpenseCategory;
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT IF EXISTS FK_Purchase_PurchaseOriginType;
ALTER TABLE [purchase].[Purchase] DROP CONSTRAINT IF EXISTS FK_Purchase_Business;
GO

-- Step 2: Truncate tables (resets identity to 1)
TRUNCATE TABLE [purchase].[Purchase];
TRUNCATE TABLE [purchase].[Supplier];
TRUNCATE TABLE [purchase].[ExpenseCategory];
GO

-- Step 3: Re-add FK constraints
ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_Business
    FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id]);

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_Supplier
    FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier]([Id]);

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_ExpenseCategory
    FOREIGN KEY ([ExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory]([Id]);

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_PurchaseOriginType
    FOREIGN KEY ([PurchaseOriginTypeId]) REFERENCES [purchase].[PurchaseOriginType]([Id]);

ALTER TABLE [purchase].[Purchase]
ADD CONSTRAINT FK_Purchase_VatSubmissionPeriod
    FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod]([Id]);
GO
