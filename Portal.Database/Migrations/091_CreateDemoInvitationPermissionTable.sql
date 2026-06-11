/*
    Migration: 091_CreateDemoInvitationPermissionTable
    Description: Creates the [portal].[DemoInvitationPermission] table to store
                 configured module permissions per demo invitation. Each row defines
                 an access level (full, readonly, none) for a specific module on an
                 invitation. A unique constraint ensures only one permission entry
                 per module per invitation.

    Requirements: 3.1 - THE Portal_Database SHALL contain [portal].[DemoInvitationPermission] with all required columns
                  3.2 - THE Portal_Database SHALL enforce a unique constraint on (DemoInvitationId, Module)
                  3.3 - THE Portal_Database SHALL enforce a check constraint on Module (allowed module list)
                  3.4 - THE Portal_Database SHALL enforce a check constraint on AccessLevel ('full','readonly','none')

    This script is idempotent — safe to run multiple times.
*/

-- =============================================================================
-- 1. Create [portal].[DemoInvitationPermission] table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[portal].[DemoInvitationPermission]')
      AND type = N'U'
)
BEGIN
    CREATE TABLE [portal].[DemoInvitationPermission]
    (
        [Id]                  INT            IDENTITY(1,1) NOT NULL,
        [DemoInvitationId]    INT            NOT NULL,
        [Module]              NVARCHAR(50)   NOT NULL,
        [AccessLevel]         NVARCHAR(20)   NOT NULL,
        [CreatedAtUtc]        DATETIME2      NOT NULL CONSTRAINT [DF_DemoInvitationPermission_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_DemoInvitationPermission] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DemoInvitationPermission_DemoInvitation] FOREIGN KEY ([DemoInvitationId])
            REFERENCES [portal].[DemoInvitation] ([Id]),
        CONSTRAINT [UQ_DemoInvitationPermission_Module] UNIQUE ([DemoInvitationId], [Module]),
        CONSTRAINT [CK_DemoInvitationPermission_Module] CHECK ([Module] IN ('customer', 'quotation', 'invoice', 'revenue', 'purchase', 'vat', 'credit', 'audit', 'products')),
        CONSTRAINT [CK_DemoInvitationPermission_AccessLevel] CHECK ([AccessLevel] IN ('full', 'readonly', 'none'))
    );
END
GO
