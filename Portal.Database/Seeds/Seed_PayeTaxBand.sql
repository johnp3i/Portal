-- ============================================================
-- Phase D: Create PayeTaxBand table
-- Stores progressive income tax bands per country and year.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayeTaxBand' AND schema_id = SCHEMA_ID('payroll'))
BEGIN
    CREATE TABLE [payroll].[PayeTaxBand]
    (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [CountryCode]       NVARCHAR(3) NOT NULL,
        [LowerBound]        DECIMAL(18,2) NOT NULL,
        [UpperBound]        DECIMAL(18,2) NULL,
        [Rate]              DECIMAL(5,4) NOT NULL,
        [EffectiveFromYear] INT NOT NULL,
        [EffectiveToYear]   INT NULL,
        [CreatedAtUtc]      DATETIME NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_PayeTaxBand] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_PayeTaxBand_Rate] CHECK ([Rate] >= 0 AND [Rate] <= 1),
        CONSTRAINT [CK_PayeTaxBand_Bounds] CHECK ([UpperBound] IS NULL OR [LowerBound] < [UpperBound])
    );

    CREATE NONCLUSTERED INDEX [IX_PayeTaxBand_Country_Year]
        ON [payroll].[PayeTaxBand] ([CountryCode], [EffectiveFromYear])
        INCLUDE ([LowerBound], [UpperBound], [Rate], [EffectiveToYear]);
END
GO
