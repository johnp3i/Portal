-- ============================================================
-- Migration 215: Convert [sales].[Meeting].ScheduledAtUtc from business-local to true UTC
-- ============================================================
-- Purpose: Historically the meeting scheduled time was stored as the user's LOCAL wall-clock
--          time (the datetime-local input value was saved verbatim) despite the column name
--          ScheduledAtUtc. The application now stores and reads true UTC (converting to/from the
--          business's local zone). This migration shifts every EXISTING meeting row from
--          business-local to UTC so old rows match the new convention.
--
--          Per-business, DST-aware conversion: interpret ScheduledAtUtc in the business's
--          Windows time zone (Business.TimeZoneId -> [notification].[TimeZone].WindowsId,
--          falling back to the platform default 'GTB Standard Time' when unset/unknown), then
--          convert to UTC via SQL Server AT TIME ZONE.
--
--          ONE-SHOT + IDEMPOTENT: guarded by a [dbo].[PlatformConfig] marker so it can never
--          double-shift, even if the script is run more than once.
-- Schema: [sales]
-- ============================================================

USE [Portal]
GO

DECLARE @MarkerKey NVARCHAR(256) = 'MeetingScheduledAtUtcTimezoneMigrated';
DECLARE @DefaultWindowsId NVARCHAR(100) = 'GTB Standard Time';

IF EXISTS (SELECT 1 FROM [dbo].[PlatformConfig] WHERE [Key] = @MarkerKey)
BEGIN
    PRINT 'Migration 215 already applied (marker present) — skipping meeting UTC conversion.';
END
ELSE
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Shift each meeting's local wall-clock time to UTC using the business's zone.
        -- AT TIME ZONE <tz> stamps the naive datetime as being in that zone (-> datetimeoffset),
        -- then AT TIME ZONE 'UTC' re-expresses it in UTC; CAST back to datetime drops the offset.
        UPDATE m
        SET m.[ScheduledAtUtc] =
            CAST(
                (m.[ScheduledAtUtc] AT TIME ZONE COALESCE(tz.[WindowsId], @DefaultWindowsId) AT TIME ZONE 'UTC')
                AS DATETIME)
        FROM [sales].[Meeting] AS m
        INNER JOIN [portal].[Business] AS b ON b.[Id] = m.[BusinessId]
        LEFT JOIN [notification].[TimeZone] AS tz ON tz.[Id] = b.[TimeZoneId];

        DECLARE @Rows INT = @@ROWCOUNT;

        -- Record the marker so this conversion never runs again.
        INSERT INTO [dbo].[PlatformConfig] ([Key], [Value], [Description])
        VALUES (
            @MarkerKey,
            CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126),
            'One-shot guard: existing [sales].[Meeting].ScheduledAtUtc rows were shifted from business-local to UTC by migration 215.');

        COMMIT TRANSACTION;
        PRINT 'Migration 215 applied — converted ' + CAST(@Rows AS NVARCHAR(10)) + ' meeting row(s) from business-local to UTC.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @Msg NVARCHAR(2048) = ERROR_MESSAGE();
        PRINT 'Migration 215 FAILED and was rolled back: ' + @Msg;
        THROW;
    END CATCH
END
GO
