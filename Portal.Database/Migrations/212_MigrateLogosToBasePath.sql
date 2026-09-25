-- ============================================================
-- Migration 212: Migrate business logos to the private storage root
-- ============================================================
-- Purpose: Business logos previously lived in the public web root
--          (wwwroot/uploads/logos/{file}) and were served as static files.
--          They now live under the private file-storage root at
--          {FileStorage:BasePath}/{BusinessId}/logos/{file} and are served
--          via the public-but-unguessable route /logo/{BusinessId}/{file}
--          (LogoImageController). This is redeploy-safe, tenant-isolated,
--          and shares the single backup root with all other uploads.
--
--          This script rewrites [portal].[BusinessLogo].[PublicUrl] from
--          '/uploads/logos/{file}' to '/logo/{BusinessId}/{file}'.
--
-- ⚠️ FILE MOVE REQUIRED FIRST (bytes are on disk, not in the DB):
--    Before or alongside running this script, physically move the logo files.
--    For each row, copy:
--        {WebRoot}/wwwroot/uploads/logos/{FileName}
--    to:
--        {FileStorage:BasePath}/{BusinessId}/logos/{FileName}
--    See docs/File_Storage_Overview.md → "Logo migration runbook" for a
--    PowerShell one-liner that does this from the DB rows.
--
-- Idempotent: only rewrites rows still pointing at '/uploads/logos/'.
-- ============================================================

USE [Portal]
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'portal' AND TABLE_NAME = 'BusinessLogo')
BEGIN
    UPDATE [portal].[BusinessLogo]
    SET [PublicUrl] = '/logo/' + CAST([BusinessId] AS NVARCHAR(20)) + '/' + [FileName]
    WHERE [PublicUrl] LIKE '/uploads/logos/%';

    PRINT 'Rewrote ' + CAST(@@ROWCOUNT AS NVARCHAR(20)) + ' BusinessLogo.PublicUrl value(s) to the /logo/{BusinessId}/{file} route.';
END
ELSE
BEGIN
    PRINT '[portal].[BusinessLogo] does not exist — nothing to migrate.';
END
GO
