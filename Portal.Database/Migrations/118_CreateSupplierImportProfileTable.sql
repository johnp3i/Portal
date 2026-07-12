-- ============================================================
-- Creates the SupplierImportProfile table for storing default
-- values per supplier during bulk purchase import.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[import].[SupplierImportProfile]') AND type = 'U')
BEGIN
    CREATE TABLE [import].[SupplierImportProfile]
    (
        [Id]                            INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]                    INT                             NOT NULL,
        [SupplierId]                    INT                             NOT NULL,
        [DefaultExpenseCategoryId]      INT                             NULL,
        [DefaultPurchaseOriginTypeId]   INT                             NULL,
        [DefaultCountry]                NVARCHAR(100)                   NULL,
        [CreatedAtUtc]                  DATETIME                        NOT NULL    CONSTRAINT [DF_SupplierImportProfile_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]                  DATETIME                        NOT NULL    CONSTRAINT [DF_SupplierImportProfile_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_SupplierImportProfile] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_SupplierImportProfile_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_SupplierImportProfile_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]),
        CONSTRAINT [FK_SupplierImportProfile_ExpenseCategory] FOREIGN KEY ([DefaultExpenseCategoryId]) REFERENCES [purchase].[ExpenseCategory] ([Id]),
        CONSTRAINT [FK_SupplierImportProfile_OriginType] FOREIGN KEY ([DefaultPurchaseOriginTypeId]) REFERENCES [purchase].[PurchaseOriginType] ([Id]),
        CONSTRAINT [UQ_SupplierImportProfile_Business_Supplier] UNIQUE ([BusinessId], [SupplierId])
    );
END
GO
