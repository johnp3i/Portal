/*
    Migration: 004_CreateUserBusinessTables
    Description: Creates the [membership].[UserBusiness] and [membership].[UserBusinessPermission]
                 tables in the Membership database.

                 UserBusiness maps users to businesses (one-to-many).
                 UserBusinessPermission grants module-level access per mapping.

    This script is idempotent — safe to run multiple times.
*/

-- ============================================================
-- Table: [membership].[UserBusiness]
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = N'membership'
      AND TABLE_NAME = N'UserBusiness'
)
BEGIN
    CREATE TABLE [membership].[UserBusiness] (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [UserId]           NVARCHAR(450) NOT NULL,
        [BusinessId]       INT NOT NULL,
        [IsDefault]        BIT NOT NULL DEFAULT 0,
        [IsActive]         BIT NOT NULL DEFAULT 1,
        [DeactivatedAtUtc] DATETIME2 NULL,
        [CreatedAtUtc]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_UserBusiness] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_UserBusiness_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]),
        CONSTRAINT [UQ_UserBusiness_UserId_BusinessId] UNIQUE ([UserId], [BusinessId])
    );

    CREATE NONCLUSTERED INDEX [IX_UserBusiness_UserId_IsActive]
        ON [membership].[UserBusiness] ([UserId], [IsActive])
        INCLUDE ([BusinessId], [IsDefault]);
END
GO

-- ============================================================
-- Table: [membership].[UserBusinessPermission]
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = N'membership'
      AND TABLE_NAME = N'UserBusinessPermission'
)
BEGIN
    CREATE TABLE [membership].[UserBusinessPermission] (
        [Id]               INT IDENTITY(1,1) NOT NULL,
        [UserBusinessId]   INT NOT NULL,
        [Module]           NVARCHAR(50) NOT NULL,
        [AccessLevel]      NVARCHAR(20) NOT NULL,
        [IsActive]         BIT NOT NULL DEFAULT 1,
        [DeactivatedAtUtc] DATETIME2 NULL,
        [CreatedAtUtc]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_UserBusinessPermission] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_UserBusinessPermission_UserBusiness] FOREIGN KEY ([UserBusinessId])
            REFERENCES [membership].[UserBusiness] ([Id]),
        CONSTRAINT [UQ_UserBusinessPermission_UserBusinessId_Module] UNIQUE ([UserBusinessId], [Module]),
        CONSTRAINT [CK_UserBusinessPermission_Module] CHECK (
            [Module] IN ('customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'audit')
        ),
        CONSTRAINT [CK_UserBusinessPermission_AccessLevel] CHECK (
            [AccessLevel] IN ('full', 'readonly', 'none')
        )
    );

    CREATE NONCLUSTERED INDEX [IX_UserBusinessPermission_UserBusinessId_IsActive]
        ON [membership].[UserBusinessPermission] ([UserBusinessId], [IsActive])
        INCLUDE ([Module], [AccessLevel]);
END
GO
