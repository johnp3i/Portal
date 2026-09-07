-- ============================================================
-- Migration 191: Create [notification] schema
-- ============================================================
-- Purpose: Bounded context for the Digital Assistants notification
--          spine (transactional outbox, assistant registry/config,
--          per-service opt-out, and timezone reference data).
-- Schema: [notification]
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'notification')
BEGIN
    EXEC('CREATE SCHEMA [notification]');
    PRINT 'Created [notification] schema.';
END
ELSE
BEGIN
    PRINT '[notification] schema already exists.';
END
GO
