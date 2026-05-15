/*
    Migration: 018_CreateVatSubmissionTable
    Description: Creates the [vat].VatSubmission table — a VAT return submission record
                 for a specific period, scoped to a Business tenant with foreign keys
                 to [portal].Business and [vat].VatSubmissionPeriod.

    Requirements: 8.1 - THE Portal_Database SHALL contain a [vat].VatSubmission table
                         with columns: Id (PK, int identity), BusinessId (FK to
                         [portal].Business), VatSubmissionPeriodId (FK to
                         [vat].VatSubmissionPeriod), TotalOutputVat (decimal(18,2)),
                         TotalInputVat (decimal(18,2)), NetVatPayable (decimal(18,2)),
                         IsSubmitted (bit, default 0), SubmittedAtUtc (datetime2, nullable),
                         Notes (nvarchar(max), nullable), CreatedAtUtc (datetime2)
                 8.5 - THE Portal_Database SHALL enforce a unique constraint on
                         [vat].VatSubmission (BusinessId, VatSubmissionPeriodId) to
                         prevent duplicate submissions per period

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'vat'
      AND TABLE_NAME = 'VatSubmission'
)
BEGIN
    CREATE TABLE [vat].[VatSubmission]
    (
        [Id]                        INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                INT                           NOT NULL,
        [VatSubmissionPeriodId]     INT                           NOT NULL,
        [TotalOutputVat]            DECIMAL(18,2)                 NOT NULL,
        [TotalInputVat]             DECIMAL(18,2)                 NOT NULL,
        [NetVatPayable]             DECIMAL(18,2)                 NOT NULL,
        [IsSubmitted]               BIT                           NOT NULL  CONSTRAINT [DF_VatSubmission_IsSubmitted] DEFAULT (0),
        [SubmittedAtUtc]            DATETIME2                     NULL,
        [Notes]                     NVARCHAR(MAX)                 NULL,
        [CreatedAtUtc]              DATETIME2                     NOT NULL  CONSTRAINT [DF_VatSubmission_CreatedAtUtc] DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_VatSubmission] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_VatSubmission_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [FK_VatSubmission_VatSubmissionPeriod] FOREIGN KEY ([VatSubmissionPeriodId]) REFERENCES [vat].[VatSubmissionPeriod] ([Id])
    );
END
GO

-- Non-clustered index on BusinessId for tenant-filtered query optimisation
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_VatSubmission_BusinessId'
      AND [object_id] = OBJECT_ID('[vat].[VatSubmission]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VatSubmission_BusinessId]
        ON [vat].[VatSubmission] ([BusinessId]);
END
GO

-- Unique constraint on (BusinessId, VatSubmissionPeriodId) to prevent duplicate submissions per period
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'UX_VatSubmission_BusinessId_VatSubmissionPeriodId'
      AND [object_id] = OBJECT_ID('[vat].[VatSubmission]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_VatSubmission_BusinessId_VatSubmissionPeriodId]
        ON [vat].[VatSubmission] ([BusinessId], [VatSubmissionPeriodId]);
END
GO
