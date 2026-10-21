/*
    Migration: 228_CreateActivityTypeLookupAndRenameColumn
    Description: Converts the prospect-activity "type" from a hardcoded in-code enumeration into a
                 proper reference table, following the schema naming convention (reference table
                 exposes [Id]; dependent table exposes [<TableName>Id]).

                 1. Creates reference table [sales].[ActivityType] and seeds the existing eight types
                    with the SAME ids already stored in the data (1 Research .. 8 Other), so the
                    rename is a pure 1:1 remap with no data transformation.
                 2. Renames [sales].[ProspectActivity].[ActivityType] -> [ActivityTypeId].
                 3. Adds the FK [ProspectActivity].[ActivityTypeId] -> [ActivityType].[Id].

                 The lookup lets new activity types be added by inserting rows (no code change needed
                 to surface them in the dropdown, filters, and names).

                 [sales].[ActivityType] is a static seed/reference table, so it is exempt from the
                 mandatory CreatedAtUtc audit column (per the schema-design steering exception).

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

-- 1. Reference table ---------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'sales' AND t.name = N'ActivityType'
)
BEGIN
    CREATE TABLE [sales].[ActivityType] (
        [Id]        TINYINT        NOT NULL,
        [Name]      NVARCHAR(50)   NOT NULL,
        [SortOrder] INT            NOT NULL CONSTRAINT [DF_ActivityType_SortOrder] DEFAULT (0),
        [IsActive]  BIT            NOT NULL CONSTRAINT [DF_ActivityType_IsActive] DEFAULT (1),

        CONSTRAINT [PK_ActivityType] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Seed the existing eight types with their current ids (idempotent per-row).
MERGE [sales].[ActivityType] AS target
USING (VALUES
    (CAST(1 AS TINYINT), N'Research', 10),
    (CAST(2 AS TINYINT), N'Call',     20),
    (CAST(3 AS TINYINT), N'Email',    30),
    (CAST(4 AS TINYINT), N'Meeting',  40),
    (CAST(5 AS TINYINT), N'Demo',     50),
    (CAST(6 AS TINYINT), N'Note',     60),
    (CAST(7 AS TINYINT), N'Social',   70),
    (CAST(8 AS TINYINT), N'Other',    80)
) AS source ([Id], [Name], [SortOrder])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [Name], [SortOrder], [IsActive])
    VALUES (source.[Id], source.[Name], source.[SortOrder], 1);
GO

-- 2. Rename the dependent column ActivityType -> ActivityTypeId ---------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'ActivityType'
)
AND NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[sales].[ProspectActivity]') AND name = N'ActivityTypeId'
)
BEGIN
    EXEC sp_rename N'[sales].[ProspectActivity].[ActivityType]', N'ActivityTypeId', N'COLUMN';
END
GO

-- 3. Foreign key -------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_ProspectActivity_ActivityType'
      AND parent_object_id = OBJECT_ID(N'[sales].[ProspectActivity]')
)
BEGIN
    ALTER TABLE [sales].[ProspectActivity] WITH CHECK
        ADD CONSTRAINT [FK_ProspectActivity_ActivityType]
        FOREIGN KEY ([ActivityTypeId]) REFERENCES [sales].[ActivityType] ([Id]);
END
GO
