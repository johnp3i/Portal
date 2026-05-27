USE [Portal];
GO

/*
    Migration: 062_CreateCreditSchema
    Description: Creates the [credit] schema for the Credit Note module.
    Schema: credit

    Requirements: 1.12 - THE Credit_Note_Service SHALL scope all credit note records
                  to the current user's BusinessId for tenant isolation.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'credit')
BEGIN
    EXEC('CREATE SCHEMA [credit]');
END
GO
