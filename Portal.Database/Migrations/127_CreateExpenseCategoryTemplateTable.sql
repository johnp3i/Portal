-- ============================================================
-- Create ExpenseCategoryTemplate table for platform-wide templates
-- ============================================================

USE [Portal]
GO

CREATE TABLE [purchase].[ExpenseCategoryTemplate]
(
    [Id]            INT             IDENTITY(1,1)   NOT NULL,
    [Name]          NVARCHAR(100)                   NOT NULL,
    [Description]   NVARCHAR(500)                   NULL,
    [IsActive]      BIT                             NOT NULL    CONSTRAINT [DF_ExpenseCategoryTemplate_IsActive] DEFAULT (1),
    [CreatedAtUtc]  DATETIME                        NOT NULL    CONSTRAINT [DF_ExpenseCategoryTemplate_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_ExpenseCategoryTemplate] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_ExpenseCategoryTemplate_Name]
    ON [purchase].[ExpenseCategoryTemplate] ([Name])
    WHERE [IsActive] = 1;
GO
