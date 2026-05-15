/*
    Migration: 001_CreateSchemas
    Description: Creates all 8 SQL Server schemas for the Portal database.
    Schemas: portal, customer, quotation, invoice, revenue, purchase, vat, audit

    Requirements: 11.1 - THE Portal_Database SHALL create the following SQL Server schemas:
                  portal, customer, quotation, invoice, revenue, purchase, vat, audit

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'portal')
BEGIN
    EXEC('CREATE SCHEMA [portal]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'customer')
BEGIN
    EXEC('CREATE SCHEMA [customer]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'quotation')
BEGIN
    EXEC('CREATE SCHEMA [quotation]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'invoice')
BEGIN
    EXEC('CREATE SCHEMA [invoice]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'revenue')
BEGIN
    EXEC('CREATE SCHEMA [revenue]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'purchase')
BEGIN
    EXEC('CREATE SCHEMA [purchase]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'vat')
BEGIN
    EXEC('CREATE SCHEMA [vat]');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA [audit]');
END
GO
