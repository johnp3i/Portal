/*
    Migration: 051_CreateProductSchema
    Description: Creates the [product] schema for the Product Catalog module.

    Requirements: 1.1 - THE Portal_Database SHALL create a [product] schema
                  if it does not already exist, prior to creating any tables within that schema.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'product')
BEGIN
    EXEC('CREATE SCHEMA [product]');
END
GO
