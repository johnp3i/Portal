-- ============================================================
-- Migration 131: Create Sales Schema
-- ============================================================
-- Purpose: Creates the [sales] schema for the Sales Pipeline module.
--          All pipeline entities (contacts, leads, meetings, responses,
--          products, templates) live under this schema.
-- ============================================================

USE [Portal]
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'sales')
BEGIN
    EXEC('CREATE SCHEMA [sales]');
    PRINT 'Created [sales] schema.';
END
ELSE
BEGIN
    PRINT '[sales] schema already exists.';
END
GO
