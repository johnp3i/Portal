/*
    Migration: 227_CreateUserPracticeFocus
    Description: Creates [sales].[UserPracticeFocus] — the single "this week's practice focus" that a
                 user chooses on the private My Development dashboard (Call Reflection module).

                 One deliberately-chosen behaviour to practise, per user per business. This is distinct
                 from a per-call "next improvement" note on [sales].[ActivitySelfEvaluation]: it is a
                 standing focus, not tied to any one activity. Private to the author.

                 Lives in [sales] beside the rest of the prospecting / self-evaluation tables and
                 reuses the same tenant (BusinessId) pattern.

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sales' AND t.name = N'UserPracticeFocus'
)
BEGIN
    CREATE TABLE [sales].[UserPracticeFocus] (
        [Id]            INT IDENTITY(1,1) NOT NULL,
        [BusinessId]    INT NOT NULL,
        -- The owner of this focus (ASP.NET Identity user id). Author-only.
        [UserId]        NVARCHAR(450) NOT NULL,
        [Focus]         NVARCHAR(1000) NOT NULL,
        [CreatedAtUtc]  DATETIME NOT NULL CONSTRAINT [DF_UserPracticeFocus_CreatedAtUtc] DEFAULT (GETUTCDATE()),
        [UpdatedAtUtc]  DATETIME NULL,

        CONSTRAINT [PK_UserPracticeFocus] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserPracticeFocus_Business]
            FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- One focus per user per business (upsert target).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_UserPracticeFocus_Business_User'
      AND object_id = OBJECT_ID(N'[sales].[UserPracticeFocus]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_UserPracticeFocus_Business_User]
        ON [sales].[UserPracticeFocus] ([BusinessId], [UserId]);
END
GO
