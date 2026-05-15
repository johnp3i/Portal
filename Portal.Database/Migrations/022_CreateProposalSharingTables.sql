/*
    Migration: 022_CreateProposalSharingTables
    Description: Creates the proposal sharing tables for the quotation module:
                 - [quotation].[ProposalSection] — named groupings of quotation lines within a proposal
                 - [quotation].[ProposalShare] — point-in-time HTML snapshots shared via secure links
                 - [quotation].[ProposalShareLogo] — junction table linking logos to proposal shares
                 Also alters [quotation].[QuotationLine] to add ReferenceUrl and ProposalSectionId columns.

    Requirements: 1.5, 2.1, 3.1, 3.5, 3.6, 9.1

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [quotation].[ProposalSection]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'ProposalSection'
)
BEGIN
    CREATE TABLE [quotation].[ProposalSection]
    (
        [Id]                  INT            IDENTITY(1,1)  NOT NULL,
        [QuotationId]         INT                           NOT NULL,
        [Name]                NVARCHAR(200)                 NOT NULL,
        [SortOrder]           INT                           NOT NULL  CONSTRAINT [DF_ProposalSection_SortOrder] DEFAULT (0),
        [ColumnConfiguration] NVARCHAR(50)                  NOT NULL  CONSTRAINT [DF_ProposalSection_ColumnConfiguration] DEFAULT ('OneTime'),

        CONSTRAINT [PK_ProposalSection] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProposalSection_Quotation] FOREIGN KEY ([QuotationId]) REFERENCES [quotation].[Quotation] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ProposalSection_QuotationId'
      AND [object_id] = OBJECT_ID('[quotation].[ProposalSection]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProposalSection_QuotationId]
        ON [quotation].[ProposalSection] ([QuotationId]);
END
GO

-- =============================================================================
-- 2. Create [quotation].[ProposalShare]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'ProposalShare'
)
BEGIN
    CREATE TABLE [quotation].[ProposalShare]
    (
        [Id]              INT                IDENTITY(1,1)  NOT NULL,
        [QuotationId]     INT                               NOT NULL,
        [BusinessId]      INT                               NOT NULL,
        [ShareToken]      NVARCHAR(128)                     NOT NULL,
        [SnapshotHtml]    NVARCHAR(MAX)                     NOT NULL,
        [CustomerEmail]   NVARCHAR(200)                     NOT NULL,
        [ExpiresAtUtc]    DATETIMEOFFSET                    NOT NULL,
        [CreatedAtUtc]    DATETIMEOFFSET                    NOT NULL  CONSTRAINT [DF_ProposalShare_CreatedAtUtc] DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedByUserId] NVARCHAR(450)                     NOT NULL,
        [IsActive]        BIT                               NOT NULL  CONSTRAINT [DF_ProposalShare_IsActive] DEFAULT (1),

        CONSTRAINT [PK_ProposalShare] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProposalShare_Quotation] FOREIGN KEY ([QuotationId]) REFERENCES [quotation].[Quotation] ([Id]),
        CONSTRAINT [FK_ProposalShare_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [UX_ProposalShare_ShareToken] UNIQUE NONCLUSTERED ([ShareToken])
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ProposalShare_QuotationId'
      AND [object_id] = OBJECT_ID('[quotation].[ProposalShare]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProposalShare_QuotationId]
        ON [quotation].[ProposalShare] ([QuotationId]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_ProposalShare_BusinessId'
      AND [object_id] = OBJECT_ID('[quotation].[ProposalShare]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProposalShare_BusinessId]
        ON [quotation].[ProposalShare] ([BusinessId]);
END
GO

-- =============================================================================
-- 3. Create [quotation].[ProposalShareLogo]
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'quotation'
      AND TABLE_NAME = 'ProposalShareLogo'
)
BEGIN
    CREATE TABLE [quotation].[ProposalShareLogo]
    (
        [Id]              INT            IDENTITY(1,1)  NOT NULL,
        [ProposalShareId] INT                           NOT NULL,
        [BusinessLogoId]  INT                           NOT NULL,
        [Placement]       NVARCHAR(20)                  NOT NULL,
        [SortOrder]       INT                           NOT NULL  CONSTRAINT [DF_ProposalShareLogo_SortOrder] DEFAULT (0),

        CONSTRAINT [PK_ProposalShareLogo] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProposalShareLogo_ProposalShare] FOREIGN KEY ([ProposalShareId]) REFERENCES [quotation].[ProposalShare] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProposalShareLogo_BusinessLogo] FOREIGN KEY ([BusinessLogoId]) REFERENCES [portal].[BusinessLogo] ([Id]),
        CONSTRAINT [CK_ProposalShareLogo_Placement] CHECK ([Placement] IN ('Hero', 'Meta'))
    );
END
GO

-- =============================================================================
-- 4. ALTER [quotation].[QuotationLine] — Add ReferenceUrl
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'ReferenceUrl'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [ReferenceUrl] NVARCHAR(2048) NULL;
END
GO

-- =============================================================================
-- 5. ALTER [quotation].[QuotationLine] — Add ProposalSectionId with FK
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[quotation].[QuotationLine]')
      AND name = N'ProposalSectionId'
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD [ProposalSectionId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_QuotationLine_ProposalSection'
      AND [parent_object_id] = OBJECT_ID('[quotation].[QuotationLine]')
)
BEGIN
    ALTER TABLE [quotation].[QuotationLine]
        ADD CONSTRAINT [FK_QuotationLine_ProposalSection]
        FOREIGN KEY ([ProposalSectionId]) REFERENCES [quotation].[ProposalSection] ([Id])
        ON DELETE NO ACTION;
END
GO
