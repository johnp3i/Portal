-- ============================================================
-- Create Signature table for digital signature management
-- ============================================================

USE [Portal]
GO

CREATE TABLE [portal].[Signature]
(
    [Id]                INT             IDENTITY(1,1)   NOT NULL,
    [BusinessId]        INT                             NOT NULL,
    [Label]             NVARCHAR(100)                   NOT NULL,
    [FileName]          NVARCHAR(200)                   NOT NULL,
    [ContentType]       NVARCHAR(50)                    NOT NULL,
    [FilePath]          NVARCHAR(500)                   NOT NULL,
    [IsDefault]         BIT                             NOT NULL    CONSTRAINT [DF_Signature_IsDefault] DEFAULT (0),
    [IsActive]          BIT                             NOT NULL    CONSTRAINT [DF_Signature_IsActive] DEFAULT (1),
    [UploadedByUserId]  NVARCHAR(450)                   NOT NULL,
    [CreatedAtUtc]      DATETIME                        NOT NULL    CONSTRAINT [DF_Signature_CreatedAtUtc] DEFAULT (GETUTCDATE()),

    CONSTRAINT [PK_Signature] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Signature_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_Signature_BusinessId]
    ON [portal].[Signature] ([BusinessId])
    WHERE [IsActive] = 1;
GO
