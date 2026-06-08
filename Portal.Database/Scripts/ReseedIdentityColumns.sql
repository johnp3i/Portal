/*
    Script: ReseedIdentityColumns
    Purpose: Automatically discovers and reseeds ALL identity columns in both
             Portal and Portal.Membership databases so that the next inserted row
             gets MAX(current_identity_column) + 1, closing gaps left by deleted test data.
             Does NOT modify any existing data.

    Usage: Run against each database (or run the whole script if cross-database USE is allowed).
    Safe to run multiple times (idempotent).
*/

-- =============================================================================
-- PORTAL DATABASE
-- =============================================================================
USE [Portal];
GO

DECLARE @schema NVARCHAR(128);
DECLARE @table NVARCHAR(128);
DECLARE @column NVARCHAR(128);
DECLARE @sql NVARCHAR(MAX);

DECLARE identity_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        SCHEMA_NAME(t.schema_id) AS SchemaName,
        t.name AS TableName,
        c.name AS ColumnName
    FROM sys.tables t
    INNER JOIN sys.columns c ON t.object_id = c.object_id
    WHERE c.is_identity = 1
    ORDER BY SchemaName, TableName;

OPEN identity_cursor;
FETCH NEXT FROM identity_cursor INTO @schema, @table, @column;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
        DECLARE @maxVal BIGINT;
        SELECT @maxVal = ISNULL(MAX([' + @column + N']), 0) FROM [' + @schema + N'].[' + @table + N'];
        DBCC CHECKIDENT (''[' + @schema + N'].[' + @table + N']'', RESEED, @maxVal);
        PRINT ''Reseeded [' + @schema + N'].[' + @table + N'] to '' + CAST(@maxVal AS NVARCHAR(20));';

    EXEC sp_executesql @sql;

    FETCH NEXT FROM identity_cursor INTO @schema, @table, @column;
END

CLOSE identity_cursor;
DEALLOCATE identity_cursor;

PRINT '';
PRINT 'Portal database: All identity columns reseeded.';
GO


-- =============================================================================
-- PORTAL.MEMBERSHIP DATABASE
-- =============================================================================
USE [Portal.Membership];
GO

DECLARE @schema NVARCHAR(128);
DECLARE @table NVARCHAR(128);
DECLARE @column NVARCHAR(128);
DECLARE @sql NVARCHAR(MAX);

DECLARE identity_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        SCHEMA_NAME(t.schema_id) AS SchemaName,
        t.name AS TableName,
        c.name AS ColumnName
    FROM sys.tables t
    INNER JOIN sys.columns c ON t.object_id = c.object_id
    WHERE c.is_identity = 1
    ORDER BY SchemaName, TableName;

OPEN identity_cursor;
FETCH NEXT FROM identity_cursor INTO @schema, @table, @column;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
        DECLARE @maxVal BIGINT;
        SELECT @maxVal = ISNULL(MAX([' + @column + N']), 0) FROM [' + @schema + N'].[' + @table + N'];
        DBCC CHECKIDENT (''[' + @schema + N'].[' + @table + N']'', RESEED, @maxVal);
        PRINT ''Reseeded [' + @schema + N'].[' + @table + N'] to '' + CAST(@maxVal AS NVARCHAR(20));';

    EXEC sp_executesql @sql;

    FETCH NEXT FROM identity_cursor INTO @schema, @table, @column;
END

CLOSE identity_cursor;
DEALLOCATE identity_cursor;

PRINT '';
PRINT 'Portal.Membership database: All identity columns reseeded.';
GO
