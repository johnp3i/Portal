-- ============================================================
-- Creates the ImportSession table for transient import state
-- during the Upload → Preview → Confirm workflow.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[import].[ImportSession]') AND type = 'U')
BEGIN
    CREATE TABLE [import].[ImportSession]
    (
        [Id]                INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]        INT                             NOT NULL,
        [SupplierId]        INT                             NOT NULL,
        [ParserTemplateId]  INT                             NULL,
        [FileName]          NVARCHAR(500)                   NOT NULL,
        [TotalRows]         INT                             NOT NULL,
        [ValidRows]         INT                             NOT NULL,
        [InvalidRows]       INT                             NOT NULL,
        [RowDataJson]       NVARCHAR(MAX)                   NOT NULL,
        [IsConfirmed]       BIT                             NOT NULL    CONSTRAINT [DF_ImportSession_IsConfirmed] DEFAULT (0),
        [CreatedAtUtc]      DATETIME                        NOT NULL    CONSTRAINT [DF_ImportSession_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_ImportSession] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ImportSession_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_ImportSession_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [purchase].[Supplier] ([Id]),
        CONSTRAINT [FK_ImportSession_Template] FOREIGN KEY ([ParserTemplateId]) REFERENCES [import].[ParserTemplate] ([Id])
    );
END
GO
