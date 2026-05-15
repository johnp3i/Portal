/*
    Migration: 003_CreateMembershipSchema
    Description: Creates the [membership] schema in the Membership database.
                 Required before creating UserBusiness and UserBusinessPermission tables.

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'membership')
BEGIN
    EXEC('CREATE SCHEMA [membership]');
END
GO
