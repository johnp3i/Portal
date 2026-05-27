/*
    Migration: 060_AddAuditLogQueryIndexes
    Description: Adds two composite non-clustered indexes to [audit].[AuditLog] to
                 support the filtered, paginated query patterns used by the Audit Log
                 Viewer. Both indexes are created idempotently (IF NOT EXISTS guards).

    Indexes added:
      IX_AuditLog_BusinessId_Timestamp — supports the most common query pattern:
        WHERE BusinessId = @b AND Timestamp BETWEEN @from AND @to ORDER BY Timestamp DESC

      IX_AuditLog_BusinessId_Action — covering index for action-filtered queries,
        includes Timestamp, TableName, UserId, RecordId to avoid key lookups.

    Requirements: 2.1, 2.7

    This script is idempotent — safe to run multiple times.
*/

-- Composite index: BusinessId + Timestamp DESC
-- Supports tenant-scoped queries ordered by recency (the default sort order)
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId_Timestamp'
      AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Timestamp]
        ON [audit].[AuditLog] ([BusinessId] ASC, [Timestamp] DESC);
END
GO

-- Covering index: BusinessId + Action, includes query columns to avoid key lookups
-- Supports action-filtered queries without returning to the clustered index
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AuditLog_BusinessId_Action'
      AND [object_id] = OBJECT_ID('[audit].[AuditLog]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_BusinessId_Action]
        ON [audit].[AuditLog] ([BusinessId], [Action])
        INCLUDE ([Timestamp], [TableName], [UserId], [RecordId]);
END
GO
