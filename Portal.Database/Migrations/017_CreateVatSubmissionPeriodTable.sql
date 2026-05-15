/*
    Migration: 017_CreateVatSubmissionPeriodTable
    Description: Creates the [vat].VatSubmissionPeriod table — a calculated time range
                 representing a single VAT reporting period for a Business tenant.
                 Scoped to a Business tenant with a foreign key to [portal].Business.

    Requirements: 8.2 - THE Portal_Database SHALL contain a [vat].VatSubmissionPeriod
                         table with columns: Id (PK, int identity), BusinessId (FK to
                         [portal].Business), PeriodStartDate (date), PeriodEndDate (date),
                         PeriodLabel (nvarchar), CreatedAtUtc (datetime2)
                 8.4 - THE Portal_Database SHALL enforce a unique constraint on
                         [vat].VatSubmissionPeriod (BusinessId, PeriodStartDate) to
                         prevent duplicate periods

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'vat'
      AND TABLE_NAME = 'VatSubmissionPeriod'
)
BEGIN
    CREATE TABLE [vat].[VatSubmissionPeriod]
    (
        [Id]                INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]        INT                           NOT NULL,
        [PeriodStartDate]   DATE                          NOT NULL,
        [PeriodEndDate]     DATE                          NOT NULL,
        [PeriodLabel]       NVARCHAR(100)                 NOT NULL,
        [CreatedAtUtc]      DATETIME2                     NOT NULL  CONSTRAINT [DF_VatSubmissionPeriod_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_VatSubmissionPeriod] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_VatSubmissionPeriod_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_VatSubmissionPeriod_BusinessId'
      AND [object_id] = OBJECT_ID('[vat].[VatSubmissionPeriod]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VatSubmissionPeriod_BusinessId]
        ON [vat].[VatSubmissionPeriod] ([BusinessId]);
END
GO

-- Unique constraint on (BusinessId, PeriodStartDate) to prevent duplicate periods per tenant
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_VatSubmissionPeriod_BusinessId_PeriodStartDate'
      AND [object_id] = OBJECT_ID('[vat].[VatSubmissionPeriod]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_VatSubmissionPeriod_BusinessId_PeriodStartDate]
        ON [vat].[VatSubmissionPeriod] ([BusinessId], [PeriodStartDate]);
END
GO
