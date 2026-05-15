/*
    Migration: 003_CreateBusinessProfileTable
    Description: Creates the [portal].BusinessProfile table — configuration record holding
                 company registration, VAT details, and contact information for a Business.

    Requirements: 2.1 - THE Portal_Database SHALL contain a [portal].BusinessProfile table
                  2.2 - THE Portal_Database SHALL enforce a one-to-one relationship between
                         [portal].BusinessProfile and [portal].Business via a unique constraint on BusinessId
                  2.3 - WHEN VatPeriodLengthInMonths is stored, THE Portal_Database SHALL accept
                         values of 1, 2, 3, 4, 6, or 12 only via a CHECK constraint

    This script is idempotent — safe to run multiple times.
*/

IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'portal'
      AND TABLE_NAME = 'BusinessProfile'
)
BEGIN
    CREATE TABLE [portal].[BusinessProfile]
    (
        [Id]                        INT            IDENTITY(1,1)  NOT NULL,
        [BusinessId]                INT                           NOT NULL,
        [CompanyRegistrationNumber] NVARCHAR(50)                  NOT NULL,
        [VatRegistrationNumber]     NVARCHAR(50)                  NOT NULL,
        [VatRegistrationDate]       DATE                          NOT NULL,
        [VatPeriodLengthInMonths]   INT                           NOT NULL,
        [AddressLine1]              NVARCHAR(200)                 NOT NULL,
        [AddressLine2]              NVARCHAR(200)                 NULL,
        [City]                      NVARCHAR(100)                 NOT NULL,
        [PostalCode]                NVARCHAR(20)                  NOT NULL,
        [Country]                   NVARCHAR(100)                 NOT NULL,
        [TelephoneNumber]           NVARCHAR(30)                  NULL,
        [MobileNumber]              NVARCHAR(30)                  NULL,
        [Email]                     NVARCHAR(200)                 NOT NULL,

        CONSTRAINT [PK_BusinessProfile] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_BusinessProfile_Business] FOREIGN KEY ([BusinessId]) REFERENCES [portal].[Business] ([Id]),
        CONSTRAINT [UQ_BusinessProfile_BusinessId] UNIQUE ([BusinessId]),
        CONSTRAINT [CK_BusinessProfile_VatPeriodLengthInMonths] CHECK ([VatPeriodLengthInMonths] IN (1, 2, 3, 4, 6, 12))
    );
END
GO
