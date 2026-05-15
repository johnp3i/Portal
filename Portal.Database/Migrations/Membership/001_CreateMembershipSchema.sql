/*
    Migration: 001_CreateMembershipSchema
    Description: Creates all ASP.NET Core Identity tables with extended ApplicationUser columns,
                 the Invitation table, and seeds the SuperAdmin role for the Membership database.

    Requirements: 4.1 - THE Portal_Web SHALL configure ASP.NET Core Identity using a dedicated
                        MembershipDbContext connected to the Membership_Database.
                  4.4 - THE Portal_Web SHALL define a "SuperAdmin" role for platform-level administration.
                  5.1 - Invitation table supports invitation-only registration flow.

    This script is idempotent — safe to run multiple times.
*/

-- ============================================================================
-- AspNetRoles
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetRoles'
)
BEGIN
    CREATE TABLE [dbo].[AspNetRoles]
    (
        [Id]               NVARCHAR(450)   NOT NULL,
        [Name]             NVARCHAR(256)   NULL,
        [NormalizedName]   NVARCHAR(256)   NULL,
        [ConcurrencyStamp] NVARCHAR(MAX)  NULL,

        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_AspNetRoles_NormalizedName]
        ON [dbo].[AspNetRoles] ([NormalizedName])
        WHERE [NormalizedName] IS NOT NULL;
END
GO

-- ============================================================================
-- AspNetUsers (with extended ApplicationUser columns)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetUsers'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUsers]
    (
        [Id]                   NVARCHAR(450)   NOT NULL,
        [UserName]             NVARCHAR(256)   NULL,
        [NormalizedUserName]   NVARCHAR(256)   NULL,
        [Email]                NVARCHAR(256)   NULL,
        [NormalizedEmail]      NVARCHAR(256)   NULL,
        [EmailConfirmed]       BIT             NOT NULL,
        [PasswordHash]         NVARCHAR(MAX)   NULL,
        [SecurityStamp]        NVARCHAR(MAX)   NULL,
        [ConcurrencyStamp]     NVARCHAR(MAX)   NULL,
        [PhoneNumber]          NVARCHAR(MAX)   NULL,
        [PhoneNumberConfirmed] BIT             NOT NULL,
        [TwoFactorEnabled]     BIT             NOT NULL,
        [LockoutEnd]           DATETIMEOFFSET  NULL,
        [LockoutEnabled]       BIT             NOT NULL,
        [AccessFailedCount]    INT             NOT NULL,

        -- Extended ApplicationUser columns
        [BusinessId]           INT             NULL,
        [FirstName]            NVARCHAR(100)   NOT NULL,
        [LastName]             NVARCHAR(100)   NOT NULL,
        [IsActive]             BIT             NOT NULL  CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT (1),
        [CreatedAtUtc]         DATETIME2       NOT NULL  CONSTRAINT [DF_AspNetUsers_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_AspNetUsers_NormalizedUserName]
        ON [dbo].[AspNetUsers] ([NormalizedUserName])
        WHERE [NormalizedUserName] IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_AspNetUsers_NormalizedEmail]
        ON [dbo].[AspNetUsers] ([NormalizedEmail]);
END
GO

-- ============================================================================
-- AspNetUserRoles
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetUserRoles'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles]
    (
        [UserId]  NVARCHAR(450)  NOT NULL,
        [RoleId]  NVARCHAR(450)  NOT NULL,

        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId]
        ON [dbo].[AspNetUserRoles] ([RoleId]);
END
GO

-- ============================================================================
-- AspNetUserClaims
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetUserClaims'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims]
    (
        [Id]         INT            IDENTITY(1,1)  NOT NULL,
        [UserId]     NVARCHAR(450)  NOT NULL,
        [ClaimType]  NVARCHAR(MAX)  NULL,
        [ClaimValue] NVARCHAR(MAX)  NULL,

        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AspNetUserClaims_UserId]
        ON [dbo].[AspNetUserClaims] ([UserId]);
END
GO

-- ============================================================================
-- AspNetRoleClaims
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetRoleClaims'
)
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims]
    (
        [Id]         INT            IDENTITY(1,1)  NOT NULL,
        [RoleId]     NVARCHAR(450)  NOT NULL,
        [ClaimType]  NVARCHAR(MAX)  NULL,
        [ClaimValue] NVARCHAR(MAX)  NULL,

        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId]
        ON [dbo].[AspNetRoleClaims] ([RoleId]);
END
GO

-- ============================================================================
-- AspNetUserLogins
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetUserLogins'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins]
    (
        [LoginProvider]       NVARCHAR(128)  NOT NULL,
        [ProviderKey]         NVARCHAR(128)  NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX)  NULL,
        [UserId]              NVARCHAR(450)  NOT NULL,

        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AspNetUserLogins_UserId]
        ON [dbo].[AspNetUserLogins] ([UserId]);
END
GO

-- ============================================================================
-- AspNetUserTokens
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'AspNetUserTokens'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens]
    (
        [UserId]        NVARCHAR(450)  NOT NULL,
        [LoginProvider] NVARCHAR(128)  NOT NULL,
        [Name]          NVARCHAR(128)  NOT NULL,
        [Value]         NVARCHAR(MAX)  NULL,

        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END
GO

-- ============================================================================
-- Invitation
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Invitation'
)
BEGIN
    CREATE TABLE [dbo].[Invitation]
    (
        [Id]              INT            IDENTITY(1,1)  NOT NULL,
        [Email]           NVARCHAR(256)                 NOT NULL,
        [BusinessId]      INT                           NOT NULL,
        [Token]           NVARCHAR(128)                 NOT NULL,
        [CreatedAtUtc]    DATETIME2                     NOT NULL,
        [ExpiresAtUtc]    DATETIME2                     NOT NULL,
        [IsUsed]          BIT                           NOT NULL  CONSTRAINT [DF_Invitation_IsUsed] DEFAULT (0),
        [CreatedByUserId] NVARCHAR(450)                 NOT NULL,

        CONSTRAINT [PK_Invitation] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Invitation_Token]
        ON [dbo].[Invitation] ([Token]);

    CREATE NONCLUSTERED INDEX [IX_Invitation_Email]
        ON [dbo].[Invitation] ([Email]);
END
GO

-- ============================================================================
-- Seed Data: SuperAdmin Role
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[AspNetRoles]
    WHERE [Id] = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890'
)
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES ('A1B2C3D4-E5F6-7890-ABCD-EF1234567890', 'SuperAdmin', 'SUPERADMIN', NEWID());
END
GO
