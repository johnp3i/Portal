/*
    Migration: 001_CreateLoggingDatabase
    Description: Creates the Portal.Logging database and the [dbo].[Logs] table for
                 structured Serilog log persistence. The database is separate from the
                 Portal and Membership databases to isolate high-volume log writes from
                 transactional business data. Uses BIGINT for the primary key to
                 accommodate high-volume log entries over the application lifetime.

    Requirements: 1.1 - THE Logging_Database SHALL be created as a SQL Server database
                         named Portal.Logging on the same server instance
                 1.2 - THE Logging_Database SHALL contain a [dbo].[Logs] table with
                         standard Serilog columns
                 1.3 - THE Log_Table SHALL include additional columns for structured
                         properties (CorrelationId, UserId, BusinessId, SourceContext,
                         RequestPath, MachineName)
                 1.4 - THE Log_Table SHALL use BIGINT for the Id column
                 1.5 - THE Log_Table SHALL include a non-clustered index on TimeStamp
                 1.6 - THE Log_Table SHALL include a non-clustered index on Level
                 1.7 - THE Log_Table SHALL include a non-clustered index on BusinessId
                 7.1 - THE migration script SHALL create the Portal.Logging database
                 7.2 - THE migration script SHALL create the [dbo].[Logs] table
                 7.3 - THE migration script SHALL create the non-clustered indexes
                 7.4 - THE migration script SHALL be idempotent
                 7.5 - THE migration script SHALL follow existing naming conventions

    This script is idempotent — safe to run multiple times without error or data loss.
*/

-- Create the Portal.Logging database if it does not exist
IF NOT EXISTS (
    SELECT 1
    FROM sys.databases
    WHERE [name] = 'Portal.Logging'
)
BEGIN
    CREATE DATABASE [Portal.Logging];
END
GO

USE [Portal.Logging];
GO

-- Create the [dbo].[Logs] table if it does not exist
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Logs'
)
BEGIN
    CREATE TABLE [dbo].[Logs]
    (
        [Id]              BIGINT          IDENTITY(1,1)  NOT NULL,
        [Message]         NVARCHAR(MAX)                  NOT NULL,
        [MessageTemplate] NVARCHAR(MAX)                  NULL,
        [Level]           NVARCHAR(128)                  NOT NULL,
        [TimeStamp]       DATETIME2                      NOT NULL,
        [Exception]       NVARCHAR(MAX)                  NULL,
        [CorrelationId]   NVARCHAR(128)                  NULL,
        [UserId]          NVARCHAR(450)                  NULL,
        [BusinessId]      INT                            NULL,
        [SourceContext]   NVARCHAR(512)                  NULL,
        [RequestPath]     NVARCHAR(512)                  NULL,
        [MachineName]     NVARCHAR(128)                  NULL,

        CONSTRAINT [PK_Logs] PRIMARY KEY CLUSTERED ([Id])
    );
END
GO

-- Non-clustered index on TimeStamp for efficient time-range queries
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Logs_TimeStamp'
      AND [object_id] = OBJECT_ID('[dbo].[Logs]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Logs_TimeStamp]
        ON [dbo].[Logs] ([TimeStamp]);
END
GO

-- Non-clustered index on Level for filtering by severity
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Logs_Level'
      AND [object_id] = OBJECT_ID('[dbo].[Logs]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Logs_Level]
        ON [dbo].[Logs] ([Level]);
END
GO

-- Non-clustered index on BusinessId for tenant-scoped log queries
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Logs_BusinessId'
      AND [object_id] = OBJECT_ID('[dbo].[Logs]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Logs_BusinessId]
        ON [dbo].[Logs] ([BusinessId]);
END
GO
