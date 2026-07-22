-- ============================================================
-- Migration: 148_CreateTeamMemberTable
-- Description: Creates the [sales].[TeamMember] table for
--              managing people assignable to leads.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'sales'
      AND TABLE_NAME = 'TeamMember'
)
BEGIN
    CREATE TABLE [sales].[TeamMember]
    (
        [Id]            INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                           NOT NULL,
        [FirstName]     NVARCHAR(100)                 NOT NULL,
        [LastName]      NVARCHAR(100)                 NULL,
        [Email]         NVARCHAR(200)                 NULL,
        [PhoneNumber]   NVARCHAR(50)                  NULL,
        [Role]          NVARCHAR(100)                 NULL,
        [UserId]        NVARCHAR(450)                 NULL,
        [IsActive]      BIT                           NOT NULL CONSTRAINT [DF_TeamMember_IsActive] DEFAULT (1),
        [CreatedAtUtc]  DATETIME                      NOT NULL CONSTRAINT [DF_TeamMember_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_TeamMember] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_TeamMember_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Partial unique index on Email per business (excluding NULLs)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_TeamMember_BusinessId_Email'
      AND [object_id] = OBJECT_ID('[sales].[TeamMember]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_TeamMember_BusinessId_Email]
        ON [sales].[TeamMember] ([BusinessId], [Email])
        WHERE [Email] IS NOT NULL;
END
GO

-- Index on BusinessId for tenant queries
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_TeamMember_BusinessId'
      AND [object_id] = OBJECT_ID('[sales].[TeamMember]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TeamMember_BusinessId]
        ON [sales].[TeamMember] ([BusinessId]);
END
GO
