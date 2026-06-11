/*
    Migration: 090_CreateDemoInvitationTable
    Description: Creates the [portal].[DemoInvitation] table — stores demo access
                 invitation data including token, recipient, expiry, status tracking,
                 and access metrics. Includes a unique index on Token for fast lookup
                 and a non-clustered index on Status for filtered queries.

    Requirements: 2.1 - THE Portal_Database SHALL contain [portal].[DemoInvitation] with all columns
                  2.2 - THE Portal_Database SHALL enforce a unique constraint on Token
                  2.3 - THE Portal_Database SHALL enforce a check constraint on Status
                  2.4 - THE Portal_Database SHALL enforce FK constraint to Business (AspNetUsers FK enforced at app layer — cross-DB)
                  2.5 - THE Portal_Database SHALL include a non-clustered index on Token

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [portal].[DemoInvitation] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal'
      AND TABLE_NAME = 'DemoInvitation'
)
BEGIN
    CREATE TABLE [portal].[DemoInvitation]
    (
        [Id]                  INT            IDENTITY(1,1) NOT NULL,
        [BusinessId]          INT            NOT NULL,
        [Token]               NVARCHAR(100)  NOT NULL,
        [RecipientEmail]      NVARCHAR(256)  NOT NULL,
        [RecipientName]       NVARCHAR(200)  NULL,
        [ExpiresAtUtc]        DATETIME2      NOT NULL,
        [Status]              NVARCHAR(20)   NOT NULL,
        [CreatedByUserId]     NVARCHAR(450)  NOT NULL,
        [FirstAccessedAtUtc]  DATETIME2      NULL,
        [LastAccessedAtUtc]   DATETIME2      NULL,
        [AccessCount]         INT            NOT NULL CONSTRAINT [DF_DemoInvitation_AccessCount] DEFAULT (0),
        [RevokedAtUtc]        DATETIME2      NULL,
        [CreatedAtUtc]        DATETIME2      NOT NULL CONSTRAINT [DF_DemoInvitation_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_DemoInvitation] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DemoInvitation_Business] FOREIGN KEY ([BusinessId])
            REFERENCES [portal].[Business] ([Id]),
        -- Note: CreatedByUserId references [dbo].[AspNetUsers] in the Membership DB.
        -- Cross-database FK constraints are not supported in SQL Server without linked servers,
        -- so this is enforced at the application layer only.
        CONSTRAINT [CK_DemoInvitation_Status] CHECK ([Status] IN ('sent', 'accessed', 'expired', 'revoked'))
    );
END
GO

-- =============================================================================
-- 2. Create unique non-clustered index on Token
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[portal].[DemoInvitation]')
      AND name = N'UX_DemoInvitation_Token'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_DemoInvitation_Token]
        ON [portal].[DemoInvitation] ([Token]);
END
GO

-- =============================================================================
-- 3. Create non-clustered index on Status (includes ExpiresAtUtc, RecipientEmail)
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[portal].[DemoInvitation]')
      AND name = N'IX_DemoInvitation_Status'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DemoInvitation_Status]
        ON [portal].[DemoInvitation] ([Status])
        INCLUDE ([ExpiresAtUtc], [RecipientEmail]);
END
GO
