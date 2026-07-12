-- ============================================================
-- Creates the [import] schema and ParserTemplate table for
-- configurable supplier file parsing during bulk purchase import.
-- ============================================================

USE [Portal]
GO

-- Create schema if it does not exist
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'import')
BEGIN
    EXEC('CREATE SCHEMA [import]')
END
GO

-- Create ParserTemplate table
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[import].[ParserTemplate]') AND type = 'U')
BEGIN
    CREATE TABLE [import].[ParserTemplate]
    (
        [Id]                    INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]            INT                             NOT NULL,
        [SupplierId]            INT                             NOT NULL,
        [Name]                  NVARCHAR(200)                   NOT NULL,
        [FileFormatType]        NVARCHAR(10)                    NOT NULL,
        [HeaderRow]             INT                             NOT NULL    CONSTRAINT [DF_ParserTemplate_HeaderRow] DEFAULT (1),
        [DataStartRow]          INT                             NOT NULL    CONSTRAINT [DF_ParserTemplate_DataStartRow] DEFAULT (2),
        [SheetName]             NVARCHAR(100)                   NULL,
        [ColumnMappingsJson]    NVARCHAR(MAX)                   NOT NULL,
        [IsManaged]             BIT                             NOT NULL    CONSTRAINT [DF_ParserTemplate_IsManaged] DEFAULT (0),
        [IsActive]              BIT                             NOT NULL    CONSTRAINT [DF_ParserTemplate_IsActive] DEFAULT (1),
        [CreatedAtUtc]          DATETIME                        NOT NULL    CONSTRAINT [DF_ParserTemplate_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]          DATETIME                        NOT NULL    CONSTRAINT [DF_ParserTemplate_UpdatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ParserTemplate] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ParserTemplate_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_ParserTemplate_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id])
    );
END
GO

-- Index for efficient supplier template lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ParserTemplate_BusinessId_SupplierId' AND object_id = OBJECT_ID(N'[import].[ParserTemplate]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ParserTemplate_BusinessId_SupplierId]
        ON [import].[ParserTemplate] ([BusinessId], [SupplierId])
        WHERE [IsActive] = 1;
END
GO
