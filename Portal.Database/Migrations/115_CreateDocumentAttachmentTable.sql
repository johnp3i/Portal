-- ============================================================
-- Creates the [document] schema and DocumentAttachment table
-- for storing file attachment metadata per business entity.
-- ============================================================

USE [Portal]
GO

-- Create schema if it does not exist
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'document')
BEGIN
    EXEC('CREATE SCHEMA [document]')
END
GO

-- Create DocumentAttachment table
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[document].[DocumentAttachment]') AND type = 'U')
BEGIN
    CREATE TABLE [document].[DocumentAttachment]
    (
        [Id]                INT             IDENTITY(1,1)   NOT NULL,
        [BusinessId]        INT                             NOT NULL,
        [EntityType]        NVARCHAR(50)                    NOT NULL,
        [EntityId]          INT                             NOT NULL,
        [FileName]          NVARCHAR(255)                   NOT NULL,
        [OriginalFileName]  NVARCHAR(255)                   NOT NULL,
        [ContentType]       NVARCHAR(100)                   NOT NULL,
        [StoragePath]       NVARCHAR(500)                   NOT NULL,
        [FileSizeBytes]     BIGINT                          NOT NULL,
        [UploadedByUserId]  NVARCHAR(450)                   NOT NULL,
        [IsDeleted]         BIT                             NOT NULL    CONSTRAINT [DF_DocumentAttachment_IsDeleted] DEFAULT (0),
        [CreatedAtUtc]      DATETIME                        NOT NULL    CONSTRAINT [DF_DocumentAttachment_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_DocumentAttachment] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DocumentAttachment_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Filtered index for efficient entity-scoped lookups (excludes soft-deleted records)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentAttachment_BusinessId_EntityType_EntityId' AND object_id = OBJECT_ID(N'[document].[DocumentAttachment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DocumentAttachment_BusinessId_EntityType_EntityId]
        ON [document].[DocumentAttachment] ([BusinessId], [EntityType], [EntityId])
        WHERE [IsDeleted] = 0;
END
GO
