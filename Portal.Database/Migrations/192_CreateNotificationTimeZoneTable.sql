-- ============================================================
-- Migration 192: Create [notification].[TimeZone] reference table + seed
-- ============================================================
-- Purpose: Supported time zones for per-business working-hours
--          scheduling. Static seed reference data (exempt from the
--          CreatedAtUtc audit column per the SQL schema convention).
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'notification' AND TABLE_NAME = 'TimeZone'
)
BEGIN
    CREATE TABLE [notification].[TimeZone]
    (
        [Id]          INT           NOT NULL,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [WindowsId]   NVARCHAR(100) NOT NULL,
        [IanaId]      NVARCHAR(100) NOT NULL,

        CONSTRAINT [PK_NotificationTimeZone] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_NotificationTimeZone_IanaId] UNIQUE ([IanaId])
    );
    PRINT 'Created [notification].[TimeZone] table.';
END
ELSE
BEGIN
    PRINT '[notification].[TimeZone] already exists.';
END
GO

-- Seed a practical set of time zones (idempotent, explicit Ids)
MERGE [notification].[TimeZone] AS target
USING (VALUES
    (1, 'Cyprus (EET/EEST)',            'GTB Standard Time',    'Europe/Nicosia'),
    (2, 'United Kingdom (GMT/BST)',     'GMT Standard Time',    'Europe/London'),
    (3, 'Ireland (GMT/IST)',            'GMT Standard Time',    'Europe/Dublin'),
    (4, 'Central Europe (CET/CEST)',    'Central European Standard Time', 'Europe/Paris'),
    (5, 'Greece (EET/EEST)',            'GTB Standard Time',    'Europe/Athens'),
    (6, 'UTC',                          'UTC',                  'Etc/UTC')
) AS source ([Id], [DisplayName], [WindowsId], [IanaId])
ON target.[Id] = source.[Id]
WHEN NOT MATCHED THEN
    INSERT ([Id], [DisplayName], [WindowsId], [IanaId])
    VALUES (source.[Id], source.[DisplayName], source.[WindowsId], source.[IanaId]);
GO

PRINT 'Seeded [notification].[TimeZone] reference data.';
GO
