-- ============================================================
-- Migration 132: Create Sales Product Table
-- ============================================================
-- Purpose: Creates the [sales].[Product] table — a business-specific
--          catalogue of products sold. Used to track which products
--          leads are interested in and associate products with pipeline
--          activity, meetings, and templates.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales' AND TABLE_NAME = 'Product'
)
BEGIN
    CREATE TABLE [sales].[Product]
    (
        [Id]            INT             IDENTITY(1,1) NOT NULL,
        [BusinessId]    INT             NOT NULL,
        [Name]          NVARCHAR(200)   NOT NULL,
        [Description]   NVARCHAR(500)   NULL,
        [IsActive]      BIT             NOT NULL CONSTRAINT [DF_SalesProduct_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME        NOT NULL CONSTRAINT [DF_SalesProduct_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_SalesProduct] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_SalesProduct_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business]([Id])
    );

    PRINT 'Created [sales].[Product] table.';
END
ELSE
BEGIN
    PRINT '[sales].[Product] already exists.';
END
GO
