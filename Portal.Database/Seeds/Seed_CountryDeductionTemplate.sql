-- ============================================================
-- Phase D: Create CountryDeductionTemplate table
-- Stores country-specific deduction templates for multi-country expansion.
-- SuperAdmin manages these; businesses import copies.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CountryDeductionTemplate' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[CountryDeductionTemplate]
    (
        [Id]                        INT IDENTITY(1,1) NOT NULL,
        [CountryCode]               NVARCHAR(3) NOT NULL,
        [DeductionName]             NVARCHAR(100) NOT NULL,
        [Code]                      NVARCHAR(50) NOT NULL,
        [IsPercentage]              BIT NOT NULL DEFAULT 1,
        [DeductionCategoryTypeId]   TINYINT NOT NULL,
        [DefaultRate]               DECIMAL(5,4) NOT NULL,
        [IsPayeDeductible]          BIT NOT NULL DEFAULT 0,
        [SortOrder]                 INT NOT NULL DEFAULT 0,
        [IsActive]                  BIT NOT NULL DEFAULT 1,
        [CreatedAtUtc]              DATETIME NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_CountryDeductionTemplate] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_CountryDeductionTemplate_Category] FOREIGN KEY ([DeductionCategoryTypeId])
            REFERENCES [payroll].[DeductionCategoryType]([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_CountryDeductionTemplate_Country]
        ON [payroll].[CountryDeductionTemplate] ([CountryCode], [IsActive])
        INCLUDE ([DeductionName], [Code], [DefaultRate], [SortOrder]);
END
GO
