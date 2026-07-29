-- ============================================================
-- Create [compliance] schema and all compliance module tables
-- ============================================================

USE [Portal]
GO

CREATE SCHEMA [compliance]
GO

-- Reference table: Application categories
CREATE TABLE [compliance].[ApplicationCategory] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [Name]          NVARCHAR(100) NOT NULL,
    [Description]   NVARCHAR(500) NULL,
    [IsActive]      BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]  DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ApplicationCategory] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_ApplicationCategory_Name] UNIQUE ([Name])
);

-- Template catalog: Filing type definitions
CREATE TABLE [compliance].[ApplicationType] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [Name]                  NVARCHAR(200) NOT NULL,
    [Description]           NVARCHAR(1000) NULL,
    [Country]               NVARCHAR(100) NOT NULL,
    [ApplicationCategoryId] INT NOT NULL,
    [Frequency]             NVARCHAR(20) NOT NULL,
    [DefaultDueMonth]       INT NULL,
    [DefaultDueDay]         INT NULL,
    [IsActive]              BIT NOT NULL DEFAULT 1,
    [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ApplicationType] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ApplicationType_Category] FOREIGN KEY ([ApplicationCategoryId])
        REFERENCES [compliance].[ApplicationCategory]([Id]),
    CONSTRAINT [UQ_ApplicationType_NameCountry] UNIQUE ([Name], [Country]),
    CONSTRAINT [CK_ApplicationType_Frequency]
        CHECK ([Frequency] IN ('Monthly', 'Quarterly', 'Annual', 'One-off')),
    CONSTRAINT [CK_ApplicationType_DueMonth]
        CHECK ([DefaultDueMonth] IS NULL OR ([DefaultDueMonth] >= 1 AND [DefaultDueMonth] <= 12)),
    CONSTRAINT [CK_ApplicationType_DueDay]
        CHECK ([DefaultDueDay] IS NULL OR ([DefaultDueDay] >= 1 AND [DefaultDueDay] <= 31))
);

-- Per-business filing instances
CREATE TABLE [compliance].[BusinessApplication] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [BusinessId]        INT NOT NULL,
    [ApplicationTypeId] INT NOT NULL,
    [DueDate]           DATE NOT NULL,
    [Status]            NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    [ReferenceNumber]   NVARCHAR(100) NULL,
    [Notes]             NVARCHAR(2000) NULL,
    [SubmittedAtUtc]    DATETIME NULL,
    [ApprovedAtUtc]     DATETIME NULL,
    [CreatedAtUtc]      DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_BusinessApplication] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_BusinessApplication_Type] FOREIGN KEY ([ApplicationTypeId])
        REFERENCES [compliance].[ApplicationType]([Id]),
    CONSTRAINT [CK_BusinessApplication_Status]
        CHECK ([Status] IN ('Pending', 'InProgress', 'Submitted', 'Approved', 'Rejected'))
);

CREATE INDEX [IX_BusinessApplication_BusinessId_DueDate]
    ON [compliance].[BusinessApplication] ([BusinessId], [DueDate]);

CREATE INDEX [IX_BusinessApplication_BusinessId_Status]
    ON [compliance].[BusinessApplication] ([BusinessId], [Status]);

-- Submission evidence attachments
CREATE TABLE [compliance].[ApplicationAttachment] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [BusinessApplicationId] INT NOT NULL,
    [FileName]              NVARCHAR(255) NOT NULL,
    [OriginalFileName]      NVARCHAR(255) NOT NULL,
    [FilePath]              NVARCHAR(500) NOT NULL,
    [ContentType]           NVARCHAR(100) NOT NULL,
    [FileSizeBytes]         BIGINT NOT NULL,
    [UploadedByUserId]      NVARCHAR(450) NOT NULL,
    [CreatedAtUtc]          DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ApplicationAttachment] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ApplicationAttachment_BusinessApplication] FOREIGN KEY ([BusinessApplicationId])
        REFERENCES [compliance].[BusinessApplication]([Id])
        ON DELETE NO ACTION
);
