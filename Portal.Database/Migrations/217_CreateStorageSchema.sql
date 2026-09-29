/*
    Migration: 217_CreateStorageSchema
    Description: Creates the [Storage] schema for the orphaned-file cleanup module (Phase 4b).
                 Groups the cleanup candidate + deletion-log tables under their own namespace,
                 separate from [dbo]/[document]/[compliance]/[portal].

    This script is idempotent — safe to run multiple times.
*/

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Storage')
BEGIN
    EXEC('CREATE SCHEMA [Storage]');
    PRINT 'Created schema [Storage].';
END
ELSE
    PRINT 'Schema [Storage] already exists.';
GO
