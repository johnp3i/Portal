/*
    Migration: 019_CreateAuditLogTable
    Description: Creates the [audit].AuditLog table — an append-only change tracking
                 record for all significant data changes across the platform. Uses bigint
                 for the primary key to accommodate high-volume audit entries. BusinessId
                 is nullable to support system-level events not tied to a specific tenant.

    Requirements: 9.1 - THE Portal_Database SHALL contain an [audit].AuditLog table
                         with columns: Id (PK, bigint identity), BusinessId (FK to
                         [portal].Business, nullable for system-level events), UserId
                         (nvarchar, nullable), Action (nvarchar, required), TableName
                         (nvarchar, required), RecordId (nvarchar, required), OldValues
                         (nvarchar(max), nullable), NewValues (nvarchar(max), nullable),
                         Timestamp (datetime2, required)
                 9.2 - THE Portal_Database SHALL use bigint for [audit].AuditLog.Id to
                         accommodate high-volume audit entries

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'audit'
      AND TABLE_NAME = 'AuditLog'
)
BEGIN
    CREATE TABLE [audit].[AuditLog]
    (
        [Id]            BIGINT          IDENTITY(1,1)  NOT NULL,
        [BusinessId]    INT                            NULL,
        [UserId]        NVARCHAR(450)                  NULL,
        [Action]        NVARCHAR(50)                   NOT NULL,
        [TableName]     NVARCHAR(200)                  NOT NULL,
        [RecordId]      NVARCHAR(50)                   NOT NULL,
        [OldValues]     NVARCHAR(MAX)                  NULL,
        [NewValues]     NVARCHAR(MAX)                  NULL,
        [Timestamp]     DATETIME2                      NOT NULL  CONSTRAINT [DF_AuditLog_Timestamp] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_AuditLog_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId'
      AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId]
        ON [audit].[AuditLog] ([BusinessId]);
END
GO
