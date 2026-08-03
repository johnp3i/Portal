-- ============================================================
-- Add optional ProductId FK to SalesProduct for catalog linking
-- ============================================================

USE [Portal]
GO

ALTER TABLE [sales].[Product]
    ADD [ProductId] INT NULL;
GO

ALTER TABLE [sales].[Product]
    ADD CONSTRAINT [FK_SalesProduct_Product] FOREIGN KEY ([ProductId]) REFERENCES [product].[Product] ([Id]);
GO

CREATE NONCLUSTERED INDEX [IX_SalesProduct_ProductId]
    ON [sales].[Product] ([ProductId])
    WHERE [ProductId] IS NOT NULL;
GO
